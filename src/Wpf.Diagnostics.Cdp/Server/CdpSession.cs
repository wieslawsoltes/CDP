using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Chrome.DevTools.Protocol;
using Xaml.Compiler.Mutation;
using Wpf.Diagnostics.Cdp.Adapters;

namespace Wpf.Diagnostics.Cdp;

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
        MutationEngine = new LosslessXamlMutationEngine(new WpfUiFrameworkAdapter(NodeMap), NodeMap, (file, diags) => SendDiagnostics(file, diags));
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
            if (_activeWindowOverride != null && _activeWindowOverride.TryGetTarget(out var win) && win.IsVisible)
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
    public bool UseLogicalTree { get; set; }

    public static Visual? GetVisualFromObject(object? obj)
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
        if (obj is Visual v) return v;
        if (obj is Domains.CdpRuntimeElement rt) return rt.visual as Visual;
        return null;
    }

    public static System.Collections.Generic.IEnumerable<Visual> GetLogicalVisualChildren(DependencyObject logical)
    {
        var children = LogicalTreeHelper.GetChildren(logical);
        if (children == null) yield break;

        foreach (var child in children)
        {
            if (child is Visual visualChild)
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

    internal bool IsLogicalNode(DependencyObject? node)
    {
        if (node == null) return false;
        if (node is Window) return true;

        var current = node;
        while (current != null)
        {
            var parent = LogicalTreeHelper.GetParent(current);
            if (parent == null)
            {
                return current is Window;
            }
            current = parent;
        }
        return false;
    }

    public Visual FindLogicalNode(Visual visual)
    {
        DependencyObject? current = visual;
        while (current != null)
        {
            if (IsLogicalNode(current) && current is Visual v)
            {
                return v;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return visual;
    }

    internal static INotifyCollectionChanged? GetVisualChildrenObservable(Visual visual)
    {
        // WPF does not expose an observable collection for visual children directly.
        return null;
    }

    internal static int GetVisualTreeStateHash(Visual? visual)
    {
        if (visual == null) return 0;
        int hash = visual.GetType().GetHashCode();
        
        if (visual is UIElement uiElement)
        {
            hash = HashCode.Combine(hash, uiElement.RenderSize.Width, uiElement.RenderSize.Height, uiElement.IsVisible);
        }

        int count = VisualTreeHelper.GetChildrenCount(visual);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(visual, i) is Visual child)
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
        Chrome.DevTools.Protocol.Domains.WebMcpDomain.CleanupSession(this);
        Domains.MvvmDomain.CleanupSession(this);
    }
}
