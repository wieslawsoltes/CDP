using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Chrome.DevTools.Protocol.Inspector;

namespace Chrome.DevTools.Protocol.Inspector.Tests;

public sealed class V8InspectorClientIntegrationTests
{
    [Fact(Timeout = 30_000)]
    public async Task NodeInspectorSupportsFullDebuggingSession()
    {
        var port = GetAvailablePort();
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v8-debug-target.js");
        Assert.True(File.Exists(fixture), $"Missing V8 fixture: {fixture}");

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
            var endpoint = new Uri($"http://127.0.0.1:{port}");
            var targets = await WaitForTargetsAsync(endpoint);
            var target = Assert.Single(targets);
            Assert.Equal("node", target.Type);
            Assert.False(string.IsNullOrWhiteSpace(target.WebSocketDebuggerUrl));

            await using var inspector = new V8InspectorClient();
            var pauses = Channel.CreateUnbounded<JsonObject>();
            var consoleCalled = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            var subscriberFailure = new TaskCompletionSource<V8InspectorSubscriberExceptionEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connectionClosed = new TaskCompletionSource<V8InspectorConnectionClosedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            inspector.EventReceived += (_, _) => throw new InvalidOperationException("Subscriber isolation probe");
            inspector.EventSubscriberFailed += (_, e) => subscriberFailure.TrySetResult(e);
            inspector.ConnectionClosed += (_, e) => connectionClosed.TrySetResult(e);
            inspector.EventReceived += (_, e) =>
            {
                if (e.Method == "Debugger.paused")
                {
                    pauses.Writer.TryWrite(e.Params);
                }
                else if (e.Method == "Runtime.consoleAPICalled")
                {
                    consoleCalled.TrySetResult(e.Params);
                }
            };

            await inspector.ConnectAsync(new Uri(target.WebSocketDebuggerUrl));
            Assert.Contains("Runtime", inspector.SupportedDomains);
            Assert.Contains("Debugger", inspector.SupportedDomains);
            Assert.Contains("Profiler", inspector.SupportedDomains);
            Assert.Contains("HeapProfiler", inspector.SupportedDomains);

            await inspector.SendCommandAsync("Runtime.enable");
            await inspector.SendCommandAsync("Debugger.enable");
            await inspector.SendCommandAsync("Debugger.setAsyncCallStackDepth", new JsonObject { ["maxDepth"] = 32 });
            await inspector.SendCommandAsync("Debugger.setBreakpointsActive", new JsonObject { ["active"] = true });
            await inspector.SendCommandAsync("Debugger.setBlackboxPatterns", new JsonObject
            {
                ["patterns"] = new JsonArray { "node:internal" },
                ["skipAnonymous"] = false
            });
            await inspector.SendCommandAsync("Profiler.enable");
            await inspector.SendCommandAsync("HeapProfiler.enable");

            var breakpoint = await inspector.SendCommandAsync("Debugger.setBreakpointByUrl", new JsonObject
            {
                ["urlRegex"] = "v8-debug-target\\.js$",
                ["lineNumber"] = 4,
                ["columnNumber"] = 0,
                ["condition"] = "a === 2 && b === 3"
            });
            Assert.False(string.IsNullOrWhiteSpace(breakpoint["breakpointId"]?.GetValue<string>()));

            await inspector.SendCommandAsync("Profiler.start");
            await inspector.SendCommandAsync("Runtime.runIfWaitingForDebugger");
            JsonObject pauseEvent;
            while (true)
            {
                pauseEvent = await pauses.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                var firstFrame = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(pauseEvent["callFrames"])[0]);
                if (firstFrame["functionName"]?.GetValue<string>() == "compute") break;
                await inspector.SendCommandAsync("Debugger.resume");
            }

            var callFrame = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(pauseEvent["callFrames"])[0]);
            Assert.Equal("compute", callFrame["functionName"]?.GetValue<string>());
            Assert.NotNull(pauseEvent["asyncStackTrace"]);
            var location = Assert.IsType<JsonObject>(callFrame["location"]);
            var source = await inspector.SendCommandAsync("Debugger.getScriptSource", new JsonObject
            {
                ["scriptId"] = location["scriptId"]!.GetValue<string>()
            });
            Assert.Contains("function compute", source["scriptSource"]?.GetValue<string>());
            var search = await inspector.SendCommandAsync("Debugger.searchInContent", new JsonObject
            {
                ["scriptId"] = location["scriptId"]!.GetValue<string>(),
                ["query"] = "asyncCompute",
                ["caseSensitive"] = true,
                ["isRegex"] = false
            });
            Assert.NotEmpty(Assert.IsType<JsonArray>(search["result"]));
            var evaluation = await inspector.SendCommandAsync("Debugger.evaluateOnCallFrame", new JsonObject
            {
                ["callFrameId"] = callFrame["callFrameId"]!.GetValue<string>(),
                ["expression"] = "sum",
                ["returnByValue"] = true
            });
            Assert.Equal(5, evaluation["result"]?["value"]?.GetValue<int>());

            var localScope = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(callFrame["scopeChain"])[0]);
            var scopeObject = Assert.IsType<JsonObject>(localScope["object"]);
            var properties = await inspector.SendCommandAsync("Runtime.getProperties", new JsonObject
            {
                ["objectId"] = scopeObject["objectId"]!.GetValue<string>(),
                ["ownProperties"] = false
            });
            Assert.Contains(Assert.IsType<JsonArray>(properties["result"]).OfType<JsonObject>(),
                property => property["name"]?.GetValue<string>() == "sum");

            var editedSource = source["scriptSource"]!.GetValue<string>().Replace("return sum * 2;", "return sum * 3;", StringComparison.Ordinal);
            var liveEdit = await inspector.SendCommandAsync("Debugger.setScriptSource", new JsonObject
            {
                ["scriptId"] = location["scriptId"]!.GetValue<string>(),
                ["scriptSource"] = editedSource,
                ["allowTopFrameEditing"] = true
            });
            Assert.Equal("Ok", liveEdit["status"]?.GetValue<string>());

            await inspector.SendCommandAsync("Debugger.setVariableValue", new JsonObject
            {
                ["scopeNumber"] = 0,
                ["variableName"] = "sum",
                ["newValue"] = new JsonObject { ["value"] = 7 },
                ["callFrameId"] = callFrame["callFrameId"]!.GetValue<string>()
            });
            var changedEvaluation = await inspector.SendCommandAsync("Debugger.evaluateOnCallFrame", new JsonObject
            {
                ["callFrameId"] = callFrame["callFrameId"]!.GetValue<string>(),
                ["expression"] = "sum",
                ["returnByValue"] = true,
                ["throwOnSideEffect"] = true
            });
            Assert.Equal(7, changedEvaluation["result"]?["value"]?.GetValue<int>());

            await inspector.SendCommandAsync("Debugger.resume");
            var consoleEvent = await consoleCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("log", consoleEvent["type"]?.GetValue<string>());

            JsonObject runtimeEvaluation = new();
            for (var attempt = 0; attempt < 20; attempt++)
            {
                runtimeEvaluation = await inspector.SendCommandAsync("Runtime.evaluate", new JsonObject
                {
                    ["expression"] = "globalThis.inspectorResult",
                    ["returnByValue"] = true
                });
                if (runtimeEvaluation["result"]?["value"]?.GetValue<int>() == 15) break;
                await Task.Delay(25);
            }
            // Live-editing the active top frame restarts that activation. The explicit
            // setVariableValue assertion above proves the local edit while paused; the
            // resumed frame then runs the edited source with its original arguments.
            Assert.Equal(15, runtimeEvaluation["result"]?["value"]?.GetValue<int>());

            var isolatedFailure = await subscriberFailure.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(string.IsNullOrWhiteSpace(isolatedFailure.Method));
            Assert.IsType<InvalidOperationException>(isolatedFailure.Exception);
            Assert.True(inspector.IsConnected);

            var profile = await inspector.SendCommandAsync("Profiler.stop");
            Assert.NotEmpty(Assert.IsType<JsonArray>(profile["profile"]?["nodes"]));
            await inspector.SendCommandAsync("HeapProfiler.collectGarbage");

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            var closed = await connectionClosed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(closed.WasRequested);
            Assert.False(inspector.IsConnected);
        }
        finally
        {
            if (!process!.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<IReadOnlyList<V8InspectorTarget>> WaitForTargetsAsync(Uri endpoint)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                var targets = await V8InspectorClient.DiscoverTargetsAsync(endpoint);
                if (targets.Count > 0) return targets;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("Node V8 Inspector discovery endpoint did not become ready.", lastError);
    }
}
