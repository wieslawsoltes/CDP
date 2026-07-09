using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace CdpInspectorApp.Controls;

public class FlameBlock
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public double StartTimeMs { get; set; }
    public double EndTimeMs { get; set; }
    public int Depth { get; set; }
    public double SelfTimeMs { get; set; }
    public double SelfTimePct { get; set; }
    public double TotalTimeMs { get; set; }
    public double TotalTimePct { get; set; }
}

public class FlameChart : Control
{
    public static readonly StyledProperty<IEnumerable<FlameBlock>?> BlocksProperty =
        AvaloniaProperty.Register<FlameChart, IEnumerable<FlameBlock>?>(nameof(Blocks));

    public static readonly StyledProperty<double> ZoomScaleProperty =
        AvaloniaProperty.Register<FlameChart, double>(nameof(ZoomScale), 1.0);

    public static readonly StyledProperty<double> OffsetXProperty =
        AvaloniaProperty.Register<FlameChart, double>(nameof(OffsetX), 0.0);

    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<FlameChart, string?>(nameof(SearchText), null);

    public static readonly StyledProperty<FlameBlock?> HoveredBlockProperty =
        AvaloniaProperty.Register<FlameChart, FlameBlock?>(nameof(HoveredBlock));

    public static readonly StyledProperty<FlameBlock?> SelectedBlockProperty =
        AvaloniaProperty.Register<FlameChart, FlameBlock?>(nameof(SelectedBlock));

    public IEnumerable<FlameBlock>? Blocks
    {
        get => GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    public double ZoomScale
    {
        get => GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    public double OffsetX
    {
        get => GetValue(OffsetXProperty);
        set => SetValue(OffsetXProperty, value);
    }

    public string? SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public FlameBlock? HoveredBlock
    {
        get => GetValue(HoveredBlockProperty);
        set => SetValue(HoveredBlockProperty, value);
    }

    public FlameBlock? SelectedBlock
    {
        get => GetValue(SelectedBlockProperty);
        set => SetValue(SelectedBlockProperty, value);
    }

    private const double RulerHeight = 22.0;
    private const double RowHeight = 18.0;
    private bool _isDragging;
    private Point _dragStartPoint;
    private Point _pointerDownPosition;
    private double _dragStartOffset;

    static FlameChart()
    {
        AffectsRender<FlameChart>(
            BlocksProperty,
            ZoomScaleProperty,
            OffsetXProperty,
            SearchTextProperty,
            HoveredBlockProperty,
            SelectedBlockProperty);
    }

    public FlameChart()
    {
        ClipToBounds = true;
    }

    private static Color GetColorForMethod(string methodName, string url)
    {
        if (string.IsNullOrEmpty(methodName)) return Color.FromRgb(100, 100, 100);
        if (methodName.Contains("(root)", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(48, 57, 66);
        if (methodName.Contains("(idle)", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(32, 33, 36);

        // Core runtime thread / native code
        if (methodName.Contains("[native code]", StringComparison.OrdinalIgnoreCase) || url.EndsWith(".dll") && !url.Contains("System") && !url.Contains("Microsoft") && !url.Contains("Avalonia"))
        {
            return Color.FromRgb(189, 189, 189); // Gray 400
        }

        // Framework code: System / Microsoft
        if (url.Contains("System.", StringComparison.OrdinalIgnoreCase) || 
            url.Contains("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
        {
            return Color.FromRgb(100, 181, 246); // Material Blue 300
        }

        // Avalonia framework layout/rendering code
        if (url.Contains("Avalonia.", StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith("Avalonia.", StringComparison.OrdinalIgnoreCase))
        {
            return Color.FromRgb(129, 199, 132); // Material Green 300
        }

        // User application code: Amber/Orange
        return Color.FromRgb(255, 183, 77); // Material Amber 300
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 0 || height <= 0 || Blocks == null) return;

        var blocksList = Blocks.ToList();
        if (blocksList.Count == 0) return;

        double maxTime = blocksList.Max(b => b.EndTimeMs);
        if (maxTime <= 0) return;

        double scaleX = width / maxTime;

        // 1. Draw Time Ruler Background & Divide Line
        var rulerBgBrush = new SolidColorBrush(Color.FromRgb(32, 33, 36));
        context.DrawRectangle(rulerBgBrush, null, new Rect(0, 0, width, RulerHeight));

        // 2. Draw horizontal grid lines (below ruler)
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x13, 0xFF, 0xFF, 0xFF)), 1.0);
        int maxDepth = blocksList.Max(b => b.Depth);
        for (int i = 0; i <= maxDepth + 1; i++)
        {
            double y = RulerHeight + i * RowHeight;
            context.DrawLine(gridPen, new Point(0, y), new Point(width, y));
        }

        // 3. Draw Flame Blocks
        var textBrush = new SolidColorBrush(Colors.White);

        foreach (var block in blocksList)
        {
            double bx = block.StartTimeMs * scaleX * ZoomScale + OffsetX;
            double bw = (block.EndTimeMs - block.StartTimeMs) * scaleX * ZoomScale;
            double by = RulerHeight + block.Depth * RowHeight;
            double bh = RowHeight - 1;

            // Viewport culling
            if (bx + bw < 0 || bx > width) continue;

            bool matchesSearch = !string.IsNullOrEmpty(SearchText) &&
                                 block.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

            var baseColor = GetColorForMethod(block.Name, block.Url);
            
            // Dim non-matching blocks if search is active
            if (!string.IsNullOrEmpty(SearchText) && !matchesSearch)
            {
                baseColor = Color.FromArgb(0x22, baseColor.R, baseColor.G, baseColor.B);
            }

            var fillBrush = new SolidColorBrush(baseColor);
            var rect = new Rect(bx, by, bw, bh);

            context.DrawRectangle(fillBrush, null, rect);

            // Highlight matches with a bright yellow border
            if (matchesSearch)
            {
                context.DrawRectangle(null, new Pen(Brushes.Yellow, 1.5), rect);
            }
            // Highlight selected block with a blue border
            if (SelectedBlock == block)
            {
                context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(66, 165, 245)), 2.0), rect);
            }
            else if (HoveredBlock == block)
            {
                context.DrawRectangle(null, new Pen(Brushes.White, 1.0), rect);
            }

            // Draw function label if width is sufficient
            if (bw > 25)
            {
                using (context.PushClip(rect))
                {
                    var formattedText = new FormattedText(
                        block.Name,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        9.0,
                        textBrush
                    );
                    context.DrawText(formattedText, new Point(bx + 4, by + 3));
                }
            }
        }

        // 4. Draw Time Ruler Tick Marks and Text Labels
        var rulerDividerPen = new Pen(new SolidColorBrush(Color.FromRgb(60, 64, 67)), 1.0);
        context.DrawLine(rulerDividerPen, new Point(0, RulerHeight), new Point(width, RulerHeight));

        var rulerTextBrush = new SolidColorBrush(Color.FromRgb(154, 160, 166));
        var tickPen = new Pen(new SolidColorBrush(Color.FromRgb(95, 99, 104)), 1.0);

        // Convert viewport horizontal coordinates back to timeline time
        double startTime = -OffsetX / ZoomScale / scaleX;
        double endTime = (width - OffsetX) / ZoomScale / scaleX;
        double visibleDuration = endTime - startTime;

        // Choose appropriate ruler intervals dynamically
        double interval = 5000.0;
        if (visibleDuration < 10.0) interval = 1.0;
        else if (visibleDuration < 50.0) interval = 5.0;
        else if (visibleDuration < 200.0) interval = 20.0;
        else if (visibleDuration < 1000.0) interval = 100.0;
        else if (visibleDuration < 5000.0) interval = 500.0;
        else if (visibleDuration < 20000.0) interval = 2000.0;

        double t = Math.Ceiling(startTime / interval) * interval;
        while (t < endTime)
        {
            double tx = t * scaleX * ZoomScale + OffsetX;
            if (tx >= 0 && tx <= width)
            {
                // Major tick
                context.DrawLine(tickPen, new Point(tx, RulerHeight - 8), new Point(tx, RulerHeight));

                string label = t >= 1000.0 ? $"{(t / 1000.0):0.##} s" : $"{t:0} ms";
                var labelText = new FormattedText(
                    label,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    8.5,
                    rulerTextBrush
                );
                context.DrawText(labelText, new Point(tx + 3, 4));
            }
            t += interval;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _pointerDownPosition = e.GetPosition(this);
            _dragStartPoint = _pointerDownPosition;
            _dragStartOffset = OffsetX;
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var currentPoint = e.GetPosition(this);

        if (_isDragging)
        {
            OffsetX = _dragStartOffset + (currentPoint.X - _dragStartPoint.X);
            InvalidateVisual();
            e.Handled = true;
        }
        else
        {
            // Hover detection
            if (Blocks == null) return;
            var blocksList = Blocks.ToList();
            if (blocksList.Count == 0) return;

            double maxTime = blocksList.Max(b => b.EndTimeMs);
            if (maxTime <= 0) return;

            double scaleX = Bounds.Width / maxTime;

            double timeMs = (currentPoint.X - OffsetX) / ZoomScale / scaleX;
            int depth = (int)((currentPoint.Y - RulerHeight) / RowHeight);

            if (currentPoint.Y < RulerHeight)
            {
                depth = -1;
            }

            var hover = blocksList.FirstOrDefault(b => b.Depth == depth && timeMs >= b.StartTimeMs && timeMs <= b.EndTimeMs);
            if (hover != HoveredBlock)
            {
                HoveredBlock = hover;
                InvalidateVisual();
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging)
        {
            _isDragging = false;
            var releasePos = e.GetPosition(this);
            double distance = Math.Sqrt(Math.Pow(releasePos.X - _pointerDownPosition.X, 2) + Math.Pow(releasePos.Y - _pointerDownPosition.Y, 2));

            // If the user barely moved, treat it as a selection click
            if (distance < 3.0)
            {
                if (Blocks != null)
                {
                    var blocksList = Blocks.ToList();
                    if (blocksList.Count > 0)
                    {
                        double maxTime = blocksList.Max(b => b.EndTimeMs);
                        if (maxTime > 0)
                        {
                            double scaleX = Bounds.Width / maxTime;
                            double timeMs = (releasePos.X - OffsetX) / ZoomScale / scaleX;
                            int depth = (int)((releasePos.Y - RulerHeight) / RowHeight);

                            if (releasePos.Y >= RulerHeight)
                            {
                                var block = blocksList.FirstOrDefault(b => b.Depth == depth && timeMs >= b.StartTimeMs && timeMs <= b.EndTimeMs);
                                SelectedBlock = block;
                            }
                            else
                            {
                                SelectedBlock = null;
                            }
                        }
                    }
                }
            }
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        double delta = e.Delta.Y;
        var mousePos = e.GetPosition(this);

        if (Blocks == null) return;
        var blocksList = Blocks.ToList();
        if (blocksList.Count == 0) return;

        double maxTime = blocksList.Max(b => b.EndTimeMs);
        if (maxTime <= 0) return;

        double scaleX = Bounds.Width / maxTime;

        // Current timeline position under the mouse cursor
        double timeUnderMouse = (mousePos.X - OffsetX) / ZoomScale;

        // Adjust ZoomScale
        double oldZoom = ZoomScale;
        ZoomScale = Math.Max(1.0, Math.Min(1000.0, ZoomScale * (delta > 0 ? 1.25 : 0.8)));

        // Adjust OffsetX to maintain center of zoom at mouse cursor
        OffsetX = mousePos.X - (timeUnderMouse * ZoomScale);

        InvalidateVisual();
        e.Handled = true;
    }
}
