using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
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

    [Fact(Timeout = 30_000)]
    public async Task RegeneratedMultilineTypeScriptPassesRealNodeV8DryRunAndApply()
    {
        var esbuild = FindEsbuild();
        if (esbuild is null) return;
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v8-source-map-target.js");
        Assert.True(File.Exists(fixture), $"Missing source-map fixture: {fixture}");
        var port = GetAvailablePort();
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { $"--inspect-brk=127.0.0.1:{port}", fixture },
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        });
        Assert.NotNull(process);

        try
        {
            var target = Assert.Single(await WaitForTargetsAsync(new Uri($"http://127.0.0.1:{port}"), cancellationToken));
            await using var inspector = new V8InspectorClient();
            var scripts = Channel.CreateUnbounded<JsonObject>();
            var pauses = Channel.CreateUnbounded<JsonObject>();
            inspector.EventReceived += (_, args) =>
            {
                if (args.Method == "Debugger.scriptParsed") scripts.Writer.TryWrite(args.Params);
                if (args.Method == "Debugger.paused") pauses.Writer.TryWrite(args.Params);
            };
            await inspector.ConnectAsync(new Uri(target.WebSocketDebuggerUrl), cancellationToken);
            await inspector.SendCommandAsync("Runtime.enable", cancellationToken: cancellationToken);
            await inspector.SendCommandAsync("Debugger.enable", cancellationToken: cancellationToken);
            await inspector.SendCommandAsync("Runtime.runIfWaitingForDebugger", cancellationToken: cancellationToken);

            JsonObject script;
            do
            {
                script = await scripts.Reader.ReadAsync(cancellationToken).AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            while (!script["url"]!.GetValue<string>().EndsWith("v8-source-map-target.js", StringComparison.Ordinal));
            _ = await pauses.Reader.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            var sourceMapUrl = script["sourceMapURL"]!.GetValue<string>();
            var sourceMapJson = Encoding.UTF8.GetString(
                Convert.FromBase64String(sourceMapUrl[(sourceMapUrl.IndexOf(',') + 1)..]));
            var generatedUrl = script["url"]!.GetValue<string>();
            var sourceMap = V8SourceMap.Parse(sourceMapJson, new Uri(generatedUrl));
            var original = Assert.Single(sourceMap.SourcesContent)!;
            var edited = original
                .Replace("export function", "function", StringComparison.Ordinal)
                .Replace("value * 2", "value * 3", StringComparison.Ordinal) +
                "\nglobalThis.regeneratedMarker = true;\nsetInterval(() => {}, 1000);\n";
            var current = await inspector.SendCommandAsync("Debugger.getScriptSource", new JsonObject
            {
                ["scriptId"] = script["scriptId"]!.GetValue<string>()
            }, cancellationToken);
            var mutation = await new V8SourceMutationEngine([new EsbuildV8SourceRegenerator(esbuild)])
                .CreatePatchAsync(
                    sourceMap,
                    0,
                    original,
                    edited,
                    current["scriptSource"]!.GetValue<string>(),
                    sourceMap.ResolveSourceUrl(0),
                    generatedUrl,
                    cancellationToken);
            Assert.True(mutation.CanApply, mutation.Message);
            Assert.Equal(V8SourceMutationKind.Regenerated, mutation.Kind);

            var dryRun = await inspector.SendCommandAsync("Debugger.setScriptSource", new JsonObject
            {
                ["scriptId"] = script["scriptId"]!.GetValue<string>(),
                ["scriptSource"] = mutation.GeneratedSource,
                ["dryRun"] = true,
                ["allowTopFrameEditing"] = true
            }, cancellationToken);
            Assert.Equal("Ok", dryRun["status"]?.GetValue<string>());
            var applied = await inspector.SendCommandAsync("Debugger.setScriptSource", new JsonObject
            {
                ["scriptId"] = script["scriptId"]!.GetValue<string>(),
                ["scriptSource"] = mutation.GeneratedSource,
                ["dryRun"] = false,
                ["allowTopFrameEditing"] = true
            }, cancellationToken);
            Assert.Equal("Ok", applied["status"]?.GetValue<string>());
            await inspector.SendCommandAsync("Debugger.resume", cancellationToken: cancellationToken);

            JsonObject result = new();
            for (var attempt = 0; attempt < 20; attempt++)
            {
                result = await inspector.SendCommandAsync("Runtime.evaluate", new JsonObject
                {
                    ["expression"] = "[globalThis.mappedResult, globalThis.regeneratedMarker]",
                    ["returnByValue"] = true
                }, cancellationToken);
                if (result["result"]?["value"] is JsonArray values &&
                    values[0]?.GetValue<int>() == 63 && values[1]?.GetValue<bool>() == true)
                {
                    break;
                }
                await Task.Delay(25, cancellationToken);
            }
            var finalValues = Assert.IsType<JsonArray>(result["result"]?["value"]);
            Assert.Equal(63, finalValues[0]?.GetValue<int>());
            Assert.True(finalValues[1]?.GetValue<bool>());
        }
        finally
        {
            if (!process!.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }
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

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<IReadOnlyList<V8InspectorTarget>> WaitForTargetsAsync(
        Uri endpoint,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                var targets = await V8InspectorClient.DiscoverTargetsAsync(endpoint, cancellationToken);
                if (targets.Count > 0) return targets;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
            }
            await Task.Delay(25, cancellationToken);
        }
        throw new TimeoutException("Node inspector did not publish a target.", lastError);
    }
}
