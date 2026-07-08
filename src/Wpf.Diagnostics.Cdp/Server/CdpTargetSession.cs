using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Chrome.DevTools.Protocol;

namespace Wpf.Diagnostics.Cdp;

public class CdpTargetSession : Chrome.DevTools.Protocol.CdpTargetSession
{
    private readonly CdpSession _session;
    private readonly CancellationTokenSource _cts = new();

    public Window? Window { get; }
    public NodeMap NodeMap { get; } = new();
    public override bool TouchEmulationEnabled { get; set; }

    // Inspect Mode
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
        : base(session, sessionId, targetId, window != null ? CdpServer.GetOrCreateTarget(window, targetId) : null)
    {
        _session = session;
        Window = window;

        if (Window != null)
        {
            Window.LayoutUpdated += OnWindowLayoutUpdated;
        }
    }

    private void UpdateInspectModeHandlers()
    {
        if (Window == null) return;
        if (_inspectModeEnabled)
        {
            Window.PreviewMouseMove += OnInspectMouseMove;
            Window.PreviewMouseDown += OnInspectMouseDown;
        }
        else
        {
            Window.PreviewMouseMove -= OnInspectMouseMove;
            Window.PreviewMouseDown -= OnInspectMouseDown;
            HighlightOverlayManager.HideHighlight(Window);
        }
    }

    private void OnInspectMouseMove(object sender, MouseEventArgs e)
    {
        if (!_inspectModeEnabled || Window == null) return;
        var pos = e.GetPosition(Window);
        var hit = HitTestElement(Window, pos);
        if (hit != null)
        {
            Visual visual = hit;
            if (_session.UseLogicalTree)
            {
                visual = _session.FindLogicalNode(visual);
            }
            HighlightOverlayManager.ShowHighlight(Window, visual);
        }
        else
        {
            HighlightOverlayManager.HideHighlight(Window);
        }
        RequestScreencastFrame();
    }

    private async void OnInspectMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_inspectModeEnabled || Window == null) return;
        e.Handled = true;
        var pos = e.GetPosition(Window);
        var hit = HitTestElement(Window, pos);
        if (hit != null)
        {
            Visual visual = hit;
            if (_session.UseLogicalTree)
            {
                visual = _session.FindLogicalNode(visual);
            }
            var nodeId = NodeMap.GetOrAdd(visual);
            await _session.SendEventAsync("Overlay.inspectNodeRequested", new JsonObject
            {
                ["backendNodeId"] = nodeId
            }, this);
        }
    }

    private static Visual? HitTestElement(Window window, Point point)
    {
        Visual? hit = null;
        VisualTreeHelper.HitTest(window, null, new HitTestResultCallback(result =>
        {
            hit = result.VisualHit as Visual;
            return HitTestResultBehavior.Stop;
        }), new PointHitTestParameters(point));
        return hit;
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
    private Rect? _accumulatedDirtyRect;

    private readonly TiledScreencastProducer _tiledScreencastProducer = new();

    private void OnWindowLayoutUpdated(object? sender, EventArgs e)
    {
        lock (_dirtyRectsLock)
        {
            if (Window != null)
            {
                _accumulatedDirtyRect = new Rect(0, 0, Window.ActualWidth, Window.ActualHeight);
            }
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

                    string base64Data = "";
                    double width = 0;
                    double height = 0;
                    int pixelWidth = 1;
                    int pixelHeight = 1;
                    byte[]? rawPngBytes = null;
                    int visualStateHash = 0;
                    
                    double scale = 1.0;
                    double windowWidth = 0;
                    double windowHeight = 0;
                    SkiaSharp.SKRect? currentDirtyRect = null;

                    var dispatcher = Window.Dispatcher;
                    await dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            var source = PresentationSource.FromVisual(Window);
                            if (source?.CompositionTarget != null)
                            {
                                scale = source.CompositionTarget.TransformToDevice.M11;
                            }

                            windowWidth = Window.ActualWidth;
                            windowHeight = Window.ActualHeight;
                            pixelWidth = Math.Max(1, (int)(windowWidth * scale));
                            pixelHeight = Math.Max(1, (int)(windowHeight * scale));
                            visualStateHash = CdpSession.GetVisualTreeStateHash(Window);

                            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * scale, 96 * scale, PixelFormats.Pbgra30);
                            rtb.Render(Window);

                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(rtb));
                            using var ms = new MemoryStream();
                            encoder.Save(ms);
                            rawPngBytes = ms.ToArray();

                            Rect? localDirty = null;
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
                        }
                    }, DispatcherPriority.Background);

                    width = windowWidth;
                    height = windowHeight;

                    if (rawPngBytes == null || rawPngBytes.Length == 0)
                    {
                        rawPngBytes = CdpSession.GetFallbackMockImageBytes(pixelWidth, pixelHeight, visualStateHash);
                    }

                    if (_screencastTransferMode == "tiled" && rawPngBytes != null && rawPngBytes.Length > 0)
                    {
                        using var skBitmap = SkiaSharp.SKBitmap.Decode(rawPngBytes);
                        var changedTiles = _tiledScreencastProducer.ProcessFrame(skBitmap, currentDirtyRect, out var frameBytes);

                        if (changedTiles.Count == 0 && _lastSentFrameBytes != null)
                        {
                            try { if (_ackSignal.CurrentCount == 0) _ackSignal.Release(); } catch { }
                            continue;
                        }

                        base64Data = Convert.ToBase64String(frameBytes);
                        _lastSentFrameBytes = frameBytes;

                        var currentFrameId = ++_lastScreencastFrameId;
                        _lastFrameSentTime = DateTime.UtcNow;

                        var tilesJson = new JsonArray();
                        foreach (var tile in changedTiles)
                        {
                            tilesJson.Add(new JsonObject
                            {
                                ["x"] = tile.X,
                                ["y"] = tile.Y,
                                ["width"] = tile.Width,
                                ["height"] = tile.Height,
                                ["data"] = Convert.ToBase64String(tile.PngData)
                            });
                        }

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
                                ["timestamp"] = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                                ["transferMode"] = "tiled",
                                ["tiles"] = tilesJson
                            },
                            ["sessionId"] = currentFrameId
                        };

                        _ = _session.SendEventAsync("Page.screencastFrame", screencastParams, this);
                    }
                    else if (rawPngBytes != null && rawPngBytes.Length > 0)
                    {
                        if (_lastSentFrameBytes != null && rawPngBytes.SequenceEqual(_lastSentFrameBytes))
                        {
                            try { if (_ackSignal.CurrentCount == 0) _ackSignal.Release(); } catch { }
                            continue;
                        }

                        base64Data = Convert.ToBase64String(rawPngBytes);
                        _lastSentFrameBytes = rawPngBytes;

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

                        _ = _session.SendEventAsync("Page.screencastFrame", screencastParams, this);
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
            Window.LayoutUpdated -= OnWindowLayoutUpdated;
        }

        UpdateInspectModeHandlers();
    }
}
