using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Linq;
using Avalonia.Controls;

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
                {
                    return new JsonObject();
                }

            case "setDiscoverTargets":
                {
                    bool discover = @params["discover"]?.GetValue<bool>() ?? false;
                    session.DiscoverTargetsEnabled = discover;
                    if (discover)
                    {
                        var targetInfos = CdpServer.GetActiveTargets();
                        foreach (var target in targetInfos)
                        {
                            if (target is JsonObject targetObj)
                            {
                                _ = session.SendEventAsync("Target.targetCreated", new JsonObject
                                {
                                    ["targetInfo"] = new JsonObject
                                    {
                                        ["targetId"] = targetObj["targetId"]?.GetValue<string>(),
                                        ["type"] = targetObj["type"]?.GetValue<string>(),
                                        ["title"] = targetObj["title"]?.GetValue<string>(),
                                        ["url"] = targetObj["url"]?.GetValue<string>(),
                                        ["attached"] = targetObj["attached"]?.GetValue<bool>() ?? false, // default false for discovery
                                        ["browserContextId"] = targetObj["browserContextId"]?.GetValue<string>()
                                    }
                                });
                            }
                        }
                    }
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
                    return new JsonObject { ["targetInfo"] = found?.DeepClone() };
                }

            case "activateTarget":
                {
                    string? targetId = @params["targetId"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(targetId))
                    {
                        var targetWin = CdpServer.GetWindows().FirstOrDefault(w => w.Id == targetId);
                        if (targetWin.Window is Window win)
                        {
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => win.Activate());
                        }
                    }
                    return new JsonObject();
                }

            case "exposeDevToolsProtocol":
            case "sendMessageToTarget":
                {
                    return new JsonObject();
                }

            case "detachFromTarget":
                {
                    string? sessionId = @params["sessionId"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(sessionId))
                    {
                        string? targetId = @params["targetId"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(targetId))
                        {
                            sessionId = session.GetSessionIdForTarget(targetId);
                        }
                    }
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        session.DetachTarget(sessionId);
                    }
                    return new JsonObject();
                }

            case "attachToTarget":
                {
                    bool flatten = @params["flatten"]?.GetValue<bool>() ?? false;
                    if (!flatten)
                    {
                        throw new Exception("Only flattened target attachments are supported. Please set flatten=true.");
                    }

                    string? targetId = @params["targetId"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(targetId))
                    {
                        throw new Exception("Missing targetId parameter");
                    }

                    var targetWin = CdpServer.GetWindows().FirstOrDefault(w => w.Id == targetId);
                    if (targetWin.Window == null)
                    {
                        throw new Exception($"Target not found: {targetId}");
                    }

                    var sessionId = Guid.NewGuid().ToString();
                    var targetSession = new CdpTargetSession(session, sessionId, targetId, targetWin.Window);
                    session.AttachTarget(sessionId, targetSession);

                    _ = session.SendEventAsync("Target.attachedToTarget", new JsonObject
                    {
                        ["sessionId"] = sessionId,
                        ["targetInfo"] = new JsonObject
                        {
                            ["targetId"] = targetId,
                            ["type"] = "page",
                            ["title"] = targetWin.Title,
                            ["url"] = $"http://localhost:{CdpServer.Port}/",
                            ["attached"] = true,
                            ["browserContextId"] = "1"
                        },
                        ["waitingForDebugger"] = false
                    });

                    return new JsonObject { ["sessionId"] = sessionId };
                }

            case "closeTarget":
                {
                    string? targetId = @params["targetId"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(targetId))
                    {
                        var targetWin = CdpServer.GetWindows().FirstOrDefault(w => w.Id == targetId);
                        if (targetWin.Window is Window win)
                        {
                            _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => win.Close());
                            return new JsonObject { ["success"] = true };
                        }
                    }
                    return new JsonObject { ["success"] = false };
                }

            case "createTarget":
                {
                    var newWin = await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var w = new Window
                        {
                            Title = "Dynamic CDP Window",
                            Width = 400,
                            Height = 300
                        };
                        w.Show();
                        return w;
                    });
                    var targetId = CdpServer.Register(newWin, "Dynamic CDP Window");
                    return new JsonObject { ["targetId"] = targetId };
                }

            default:
                throw new Exception($"Method Target.{action} is not implemented");
        }
    }
}
