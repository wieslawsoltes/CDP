using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace WinUI.Diagnostics.Cdp
{
    public interface IMcpTool
    {
        string Name { get; }
        string Description { get; }
        JsonObject? InputSchema { get; }
        Task<JsonNode?> InvokeAsync(JsonObject input);
    }

    public interface IMcpToolProvider
    {
        IEnumerable<IMcpTool> GetTools();
    }

    public static class McpToolRegistry
    {
        private static readonly List<IMcpTool> _staticTools = new();
        private static readonly List<IMcpToolProvider> _providers = new();

        public static void RegisterTool(IMcpTool tool)
        {
            lock (_staticTools)
            {
                _staticTools.Add(tool);
            }

            var frameId = "main-frame-id";
            var toolsArray = new JsonArray();
            var toolJson = new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["frameId"] = frameId
            };
            if (tool.InputSchema != null)
            {
                toolJson["inputSchema"] = tool.InputSchema.DeepClone();
            }
            toolsArray.Add(toolJson);

            foreach (var session in Domains.WebMcpDomain.ActiveSessions)
            {
                var sessionFrameId = session.Target?.Id ?? "main-frame-id";
                var sessionToolJson = toolJson.DeepClone() as JsonObject;
                if (sessionToolJson != null)
                {
                    sessionToolJson["frameId"] = sessionFrameId;
                }
                var sessionToolsArray = new JsonArray { sessionToolJson };
                _ = session.SendEventAsync("WebMCP.toolsAdded", new JsonObject { ["tools"] = sessionToolsArray });
            }
        }

        public static void RegisterProvider(IMcpToolProvider provider)
        {
            lock (_providers)
            {
                _providers.Add(provider);
            }

            var toolsArray = new JsonArray();
            foreach (var tool in provider.GetTools())
            {
                var toolJson = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["frameId"] = "main-frame-id"
                };
                if (tool.InputSchema != null)
                {
                    toolJson["inputSchema"] = tool.InputSchema.DeepClone();
                }
                toolsArray.Add(toolJson);
            }

            if (toolsArray.Count > 0)
            {
                foreach (var session in Domains.WebMcpDomain.ActiveSessions)
                {
                    var sessionFrameId = session.Target?.Id ?? "main-frame-id";
                    var sessionTools = new JsonArray();
                    foreach (var node in toolsArray)
                    {
                        var sessionToolJson = node?.DeepClone() as JsonObject;
                        if (sessionToolJson != null)
                        {
                            sessionToolJson["frameId"] = sessionFrameId;
                        }
                        sessionTools.Add(sessionToolJson);
                    }
                    _ = session.SendEventAsync("WebMCP.toolsAdded", new JsonObject { ["tools"] = sessionTools });
                }
            }
        }

        public static IEnumerable<IMcpTool> GetTools()
        {
            var tools = new List<IMcpTool>();
            lock (_staticTools)
            {
                tools.AddRange(_staticTools);
            }
            lock (_providers)
            {
                foreach (var provider in _providers)
                {
                    tools.AddRange(provider.GetTools());
                }
            }
            return tools;
        }
    }
}

namespace WinUI.Diagnostics.Cdp.Domains
{
    public static class WebMcpDomain
    {
        private static readonly ConcurrentDictionary<CdpSession, bool> _activeSessions = new();

        public static IEnumerable<CdpSession> ActiveSessions => _activeSessions.Keys;

        public static void CleanupSession(CdpSession session)
        {
            _activeSessions.TryRemove(session, out _);
        }

        public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
        {
            switch (action)
            {
                case "enable":
                    {
                        _activeSessions[session] = true;
                        var tools = McpToolRegistry.GetTools();
                        var toolsArray = new JsonArray();
                        var frameId = session.Target?.Id ?? "main-frame-id";
                        foreach (var tool in tools)
                        {
                            var toolJson = new JsonObject
                            {
                                ["name"] = tool.Name,
                                ["description"] = tool.Description,
                                ["frameId"] = frameId
                            };
                            if (tool.InputSchema != null)
                            {
                                toolJson["inputSchema"] = tool.InputSchema.DeepClone();
                            }
                            toolsArray.Add(toolJson);
                        }

                        _ = session.SendEventAsync("WebMCP.toolsAdded", new JsonObject { ["tools"] = toolsArray });
                        return new JsonObject();
                    }

                case "disable":
                    {
                        _activeSessions.TryRemove(session, out _);
                        return new JsonObject();
                    }

                case "invokeTool":
                    {
                        string frameId = @params["frameId"]?.GetValue<string>() ?? session.Target?.Id ?? "main-frame-id";
                        string toolName = @params["toolName"]?.GetValue<string>() ?? "";
                        JsonObject input = (@params["input"] as JsonObject) ?? new JsonObject();

                        IMcpTool? targetTool = null;
                        foreach (var t in McpToolRegistry.GetTools())
                        {
                            if (t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase))
                            {
                                targetTool = t;
                                break;
                            }
                        }

                        if (targetTool == null)
                        {
                            throw new Exception($"Tool '{toolName}' not found");
                        }

                        string invocationId = Guid.NewGuid().ToString();

                        _ = session.SendEventAsync("WebMCP.toolInvoked", new JsonObject
                        {
                            ["toolName"] = toolName,
                            ["frameId"] = frameId,
                            ["invocationId"] = invocationId,
                            ["input"] = input.ToJsonString()
                        });

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var output = await targetTool.InvokeAsync(input);
                                _ = session.SendEventAsync("WebMCP.toolResponded", new JsonObject
                                {
                                    ["invocationId"] = invocationId,
                                    ["status"] = "Completed",
                                    ["output"] = output?.DeepClone()
                                });
                            }
                            catch (Exception ex)
                            {
                                _ = session.SendEventAsync("WebMCP.toolResponded", new JsonObject
                                {
                                    ["invocationId"] = invocationId,
                                    ["status"] = "Error",
                                    ["errorText"] = ex.Message
                                });
                            }
                        });

                        return new JsonObject { ["invocationId"] = invocationId };
                    }

                case "cancelInvocation":
                    {
                        return new JsonObject();
                    }

                default:
                    throw new Exception($"Method WebMCP.{action} is not implemented");
            }
        }
    }
}
