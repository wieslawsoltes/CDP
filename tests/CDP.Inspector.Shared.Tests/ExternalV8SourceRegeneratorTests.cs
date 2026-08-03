using System.Text.Json.Nodes;
using CdpInspectorApp.Services;
using Chrome.DevTools.Protocol.Inspector;

namespace CDP.Inspector.Shared.Tests;

public sealed class ExternalV8SourceRegeneratorTests
{
    [Fact(Timeout = 30_000)]
    public async Task RegeneratesCoffeeScriptThroughManifestConfiguredJsonProtocol()
    {
        var node = FindOnPath(OperatingSystem.IsWindows() ? "node.exe" : "node");
        if (node is null) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), $"cdp-external-regenerator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var compilerPath = Path.Combine(directory, "coffee-regenerator.mjs");
            await File.WriteAllTextAsync(compilerPath, """
                let input = '';
                process.stdin.setEncoding('utf8');
                process.stdin.on('data', chunk => input += chunk);
                process.stdin.on('end', () => {
                  const request = JSON.parse(input);
                  if (request.protocolVersion !== 1 || request.sourceMap.version !== 3) {
                    process.stdout.write(JSON.stringify({ protocolVersion: 1, success: false, message: 'invalid request' }));
                    return;
                  }
                  const sourceMap = {
                    version: 3,
                    file: 'generated.js',
                    sources: ['source.coffee'],
                    sourcesContent: [request.editedSource],
                    names: [],
                    mappings: 'AAAA'
                  };
                  process.stdout.write(JSON.stringify({
                    protocolVersion: 1,
                    success: true,
                    message: 'CoffeeScript fixture regenerated',
                    generatedSource: 'globalThis.externalMutation = 42;\n',
                    sourceIndex: 0,
                    sourceMap
                  }));
                });
                """, cancellationToken);
            var manifestPath = Path.Combine(directory, "regenerators.json");
            var manifest = new JsonObject
            {
                ["regenerators"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "CoffeeScript fixture",
                        ["executable"] = node,
                        ["arguments"] = new JsonArray("coffee-regenerator.mjs"),
                        ["extensions"] = new JsonArray("coffee"),
                        ["workingDirectory"] = ".",
                        ["timeoutSeconds"] = 10
                    }
                }
            };
            await File.WriteAllTextAsync(manifestPath, manifest.ToJsonString(), cancellationToken);
            const string original = "square = (value) -> value * value\n";
            const string edited = "square = (value) ->\n  value * value\nglobalThis.externalMutation = square 7\n";
            var sourcePath = Path.Combine(directory, "source.coffee");
            var sourceMap = V8SourceMap.Parse($$"""
                {
                  "version": 3,
                  "file": "generated.js",
                  "sources": ["source.coffee"],
                  "sourcesContent": [{{System.Text.Json.JsonSerializer.Serialize(original)}}],
                  "names": [],
                  "mappings": "AAAA"
                }
                """);
            var adapter = Assert.Single(ExternalV8SourceRegenerator.LoadManifest(manifestPath));

            var mutation = await new V8SourceMutationEngine([adapter]).CreatePatchAsync(
                sourceMap,
                0,
                original,
                edited,
                "globalThis.externalMutation = 49;\n",
                new Uri(sourcePath).AbsoluteUri,
                new Uri(Path.Combine(directory, "generated.js")).AbsoluteUri,
                cancellationToken);

            Assert.True(mutation.CanApply, mutation.Message);
            Assert.Equal(V8SourceMutationKind.Regenerated, mutation.Kind);
            Assert.Equal("CoffeeScript fixture", mutation.Preview?.AdapterName);
            Assert.Equal("globalThis.externalMutation = 42;\n", mutation.GeneratedSource);
            Assert.Equal(edited, Assert.Single(mutation.UpdatedSourceMap!.SourcesContent));
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // The OS can remove an orphaned temporary directory later.
            }
        }
    }

    [Fact]
    public void ManifestNormalizesExtensionsAndResolvesRelativeCompilerPaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cdp-regenerator-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "tools"));
        try
        {
            var manifestPath = Path.Combine(directory, "regenerators.json");
            File.WriteAllText(manifestPath, """
                [{
                  "name": "Vue compiler",
                  "executable": "tools/compiler",
                  "extensions": ["vue", ".svelte"],
                  "workingDirectory": "."
                }]
                """);

            var adapter = Assert.Single(ExternalV8SourceRegenerator.LoadManifest(manifestPath));
            var map = V8SourceMap.Parse("""
                {"version":3,"sources":["component.vue"],"sourcesContent":["<template />"],"names":[],"mappings":"AAAA"}
                """);
            var request = new V8SourceRegenerationRequest(
                map,
                0,
                "file:///workspace/component.vue",
                "file:///workspace/component.js",
                "<template />",
                "<template><p>Hello</p></template>",
                "");

            Assert.True(adapter.CanRegenerate(request));
            Assert.True(adapter.CanRegenerate(request with { SourceUrl = "file:///workspace/component.svelte" }));
            Assert.False(adapter.CanRegenerate(request with { SourceUrl = "file:///workspace/component.ts" }));
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // The OS can remove an orphaned temporary directory later.
            }
        }
    }

    private static string? FindOnPath(string executable)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            var candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
