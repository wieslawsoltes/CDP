using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class TargetDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getTargets":
                {
                    var targetInfos = CdpServer.GetActiveTargets();
                    return new JsonObject { ["targetInfos"] = targetInfos };
                }

            case "setAutoAttach":
            case "setDiscoverTargets":
                {
                    // STUB: Return success
                    return new JsonObject();
                }

            case "getTargetInfo":
                {
                    string? targetId = @params["targetId"]?.GetValue<string>();
                    var targets = CdpServer.GetActiveTargets();
                    JsonObject? found = null;
                    if (!string.IsNullOrEmpty(targetId))
                    {
                        foreach (var targetNode in targets)
                        {
                            if (targetNode?["targetId"]?.GetValue<string>() == targetId)
                            {
                                found = targetNode as JsonObject;
                                break;
                            }
                        }
                    }
                    if (found == null && targets.Count > 0)
                    {
                        found = targets[0] as JsonObject;
                    }
                    if (found == null)
                    {
                        string title = "Avalonia Window";
                        try
                        {
                            if (session.Window != null)
                            {
                                var titleProp = session.Window.GetType().GetProperty("Title");
                                if (titleProp != null)
                                {
                                    title = titleProp.GetValue(session.Window) as string ?? title;
                                }
                            }
                        }
                        catch {}

                        found = new JsonObject
                        {
                            ["targetId"] = targetId ?? "page-1",
                            ["type"] = "page",
                            ["title"] = title,
                            ["url"] = "http://localhost:9222/",
                            ["attached"] = true,
                            ["browserContextId"] = "1"
                        };
                    }
                    return new JsonObject { ["targetInfo"] = found };
                }

            case "activateTarget":
            case "detachFromTarget":
            case "exposeDevToolsProtocol":
            case "sendMessageToTarget":
                {
                    return new JsonObject();
                }

            case "attachToTarget":
                {
                    return new JsonObject { ["sessionId"] = "session-1" };
                }

            case "closeTarget":
                {
                    return new JsonObject { ["success"] = true };
                }

            case "createTarget":
                {
                    return new JsonObject { ["targetId"] = Guid.NewGuid().ToString() };
                }

            default:
                throw new Exception($"Method Target.{action} is not implemented");
        }
    }
}
