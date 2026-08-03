using CdpInspectorApp.Services;
using Chrome.DevTools.Protocol.Inspector;

namespace Avalonia.Diagnostics.Cdp.Tests;

public sealed class EsbuildV8SourceRegeneratorTests
{
    [Fact]
    public async Task RegeneratesTypeScriptAndProducesUpdatedSourceMapWhenEsbuildIsAvailable()
    {
        var esbuild = FindEsbuild();
        if (esbuild is null) return;

        const string original = "const value: number = 2;\nconsole.log(value);\n";
        const string edited = "const value: number = 3;\nconsole.log(value);\nconsole.log('again');\n";
        var map = ParseMap(original);
        var adapter = new EsbuildV8SourceRegenerator(esbuild);
        var request = new V8SourceRegenerationRequest(
            map,
            0,
            new Uri(Path.Combine(Environment.CurrentDirectory, "source.ts")).AbsoluteUri,
            new Uri(Path.Combine(Environment.CurrentDirectory, "generated.js")).AbsoluteUri,
            original,
            edited,
            "const value = 2;\nconsole.log(value);\n");

        var result = await adapter.RegenerateAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Message);
        Assert.Contains("const value = 3", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("console.log(\"again\")", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Equal(edited, Assert.Single(result.SourceMap!.SourcesContent));
        Assert.Equal("source.ts", Assert.Single(result.SourceMap.Sources));
    }

    [Fact]
    public async Task ReportsUnavailableExplicitCompilerAndRejectsBundledMaps()
    {
        const string content = "const value: number = 2;\n";
        var adapter = new EsbuildV8SourceRegenerator(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var map = ParseMap(content);
        var request = new V8SourceRegenerationRequest(
            map,
            0,
            "file:///app/source.ts",
            "file:///app/source.js",
            content,
            content + "console.log(value);\n",
            "const value = 2;\n");

        Assert.True(adapter.CanRegenerate(request));
        var unavailable = await adapter.RegenerateAsync(request, TestContext.Current.CancellationToken);
        Assert.False(unavailable.Success);
        Assert.Contains("not found", unavailable.Message, StringComparison.OrdinalIgnoreCase);

        var bundledMap = V8SourceMap.Parse("""
            {
              "version": 3,
              "sources": ["source.ts", "other.ts"],
              "sourcesContent": ["const value = 2;", "export {}"],
              "names": [],
              "mappings": "AAAA;ACAA"
            }
            """);
        Assert.False(adapter.CanRegenerate(request with { SourceMap = bundledMap }));
    }

    private static string? FindEsbuild()
    {
        var executable = OperatingSystem.IsWindows() ? "esbuild.cmd" : "esbuild";
        for (var directory = AppContext.BaseDirectory; !string.IsNullOrWhiteSpace(directory); directory = Directory.GetParent(directory)?.FullName)
        {
            var candidate = Path.Combine(directory, "node_modules", ".bin", executable);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static V8SourceMap ParseMap(string content) => V8SourceMap.Parse($$"""
        {
          "version": 3,
          "sources": ["source.ts"],
          "sourcesContent": [{{System.Text.Json.JsonSerializer.Serialize(content)}}],
          "names": [],
          "mappings": "AAAA;AACA;AACA"
        }
        """);
}
