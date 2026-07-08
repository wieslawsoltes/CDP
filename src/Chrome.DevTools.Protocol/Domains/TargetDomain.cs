using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Linq;

namespace Chrome.DevTools.Protocol.Domains;

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
                    bool autoAttach = @params["autoAttach"]?.GetValue<bool>() ?? false;
                    bool flatten = @params["flatten"]?.GetValue<bool>() ?? false;
                    bool waitForDebuggerOnStart = @params["waitForDebuggerOnStart"]?.GetValue<bool>() ?? false;

                    session.AutoAttachEnabled = autoAttach;
                    session.WaitForDebuggerOnStart = waitForDebuggerOnStart;

                    if (autoAttach)
                    {
                        if (!flatten)
                        {
                            throw new Exception("Only flattened target attachments are supported. Please set flatten=true.");
                        }

                        bool excludePages = false;
                        var filterArray = @params["filter"] as JsonArray;
                        if (filterArray != null)
                        {
                            foreach (var f in filterArray)
                            {
                                if (f is JsonObject fObj)
                                {
                                    if (fObj["type"]?.GetValue<string>() == "page" && fObj["exclude"]?.GetValue<bool>() == true)
                                    {
                                        excludePages = true;
                                    }
                                }
                            }
                        }

                        // Attach all current targets
                        var activeTabSession = session.CurrentTargetSession;
                        foreach (var target in CdpServer.GetTargets())
                        {
                            if (target.Type == "page" && excludePages)
                            {
                                continue;
                            }

                            if (target.Type == "page" && activeTabSession != null && activeTabSession.Target.Type == "tab")
                            {
                                session.AutoAttachTarget(target, activeTabSession);
                            }
                            else
                            {
                                session.AutoAttachTarget(target);
                            }
                        }
                    }
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
                                        ["attached"] = targetObj["attached"]?.GetValue<bool>() ?? false,
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
                        string title = "Browser Window";
                        found = new JsonObject
                        {
                            ["targetId"] = targetId ?? "page-1",
                            ["type"] = "page",
                            ["title"] = title,
                            ["url"] = $"http://localhost:{CdpServer.Port}/",
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
                        var target = CdpServer.GetTargets().FirstOrDefault(w => w.Id == targetId);
                        if (target != null)
                        {
                            target.Activate();
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

                    var target = CdpServer.GetTargets().FirstOrDefault(w => w.Id == targetId);
                    if (target == null)
                    {
                        throw new Exception($"Target not found: {targetId}");
                    }

                    var sessionId = Guid.NewGuid().ToString();
                    var targetSession = CdpServer.TargetSessionFactory?.Invoke(session, sessionId, targetId, target)
                                        ?? new CdpTargetSession(session, sessionId, targetId, target);
                    session.AttachTarget(sessionId, targetSession);

                    _ = session.SendEventAsync("Target.attachedToTarget", new JsonObject
                    {
                        ["sessionId"] = sessionId,
                        ["targetInfo"] = new JsonObject
                        {
                            ["targetId"] = targetId,
                            ["type"] = target.Type,
                            ["title"] = target.Title,
                            ["url"] = target.Url,
                            ["attached"] = true,
                            ["browserContextId"] = "1"
                        },
                        ["waitingForDebugger"] = CdpServer.IsTargetWaitingForDebugger(targetId)
                    });

                    return new JsonObject { ["sessionId"] = sessionId };
                }

            case "closeTarget":
                {
                    string? targetId = @params["targetId"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(targetId))
                    {
                        var target = CdpServer.GetTargets().FirstOrDefault(w => w.Id == targetId);
                        if (target != null)
                        {
                            target.Close();
                            return new JsonObject { ["success"] = true };
                        }
                    }
                    return new JsonObject { ["success"] = false };
                }

            case "createTarget":
                {
                    if (CdpServer.TargetFactory != null)
                    {
                        var url = @params["url"]?.GetValue<string>() ?? "";
                        var target = await CdpServer.TargetFactory(url, "Dynamic Window");
                        CdpServer.Register(target);
                        return new JsonObject { ["targetId"] = target.Id };
                    }
                    throw new Exception("TargetFactory is not registered on CdpServer.");
                }

            case "getBrowserContexts":
                {
                    return new JsonObject { ["browserContextIds"] = new JsonArray { "1" } };
                }

            default:
                throw new Exception($"Method Target.{action} is not implemented");
        }
    }
}
