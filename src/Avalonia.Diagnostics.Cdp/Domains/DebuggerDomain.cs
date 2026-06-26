using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

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
                    string breakpointId = $"{url}:{lineNumber}";

                    var bp = new BreakpointInfo
                    {
                        BreakpointId = breakpointId,
                        Url = url,
                        LineNumber = lineNumber
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
        foreach (var bp in _breakpoints.Values)
        {
            bool lineMatches = bp.LineNumber == line || bp.LineNumber + 1 == line;
            bool urlMatches = string.Equals(bp.Url, url, StringComparison.OrdinalIgnoreCase)
                              || url.Contains(bp.Url, StringComparison.OrdinalIgnoreCase)
                              || bp.Url.Contains(url, StringComparison.OrdinalIgnoreCase);

            if (lineMatches && urlMatches)
            {
                hit = true;
                break;
            }
        }

        if (hit)
        {
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
        }
    }

    private class BreakpointInfo
    {
        public string BreakpointId { get; set; } = "";
        public string Url { get; set; } = "";
        public int LineNumber { get; set; }
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
