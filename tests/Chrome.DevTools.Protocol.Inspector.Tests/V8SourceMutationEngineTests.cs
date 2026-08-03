using Chrome.DevTools.Protocol.Inspector;

namespace Chrome.DevTools.Protocol.Inspector.Tests;

public sealed class V8SourceMutationEngineTests
{
    [Fact]
    public void PatchesIdentityMappedJavaScriptAndUpdatesSourceContent()
    {
        var map = ParseLineMappedSource("source.js", "function compute(value) {\n  return value * 2;\n}\n");
        const string original = "function compute(value) {\n  return value * 2;\n}\n";
        const string edited = "function compute(value) {\n  return value * 3;\n}\n";
        var result = new V8SourceMutationEngine().CreatePatch(map, 0, original, edited, original);

        Assert.True(result.CanApply);
        Assert.True(result.HasChanges);
        Assert.Equal(edited, result.GeneratedSource);
        Assert.Equal(edited, Assert.Single(result.UpdatedSourceMap!.SourcesContent));
        Assert.Equal(new V8SourceMutationRange(1, 17, 1, 18), result.OriginalRange);
    }

    [Fact]
    public void PatchesIdentityPreservingTypeScriptBodyInsideGeneratedJavaScript()
    {
        const string original = "function compute(value: number) {\n  const doubled = value * 2;\n  return doubled;\n}\n";
        const string edited = "function compute(value: number) {\n  const doubled = value * 4;\n  return doubled;\n}\n";
        const string generated = "function compute(value) {\n  const doubled = value * 2;\n  return doubled;\n}\n";
        var map = ParseLineMappedSource("source.ts", original);

        var result = new V8SourceMutationEngine().CreatePatch(map, 0, original, edited, generated);

        Assert.True(result.CanApply);
        Assert.Equal("function compute(value) {\n  const doubled = value * 4;\n  return doubled;\n}\n", result.GeneratedSource);
        Assert.Equal(new V8SourceMutationRange(1, 26, 1, 27), result.GeneratedRange);
    }

    [Fact]
    public void RejectsCompilerTransformedAndMultilineMutations()
    {
        const string original = "const value: number = 2;\nconsole.log(value);\n";
        var map = ParseLineMappedSource("source.ts", original);
        var transformed = new V8SourceMutationEngine().CreatePatch(
            map,
            0,
            original,
            "const value: number = 3;\nconsole.log(value);\n",
            "const value = 2;\nconsole.log(value);\n");
        Assert.False(transformed.CanApply);
        Assert.Contains("transformed", transformed.Message, StringComparison.OrdinalIgnoreCase);

        var multiline = new V8SourceMutationEngine().CreatePatch(
            map,
            0,
            original,
            "const value: number = 2;\nconsole.log(value);\nconsole.log('again');\n",
            original);
        Assert.False(multiline.CanApply);
        Assert.Contains("one mapping-preserving line", multiline.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static V8SourceMap ParseLineMappedSource(string source, string content) => V8SourceMap.Parse($$"""
        {
          "version": 3,
          "sources": ["{{source}}"],
          "sourcesContent": [{{System.Text.Json.JsonSerializer.Serialize(content)}}],
          "names": [],
          "mappings": "AAAA;AACA;AACA;AACA"
        }
        """);
}
