using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Jint;

namespace Chrome.DevTools.Protocol;

public class CdpSession
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger("CdpSession");
    private readonly WebSocket _webSocket;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    public event Action<JsonObject>? EventSentForTesting;

    private readonly ConcurrentDictionary<string, CdpTargetSession> _attachedTargets = new();
    public bool IsTargetAttached(string targetId) => _attachedTargets.Values.Any(x => x.TargetId == targetId);
    private readonly CdpTargetSession? _defaultTargetSession;
    private readonly AsyncLocal<CdpTargetSession?> _currentTargetSession = new();

    private static readonly ConcurrentDictionary<string, object> _dummyRemoteObjects = new();
    private static readonly ConcurrentDictionary<string, string> _dummyScripts = new();
    private static readonly System.Collections.Generic.List<JsonObject> _dummyCookies = new();

    public CdpTargetSession? CurrentTargetSession
    {
        get => _currentTargetSession.Value ?? _defaultTargetSession;
        set => _currentTargetSession.Value = value;
    }

    public ICdpTarget? Target => CurrentTargetSession?.Target;
    public ConcurrentDictionary<string, object> RemoteObjects => CurrentTargetSession?.RemoteObjects ?? _dummyRemoteObjects;
    public int InspectedNodeId
    {
        get => CurrentTargetSession?.InspectedNodeId ?? 0;
        set { if (CurrentTargetSession != null) CurrentTargetSession.InspectedNodeId = value; }
    }
    public bool DiscoverTargetsEnabled { get; set; }
    public bool AutoAttachEnabled { get; set; }
    public bool IsDomEnabled => CurrentTargetSession?.IsDomEnabled ?? false;
    public ConcurrentDictionary<string, string> ScriptsToEvaluateOnNewDocument => CurrentTargetSession?.ScriptsToEvaluateOnNewDocument ?? _dummyScripts;
    public ConcurrentDictionary<string, string> ScriptsToEvaluateOnNewDocumentWorlds => CurrentTargetSession?.ScriptsToEvaluateOnNewDocumentWorlds ?? _dummyScripts;
    public ConcurrentDictionary<string, string> ScriptsToEvaluateOnLoad => CurrentTargetSession?.ScriptsToEvaluateOnLoad ?? _dummyScripts;
    public System.Collections.Generic.List<JsonObject> Cookies => CurrentTargetSession?.Cookies ?? _dummyCookies;

    public JsonObject? GeolocationOverride
    {
        get => CurrentTargetSession?.GeolocationOverride;
        set { if (CurrentTargetSession != null) CurrentTargetSession.GeolocationOverride = value; }
    }
    public JsonObject? DeviceOrientationOverride
    {
        get => CurrentTargetSession?.DeviceOrientationOverride;
        set { if (CurrentTargetSession != null) CurrentTargetSession.DeviceOrientationOverride = value; }
    }
    public bool TouchEmulationEnabled
    {
        get => CurrentTargetSession?.TouchEmulationEnabled ?? false;
        set { if (CurrentTargetSession != null) CurrentTargetSession.TouchEmulationEnabled = value; }
    }
    public bool LifecycleEventsEnabled
    {
        get => CurrentTargetSession?.LifecycleEventsEnabled ?? false;
        set { if (CurrentTargetSession != null) CurrentTargetSession.LifecycleEventsEnabled = value; }
    }
    public bool AdBlockingEnabled
    {
        get => CurrentTargetSession?.AdBlockingEnabled ?? false;
        set { if (CurrentTargetSession != null) CurrentTargetSession.AdBlockingEnabled = value; }
    }
    public bool BypassCSP
    {
        get => CurrentTargetSession?.BypassCSP ?? false;
        set { if (CurrentTargetSession != null) CurrentTargetSession.BypassCSP = value; }
    }
    public JsonObject? FontFamilies
    {
        get => CurrentTargetSession?.FontFamilies;
        set { if (CurrentTargetSession != null) CurrentTargetSession.FontFamilies = value; }
    }
    public JsonObject? FontSizes
    {
        get => CurrentTargetSession?.FontSizes;
        set { if (CurrentTargetSession != null) CurrentTargetSession.FontSizes = value; }
    }
    public string? DownloadBehavior
    {
        get => CurrentTargetSession?.DownloadBehavior;
        set { if (CurrentTargetSession != null) CurrentTargetSession.DownloadBehavior = value; }
    }
    public string? DownloadPath
    {
        get => CurrentTargetSession?.DownloadPath;
        set { if (CurrentTargetSession != null) CurrentTargetSession.DownloadPath = value; }
    }
    public bool InterceptFileChooserDialog
    {
        get => CurrentTargetSession?.InterceptFileChooserDialog ?? false;
        set { if (CurrentTargetSession != null) CurrentTargetSession.InterceptFileChooserDialog = value; }
    }
    public bool PrerenderingAllowed
    {
        get => CurrentTargetSession?.PrerenderingAllowed ?? false;
        set { if (CurrentTargetSession != null) CurrentTargetSession.PrerenderingAllowed = value; }
    }
    public string? RPHRegistrationMode
    {
        get => CurrentTargetSession?.RPHRegistrationMode;
        set { if (CurrentTargetSession != null) CurrentTargetSession.RPHRegistrationMode = value; }
    }
    public string? SPCTransactionMode
    {
        get => CurrentTargetSession?.SPCTransactionMode;
        set { if (CurrentTargetSession != null) CurrentTargetSession.SPCTransactionMode = value; }
    }
    public string? WebLifecycleState
    {
        get => CurrentTargetSession?.WebLifecycleState;
        set { if (CurrentTargetSession != null) CurrentTargetSession.WebLifecycleState = value; }
    }
    public ConcurrentDictionary<string, string> CompilationCache => CurrentTargetSession?.CompilationCache ?? _dummyScripts;
    public System.Collections.Generic.List<JsonObject> NavigationHistory => CurrentTargetSession?.NavigationHistory ?? _dummyCookies;
    public int NavigationHistoryIndex
    {
        get => CurrentTargetSession?.NavigationHistoryIndex ?? -1;
        set { if (CurrentTargetSession != null) CurrentTargetSession.NavigationHistoryIndex = value; }
    }
    public object? ScriptSession
    {
        get => CurrentTargetSession?.ScriptSession;
        set { if (CurrentTargetSession != null) CurrentTargetSession.ScriptSession = value; }
    }
    public int? ScriptSessionNodeId
    {
        get => CurrentTargetSession?.ScriptSessionNodeId;
        set { if (CurrentTargetSession != null) CurrentTargetSession.ScriptSessionNodeId = value; }
    }

    private bool _useLogicalTree = false;
    public bool UseLogicalTree
    {
        get => _useLogicalTree;
        set
        {
            if (_useLogicalTree != value)
            {
                _useLogicalTree = value;
                if (CurrentTargetSession != null && CurrentTargetSession.IsDomEnabled)
                {
                    CurrentTargetSession.StartObservingVisualTree();
                }
            }
        }
    }

    private int _nextObjectId = 1;

    public bool InspectModeEnabled
    {
        get => CurrentTargetSession?.InspectModeEnabled ?? false;
        set { if (CurrentTargetSession != null) CurrentTargetSession.InspectModeEnabled = value; }
    }

    public CdpSession(WebSocket webSocket, ICdpTarget? target)
    {
        _webSocket = webSocket;
        if (target != null)
        {
            var targetId = CdpServer.Register(target);
            _defaultTargetSession = CdpServer.TargetSessionFactory?.Invoke(this, "", targetId, target)
                                    ?? new CdpTargetSession(this, "", targetId, target);
        }
    }

    public void AttachTarget(string sessionId, CdpTargetSession targetSession)
    {
        _attachedTargets[sessionId] = targetSession;
    }

    public CdpTargetSession? GetAttachedSessionForTarget(string targetId)
    {
        return _attachedTargets.Values.FirstOrDefault(x => x.TargetId == targetId);
    }

    public void AutoAttachTarget(ICdpTarget target, CdpTargetSession? parentSession = null)
    {
        if (_attachedTargets.Values.Any(x => x.TargetId == target.Id))
        {
            return;
        }

        var sessionId = Guid.NewGuid().ToString();
        var targetSession = CdpServer.TargetSessionFactory?.Invoke(this, sessionId, target.Id, target)
                            ?? new CdpTargetSession(this, sessionId, target.Id, target);
        AttachTarget(sessionId, targetSession);

        var @params = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["targetInfo"] = new JsonObject
            {
                ["targetId"] = target.Id,
                ["type"] = target.Type,
                ["title"] = target.Title,
                ["url"] = target.Url,
                ["attached"] = true,
                ["browserContextId"] = "1"
            },
            ["waitingForDebugger"] = false
        };

        var activeTarget = parentSession ?? CurrentTargetSession;
        if (activeTarget != null && activeTarget.Target.Type == "tab")
        {
            _ = SendEventAsync("Target.attachedToTarget", @params, activeTarget);
        }
        else
        {
            _ = SendEventAsync("Target.attachedToTarget", @params);
        }
    }

    public void DetachTarget(string sessionId)
    {
        if (_attachedTargets.TryRemove(sessionId, out var targetSession))
        {
            targetSession.Dispose();

            // Broadcast detached event
            _ = SendEventAsync("Target.detachedFromTarget", new JsonObject
            {
                ["sessionId"] = sessionId,
                ["targetId"] = targetSession.TargetId
            });
        }
    }

    public string? GetSessionIdForTarget(string targetId)
    {
        return _attachedTargets.FirstOrDefault(x => x.Value.TargetId == targetId).Key;
    }

    public void DetachTargetById(string targetId)
    {
        var sessionId = GetSessionIdForTarget(targetId);
        if (!string.IsNullOrEmpty(sessionId))
        {
            DetachTarget(sessionId);
        }
    }

    public string RegisterObject(object obj)
    {
        string id = $"object:{Interlocked.Increment(ref _nextObjectId)}";
        RemoteObjects[id] = obj;
        return id;
    }

    public object? GetObject(string id)
    {
        if (RemoteObjects.TryGetValue(id, out var obj) && obj != null)
        {
            if (obj is Jint.Native.JsValue jsVal)
            {
                if (jsVal.IsObject())
                {
                    try
                    {
                        var unwrapped = jsVal.ToObject();
                        if (unwrapped != null)
                        {
                            var typeName = unwrapped.GetType().FullName ?? "";
                            if (typeName.StartsWith("Avalonia.") || typeName.Contains("CdpRuntime") || typeName.Contains("Mock"))
                            {
                                return unwrapped;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }
            return obj;
        }
        return null;
    }

    public void Close()
    {
        _cts.Cancel();
    }

    public async Task StartAsync()
    {
        var buffer = new byte[8192];
        try
        {
            CdpServer.AddSession(this);
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
        catch (Exception)
        {
            // Session closed or faulted
        }
        finally
        {
            Cleanup();
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
        }
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
            CdpServer.OriginalOut.WriteLine($"[CDP SERVER INCOMING] method: {method}, id: {id}, params: {paramsNode.ToJsonString()}");

            string? sessionId = null;
            if (obj.ContainsKey("sessionId"))
            {
                sessionId = obj["sessionId"]?.GetValue<string>();
            }

            try
            {
                var result = await DispatchMethodAsync(method, paramsNode, sessionId);
                await SendResponseAsync(id, result, sessionId);
            }
            catch (Exception ex)
            {
                await SendErrorAsync(id, -32603, ex.Message, sessionId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("CdpSession", "Error processing message", ex);
        }
    }

    private async Task<JsonObject> DispatchMethodAsync(string method, JsonObject @params, string? sessionId)
    {
        Func<Task<JsonObject>> action = async () =>
        {
            CdpTargetSession? targetSession = null;
            if (!string.IsNullOrEmpty(sessionId))
            {
                if (!_attachedTargets.TryGetValue(sessionId, out targetSession))
                {
                    throw new Exception($"No session with given id: {sessionId}");
                }
            }

            var previousSession = _currentTargetSession.Value;
            _currentTargetSession.Value = targetSession ?? _defaultTargetSession;
            try
            {
                return await CdpDispatcher.DispatchAsync(this, method, @params);
            }
            finally
            {
                _currentTargetSession.Value = previousSession;
            }
        };

        if (CdpServer.UIThreadInvoker != null)
        {
            return await CdpServer.UIThreadInvoker(action);
        }
        else
        {
            return await action();
        }
    }

    public async Task SendResponseAsync(int id, JsonObject result, string? sessionId = null)
    {
        var response = new JsonObject
        {
            ["id"] = id,
            ["result"] = result
        };
        if (!string.IsNullOrEmpty(sessionId))
        {
            response["sessionId"] = sessionId;
        }
        await SendJsonAsync(response);
    }

    public async Task SendErrorAsync(int id, int code, string message, string? sessionId = null)
    {
        var response = new JsonObject
        {
            ["id"] = id,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
        if (!string.IsNullOrEmpty(sessionId))
        {
            response["sessionId"] = sessionId;
        }
        await SendJsonAsync(response);
    }

    public async Task SendEventAsync(string method, JsonObject @params, CdpTargetSession? targetSession)
    {
        var evt = new JsonObject
        {
            ["method"] = method,
            ["params"] = @params
        };
        var activeTarget = targetSession ?? CurrentTargetSession;
        if (activeTarget != null && !string.IsNullOrEmpty(activeTarget.SessionId))
        {
            bool isBrowserLevelTargetEvent = 
                method == "Target.targetCreated" || 
                method == "Target.targetDestroyed" || 
                method == "Target.targetInfoChanged" ||
                method == "Target.attachedToTarget" ||
                method == "Target.detachedFromTarget";

            if ((!method.StartsWith("Target.", StringComparison.OrdinalIgnoreCase) || !isBrowserLevelTargetEvent || targetSession != null) && 
                !method.StartsWith("Browser.", StringComparison.OrdinalIgnoreCase))
            {
                evt["sessionId"] = activeTarget.SessionId;
            }
        }
        await SendJsonAsync(evt);
    }

    public async Task SendEventAsync(string method, JsonObject @params)
    {
        await SendEventAsync(method, @params, null);
    }

    private async Task SendJsonAsync(JsonObject node)
    {
        EventSentForTesting?.Invoke(node);
        string jsonStr = node.ToJsonString(new JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() });
        if (!jsonStr.Contains("screencastFrame") && !jsonStr.Contains("data:image"))
        {
            CdpServer.OriginalOut.WriteLine($"[CDP SERVER OUTGOING] {jsonStr}");
        }
        if (_webSocket.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(jsonStr);
        try
        {
            await _sendSemaphore.WaitAsync();
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
            // Ignore socket writing errors if client disconnected/closed
        }
        finally
        {
            try
            {
                _sendSemaphore.Release();
            }
            catch (ObjectDisposedException) { }
        }
    }

    public void RequestScreencastFrame()
    {
        CurrentTargetSession?.RequestScreencastFrame();
    }

    public void StartScreencast(string format = "png", int? quality = null, int? maxWidth = null, int? maxHeight = null, int? everyNthFrame = null, string? transferMode = null)
    {
        CurrentTargetSession?.StartScreencast(format, quality, maxWidth, maxHeight, everyNthFrame, transferMode);
    }

    public void StopScreencast()
    {
        CurrentTargetSession?.StopScreencast();
    }

    public void AcknowledgeScreencastFrame(int sessionId)
    {
        CurrentTargetSession?.AcknowledgeScreencastFrame(sessionId);
    }

    private void Cleanup()
    {
        CdpServer.RemoveSession(this);
        _cts.Cancel();

        _defaultTargetSession?.Dispose();
        foreach (var target in _attachedTargets.Values)
        {
            target.Dispose();
        }
        _attachedTargets.Clear();

        Chrome.DevTools.Protocol.Domains.LogDomain.RemoveSession(this);
        Chrome.DevTools.Protocol.Domains.NetworkDomain.RemoveSession(this);
        Chrome.DevTools.Protocol.Domains.FetchDomain.RemoveSession(this);
        Chrome.DevTools.Protocol.Domains.TracingDomain.CleanupSession(this);
        Chrome.DevTools.Protocol.Domains.ProfilerDomain.CleanupSession(this);
        Chrome.DevTools.Protocol.Domains.BackgroundServiceDomain.RemoveSession(this);

        OnCleanup();

        RemoteObjects.Clear();
        _sendSemaphore.Dispose();
    }

    protected virtual void OnCleanup()
    {
    }

    public void StartObservingVisualTree()
    {
        CurrentTargetSession?.StartObservingVisualTree();
    }

    public void StopObservingVisualTree()
    {
        CurrentTargetSession?.StopObservingVisualTree();
    }
}
