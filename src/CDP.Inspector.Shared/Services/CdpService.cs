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
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services;

public class CdpService : ICdpService, INotifyPropertyChanged
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private int _messageId = 1;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> _pendingRequests = new();
    private readonly HttpClient _httpClient = new();

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
            var url = $"{host}/json";
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
            await _ws.ConnectAsync(new Uri(target.WebSocketUrl), CancellationToken.None);
            IsConnected = true;
            ConnectionStatus = "Connected";
            ConnectedHost = host;
            ConnectedTargetId = target.Id;

            // Start reader thread
            _ = Task.Run(ReceiveLoopAsync);
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
        }
    }

    public async Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
    {
        var ws = _ws;
        if (ws == null || ws.State != WebSocketState.Open)
        {
            throw new Exception("Not connected to a target");
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
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

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
