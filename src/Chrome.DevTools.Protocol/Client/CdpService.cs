using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Chrome.DevTools.Protocol;

public class CdpService : ICdpService, INotifyPropertyChanged
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<CdpService>();
    private ClientWebSocket? _ws;
    private OsAutomationCdpSession? _osSession;
    private CancellationTokenSource? _cts;
    private int _messageId = 1;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> _pendingRequests = new();
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private SynchronizationContext? _notificationContext;

    private bool _isConnected;
    private string _connectionStatus = "Disconnected";
    private string _connectedHost = "";
    private string _connectedTargetId = "";
    private string _connectedTargetType = "";
    private string _connectedTargetUrl = "";
    private IReadOnlySet<string> _supportedDomains = new HashSet<string>();

    public bool IsConnected
    {
        get => _isConnected;
        private set { _isConnected = value; OnPropertyChanged(nameof(IsConnected)); }
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set { _connectionStatus = value; OnPropertyChanged(nameof(ConnectionStatus)); }
    }

    public string ConnectedHost
    {
        get => _connectedHost;
        private set { _connectedHost = value; OnPropertyChanged(nameof(ConnectedHost)); }
    }

    public string ConnectedTargetId
    {
        get => _connectedTargetId;
        private set { _connectedTargetId = value; OnPropertyChanged(nameof(ConnectedTargetId)); }
    }

    public string ConnectedTargetType
    {
        get => _connectedTargetType;
        private set { _connectedTargetType = value; OnPropertyChanged(nameof(ConnectedTargetType)); }
    }

    public string ConnectedTargetUrl
    {
        get => _connectedTargetUrl;
        private set { _connectedTargetUrl = value; OnPropertyChanged(nameof(ConnectedTargetUrl)); }
    }

    public IReadOnlySet<string> SupportedDomains
    {
        get => _supportedDomains;
        private set { _supportedDomains = value; OnPropertyChanged(nameof(SupportedDomains)); }
    }

    public bool SupportsDomain(string domain) => SupportedDomains.Count == 0 || SupportedDomains.Contains(domain);

    private bool _isPreviewScreencastActive;
    public bool IsPreviewScreencastActive
    {
        get => _isPreviewScreencastActive;
        set { _isPreviewScreencastActive = value; OnPropertyChanged(nameof(IsPreviewScreencastActive)); }
    }

    private bool _recordFullFrames;
    public bool RecordFullFrames
    {
        get => _recordFullFrames;
        set { _recordFullFrames = value; OnPropertyChanged(nameof(RecordFullFrames)); }
    }

    private byte[]? _lastReconstructedFrameBytes;
    public byte[]? LastReconstructedFrameBytes
    {
        get => _lastReconstructedFrameBytes;
        private set { _lastReconstructedFrameBytes = value; OnPropertyChanged(nameof(LastReconstructedFrameBytes)); }
    }

    private readonly ScreencastReconstructor _screencastReconstructor = new();
    public ScreencastReconstructor ScreencastReconstructor => _screencastReconstructor;

    private int _lastDispatchedIndex = -1;
    public ITimeMachineService TimeMachine { get; } = new TimeMachineService();

    public event EventHandler<CdpEventEventArgs>? EventReceived;
    public event PropertyChangedEventHandler? PropertyChanged;

    public CdpService()
    {
        TimeMachine.FrameChanged += TimeMachine_FrameChanged;
        TimeMachine.ReplayStateCleared += TimeMachine_ReplayStateCleared;
    }

    private void TimeMachine_ReplayStateCleared(object? sender, EventArgs e)
    {
        _lastDispatchedIndex = -1;
    }

    private void TimeMachine_FrameChanged(object? sender, EventArgs e)
    {
        if (TimeMachine.IsReplaying)
        {
            var currentIndex = TimeMachine.CurrentFrameIndex;
            var frames = TimeMachine.Frames;
            if (currentIndex >= 0 && currentIndex < frames.Count)
            {
                if (currentIndex == _lastDispatchedIndex + 1)
                {
                    var frame = frames[currentIndex];
                    if (frame.Type == "Event")
                    {
                        EventReceived?.Invoke(this, new CdpEventEventArgs(frame.Method, frame.Payload ?? new JsonObject()));
                    }
                    _lastDispatchedIndex = currentIndex;
                }
                else
                {
                    for (int i = 0; i <= currentIndex; i++)
                    {
                        var frame = frames[i];
                        if (frame.Type == "Event")
                        {
                            EventReceived?.Invoke(this, new CdpEventEventArgs(frame.Method, frame.Payload ?? new JsonObject()));
                        }
                    }
                    _lastDispatchedIndex = currentIndex;
                }
            }
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        var context = _notificationContext;
        if (context is not null && !ReferenceEquals(SynchronizationContext.Current, context))
        {
            context.Post(static state =>
            {
                var (service, name) = ((CdpService Service, string Name))state!;
                service.RaisePropertyChanged(name);
            }, (this, propertyName));
            return;
        }

        RaisePropertyChanged(propertyName);
    }

    private void RaisePropertyChanged(string propertyName)
    {
        var subscribers = PropertyChanged;
        if (subscribers is null) return;
        var args = new PropertyChangedEventArgs(propertyName);
        foreach (PropertyChangedEventHandler subscriber in subscribers.GetInvocationList())
        {
            try
            {
                subscriber(this, args);
            }
            catch (Exception ex)
            {
                Logger.LogErrorMessage("CdpService", $"Property subscriber failed for {propertyName}", ex);
            }
        }
    }

    public async Task<List<TargetItem>> GetTargetsAsync(string host)
    {
        if (host != null && host.StartsWith("os://", StringComparison.OrdinalIgnoreCase))
        {
            var list = new List<TargetItem>();
            try
            {
                var windows = OsAutomationProvider.Instance?.GetWindows() ?? System.Array.Empty<CDP.Automation.OS.OSWindow>();
                foreach (var win in windows)
                {
                    list.Add(new TargetItem(win.Title, $"os://{win.Id}", win.Id));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to get OS windows for target list");
            }
            return list;
        }

        try
        {
            var targetHost = host;
            if (!string.IsNullOrEmpty(host))
            {
                if (host.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var uri = new Uri(host);
                        targetHost = $"http://{uri.Authority}";
                    }
                    catch
                    {
                        // Fallback if parsing fails
                    }
                }
                else if (host.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var uri = new Uri(host);
                        targetHost = $"https://{uri.Authority}";
                    }
                    catch
                    {
                        // Fallback
                    }
                }
            }

            if (targetHost != null)
            {
                targetHost = targetHost.TrimEnd('/');
            }
            var url = $"{targetHost}/json";
            var jsonStr = await _httpClient.GetStringAsync(url);
            var arr = JsonNode.Parse(jsonStr) as JsonArray;
            var list = new List<TargetItem>();
            if (arr != null)
            {
                foreach (var item in arr)
                {
                    var obj = item as JsonObject;
                    if (obj == null) continue;
                    string type = obj["type"]?.GetValue<string>() ?? "";
                    if (type is "page" or "app" or "node" or "worker")
                    {
                        string title = obj["title"]?.GetValue<string>() ?? "Unnamed";
                        string wsUrl = obj["webSocketDebuggerUrl"]?.GetValue<string>() ?? "";
                        string id = obj["id"]?.GetValue<string>() ?? "";
                        string targetUrl = obj["url"]?.GetValue<string>() ?? "";
                        string description = obj["description"]?.GetValue<string>() ?? "";
                        list.Add(new TargetItem(title, wsUrl, id, type, targetUrl, description));
                    }
                }
            }
            return list;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to scan targets: {ex.Message}", ex);
        }
    }

    public Task ConnectAsync(string host, TargetItem target) => ConnectAsync(host, target, autoResume: false);

    public async Task ConnectAsync(string host, TargetItem target, bool autoResume)
    {
        // Preserve the caller's notification context (Avalonia's UI dispatcher
        // in the desktop inspector) across protocol awaits and receive-loop work.
        _notificationContext = SynchronizationContext.Current ?? _notificationContext;
        await DisconnectAsync();
        TimeMachine.IsReplaying = false;

        if (host != null && host.StartsWith("os://", StringComparison.OrdinalIgnoreCase))
        {
            ConnectionStatus = "Connecting...";
            try
            {
                _osSession = new OsAutomationCdpSession(target.Id);
                _osSession.EventReceived += (sender, args) =>
                {
                    EventReceived?.Invoke(this, args);
                };
                IsConnected = true;
                bool hasAccess = true;
                bool hasScreen = true;
                try
                {
                    hasAccess = OsAutomationProvider.Instance?.HasAccessibilityPermission() ?? true;
                    hasScreen = OsAutomationProvider.Instance?.HasScreenCapturePermission() ?? true;
                }
                catch {}

                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    if (!hasAccess && !hasScreen)
                    {
                        ConnectionStatus = "Connected (No Accessibility & Screen Recording Permissions)";
                    }
                    else if (!hasAccess)
                    {
                        ConnectionStatus = "Connected (No Accessibility Permission)";
                    }
                    else if (!hasScreen)
                    {
                        ConnectionStatus = "Connected (No Screen Recording Permission)";
                    }
                    else
                    {
                        ConnectionStatus = "Connected";
                    }
                }
                else
                {
                    ConnectionStatus = "Connected";
                }
                ConnectedHost = host;
                ConnectedTargetId = target.Id;
                ConnectedTargetType = target.Type;
                ConnectedTargetUrl = target.Url;
                SupportedDomains = new HashSet<string>();
                Logger.ClientConnected(target.Id, host);
                return;
            }
            catch (Exception ex)
            {
                ConnectionStatus = "Connection Failed";
                Logger.ClientConnectionFailed(ex.Message, ex);
                await DisconnectAsync();
                throw new Exception($"Failed to connect to target: {ex.Message}", ex);
            }
        }

        _ws = new ClientWebSocket();
        ConfigureInspectorKeepAlive(_ws.Options);
        _cts = new CancellationTokenSource();
        _pendingRequests.Clear();

        ConnectionStatus = "Connecting...";
        try
        {
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _ws.ConnectAsync(new Uri(target.WebSocketUrl), connectCts.Token);
            ConnectedHost = host;
            ConnectedTargetId = target.Id;
            ConnectedTargetType = target.Type;
            ConnectedTargetUrl = target.Url;
            Logger.ClientConnected(target.Id, host);

            // Bind the reader to this exact connection. A stale reader from a
            // previous session must never consume or close a newer session.
            var connectedSocket = _ws;
            var connectedCancellation = _cts;
            _ = Task.Run(() => ReceiveLoopAsync(connectedSocket, connectedCancellation));

            await DiscoverSupportedDomainsAsync().ConfigureAwait(false);

            // Node starts behind a Runtime waiting gate. Enable the two V8
            // debugger domains before publishing IsConnected so no startup pause
            // or scriptParsed event can be lost during panel initialization.
            if (autoResume && target.Type is "node" or "worker")
            {
                if (SupportsDomain("Runtime")) await SendCommandAsync("Runtime.enable").ConfigureAwait(false);
                if (SupportsDomain("Debugger")) await SendCommandAsync("Debugger.enable").ConfigureAwait(false);
            }

            IsConnected = true;
            ConnectionStatus = "Connected";

            // Standalone V8 Inspector targets do not implement the browser Target domain.
            if (SupportsDomain("Target") && target.Type is "page" or "app")
            {
                _ = SendCommandAsync("Target.setDiscoverTargets", new JsonObject { ["discover"] = true });
            }

            if (autoResume)
            {
                // Automatically resume targets that are blocked waiting for debugger connections
                await SendCommandAsync("Runtime.runIfWaitingForDebugger").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Connection Failed";
            Logger.ClientConnectionFailed(ex.Message, ex);
            await DisconnectAsync();
            throw new Exception($"Failed to connect to target: {ex.Message}", ex);
        }
    }

    private static void ConfigureInspectorKeepAlive(ClientWebSocketOptions options)
    {
        // ClientWebSocket defaults to an unsolicited PONG every 30 seconds.
        // Standalone V8 inspector endpoints can treat that unsolicited control
        // frame as a protocol error and abruptly close an otherwise healthy
        // debugging session. Use the standard PING/PONG exchange instead.
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.KeepAliveTimeout = TimeSpan.FromSeconds(5);
    }

    private readonly object _disconnectLock = new();

    public async Task DisconnectAsync()
    {
        if (_osSession != null)
        {
            lock (_disconnectLock)
            {
                try
                {
                    _osSession.Dispose();
                }
                catch {}
                _osSession = null;
                IsConnected = false;
                IsPreviewScreencastActive = false;
                ConnectionStatus = "Disconnected";
                ConnectedHost = "";
                ConnectedTargetId = "";
                ConnectedTargetType = "";
                ConnectedTargetUrl = "";
                SupportedDomains = new HashSet<string>();
            }
            Logger.ClientDisconnected();
            return;
        }

        ClientWebSocket? ws = null;
        CancellationTokenSource? cts = null;

        lock (_disconnectLock)
        {
            if (_ws == null) return;
            ws = _ws;
            cts = _cts;
            _ws = null;
            _cts = null;
            IsConnected = false;
            IsPreviewScreencastActive = false;
            ConnectionStatus = "Disconnecting...";
        }

        try
        {
            cts?.Cancel();
            if (ws.State == WebSocketState.Open)
            {
                var closeTask = ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                await Task.WhenAny(closeTask, Task.Delay(1000));
            }
        }
        catch (Exception)
        {
            // Ignore errors during close
        }
        finally
        {
            Logger.ClientDisconnected();
            ws.Dispose();
            cts?.Dispose();
            ConnectionStatus = "Disconnected";
            ConnectedHost = "";
            ConnectedTargetId = "";
            ConnectedTargetType = "";
            ConnectedTargetUrl = "";
            SupportedDomains = new HashSet<string>();
            _screencastReconstructor.Dispose();
        }
    }

    private async Task DiscoverSupportedDomainsAsync()
    {
        try
        {
            var response = await SendCommandAsync("Schema.getDomains").ConfigureAwait(false);
            var domains = response["domains"] as JsonArray;
            if (domains is null)
            {
                SupportedDomains = new HashSet<string>();
                return;
            }

            SupportedDomains = domains
                .OfType<JsonObject>()
                .Select(domain => domain["name"]?.GetValue<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Target does not expose Schema.getDomains; capabilities will be discovered lazily");
            SupportedDomains = new HashSet<string>();
        }
    }

    public async Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
    {
        if (TimeMachine.IsReplaying)
        {
            var replayResponse = TimeMachine.GetReplayResponse(method, parameters);
            return replayResponse ?? new JsonObject();
        }

        if (_osSession != null)
        {
            var osResult = await _osSession.HandleCommandAsync(method, parameters);
            if (TimeMachine.IsRecording)
            {
                TimeMachine.RecordResponse(method, parameters, osResult);
            }
            return osResult;
        }

        var ws = _ws;
        if (ws == null || ws.State != WebSocketState.Open)
        {
            throw new Exception($"Not connected to a target (ws is {(ws == null ? "null" : ws.State.ToString())})");
        }

        int id = Interlocked.Increment(ref _messageId);
        var request = new JsonObject
        {
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters ?? new JsonObject()
        };

        var tcs = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;
        Logger.SendingCommand(method, id);

        var bytes = Encoding.UTF8.GetBytes(request.ToJsonString());
        
        await _sendSemaphore.WaitAsync();
        try
        {
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        finally
        {
            _sendSemaphore.Release();
        }

        var response = await tcs.Task;
        Logger.ReceivedResponse(id);
        if (response.ContainsKey("error"))
        {
            var err = response["error"] as JsonObject;
            throw new Exception(err?["message"]?.GetValue<string>() ?? "Unknown CDP error");
        }

        var resultNode = response["result"] as JsonObject ?? new JsonObject();
        if (TimeMachine.IsRecording)
        {
            TimeMachine.RecordResponse(method, parameters, resultNode);
        }
        return resultNode;
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationTokenSource cancellation)
    {
        var buffer = new byte[8192];
        try
        {
            while (socket.State == WebSocketState.Open && !cancellation.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation.Token);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var jsonStr = Encoding.UTF8.GetString(ms.ToArray());
                var node = JsonNode.Parse(jsonStr, null, new JsonDocumentOptions { MaxDepth = 1024 }) as JsonObject;
                if (node == null) continue;

                if (node.ContainsKey("id"))
                {
                    int id = node["id"]!.GetValue<int>();
                    if (_pendingRequests.TryRemove(id, out var tcs))
                    {
                        tcs.SetResult(node);
                    }
                }
                else if (node.ContainsKey("method"))
                {
                    string method = node["method"]!.GetValue<string>();
                    var parameters = node["params"] as JsonObject ?? new JsonObject();
                    if (method != "Log.entryAdded")
                    {
                        Logger.ReceivedEvent(method);
                    }

                    if (method == "Page.screencastFrame")
                    {
                        var transferMode = parameters["transferMode"]?.GetValue<string>();
                        if (string.Equals(transferMode, "tiled", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                int pixelWidth = parameters["pixelWidth"]?.GetValue<int>() ?? 0;
                                int pixelHeight = parameters["pixelHeight"]?.GetValue<int>() ?? 0;
                                int tileWidth = parameters["tileWidth"]?.GetValue<int>() ?? 64;
                                int tileHeight = parameters["tileHeight"]?.GetValue<int>() ?? 64;
                                var tiles = parameters["tiles"] as JsonArray;

                                if (tiles != null && pixelWidth > 0 && pixelHeight > 0)
                                {
                                    _screencastReconstructor.Update(pixelWidth, pixelHeight, tileWidth, tileHeight, tiles);

                                    if (RecordFullFrames)
                                    {
                                        var fullBytes = _screencastReconstructor.EncodeToJpeg(90);
                                        parameters["data"] = Convert.ToBase64String(fullBytes);
                                        LastReconstructedFrameBytes = fullBytes;
                                    }
                                    else
                                    {
                                        LastReconstructedFrameBytes = null;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogErrorMessage("CdpService", "Error reconstructing tiled screencast frame", ex);
                            }
                        }
                    }

                    // Record event
                    TimeMachine.RecordEvent(method, parameters);

                    // A UI subscriber must not be able to terminate the transport
                    // receive loop and disconnect every other protocol consumer.
                    var eventArgs = new CdpEventEventArgs(method, parameters);
                    var subscribers = EventReceived;
                    if (subscribers is not null)
                    {
                        foreach (EventHandler<CdpEventEventArgs> subscriber in subscribers.GetInvocationList())
                        {
                            try
                            {
                                subscriber(this, eventArgs);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogErrorMessage("CdpService", $"Event subscriber failed for {method}", ex);
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Expected during an explicit disconnect or reconnect.
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("CdpService", "ReceiveLoopAsync Exception", ex);
        }
        finally
        {
            bool ownsCurrentConnection;
            lock (_disconnectLock)
            {
                ownsCurrentConnection = ReferenceEquals(_ws, socket) && ReferenceEquals(_cts, cancellation);
            }

            if (ownsCurrentConnection)
            {
                foreach (var pending in _pendingRequests.ToArray())
                {
                    if (_pendingRequests.TryRemove(pending.Key, out var completion))
                    {
                        completion.TrySetException(new WebSocketException("The CDP connection closed before a response was received."));
                    }
                }

                if (IsConnected)
                {
                    // Force disconnection cleanup in a background task.
                    _ = Task.Run(DisconnectAsync);
                }
            }
        }
    }
}
