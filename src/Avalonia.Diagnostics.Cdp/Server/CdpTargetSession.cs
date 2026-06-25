using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;

namespace Avalonia.Diagnostics.Cdp;

public class CdpTargetSession : Chrome.DevTools.Protocol.CdpTargetSession
{
    private readonly CdpSession _session;
    private readonly CancellationTokenSource _cts = new();

    public TopLevel? Window { get; }
    public NodeMap NodeMap { get; } = new();
    public override bool TouchEmulationEnabled { get; set; }
    public IInputDevice TouchDevice { get; } =
        (IInputDevice)Activator.CreateInstance(typeof(TouchDevice), nonPublic: true)!;

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

    private void UpdateInspectModeHandlers()
    {
        if (Window == null) return;
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
        if (!_inspectModeEnabled || Window == null) return;
        var pos = e.GetPosition(Window);
        var hit = Window.InputHitTest(pos);
        if (hit is Visual visual)
        {
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

    private async void OnInspectPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_inspectModeEnabled || Window == null) return;
        e.Handled = true;
        var pos = e.GetPosition(Window);
        var hit = Window.InputHitTest(pos);
        if (hit is Visual visual)
        {
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

    // Screencast fields
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

    public override void StartScreencast(string format = "png", int? quality = null, int? maxWidth = null, int? maxHeight = null, int? everyNthFrame = null)
    {
        if (Window == null) return;

        _screencastFormat = format;
        _screencastQuality = quality;
        _screencastMaxWidth = maxWidth;
        _screencastMaxHeight = maxHeight;
        _screencastEveryNthFrame = everyNthFrame;
        _screencastDirty = true;
        _lastSentFrameBytes = null; // force fresh capture & transmission

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

        // Reset the ACK signal count to 1
        try
        {
            if (_ackSignal.CurrentCount == 0)
            {
                _ackSignal.Release();
            }
        }
        catch { }

        Window.LayoutUpdated += OnWindowLayoutUpdated;

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
                            var scale = Window.RenderScaling;
                            width = Window.Bounds.Width;
                            height = Window.Bounds.Height;
                            pixelWidth = Math.Max(1, (int)(width * scale));
                            pixelHeight = Math.Max(1, (int)(height * scale));
                            visualStateHash = CdpSession.GetVisualTreeStateHash(Window);

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
                        rawPngBytes = CdpSession.GetFallbackMockImageBytes(pixelWidth, pixelHeight, visualStateHash);
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

                    await _session.SendEventAsync("Page.screencastFrame", new JsonObject
                    {
                        ["data"] = base64Data,
                        ["metadata"] = metadata,
                        ["sessionId"] = currentFrameId
                    }, this);
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

    public override void StopScreencast()
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

    public override void AcknowledgeScreencastFrame(int sessionId)
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

    // DOM Observation Fields
    public override bool IsDomEnabled { get; set; }
    private readonly ConcurrentDictionary<object, (INotifyCollectionChanged Observable, NotifyCollectionChangedEventHandler Handler)> _collectionHandlers = new();
    private readonly ConcurrentDictionary<Visual, EventHandler<AvaloniaPropertyChangedEventArgs>> _propertyHandlers = new();
    private readonly ConcurrentDictionary<Control, NotifyCollectionChangedEventHandler> _classesHandlers = new();

    public override void StartObservingVisualTree()
    {
        if (Window == null) return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(StartObservingVisualTree);
            return;
        }
        StopObservingVisualTree();
        IsDomEnabled = true;
        SubscribeToVisual(Window);
    }

    public override void StopObservingVisualTree()
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
    }

    private System.Collections.Generic.IEnumerable<Visual> GetTreeChildren(Visual visual)
    {
        if (_session.UseLogicalTree && visual is ILogical logical)
        {
            return CdpSession.GetLogicalVisualChildren(logical);
        }
        return visual.GetVisualChildren().Where(c => !(c is HighlightAdorner));
    }

    private INotifyCollectionChanged? GetChildrenObservable(Visual visual)
    {
        if (_session.UseLogicalTree && visual is ILogical logical)
        {
            return logical.LogicalChildren;
        }
        return CdpSession.GetVisualChildrenObservable(visual);
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
                            _ = _session.SendEventAsync("DOM.removeAttribute", new JsonObject { ["nodeId"] = nodeId, ["name"] = "class" }, this);
                        }
                        else
                        {
                            _ = _session.SendEventAsync("DOM.attributeModified", new JsonObject
                            {
                                ["nodeId"] = nodeId,
                                ["name"] = "class",
                                ["value"] = string.Join(" ", control.Classes)
                            }, this);
                        }
                    };
                    classesNotify.CollectionChanged += classesHandler;
                    _classesHandlers[control] = classesHandler;
                }
            }
        }

        if (_session.UseLogicalTree && visual is ILogical logical)
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

                            var childNode = Domains.DomDomain.BuildDomNode(child, _session, 1, 1);
                            _ = _session.SendEventAsync("DOM.childNodeInserted", new JsonObject
                            {
                                ["parentNodeId"] = parentNodeId,
                                ["previousNodeId"] = previousNodeId,
                                ["node"] = childNode
                            }, this);
                            SubscribeToVisual(child);
                        }
                        else if (item is ILogical childLogical)
                        {
                            SubscribeToNonVisualLogical(childLogical, parentVisual);

                            foreach (var desc in CdpSession.GetLogicalVisualChildren(childLogical))
                            {
                                var children = GetTreeChildren(parentVisual).ToList();
                                int childIndex = children.IndexOf(desc);
                                int previousNodeId = 0;
                                if (childIndex > 0)
                                {
                                    previousNodeId = NodeMap.GetOrAdd(children[childIndex - 1]);
                                }

                                var childNode = Domains.DomDomain.BuildDomNode(desc, _session, 1, 1);
                                _ = _session.SendEventAsync("DOM.childNodeInserted", new JsonObject
                                {
                                    ["parentNodeId"] = parentNodeId,
                                    ["previousNodeId"] = previousNodeId,
                                    ["node"] = childNode
                                }, this);
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
                        if (item is StyledElement se && se.TemplatedParent != null)
                        {
                            continue;
                        }

                        if (item is Visual child)
                        {
                            if (NodeMap.TryGetId(child, out int nodeId))
                            {
                                _ = _session.SendEventAsync("DOM.childNodeRemoved", new JsonObject
                                {
                                    ["parentNodeId"] = parentNodeId,
                                    ["nodeId"] = nodeId
                                }, this);
                                UnsubscribeFromVisual(child);
                            }
                        }
                        else if (item is ILogical childLogical)
                        {
                            UnsubscribeFromNonVisualLogical(childLogical);
                            foreach (var desc in CdpSession.GetLogicalVisualChildren(childLogical))
                            {
                                if (NodeMap.TryGetId(desc, out int nodeId))
                                {
                                    _ = _session.SendEventAsync("DOM.childNodeRemoved", new JsonObject
                                    {
                                        ["parentNodeId"] = parentNodeId,
                                        ["nodeId"] = nodeId
                                    }, this);
                                    UnsubscribeFromVisual(desc);
                                }
                            }
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // For simplicity, notify document updated to trigger rebuild
                _ = _session.SendEventAsync("DOM.documentUpdated", new JsonObject(), this);
                break;
        }
    }

    private void UnsubscribeFromVisual(Visual visual)
    {
        if (_propertyHandlers.TryRemove(visual, out var propHandler))
        {
            try { visual.PropertyChanged -= propHandler; } catch { }
        }

        if (_collectionHandlers.TryRemove(visual, out var colEntry))
        {
            try { colEntry.Observable.CollectionChanged -= colEntry.Handler; } catch { }
        }

        if (visual is Control control)
        {
            if (_classesHandlers.TryRemove(control, out var classesHandler))
            {
                if (control.Classes is INotifyCollectionChanged classesNotify)
                {
                    try { classesNotify.CollectionChanged -= classesHandler; } catch { }
                }
            }
        }

        if (_session.UseLogicalTree && visual is ILogical logical)
        {
            UnsubscribeFromNonVisualLogical(logical);
        }

        foreach (var child in GetTreeChildren(visual))
        {
            UnsubscribeFromVisual(child);
        }

        NodeMap.Remove(visual);
    }

    private void OnVisualChildrenChanged(Visual visual, NotifyCollectionChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnVisualChildrenChanged(visual, e));
            return;
        }

        var parentNodeId = NodeMap.GetOrAdd(visual);
        if (parentNodeId == 0) return;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems)
                    {
                        if (item is Visual child && child is not HighlightAdorner)
                        {
                            var children = GetTreeChildren(visual).ToList();
                            int childIndex = children.IndexOf(child);
                            int previousNodeId = 0;
                            if (childIndex > 0)
                            {
                                previousNodeId = NodeMap.GetOrAdd(children[childIndex - 1]);
                            }

                            var childNode = Domains.DomDomain.BuildDomNode(child, _session, 1, 1);
                            _ = _session.SendEventAsync("DOM.childNodeInserted", new JsonObject
                            {
                                ["parentNodeId"] = parentNodeId,
                                ["previousNodeId"] = previousNodeId,
                                ["node"] = childNode
                            }, this);
                            SubscribeToVisual(child);
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
                            if (NodeMap.TryGetId(child, out int nodeId))
                            {
                                _ = _session.SendEventAsync("DOM.childNodeRemoved", new JsonObject
                                {
                                    ["parentNodeId"] = parentNodeId,
                                    ["nodeId"] = nodeId
                                }, this);
                                UnsubscribeFromVisual(child);
                            }
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                _ = _session.SendEventAsync("DOM.documentUpdated", new JsonObject(), this);
                break;
        }
    }

    private void OnAvaloniaPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not Visual visual) return;
        var nodeId = NodeMap.GetOrAdd(visual);
        if (nodeId == 0) return;

        if (e.Property.Name == "Name")
        {
            var name = e.NewValue as string;
            if (string.IsNullOrEmpty(name))
            {
                _ = _session.SendEventAsync("DOM.removeAttribute", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Name" }, this);
                _ = _session.SendEventAsync("DOM.removeAttribute", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Id" }, this);
            }
            else
            {
                _ = _session.SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Name", ["value"] = name }, this);
                _ = _session.SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Id", ["value"] = name }, this);
            }
        }
        else if (e.Property.Name == "Classes")
        {
            if (visual is Control control)
            {
                var classes = control.Classes;
                if (classes.Count == 0)
                {
                    _ = _session.SendEventAsync("DOM.removeAttribute", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Class" }, this);
                }
                else
                {
                    _ = _session.SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Class", ["value"] = string.Join(" ", classes) }, this);
                }
            }
        }
        else if (e.Property.Name == "IsEnabled")
        {
            var isEnabled = e.NewValue as bool? ?? true;
            _ = _session.SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "IsEnabled", ["value"] = isEnabled.ToString().ToLowerInvariant() }, this);
        }
        else if (e.Property.Name == "IsVisible")
        {
            var isVisible = e.NewValue as bool? ?? true;
            _ = _session.SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "IsVisible", ["value"] = isVisible.ToString().ToLowerInvariant() }, this);
        }
        else if (e.Property.Name == "Bounds")
        {
            var bounds = (sender as Control)?.Bounds ?? default;
            _ = _session.SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Bounds", ["value"] = $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}" }, this);
        }
        else if (e.Property.Name == "Text" || e.Property.Name == "Content")
        {
            var text = e.NewValue?.ToString();
            if (string.IsNullOrEmpty(text))
            {
                _ = _session.SendEventAsync("DOM.removeAttribute", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Text" }, this);
            }
            else
            {
                _ = _session.SendEventAsync("DOM.attributeModified", new JsonObject { ["nodeId"] = nodeId, ["name"] = "Text", ["value"] = text }, this);
            }
        }
    }

    public CdpTargetSession(CdpSession session, string sessionId, string targetId, TopLevel? window)
        : base(session, sessionId, targetId, window != null ? CdpServer.GetOrCreateTarget(window, targetId) : null!)
    {
        _session = session;
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
                SubscribeToVisibility();
            }
            else
            {
                Dispatcher.UIThread.Post(SubscribeToVisibility);
            }
        }
    }

    private void SubscribeToVisibility()
    {
        if (Window == null) return;
        _isVisibleSubscription = Window.GetObservable(Visual.IsVisibleProperty).Subscribe(new AnonymousObserver<bool>(visible =>
        {
            if (_screencastEnabled)
            {
                _ = _session.SendEventAsync("Page.screencastVisibilityChanged", new JsonObject
                {
                    ["visible"] = visible
                }, this);
            }
        }));
    }

    public override void Dispose()
    {
        base.Dispose();
        _cts.Cancel();
        StopScreencast();
        StopObservingVisualTree();
        InspectModeEnabled = false;
        _isVisibleSubscription?.Dispose();
        _captureStream.Dispose();
        _ackSignal.Dispose();
        NodeMap.Clear();
        if (Window != null)
        {
            HighlightOverlayManager.HideHighlight(Window);
        }
    }
}
