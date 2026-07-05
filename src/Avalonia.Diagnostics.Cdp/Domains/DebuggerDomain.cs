using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Runtime;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class DebuggerDomain
{
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

                        var selectedNode = session.NodeMap.GetVisual(session.InspectedNodeId);
                        var control = selectedNode as Avalonia.Controls.Control;
                        var dataContext = control?.DataContext;
                        var windowObj = (selectedNode as Avalonia.Controls.Window) ?? (session.Window as Avalonia.Controls.Window);

                        engine.SetValue("SelectedNode", selectedNode);
                        engine.SetValue("Control", control);
                        engine.SetValue("DataContext", dataContext);
                        engine.SetValue("ViewModel", dataContext);
                        engine.SetValue("Window", windowObj);
                        engine.SetValue("window", new Avalonia.Diagnostics.Cdp.Domains.CdpRuntimeWindow(session));
                        engine.SetValue("document", new Avalonia.Diagnostics.Cdp.Domains.CdpRuntimeDocument(session));
                        engine.SetValue("Print", new Action<object?>(Console.WriteLine));
                        engine.SetValue("Query", new Func<string, Visual?>(s => Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelector(session.Window ?? selectedNode, s, session.UseLogicalTree)));
                        engine.SetValue("QueryAll", new Func<string, IEnumerable<Visual>>(s => Avalonia.Diagnostics.Cdp.SelectorEngine.QuerySelectorAll(session.Window ?? selectedNode, s, session.UseLogicalTree)));
                        
                        engine.Evaluate(@"
                            if (typeof globalThis.setInterval === 'undefined') {
                                const timers = new Map();
                                let nextTimerId = 1;
                                globalThis.setInterval = function(callback, delay) {
                                    const id = nextTimerId++;
                                    timers.set(id, { callback, isInterval: true });
                                    return id;
                                };
                                globalThis.setTimeout = function(callback, delay) {
                                    const id = nextTimerId++;
                                    timers.set(id, { callback, isInterval: false });
                                    return id;
                                };
                                globalThis.clearInterval = function(id) {
                                    timers.delete(id);
                                };
                                globalThis.clearTimeout = function(id) {
                                    timers.delete(id);
                                };
                                globalThis.requestAnimationFrame = function(callback) {
                                    return globalThis.setTimeout(callback, 16);
                                };
                                globalThis.cancelAnimationFrame = function(id) {
                                    globalThis.clearTimeout(id);
                                };
                                globalThis.__runTimers = function() {
                                    for (const [id, timer] of timers.entries()) {
                                        try {
                                            timer.callback();
                                        } catch(e) {}
                                        if (!timer.isInterval) {
                                            timers.delete(id);
                                        }
                                    }
                                };
                            }
                            if (typeof globalThis.MutationObserver === 'undefined') {
                                globalThis.MutationObserver = class {
                                    constructor(callback) {
                                        this.callback = callback;
                                        this.intervalId = null;
                                    }
                                    observe(target, options) {
                                        this.intervalId = globalThis.setInterval(() => {
                                            try {
                                                this.callback([], this);
                                            } catch(e) {}
                                        }, 30);
                                    }
                                    disconnect() {
                                        if (this.intervalId) {
                                            globalThis.clearInterval(this.intervalId);
                                            this.intervalId = null;
                                        }
                                    }
                                };
                            }
                            if (typeof globalThis.Node === 'undefined') {
                                globalThis.Node = class {
                                    static [Symbol.hasInstance](instance) { return true; }
                                };
                                globalThis.Node.ELEMENT_NODE = 1;
                            }
                            if (typeof globalThis.HTMLElement === 'undefined') {
                                globalThis.HTMLElement = class {
                                    static [Symbol.hasInstance](instance) { return true; }
                                };
                            }
                            if (typeof globalThis.DOMRect === 'undefined') {
                                globalThis.DOMRect = class {
                                    constructor(x, y, width, height) {
                                        this.x = x || 0;
                                        this.y = y || 0;
                                        this.width = width || 0;
                                        this.height = height || 0;
                                        this.left = this.x;
                                        this.top = this.y;
                                        this.right = this.x + this.width;
                                        this.bottom = this.y + this.height;
                                    }
                                };
                            }
                            if (typeof globalThis.ClientRect === 'undefined') {
                                globalThis.ClientRect = globalThis.DOMRect;
                            }
                            globalThis.__wrap = function(target) {
                                if (!target) return target;
                                return new Proxy(target, {
                                    get(t, prop, receiver) {
                                        if (prop === 'id') return t.name || '';
                                        if (prop === 'name') return t.name || '';
                                        if (prop === 'textContent' || prop === 'innerText') {
                                            if ('text' in t) return t.text || '';
                                            if ('content' in t) return String(t.content || '');
                                            return '';
                                        }
                                        if (prop === 'value') {
                                            if ('text' in t) return t.text || '';
                                            if ('content' in t) return String(t.content || '');
                                            return '';
                                        }
                                        if (prop === 'isVisible') return t.isVisible === true;
                                        if (prop === 'isEffectivelyVisible') return t.isVisible === true;
                                        if (prop === 'isEnabled') return t.isEnabled !== false;
                                        if (prop === 'isChecked') return t.isChecked === true;
                                        if (prop === 'selectedIndex') return 'selectedIndex' in t ? t.selectedIndex : -1;
                                        return Reflect.get(t, prop, receiver);
                                    },
                                    set(t, prop, value, receiver) {
                                        if (prop === 'textContent' || prop === 'innerText' || prop === 'value') {
                                            if ('text' in t) {
                                                t.text = value;
                                                return true;
                                            }
                                            if ('content' in t) {
                                                t.content = value;
                                                return true;
                                            }
                                        }
                                        if (prop === 'selectedIndex') {
                                            if ('selectedIndex' in t) {
                                                t.selectedIndex = value;
                                                return true;
                                            }
                                        }
                                        return Reflect.set(t, prop, value, receiver);
                                    }
                                });
                            };
                        ");

                        if (selectedNode != null)
                        {
                            engine.SetValue("__raw_0", selectedNode);
                            var wrapped = engine.Evaluate("__wrap(__raw_0)");
                            engine.SetValue("$0", wrapped);
                            engine.SetValue("_0", wrapped);
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
                        CdpServer.OriginalOut.WriteLine($"[CDP DEBUGGER ERROR] Failed to evaluate breakpoint condition '{hitBp.Condition}': {ex}");
                        return true;
                    }
                };

                if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                {
                    shouldPause = evalCondition();
                }
                else
                {
                    shouldPause = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(evalCondition).GetAwaiter().GetResult();
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
