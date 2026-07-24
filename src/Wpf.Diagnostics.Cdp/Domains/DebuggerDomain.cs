using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Jint;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Chrome.DevTools.Protocol;

namespace Wpf.Diagnostics.Cdp.Domains;

public static class DebuggerDomain
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger("DebuggerDomain");
    public static readonly ManualResetEventSlim DebuggerBlockEvent = new(true);
    public static bool IsPaused { get; set; }

    private static readonly ConcurrentDictionary<string, BreakpointInfo> _breakpoints = new();

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        await Task.CompletedTask;
        switch (action)
        {
            case "enable":
                {
                    return new JsonObject
                    {
                        ["debuggerId"] = "1"
                    };
                }

            case "disable":
                {
                    return new JsonObject();
                }

            case "setBreakpointByUrl":
                {
                    string url = @params["url"]?.GetValue<string>() ?? @params["urlRegex"]?.GetValue<string>() ?? "";
                    int lineNumber = @params["lineNumber"]?.GetValue<int>() ?? 0;
                    string? condition = @params["condition"]?.GetValue<string>();
                    string breakpointId = $"{url}:{lineNumber}";

                    var bp = new BreakpointInfo
                    {
                        BreakpointId = breakpointId,
                        Url = url,
                        LineNumber = lineNumber,
                        Condition = condition
                    };
                    _breakpoints[breakpointId] = bp;

                    var locations = new JsonArray
                    {
                        new JsonObject
                        {
                            ["scriptId"] = "1",
                            ["lineNumber"] = lineNumber,
                            ["columnNumber"] = 0
                        }
                    };

                    return new JsonObject
                    {
                        ["breakpointId"] = breakpointId,
                        ["locations"] = locations
                    };
                }

            case "removeBreakpoint":
                {
                    string breakpointId = @params["breakpointId"]?.GetValue<string>() ?? "";
                    _breakpoints.TryRemove(breakpointId, out _);
                    return new JsonObject();
                }

            case "resume":
            case "stepOver":
            case "stepInto":
            case "stepOut":
                {
                    DebuggerBlockEvent.Set();
                    return new JsonObject();
                }

            default:
                throw new NotSupportedException($"Action {action} is not supported in Debugger domain.");
        }
    }

    public static void CheckBreakpoint(CdpSession session, string url, int line)
    {
        bool hit = false;
        BreakpointInfo? hitBp = null;
        foreach (var bp in _breakpoints.Values)
        {
            bool lineMatches = bp.LineNumber == line || bp.LineNumber + 1 == line;
            bool urlMatches = string.Equals(bp.Url, url, StringComparison.OrdinalIgnoreCase)
                              || url.Contains(bp.Url, StringComparison.OrdinalIgnoreCase)
                              || bp.Url.Contains(url, StringComparison.OrdinalIgnoreCase);

            if (lineMatches && urlMatches)
            {
                hit = true;
                hitBp = bp;
                break;
            }
        }

        if (hit && hitBp != null)
        {
            bool shouldPause = true;
            if (!string.IsNullOrWhiteSpace(hitBp.Condition))
            {
                Func<bool> evalCondition = () =>
                {
                    try
                    {
                        var engine = new Engine(options =>
                        {
                            options.Interop.TypeResolver = new Jint.Runtime.Interop.TypeResolver
                            {
                                MemberNameCreator = member => 
                                {
                                    var name = member.Name;
                                    if (string.IsNullOrEmpty(name)) return new[] { name };
                                    var camel = char.ToLowerInvariant(name[0]) + name.Substring(1);
                                    return name == camel ? new[] { name } : new[] { name, camel };
                                }
                            };
                            options.TimeoutInterval(TimeSpan.FromSeconds(5));
                        });

                        var selectedNode = session.NodeMap.GetVisual(session.InspectedNodeId) as UIElement;
                        var control = selectedNode as FrameworkElement;
                        var dataContext = control?.DataContext;
                        var windowObj = session.Window;

                        engine.SetValue("SelectedNode", selectedNode);
                        engine.SetValue("Control", control);
                        engine.SetValue("DataContext", dataContext);
                        engine.SetValue("ViewModel", dataContext);
                        engine.SetValue("Window", windowObj);
                        engine.SetValue("window", new CdpRuntimeWindow(session));
                        engine.SetValue("document", new CdpRuntimeDocument(session));
                        engine.SetValue("Print", new Action<object?>(obj =>
                        {
                            var msg = obj?.ToString() ?? "null";
                            Logger.LogInfoMessage("Debugger", msg);
                            Chrome.DevTools.Protocol.Domains.LogDomain.BroadcastLog("Console", "Information", msg);
                        }));
                        engine.SetValue("Query", new Func<string, UIElement?>(s => SelectorEngine.QuerySelector(session.Window?.Content as UIElement ?? selectedNode, s, session.UseLogicalTree)));
                        engine.SetValue("QueryAll", new Func<string, IEnumerable<UIElement>>(s => SelectorEngine.QuerySelectorAll(session.Window?.Content as UIElement ?? selectedNode, s, session.UseLogicalTree)));

                        if (selectedNode != null)
                        {
                            var elementObj = new CdpRuntimeElement(selectedNode, session);
                            engine.SetValue("$0", elementObj);
                            engine.SetValue("_0", elementObj);
                        }
                        else
                        {
                            engine.SetValue("$0", Jint.Native.JsValue.Null);
                            engine.SetValue("_0", Jint.Native.JsValue.Null);
                        }

                        var preprocessed = ScriptPreprocessor.Preprocess(hitBp.Condition);
                        var jsVal = engine.Evaluate(preprocessed);
                        var result = jsVal.ToObject();

                        if (result is bool bResult)
                        {
                            return bResult;
                        }
                        else if (result != null)
                        {
                            if (bool.TryParse(result.ToString(), out bool parsedBool))
                            {
                                return parsedBool;
                            }
                            else
                            {
                                return true;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogErrorMessage("DebuggerDomain", $"Failed to evaluate breakpoint condition '{hitBp.Condition}'", ex);
                        return true;
                    }
                };

                if (session.Window != null && session.Window.Dispatcher.CheckAccess())
                {
                    shouldPause = evalCondition();
                }
                else if (session.Window != null)
                {
                    shouldPause = session.Window.Dispatcher.Invoke(evalCondition);
                }
            }

            if (!shouldPause)
            {
                return;
            }

            IsPaused = true;
            DebuggerBlockEvent.Reset();

            var localVars = new LocalVariablesScope(url, line, "Paused on breakpoint");
            string localScopeId = session.RegisterObject(localVars);

            var callFrame = new JsonObject
            {
                ["callFrameId"] = "frame:0",
                ["functionName"] = "Evaluate",
                ["location"] = new JsonObject
                {
                    ["scriptId"] = "1",
                    ["lineNumber"] = line,
                    ["columnNumber"] = 0
                },
                ["url"] = url,
                ["scopeChain"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "local",
                        ["object"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["className"] = typeof(LocalVariablesScope).FullName ?? "LocalVariablesScope",
                            ["description"] = "Local variables",
                            ["objectId"] = localScopeId
                        }
                    }
                },
                ["this"] = new JsonObject
                {
                    ["type"] = "object",
                    ["className"] = "System.Object",
                    ["description"] = "this"
                }
            };

            var pausedParams = new JsonObject
            {
                ["callFrames"] = new JsonArray { callFrame },
                ["reason"] = "other"
            };

            _ = session.SendEventAsync("Debugger.paused", pausedParams);
            DebuggerBlockEvent.Wait();
            IsPaused = false;
            _ = session.SendEventAsync("Debugger.resumed", new JsonObject());
        }
    }

    private class BreakpointInfo
    {
        public string BreakpointId { get; set; } = "";
        public string Url { get; set; } = "";
        public int LineNumber { get; set; }
        public string? Condition { get; set; }
    }

    public class LocalVariablesScope
    {
        public string CurrentUrl { get; }
        public int CurrentLine { get; }
        public string Message { get; }

        public LocalVariablesScope(string url, int line, string message)
        {
            CurrentUrl = url;
            CurrentLine = line;
            Message = message;
        }
    }
}
