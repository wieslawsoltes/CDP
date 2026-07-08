using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Wpf.Diagnostics.Cdp;

public class PaintRectsAdorner : Adorner
{
    private readonly Window _window;
    private readonly Dictionary<Visual, Rect> _previousBounds = new();
    private readonly List<PaintRectInfo> _paintRects = new();
    private readonly DispatcherTimer _timer;

    private class PaintRectInfo
    {
        public Rect Rect { get; set; }
        public DateTime SpawnTime { get; set; }
        public Color Color { get; set; }
    }

    public PaintRectsAdorner(Window window) : base(window)
    {
        _window = window;
        IsHitTestVisible = false;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _timer.Tick += OnTimerTick;

        _window.LayoutUpdated += OnLayoutUpdated;
    }

    public void Detach()
    {
        _window.LayoutUpdated -= OnLayoutUpdated;
        _timer.Stop();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var visited = new HashSet<Visual>();
        bool isFirstPass = _previousBounds.Count == 0;

        Traverse(_window, visited, isFirstPass);

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
            int childCount = VisualTreeHelper.GetChildrenCount(visual);
            for (int i = 0; i < childCount; i++)
            {
                if (VisualTreeHelper.GetChild(visual, i) is Visual child)
                {
                    Traverse(child, visited, isFirstPass);
                }
            }
            return;
        }

        visited.Add(visual);

        if (visual != _window && visual is UIElement ui && ui.IsVisible && ui.RenderSize.Width > 0 && ui.RenderSize.Height > 0)
        {
            try
            {
                var pStart = ui.TranslatePoint(new Point(0, 0), _window);
                var currentRect = new Rect(pStart.X, pStart.Y, ui.RenderSize.Width, ui.RenderSize.Height);

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
            catch { }
        }

        int count = VisualTreeHelper.GetChildrenCount(visual);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(visual, i) is Visual child)
            {
                Traverse(child, visited, isFirstPass);
            }
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

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);

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
