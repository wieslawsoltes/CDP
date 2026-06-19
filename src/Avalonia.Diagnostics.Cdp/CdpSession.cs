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

    public TopLevel Window { get; }
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

    public CdpSession(WebSocket webSocket, TopLevel window)
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
        HighlightOverlayManager.HideHighlight(Window);
        Domains.CssDomain.CleanupSession(this);
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

            case NotifyCollectionChangedAction.Reset:
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

            case NotifyCollectionChangedAction.Reset:
                _ = SendEventAsync("DOM.documentUpdated", new JsonObject());
                break;
        }
    }

    private void OnAvaloniaPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not Visual visual) return;
        
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
}
