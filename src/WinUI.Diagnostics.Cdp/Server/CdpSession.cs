using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Chrome.DevTools.Protocol;
using Xaml.Compiler.Mutation;
using WinUI.Diagnostics.Cdp.Adapters;

namespace WinUI.Diagnostics.Cdp;

public class CdpSession : Chrome.DevTools.Protocol.CdpSession
{
    private readonly Window? _window;
    private readonly NodeMap _nodeMap = new();

    private WeakReference<Window>? _activeWindowOverride;

    public CdpSession(WebSocket webSocket, Window? window) 
        : base(webSocket, window != null ? CdpServer.GetOrCreateTarget(window) : null)
    {
        _window = window;
        CdpServer.EnsureInitialized();
        if (window != null)
        {
            MutationEngine = new LosslessXamlMutationEngine(new WinUiFrameworkAdapter(window.DispatcherQueue, NodeMap), NodeMap, (file, diags) => SendDiagnostics(file, diags));
        }
    }

    private void SendDiagnostics(string file, System.Collections.Generic.List<Xaml.Compiler.Ast.Diagnostic> diags)
    {
        var diagArray = new JsonArray();
        foreach (var diag in diags)
        {
            diagArray.Add(new JsonObject
            {
                ["code"] = diag.Code,
                ["message"] = diag.Message,
                ["severity"] = diag.Severity.ToString(),
                ["range"] = new JsonObject
                {
                    ["start"] = new JsonObject
                    {
                        ["offset"] = diag.Span.Start.Offset,
                        ["line"] = diag.Span.Start.Line,
                        ["column"] = diag.Span.Start.Column
                    },
                    ["end"] = new JsonObject
                    {
                        ["offset"] = diag.Span.End.Offset,
                        ["line"] = diag.Span.End.Line,
                        ["column"] = diag.Span.End.Column
                    }
                }
            });
        }

        _ = SendEventAsync("XamlLsp.diagnosticsUpdated", new JsonObject
        {
            ["file"] = file,
            ["diagnostics"] = diagArray
        });
    }

    public new CdpTargetSession? CurrentTargetSession => base.CurrentTargetSession as CdpTargetSession;

    public Window? Window
    {
        get
        {
            if (_activeWindowOverride != null && _activeWindowOverride.TryGetTarget(out var win) && win.Content != null)
            {
                return win;
            }
            return CurrentTargetSession?.Window ?? _window;
        }
        set
        {
            if (value != null)
            {
                _activeWindowOverride = new WeakReference<Window>(value);
            }
            else
            {
                _activeWindowOverride = null;
            }
        }
    }
    
    public NodeMap NodeMap => CurrentTargetSession?.NodeMap ?? _nodeMap;
    public bool UseSlimTree { get; set; }

    public static UIElement? GetVisualFromObject(object? obj)
    {
        if (obj is Jint.Native.JsValue jsVal)
        {
            try
            {
                obj = jsVal.ToObject();
            }
            catch
            {
                // Ignore
            }
        }
        if (obj is UIElement v) return v;
        if (obj is Domains.CdpRuntimeElement rt) return rt.visual as UIElement;
        return null;
    }

    public static System.Collections.Generic.IEnumerable<UIElement> GetLogicalVisualChildren(DependencyObject logical)
    {
        var children = GetLogicalChildren(logical);
        foreach (var child in children)
        {
            if (child is UIElement visualChild)
            {
                yield return visualChild;
            }
            else if (child is DependencyObject childLogical)
            {
                foreach (var desc in GetLogicalVisualChildren(childLogical))
                {
                    yield return desc;
                }
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> GetLogicalChildren(DependencyObject parent)
    {
        if (parent is Microsoft.UI.Xaml.Controls.Panel panel)
        {
            foreach (var child in panel.Children) yield return child;
        }
        else if (parent is Microsoft.UI.Xaml.Controls.ContentControl cc)
        {
            if (cc.Content is DependencyObject doChild) yield return doChild;
        }
        else if (parent is Microsoft.UI.Xaml.Controls.Border border)
        {
            if (border.Child != null) yield return border.Child;
        }
        else if (parent is Microsoft.UI.Xaml.Controls.ItemsControl ic)
        {
            foreach (var item in ic.Items)
            {
                if (item is DependencyObject doItem) yield return doItem;
            }
        }
    }

    internal bool IsLogicalNode(DependencyObject? node)
    {
        if (node == null) return false;
        if (node is Window || node == Window?.Content) return true;

        var current = node;
        while (current != null)
        {
            var parent = current is FrameworkElement fe ? fe.Parent : null;
            if (parent == null)
            {
                return current == Window?.Content;
            }
            current = parent;
        }
        return false;
    }

    public UIElement FindLogicalNode(UIElement visual)
    {
        DependencyObject? current = visual;
        while (current != null)
        {
            if (IsLogicalNode(current) && current is UIElement v)
            {
                return v;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return visual;
    }

    internal static INotifyCollectionChanged? GetVisualChildrenObservable(UIElement visual)
    {
        return null;
    }

    internal static int GetVisualTreeStateHash(UIElement? visual)
    {
        if (visual == null) return 0;
        int hash = visual.GetType().GetHashCode();
        
        hash = HashCode.Combine(hash, visual.ActualSize.X, visual.ActualSize.Y, visual.Visibility == Visibility.Visible);

        int count = VisualTreeHelper.GetChildrenCount(visual);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(visual, i) is UIElement child)
            {
                hash = HashCode.Combine(hash, GetVisualTreeStateHash(child));
            }
        }
        
        return hash;
    }

    internal static byte[] GetFallbackMockImageBytes(int pixelWidth, int pixelHeight, int stateHash)
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

    protected override void OnCleanup()
    {
        base.OnCleanup();
        Domains.CssDomain.CleanupSession(this);
        Domains.PerformanceDomain.CleanupSession(this);
        Domains.RecorderDomain.RemoveSession(this);
        Domains.WebMcpDomain.CleanupSession(this);
        Domains.MvvmDomain.CleanupSession(this);
    }
}
