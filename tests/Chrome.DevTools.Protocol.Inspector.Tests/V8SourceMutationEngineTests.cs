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
        Assert.Equal("mapped patch · source 4→4 lines · output 4→4 lines", result.Preview?.Summary);
        Assert.True(result.Preview?.GeneratedRevision.Matches(original));
        Assert.True(result.Preview?.ResultRevision.Matches(edited));
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
    public void PreservesGeneratedCrLfWhenSourceMapContentUsesLf()
    {
        const string original = "function compute(value: number) {\n  const doubled = value * 2;\n  return doubled;\n}\n";
        const string edited = "function compute(value: number) {\n  const doubled = value * 4;\n  return doubled;\n}\n";
        const string generated = "function compute(value) {\r\n  const doubled = value * 2;\r\n  return doubled;\r\n}\r\n";
        const string expected = "function compute(value) {\r\n  const doubled = value * 4;\r\n  return doubled;\r\n}\r\n";
        var map = ParseLineMappedSource("source.ts", original);

        var result = new V8SourceMutationEngine().CreatePatch(
            map,
            0,
            original,
            edited,
            generated);

        Assert.True(result.CanApply, result.Message);
        Assert.Equal(expected, result.GeneratedSource);
        Assert.Equal(new V8SourceMutationRange(1, 26, 1, 27), result.GeneratedRange);
        Assert.DoesNotContain("\n", result.GeneratedSource.Replace("\r\n", "", StringComparison.Ordinal));
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

    [Fact]
    public async Task RegeneratesTransformedMultilineTypeScriptWithCompilerAdapter()
    {
        const string original = "const value: number = 2;\nconsole.log(value);\n";
        const string edited = "const value: number = 3;\nconsole.log(value);\nconsole.log('again');\n";
        const string generated = "const value = 2;\nconsole.log(value);\n";
        const string regenerated = "const value = 3;\nconsole.log(value);\nconsole.log('again');\n";
        var originalMap = ParseLineMappedSource("source.ts", original);
        var regeneratedMap = ParseLineMappedSource("source.ts", edited);
        var adapter = new TestRegenerator(regenerated, regeneratedMap);

        var result = await new V8SourceMutationEngine([adapter]).CreatePatchAsync(
            originalMap,
            0,
            original,
            edited,
            generated,
            "file:///app/source.ts",
            "file:///app/source.js");

        Assert.True(result.CanApply);
        Assert.True(result.HasChanges);
        Assert.Equal(V8SourceMutationKind.Regenerated, result.Kind);
        Assert.Equal(regenerated, result.GeneratedSource);
        Assert.Same(regeneratedMap, result.UpdatedSourceMap);
        Assert.Null(result.OriginalRange);
        Assert.Null(result.GeneratedRange);
        Assert.Equal("file:///app/source.ts", adapter.Request!.SourceUrl);
        Assert.Equal("file:///app/source.js", adapter.Request.GeneratedUrl);
        Assert.Equal(edited, adapter.Request.EditedSource);
        Assert.Equal("Test compiler", result.Preview?.AdapterName);
        Assert.Equal(V8SourceMutationKind.Regenerated, result.Preview?.Kind);
    }

    [Fact]
    public async Task ReportsCompilerFailureAndRejectsStaleSourceMapContent()
    {
        const string original = "const value: number = 2;\n";
        const string edited = "const value: number = 3;\nconst next = value + 1;\n";
        var map = ParseLineMappedSource("source.ts", original);

        var failed = await new V8SourceMutationEngine([
            new TestRegenerator(V8SourceRegenerationResult.Failed("TypeScript diagnostic TS1005"))
        ]).CreatePatchAsync(map, 0, original, edited, "const value = 2;\n");
        Assert.False(failed.CanApply);
        Assert.Contains("TS1005", failed.Message, StringComparison.Ordinal);

        var stale = await new V8SourceMutationEngine([
            new TestRegenerator("const value = 3;\n", map)
        ]).CreatePatchAsync(map, 0, original, edited, "const value = 2;\n");
        Assert.False(stale.CanApply);
        Assert.Contains("edited source", stale.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FallsBackToNextCompatibleRegeneratorAndReportsRevisionFingerprints()
    {
        const string original = "const value: number = 2;\n";
        const string edited = "const value: number = 3;\nconst next = value + 1;\n";
        const string generated = "const value = 2;\n";
        const string regenerated = "const value = 3;\nconst next = value + 1;\n";
        var originalMap = ParseLineMappedSource("source.ts", original);
        var regeneratedMap = ParseLineMappedSource("source.ts", edited);
        var failing = new TestRegenerator(
            V8SourceRegenerationResult.Failed("primary compiler failed"),
            "Primary compiler");
        var fallback = new TestRegenerator(regenerated, regeneratedMap, "Fallback compiler");

        var result = await new V8SourceMutationEngine([failing, fallback]).CreatePatchAsync(
            originalMap, 0, original, edited, generated, "file:///app/source.ts", "file:///app/source.js");

        Assert.True(result.CanApply, result.Message);
        Assert.NotNull(failing.Request);
        Assert.NotNull(fallback.Request);
        Assert.Equal("Fallback compiler", result.Preview?.AdapterName);
        Assert.Equal(V8SourceRevision.Create(original), fallback.Request!.OriginalRevision);
        Assert.Equal(V8SourceRevision.Create(generated), fallback.Request.GeneratedRevision);
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

    private sealed class TestRegenerator : IV8SourceRegenerator
    {
        private readonly V8SourceRegenerationResult _result;

        public TestRegenerator(string generatedSource, V8SourceMap sourceMap, string name = "Test compiler")
            : this(V8SourceRegenerationResult.Regenerated(generatedSource, sourceMap, "Test compilation completed."), name)
        {
        }

        public TestRegenerator(V8SourceRegenerationResult result, string name = "Test compiler")
        {
            _result = result;
            Name = name;
        }

        public string Name { get; }
        public V8SourceRegenerationRequest? Request { get; private set; }
        public bool CanRegenerate(V8SourceRegenerationRequest request) => request.SourceUrl.EndsWith(".ts", StringComparison.Ordinal);

        public ValueTask<V8SourceRegenerationResult> RegenerateAsync(
            V8SourceRegenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return ValueTask.FromResult(_result);
        }
    }
}
