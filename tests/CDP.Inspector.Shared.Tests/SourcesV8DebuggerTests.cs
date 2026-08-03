using System.ComponentModel;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using Chrome.DevTools.Protocol.Inspector;

namespace Avalonia.Diagnostics.Cdp.Tests;

public sealed class SourcesV8DebuggerTests
{
    [AvaloniaFact]
    public async Task V8EventsPopulateScriptsFramesScopesAndEvaluation()
    {
        var service = new V8FakeCdpService();
        var viewModel = new SourcesViewModel(service);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);

        service.Raise("Debugger.scriptParsed", new JsonObject
        {
            ["scriptId"] = "42",
            ["url"] = "file:///app/example.js",
            ["startLine"] = 0,
            ["startColumn"] = 0,
            ["endLine"] = 10,
            ["endColumn"] = 0,
            ["executionContextId"] = 1,
            ["hash"] = "abc"
        });
        await WaitUntilAsync(() => viewModel.RuntimeScripts.Count == 1);
        Assert.Equal("example.js", viewModel.RuntimeScripts[0].DisplayName);

        service.Raise("Debugger.paused", new JsonObject
        {
            ["reason"] = "breakpoint",
            ["callFrames"] = new JsonArray
            {
                new JsonObject
                {
                    ["callFrameId"] = "frame-1",
                    ["functionName"] = "compute",
                    ["url"] = "file:///app/example.js",
                    ["location"] = new JsonObject
                    {
                        ["scriptId"] = "42",
                        ["lineNumber"] = 4,
                        ["columnNumber"] = 2
                    },
                    ["returnValue"] = new JsonObject { ["type"] = "number", ["value"] = 5 },
                    ["scopeChain"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "local",
                            ["object"] = new JsonObject { ["objectId"] = "scope-1", ["type"] = "object" }
                        }
                    }
                }
            },
            ["asyncStackTrace"] = new JsonObject
            {
                ["description"] = "await compute",
                ["callFrames"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["functionName"] = "scheduleCompute",
                        ["scriptId"] = "42",
                        ["url"] = "file:///app/example.js",
                        ["lineNumber"] = 1,
                        ["columnNumber"] = 3
                    }
                }
            }
        });

        await WaitUntilAsync(() => viewModel.ScopeVariables.Count == 1);
        var localScope = Assert.Single(viewModel.ScopeVariables);
        Assert.True(localScope.IsScopeGroup);
        Assert.Equal("Local", localScope.DisplayName);
        Assert.DoesNotContain(service.Commands, command => command.Method == "Runtime.getProperties");
        await localScope.EnsureChildrenLoadedAsync();
        Assert.Contains(service.Commands, command => command.Method == "Runtime.getProperties" &&
            command.Parameters?["objectId"]?.GetValue<string>() == "scope-1");
        var sumVariable = Assert.Single(localScope.Children);
        Assert.True(viewModel.IsDebuggerPaused);
        Assert.Equal("breakpoint", viewModel.PauseReason);
        Assert.Equal(5, viewModel.ActiveDebugLine);
        Assert.Equal("sum", sumVariable.DisplayName);
        Assert.Equal("5", sumVariable.Value);
        Assert.Equal(3, viewModel.CallFrames.Count);
        Assert.True(viewModel.CallFrames[1].IsAsyncBoundary);
        Assert.Equal("scheduleCompute", viewModel.CallFrames[2].FunctionName);

        viewModel.DebuggerEvaluationExpression = "sum * 2";
        await Task.Delay(10);
        Assert.True(viewModel.EvaluateOnCallFrameCommand.CanExecute(null));
        viewModel.EvaluateOnCallFrameCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.DebuggerEvaluationResult == "10");
        Assert.Contains(service.Commands, command => command.Method == "Debugger.evaluateOnCallFrame");

        var hoverValue = await viewModel.EvaluateHoverAsync("sum");
        Assert.Equal("10", hoverValue);
        var hover = service.Commands.Last(command => command.Method == "Debugger.evaluateOnCallFrame");
        Assert.True(hover.Parameters?["throwOnSideEffect"]?.GetValue<bool>());
        Assert.Contains(service.Commands, command => command.Method == "Runtime.releaseObjectGroup" &&
            command.Parameters?["objectGroup"]?.GetValue<string>() == "cdp-inspector-hover");

        viewModel.DebuggerEvaluationExpression = "sum * 3";
        Assert.True(viewModel.SetReturnValueCommand.CanExecute(null));
        await viewModel.SetReturnValueAsync();
        var setReturnValue = Assert.Single(service.Commands, command => command.Method == "Debugger.setReturnValue");
        Assert.Equal(10, setReturnValue.Parameters?["newValue"]?["value"]?.GetValue<int>());
        Assert.Equal("Return value = 10", viewModel.DebuggerEvaluationResult);
        Assert.Contains(service.Commands, command => command.Method == "Runtime.releaseObjectGroup" &&
            command.Parameters?["objectGroup"]?.GetValue<string>() == "cdp-inspector-return-value");

        viewModel.SelectedScopeVariable = sumVariable;
        viewModel.NewVariableValueExpression = "42";
        viewModel.SetVariableValueCommand.Execute(null);
        await WaitUntilAsync(() => service.Commands.Any(command => command.Method == "Debugger.setVariableValue"));
        var setVariable = service.Commands.Last(command => command.Method == "Debugger.setVariableValue");
        Assert.Equal(0, setVariable.Parameters?["scopeNumber"]?.GetValue<int>());
        Assert.Equal("sum", setVariable.Parameters?["variableName"]?.GetValue<string>());
        Assert.Equal(10, setVariable.Parameters?["newValue"]?["value"]?.GetValue<int>());

        viewModel.NewWatchExpression = "sum";
        viewModel.AddWatchExpressionCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.WatchExpressions.Count == 1 && viewModel.WatchExpressions[0].Value == "10");
        Assert.Equal("sum", viewModel.WatchExpressions[0].Expression);

        viewModel.RestartFrameCommand.Execute(null);
        await WaitUntilAsync(() => service.Commands.Any(command => command.Method == "Debugger.restartFrame"));
    }

    [AvaloniaFact]
    public async Task ScopeVariablesExpandNestedCircularAccessorPrivateAndInternalProperties()
    {
        var service = new V8FakeCdpService { ProvideNestedScopeValues = true };
        var viewModel = new SourcesViewModel(service);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);

        service.Raise("Debugger.paused", new JsonObject
        {
            ["reason"] = "breakpoint",
            ["callFrames"] = new JsonArray
            {
                new JsonObject
                {
                    ["callFrameId"] = "frame-nested",
                    ["functionName"] = "inspectState",
                    ["url"] = "file:///app/example.js",
                    ["location"] = new JsonObject { ["scriptId"] = "42", ["lineNumber"] = 4, ["columnNumber"] = 0 },
                    ["scopeChain"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "local",
                            ["object"] = new JsonObject { ["objectId"] = "scope-nested", ["type"] = "object" }
                        }
                    }
                }
            }
        });

        await WaitUntilAsync(() => viewModel.ScopeVariables.Count == 1);
        var localScope = Assert.Single(viewModel.ScopeVariables);
        await localScope.EnsureChildrenLoadedAsync();
        Assert.Equal(2, localScope.Children.Count);
        var state = localScope.Children.Single(variable => variable.Name == "state");
        Assert.True(state.IsExpandable);
        await state.EnsureChildrenLoadedAsync();

        Assert.Equal(5, state.Children.Count);
        var nested = state.Children.Single(variable => variable.Name == "nested");
        Assert.True(nested.IsExpandable);
        await nested.EnsureChildrenLoadedAsync();
        Assert.Equal("5", Assert.Single(nested.Children).Value);

        var self = state.Children.Single(variable => variable.Name == "self");
        Assert.True(self.IsCircular);
        Assert.False(self.IsExpandable);
        Assert.Equal("[Circular]", self.Value);

        var risky = state.Children.Single(variable => variable.Name == "risky");
        Assert.True(risky.IsAccessor);
        Assert.False(risky.IsExpandable);
        Assert.Equal("(…)", risky.Value);
        Assert.True(state.Children.Single(variable => variable.Name == "#secret").IsPrivate);
        Assert.True(state.Children.Single(variable => variable.Name == "[[Prototype]]").IsInternal);

        service.Raise("Debugger.resumed", new JsonObject());
        await WaitUntilAsync(() => viewModel.ScopeVariables.Count == 0);
        Assert.Contains(service.Commands, command => command.Method == "Runtime.releaseObjectGroup");
    }

    [AvaloniaFact]
    public async Task RuntimeSourceCanBeLiveEditedAndSearched()
    {
        var service = new V8FakeCdpService();
        var viewModel = new SourcesViewModel(service);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);

        service.Raise("Debugger.scriptParsed", new JsonObject
        {
            ["scriptId"] = "42",
            ["url"] = "file:///app/example.js",
            ["startLine"] = 0,
            ["startColumn"] = 0,
            ["endLine"] = 10,
            ["endColumn"] = 0,
            ["executionContextId"] = 1,
            ["hash"] = "abc"
        });
        await WaitUntilAsync(() => viewModel.RuntimeScripts.Count == 1);

        viewModel.SelectedRuntimeScript = viewModel.RuntimeScripts[0];
        await WaitUntilAsync(() => viewModel.SelectedFileContent.Contains("compute", StringComparison.Ordinal));
        Assert.True(viewModel.CanEditCurrentSource);

        const string editedSource = "function compute() { return 42; }";
        await viewModel.ApplySourceChangesAsync(editedSource);
        Assert.Equal("Live edit applied", viewModel.LiveEditStatus);
        Assert.Equal(editedSource, viewModel.SelectedFileContent);
        var liveEdits = service.Commands.Where(command => command.Method == "Debugger.setScriptSource").ToArray();
        Assert.Equal(2, liveEdits.Length);
        Assert.True(liveEdits[0].Parameters?["dryRun"]?.GetValue<bool>());
        Assert.False(liveEdits[1].Parameters?["dryRun"]?.GetValue<bool>());
        Assert.All(liveEdits, liveEdit =>
        {
            Assert.Equal("42", liveEdit.Parameters?["scriptId"]?.GetValue<string>());
            Assert.Equal(editedSource, liveEdit.Parameters?["scriptSource"]?.GetValue<string>());
        });

        viewModel.SearchQuery = "return";
        await viewModel.SearchAsync();
        await WaitUntilAsync(() => viewModel.SearchResults.Count == 1);
        Assert.Equal(3, viewModel.SearchResults[0].LineNumber);
        Assert.Equal("  return 42;", viewModel.SearchResults[0].LineContent);
        Assert.Contains(service.Commands, command => command.Method == "Debugger.searchInContent");
    }

    [AvaloniaFact]
    public async Task OptionalDebuggerActionsDoNotDisableSourcesDebugger()
    {
        var service = new V8FakeCdpService { RejectOptionalDebuggerCommands = true };
        var viewModel = new SourcesViewModel(service);

        service.IsConnected = true;

        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);
        Assert.Equal("Debugger ready (node)", viewModel.DebuggerStatusText);
        Assert.Contains(service.Commands, command => command.Method == "Debugger.enable");
        Assert.Contains(service.Commands, command => command.Method == "Debugger.setAsyncCallStackDepth");
        Assert.Contains(service.Commands, command => command.Method == "Debugger.setPauseOnExceptions");
    }

    [AvaloniaFact]
    public async Task BreakpointsSupportConditionsLogpointsDisableAndReconnect()
    {
        var service = new V8FakeCdpService();
        var viewModel = new SourcesViewModel(service);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);

        service.Raise("Debugger.scriptParsed", new JsonObject
        {
            ["scriptId"] = "42",
            ["url"] = "file:///app/example.js",
            ["endLine"] = 10
        });
        await WaitUntilAsync(() => viewModel.RuntimeScripts.Count == 1);
        viewModel.SelectedRuntimeScript = viewModel.RuntimeScripts[0];
        await WaitUntilAsync(() => viewModel.SelectedFileContent.Contains("compute", StringComparison.Ordinal));

        viewModel.BreakpointKind = V8BreakpointKinds.Conditional;
        viewModel.BreakpointCondition = "sum > 4";
        await viewModel.ToggleBreakpointAsync(5);

        var breakpoint = Assert.Single(viewModel.V8Breakpoints);
        Assert.True(breakpoint.IsResolved);
        Assert.Equal(V8BreakpointKinds.Conditional, breakpoint.Kind);
        var conditional = Assert.Single(service.Commands, command => command.Method == "Debugger.setBreakpointByUrl");
        Assert.Equal("sum > 4", conditional.Parameters?["condition"]?.GetValue<string>());

        viewModel.SelectedBreakpoint = breakpoint;
        viewModel.BreakpointKind = V8BreakpointKinds.Logpoint;
        viewModel.BreakpointLogMessage = "sum = {sum}, literal {{brace}}";
        viewModel.UpdateSelectedBreakpointCommand.Execute(null);
        await WaitUntilAsync(() => service.Commands.Count(command => command.Method == "Debugger.setBreakpointByUrl") == 2);
        var logpoint = service.Commands.Last(command => command.Method == "Debugger.setBreakpointByUrl");
        var logCondition = logpoint.Parameters?["condition"]?.GetValue<string>() ?? "";
        Assert.Contains("console.log", logCondition);
        Assert.Contains("(sum)", logCondition);
        Assert.Contains("literal {brace}", logCondition);
        Assert.EndsWith(", false", logCondition);

        viewModel.ToggleSelectedBreakpointEnabledCommand.Execute(null);
        await WaitUntilAsync(() => !breakpoint.IsEnabled);
        Assert.False(breakpoint.IsResolved);
        Assert.Contains(service.Commands, command => command.Method == "Debugger.removeBreakpoint");

        viewModel.ToggleSelectedBreakpointEnabledCommand.Execute(null);
        await WaitUntilAsync(() => breakpoint.IsEnabled && breakpoint.IsResolved);
        var bindingCount = service.Commands.Count(command => command.Method == "Debugger.setBreakpointByUrl");

        service.IsConnected = false;
        await WaitUntilAsync(() => !viewModel.IsDebuggerEnabled && string.IsNullOrEmpty(breakpoint.BreakpointId));
        Assert.Single(viewModel.V8Breakpoints);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled && breakpoint.IsResolved &&
            service.Commands.Count(command => command.Method == "Debugger.setBreakpointByUrl") > bindingCount);
    }

    [AvaloniaFact]
    public async Task BlackboxPatternsAreAppliedToV8Debugger()
    {
        var service = new V8FakeCdpService();
        var viewModel = new SourcesViewModel(service);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);

        viewModel.NewBlackboxPattern = "/node_modules/";
        viewModel.AddBlackboxPatternCommand.Execute(null);
        await WaitUntilAsync(() => service.Commands.Count(command => command.Method == "Debugger.setBlackboxPatterns") >= 2);

        var command = service.Commands.Last(item => item.Method == "Debugger.setBlackboxPatterns");
        var patterns = Assert.IsType<JsonArray>(command.Parameters?["patterns"]);
        Assert.Equal("/node_modules/", Assert.Single(patterns)?.GetValue<string>());
        Assert.False(command.Parameters?["skipAnonymous"]?.GetValue<bool>());

        viewModel.SkipAnonymousScripts = true;
        await WaitUntilAsync(() => service.Commands.Last(item => item.Method == "Debugger.setBlackboxPatterns")
            .Parameters?["skipAnonymous"]?.GetValue<bool>() == true);
        Assert.Contains("anonymous", viewModel.BlackboxStatusText);
    }

    [AvaloniaFact]
    public async Task SourceMappedOriginalsNavigateAndBindBreakpointsToGeneratedCode()
    {
        var service = new V8FakeCdpService
        {
            GeneratedScriptSource = "export const App = () => <main>Hello</main>;\nexport const vendor = true;"
        };
        var viewModel = new SourcesViewModel(service);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);
        const string sourceMapJson = """
            {
              "version": 3,
              "sourceRoot": "../src",
              "sources": ["App.tsx", "vendor.ts"],
              "sourcesContent": ["export const App = () => <main>Hello</main>;", "export const vendor = true;"],
              "names": [],
              "ignoreList": [1],
              "mappings": "AAAA;ACAA"
            }
            """;

        service.Raise("Debugger.scriptParsed", new JsonObject
        {
            ["scriptId"] = "42",
            ["url"] = "file:///app/dist/bundle.js",
            ["sourceMapURL"] = $"data:application/json,{Uri.EscapeDataString(sourceMapJson)}",
            ["endLine"] = 2
        });

        await WaitUntilAsync(() => viewModel.RuntimeScripts.Count == 3);
        var app = Assert.Single(viewModel.RuntimeScripts, script => script.Url == "file:///app/src/App.tsx");
        var vendor = Assert.Single(viewModel.RuntimeScripts, script => script.Url == "file:///app/src/vendor.ts");
        Assert.True(app.IsOriginalSource);
        Assert.False(app.IsIgnoredSource);
        Assert.True(vendor.IsIgnoredSource);

        await WaitUntilAsync(() => service.Commands.Any(command => command.Method == "Debugger.setBlackboxedRanges"));
        var blackbox = service.Commands.Last(command => command.Method == "Debugger.setBlackboxedRanges");
        Assert.Equal("42", blackbox.Parameters?["scriptId"]?.GetValue<string>());
        var position = Assert.Single(Assert.IsType<JsonArray>(blackbox.Parameters?["positions"]));
        Assert.Equal(1, position?["lineNumber"]?.GetValue<int>());
        Assert.Equal(0, position?["columnNumber"]?.GetValue<int>());

        viewModel.SelectedRuntimeScript = app;
        await WaitUntilAsync(() => viewModel.SelectedFileContent.Contains("<main>Hello</main>", StringComparison.Ordinal));
        await viewModel.ToggleBreakpointAsync(1);

        var breakpoint = Assert.Single(viewModel.V8Breakpoints);
        Assert.Equal("file:///app/src/App.tsx", breakpoint.Url);
        Assert.Equal("file:///app/dist/bundle.js", breakpoint.BindingUrl);
        Assert.Equal(0, breakpoint.LineNumber);
        Assert.Equal(0, breakpoint.ColumnNumber);
        var bind = service.Commands.Last(command => command.Method == "Debugger.setBreakpointByUrl");
        Assert.Equal("file:///app/dist/bundle.js", bind.Parameters?["url"]?.GetValue<string>());
        Assert.Equal(0, bind.Parameters?["lineNumber"]?.GetValue<int>());

        Assert.True(viewModel.CanEditCurrentSource);
        const string editedOriginal = "export const App = () => <main>Updated</main>;";
        await viewModel.ApplySourceChangesAsync(editedOriginal);
        Assert.Equal("Source-mapped live edit applied", viewModel.LiveEditStatus);
        Assert.Equal(editedOriginal, viewModel.SelectedFileContent);
        var liveEdits = service.Commands.Where(command => command.Method == "Debugger.setScriptSource").ToArray();
        Assert.Equal(2, liveEdits.Length);
        Assert.True(liveEdits[0].Parameters?["dryRun"]?.GetValue<bool>());
        Assert.False(liveEdits[1].Parameters?["dryRun"]?.GetValue<bool>());
        Assert.All(liveEdits, liveEdit =>
        {
            Assert.Equal("42", liveEdit.Parameters?["scriptId"]?.GetValue<string>());
            Assert.Contains("<main>Updated</main>", liveEdit.Parameters?["scriptSource"]?.GetValue<string>());
        });
        Assert.True(breakpoint.IsResolved);
        Assert.True(service.Commands.Count(command => command.Method == "Debugger.setBreakpointByUrl") >= 2);
    }

    [AvaloniaFact]
    public async Task CompilerAdapterRegeneratesTransformedOriginalThroughV8ValidationAndApply()
    {
        const string original = "const value: number = 2;\nconsole.log(value);\n";
        const string edited = "const value: number = 3;\nconsole.log(value);\nconsole.log('again');\n";
        const string generated = "const value = 2;\nconsole.log(value);\n";
        const string regenerated = "const value = 3;\nconsole.log(value);\nconsole.log('again');\n";
        var service = new V8FakeCdpService { GeneratedScriptSource = generated };
        var regenerator = new FakeSourceRegenerator(regenerated);
        var viewModel = new SourcesViewModel(service, [regenerator]);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);
        var sourceMapJson = $$"""
            {
              "version": 3,
              "sources": ["source.ts"],
              "sourcesContent": [{{System.Text.Json.JsonSerializer.Serialize(original)}}],
              "names": [],
              "mappings": "AAAA;AACA"
            }
            """;
        service.Raise("Debugger.scriptParsed", new JsonObject
        {
            ["scriptId"] = "42",
            ["url"] = "file:///app/source.js",
            ["sourceMapURL"] = $"data:application/json,{Uri.EscapeDataString(sourceMapJson)}",
            ["endLine"] = 2
        });

        await WaitUntilAsync(() => viewModel.RuntimeScripts.Count == 2);
        viewModel.SelectedRuntimeScript = Assert.Single(viewModel.RuntimeScripts, script => script.IsOriginalSource);
        await WaitUntilAsync(() => viewModel.SelectedFileContent == original);
        await viewModel.ApplySourceChangesAsync(edited);

        Assert.Equal("Regenerated source live edit applied", viewModel.LiveEditStatus);
        Assert.Equal(edited, viewModel.SelectedFileContent);
        Assert.Equal(edited, regenerator.Request!.EditedSource);
        var liveEdits = service.Commands.Where(command => command.Method == "Debugger.setScriptSource").ToArray();
        Assert.Equal(2, liveEdits.Length);
        Assert.All(liveEdits, liveEdit =>
            Assert.Equal(regenerated, liveEdit.Parameters?["scriptSource"]?.GetValue<string>()));
    }

    [AvaloniaFact]
    public async Task RunToCursorMapsOriginalSourceAndSnapsToPossibleBreakpoint()
    {
        var service = new V8FakeCdpService();
        var viewModel = new SourcesViewModel(service);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);
        const string sourceMapJson = """
            {
              "version": 3,
              "sources": ["source.ts"],
              "sourcesContent": ["function compute(value: number) {\n  return value * 2;\n}\n"],
              "names": [],
              "mappings": "AAAA;AACA;AACA"
            }
            """;
        service.Raise("Debugger.scriptParsed", new JsonObject
        {
            ["scriptId"] = "42",
            ["url"] = "file:///app/source.js",
            ["sourceMapURL"] = $"data:application/json,{Uri.EscapeDataString(sourceMapJson)}",
            ["endLine"] = 3
        });
        await WaitUntilAsync(() => viewModel.RuntimeScripts.Count == 2);
        var original = Assert.Single(viewModel.RuntimeScripts, script => script.IsOriginalSource);
        viewModel.SelectedRuntimeScript = original;
        service.Raise("Debugger.paused", new JsonObject
        {
            ["reason"] = "breakpoint",
            ["callFrames"] = new JsonArray
            {
                new JsonObject
                {
                    ["callFrameId"] = "frame-run-to-cursor",
                    ["functionName"] = "compute",
                    ["url"] = "file:///app/source.js",
                    ["location"] = new JsonObject { ["scriptId"] = "42", ["lineNumber"] = 0, ["columnNumber"] = 0 },
                    ["scopeChain"] = new JsonArray()
                }
            }
        });
        await WaitUntilAsync(() => viewModel.IsDebuggerPaused);
        viewModel.SelectedRuntimeScript = original;

        Assert.True(viewModel.RunToCursorCommand.CanExecute(2));
        await viewModel.RunToCursorAsync(2);

        var possible = Assert.Single(service.Commands, command => command.Method == "Debugger.getPossibleBreakpoints");
        Assert.Equal("42", possible.Parameters?["start"]?["scriptId"]?.GetValue<string>());
        Assert.Equal(1, possible.Parameters?["start"]?["lineNumber"]?.GetValue<int>());
        var run = Assert.Single(service.Commands, command => command.Method == "Debugger.continueToLocation");
        Assert.Equal("42", run.Parameters?["location"]?["scriptId"]?.GetValue<string>());
        Assert.Equal(1, run.Parameters?["location"]?["lineNumber"]?.GetValue<int>());
        Assert.Equal(7, run.Parameters?["location"]?["columnNumber"]?.GetValue<int>());
        Assert.Equal("any", run.Parameters?["targetCallFrames"]?.GetValue<string>());
        Assert.Equal("Running to source.ts:2", viewModel.LiveEditStatus);
    }

    [AvaloniaFact]
    public async Task FunctionBreakpointEvaluatesExpressionAndBindsByObjectId()
    {
        var service = new V8FakeCdpService();
        var viewModel = new SourcesViewModel(service);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);
        viewModel.FunctionBreakpointExpression = "computeReturnValue";
        viewModel.BreakpointCondition = "arguments[0] > 0";

        Assert.True(viewModel.AddFunctionBreakpointCommand.CanExecute(null));
        await viewModel.AddFunctionBreakpointAsync();

        var evaluation = Assert.Single(service.Commands, command => command.Method == "Runtime.evaluate");
        Assert.Equal("computeReturnValue", evaluation.Parameters?["expression"]?.GetValue<string>());
        var bind = Assert.Single(service.Commands, command => command.Method == "Debugger.setBreakpointOnFunctionCall");
        Assert.Equal("function-object-1", bind.Parameters?["objectId"]?.GetValue<string>());
        Assert.Equal("arguments[0] > 0", bind.Parameters?["condition"]?.GetValue<string>());
        var breakpoint = Assert.Single(viewModel.V8Breakpoints);
        Assert.Equal(V8BreakpointKinds.FunctionCall, breakpoint.Kind);
        Assert.True(breakpoint.IsResolved);
        Assert.Contains("computeReturnValue", breakpoint.DisplayName);
    }

    [AvaloniaFact]
    public async Task InstrumentationBreakpointBindsAndUsesFriendlyDisplayName()
    {
        var service = new V8FakeCdpService();
        var viewModel = new SourcesViewModel(service);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);

        Assert.Equal(V8InstrumentationBreakpoints.BeforeSourceMappedScriptDisplayName,
            viewModel.InstrumentationBreakpoint);
        Assert.True(viewModel.AddInstrumentationBreakpointCommand.CanExecute(null));
        await viewModel.AddInstrumentationBreakpointAsync();

        var bind = Assert.Single(service.Commands,
            command => command.Method == "Debugger.setInstrumentationBreakpoint");
        Assert.Equal(V8InstrumentationBreakpoints.BeforeScriptWithSourceMapExecution,
            bind.Parameters?["instrumentation"]?.GetValue<string>());
        var breakpoint = Assert.Single(viewModel.V8Breakpoints);
        Assert.Equal(V8BreakpointKinds.Instrumentation, breakpoint.Kind);
        Assert.True(breakpoint.IsResolved);
        Assert.Contains("Before source-mapped script", breakpoint.DisplayName);
    }

    [AvaloniaFact]
    public async Task ScriptParsedResolvedBreakpointsUpdateEditorState()
    {
        var service = new V8FakeCdpService();
        var viewModel = new SourcesViewModel(service);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);

        service.Raise("Debugger.scriptParsed", new JsonObject
        {
            ["scriptId"] = "42",
            ["url"] = "file:///app/example.js",
            ["endLine"] = 10
        });
        await WaitUntilAsync(() => viewModel.RuntimeScripts.Count == 1);
        viewModel.SelectedRuntimeScript = viewModel.RuntimeScripts[0];
        await viewModel.ToggleBreakpointAsync(5);
        var breakpoint = Assert.Single(viewModel.V8Breakpoints);
        breakpoint.IsResolved = false;

        service.Raise("Debugger.scriptParsed", new JsonObject
        {
            ["scriptId"] = "43",
            ["url"] = "file:///app/late.js",
            ["resolvedBreakpoints"] = new JsonArray
            {
                new JsonObject
                {
                    ["breakpointId"] = breakpoint.BreakpointId,
                    ["location"] = new JsonObject
                    {
                        ["scriptId"] = "43",
                        ["lineNumber"] = 7,
                        ["columnNumber"] = 3
                    }
                }
            }
        });

        await WaitUntilAsync(() => breakpoint.IsResolved && breakpoint.ResolvedLineNumber == 7);
        Assert.Equal(3, breakpoint.ResolvedColumnNumber);
    }

    [AvaloniaFact]
    public async Task ExternalAsyncStackTraceIsResolved()
    {
        var service = new V8FakeCdpService();
        var viewModel = new SourcesViewModel(service);
        service.IsConnected = true;
        await WaitUntilAsync(() => viewModel.IsDebuggerEnabled);

        service.Raise("Debugger.paused", new JsonObject
        {
            ["reason"] = "promiseRejection",
            ["callFrames"] = new JsonArray
            {
                new JsonObject
                {
                    ["callFrameId"] = "frame-1",
                    ["functionName"] = "continuation",
                    ["location"] = new JsonObject { ["scriptId"] = "42", ["lineNumber"] = 6, ["columnNumber"] = 0 }
                }
            },
            ["asyncStackTraceId"] = new JsonObject { ["id"] = "async-1", ["debuggerId"] = "debugger-1" }
        });

        await WaitUntilAsync(() => viewModel.CallFrames.Count == 3);
        Assert.Contains(service.Commands, command => command.Method == "Debugger.getStackTrace");
        Assert.True(viewModel.CallFrames[1].IsAsyncBoundary);
        Assert.Equal("asyncParent", viewModel.CallFrames[2].FunctionName);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(10);
        Assert.True(condition());
    }

    private sealed class FakeSourceRegenerator : IV8SourceRegenerator
    {
        private readonly string _generatedSource;

        public FakeSourceRegenerator(string generatedSource)
        {
            _generatedSource = generatedSource;
        }

        public string Name => "Fake TypeScript";
        public V8SourceRegenerationRequest? Request { get; private set; }
        public bool CanRegenerate(V8SourceRegenerationRequest request) => request.SourceUrl.EndsWith(".ts", StringComparison.Ordinal);

        public ValueTask<V8SourceRegenerationResult> RegenerateAsync(
            V8SourceRegenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            var map = V8SourceMap.Parse($$"""
                {
                  "version": 3,
                  "sources": ["source.ts"],
                  "sourcesContent": [{{System.Text.Json.JsonSerializer.Serialize(request.EditedSource)}}],
                  "names": [],
                  "mappings": "AAAA;AACA;AACA"
                }
                """);
            return ValueTask.FromResult(V8SourceRegenerationResult.Regenerated(
                _generatedSource,
                map,
                "Fake TypeScript compilation completed."));
        }
    }

    private sealed class V8FakeCdpService : ICdpService
    {
        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
            }
        }

        public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";
        public string ConnectedHost => "http://127.0.0.1:9229";
        public string ConnectedTargetId => "node-target";
        public string ConnectedTargetType => "node";
        public string ConnectedTargetUrl => "file:///app/example.js";
        public IReadOnlySet<string> SupportedDomains { get; } = new HashSet<string> { "Schema", "Runtime", "Debugger", "Profiler", "HeapProfiler" };
        public bool IsPreviewScreencastActive { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;
        public List<(string Method, JsonObject? Parameters)> Commands { get; } = new();
        public bool RejectOptionalDebuggerCommands { get; init; }
        public bool ProvideNestedScopeValues { get; init; }
        public string GeneratedScriptSource { get; init; } = "function compute() {}";
        private int _nextBreakpointId;

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            Commands.Add((method, parameters));
            if (RejectOptionalDebuggerCommands &&
                method is "Debugger.setAsyncCallStackDepth" or "Debugger.setPauseOnExceptions" or
                    "Debugger.setBreakpointsActive" or "Debugger.setBlackboxPatterns" or
                    "Debugger.setBlackboxedRanges")
            {
                return Task.FromException<JsonObject>(new InvalidOperationException($"Action {method} is not supported."));
            }
            if (method is "Debugger.setBreakpointByUrl" or "Debugger.setBreakpoint")
            {
                return Task.FromResult(new JsonObject
                {
                    ["breakpointId"] = $"breakpoint-{++_nextBreakpointId}",
                    [method == "Debugger.setBreakpoint" ? "actualLocation" : "locations"] = method == "Debugger.setBreakpoint"
                        ? new JsonObject { ["scriptId"] = "42", ["lineNumber"] = 4, ["columnNumber"] = 0 }
                        : new JsonArray { new JsonObject { ["scriptId"] = "42", ["lineNumber"] = 4, ["columnNumber"] = 0 } }
                });
            }
            return Task.FromResult(method switch
            {
                "Debugger.getScriptSource" => new JsonObject { ["scriptSource"] = GeneratedScriptSource },
                "Runtime.getProperties" => GetProperties(parameters),
                "Debugger.evaluateOnCallFrame" => Evaluate(parameters),
                "Runtime.evaluate" => Evaluate(parameters),
                "Debugger.setBreakpointOnFunctionCall" => new JsonObject { ["breakpointId"] = $"function-breakpoint-{++_nextBreakpointId}" },
                "Debugger.setInstrumentationBreakpoint" => new JsonObject { ["breakpointId"] = $"instrumentation-breakpoint-{++_nextBreakpointId}" },
                "Debugger.setScriptSource" => new JsonObject { ["status"] = "Ok" },
                "Debugger.getPossibleBreakpoints" => GetPossibleBreakpoints(parameters),
                "Debugger.restartFrame" => new JsonObject(),
                "Debugger.getStackTrace" => new JsonObject
                {
                    ["stackTrace"] = new JsonObject
                    {
                        ["description"] = "Promise.then",
                        ["callFrames"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["functionName"] = "asyncParent",
                                ["scriptId"] = "42",
                                ["url"] = "file:///app/example.js",
                                ["lineNumber"] = 2,
                                ["columnNumber"] = 1
                            }
                        }
                    }
                },
                "Debugger.searchInContent" => new JsonObject
                {
                    ["result"] = new JsonArray
                    {
                        new JsonObject { ["lineNumber"] = 2, ["lineContent"] = "  return 42;" }
                    }
                },
                _ => new JsonObject()
            });
        }

        private static JsonObject GetPossibleBreakpoints(JsonObject? parameters)
        {
            var start = parameters?["start"] as JsonObject;
            return new JsonObject
            {
                ["locations"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["scriptId"] = start?["scriptId"]?.GetValue<string>() ?? "",
                        ["lineNumber"] = start?["lineNumber"]?.GetValue<int>() ?? 0,
                        ["columnNumber"] = 7
                    }
                }
            };
        }

        private static JsonObject Evaluate(JsonObject? parameters)
        {
            return parameters?["expression"]?.GetValue<string>() == "computeReturnValue"
                ? new JsonObject
                {
                    ["result"] = new JsonObject
                    {
                        ["type"] = "function",
                        ["objectId"] = "function-object-1",
                        ["description"] = "function computeReturnValue()"
                    }
                }
                : new JsonObject
                {
                    ["result"] = new JsonObject { ["type"] = "number", ["value"] = 10 }
                };
        }

        private JsonObject GetProperties(JsonObject? parameters)
        {
            if (!ProvideNestedScopeValues)
            {
                return new JsonObject
                {
                    ["result"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "sum",
                            ["writable"] = true,
                            ["value"] = new JsonObject { ["type"] = "number", ["value"] = 5 }
                        }
                    }
                };
            }

            return parameters?["objectId"]?.GetValue<string>() switch
            {
                "scope-nested" => new JsonObject
                {
                    ["result"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "sum",
                            ["writable"] = true,
                            ["value"] = new JsonObject { ["type"] = "number", ["value"] = 5 }
                        },
                        new JsonObject
                        {
                            ["name"] = "state",
                            ["writable"] = true,
                            ["value"] = new JsonObject { ["type"] = "object", ["description"] = "Object", ["objectId"] = "object-state" }
                        }
                    }
                },
                "object-state" => new JsonObject
                {
                    ["result"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "nested",
                            ["value"] = new JsonObject { ["type"] = "object", ["description"] = "Object", ["objectId"] = "object-nested" }
                        },
                        new JsonObject
                        {
                            ["name"] = "self",
                            ["value"] = new JsonObject { ["type"] = "object", ["description"] = "Object", ["objectId"] = "object-state" }
                        },
                        new JsonObject
                        {
                            ["name"] = "risky",
                            ["get"] = new JsonObject { ["type"] = "function", ["description"] = "get risky()", ["objectId"] = "getter-risky" }
                        }
                    },
                    ["privateProperties"] = new JsonArray
                    {
                        new JsonObject { ["name"] = "#secret", ["value"] = new JsonObject { ["type"] = "number", ["value"] = 9 } }
                    },
                    ["internalProperties"] = new JsonArray
                    {
                        new JsonObject { ["name"] = "[[Prototype]]", ["value"] = new JsonObject { ["type"] = "object", ["description"] = "Object", ["objectId"] = "object-prototype" } }
                    }
                },
                "object-nested" => new JsonObject
                {
                    ["result"] = new JsonArray
                    {
                        new JsonObject { ["name"] = "value", ["value"] = new JsonObject { ["type"] = "number", ["value"] = 5 } }
                    }
                },
                _ => new JsonObject { ["result"] = new JsonArray() }
            };
        }

        public void Raise(string method, JsonObject parameters) =>
            EventReceived?.Invoke(this, new CdpEventEventArgs(method, parameters));
    }
}
