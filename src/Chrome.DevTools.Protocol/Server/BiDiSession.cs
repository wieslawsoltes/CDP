using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Chrome.DevTools.Protocol;

public class BiDiSession
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger("BiDiSession");
    private readonly string _sessionId;
    private WebSocket? _webSocket;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    
    private readonly HashSet<string> _subscribedEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _subscribedContexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JsonObject> _pendingResponses = new();

    public string SessionId => _sessionId;

    public BiDiSession(string sessionId)
    {
        _sessionId = sessionId;
    }

    public bool IsSubscribedToNetworkEvents()
    {
        return _subscribedEvents.Contains("network.beforeRequestSent") || 
               _subscribedEvents.Contains("network.responseCompleted") || 
               _subscribedEvents.Contains("network.fetchError");
    }

    public async Task StartAsync(WebSocket webSocket)
    {
        _webSocket = webSocket;
        Chrome.DevTools.Protocol.Domains.NetworkDomain.NetworkEventBroadcasted += OnNetworkEventBroadcasted;
        
        var buffer = new byte[8192];
        try
        {
            while (_webSocket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var jsonStr = Encoding.UTF8.GetString(ms.ToArray());
                await HandleMessageAsync(jsonStr);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in BiDi session receive loop");
        }
        finally
        {
            await CloseAsync();
        }
    }

    public async Task CloseAsync()
    {
        _cts.Cancel();
        Chrome.DevTools.Protocol.Domains.NetworkDomain.NetworkEventBroadcasted -= OnNetworkEventBroadcasted;
        _pendingResponses.Clear();
        
        if (_webSocket != null)
        {
            if (_webSocket.State == WebSocketState.Open || 
                _webSocket.State == WebSocketState.CloseReceived || 
                _webSocket.State == WebSocketState.CloseSent)
            {
                try
                {
                    await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                catch { }
            }
            try
            {
                _webSocket.Dispose();
            }
            catch { }
            _webSocket = null;
        }
    }

    public void Close()
    {
        _cts.Cancel();
    }

    private async Task HandleMessageAsync(string jsonStr)
    {
        try
        {
            var node = JsonNode.Parse(jsonStr);
            if (node is not JsonObject obj) return;

            var idNode = obj["id"];
            if (idNode == null) return;
            var id = idNode.GetValue<int>();

            var method = obj["method"]?.GetValue<string>() ?? "";
            var paramsNode = obj["params"] as JsonObject ?? new JsonObject();
            CdpServer.OriginalOut.WriteLine($"[BIDI SERVER INCOMING] method: {method}, id: {id}, params: {paramsNode.ToJsonString()}");

            try
            {
                var result = await DispatchMethodAsync(method, paramsNode);
                await SendResponseAsync(id, result);
            }
            catch (BiDiException ex)
            {
                await SendErrorAsync(id, ex.Error, ex.Message);
            }
            catch (Exception ex)
            {
                var errorType = "invalid argument";
                if (ex.Message.Contains("not found") || ex.Message.Contains("no such frame"))
                {
                    errorType = "no such frame";
                }
                else if (ex.Message.Contains("not implemented") || ex.Message.Contains("unknown command"))
                {
                    errorType = "unknown command";
                }
                await SendErrorAsync(id, errorType, ex.Message);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing BiDi message");
        }
    }

    private async Task<JsonObject> DispatchMethodAsync(string method, JsonObject @params)
    {
        if (method == "session.subscribe")
        {
            var eventsNode = @params["events"] as JsonArray;
            if (eventsNode != null)
            {
                foreach (var ev in eventsNode)
                {
                    var evStr = ev?.GetValue<string>();
                    if (evStr != null) _subscribedEvents.Add(evStr);
                }
            }
            var contextsNode = @params["contexts"] as JsonArray;
            if (contextsNode != null)
            {
                foreach (var ctx in contextsNode)
                {
                    var ctxStr = ctx?.GetValue<string>();
                    if (ctxStr != null) _subscribedContexts.Add(ctxStr);
                }
            }
            return new JsonObject();
        }
        
        if (method == "session.unsubscribe")
        {
            var eventsNode = @params["events"] as JsonArray;
            if (eventsNode != null)
            {
                foreach (var ev in eventsNode)
                {
                    var evStr = ev?.GetValue<string>();
                    if (evStr != null) _subscribedEvents.Remove(evStr);
                }
            }
            return new JsonObject();
        }

        if (method == "browsingContext.getTree")
        {
            var contexts = new JsonArray();
            foreach (var target in CdpServer.GetTargets())
            {
                if (target.Type == "tab") continue;
                contexts.Add(new JsonObject
                {
                    ["context"] = target.Id,
                    ["url"] = target.Url,
                    ["children"] = new JsonArray(),
                    ["userContext"] = "default"
                });
            }
            return new JsonObject { ["contexts"] = contexts };
        }

        if (method == "browsingContext.navigate")
        {
            var contextId = @params["context"]?.GetValue<string>() ?? "";
            var url = @params["url"]?.GetValue<string>() ?? "";
            
            var cdpSession = GetOrCreateCdpSession(contextId);
            if (cdpSession == null)
            {
                throw new BiDiException("no such frame", $"Browsing context {contextId} not found");
            }

            var previousSession = cdpSession.CurrentTargetSession;
            var targetSession = cdpSession.GetAttachedSessionForTarget(contextId);
            if (targetSession != null)
            {
                cdpSession.CurrentTargetSession = targetSession;
            }

            try
            {
                if (CdpDomainRegistry.TryGetHandler("Page", out var pageHandler))
                {
                    var p = new JsonObject { ["url"] = url };
                    var res = await pageHandler(cdpSession, "navigate", p);
                    return new JsonObject
                    {
                        ["navigation"] = res["loaderId"]?.DeepClone() ?? (JsonNode)JsonValue.Create(Guid.NewGuid().ToString()),
                        ["url"] = url
                    };
                }
                else
                {
                    throw new Exception("Page domain handler not registered");
                }
            }
            finally
            {
                cdpSession.CurrentTargetSession = previousSession;
            }
        }

        if (method == "script.evaluate")
        {
            var expression = @params["expression"]?.GetValue<string>() ?? "";
            var awaitPromise = @params["awaitPromise"]?.GetValue<bool>() ?? false;
            var targetNode = @params["target"] as JsonObject;
            var contextId = targetNode?["context"]?.GetValue<string>() ?? "";

            var cdpSession = GetOrCreateCdpSession(contextId);
            if (cdpSession == null)
            {
                throw new BiDiException("no such frame", $"Browsing context {contextId} not found");
            }

            var previousSession = cdpSession.CurrentTargetSession;
            var targetSession = cdpSession.GetAttachedSessionForTarget(contextId);
            if (targetSession != null)
            {
                cdpSession.CurrentTargetSession = targetSession;
            }

            try
            {
                if (CdpDomainRegistry.TryGetHandler("Runtime", out var runtimeHandler))
                {
                    var maxDepth = 3;
                    var serializationOptions = @params["serializationOptions"] as JsonObject;
                    if (serializationOptions != null && serializationOptions.TryGetPropertyValue("maxDepth", out var maxDepthNode))
                    {
                        maxDepth = maxDepthNode?.GetValue<int>() ?? 3;
                    }

                    var cdpParams = new JsonObject
                    {
                        ["expression"] = expression,
                        ["awaitPromise"] = awaitPromise,
                        ["returnByValue"] = false,
                        ["serializationOptions"] = new JsonObject
                        {
                            ["serialization"] = "deep",
                            ["maxDepth"] = maxDepth
                        }
                    };

                    var cdpResult = await runtimeHandler(cdpSession, "evaluate", cdpParams);
                    
                    if (cdpResult.TryGetPropertyValue("exceptionDetails", out var exceptionDetails))
                    {
                        var text = exceptionDetails?["text"]?.GetValue<string>() ?? "Script evaluation failed";
                        throw new Exception(text);
                    }

                    var resultNode = cdpResult["result"] as JsonObject;
                    var deepSerialized = resultNode?["deepSerializedValue"] as JsonObject;
                    
                    var mappedResult = MapToBiDiRemoteValue(deepSerialized ?? resultNode);
                    return new JsonObject { ["result"] = mappedResult };
                }
                else
                {
                    throw new Exception("Runtime domain handler not registered");
                }
            }
            finally
            {
                cdpSession.CurrentTargetSession = previousSession;
            }
        }

        if (method == "input.performActions")
        {
            var contextId = @params["context"]?.GetValue<string>() ?? "";
            var cdpSession = GetOrCreateCdpSession(contextId);
            if (cdpSession == null)
            {
                throw new BiDiException("no such frame", $"Browsing context {contextId} not found");
            }

            var previousSession = cdpSession.CurrentTargetSession;
            var targetSession = cdpSession.GetAttachedSessionForTarget(contextId);
            if (targetSession != null)
            {
                cdpSession.CurrentTargetSession = targetSession;
            }

            try
            {
                var actionsNode = @params["actions"] as JsonArray;
                if (actionsNode != null)
                {
                    double lastX = 0;
                    double lastY = 0;
                    
                    foreach (var chain in actionsNode)
                    {
                        if (chain is not JsonObject chainObj) continue;
                        var chainType = chainObj["type"]?.GetValue<string>();
                        var innerActions = chainObj["actions"] as JsonArray;
                        if (innerActions == null) continue;

                        foreach (var action in innerActions)
                        {
                            if (action is not JsonObject actObj) continue;
                            var actType = actObj["type"]?.GetValue<string>();
                            if (chainType == "pointer")
                            {
                                if (actType == "pointerMove")
                                {
                                    lastX = actObj["x"]?.GetValue<double>() ?? lastX;
                                    lastY = actObj["y"]?.GetValue<double>() ?? lastY;
                                    await DispatchMouseEventAsync(cdpSession, "mouseMoved", lastX, lastY, "none");
                                }
                                else if (actType == "pointerDown")
                                {
                                    var buttonNode = actObj["button"];
                                    var buttonCode = buttonNode?.GetValue<int>() ?? 0;
                                    var buttonStr = buttonCode == 2 ? "right" : (buttonCode == 1 ? "middle" : "left");
                                    lastX = actObj["x"]?.GetValue<double>() ?? lastX;
                                    lastY = actObj["y"]?.GetValue<double>() ?? lastY;
                                    await DispatchMouseEventAsync(cdpSession, "mousePressed", lastX, lastY, buttonStr);
                                }
                                else if (actType == "pointerUp")
                                {
                                    var buttonNode = actObj["button"];
                                    var buttonCode = buttonNode?.GetValue<int>() ?? 0;
                                    var buttonStr = buttonCode == 2 ? "right" : (buttonCode == 1 ? "middle" : "left");
                                    lastX = actObj["x"]?.GetValue<double>() ?? lastX;
                                    lastY = actObj["y"]?.GetValue<double>() ?? lastY;
                                    await DispatchMouseEventAsync(cdpSession, "mouseReleased", lastX, lastY, buttonStr);
                                }
                            }
                            else if (chainType == "key")
                            {
                                if (actType == "keyDown")
                                {
                                    var val = actObj["value"]?.GetValue<string>() ?? "";
                                    if (!string.IsNullOrEmpty(val))
                                    {
                                        var (key, code, text) = MapBiDiKey(val);
                                        await DispatchKeyEventAsync(cdpSession, "rawKeyDown", key, code, text);
                                        if (val.Length == 1 && (val[0] < '\uE000' || val[0] > '\uE03D') && val[0] >= 32 && val[0] != 127)
                                        {
                                            await InsertTextAsync(cdpSession, val);
                                        }
                                    }
                                }
                                else if (actType == "keyUp")
                                {
                                    var val = actObj["value"]?.GetValue<string>() ?? "";
                                    if (!string.IsNullOrEmpty(val))
                                    {
                                        var (key, code, text) = MapBiDiKey(val);
                                        await DispatchKeyEventAsync(cdpSession, "keyUp", key, code, text);
                                    }
                                }
                            }
                        }
                    }
                }

                return new JsonObject();
            }
            finally
            {
                cdpSession.CurrentTargetSession = previousSession;
            }
        }

        throw new BiDiException("unknown command", $"Method '{method}' is not implemented");
    }

    private CdpSession? GetOrCreateCdpSession(string targetId)
    {
        var existing = CdpServer.Sessions.FirstOrDefault(s => 
            s.IsTargetAttached(targetId) || (s.Target != null && s.Target.Id == targetId));
        if (existing != null)
        {
            return existing;
        }

        var target = CdpServer.GetTargets().FirstOrDefault(t => t.Id == targetId);
        if (target == null)
        {
            return null;
        }

        var dummySocket = new DummyWebSocket();
        var newSession = CdpServer.SessionFactory?.Invoke(dummySocket, target) ?? new CdpSession(dummySocket, target);
        
        CdpServer.AddSession(newSession);
        return newSession;
    }

    private static async Task DispatchMouseEventAsync(CdpSession session, string type, double x, double y, string button)
    {
        if (CdpDomainRegistry.TryGetHandler("Input", out var inputHandler))
        {
            var p = new JsonObject
            {
                ["type"] = type,
                ["x"] = x,
                ["y"] = y,
                ["button"] = button,
                ["clickCount"] = 1
            };
            await inputHandler(session, "dispatchMouseEvent", p);
        }
    }

    private static (string key, string code, string text) MapBiDiKey(string val)
    {
        if (string.IsNullOrEmpty(val)) return ("", "", "");
        if (val.Length == 1)
        {
            char ch = val[0];
            switch (ch)
            {
                case '\uE000': return ("Unidentified", "", "");
                case '\uE001': return ("Cancel", "", "");
                case '\uE002': return ("Help", "", "");
                case '\uE003': return ("Backspace", "Backspace", "");
                case '\uE004': return ("Tab", "Tab", "");
                case '\uE005': return ("Clear", "", "");
                case '\uE006': return ("Enter", "Enter", "\r");
                case '\uE007': return ("Enter", "Enter", "\r");
                case '\uE008': return ("Shift", "ShiftLeft", "");
                case '\uE009': return ("Control", "ControlLeft", "");
                case '\uE00A': return ("Alt", "AltLeft", "");
                case '\uE00B': return ("Pause", "", "");
                case '\uE00C': return ("Escape", "Escape", "");
                case '\uE00D': return ("Space", "Space", " ");
                case '\uE00E': return ("PageUp", "PageUp", "");
                case '\uE00F': return ("PageDown", "PageDown", "");
                case '\uE010': return ("End", "End", "");
                case '\uE011': return ("Home", "Home", "");
                case '\uE012': return ("ArrowLeft", "ArrowLeft", "");
                case '\uE013': return ("ArrowUp", "ArrowUp", "");
                case '\uE014': return ("ArrowRight", "ArrowRight", "");
                case '\uE015': return ("ArrowDown", "ArrowDown", "");
                case '\uE016': return ("Insert", "Insert", "");
                case '\uE017': return ("Delete", "Delete", "");
                case '\uE018': return (";", "Semicolon", ";");
                case '\uE019': return ("=", "Equal", "=");
                case '\uE01A': return ("0", "Numpad0", "0");
                case '\uE01B': return ("1", "Numpad1", "1");
                case '\uE01C': return ("2", "Numpad2", "2");
                case '\uE01D': return ("3", "Numpad3", "3");
                case '\uE01E': return ("4", "Numpad4", "4");
                case '\uE01F': return ("5", "Numpad5", "5");
                case '\uE020': return ("6", "Numpad6", "6");
                case '\uE021': return ("7", "Numpad7", "7");
                case '\uE022': return ("8", "Numpad8", "8");
                case '\uE023': return ("9", "Numpad9", "9");
                case '\uE024': return ("*", "NumpadMultiply", "*");
                case '\uE025': return ("+", "NumpadAdd", "+");
                case '\uE026': return (",", "NumpadComma", ",");
                case '\uE027': return ("-", "NumpadSubtract", "-");
                case '\uE028': return (".", "NumpadDecimal", ".");
                case '\uE029': return ("/", "NumpadDivide", "/");
                case '\uE031': return ("F1", "F1", "");
                case '\uE032': return ("F2", "F2", "");
                case '\uE033': return ("F3", "F3", "");
                case '\uE034': return ("F4", "F4", "");
                case '\uE035': return ("F5", "F5", "");
                case '\uE036': return ("F6", "F6", "");
                case '\uE037': return ("F7", "F7", "");
                case '\uE038': return ("F8", "F8", "");
                case '\uE039': return ("F9", "F9", "");
                case '\uE03A': return ("F10", "F10", "");
                case '\uE03B': return ("F11", "F11", "");
                case '\uE03C': return ("F12", "F12", "");
                case '\uE03D': return ("Meta", "MetaLeft", "");
            }
        }
        return (val, "", val);
    }

    private static async Task DispatchKeyEventAsync(CdpSession session, string type, string key, string code, string text)
    {
        if (CdpDomainRegistry.TryGetHandler("Input", out var inputHandler))
        {
            var p = new JsonObject
            {
                ["type"] = type,
                ["key"] = key,
                ["code"] = code,
                ["text"] = text
            };
            await inputHandler(session, "dispatchKeyEvent", p);
        }
    }

    private static async Task InsertTextAsync(CdpSession session, string text)
    {
        if (CdpDomainRegistry.TryGetHandler("Input", out var inputHandler))
        {
            var p = new JsonObject
            {
                ["text"] = text
            };
            await inputHandler(session, "insertText", p);
        }
    }

    private void OnNetworkEventBroadcasted(string cdpMethod, JsonObject cdpParams)
    {
        if (cdpMethod == "Network.requestWillBeSent" && _subscribedEvents.Contains("network.beforeRequestSent"))
        {
            var requestNode = cdpParams["request"] as JsonObject;
            if (requestNode == null) return;

            var requestId = cdpParams["requestId"]?.GetValue<string>() ?? "";
            var url = requestNode["url"]?.GetValue<string>() ?? "";
            var method = requestNode["method"]?.GetValue<string>() ?? "GET";
            
            var bidiHeaders = new JsonArray();
            var headersNode = requestNode["headers"] as JsonObject;
            if (headersNode != null)
            {
                foreach (var prop in headersNode)
                {
                    bidiHeaders.Add(new JsonObject
                    {
                        ["name"] = prop.Key,
                        ["value"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["value"] = prop.Value?.ToString() ?? ""
                        }
                    });
                }
            }

            var bidiParams = new JsonObject
            {
                ["context"] = "main",
                ["navigation"] = null,
                ["redirectCount"] = 0,
                ["request"] = new JsonObject
                {
                    ["request"] = requestId,
                    ["url"] = url,
                    ["method"] = method,
                    ["headers"] = bidiHeaders,
                    ["cookies"] = new JsonArray(),
                    ["headersSize"] = 0,
                    ["bodySize"] = 0,
                    ["timings"] = new JsonObject()
                },
                ["timestamp"] = (long)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["initiator"] = new JsonObject { ["type"] = "other" }
            };

            var bidiEvent = new JsonObject
            {
                ["type"] = "event",
                ["method"] = "network.beforeRequestSent",
                ["params"] = bidiParams
            };

            _ = SendJsonAsync(bidiEvent);
        }
        else if (cdpMethod == "Network.responseReceived" && _subscribedEvents.Contains("network.responseCompleted"))
        {
            var requestId = cdpParams["requestId"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(requestId))
            {
                _pendingResponses[requestId] = cdpParams.DeepClone().AsObject();
            }
        }
        else if (cdpMethod == "Network.loadingFinished" && _subscribedEvents.Contains("network.responseCompleted"))
        {
            var requestId = cdpParams["requestId"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(requestId) && _pendingResponses.TryRemove(requestId, out var responseParams))
            {
                var responseNode = responseParams["response"] as JsonObject;
                if (responseNode != null)
                {
                    var url = responseNode["url"]?.GetValue<string>() ?? "";
                    var status = responseNode["status"]?.GetValue<int>() ?? 200;
                    var statusText = responseNode["statusText"]?.GetValue<string>() ?? "OK";
                    var mimeType = responseNode["mimeType"]?.GetValue<string>() ?? "application/json";

                    var bidiHeaders = new JsonArray();
                    var headersNode = responseNode["headers"] as JsonObject;
                    if (headersNode != null)
                    {
                        foreach (var prop in headersNode)
                        {
                            bidiHeaders.Add(new JsonObject
                            {
                                ["name"] = prop.Key,
                                ["value"] = new JsonObject
                                {
                                    ["type"] = "string",
                                    ["value"] = prop.Value?.ToString() ?? ""
                                }
                            });
                        }
                    }

                    var bidiParams = new JsonObject
                    {
                        ["context"] = "main",
                        ["navigation"] = null,
                        ["redirectCount"] = 0,
                        ["request"] = new JsonObject
                        {
                            ["request"] = requestId,
                            ["url"] = url,
                            ["method"] = "GET",
                            ["headers"] = new JsonArray(),
                            ["cookies"] = new JsonArray(),
                            ["headersSize"] = 0,
                            ["bodySize"] = 0,
                            ["timings"] = new JsonObject()
                        },
                        ["response"] = new JsonObject
                        {
                            ["url"] = url,
                            ["status"] = status,
                            ["statusText"] = statusText,
                            ["headers"] = bidiHeaders,
                            ["mimeType"] = mimeType,
                            ["bytesReceived"] = GetLongValue(cdpParams["encodedDataLength"]) != 0 
                                ? GetLongValue(cdpParams["encodedDataLength"]) 
                                : GetLongValue(responseNode["encodedDataLength"]),
                            ["headersSize"] = 0,
                            ["bodySize"] = 0
                        },
                        ["timestamp"] = (long)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    var bidiEvent = new JsonObject
                    {
                        ["type"] = "event",
                        ["method"] = "network.responseCompleted",
                        ["params"] = bidiParams
                    };

                    _ = SendJsonAsync(bidiEvent);
                }
            }
        }
        else if (cdpMethod == "Network.loadingFailed")
        {
            var requestId = cdpParams["requestId"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(requestId))
            {
                _pendingResponses.TryRemove(requestId, out _);
            }
        }
    }

    private static JsonNode? MapToBiDiRemoteValue(JsonNode? node)
    {
        if (node == null) return new JsonObject { ["type"] = "null" };
        if (node is not JsonObject obj) return node.DeepClone();

        var type = obj["type"]?.GetValue<string>();
        if (string.IsNullOrEmpty(type))
        {
            return new JsonObject { ["type"] = "undefined" };
        }

        var result = new JsonObject { ["type"] = type };

        if (type == "string" || type == "number" || type == "boolean")
        {
            if (obj.ContainsKey("value"))
            {
                result["value"] = obj["value"]?.DeepClone();
            }
            return result;
        }
        
        if (type == "null" || type == "undefined")
        {
            return result;
        }

        if (type == "array")
        {
            var valArray = obj["value"] as JsonArray;
            if (valArray != null)
            {
                var bidiArr = new JsonArray();
                foreach (var item in valArray)
                {
                    bidiArr.Add(MapToBiDiRemoteValue(item));
                }
                result["value"] = bidiArr;
            }
            return result;
        }

        if (type == "map")
        {
            var valArray = obj["value"] as JsonArray;
            if (valArray != null)
            {
                var bidiMapVal = new JsonArray();
                foreach (var item in valArray)
                {
                    if (item is JsonObject pairObj)
                    {
                        var key = MapToBiDiRemoteValue(pairObj["key"]);
                        var val = MapToBiDiRemoteValue(pairObj["value"]);
                        var pair = new JsonArray { key, val };
                        bidiMapVal.Add(pair);
                    }
                }
                result["value"] = bidiMapVal;
            }
            return result;
        }

        if (type == "set")
        {
            var valArray = obj["value"] as JsonArray;
            if (valArray != null)
            {
                var bidiSet = new JsonArray();
                foreach (var item in valArray)
                {
                    bidiSet.Add(MapToBiDiRemoteValue(item));
                }
                result["value"] = bidiSet;
            }
            return result;
        }

        if (type == "date" || type == "regexp")
        {
            if (obj.ContainsKey("value"))
            {
                result["value"] = obj["value"]?.DeepClone();
            }
            return result;
        }

        if (type == "object")
        {
            var valArray = obj["value"] as JsonArray;
            if (valArray != null)
            {
                var bidiObjVal = new JsonArray();
                foreach (var item in valArray)
                {
                    if (item is JsonObject propObj)
                    {
                        var name = propObj["name"]?.GetValue<string>() ?? "";
                        var val = MapToBiDiRemoteValue(propObj["value"]);
                        var pair = new JsonArray { name, val };
                        bidiObjVal.Add(pair);
                    }
                }
                result["value"] = bidiObjVal;
            }
            else if (obj.ContainsKey("description"))
            {
                result["value"] = obj["description"]?.GetValue<string>() ?? "";
            }
            return result;
        }

        if (type == "node")
        {
            result["value"] = obj["value"]?.DeepClone();
            return result;
        }

        if (obj.ContainsKey("value"))
        {
            result["value"] = obj["value"]?.DeepClone();
        }
        return result;
    }

    public async Task SendResponseAsync(int id, JsonObject result)
    {
        var response = new JsonObject
        {
            ["id"] = id,
            ["type"] = "success",
            ["result"] = result
        };
        await SendJsonAsync(response);
    }

    public async Task SendErrorAsync(int id, string error, string message)
    {
        var response = new JsonObject
        {
            ["id"] = id,
            ["type"] = "error",
            ["error"] = error,
            ["message"] = message
        };
        await SendJsonAsync(response);
    }

    private async Task SendJsonAsync(JsonObject node)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;
        string jsonStr = node.ToJsonString(new JsonSerializerOptions 
        { 
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals, 
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() 
        });
        CdpServer.OriginalOut.WriteLine($"[BIDI SERVER OUTGOING] {jsonStr}");
        var bytes = Encoding.UTF8.GetBytes(jsonStr);
        try
        {
            await _sendLock.WaitAsync();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            try
            {
                _sendLock.Release();
            }
            catch (ObjectDisposedException) { }
        }
    }

    private static long GetLongValue(JsonNode? node)
    {
        if (node == null) return 0;
        try
        {
            if (node.AsValue().TryGetValue<long>(out var lVal)) return lVal;
            if (node.AsValue().TryGetValue<int>(out var iVal)) return iVal;
            if (node.AsValue().TryGetValue<double>(out var dVal)) return (long)dVal;
        }
        catch { }
        return 0;
    }
}

public class DummyWebSocket : System.Net.WebSockets.WebSocket
{
    private readonly WebSocketState _state = WebSocketState.Open;
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;
    public override void Abort() { }
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
    public override void Dispose() { }
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Text, true));
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class BiDiException : Exception
{
    public string Error { get; }
    public BiDiException(string error, string message) : base(message)
    {
        Error = error;
    }
}
