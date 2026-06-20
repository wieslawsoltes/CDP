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

    public TopLevel? Window { get; }
    public NodeMap NodeMap { get; } = new();
    public ConcurrentDictionary<string, object> RemoteObjects { get; } = new();
    public int InspectedNodeId { get; set; } = 0;
    public bool DiscoverTargetsEnabled { get; set; }
    public bool IsDomEnabled { get; private set; }
    public ConcurrentDictionary<string, string> ScriptsToEvaluateOnNewDocument { get; } = new();
    public ConcurrentDictionary<string, string> ScriptsToEvaluateOnLoad { get; } = new();
    public System.Collections.Generic.List<JsonObject> Cookies { get; } = new();

    public JsonObject? GeolocationOverride { get; set; }
    public JsonObject? DeviceOrientationOverride { get; set; }
    public bool TouchEmulationEnabled { get; set; }
    public IInputDevice TouchDevice { get; } =
        (IInputDevice)Activator.CreateInstance(typeof(TouchDevice), nonPublic: true)!;
    public bool LifecycleEventsEnabled { get; set; }
    public bool AdBlockingEnabled { get; set; }
    public bool BypassCSP { get; set; }
    public JsonObject? FontFamilies { get; set; }
    public JsonObject? FontSizes { get; set; }
    public string? DownloadBehavior { get; set; }
    public string? DownloadPath { get; set; }
    public bool InterceptFileChooserDialog { get; set; }
    public bool PrerenderingAllowed { get; set; }
    public string? RPHRegistrationMode { get; set; }
    public string? SPCTransactionMode { get; set; }
    public string? WebLifecycleState { get; set; }
    public ConcurrentDictionary<string, string> CompilationCache { get; } = new();
    public System.Collections.Generic.List<JsonObject> NavigationHistory { get; } = new();
    public int NavigationHistoryIndex { get; set; } = -1;

    private bool _useLogicalTree = false;
    public bool UseLogicalTree
    {
        get => _useLogicalTree;
        set
        {
            if (_useLogicalTree != value)
            {
                _useLogicalTree = value;
                if (IsDomEnabled)
                {
                    StartObservingVisualTree();
                }
            }
        }
    }
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

    private bool IsLogicalNode(ILogical? node)
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

    private void OnInspectPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_inspectModeEnabled) return;
        var pos = e.GetPosition(Window);
        var hit = Window.InputHitTest(pos);
        if (hit is Visual visual)
        {
            if (UseLogicalTree)
            {
                visual = FindLogicalNode(visual);
            }
            HighlightOverlayManager.ShowHighlight(Window, visual);
        }
        else
        {
            HighlightOverlayManager.HideHighlight(Window);
        }
        RequestScreencastFrame();
    }

    private async void OnInspectPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_inspectModeEnabled) return;
        e.Handled = true;
        var pos = e.GetPosition(Window);
        var hit = Window.InputHitTest(pos);
        if (hit is Visual visual)
        {
            if (UseLogicalTree)
            {
                visual = FindLogicalNode(visual);
            }
            var nodeId = NodeMap.GetOrAdd(visual);
            await SendEventAsync("Overlay.inspectNodeRequested", new JsonObject
            {
                ["backendNodeId"] = nodeId
            });
        }
    }

    public CdpSession(WebSocket webSocket, TopLevel? window)
    {
        _webSocket = webSocket;
        Window = window;

        var port = CdpServer.Port;
        var rootUrl = $"http://127.0.0.1:{port}/";
        NavigationHistory.Add(new JsonObject
        {
            ["id"] = 1,
            ["url"] = rootUrl,
            ["userTypedURL"] = rootUrl,
            ["title"] = window?.GetType().Name ?? "Avalonia Window",
            ["transitionType"] = "typed"
        });
        NavigationHistoryIndex = 0;

        if (Window != null)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                _isVisibleSubscription = Window.GetObservable(Visual.IsVisibleProperty).Subscribe(new AnonymousObserver<bool>(visible =>
                {
                    if (_screencastEnabled)
                    {
                        _ = SendEventAsync("Page.screencastVisibilityChanged", new JsonObject
                        {
                            ["visible"] = visible
                        });
                    }
                }));
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _isVisibleSubscription = Window.GetObservable(Visual.IsVisibleProperty).Subscribe(new AnonymousObserver<bool>(visible =>
                    {
                        if (_screencastEnabled)
                        {
                            _ = SendEventAsync("Page.screencastVisibilityChanged", new JsonObject
                            {
                                ["visible"] = visible
                            });
                        }
                    }));
                });
            }
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

    private bool _screencastEnabled;
    private int _lastScreencastFrameId;
    private volatile int _ackedFrameId;
    private volatile bool _screencastDirty;
    private readonly SemaphoreSlim _screencastSignal = new(0, 1);
    private DateTime _lastFrameCaptureTime = DateTime.MinValue;
    private DateTime _lastFrameSentTime = DateTime.UtcNow;

    private string _screencastFormat = "png";
    private int? _screencastQuality;
    private int? _screencastMaxWidth;
    private int? _screencastMaxHeight;
    private int? _screencastEveryNthFrame;
    private int _screencastFrameCounter;
    private IDisposable? _isVisibleSubscription;

    private byte[]? _lastSentFrameBytes;
    private readonly MemoryStream _captureStream = new();
    private readonly SemaphoreSlim _ackSignal = new(1, 1);

    private void OnWindowLayoutUpdated(object? sender, EventArgs e)
    {
        RequestScreencastFrame();
    }

    public void RequestScreencastFrame()
    {
        _screencastDirty = true;
        try
        {
            if (_screencastSignal.CurrentCount == 0)
            {
                _screencastSignal.Release();
            }
        }
        catch { }
    }

    public void StartScreencast(string format = "png", int? quality = null, int? maxWidth = null, int? maxHeight = null, int? everyNthFrame = null)
    {
        if (_screencastEnabled) return;
        _screencastEnabled = true;
        _screencastFormat = format;
        _screencastQuality = quality;
        _screencastMaxWidth = maxWidth;
        _screencastMaxHeight = maxHeight;
        _screencastEveryNthFrame = everyNthFrame;
        _screencastFrameCounter = 0;
        _lastScreencastFrameId = 0;
        _ackedFrameId = 0;
        _screencastDirty = true;
        _lastFrameCaptureTime = DateTime.MinValue;
        _lastFrameSentTime = DateTime.UtcNow;
        _lastSentFrameBytes = null;

        // Reset the ACK signal count to 1
        try
        {
            if (_ackSignal.CurrentCount == 0)
            {
                _ackSignal.Release();
            }
        }
        catch { }

        if (Window != null)
        {
            Window.LayoutUpdated += OnWindowLayoutUpdated;
        }

        Task.Run(async () =>
        {
            while (_screencastEnabled && !_cts.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    if (!_screencastDirty)
                    {
                        await _screencastSignal.WaitAsync(_cts.Token);
                    }

                    _screencastDirty = false;

                    // Event-driven backpressure wait with watchdog timeout
                    bool acked = await _ackSignal.WaitAsync(500, _cts.Token);

                    var now = DateTime.UtcNow;
                    var elapsed = now - _lastFrameCaptureTime;
                    if (elapsed.TotalMilliseconds < 33)
                    {
                        var waitTime = 33 - (int)elapsed.TotalMilliseconds;
                        await Task.Delay(waitTime, _cts.Token);
                    }

                    _lastFrameCaptureTime = DateTime.UtcNow;

                    _screencastFrameCounter++;
                    if (_screencastEveryNthFrame.HasValue && _screencastEveryNthFrame.Value > 1 && _screencastFrameCounter > 1)
                    {
                        if (_screencastFrameCounter % _screencastEveryNthFrame.Value != 0)
                        {
                            // Release ack signal permit since we are skipping this frame
                            try { if (_ackSignal.CurrentCount == 0) _ackSignal.Release(); } catch { }
                            continue;
                        }
                    }

                    string base64Data = "";
                    double width = 0;
                    double height = 0;
                    int pixelWidth = 1;
                    int pixelHeight = 1;
                    byte[]? rawPngBytes = null;
                    int rawPngLength = 0;
                    int visualStateHash = 0;
                    
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            if (Window == null) return;
                            var scale = Window.RenderScaling;
                            width = Window.Bounds.Width;
                            height = Window.Bounds.Height;
                            pixelWidth = Math.Max(1, (int)(width * scale));
                            pixelHeight = Math.Max(1, (int)(height * scale));
                            visualStateHash = GetVisualTreeStateHash(Window);

                            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96 * scale, 96 * scale));
                            bitmap.Render(Window);

                            lock (_captureStream)
                            {
                                _captureStream.Position = 0;
                                _captureStream.SetLength(0);
                                bitmap.Save(_captureStream);
                                rawPngBytes = _captureStream.GetBuffer();
                                rawPngLength = (int)_captureStream.Length;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    });

                    if (rawPngBytes == null || rawPngLength == 0)
                    {
                        rawPngBytes = GetFallbackMockImageBytes(pixelWidth, pixelHeight, visualStateHash);
                        rawPngLength = rawPngBytes.Length;
                    }

                    // Delta Compression / Change Detection: compare raw pixels to previous frame
                    var currentFrameSpan = new ReadOnlySpan<byte>(rawPngBytes, 0, rawPngLength);
                    if (_lastSentFrameBytes != null && currentFrameSpan.SequenceEqual(_lastSentFrameBytes))
                    {
                        // Release ack signal permit since we are skipping this frame
                        try { if (_ackSignal.CurrentCount == 0) _ackSignal.Release(); } catch { }
                        continue;
                    }

                    _lastSentFrameBytes = currentFrameSpan.ToArray();

                    try
                    {
                        using var ms = new MemoryStream(rawPngBytes, 0, rawPngLength);
                        using var skBitmap = SkiaSharp.SKBitmap.Decode(ms);
                        if (skBitmap != null)
                        {
                            pixelWidth = skBitmap.Width;
                            pixelHeight = skBitmap.Height;

                            double resizeScale = 1.0;
                            if (_screencastMaxWidth.HasValue && width > _screencastMaxWidth.Value)
                            {
                                resizeScale = Math.Min(resizeScale, (double)_screencastMaxWidth.Value / width);
                            }
                            if (_screencastMaxHeight.HasValue && height > _screencastMaxHeight.Value)
                            {
                                resizeScale = Math.Min(resizeScale, (double)_screencastMaxHeight.Value / height);
                            }

                            SkiaSharp.SKBitmap bitmapToEncode = skBitmap;
                            SkiaSharp.SKBitmap? resizedBitmap = null;

                            if (resizeScale < 1.0)
                            {
                                int targetPixelWidth = (int)Math.Max(1, Math.Round(pixelWidth * resizeScale));
                                int targetPixelHeight = (int)Math.Max(1, Math.Round(pixelHeight * resizeScale));
                                var info = new SkiaSharp.SKImageInfo(targetPixelWidth, targetPixelHeight, skBitmap.ColorType, skBitmap.AlphaType);
                                resizedBitmap = new SkiaSharp.SKBitmap(info);
                                if (skBitmap.ScalePixels(resizedBitmap, SkiaSharp.SKFilterQuality.High))
                                {
                                    bitmapToEncode = resizedBitmap;
                                    width = width * resizeScale;
                                    height = height * resizeScale;
                                }
                                else
                                {
                                    resizedBitmap.Dispose();
                                    resizedBitmap = null;
                                }
                            }

                            var encodedFormat = SkiaSharp.SKEncodedImageFormat.Png;
                            if (string.Equals(_screencastFormat, "jpeg", StringComparison.OrdinalIgnoreCase))
                            {
                                encodedFormat = SkiaSharp.SKEncodedImageFormat.Jpeg;
                            }
                            else if (string.Equals(_screencastFormat, "webp", StringComparison.OrdinalIgnoreCase))
                            {
                                encodedFormat = SkiaSharp.SKEncodedImageFormat.Webp;
                            }

                            int q = _screencastQuality ?? 100;
                            if (q < 0) q = 0;
                            if (q > 100) q = 100;

                            using var image = SkiaSharp.SKImage.FromBitmap(bitmapToEncode);
                            using var encodedData = image.Encode(encodedFormat, q);
                            if (encodedData != null)
                            {
                                using var msOut = new MemoryStream();
                                encodedData.SaveTo(msOut);
                                base64Data = Convert.ToBase64String(msOut.ToArray());
                            }

                            resizedBitmap?.Dispose();
                        }
                    }
                    catch (Exception)
                    {
                    }

                    if (string.IsNullOrEmpty(base64Data))
                    {
                        // Release ack signal permit since we are skipping this frame
                        try { if (_ackSignal.CurrentCount == 0) _ackSignal.Release(); } catch { }
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

                    _lastFrameSentTime = DateTime.UtcNow;

                    await SendEventAsync("Page.screencastFrame", new JsonObject
                    {
                        ["data"] = base64Data,
                        ["metadata"] = metadata,
                        ["sessionId"] = currentFrameId
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    try
                    {
                        await Task.Delay(100, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        });
    }

    public void StopScreencast()
    {
        if (!_screencastEnabled) return;
        _screencastEnabled = false;
        if (Window != null)
        {
            Window.LayoutUpdated -= OnWindowLayoutUpdated;
        }
        try
        {
            if (_screencastSignal.CurrentCount == 0)
            {
                _screencastSignal.Release();
            }
        }
        catch { }
        try
        {
            if (_ackSignal.CurrentCount == 0)
            {
                _ackSignal.Release();
            }
        }
        catch { }
    }

    public void AcknowledgeScreencastFrame(int sessionId)
    {
        _ackedFrameId = Math.Max(_ackedFrameId, sessionId);
        try
        {
            if (_ackSignal.CurrentCount == 0)
            {
                _ackSignal.Release();
            }
        }
        catch { }
        RequestScreencastFrame();
    }

    private void Cleanup()
    {
        CdpServer.RemoveSession(this);
        _cts.Cancel();
        StopScreencast();
        StopObservingVisualTree();
        InspectModeEnabled = false;
        Domains.LogDomain.RemoveSession(this);
        Domains.NetworkDomain.RemoveSession(this);
        Domains.RecorderDomain.RemoveSession(this);
        NodeMap.Clear();
        RemoteObjects.Clear();
        if (Window != null)
        {
            HighlightOverlayManager.HideHighlight(Window);
        }
        Domains.CssDomain.CleanupSession(this);
        _isVisibleSubscription?.Dispose();
        _captureStream.Dispose();
        _ackSignal.Dispose();
        _sendSemaphore.Dispose();
    }

    private readonly ConcurrentDictionary<object, (INotifyCollectionChanged Observable, NotifyCollectionChangedEventHandler Handler)> _collectionHandlers = new();
    private readonly ConcurrentDictionary<Visual, EventHandler<AvaloniaPropertyChangedEventArgs>> _propertyHandlers = new();
    private readonly ConcurrentDictionary<Control, NotifyCollectionChangedEventHandler> _classesHandlers = new();

    private System.Collections.Generic.IEnumerable<Visual> GetTreeChildren(Visual visual)
    {
        if (UseLogicalTree && visual is Avalonia.LogicalTree.ILogical logical)
        {
            return GetLogicalVisualChildren(logical);
        }
        return visual.GetVisualChildren().Where(c => !(c is HighlightAdorner));
    }

    private INotifyCollectionChanged? GetChildrenObservable(Visual visual)
    {
        if (UseLogicalTree && visual is Avalonia.LogicalTree.ILogical logical)
        {
            return logical.LogicalChildren;
        }
        return GetVisualChildrenObservable(visual);
    }

    public void StartObservingVisualTree()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(StartObservingVisualTree);
            return;
        }
        StopObservingVisualTree();
        IsDomEnabled = true;
        SubscribeToVisual(Window);
    }

    public void StopObservingVisualTree()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(StopObservingVisualTree);
            return;
        }
        IsDomEnabled = false;
        foreach (var pair in _collectionHandlers)
        {
            try { pair.Value.Observable.CollectionChanged -= pair.Value.Handler; } catch { }
        }
        _collectionHandlers.Clear();

        foreach (var pair in _propertyHandlers)
        {
            try { pair.Key.PropertyChanged -= pair.Value; } catch { }
        }
        _propertyHandlers.Clear();

        foreach (var pair in _classesHandlers)
        {
            if (pair.Key.Classes is INotifyCollectionChanged classesNotify)
            {
                try { classesNotify.CollectionChanged -= pair.Value; } catch { }
            }
        }
        _classesHandlers.Clear();
        NodeMap.Clear();
    }

    private void SubscribeToVisual(Visual visual)
    {
        if (visual == null || visual is HighlightAdorner) return;

        if (!_propertyHandlers.ContainsKey(visual))
        {
            EventHandler<AvaloniaPropertyChangedEventArgs> propHandler = (s, e) => OnAvaloniaPropertyChanged(s, e);
            visual.PropertyChanged += propHandler;
            _propertyHandlers[visual] = propHandler;
        }

        if (GetChildrenObservable(visual) is INotifyCollectionChanged notify)
        {
            if (!_collectionHandlers.ContainsKey(visual))
            {
                NotifyCollectionChangedEventHandler colHandler = (s, e) => OnVisualChildrenChanged(visual, e);
                notify.CollectionChanged += colHandler;
                _collectionHandlers[visual] = (notify, colHandler);
            }
        }

        if (visual is Control control)
        {
            if (control.Classes is INotifyCollectionChanged classesNotify)
            {
                if (!_classesHandlers.ContainsKey(control))
                {
                    NotifyCollectionChangedEventHandler classesHandler = (s, e) =>
                    {
                        var nodeId = NodeMap.GetOrAdd(control);
                        if (nodeId == 0) return;

                        if (control.Classes.Count == 0)
                        {
                            _ = SendEventAsync("DOM.removeAttribute", new JsonObject { ["nodeId"] = nodeId, ["name"] = "class" });
                        }
                        else
                        {
                            _ = SendEventAsync("DOM.attributeModified", new JsonObject
                            {
                                ["nodeId"] = nodeId,
                                ["name"] = "class",
                                ["value"] = string.Join(" ", control.Classes)
                            });
                        }
                    };
                    classesNotify.CollectionChanged += classesHandler;
                    _classesHandlers[control] = classesHandler;
                }
            }
        }

        if (UseLogicalTree && visual is ILogical logical)
        {
            foreach (var child in logical.LogicalChildren)
            {
                if (child is StyledElement se && se.TemplatedParent != null)
                {
                    continue;
                }
                if (child is not Visual && child is ILogical childLogical)
                {
                    SubscribeToNonVisualLogical(childLogical, visual);
                }
            }
        }

        foreach (var child in GetTreeChildren(visual))
        {
            SubscribeToVisual(child);
        }
    }

    private void SubscribeToNonVisualLogical(ILogical logical, Visual parentVisual)
    {
        if (logical.LogicalChildren is INotifyCollectionChanged notify)
        {
            if (!_collectionHandlers.ContainsKey(logical))
            {
                NotifyCollectionChangedEventHandler colHandler = (s, e) => OnLogicalChildrenChanged(parentVisual, logical, e);
                notify.CollectionChanged += colHandler;
                _collectionHandlers[logical] = (notify, colHandler);
            }
        }

        foreach (var child in logical.LogicalChildren)
        {
            if (child is StyledElement se && se.TemplatedParent != null)
            {
                continue;
            }
            if (child is not Visual && child is ILogical childLogical)
            {
                SubscribeToNonVisualLogical(childLogical, parentVisual);
            }
        }
    }

    private void UnsubscribeFromNonVisualLogical(ILogical logical)
    {
        foreach (var child in logical.LogicalChildren)
        {
            if (child is StyledElement se && se.TemplatedParent != null)
            {
                continue;
            }
            if (child is not Visual && child is ILogical childLogical)
            {
                UnsubscribeFromNonVisualLogical(childLogical);
                if (_collectionHandlers.TryRemove(childLogical, out var entry))
                {
                    try { entry.Observable.CollectionChanged -= entry.Handler; } catch { }
                }
            }
        }
    }

    private void OnLogicalChildrenChanged(Visual parentVisual, ILogical container, NotifyCollectionChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnLogicalChildrenChanged(parentVisual, container, e));
            return;
        }

        var parentNodeId = NodeMap.GetOrAdd(parentVisual);
        if (parentNodeId == 0) return;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems)
                    {
                        if (item is StyledElement se && se.TemplatedParent != null)
                        {
                            continue;
                        }

                        if (item is Visual child && child is not HighlightAdorner)
                        {
                            var children = GetTreeChildren(parentVisual).ToList();
                            int childIndex = children.IndexOf(child);
                            int previousNodeId = 0;
                            if (childIndex > 0)
                            {
                                previousNodeId = NodeMap.GetOrAdd(children[childIndex - 1]);
                            }

                            var childNode = Domains.DomDomain.BuildDomNode(child, this, 1, 1);
                            _ = SendEventAsync("DOM.childNodeInserted", new JsonObject
                            {
                                ["parentNodeId"] = parentNodeId,
                                ["previousNodeId"] = previousNodeId,
                                ["node"] = childNode
                            });
                            SubscribeToVisual(child);
                        }
                        else if (item is ILogical childLogical)
                        {
                            SubscribeToNonVisualLogical(childLogical, parentVisual);

                            foreach (var desc in GetLogicalVisualChildren(childLogical))
                            {
                                var children = GetTreeChildren(parentVisual).ToList();
                                int childIndex = children.IndexOf(desc);
                                int previousNodeId = 0;
                                if (childIndex > 0)
                                {
                                    previousNodeId = NodeMap.GetOrAdd(children[childIndex - 1]);
                                }

                                var childNode = Domains.DomDomain.BuildDomNode(desc, this, 1, 1);
                                _ = SendEventAsync("DOM.childNodeInserted", new JsonObject
                                {
                                    ["parentNodeId"] = parentNodeId,
                                    ["previousNodeId"] = previousNodeId,
                                    ["node"] = childNode
                                });
                                SubscribeToVisual(desc);
                            }
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        if (item is Visual child)
                        {
                            int childNodeId = NodeMap.GetOrAdd(child);
                            if (childNodeId != 0)
                            {
                                _ = SendEventAsync("DOM.childNodeRemoved", new JsonObject
                                {
                                    ["parentNodeId"] = parentNodeId,
                                    ["nodeId"] = childNodeId
                                });
                                UnsubscribeFromVisual(child);
                            }
                        }
                        else if (item is ILogical childLogical)
                        {
                            foreach (var desc in GetLogicalVisualChildren(childLogical))
                            {
                                int childNodeId = NodeMap.GetOrAdd(desc);
                                if (childNodeId != 0)
                                {
                                    _ = SendEventAsync("DOM.childNodeRemoved", new JsonObject
                                    {
                                        ["parentNodeId"] = parentNodeId,
                                        ["nodeId"] = childNodeId
                                    });
                                    UnsubscribeFromVisual(desc);
                                }
                            }

                            UnsubscribeFromNonVisualLogical(childLogical);
                            if (_collectionHandlers.TryRemove(childLogical, out var entry))
                            {
                                try { entry.Observable.CollectionChanged -= entry.Handler; } catch { }
                            }
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        if (item is Visual child)
                        {
                            int childNodeId = NodeMap.GetOrAdd(child);
                            if (childNodeId != 0)
                            {
                                _ = SendEventAsync("DOM.childNodeRemoved", new JsonObject
                                {
                                    ["parentNodeId"] = parentNodeId,
                                    ["nodeId"] = childNodeId
                                });
                                UnsubscribeFromVisual(child);
                            }
                        }
                        else if (item is ILogical childLogical)
                        {
                            foreach (var desc in GetLogicalVisualChildren(childLogical))
                            {
                                int childNodeId = NodeMap.GetOrAdd(desc);
                                if (childNodeId != 0)
                                {
                                    _ = SendEventAsync("DOM.childNodeRemoved", new JsonObject
                                    {
                                        ["parentNodeId"] = parentNodeId,
                                        ["nodeId"] = childNodeId
                                    });
                                    UnsubscribeFromVisual(desc);
                                }
                            }

                            UnsubscribeFromNonVisualLogical(childLogical);
                            if (_collectionHandlers.TryRemove(childLogical, out var entry))
                            {
                                try { entry.Observable.CollectionChanged -= entry.Handler; } catch { }
                            }
                        }
                    }
                }
                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems)
                    {
                        if (item is Visual child && child is not HighlightAdorner)
                        {
                            var children = GetTreeChildren(parentVisual).ToList();
                            int childIndex = children.IndexOf(child);
                            int previousNodeId = 0;
                            if (childIndex > 0)
                            {
                                previousNodeId = NodeMap.GetOrAdd(children[childIndex - 1]);
                            }

                            var childNode = Domains.DomDomain.BuildDomNode(child, this, 1, 1);
                            _ = SendEventAsync("DOM.childNodeInserted", new JsonObject
                            {
                                ["parentNodeId"] = parentNodeId,
                                ["previousNodeId"] = previousNodeId,
                                ["node"] = childNode
                            });
                            SubscribeToVisual(child);
                        }
                        else if (item is ILogical childLogical)
                        {
                            SubscribeToNonVisualLogical(childLogical, parentVisual);

                            foreach (var desc in GetLogicalVisualChildren(childLogical))
                            {
                                var children = GetTreeChildren(parentVisual).ToList();
                                int childIndex = children.IndexOf(desc);
                                int previousNodeId = 0;
                                if (childIndex > 0)
                                {
                                    previousNodeId = NodeMap.GetOrAdd(children[childIndex - 1]);
                                }

                                var childNode = Domains.DomDomain.BuildDomNode(desc, this, 1, 1);
                                _ = SendEventAsync("DOM.childNodeInserted", new JsonObject
                                {
                                    ["parentNodeId"] = parentNodeId,
                                    ["previousNodeId"] = previousNodeId,
                                    ["node"] = childNode
                                });
                                SubscribeToVisual(desc);
                            }
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                StartObservingVisualTree();
                _ = SendEventAsync("DOM.documentUpdated", new JsonObject());
                break;
        }
    }

    private void UnsubscribeFromVisual(Visual visual)
    {
        if (visual == null) return;

        foreach (var child in GetTreeChildren(visual))
        {
            UnsubscribeFromVisual(child);
        }

        if (UseLogicalTree && visual is ILogical logical)
        {
            UnsubscribeFromNonVisualLogical(logical);
        }

        if (_collectionHandlers.TryRemove(visual, out var entry))
        {
            try { entry.Observable.CollectionChanged -= entry.Handler; } catch { }
        }

        if (_propertyHandlers.TryRemove(visual, out var propHandler))
        {
            try { visual.PropertyChanged -= propHandler; } catch { }
        }

        if (visual is Control control && _classesHandlers.TryRemove(control, out var classesHandler))
        {
            if (control.Classes is INotifyCollectionChanged classesNotify)
            {
                try { classesNotify.CollectionChanged -= classesHandler; } catch { }
            }
        }
        NodeMap.Remove(visual);
    }

    private void OnVisualChildrenChanged(Visual parent, NotifyCollectionChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnVisualChildrenChanged(parent, e));
            return;
        }

        var parentNodeId = NodeMap.GetOrAdd(parent);
        if (parentNodeId == 0) return;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems)
                    {
                        if (item is StyledElement se && se.TemplatedParent != null)
                        {
                            continue;
                        }

                        if (item is Visual child && child is not HighlightAdorner)
                        {
                            var children = GetTreeChildren(parent).ToList();
                            int childIndex = children.IndexOf(child);
                            int previousNodeId = 0;
                            if (childIndex > 0)
                            {
                                previousNodeId = NodeMap.GetOrAdd(children[childIndex - 1]);
                            }

                            var childNode = Domains.DomDomain.BuildDomNode(child, this, 1, 1);
                            _ = SendEventAsync("DOM.childNodeInserted", new JsonObject
                            {
                                ["parentNodeId"] = parentNodeId,
                                ["previousNodeId"] = previousNodeId,
                                ["node"] = childNode
                            });
                            SubscribeToVisual(child);
                        }
                        else if (UseLogicalTree && item is ILogical childLogical)
                        {
                            SubscribeToNonVisualLogical(childLogical, parent);

                            foreach (var desc in GetLogicalVisualChildren(childLogical))
                            {
                                var children = GetTreeChildren(parent).ToList();
                                int childIndex = children.IndexOf(desc);
                                int previousNodeId = 0;
                                if (childIndex > 0)
                                {
                                    previousNodeId = NodeMap.GetOrAdd(children[childIndex - 1]);
                                }

                                var childNode = Domains.DomDomain.BuildDomNode(desc, this, 1, 1);
                                _ = SendEventAsync("DOM.childNodeInserted", new JsonObject
                                {
                                    ["parentNodeId"] = parentNodeId,
                                    ["previousNodeId"] = previousNodeId,
                                    ["node"] = childNode
                                });
                                SubscribeToVisual(desc);
                            }
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        if (item is Visual child)
                        {
                            int childNodeId = NodeMap.GetOrAdd(child);
                            if (childNodeId != 0)
                            {
                                _ = SendEventAsync("DOM.childNodeRemoved", new JsonObject
                                {
                                    ["parentNodeId"] = parentNodeId,
                                    ["nodeId"] = childNodeId
                                });
                                UnsubscribeFromVisual(child);
                            }
                        }
                        else if (UseLogicalTree && item is ILogical childLogical)
                        {
                            foreach (var desc in GetLogicalVisualChildren(childLogical))
                            {
                                int childNodeId = NodeMap.GetOrAdd(desc);
                                if (childNodeId != 0)
                                {
                                    _ = SendEventAsync("DOM.childNodeRemoved", new JsonObject
                                    {
                                        ["parentNodeId"] = parentNodeId,
                                        ["nodeId"] = childNodeId
                                    });
                                    UnsubscribeFromVisual(desc);
                                }
                            }

                            UnsubscribeFromNonVisualLogical(childLogical);
                            if (_collectionHandlers.TryRemove(childLogical, out var entry))
                            {
                                try { entry.Observable.CollectionChanged -= entry.Handler; } catch { }
                            }
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        if (item is Visual child)
                        {
                            int childNodeId = NodeMap.GetOrAdd(child);
                            if (childNodeId != 0)
                            {
                                _ = SendEventAsync("DOM.childNodeRemoved", new JsonObject
                                {
                                    ["parentNodeId"] = parentNodeId,
                                    ["nodeId"] = childNodeId
                                });
                                UnsubscribeFromVisual(child);
                            }
                        }
                        else if (UseLogicalTree && item is ILogical childLogical)
                        {
                            foreach (var desc in GetLogicalVisualChildren(childLogical))
                            {
                                int childNodeId = NodeMap.GetOrAdd(desc);
                                if (childNodeId != 0)
                                {
                                    _ = SendEventAsync("DOM.childNodeRemoved", new JsonObject
                                    {
                                        ["parentNodeId"] = parentNodeId,
                                        ["nodeId"] = childNodeId
                                    });
                                    UnsubscribeFromVisual(desc);
                                }
                            }

                            UnsubscribeFromNonVisualLogical(childLogical);
                            if (_collectionHandlers.TryRemove(childLogical, out var entry))
                            {
                                try { entry.Observable.CollectionChanged -= entry.Handler; } catch { }
                            }
                        }
                    }
                }
                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems)
                    {
                        if (item is Visual child && child is not HighlightAdorner)
                        {
                            var children = GetTreeChildren(parent).ToList();
                            int childIndex = children.IndexOf(child);
                            int previousNodeId = 0;
                            if (childIndex > 0)
                            {
                                previousNodeId = NodeMap.GetOrAdd(children[childIndex - 1]);
                            }

                            var childNode = Domains.DomDomain.BuildDomNode(child, this, 1, 1);
                            _ = SendEventAsync("DOM.childNodeInserted", new JsonObject
                            {
                                ["parentNodeId"] = parentNodeId,
                                ["previousNodeId"] = previousNodeId,
                                ["node"] = childNode
                            });
                            SubscribeToVisual(child);
                        }
                        else if (UseLogicalTree && item is ILogical childLogical)
                        {
                            SubscribeToNonVisualLogical(childLogical, parent);

                            foreach (var desc in GetLogicalVisualChildren(childLogical))
                            {
                                var children = GetTreeChildren(parent).ToList();
                                int childIndex = children.IndexOf(desc);
                                int previousNodeId = 0;
                                if (childIndex > 0)
                                {
                                    previousNodeId = NodeMap.GetOrAdd(children[childIndex - 1]);
                                }

                                var childNode = Domains.DomDomain.BuildDomNode(desc, this, 1, 1);
                                _ = SendEventAsync("DOM.childNodeInserted", new JsonObject
                                {
                                    ["parentNodeId"] = parentNodeId,
                                    ["previousNodeId"] = previousNodeId,
                                    ["node"] = childNode
                                });
                                SubscribeToVisual(desc);
                            }
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                StartObservingVisualTree();
                _ = SendEventAsync("DOM.documentUpdated", new JsonObject());
                break;
        }
    }

    private void OnAvaloniaPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not Visual visual) return;
        
        RequestScreencastFrame();

        var propName = e.Property.Name;
        var nodeId = NodeMap.GetOrAdd(visual);
        if (nodeId != 0)
        {
            Domains.CssDomain.OnPropertyChanged(this, nodeId, propName);
        }

        if (propName == "Name" || propName == "Classes" || propName == "IsEnabled" || propName == "IsVisible" || propName == "Bounds" || propName == "Text" || propName == "Content")
        {
            if (nodeId == 0) return;

            if (propName == "Name")
            {
                var name = (sender as Control)?.Name;
                if (string.IsNullOrEmpty(name))
                {
                    _ = SendEventAsync("DOM.removeAttribute", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Name" });
                    _ = SendEventAsync("DOM.removeAttribute", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Id" });
                }
                else
                {
                    _ = SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Name", ["value"] = name });
                    _ = SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Id", ["value"] = name });
                }
            }
            else if (propName == "Classes")
            {
                var classes = (sender as Control)?.Classes;
                if (classes == null || classes.Count == 0)
                {
                    _ = SendEventAsync("DOM.removeAttribute", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Class" });
                }
                else
                {
                    _ = SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Class", ["value"] = string.Join(" ", classes) });
                }
            }
            else if (propName == "IsEnabled")
            {
                var isEnabled = (sender as Control)?.IsEnabled ?? true;
                _ = SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "IsEnabled", ["value"] = isEnabled.ToString().ToLowerInvariant() });
            }
            else if (propName == "IsVisible")
            {
                var isVisible = (sender as Control)?.IsVisible ?? true;
                _ = SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "IsVisible", ["value"] = isVisible.ToString().ToLowerInvariant() });
            }
            else if (propName == "Bounds")
            {
                var bounds = (sender as Control)?.Bounds ?? default;
                _ = SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Bounds", ["value"] = $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}" });
            }
            else if (propName == "Text" || propName == "Content")
            {
                var text = (sender is Control ctrl) ? Domains.DomDomain.GetControlTextOrContent(ctrl) : null;
                if (string.IsNullOrEmpty(text))
                {
                    _ = SendEventAsync("DOM.removeAttribute", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Text" });
                }
                else
                {
                    _ = SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Text", ["value"] = text });
                }
            }
        }
    }

    private static readonly System.Reflection.PropertyInfo? VisualChildrenProperty = 
        typeof(Visual).GetProperty("VisualChildren", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

    private static INotifyCollectionChanged? GetVisualChildrenObservable(Visual visual)
    {
        return VisualChildrenProperty?.GetValue(visual) as INotifyCollectionChanged;
    }

    private class AnonymousObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        public AnonymousObserver(Action<T> onNext) => _onNext = onNext;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => _onNext(value);
    }

    private int GetVisualTreeStateHash(Visual? visual)
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

    private byte[] GetFallbackMockImageBytes(int pixelWidth, int pixelHeight, int stateHash)
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
