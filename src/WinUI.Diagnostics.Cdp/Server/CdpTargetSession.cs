using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Chrome.DevTools.Protocol;

namespace WinUI.Diagnostics.Cdp;

public class CdpTargetSession : Chrome.DevTools.Protocol.CdpTargetSession
{
    private readonly CdpSession _winUiSession;
    private readonly CancellationTokenSource _cts = new();

    public Window? Window { get; }
    public NodeMap NodeMap { get; } = new();
    public override bool TouchEmulationEnabled { get; set; }

    private bool _inspectModeEnabled;
    public override bool InspectModeEnabled
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

    public CdpTargetSession(CdpSession session, string sessionId, string targetId, Window? window)
        : base(session, sessionId, targetId, window != null ? CdpServer.GetOrCreateTarget(window) : null)
    {
        _winUiSession = session;
        Window = window;

        if (Window != null)
        {
            Window.SizeChanged += OnWindowSizeChanged;
        }
    }

    private void UpdateInspectModeHandlers()
    {
        if (Window?.Content == null) return;
        var content = Window.Content;
        if (_inspectModeEnabled)
        {
            content.PointerMoved += OnInspectPointerMoved;
            content.PointerPressed += OnInspectPointerPressed;
        }
        else
        {
            content.PointerMoved -= OnInspectPointerMoved;
            content.PointerPressed -= OnInspectPointerPressed;
            HighlightOverlayManager.HideHighlight(Window);
        }
    }

    private void OnInspectPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_inspectModeEnabled || Window == null) return;
        var pos = e.GetCurrentPoint(Window.Content).Position;
        var hit = HitTestElement(Window, pos);
        if (hit != null)
        {
            UIElement visual = hit;
            if (_winUiSession.UseLogicalTree)
            {
                visual = _winUiSession.FindLogicalNode(visual);
            }
            HighlightOverlayManager.ShowHighlight(Window, visual);
        }
        else
        {
            HighlightOverlayManager.HideHighlight(Window);
        }
        RequestScreencastFrame();
    }

    private async void OnInspectPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_inspectModeEnabled || Window == null) return;
        e.Handled = true;
        var pos = e.GetCurrentPoint(Window.Content).Position;
        var hit = HitTestElement(Window, pos);
        if (hit != null)
        {
            UIElement visual = hit;
            if (_winUiSession.UseLogicalTree)
            {
                visual = _winUiSession.FindLogicalNode(visual);
            }
            var nodeId = NodeMap.GetOrAdd(visual);
            await _winUiSession.SendEventAsync("Overlay.inspectNodeRequested", new JsonObject
            {
                ["backendNodeId"] = nodeId
            }, this);
        }
    }

    private static UIElement? HitTestElement(Window window, Windows.Foundation.Point point)
    {
        if (window.Content == null) return null;
        var elements = VisualTreeHelper.FindElementsInHostCoordinates(point, window.Content);
        return elements.FirstOrDefault();
    }

    // Screencast fields
    private int _lastScreencastFrameId;
    private int _ackedFrameId;
    private string _screencastFormat = "png";
    private int? _screencastQuality;
    private int? _screencastMaxWidth;
    private int? _screencastMaxHeight;
    private int? _screencastEveryNthFrame;
    private string? _screencastTransferMode;
    private bool _screencastEnabled;
    private bool _screencastDirty;
    private int _screencastFrameCounter;
    private DateTime _lastFrameCaptureTime = DateTime.MinValue;
    private DateTime _lastFrameSentTime = DateTime.UtcNow;
    private byte[]? _lastSentFrameBytes;

    private readonly SemaphoreSlim _screencastSignal = new(0);
    private readonly SemaphoreSlim _ackSignal = new(1);
    private readonly object _dirtyRectsLock = new();
    private Windows.Foundation.Rect? _accumulatedDirtyRect;

    private readonly TiledScreencastProducer _tiledScreencastProducer = new();

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
    {
        lock (_dirtyRectsLock)
        {
            _accumulatedDirtyRect = new Windows.Foundation.Rect(0, 0, e.Size.Width, e.Size.Height);
        }
        RequestScreencastFrame();
    }

    public override void RequestScreencastFrame()
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

    public override void StartScreencast(string format = "png", int? quality = null, int? maxWidth = null, int? maxHeight = null, int? everyNthFrame = null, string? transferMode = null)
    {
        if (Window == null) return;

        _screencastFormat = format;
        _screencastQuality = quality;
        _screencastMaxWidth = maxWidth;
        _screencastMaxHeight = maxHeight;
        _screencastEveryNthFrame = everyNthFrame;
        _screencastTransferMode = transferMode;
        _tiledScreencastProducer.Reset();
        _screencastDirty = true;
        _lastSentFrameBytes = null;

        if (_screencastEnabled)
        {
            try
            {
                if (_ackSignal.CurrentCount == 0)
                {
                    _ackSignal.Release();
                }
            }
            catch { }
            RequestScreencastFrame();
            return;
        }

        _screencastEnabled = true;
        _screencastFrameCounter = 0;
        _lastScreencastFrameId = 0;
        _ackedFrameId = 0;
        _lastFrameCaptureTime = DateTime.MinValue;
        _lastFrameSentTime = DateTime.UtcNow;

        try
        {
            if (_ackSignal.CurrentCount == 0)
            {
                _ackSignal.Release();
            }
        }
        catch { }

        Task.Run(async () =>
        {
            while (_screencastEnabled && !_cts.IsCancellationRequested)
            {
                try
                {
                    if (!_screencastDirty)
                    {
                        await _screencastSignal.WaitAsync(_cts.Token);
                    }

                    _screencastDirty = false;

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
                            try { if (_ackSignal.CurrentCount == 0) _ackSignal.Release(); } catch { }
                            continue;
                        }
                    }

                    double width = 0;
                    double height = 0;
                    int pixelWidth = 1;
                    int pixelHeight = 1;
                    int visualStateHash = 0;
                    
                    double scale = 1.0; // WinUI handles high-DPI internally
                    double windowWidth = 0;
                    double windowHeight = 0;
                    SkiaSharp.SKRect? currentDirtyRect = null;
                    SkiaSharp.SKBitmap? bitmapToProcess = null;

                    var dispatcher = Window.DispatcherQueue;
                    await dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            if (Window.Content is FrameworkElement fe)
                            {
                                windowWidth = fe.ActualWidth;
                                windowHeight = fe.ActualHeight;
                            }
                            else
                            {
                                windowWidth = Window.Bounds.Width;
                                windowHeight = Window.Bounds.Height;
                            }

                            pixelWidth = Math.Max(1, (int)(windowWidth * scale));
                            pixelHeight = Math.Max(1, (int)(windowHeight * scale));
                            visualStateHash = CdpSession.GetVisualTreeStateHash(Window.Content as UIElement);

                            if (Window.Content != null)
                            {
                                var rtb = new RenderTargetBitmap();
                                await rtb.RenderAsync(Window.Content);
                                
                                var pixelBuffer = await rtb.GetPixelsAsync();
                                byte[] bgraPixels = new byte[pixelBuffer.Length];
                                using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(pixelBuffer))
                                {
                                    reader.ReadBytes(bgraPixels);
                                }

                                var info = new SkiaSharp.SKImageInfo(rtb.PixelWidth, rtb.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                                var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(bgraPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                                try
                                {
                                    bitmapToProcess = new SkiaSharp.SKBitmap();
                                    bitmapToProcess.InstallPixels(info, gcHandle.AddrOfPinnedObject(), info.RowBytes, delegate { gcHandle.Free(); });
                                }
                                catch
                                {
                                    gcHandle.Free();
                                    bitmapToProcess?.Dispose();
                                    bitmapToProcess = null;
                                }
                            }

                            Windows.Foundation.Rect? localDirty = null;
                            lock (_dirtyRectsLock)
                            {
                                localDirty = _accumulatedDirtyRect;
                                _accumulatedDirtyRect = null;
                            }

                            if (localDirty.HasValue)
                            {
                                float left = (float)(localDirty.Value.X * scale);
                                float top = (float)(localDirty.Value.Y * scale);
                                float right = (float)((localDirty.Value.X + localDirty.Value.Width) * scale);
                                float bottom = (float)((localDirty.Value.Y + localDirty.Value.Height) * scale);
                                currentDirtyRect = new SkiaSharp.SKRect(left, top, right, bottom);
                            }
                        }
                        catch (Exception)
                        {
                            bitmapToProcess?.Dispose();
                            bitmapToProcess = null;
                        }
                    });

                    width = windowWidth;
                    height = windowHeight;

                    if (bitmapToProcess == null)
                    {
                        // Fallback mock bitmap
                        byte[] fallbackBytes = CdpSession.GetFallbackMockImageBytes(pixelWidth, pixelHeight, visualStateHash);
                        if (fallbackBytes != null && fallbackBytes.Length > 0)
                        {
                            bitmapToProcess = SkiaSharp.SKBitmap.Decode(fallbackBytes);
                        }
                    }

                    if (bitmapToProcess != null)
                    {
                        if (string.Equals(_screencastTransferMode, "tiled", StringComparison.OrdinalIgnoreCase))
                        {
                            var changedTiles = _tiledScreencastProducer.ProcessFrame(
                                bitmapToProcess,
                                _screencastFormat ?? "png",
                                _screencastQuality,
                                currentDirtyRect,
                                out int cols,
                                out int rows);

                            bitmapToProcess.Dispose();

                            if (changedTiles == null || changedTiles.Count == 0)
                            {
                                try { if (_ackSignal.CurrentCount == 0) _ackSignal.Release(); } catch { }
                                continue;
                            }

                            int tileWidth = 64; // TileSize
                            int tileHeight = 64;

                            var currentFrameId = ++_lastScreencastFrameId;
                            var metadata = new JsonObject
                            {
                                ["deviceWidth"] = width,
                                ["deviceHeight"] = height,
                                ["offsetTop"] = 0,
                                ["pageScaleFactor"] = 1,
                                ["scrollX"] = 0,
                                ["scrollY"] = 0,
                                ["timestamp"] = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0
                            };

                            _lastFrameSentTime = DateTime.UtcNow;

                            _ = _winUiSession.SendEventAsync("Page.screencastFrame", new JsonObject
                            {
                                ["transferMode"] = "tiled",
                                ["pixelWidth"] = pixelWidth,
                                ["pixelHeight"] = pixelHeight,
                                ["tileWidth"] = tileWidth,
                                ["tileHeight"] = tileHeight,
                                ["cols"] = cols,
                                ["rows"] = rows,
                                ["tiles"] = changedTiles,
                                ["metadata"] = metadata,
                                ["sessionId"] = currentFrameId
                            }, this);
                        }
                        else
                        {
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

                            byte[]? frameBytes = null;
                            using (var image = SkiaSharp.SKImage.FromBitmap(bitmapToProcess))
                            using (var encodedData = image.Encode(encodedFormat, q))
                            {
                                if (encodedData != null)
                                {
                                    frameBytes = encodedData.ToArray();
                                }
                            }

                            bitmapToProcess.Dispose();

                            if (frameBytes == null || frameBytes.Length == 0)
                            {
                                try { if (_ackSignal.CurrentCount == 0) _ackSignal.Release(); } catch { }
                                continue;
                            }

                            if (_lastSentFrameBytes != null && frameBytes.SequenceEqual(_lastSentFrameBytes))
                            {
                                try { if (_ackSignal.CurrentCount == 0) _ackSignal.Release(); } catch { }
                                continue;
                            }

                            var base64Data = Convert.ToBase64String(frameBytes);
                            _lastSentFrameBytes = frameBytes;

                            var currentFrameId = ++_lastScreencastFrameId;
                            _lastFrameSentTime = DateTime.UtcNow;

                            var screencastParams = new JsonObject
                            {
                                ["data"] = base64Data,
                                ["metadata"] = new JsonObject
                                {
                                    ["deviceWidth"] = width,
                                    ["deviceHeight"] = height,
                                    ["pageScaleFactor"] = 1,
                                    ["offsetTop"] = 0,
                                    ["scrollX"] = 0,
                                    ["scrollY"] = 0,
                                    ["timestamp"] = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0
                                },
                                ["sessionId"] = currentFrameId
                            };

                            _ = _winUiSession.SendEventAsync("Page.screencastFrame", screencastParams, this);
                        }
                    }
                    else
                    {
                        try { if (_ackSignal.CurrentCount == 0) _ackSignal.Release(); } catch { }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    try { if (_ackSignal.CurrentCount == 0) _ackSignal.Release(); } catch { }
                }
            }
        });
    }

    public override void StopScreencast()
    {
        _screencastEnabled = false;
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

    public override void AcknowledgeScreencastFrame(int sessionId)
    {
        _ackedFrameId = sessionId;
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

    public override void Dispose()
    {
        base.Dispose();
        _cts.Cancel();
        StopScreencast();
        _tiledScreencastProducer.Dispose();

        if (Window != null)
        {
            Window.SizeChanged -= OnWindowSizeChanged;
        }

        UpdateInspectModeHandlers();
    }
}
