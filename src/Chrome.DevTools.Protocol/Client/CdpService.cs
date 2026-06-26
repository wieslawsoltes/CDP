using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol;

public class CdpService : ICdpService, INotifyPropertyChanged
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private int _messageId = 1;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> _pendingRequests = new();
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    private bool _isConnected;
    private string _connectionStatus = "Disconnected";
    private string _connectedHost = "";
    private string _connectedTargetId = "";

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

    public event EventHandler<CdpEventEventArgs>? EventReceived;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public async Task<List<TargetItem>> GetTargetsAsync(string host)
    {
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
                    if (type == "page" || type == "app")
                    {
                        string title = obj["title"]?.GetValue<string>() ?? "Unnamed";
                        string wsUrl = obj["webSocketDebuggerUrl"]?.GetValue<string>() ?? "";
                        string id = obj["id"]?.GetValue<string>() ?? "";
                        list.Add(new TargetItem(title, wsUrl, id));
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

    public async Task ConnectAsync(string host, TargetItem target)
    {
        await DisconnectAsync();

        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();
        _messageId = 1;
        _pendingRequests.Clear();

        ConnectionStatus = "Connecting...";
        try
        {
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _ws.ConnectAsync(new Uri(target.WebSocketUrl), connectCts.Token);
            IsConnected = true;
            ConnectionStatus = "Connected";
            ConnectedHost = host;
            ConnectedTargetId = target.Id;

            // Start reader thread
            _ = Task.Run(ReceiveLoopAsync);

            // Enable real-time target discovery
            _ = SendCommandAsync("Target.setDiscoverTargets", new JsonObject { ["discover"] = true });
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Connection Failed";
            await DisconnectAsync();
            throw new Exception($"Failed to connect to target: {ex.Message}", ex);
        }
    }

    private readonly object _disconnectLock = new();

    public async Task DisconnectAsync()
    {
        ClientWebSocket? ws = null;
        CancellationTokenSource? cts = null;

        lock (_disconnectLock)
        {
            if (_ws == null) return;
            ws = _ws;
            cts = _cts;
            _ws = null;
            _cts = null;
            ConnectionStatus = "Disconnecting...";
        }

        try
        {
            cts?.Cancel();
            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
        catch (Exception)
        {
            // Ignore errors during close
        }
        finally
        {
            ws.Dispose();
            cts?.Dispose();
            IsConnected = false;
            ConnectionStatus = "Disconnected";
            ConnectedHost = "";
            ConnectedTargetId = "";
            IsPreviewScreencastActive = false;
            _screencastReconstructor.Dispose();
        }
    }

    public async Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
    {
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

        var tcs = new TaskCompletionSource<JsonObject>();
        _pendingRequests[id] = tcs;

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
        if (response.ContainsKey("error"))
        {
            var err = response["error"] as JsonObject;
            throw new Exception(err?["message"]?.GetValue<string>() ?? "Unknown CDP error");
        }

        return response["result"] as JsonObject ?? new JsonObject();
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[8192];
        try
        {
            while (_ws != null && _ws.State == WebSocketState.Open && !_cts!.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
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
                                    var fullBytes = _screencastReconstructor.EncodeToJpeg(90);
                                    LastReconstructedFrameBytes = fullBytes;

                                    if (RecordFullFrames)
                                    {
                                        parameters["data"] = Convert.ToBase64String(fullBytes);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error reconstructing tiled screencast frame: {ex.Message}");
                            }
                        }
                    }

                    // Raise event to subscribers
                    EventReceived?.Invoke(this, new CdpEventEventArgs(method, parameters));
                }
            }
        }
        catch (Exception)
        {
            // Ignore socket receive exceptions
        }
        finally
        {
            if (IsConnected)
            {
                // Force disconnection cleanup in a background task
                _ = Task.Run(DisconnectAsync);
            }
        }
    }
}
