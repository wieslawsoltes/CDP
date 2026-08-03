using System.Text.Json.Nodes;
using System.Threading.Channels;
using Chrome.DevTools.Protocol.Inspector;

namespace Chrome.DevTools.Protocol.Inspector.Tests;

public sealed class WebSceneV8InspectorAcceptanceTests
{
    [Fact(Timeout = 60_000)]
    public async Task WebSceneReactSourceMapBreakpointExposesOriginalStateAndResumes()
    {
        var endpointValue = Environment.GetEnvironmentVariable("WEBSCENE_V8_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            Assert.Skip(
                "Set WEBSCENE_V8_ENDPOINT to an inspect-brk WebScene discovery endpoint "
                + "to run the real React/source-map acceptance lane.");
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var endpoint = new Uri(endpointValue);
        var expectedScript = Environment.GetEnvironmentVariable("WEBSCENE_V8_EXPECTED_SCRIPT")
            ?? "main.js";
        var originalSuffix = Environment.GetEnvironmentVariable("WEBSCENE_V8_ORIGINAL_SOURCE_SUFFIX")
            ?? "/src/main.jsx";
        var breakpointMarker = Environment.GetEnvironmentVariable("WEBSCENE_V8_BREAKPOINT_MARKER")
            ?? "setCount(count + 1);";
        var mutatedBreakpointMarker = Environment.GetEnvironmentVariable("WEBSCENE_V8_MUTATED_BREAKPOINT_MARKER")
            ?? "setCount(count + 2);";
        var triggerExpression = Environment.GetEnvironmentVariable("WEBSCENE_V8_TRIGGER_EXPRESSION")
            ?? "document.querySelector('.counter-row button').click()";
        var resultExpression = Environment.GetEnvironmentVariable("WEBSCENE_V8_RESULT_EXPRESSION")
            ?? "document.querySelector('.count-output').textContent";

        var target = Assert.Single(await WaitForTargetsAsync(endpoint, cancellationToken));
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

        Assert.DoesNotContain(
            Drain(scripts.Reader),
            script => ScriptUrl(script).EndsWith(expectedScript, StringComparison.Ordinal));

        await inspector.SendCommandAsync(
            "Runtime.runIfWaitingForDebugger",
            cancellationToken: cancellationToken);
        var generatedScript = await ReadUntilAsync(
            scripts.Reader,
            script => ScriptUrl(script).EndsWith(expectedScript, StringComparison.Ordinal),
            cancellationToken);
        var generatedUri = new Uri(ScriptUrl(generatedScript));
        var sourceMapUrl = generatedScript["sourceMapURL"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(sourceMapUrl));
        var sourceMapUri = new Uri(generatedUri, sourceMapUrl);
        var sourceMapJson = await ReadSourceMapAsync(sourceMapUri, cancellationToken);
        var sourceMap = V8SourceMap.Parse(sourceMapJson, sourceMapUri);
        var sourceIndex = Enumerable.Range(0, sourceMap.Sources.Count).Single(index =>
            sourceMap.ResolveSourceUrl(index).EndsWith(originalSuffix, StringComparison.Ordinal));
        var originalSource = Assert.IsType<string>(sourceMap.SourcesContent[sourceIndex]);
        var originalLines = originalSource.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var originalLine = Array.FindIndex(originalLines, line => line.Contains(breakpointMarker, StringComparison.Ordinal));
        Assert.True(originalLine >= 0, $"Breakpoint marker '{breakpointMarker}' was not embedded in the source map.");
        var originalColumn = originalLines[originalLine].IndexOf(breakpointMarker, StringComparison.Ordinal);
        var generatedLocation = Assert.IsType<V8SourceMapEntry>(
            sourceMap.FindGeneratedLocation(sourceIndex, originalLine, originalColumn));

        var breakpoint = await inspector.SendCommandAsync("Debugger.setBreakpoint", new JsonObject
        {
            ["location"] = new JsonObject
            {
                ["scriptId"] = generatedScript["scriptId"]!.GetValue<string>(),
                ["lineNumber"] = generatedLocation.GeneratedLine,
                ["columnNumber"] = generatedLocation.GeneratedColumn
            }
        }, cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(breakpoint["breakpointId"]?.GetValue<string>()));

        // Intentionally keep Runtime.evaluate outstanding while the breakpoint is
        // paused. A real debug console must allow step/resume commands to overtake
        // the evaluation that caused the pause on the same Inspector session.
        var triggerTask = inspector.SendCommandAsync("Runtime.evaluate", new JsonObject
        {
            ["expression"] = triggerExpression,
            ["returnByValue"] = true
        }, cancellationToken);

        var pause = await pauses.Reader.ReadAsync(cancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        var frame = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(pause["callFrames"])[0]);
        Assert.Equal("increment", frame["functionName"]?.GetValue<string>());
        var frameLocation = Assert.IsType<JsonObject>(frame["location"]);
        var pausedGeneratedLine = frameLocation["lineNumber"]!.GetValue<int>();
        var pausedGeneratedColumn = frameLocation["columnNumber"]!.GetValue<int>();
        var pausedOriginal = Assert.IsType<V8SourceMapEntry>(
            sourceMap.FindOriginalLocation(pausedGeneratedLine, pausedGeneratedColumn));
        Assert.Equal(sourceIndex, pausedOriginal.SourceIndex);
        Assert.Equal(originalLine, pausedOriginal.OriginalLine);

        var evaluated = await inspector.SendCommandAsync("Debugger.evaluateOnCallFrame", new JsonObject
        {
            ["callFrameId"] = frame["callFrameId"]!.GetValue<string>(),
            ["expression"] = "count",
            ["returnByValue"] = true,
            ["silent"] = false
        }, cancellationToken);
        Assert.Equal(0, evaluated["result"]?["value"]?.GetValue<int>());

        var counterScope = Assert.IsType<JsonArray>(frame["scopeChain"])
            .OfType<JsonObject>()
            .First(scope => scope["name"]?.GetValue<string>() == "Counter");
        var scopeObjectId = counterScope["object"]?["objectId"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(scopeObjectId));
        var scopeProperties = await inspector.SendCommandAsync("Runtime.getProperties", new JsonObject
        {
            ["objectId"] = scopeObjectId,
            ["ownProperties"] = false,
            ["accessorPropertiesOnly"] = false,
            ["generatePreview"] = true
        }, cancellationToken);
        Assert.Contains(
            Assert.IsType<JsonArray>(scopeProperties["result"]).OfType<JsonObject>(),
            property => property["name"]?.GetValue<string>() == "count"
                && property["value"]?["value"]?.GetValue<int>() == 0);
        Assert.Contains(
            Assert.IsType<JsonArray>(scopeProperties["result"]).OfType<JsonObject>(),
            property => property["name"]?.GetValue<string>() == "setCount"
                && property["value"]?["type"]?.GetValue<string>() == "function");

        await inspector.SendCommandAsync("Debugger.stepOver", cancellationToken: cancellationToken);
        var stepPause = await pauses.Reader.ReadAsync(cancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        Assert.NotEmpty(Assert.IsType<JsonArray>(stepPause["callFrames"]));
        await inspector.SendCommandAsync("Debugger.resume", cancellationToken: cancellationToken);
        var additionalPauses = 0;
        for (var attempt = 0; attempt < 200 && !triggerTask.IsCompleted; attempt++)
        {
            while (pauses.Reader.TryRead(out _))
            {
                additionalPauses++;
                await inspector.SendCommandAsync("Debugger.resume", cancellationToken: cancellationToken);
            }
            await Task.Delay(25, cancellationToken);
        }
        try
        {
            await triggerTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        }
        catch (TimeoutException error)
        {
            var stepFrame = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(stepPause["callFrames"])[0]);
            throw new TimeoutException(
                $"Runtime.evaluate remained pending after step/resume at "
                + $"{stepFrame["functionName"]?.GetValue<string>()}; "
                + $"additional pauses: {additionalPauses}.",
                error);
        }
        Assert.Equal(0, additionalPauses);

        JsonObject completedResult = new();
        for (var attempt = 0; attempt < 40; attempt++)
        {
            completedResult = await inspector.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = resultExpression,
                ["returnByValue"] = true
            }, cancellationToken);
            if (completedResult["result"]?["value"]?.GetValue<string>() == "1") break;
            await Task.Delay(25, cancellationToken);
        }
        Assert.Equal("1", completedResult["result"]?["value"]?.GetValue<string>());

        await inspector.SendCommandAsync("Debugger.removeBreakpoint", new JsonObject
        {
            ["breakpointId"] = breakpoint["breakpointId"]!.GetValue<string>()
        }, cancellationToken);
        var generatedSourceResult = await inspector.SendCommandAsync("Debugger.getScriptSource", new JsonObject
        {
            ["scriptId"] = generatedScript["scriptId"]!.GetValue<string>()
        }, cancellationToken);
        var generatedSource = generatedSourceResult["scriptSource"]!.GetValue<string>();
        var editedOriginal = originalSource.Replace(
            breakpointMarker,
            mutatedBreakpointMarker,
            StringComparison.Ordinal);
        Assert.NotEqual(originalSource, editedOriginal);
        var mutation = new V8SourceMutationEngine().CreatePatch(
            sourceMap,
            sourceIndex,
            originalSource,
            editedOriginal,
            generatedSource);
        Assert.True(mutation.CanApply, mutation.Message);
        var dryRun = await inspector.SendCommandAsync("Debugger.setScriptSource", new JsonObject
        {
            ["scriptId"] = generatedScript["scriptId"]!.GetValue<string>(),
            ["scriptSource"] = mutation.GeneratedSource,
            ["dryRun"] = true,
            ["allowTopFrameEditing"] = true
        }, cancellationToken);
        Assert.Equal("Ok", dryRun["status"]?.GetValue<string>());
        var applied = await inspector.SendCommandAsync("Debugger.setScriptSource", new JsonObject
        {
            ["scriptId"] = generatedScript["scriptId"]!.GetValue<string>(),
            ["scriptSource"] = mutation.GeneratedSource,
            ["dryRun"] = false,
            ["allowTopFrameEditing"] = true
        }, cancellationToken);
        Assert.Equal("Ok", applied["status"]?.GetValue<string>());

        await inspector.SendCommandAsync("Runtime.evaluate", new JsonObject
        {
            ["expression"] = triggerExpression,
            ["returnByValue"] = true
        }, cancellationToken);
        JsonObject mutatedResult = new();
        for (var attempt = 0; attempt < 40; attempt++)
        {
            mutatedResult = await inspector.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = resultExpression,
                ["returnByValue"] = true
            }, cancellationToken);
            if (mutatedResult["result"]?["value"]?.GetValue<string>() == "3") break;
            await Task.Delay(25, cancellationToken);
        }
        Assert.Equal("3", mutatedResult["result"]?["value"]?.GetValue<string>());

        await WriteReportAsync(
            Environment.GetEnvironmentVariable("WEBSCENE_V8_REPORT_PATH"),
            new JsonObject
            {
                ["status"] = "passed",
                ["targetTitle"] = target.Title,
                ["targetUrl"] = target.Url,
                ["generatedScript"] = generatedUri.ToString(),
                ["sourceMap"] = sourceMapUri.ToString(),
                ["originalSource"] = sourceMap.ResolveSourceUrl(sourceIndex),
                ["originalLine"] = originalLine + 1,
                ["generatedLine"] = generatedLocation.GeneratedLine + 1,
                ["functionName"] = frame["functionName"]?.GetValue<string>(),
                ["additionalPauses"] = additionalPauses,
                ["closureCount"] = 0,
                ["result"] = "1",
                ["liveEditStatus"] = applied["status"]?.GetValue<string>(),
                ["mutatedMarker"] = mutatedBreakpointMarker,
                ["mutatedResult"] = "3"
            },
            cancellationToken);
    }

    private static string ScriptUrl(JsonObject script) => script["url"]?.GetValue<string>() ?? "";

    private static IReadOnlyList<JsonObject> Drain(ChannelReader<JsonObject> reader)
    {
        var values = new List<JsonObject>();
        while (reader.TryRead(out var value)) values.Add(value);
        return values;
    }

    private static async Task<JsonObject> ReadUntilAsync(
        ChannelReader<JsonObject> reader,
        Func<JsonObject, bool> predicate,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (await reader.WaitToReadAsync(timeout.Token))
        {
            while (reader.TryRead(out var value))
            {
                if (predicate(value)) return value;
            }
        }
        throw new TimeoutException("Expected WebScene V8 script was not parsed.");
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
            await Task.Delay(100, cancellationToken);
        }
        throw new TimeoutException("WebScene V8 Inspector discovery endpoint did not become ready.", lastError);
    }

    private static async Task<string> ReadSourceMapAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.IsFile) return await File.ReadAllTextAsync(uri.LocalPath, cancellationToken);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        return await client.GetStringAsync(uri, cancellationToken);
    }

    private static async Task WriteReportAsync(
        string? path,
        JsonObject report,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(
            fullPath,
            report.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }
}
