using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using Chrome.DevTools.Protocol;

namespace Avalonia.Diagnostics.Cdp;

public class CdpSession : Chrome.DevTools.Protocol.CdpSession
{
    private static readonly IInputDevice _dummyTouchDevice = (IInputDevice)Activator.CreateInstance(typeof(TouchDevice), nonPublic: true)!;

    public CdpSession(WebSocket webSocket, TopLevel? window) 
        : base(webSocket, window != null ? CdpServer.GetOrCreateTarget(window) : null)
    {
    }

    public new CdpTargetSession? CurrentTargetSession => base.CurrentTargetSession as CdpTargetSession;

    public TopLevel? Window => CurrentTargetSession?.Window;
    public NodeMap NodeMap => CurrentTargetSession?.NodeMap ?? new NodeMap();
    public IInputDevice TouchDevice => CurrentTargetSession?.TouchDevice ?? _dummyTouchDevice;

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

    internal bool IsLogicalNode(ILogical? node)
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

    internal static readonly System.Reflection.PropertyInfo? VisualChildrenProperty = 
        typeof(Visual).GetProperty("VisualChildren", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

    internal static INotifyCollectionChanged? GetVisualChildrenObservable(Visual visual)
    {
        return VisualChildrenProperty?.GetValue(visual) as INotifyCollectionChanged;
    }

    internal static int GetVisualTreeStateHash(Visual? visual)
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
    }
}

internal class AnonymousObserver<T> : IObserver<T>
{
    private readonly Action<T> _onNext;
    public AnonymousObserver(Action<T> onNext) => _onNext = onNext;
    public void OnCompleted() { }
    public void OnError(Exception error) { }
    public void OnNext(T value) => _onNext(value);
}
