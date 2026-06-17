using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.Cdp;

public class CdpSession
{
    private readonly WebSocket _webSocket;
    private readonly CancellationTokenSource _cts = new();

    public TopLevel Window { get; }
    public NodeMap NodeMap { get; } = new();
    public ConcurrentDictionary<string, object> RemoteObjects { get; } = new();
    public int InspectedNodeId { get; set; } = 0;
    private int _nextObjectId = 1;

    public CdpSession(WebSocket webSocket, TopLevel window)
    {
        _webSocket = webSocket;
        Window = window;
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

            try
            {
                var result = await DispatchMethodAsync(method, paramsNode);
                await SendResponseAsync(id, result);
            }
            catch (Exception ex)
            {
                await SendErrorAsync(id, -32603, ex.Message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex}");
        }
    }

    private async Task<JsonObject> DispatchMethodAsync(string method, JsonObject @params)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            return await CdpDispatcher.DispatchAsync(this, method, @params);
        });
    }

    public async Task SendResponseAsync(int id, JsonObject result)
    {
        var response = new JsonObject
        {
            ["id"] = id,
            ["result"] = result
        };
        await SendJsonAsync(response);
    }

    public async Task SendErrorAsync(int id, int code, string message)
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
        await SendJsonAsync(response);
    }

    public async Task SendEventAsync(string method, JsonObject @params)
    {
        var evt = new JsonObject
        {
            ["method"] = method,
            ["params"] = @params
        };
        await SendJsonAsync(evt);
    }

    private async Task SendJsonAsync(JsonObject node)
    {
        if (_webSocket.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private void Cleanup()
    {
        _cts.Cancel();
        Domains.LogDomain.RemoveSession(this);
        NodeMap.Clear();
        RemoteObjects.Clear();
        HighlightOverlayManager.HideHighlight(Window);
    }
}
