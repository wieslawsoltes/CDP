using System.Text;
using System.Text.Json.Nodes;

namespace Chrome.DevTools.Protocol.Inspector;

/// <summary>
/// Complete WebAssembly disassembly returned by the V8 Debugger domain. Display lines retain a
/// one-to-one relationship with bytecode offsets so editor navigation and breakpoints can map
/// back to protocol locations without parsing the human-readable instruction text.
/// </summary>
public sealed class V8WasmDisassembly
{
    internal V8WasmDisassembly(
        IReadOnlyList<string> lines,
        IReadOnlyList<int> bytecodeOffsets,
        IReadOnlyList<int> functionBodyOffsets,
        int totalNumberOfLines)
    {
        Lines = lines;
        BytecodeOffsets = bytecodeOffsets;
        FunctionBodyOffsets = functionBodyOffsets;
        TotalNumberOfLines = totalNumberOfLines;
    }

    public IReadOnlyList<string> Lines { get; }
    public IReadOnlyList<int> BytecodeOffsets { get; }
    public IReadOnlyList<int> FunctionBodyOffsets { get; }
    public int TotalNumberOfLines { get; }

    public int GetBytecodeOffset(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= BytecodeOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        return BytecodeOffsets[lineIndex];
    }

    public int FindLineIndex(int bytecodeOffset)
    {
        if (BytecodeOffsets.Count == 0) return -1;
        var index = BytecodeOffsets.BinarySearch(bytecodeOffset);
        if (index >= 0) return index;
        index = ~index - 1;
        return Math.Max(0, index);
    }

    public string FormatText()
    {
        var builder = new StringBuilder();
        for (var index = 0; index < Lines.Count; index++)
        {
            builder.Append("0x");
            builder.Append(BytecodeOffsets[index].ToString("x8", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append("  ");
            builder.AppendLine(Lines[index]);
        }
        return builder.ToString();
    }
}

/// <summary>
/// Validates and joins the initial `Debugger.disassembleWasmModule` chunk with subsequent
/// `Debugger.nextWasmDisassemblyChunk` responses.
/// </summary>
public sealed class V8WasmDisassemblyBuilder
{
    private readonly List<string> _lines = [];
    private readonly List<int> _bytecodeOffsets = [];
    private readonly int[] _functionBodyOffsets;

    private V8WasmDisassemblyBuilder(string streamId, int totalNumberOfLines, int[] functionBodyOffsets)
    {
        if (totalNumberOfLines < 0) throw new FormatException("Wasm disassembly line count cannot be negative.");
        if (functionBodyOffsets.Length % 2 != 0)
            throw new FormatException("Wasm function body offsets must contain start/end pairs.");
        StreamId = streamId;
        TotalNumberOfLines = totalNumberOfLines;
        _functionBodyOffsets = functionBodyOffsets;
    }

    public string StreamId { get; }
    public int TotalNumberOfLines { get; }
    public int ReceivedLineCount => _lines.Count;
    public bool NeedsMoreChunks => StreamId.Length > 0 && ReceivedLineCount < TotalNumberOfLines;

    public static V8WasmDisassemblyBuilder FromInitialResponse(JsonObject response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var builder = new V8WasmDisassemblyBuilder(
            response["streamId"]?.GetValue<string>() ?? "",
            response["totalNumberOfLines"]?.GetValue<int>() ?? 0,
            ReadIntegers(response["functionBodyOffsets"] as JsonArray));
        builder.AppendChunk(response["chunk"] as JsonObject);
        return builder;
    }

    public void AppendNextResponse(JsonObject response)
    {
        ArgumentNullException.ThrowIfNull(response);
        AppendChunk(response["chunk"] as JsonObject);
    }

    public V8WasmDisassembly Build()
    {
        if (_lines.Count != TotalNumberOfLines)
        {
            throw new FormatException(
                $"Wasm disassembly ended after {_lines.Count} of {TotalNumberOfLines} advertised lines.");
        }
        return new V8WasmDisassembly(
            _lines.ToArray(),
            _bytecodeOffsets.ToArray(),
            _functionBodyOffsets,
            TotalNumberOfLines);
    }

    private void AppendChunk(JsonObject? chunk)
    {
        if (chunk is null) throw new FormatException("Wasm disassembly response is missing a chunk.");
        var lines = (chunk["lines"] as JsonArray)?.Select(node => node?.GetValue<string>() ?? "").ToArray() ?? [];
        var offsets = ReadIntegers(chunk["bytecodeOffsets"] as JsonArray);
        if (lines.Length != offsets.Length)
            throw new FormatException("Wasm disassembly lines and bytecode offsets have different lengths.");
        if (_lines.Count + lines.Length > TotalNumberOfLines)
            throw new FormatException("Wasm disassembly contains more lines than advertised.");

        foreach (var offset in offsets)
        {
            if (offset < 0 || _bytecodeOffsets.Count > 0 && offset < _bytecodeOffsets[^1])
                throw new FormatException("Wasm disassembly bytecode offsets must be non-negative and sorted.");
            _bytecodeOffsets.Add(offset);
        }
        _lines.AddRange(lines);
    }

    private static int[] ReadIntegers(JsonArray? values) =>
        values?.Select(node => node?.GetValue<int>() ?? 0).ToArray() ?? [];
}

internal static class V8WasmIntegerListExtensions
{
    public static int BinarySearch(this IReadOnlyList<int> values, int value)
    {
        var low = 0;
        var high = values.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var comparison = values[middle].CompareTo(value);
            if (comparison == 0) return middle;
            if (comparison < 0) low = middle + 1;
            else high = middle - 1;
        }
        return ~low;
    }
}
