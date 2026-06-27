using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp;

public class PaintRectsAdorner : Control
{
    private readonly TopLevel _topLevel;
    private readonly Dictionary<Visual, Rect> _previousBounds = new();
    private readonly List<PaintRectInfo> _paintRects = new();
    private readonly DispatcherTimer _timer;

    private class PaintRectInfo
    {
        public Rect Rect { get; set; }
        public DateTime SpawnTime { get; set; }
        public Color Color { get; set; }
    }

    public PaintRectsAdorner(TopLevel topLevel)
    {
        _topLevel = topLevel;
        IsHitTestVisible = false;
        ClipToBounds = false;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _timer.Tick += OnTimerTick;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel.LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel.LayoutUpdated -= OnLayoutUpdated;
        _timer.Stop();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var visited = new HashSet<Visual>();
        bool isFirstPass = _previousBounds.Count == 0;

        Traverse(_topLevel, visited, isFirstPass);

        var keysToRemove = new List<Visual>();
        foreach (var key in _previousBounds.Keys)
        {
            if (!visited.Contains(key))
            {
                keysToRemove.Add(key);
            }
        }
        foreach (var key in keysToRemove)
        {
            _previousBounds.Remove(key);
        }

        if (_paintRects.Count > 0)
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
            InvalidateVisual();
        }
    }

    private void Traverse(Visual visual, HashSet<Visual> visited, bool isFirstPass)
    {
        if (visual == this || visual is HighlightAdorner || visual is PaintRectsAdorner)
        {
            return;
        }

        if (visual is AdornerLayer)
        {
            foreach (var child in visual.GetVisualChildren())
            {
                Traverse(child, visited, isFirstPass);
            }
            return;
        }

        visited.Add(visual);

        if (visual != _topLevel && visual.IsVisible && visual.Bounds.Width > 0 && visual.Bounds.Height > 0)
        {
            var pStart = visual.TranslatePoint(new Point(0, 0), _topLevel);
            if (pStart.HasValue)
            {
                var currentRect = new Rect(pStart.Value.X, pStart.Value.Y, visual.Bounds.Width, visual.Bounds.Height);

                if (_previousBounds.TryGetValue(visual, out var prevRect))
                {
                    if (prevRect != currentRect)
                    {
                        bool sizeChanged = prevRect.Width != currentRect.Width || prevRect.Height != currentRect.Height;
                        Color color = sizeChanged ? Color.FromArgb(64, 0, 255, 0) : Color.FromArgb(64, 255, 0, 0);

                        _paintRects.Add(new PaintRectInfo
                        {
                            Rect = currentRect,
                            SpawnTime = DateTime.UtcNow,
                            Color = color
                        });
                    }
                }
                else if (!isFirstPass)
                {
                    _paintRects.Add(new PaintRectInfo
                    {
                        Rect = currentRect,
                        SpawnTime = DateTime.UtcNow,
                        Color = Color.FromArgb(64, 0, 255, 0)
                    });
                }

                _previousBounds[visual] = currentRect;
            }
        }

        foreach (var child in visual.GetVisualChildren())
        {
            Traverse(child, visited, isFirstPass);
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        _paintRects.RemoveAll(r => (now - r.SpawnTime).TotalMilliseconds >= 300);

        if (_paintRects.Count == 0)
        {
            _timer.Stop();
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var now = DateTime.UtcNow;
        foreach (var rectInfo in _paintRects)
        {
            double elapsedMs = (now - rectInfo.SpawnTime).TotalMilliseconds;
            double progress = elapsedMs / 300.0;
            double opacity = Math.Max(0.0, 1.0 - progress);
            if (opacity <= 0) continue;

            var fillBrush = new SolidColorBrush(Color.FromArgb((byte)(rectInfo.Color.A * opacity), rectInfo.Color.R, rectInfo.Color.G, rectInfo.Color.B));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(255 * opacity), rectInfo.Color.R, rectInfo.Color.G, rectInfo.Color.B)), 1.5);
            context.DrawRectangle(fillBrush, borderPen, rectInfo.Rect);
        }
    }
}
