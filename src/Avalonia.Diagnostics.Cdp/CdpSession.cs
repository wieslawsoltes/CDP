using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;

namespace Avalonia.Diagnostics.Cdp;

public class CdpSession
{
    private readonly WebSocket _webSocket;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    private readonly ConcurrentDictionary<string, CdpTargetSession> _attachedTargets = new();
    private readonly CdpTargetSession? _defaultTargetSession;
    private readonly AsyncLocal<CdpTargetSession?> _currentTargetSession = new();

    private static readonly ConcurrentDictionary<string, object> _dummyRemoteObjects = new();
    private static readonly ConcurrentDictionary<string, string> _dummyScripts = new();
    private static readonly System.Collections.Generic.List<JsonObject> _dummyCookies = new();
    private static readonly IInputDevice _dummyTouchDevice = (IInputDevice)Activator.CreateInstance(typeof(TouchDevice), nonPublic: true)!;

    public CdpTargetSession? CurrentTargetSession
    {
        get => _currentTargetSession.Value ?? _defaultTargetSession;
        set => _currentTargetSession.Value = value;
    }

    public TopLevel? Window => CurrentTargetSession?.Window;
    public NodeMap NodeMap => CurrentTargetSession?.NodeMap ?? new NodeMap();
    public ConcurrentDictionary<string, object> RemoteObjects => CurrentTargetSession?.RemoteObjects ?? _dummyRemoteObjects;
    public int InspectedNodeId
    {
        get => CurrentTargetSession?.InspectedNodeId ?? 0;
        set { if (CurrentTargetSession != null) CurrentTargetSession.InspectedNodeId = value; }
    }
    public bool DiscoverTargetsEnabled { get; set; }
    public bool IsDomEnabled => CurrentTargetSession?.IsDomEnabled ?? false;
    public ConcurrentDictionary<string, string> ScriptsToEvaluateOnNewDocument => CurrentTargetSession?.ScriptsToEvaluateOnNewDocument ?? _dummyScripts;
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
    public IInputDevice TouchDevice => CurrentTargetSession?.TouchDevice ?? _dummyTouchDevice;
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

    public static System.Collections.Generic.IEnumerable<Visual> GetLogicalVisualChildren(ILogical logical)
    {
        foreach (var child in logical.LogicalChildren)
        {
            if (child is StyledElement se && se.TemplatedParent != null)
            {
                continue;
            }
            if (child is Visual visualChild)
            {
                if (visualChild.GetVisualParent() is not Avalonia.Controls.Presenters.ContentPresenter cp || cp.Content == visualChild)
                {
                    yield return visualChild;
                }
            }
            else if (child is ILogical childLogical)
            {
                foreach (var desc in GetLogicalVisualChildren(childLogical))
                {
                    yield return desc;
                }
            }
        }
    }

    internal bool IsLogicalNode(ILogical? node)
    {
        if (node == null) return false;
        if (node is TopLevel) return true;
        if (node is StyledElement se && se.TemplatedParent != null) return false;
        if (node is Visual visual)
        {
            var vp = visual.GetVisualParent();
            if (vp is Avalonia.Controls.Presenters.ContentPresenter cp && cp.Content != visual)
            {
                return false;
            }
        }

        var current = node;
        while (current != null)
        {
            var parent = current.LogicalParent;
            if (parent == null)
            {
                return current is TopLevel;
            }
            if (current is StyledElement cse && cse.TemplatedParent != null)
            {
                return false;
            }
            if (current is Visual v)
            {
                var vp = v.GetVisualParent();
                if (vp is Avalonia.Controls.Presenters.ContentPresenter cp && cp.Content != v)
                {
                    return false;
                }
            }
            if (!parent.LogicalChildren.Contains(current))
            {
                return false;
            }
            current = parent;
        }
        return false;
    }

    public Visual FindLogicalNode(Visual visual)
    {
        var current = visual;
        while (current != null)
        {
            if (current is ILogical logical && IsLogicalNode(logical))
            {
                return current;
            }
            current = current.GetVisualParent();
        }
        return visual;
    }

    public CdpSession(WebSocket webSocket, TopLevel? window)
    {
        _webSocket = webSocket;
        if (window != null)
        {
            string title = "Window";
            if (Dispatcher.UIThread.CheckAccess())
            {
                title = (window as Window)?.Title ?? window.GetType().Name;
            }
            else
            {
                title = Dispatcher.UIThread.Invoke(() => (window as Window)?.Title ?? window.GetType().Name);
            }
            var targetId = CdpServer.Register(window, title);
            _defaultTargetSession = new CdpTargetSession(this, "", targetId, window);
        }
    }

    public void AttachTarget(string sessionId, CdpTargetSession targetSession)
    {
        _attachedTargets[sessionId] = targetSession;
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

    public string RegisterObject(object obj)
    {
        string id = $"object:{Interlocked.Increment(ref _nextObjectId)}";
        RemoteObjects[id] = obj;
        return id;
    }

    public object? GetObject(string id)
    {
        return RemoteObjects.TryGetValue(id, out var obj) ? obj : null;
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
            Console.WriteLine($"Error processing message: {ex}");
        }
    }

    private async Task<JsonObject> DispatchMethodAsync(string method, JsonObject @params, string? sessionId)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
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
        });
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
            evt["sessionId"] = activeTarget.SessionId;
        }
        await SendJsonAsync(evt);
    }

    public async Task SendEventAsync(string method, JsonObject @params)
    {
        await SendEventAsync(method, @params, null);
    }

    private async Task SendJsonAsync(JsonObject node)
    {
        if (_webSocket.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
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

    public void StartScreencast(string format = "png", int? quality = null, int? maxWidth = null, int? maxHeight = null, int? everyNthFrame = null)
    {
        CurrentTargetSession?.StartScreencast(format, quality, maxWidth, maxHeight, everyNthFrame);
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

        Domains.LogDomain.RemoveSession(this);
        Domains.NetworkDomain.RemoveSession(this);
        Domains.FetchDomain.RemoveSession(this);
        Domains.RecorderDomain.RemoveSession(this);
        NodeMap.Clear();
        RemoteObjects.Clear();
        if (Window != null)
        {
            HighlightOverlayManager.HideHighlight(Window);
        }
        Domains.CssDomain.CleanupSession(this);
        Domains.PerformanceDomain.CleanupSession(this);
        Domains.TracingDomain.CleanupSession(this);
        _sendSemaphore.Dispose();
    }

    public void StartObservingVisualTree()
    {
        CurrentTargetSession?.StartObservingVisualTree();
    }

    public void StopObservingVisualTree()
    {
        CurrentTargetSession?.StopObservingVisualTree();
    }

    internal static readonly System.Reflection.PropertyInfo? VisualChildrenProperty = 
        typeof(Visual).GetProperty("VisualChildren", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

    internal static INotifyCollectionChanged? GetVisualChildrenObservable(Visual visual)
    {
        return VisualChildrenProperty?.GetValue(visual) as INotifyCollectionChanged;
    }

    internal class AnonymousObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        public AnonymousObserver(Action<T> onNext) => _onNext = onNext;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => _onNext(value);
    }

    internal static int GetVisualTreeStateHash(Visual? visual)
    {
        if (visual == null) return 0;
        int hash = visual.GetType().GetHashCode();
        hash = HashCode.Combine(hash, visual.Bounds.Width, visual.Bounds.Height, visual.IsVisible);
        
        if (visual is Panel panel && panel.Background != null)
        {
            hash = HashCode.Combine(hash, panel.Background.ToString());
        }
        else if (visual is Avalonia.Controls.Primitives.TemplatedControl tc && tc.Background != null)
        {
            hash = HashCode.Combine(hash, tc.Background.ToString());
        }

        foreach (var child in visual.GetVisualChildren())
        {
            hash = HashCode.Combine(hash, GetVisualTreeStateHash(child));
        }
        
        return hash;
    }

    internal static byte[] GetFallbackMockImageBytes(int pixelWidth, int pixelHeight, int stateHash)
    {
        try
        {
            using var bitmap = new SkiaSharp.SKBitmap(pixelWidth, pixelHeight);
            using var canvas = new SkiaSharp.SKCanvas(bitmap);
            canvas.Clear(SkiaSharp.SKColors.Transparent);
            
            using var paint = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor((uint)stateHash | 0xFF000000) };
            canvas.DrawPoint(0, 0, paint);
            
            using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return data?.ToArray() ?? Array.Empty<byte>();
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }
}

internal class AnonymousObserver<T> : IObserver<T>
{
    private readonly Action<T> _onNext;
    public AnonymousObserver(Action<T> onNext) => _onNext = onNext;
    public void OnCompleted() { }
    public void OnError(Exception error) { }
    public void OnNext(T value) => _onNext(value);
}
