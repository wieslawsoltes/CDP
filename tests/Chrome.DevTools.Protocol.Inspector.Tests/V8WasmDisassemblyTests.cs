using System.Text.Json.Nodes;
using Chrome.DevTools.Protocol.Inspector;

namespace Chrome.DevTools.Protocol.Inspector.Tests;

public sealed class V8WasmDisassemblyTests
{
    [Fact]
    public void JoinsStreamedChunksAndMapsDisplayLinesToBytecodeOffsets()
    {
        var builder = V8WasmDisassemblyBuilder.FromInitialResponse(JsonNode.Parse("""
            {
              "streamId": "wasm-stream-1",
              "totalNumberOfLines": 4,
              "functionBodyOffsets": [32, 41],
              "chunk": {
                "lines": ["func $add", "local.get 0"],
                "bytecodeOffsets": [32, 34]
              }
            }
            """)!.AsObject());

        Assert.True(builder.NeedsMoreChunks);
        builder.AppendNextResponse(JsonNode.Parse("""
            {
              "chunk": {
                "lines": ["local.get 1", "i32.add"],
                "bytecodeOffsets": [36, 38]
              }
            }
            """)!.AsObject());
        var disassembly = builder.Build();

        Assert.False(builder.NeedsMoreChunks);
        Assert.Equal(new[] { 32, 34, 36, 38 }, disassembly.BytecodeOffsets);
        Assert.Equal(new[] { 32, 41 }, disassembly.FunctionBodyOffsets);
        Assert.Equal(36, disassembly.GetBytecodeOffset(2));
        Assert.Equal(1, disassembly.FindLineIndex(35));
        Assert.Contains("0x00000026  i32.add", disassembly.FormatText(), StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsIncompleteOrMisalignedDisassemblyChunks()
    {
        Assert.Throws<FormatException>(() => V8WasmDisassemblyBuilder.FromInitialResponse(JsonNode.Parse("""
            {
              "totalNumberOfLines": 1,
              "functionBodyOffsets": [10],
              "chunk": { "lines": ["nop"], "bytecodeOffsets": [10] }
            }
            """)!.AsObject()));

        var incomplete = V8WasmDisassemblyBuilder.FromInitialResponse(JsonNode.Parse("""
            {
              "streamId": "stream",
              "totalNumberOfLines": 2,
              "functionBodyOffsets": [],
              "chunk": { "lines": ["nop"], "bytecodeOffsets": [10] }
            }
            """)!.AsObject());
        Assert.Throws<FormatException>(() => incomplete.Build());
    }
}
