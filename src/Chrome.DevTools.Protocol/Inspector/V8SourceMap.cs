using System.Text.Json.Nodes;

namespace Chrome.DevTools.Protocol.Inspector;

/// <summary>Parses source map revision 3 mappings used by V8/Chrome DevTools.</summary>
public sealed class V8SourceMap
{
    private readonly IReadOnlyList<V8SourceMapEntry> _entries;

    private V8SourceMap(
        IReadOnlyList<string> sources,
        IReadOnlyList<string?> sourcesContent,
        IReadOnlyList<string> names,
        IReadOnlyList<V8SourceMapEntry> entries,
        string sourceRoot)
    {
        Sources = sources;
        SourcesContent = sourcesContent;
        Names = names;
        _entries = entries;
        SourceRoot = sourceRoot;
    }

    public IReadOnlyList<string> Sources { get; }
    public IReadOnlyList<string?> SourcesContent { get; }
    public IReadOnlyList<string> Names { get; }
    public IReadOnlyList<V8SourceMapEntry> Entries => _entries;
    public string SourceRoot { get; }

    public static V8SourceMap Parse(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new FormatException("Source map must be a JSON object.");
        if (root["version"]?.GetValue<int>() != 3) throw new NotSupportedException("Only source map revision 3 is supported.");

        var sources = ReadStrings(root["sources"] as JsonArray);
        var names = ReadStrings(root["names"] as JsonArray);
        var content = root["sourcesContent"] is JsonArray contentArray
            ? contentArray.Select(node => node?.GetValue<string>()).ToArray()
            : Array.Empty<string?>();
        var mappings = root["mappings"]?.GetValue<string>() ?? "";
        return new V8SourceMap(
            sources,
            content,
            names,
            ParseMappings(mappings, sources.Count, names.Count),
            root["sourceRoot"]?.GetValue<string>() ?? "");
    }

    public V8SourceMapEntry? FindOriginalLocation(int generatedLine, int generatedColumn)
    {
        V8SourceMapEntry? best = null;
        foreach (var entry in _entries)
        {
            if (entry.GeneratedLine > generatedLine) break;
            if (entry.GeneratedLine == generatedLine && entry.GeneratedColumn <= generatedColumn) best = entry;
        }
        return best;
    }

    public V8SourceMapEntry? FindGeneratedLocation(int sourceIndex, int originalLine, int originalColumn = 0) =>
        _entries
            .Where(entry => entry.SourceIndex == sourceIndex &&
                (entry.OriginalLine > originalLine || entry.OriginalLine == originalLine && entry.OriginalColumn >= originalColumn))
            .OrderBy(entry => Math.Abs(entry.OriginalLine - originalLine))
            .ThenBy(entry => Math.Abs(entry.OriginalColumn - originalColumn))
            .ThenBy(entry => entry.GeneratedLine)
            .ThenBy(entry => entry.GeneratedColumn)
            .FirstOrDefault()
        ?? _entries.LastOrDefault(entry => entry.SourceIndex == sourceIndex && entry.OriginalLine <= originalLine);

    private static IReadOnlyList<string> ReadStrings(JsonArray? values) =>
        values?.Select(value => value?.GetValue<string>() ?? "").ToArray() ?? Array.Empty<string>();

    private static IReadOnlyList<V8SourceMapEntry> ParseMappings(string mappings, int sourceCount, int nameCount)
    {
        var result = new List<V8SourceMapEntry>();
        var sourceIndex = 0;
        var originalLine = 0;
        var originalColumn = 0;
        var nameIndex = 0;
        var generatedLine = 0;

        foreach (var line in mappings.Split(';'))
        {
            var generatedColumn = 0;
            foreach (var segment in line.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var values = DecodeVlqSegment(segment);
                if (values.Count == 0) continue;
                generatedColumn += values[0];
                if (values.Count < 4) continue;

                sourceIndex += values[1];
                originalLine += values[2];
                originalColumn += values[3];
                int? mappedName = null;
                if (values.Count >= 5)
                {
                    nameIndex += values[4];
                    if (nameIndex >= 0 && nameIndex < nameCount) mappedName = nameIndex;
                }

                if (sourceIndex >= 0 && sourceIndex < sourceCount)
                {
                    result.Add(new V8SourceMapEntry(
                        generatedLine, generatedColumn, sourceIndex, originalLine, originalColumn, mappedName));
                }
            }
            generatedLine++;
        }
        return result;
    }

    private static List<int> DecodeVlqSegment(string segment)
    {
        var result = new List<int>();
        var value = 0;
        var shift = 0;
        foreach (var character in segment)
        {
            var digit = DecodeBase64(character);
            var continuation = (digit & 32) != 0;
            digit &= 31;
            value += digit << shift;
            if (continuation)
            {
                shift += 5;
                continue;
            }

            var negative = (value & 1) == 1;
            value >>= 1;
            result.Add(negative ? -value : value);
            value = 0;
            shift = 0;
        }
        if (shift != 0) throw new FormatException("Incomplete source-map VLQ segment.");
        return result;
    }

    private static int DecodeBase64(char character) => character switch
    {
        >= 'A' and <= 'Z' => character - 'A',
        >= 'a' and <= 'z' => character - 'a' + 26,
        >= '0' and <= '9' => character - '0' + 52,
        '+' => 62,
        '/' => 63,
        _ => throw new FormatException($"Invalid source-map base64 character '{character}'.")
    };
}

public sealed record V8SourceMapEntry(
    int GeneratedLine,
    int GeneratedColumn,
    int SourceIndex,
    int OriginalLine,
    int OriginalColumn,
    int? NameIndex);
