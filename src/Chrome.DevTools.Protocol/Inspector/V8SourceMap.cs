using System.Text.Json.Nodes;

namespace Chrome.DevTools.Protocol.Inspector;

/// <summary>Parses regular and indexed source map revision 3 documents used by V8/Chrome DevTools.</summary>
public sealed class V8SourceMap
{
    private readonly IReadOnlyList<V8SourceMapEntry> _entries;
    private readonly IReadOnlyList<string> _sourceRoots;
    private readonly IReadOnlyList<Uri?> _sourceMapUris;
    private readonly IReadOnlySet<int> _ignoredSourceIndexes;
    private readonly bool _isIndexed;

    private V8SourceMap(
        IReadOnlyList<string> sources,
        IReadOnlyList<string?> sourcesContent,
        IReadOnlyList<string> names,
        IReadOnlyList<V8SourceMapEntry> entries,
        IReadOnlyList<string> sourceRoots,
        IReadOnlyList<Uri?> sourceMapUris,
        IReadOnlySet<int> ignoredSourceIndexes,
        string file,
        string sourceRoot,
        bool isIndexed)
    {
        Sources = sources;
        SourcesContent = sourcesContent;
        Names = names;
        _entries = entries;
        _sourceRoots = sourceRoots;
        _sourceMapUris = sourceMapUris;
        _ignoredSourceIndexes = ignoredSourceIndexes;
        File = file;
        SourceRoot = sourceRoot;
        _isIndexed = isIndexed;
    }

    public IReadOnlyList<string> Sources { get; }
    public IReadOnlyList<string?> SourcesContent { get; }
    public IReadOnlyList<string> Names { get; }
    public IReadOnlyList<V8SourceMapEntry> Entries => _entries;
    public IReadOnlySet<int> IgnoredSourceIndexes => _ignoredSourceIndexes;
    public string File { get; }
    public string SourceRoot { get; }
    public bool IsIndexed => _isIndexed;

    public static V8SourceMap Parse(string json, Uri? sourceMapUri = null)
    {
        var root = ParseRoot(json);
        return ParseObject(root, sourceMapUri);
    }

    public static async Task<V8SourceMap> ParseAsync(
        string json,
        Uri? sourceMapUri,
        Func<Uri, CancellationToken, Task<string>> sectionLoader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sectionLoader);
        var root = ParseRoot(json);
        return await ParseObjectAsync(root, sourceMapUri, sectionLoader, cancellationToken).ConfigureAwait(false);
    }

    public bool IsIgnoredSource(int sourceIndex) => _ignoredSourceIndexes.Contains(sourceIndex);

    /// <summary>
    /// Moves a compiler-emitted source to the requested stable index while preserving every
    /// mapping, source root, map URI, ignored-source flag, and the source displaced by the move.
    /// This lets live-edit regenerators keep editor source indexes stable when a bundler orders
    /// dependencies before its entry point.
    /// </summary>
    public V8SourceMap RemapSourceIndex(
        int currentSourceIndex,
        int targetSourceIndex,
        string source,
        string? sourceContent)
    {
        if (currentSourceIndex < 0 || currentSourceIndex >= Sources.Count)
            throw new ArgumentOutOfRangeException(nameof(currentSourceIndex));
        if (targetSourceIndex < 0 || targetSourceIndex >= Sources.Count)
            throw new ArgumentOutOfRangeException(nameof(targetSourceIndex));
        ArgumentNullException.ThrowIfNull(source);

        var sources = Sources.ToArray();
        var content = SourcesContent.ToArray();
        var roots = _sourceRoots.ToArray();
        var uris = _sourceMapUris.ToArray();
        Swap(sources, currentSourceIndex, targetSourceIndex);
        Swap(content, currentSourceIndex, targetSourceIndex);
        Swap(roots, currentSourceIndex, targetSourceIndex);
        Swap(uris, currentSourceIndex, targetSourceIndex);
        sources[targetSourceIndex] = source;
        content[targetSourceIndex] = sourceContent;

        var entries = _entries.Select(entry => entry with
        {
            SourceIndex = entry.SourceIndex == currentSourceIndex
                ? targetSourceIndex
                : entry.SourceIndex == targetSourceIndex
                    ? currentSourceIndex
                    : entry.SourceIndex
        }).ToArray();
        var ignored = _ignoredSourceIndexes.Select(index => index == currentSourceIndex
            ? targetSourceIndex
            : index == targetSourceIndex
                ? currentSourceIndex
                : index).ToHashSet();
        return new V8SourceMap(
            sources,
            content,
            Names,
            entries,
            roots,
            uris,
            ignored,
            File,
            SourceRoot,
            _isIndexed);
    }

    public string ResolveSourceUrl(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= Sources.Count) return "";
        var source = Sources[sourceIndex];
        if (Uri.TryCreate(source, UriKind.Absolute, out var sourceUri)) return sourceUri.ToString();

        var sourceRoot = sourceIndex < _sourceRoots.Count ? _sourceRoots[sourceIndex] : "";
        var rootedSource = string.IsNullOrWhiteSpace(sourceRoot)
            ? source
            : sourceRoot.TrimEnd('/') + "/" + source.TrimStart('/');
        if (Uri.TryCreate(rootedSource, UriKind.Absolute, out var rootedUri)) return rootedUri.ToString();

        var mapUri = sourceIndex < _sourceMapUris.Count ? _sourceMapUris[sourceIndex] : null;
        return mapUri is not null && Uri.TryCreate(mapUri, rootedSource, out var resolved)
            ? resolved.ToString()
            : rootedSource;
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

    public V8SourceMapEntry? FindGeneratedLocation(int sourceIndex, int originalLine, int originalColumn = 0)
    {
        var sourceEntries = _entries.Where(entry => entry.SourceIndex == sourceIndex).ToArray();
        return sourceEntries
            .Where(entry => entry.OriginalLine == originalLine && entry.OriginalColumn >= originalColumn)
            .OrderBy(entry => entry.OriginalColumn)
            .ThenBy(entry => entry.GeneratedLine)
            .ThenBy(entry => entry.GeneratedColumn)
            .FirstOrDefault()
            ?? sourceEntries
                .Where(entry => entry.OriginalLine == originalLine)
                .OrderByDescending(entry => entry.OriginalColumn)
                .ThenBy(entry => entry.GeneratedLine)
                .ThenBy(entry => entry.GeneratedColumn)
                .FirstOrDefault()
            ?? sourceEntries
                .Where(entry => entry.OriginalLine > originalLine)
                .OrderBy(entry => entry.OriginalLine)
                .ThenBy(entry => entry.OriginalColumn)
                .ThenBy(entry => entry.GeneratedLine)
                .ThenBy(entry => entry.GeneratedColumn)
                .FirstOrDefault()
            ?? sourceEntries.LastOrDefault(entry => entry.OriginalLine < originalLine);
    }

    /// <summary>
    /// Returns the sorted generated positions where V8 should toggle blackboxing state.
    /// The first interval is not blackboxed, matching Debugger.setBlackboxedRanges.
    /// </summary>
    public IReadOnlyList<V8SourceMapPosition> GetBlackboxedStateTransitions()
    {
        var result = new List<V8SourceMapPosition>();
        var ignored = false;
        foreach (var entry in _entries)
        {
            var nextIgnored = IsIgnoredSource(entry.SourceIndex);
            if (nextIgnored == ignored) continue;
            var position = new V8SourceMapPosition(entry.GeneratedLine, entry.GeneratedColumn);
            if (result.Count > 0 && result[^1] == position)
            {
                result.RemoveAt(result.Count - 1);
            }
            else
            {
                result.Add(position);
            }
            ignored = nextIgnored;
        }
        return result;
    }

    internal V8SourceMap WithSingleLineMutation(
        int sourceIndex,
        string sourceContent,
        int originalLine,
        int originalStartColumn,
        int originalEndColumn,
        int originalReplacementLength,
        int generatedLine,
        int generatedStartColumn,
        int generatedEndColumn,
        int generatedReplacementLength)
    {
        var originalDelta = originalReplacementLength - (originalEndColumn - originalStartColumn);
        var generatedDelta = generatedReplacementLength - (generatedEndColumn - generatedStartColumn);
        var entries = new List<V8SourceMapEntry>(_entries.Count);
        foreach (var entry in _entries)
        {
            var insideOriginalMutation = entry.SourceIndex == sourceIndex && entry.OriginalLine == originalLine &&
                entry.OriginalColumn > originalStartColumn && entry.OriginalColumn < originalEndColumn;
            var insideGeneratedMutation = entry.GeneratedLine == generatedLine &&
                entry.GeneratedColumn > generatedStartColumn && entry.GeneratedColumn < generatedEndColumn;
            if (insideOriginalMutation || insideGeneratedMutation) continue;

            var originalColumn = entry.OriginalColumn;
            if (entry.SourceIndex == sourceIndex && entry.OriginalLine == originalLine &&
                entry.OriginalColumn >= originalEndColumn)
            {
                originalColumn += originalDelta;
            }
            var generatedColumn = entry.GeneratedColumn;
            if (entry.GeneratedLine == generatedLine && entry.GeneratedColumn >= generatedEndColumn)
            {
                generatedColumn += generatedDelta;
            }
            entries.Add(entry with { OriginalColumn = originalColumn, GeneratedColumn = generatedColumn });
        }

        var sourcesContent = SourcesContent.ToArray();
        sourcesContent[sourceIndex] = sourceContent;
        return new V8SourceMap(
            Sources,
            sourcesContent,
            Names,
            entries,
            _sourceRoots,
            _sourceMapUris,
            _ignoredSourceIndexes,
            File,
            SourceRoot,
            _isIndexed);
    }

    private static void Swap<T>(T[] values, int left, int right)
    {
        if (left == right) return;
        (values[left], values[right]) = (values[right], values[left]);
    }

    private static JsonObject ParseRoot(string json) =>
        JsonNode.Parse(json) as JsonObject ?? throw new FormatException("Source map must be a JSON object.");

    private static V8SourceMap ParseObject(JsonObject root, Uri? sourceMapUri)
    {
        ValidateVersion(root);
        if (root["sections"] is not JsonArray sections) return ParseRegularMap(root, sourceMapUri);

        var maps = new List<(V8SourceMap Map, int Line, int Column)>();
        ValidateAndVisitSections(sections, section =>
        {
            if (section["url"] is not null)
            {
                throw new NotSupportedException("Indexed source maps with external section URLs require ParseAsync.");
            }
            var child = section["map"] as JsonObject ?? throw new FormatException("A source-map section must contain either 'map' or 'url'.");
            var offset = ReadOffset(section);
            maps.Add((ParseObject(child, sourceMapUri), offset.Line, offset.Column));
        });
        return MergeIndexedMap(root, maps);
    }

    private static async Task<V8SourceMap> ParseObjectAsync(
        JsonObject root,
        Uri? sourceMapUri,
        Func<Uri, CancellationToken, Task<string>> sectionLoader,
        CancellationToken cancellationToken)
    {
        ValidateVersion(root);
        if (root["sections"] is not JsonArray sections) return ParseRegularMap(root, sourceMapUri);

        var maps = new List<(V8SourceMap Map, int Line, int Column)>();
        var previousLine = -1;
        var previousColumn = -1;
        foreach (var section in sections.OfType<JsonObject>())
        {
            var offset = ReadOffset(section);
            ValidateSectionOrder(offset.Line, offset.Column, ref previousLine, ref previousColumn);
            V8SourceMap childMap;
            if (section["map"] is JsonObject embedded)
            {
                childMap = await ParseObjectAsync(embedded, sourceMapUri, sectionLoader, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var sectionUrl = section["url"]?.GetValue<string>() ??
                    throw new FormatException("A source-map section must contain either 'map' or 'url'.");
                if (!Uri.TryCreate(sectionUrl, UriKind.Absolute, out var sectionUri))
                {
                    if (sourceMapUri is null || !Uri.TryCreate(sourceMapUri, sectionUrl, out sectionUri))
                    {
                        throw new FormatException($"Unable to resolve source-map section URL '{sectionUrl}'.");
                    }
                }
                var sectionJson = await sectionLoader(sectionUri, cancellationToken).ConfigureAwait(false);
                childMap = await ParseObjectAsync(ParseRoot(sectionJson), sectionUri, sectionLoader, cancellationToken).ConfigureAwait(false);
            }
            maps.Add((childMap, offset.Line, offset.Column));
        }
        return MergeIndexedMap(root, maps);
    }

    private static V8SourceMap ParseRegularMap(JsonObject root, Uri? sourceMapUri)
    {
        var sources = ReadStrings(root["sources"] as JsonArray);
        var names = ReadStrings(root["names"] as JsonArray);
        var sourceRoot = root["sourceRoot"]?.GetValue<string>() ?? "";
        var content = new string?[sources.Count];
        if (root["sourcesContent"] is JsonArray contentArray)
        {
            for (var index = 0; index < Math.Min(content.Length, contentArray.Count); index++)
            {
                content[index] = contentArray[index]?.GetValue<string>();
            }
        }
        var mappings = root["mappings"]?.GetValue<string>() ??
            throw new FormatException("A regular source map must contain a mappings string.");
        var ignored = ReadIgnoredSourceIndexes(root, sources.Count);
        return new V8SourceMap(
            sources,
            content,
            names,
            ParseMappings(mappings, sources.Count, names.Count),
            Enumerable.Repeat(sourceRoot, sources.Count).ToArray(),
            Enumerable.Repeat(sourceMapUri, sources.Count).ToArray(),
            ignored,
            root["file"]?.GetValue<string>() ?? "",
            sourceRoot,
            false);
    }

    private static V8SourceMap MergeIndexedMap(
        JsonObject root,
        IReadOnlyList<(V8SourceMap Map, int Line, int Column)> sectionMaps)
    {
        var sources = new List<string>();
        var content = new List<string?>();
        var names = new List<string>();
        var entries = new List<V8SourceMapEntry>();
        var roots = new List<string>();
        var uris = new List<Uri?>();
        var ignored = new HashSet<int>();

        foreach (var (map, lineOffset, columnOffset) in sectionMaps)
        {
            var sourceOffset = sources.Count;
            var nameOffset = names.Count;
            sources.AddRange(map.Sources);
            content.AddRange(map.SourcesContent);
            names.AddRange(map.Names);
            roots.AddRange(map._sourceRoots);
            uris.AddRange(map._sourceMapUris);
            foreach (var ignoredIndex in map.IgnoredSourceIndexes) ignored.Add(sourceOffset + ignoredIndex);
            foreach (var entry in map.Entries)
            {
                entries.Add(entry with
                {
                    GeneratedLine = entry.GeneratedLine + lineOffset,
                    GeneratedColumn = entry.GeneratedColumn + (entry.GeneratedLine == 0 ? columnOffset : 0),
                    SourceIndex = entry.SourceIndex + sourceOffset,
                    NameIndex = entry.NameIndex is int nameIndex ? nameIndex + nameOffset : null
                });
            }
        }

        entries.Sort(static (left, right) =>
        {
            var line = left.GeneratedLine.CompareTo(right.GeneratedLine);
            return line != 0 ? line : left.GeneratedColumn.CompareTo(right.GeneratedColumn);
        });
        return new V8SourceMap(
            sources,
            content,
            names,
            entries,
            roots,
            uris,
            ignored,
            root["file"]?.GetValue<string>() ?? "",
            "",
            true);
    }

    private static IReadOnlySet<int> ReadIgnoredSourceIndexes(JsonObject root, int sourceCount)
    {
        var values = root["ignoreList"] as JsonArray ?? root["x_google_ignoreList"] as JsonArray;
        if (values is null) return new HashSet<int>();
        var result = new HashSet<int>();
        foreach (var node in values)
        {
            if (node is not JsonValue value || !value.TryGetValue<int>(out var index) || index < 0 || index >= sourceCount)
            {
                throw new FormatException("Source-map ignore-list entries must be valid source indexes.");
            }
            result.Add(index);
        }
        return result;
    }

    private static void ValidateAndVisitSections(JsonArray sections, Action<JsonObject> visitor)
    {
        var previousLine = -1;
        var previousColumn = -1;
        foreach (var section in sections.OfType<JsonObject>())
        {
            var offset = ReadOffset(section);
            ValidateSectionOrder(offset.Line, offset.Column, ref previousLine, ref previousColumn);
            visitor(section);
        }
    }

    private static (int Line, int Column) ReadOffset(JsonObject section)
    {
        var offset = section["offset"] as JsonObject ?? throw new FormatException("A source-map section must contain an offset.");
        var line = offset["line"]?.GetValue<int>() ?? throw new FormatException("A source-map section offset must contain a line.");
        var column = offset["column"]?.GetValue<int>() ?? throw new FormatException("A source-map section offset must contain a column.");
        if (line < 0 || column < 0) throw new FormatException("Source-map section offsets cannot be negative.");
        return (line, column);
    }

    private static void ValidateSectionOrder(int line, int column, ref int previousLine, ref int previousColumn)
    {
        if (line < previousLine || line == previousLine && column <= previousColumn)
        {
            throw new FormatException("Source-map sections must have strictly increasing offsets.");
        }
        previousLine = line;
        previousColumn = column;
    }

    private static void ValidateVersion(JsonObject root)
    {
        if (root["version"]?.GetValue<int>() != 3) throw new NotSupportedException("Only source map revision 3 is supported.");
    }

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

public sealed record V8SourceMapPosition(int LineNumber, int ColumnNumber);
