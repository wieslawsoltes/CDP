using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Chrome.DevTools.Protocol;
using Chrome.DevTools.Protocol.Inspector;

namespace Chrome.DevTools.Protocol.Inspector.Tests;

public sealed class V8InspectorClientIntegrationTests
{
    [Fact(Timeout = 60_000)]
    public async Task CdpServiceMaintainsPausedNodeInspectorSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
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

        var service = new CdpService();
        try
        {
            var endpoint = $"http://127.0.0.1:{port}";
            var target = Assert.Single(await WaitForCdpTargetsAsync(service, endpoint, cancellationToken));
            var pauses = Channel.CreateUnbounded<JsonObject>();
            service.EventReceived += (_, e) =>
            {
                if (e.Method == "Debugger.paused") pauses.Writer.TryWrite(e.Params);
            };

            await service.ConnectAsync(endpoint, target, autoResume: false);
            await service.SendCommandAsync("Runtime.enable");
            await service.SendCommandAsync("Debugger.enable");
            await service.SendCommandAsync("Debugger.setBreakpointByUrl", new JsonObject
            {
                ["urlRegex"] = "v8-debug-target\\.js$",
                ["lineNumber"] = 4,
                ["columnNumber"] = 0
            });
            await service.SendCommandAsync("Runtime.runIfWaitingForDebugger");

            JsonObject computePause;
            while (true)
            {
                computePause = await pauses.Reader.ReadAsync(cancellationToken).AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                var frame = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(computePause["callFrames"])[0]);
                if (frame["functionName"]?.GetValue<string>() == "compute") break;
                await service.SendCommandAsync("Debugger.resume");
            }

            Assert.True(service.IsConnected);
            // Cross ClientWebSocket's otherwise-default 30-second heartbeat boundary.
            await Task.Delay(TimeSpan.FromSeconds(32), cancellationToken);
            Assert.True(service.IsConnected);

            var computeFrame = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(computePause["callFrames"])[0]);
            var localScope = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(computeFrame["scopeChain"])[0]);
            var scopeObject = Assert.IsType<JsonObject>(localScope["object"]);
            var localProperties = await service.SendCommandAsync("Runtime.getProperties", new JsonObject
            {
                ["objectId"] = scopeObject["objectId"]!.GetValue<string>(),
                ["ownProperties"] = false,
                ["accessorPropertiesOnly"] = false,
                ["generatePreview"] = true
            });
            var state = Assert.Single(Assert.IsType<JsonArray>(localProperties["result"]).OfType<JsonObject>(),
                property => property["name"]?.GetValue<string>() == "state");
            var stateProperties = await service.SendCommandAsync("Runtime.getProperties", new JsonObject
            {
                ["objectId"] = state["value"]?["objectId"]!.GetValue<string>(),
                ["ownProperties"] = true,
                ["accessorPropertiesOnly"] = false,
                ["generatePreview"] = true
            });
            Assert.Contains(Assert.IsType<JsonArray>(stateProperties["result"]).OfType<JsonObject>(),
                property => property["name"]?.GetValue<string>() == "nested");

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            Assert.True(service.IsConnected);
        }
        finally
        {
            await service.DisconnectAsync();
            if (!process!.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task NodeInspectorPublishesSourceMapAndPausesAtMappedOriginalBreakpoint()
    {
        var port = GetAvailablePort();
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v8-source-map-target.js");
        Assert.True(File.Exists(fixture), $"Missing source-map fixture: {fixture}");

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
            var target = Assert.Single(await WaitForTargetsAsync(new Uri($"http://127.0.0.1:{port}")));
            await using var inspector = new V8InspectorClient();
            var parsedScripts = Channel.CreateUnbounded<JsonObject>();
            var pauses = Channel.CreateUnbounded<JsonObject>();
            inspector.EventReceived += (_, e) =>
            {
                if (e.Method == "Debugger.scriptParsed") parsedScripts.Writer.TryWrite(e.Params);
                if (e.Method == "Debugger.paused") pauses.Writer.TryWrite(e.Params);
            };

            await inspector.ConnectAsync(new Uri(target.WebSocketDebuggerUrl));
            await inspector.SendCommandAsync("Runtime.enable");
            await inspector.SendCommandAsync("Debugger.enable");
            await inspector.SendCommandAsync("Runtime.runIfWaitingForDebugger");

            JsonObject script;
            do
            {
                script = await parsedScripts.Reader.ReadAsync(TestContext.Current.CancellationToken)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            while (!script["url"]!.GetValue<string>().EndsWith("v8-source-map-target.js", StringComparison.Ordinal));

            var sourceMapUrl = script["sourceMapURL"]?.GetValue<string>() ?? "";
            Assert.StartsWith("data:application/json;base64,", sourceMapUrl);
            var sourceMapJson = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(sourceMapUrl[(sourceMapUrl.IndexOf(',') + 1)..]));
            var generatedUri = new Uri(script["url"]!.GetValue<string>());
            var sourceMap = V8SourceMap.Parse(sourceMapJson, generatedUri);
            Assert.EndsWith("/src/mapped-target.ts", sourceMap.ResolveSourceUrl(0));
            Assert.Contains("value: number", Assert.Single(sourceMap.SourcesContent));

            var mapped = Assert.IsType<V8SourceMapEntry>(sourceMap.FindGeneratedLocation(0, 1));
            var initialPause = await pauses.Reader.ReadAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.NotEmpty(Assert.IsType<JsonArray>(initialPause["callFrames"]));
            var breakpoint = await inspector.SendCommandAsync("Debugger.setBreakpoint", new JsonObject
            {
                ["location"] = new JsonObject
                {
                    ["scriptId"] = script["scriptId"]!.GetValue<string>(),
                    ["lineNumber"] = mapped.GeneratedLine,
                    ["columnNumber"] = mapped.GeneratedColumn
                }
            });
            Assert.False(string.IsNullOrWhiteSpace(breakpoint["breakpointId"]?.GetValue<string>()));

            await inspector.SendCommandAsync("Debugger.resume");
            var mappedPause = await pauses.Reader.ReadAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var frame = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(mappedPause["callFrames"])[0]);
            Assert.Equal("mappedCompute", frame["functionName"]?.GetValue<string>());
            var location = Assert.IsType<JsonObject>(frame["location"]);
            Assert.Equal(mapped.GeneratedLine, location["lineNumber"]?.GetValue<int>());
            var value = await inspector.SendCommandAsync("Debugger.evaluateOnCallFrame", new JsonObject
            {
                ["callFrameId"] = frame["callFrameId"]!.GetValue<string>(),
                ["expression"] = "value",
                ["returnByValue"] = true
            });
            Assert.Equal(21, value["result"]?["value"]?.GetValue<int>());

            var generatedSource = await inspector.SendCommandAsync("Debugger.getScriptSource", new JsonObject
            {
                ["scriptId"] = script["scriptId"]!.GetValue<string>()
            });
            var originalSource = Assert.Single(sourceMap.SourcesContent)!;
            var editedOriginal = originalSource.Replace("value * 2", "value * 3", StringComparison.Ordinal);
            var mutation = new V8SourceMutationEngine().CreatePatch(
                sourceMap,
                0,
                originalSource,
                editedOriginal,
                generatedSource["scriptSource"]!.GetValue<string>());
            Assert.True(mutation.CanApply, mutation.Message);
            var dryRun = await inspector.SendCommandAsync("Debugger.setScriptSource", new JsonObject
            {
                ["scriptId"] = script["scriptId"]!.GetValue<string>(),
                ["scriptSource"] = mutation.GeneratedSource,
                ["dryRun"] = true,
                ["allowTopFrameEditing"] = true
            });
            Assert.Equal("Ok", dryRun["status"]?.GetValue<string>());
            var applied = await inspector.SendCommandAsync("Debugger.setScriptSource", new JsonObject
            {
                ["scriptId"] = script["scriptId"]!.GetValue<string>(),
                ["scriptSource"] = mutation.GeneratedSource,
                ["dryRun"] = false,
                ["allowTopFrameEditing"] = true
            });
            Assert.Equal("Ok", applied["status"]?.GetValue<string>());
            await inspector.SendCommandAsync("Debugger.resume");

            JsonObject mappedResult = new();
            for (var attempt = 0; attempt < 20; attempt++)
            {
                mappedResult = await inspector.SendCommandAsync("Runtime.evaluate", new JsonObject
                {
                    ["expression"] = "globalThis.mappedResult",
                    ["returnByValue"] = true
                });
                if (mappedResult["result"]?["value"]?.GetValue<int>() == 63) break;
                await Task.Delay(25, TestContext.Current.CancellationToken);
            }
            Assert.Equal(63, mappedResult["result"]?["value"]?.GetValue<int>());
        }
        finally
        {
            if (!process!.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact(Timeout = 65_000)]
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
            // Cross ClientWebSocket's otherwise-default 30-second heartbeat boundary.
            await Task.Delay(TimeSpan.FromSeconds(32), TestContext.Current.CancellationToken);
            Assert.True(inspector.IsConnected);
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

            var stateProperty = Assert.Single(Assert.IsType<JsonArray>(properties["result"]).OfType<JsonObject>(),
                property => property["name"]?.GetValue<string>() == "state");
            var stateObjectId = stateProperty["value"]?["objectId"]?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(stateObjectId));
            var stateProperties = await inspector.SendCommandAsync("Runtime.getProperties", new JsonObject
            {
                ["objectId"] = stateObjectId,
                ["ownProperties"] = true,
                ["accessorPropertiesOnly"] = false,
                ["generatePreview"] = true
            });
            var stateDescriptors = Assert.IsType<JsonArray>(stateProperties["result"]).OfType<JsonObject>().ToArray();
            var nestedDescriptor = Assert.Single(stateDescriptors, property => property["name"]?.GetValue<string>() == "nested");
            var selfDescriptor = Assert.Single(stateDescriptors, property => property["name"]?.GetValue<string>() == "self");
            var riskyDescriptor = Assert.Single(stateDescriptors, property => property["name"]?.GetValue<string>() == "risky");
            var selfIdentity = await inspector.SendCommandAsync("Runtime.callFunctionOn", new JsonObject
            {
                ["objectId"] = selfDescriptor["value"]?["objectId"]?.GetValue<string>(),
                ["functionDeclaration"] = "function (other) { return this === other; }",
                ["arguments"] = new JsonArray { new JsonObject { ["objectId"] = stateObjectId } },
                ["silent"] = true,
                ["returnByValue"] = true,
                ["throwOnSideEffect"] = true
            });
            Assert.True(selfIdentity["result"]?["value"]?.GetValue<bool>());
            Assert.Null(riskyDescriptor["value"]);
            Assert.NotNull(riskyDescriptor["get"]);

            var nestedProperties = await inspector.SendCommandAsync("Runtime.getProperties", new JsonObject
            {
                ["objectId"] = nestedDescriptor["value"]?["objectId"]?.GetValue<string>(),
                ["ownProperties"] = true
            });
            Assert.Contains(Assert.IsType<JsonArray>(nestedProperties["result"]).OfType<JsonObject>(),
                property => property["name"]?.GetValue<string>() == "value" &&
                    property["value"]?["value"]?.GetValue<int>() == 5);

            var groupedObject = await inspector.SendCommandAsync("Debugger.evaluateOnCallFrame", new JsonObject
            {
                ["callFrameId"] = callFrame["callFrameId"]!.GetValue<string>(),
                ["expression"] = "state",
                ["objectGroup"] = "v8-inspector-integration",
                ["returnByValue"] = false
            });
            var groupedObjectId = groupedObject["result"]?["objectId"]?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(groupedObjectId));
            await inspector.SendCommandAsync("Runtime.releaseObjectGroup", new JsonObject
            {
                ["objectGroup"] = "v8-inspector-integration"
            });
            await Assert.ThrowsAsync<V8InspectorProtocolException>(() => inspector.SendCommandAsync("Runtime.getProperties", new JsonObject
            {
                ["objectId"] = groupedObjectId
            }));

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

    private static async Task<IReadOnlyList<TargetItem>> WaitForCdpTargetsAsync(
        CdpService service,
        string endpoint,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                var targets = await service.GetTargetsAsync(endpoint);
                if (targets.Count > 0) return targets;
            }
            catch (Exception ex) when (ex.InnerException is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
            }
            await Task.Delay(100, cancellationToken);
        }
        throw new TimeoutException("Node V8 Inspector discovery endpoint did not become ready.", lastError);
    }
}
