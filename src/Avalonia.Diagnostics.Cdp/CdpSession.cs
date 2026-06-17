using System;
using System.Collections.Concurrent;
using System.IO;
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

namespace Avalonia.Diagnostics.Cdp;

public class CdpSession
{
    private readonly WebSocket _webSocket;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    public TopLevel Window { get; }
    public NodeMap NodeMap { get; } = new();
    public ConcurrentDictionary<string, object> RemoteObjects { get; } = new();
    public int InspectedNodeId { get; set; } = 0;
    private int _nextObjectId = 1;

    private bool _inspectModeEnabled;
    public bool InspectModeEnabled
    {
        get => _inspectModeEnabled;
        set
        {
            if (_inspectModeEnabled != value)
            {
                _inspectModeEnabled = value;
                UpdateInspectModeHandlers();
            }
        }
    }

    private void UpdateInspectModeHandlers()
    {
        if (_inspectModeEnabled)
        {
            Window.AddHandler(InputElement.PointerMovedEvent, OnInspectPointerMoved, RoutingStrategies.Tunnel);
            Window.AddHandler(InputElement.PointerPressedEvent, OnInspectPointerPressed, RoutingStrategies.Tunnel);
        }
        else
        {
            Window.RemoveHandler(InputElement.PointerMovedEvent, OnInspectPointerMoved);
            Window.RemoveHandler(InputElement.PointerPressedEvent, OnInspectPointerPressed);
            HighlightOverlayManager.HideHighlight(Window);
        }
    }

    private void OnInspectPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_inspectModeEnabled) return;
        var pos = e.GetPosition(Window);
        var hit = Window.InputHitTest(pos);
        if (hit is Visual visual)
        {
            HighlightOverlayManager.ShowHighlight(Window, visual);
        }
        else
        {
            HighlightOverlayManager.HideHighlight(Window);
        }
    }

    private async void OnInspectPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_inspectModeEnabled) return;
        e.Handled = true;
        var pos = e.GetPosition(Window);
        var hit = Window.InputHitTest(pos);
        if (hit is Visual visual)
        {
            var nodeId = NodeMap.GetOrAdd(visual);
            await SendEventAsync("Overlay.inspectNodeRequested", new JsonObject
            {
                ["backendNodeId"] = nodeId
            });
        }
    }

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
        await _sendSemaphore.WaitAsync();
        try
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private bool _screencastEnabled;
    private int _lastScreencastFrameId;
    private volatile int _ackedFrameId;

    public void StartScreencast()
    {
        if (_screencastEnabled) return;
        _screencastEnabled = true;
        _lastScreencastFrameId = 0;
        _ackedFrameId = 0;

        Task.Run(async () =>
        {
            try
            {
                while (_screencastEnabled && !_cts.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
                {
                    if (_lastScreencastFrameId > _ackedFrameId)
                    {
                        await Task.Delay(50);
                        continue;
                    }

                    string base64Data = "";
                    double width = 0;
                    double height = 0;
                    
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            var scale = Window.RenderScaling;
                            width = Window.Bounds.Width;
                            height = Window.Bounds.Height;
                            int pixelWidth = Math.Max(1, (int)(width * scale));
                            int pixelHeight = Math.Max(1, (int)(height * scale));

                            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96 * scale, 96 * scale));
                            bitmap.Render(Window);

                            using var ms = new MemoryStream();
                            bitmap.Save(ms);
                            base64Data = Convert.ToBase64String(ms.ToArray());
                        }
                        catch { }
                    });

                    if (string.IsNullOrEmpty(base64Data))
                    {
                        await Task.Delay(100);
                        continue;
                    }

                    var currentFrameId = ++_lastScreencastFrameId;

                    var metadata = new JsonObject
                    {
                        ["deviceWidth"] = width,
                        ["deviceHeight"] = height,
                        ["offsetTop"] = 0,
                        ["pageScaleFactor"] = 1,
                        ["scrollX"] = 0,
                        ["scrollY"] = 0,
                        ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

                    await SendEventAsync("Page.screencastFrame", new JsonObject
                    {
                        ["data"] = base64Data,
                        ["metadata"] = metadata,
                        ["sessionId"] = currentFrameId
                    });

                    await Task.Delay(100);
                }
            }
            catch { }
        });
    }

    public void StopScreencast()
    {
        _screencastEnabled = false;
    }

    public void AcknowledgeScreencastFrame(int sessionId)
    {
        _ackedFrameId = Math.Max(_ackedFrameId, sessionId);
    }

    private void Cleanup()
    {
        _cts.Cancel();
        StopScreencast();
        InspectModeEnabled = false;
        Domains.LogDomain.RemoveSession(this);
        Domains.NetworkDomain.RemoveSession(this);
        Domains.RecorderDomain.RemoveSession(this);
        NodeMap.Clear();
        RemoteObjects.Clear();
        HighlightOverlayManager.HideHighlight(Window);
        _sendSemaphore.Dispose();
    }
}
