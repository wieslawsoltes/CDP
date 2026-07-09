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
    private const double MinimapHeight = 35.0;
    private const double RowHeight = 18.0;
    private bool _isDragging;
    private bool _isDraggingMinimap;
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

        SelectedBlockProperty.Changed.AddClassHandler<FlameChart>((chart, e) => chart.OnSelectedBlockChanged(e));
    }

    public FlameChart()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    private void OnSelectedBlockChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is FlameBlock block)
        {
            CenterBlock(block);
        }
    }

    public void CenterBlock(FlameBlock block)
    {
        if (block == null) return;
        double width = Bounds.Width;
        if (width <= 0) return;

        if (Blocks == null) return;
        var blocksList = Blocks.ToList();
        if (blocksList.Count == 0) return;

        double maxTime = blocksList.Max(b => b.EndTimeMs);
        if (maxTime <= 0) return;

        double scaleX = width / maxTime;
        double blockCenterTime = block.StartTimeMs + (block.EndTimeMs - block.StartTimeMs) / 2.0;

        // OffsetX corresponding to centering this time in the viewport
        OffsetX = (width / 2.0) - (blockCenterTime * scaleX * ZoomScale);
        InvalidateVisual();
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

        // 2. Draw Minimap / Overview Background & Content
        var minimapBgBrush = new SolidColorBrush(Color.FromRgb(24, 25, 28));
        context.DrawRectangle(minimapBgBrush, null, new Rect(0, RulerHeight, width, MinimapHeight));

        int maxDepth = blocksList.Max(b => b.Depth);
        double miniRowHeight = MinimapHeight / (maxDepth + 1);
        foreach (var block in blocksList)
        {
            double mx = block.StartTimeMs * scaleX;
            double mw = (block.EndTimeMs - block.StartTimeMs) * scaleX;
            double my = RulerHeight + block.Depth * miniRowHeight;
            double mh = miniRowHeight - 0.5;

            var mBrush = new SolidColorBrush(GetColorForMethod(block.Name, block.Url));
            context.DrawRectangle(mBrush, null, new Rect(mx, my, mw, mh));
        }

        // Draw visible window overlay on minimap
        double vx1 = -OffsetX / ZoomScale;
        double vx2 = (width - OffsetX) / ZoomScale;
        // Clamp to screen bounds
        vx1 = Math.Max(0.0, Math.Min(width, vx1));
        vx2 = Math.Max(0.0, Math.Min(width, vx2));

        var maskBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x10, 0x10, 0x10));
        // Draw left mask
        if (vx1 > 0)
        {
            context.DrawRectangle(maskBrush, null, new Rect(0, RulerHeight, vx1, MinimapHeight));
        }
        // Draw right mask
        if (vx2 < width)
        {
            context.DrawRectangle(maskBrush, null, new Rect(vx2, RulerHeight, width - vx2, MinimapHeight));
        }

        // Highlight visible window outline
        var windowBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(138, 180, 248)), 1.5);
        context.DrawRectangle(null, windowBorderPen, new Rect(vx1, RulerHeight, vx2 - vx1, MinimapHeight));

        // Dividers
        var dividerPen = new Pen(new SolidColorBrush(Color.FromRgb(60, 64, 67)), 1.0);
        context.DrawLine(dividerPen, new Point(0, RulerHeight), new Point(width, RulerHeight));
        context.DrawLine(dividerPen, new Point(0, RulerHeight + MinimapHeight), new Point(width, RulerHeight + MinimapHeight));

        // 3. Draw horizontal grid lines (below minimap)
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x13, 0xFF, 0xFF, 0xFF)), 1.0);
        double blocksOffsetY = RulerHeight + MinimapHeight;
        for (int i = 0; i <= maxDepth + 1; i++)
        {
            double y = blocksOffsetY + i * RowHeight;
            context.DrawLine(gridPen, new Point(0, y), new Point(width, y));
        }

        // 4. Draw Flame Blocks
        var textBrush = new SolidColorBrush(Colors.White);

        foreach (var block in blocksList)
        {
            double bx = block.StartTimeMs * scaleX * ZoomScale + OffsetX;
            double bw = (block.EndTimeMs - block.StartTimeMs) * scaleX * ZoomScale;
            double by = blocksOffsetY + block.Depth * RowHeight;
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

        // 5. Draw Time Ruler Tick Marks and Text Labels (Top Ruler)
        var rulerDividerPen = new Pen(new SolidColorBrush(Color.FromRgb(60, 64, 67)), 1.0);
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
        Focus();
        var pt = e.GetCurrentPoint(this);
        double y = pt.Position.Y;

        if (pt.Properties.IsLeftButtonPressed)
        {
            if (y >= RulerHeight && y < RulerHeight + MinimapHeight)
            {
                // Interaction with Minimap
                _isDraggingMinimap = true;
                UpdateMinimapOffset(pt.Position.X);
                e.Handled = true;
            }
            else
            {
                // Dragging Main Timeline
                _isDragging = true;
                _pointerDownPosition = e.GetPosition(this);
                _dragStartPoint = _pointerDownPosition;
                _dragStartOffset = OffsetX;
                e.Handled = true;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var currentPoint = e.GetPosition(this);

        if (_isDraggingMinimap)
        {
            UpdateMinimapOffset(currentPoint.X);
            e.Handled = true;
        }
        else if (_isDragging)
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
            double blocksOffsetY = RulerHeight + MinimapHeight;
            int depth = (int)((currentPoint.Y - blocksOffsetY) / RowHeight);

            if (currentPoint.Y < blocksOffsetY)
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
        if (_isDraggingMinimap)
        {
            _isDraggingMinimap = false;
            e.Handled = true;
        }
        else if (_isDragging)
        {
            _isDragging = false;
            var releasePos = e.GetPosition(this);
            double distance = Math.Sqrt(Math.Pow(releasePos.X - _pointerDownPosition.X, 2) + Math.Pow(releasePos.Y - _pointerDownPosition.Y, 2));

            // If user barely moved, treat it as a click selection
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
                            double blocksOffsetY = RulerHeight + MinimapHeight;
                            int depth = (int)((releasePos.Y - blocksOffsetY) / RowHeight);

                            if (releasePos.Y >= blocksOffsetY)
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

    private void UpdateMinimapOffset(double rawX)
    {
        if (Blocks == null) return;
        var blocksList = Blocks.ToList();
        if (blocksList.Count == 0) return;

        double maxTime = blocksList.Max(b => b.EndTimeMs);
        if (maxTime <= 0) return;

        double width = Bounds.Width;
        double scaleX = width / maxTime;

        double clickedTime = rawX / scaleX;
        double visibleDuration = width / scaleX / ZoomScale;
        double newStartTime = clickedTime - visibleDuration / 2.0;

        // Apply new OffsetX
        OffsetX = -newStartTime * scaleX * ZoomScale;
        InvalidateVisual();
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
        ZoomScale = Math.Max(1.0, Math.Min(1000.0, ZoomScale * (delta > 0 ? 1.25 : 0.8)));

        // Adjust OffsetX to maintain center of zoom at mouse cursor
        OffsetX = mousePos.X - (timeUnderMouse * ZoomScale);

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Blocks == null) return;
        var blocksList = Blocks.ToList();
        if (blocksList.Count == 0) return;

        double maxTime = blocksList.Max(b => b.EndTimeMs);
        if (maxTime <= 0) return;

        double width = Bounds.Width;
        double scaleX = width / maxTime;

        // Pan Left / Right
        if (e.Key == Key.A || e.Key == Key.Left)
        {
            if (SelectedBlock != null && e.Key == Key.Left)
            {
                var sibling = blocksList
                    .Where(b => b.Depth == SelectedBlock.Depth && b.EndTimeMs <= SelectedBlock.StartTimeMs)
                    .OrderByDescending(b => b.EndTimeMs)
                    .FirstOrDefault();
                if (sibling != null)
                {
                    SelectedBlock = sibling;
                    CenterBlock(sibling);
                    e.Handled = true;
                    return;
                }
            }
            OffsetX += 50.0;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.D || e.Key == Key.Right)
        {
            if (SelectedBlock != null && e.Key == Key.Right)
            {
                var sibling = blocksList
                    .Where(b => b.Depth == SelectedBlock.Depth && b.StartTimeMs >= SelectedBlock.EndTimeMs)
                    .OrderBy(b => b.StartTimeMs)
                    .FirstOrDefault();
                if (sibling != null)
                {
                    SelectedBlock = sibling;
                    CenterBlock(sibling);
                    e.Handled = true;
                    return;
                }
            }
            OffsetX -= 50.0;
            InvalidateVisual();
            e.Handled = true;
        }
        // Zoom In / Out / Stack Traverse Up
        else if (e.Key == Key.W || e.Key == Key.Up)
        {
            if (SelectedBlock != null && e.Key == Key.Up)
            {
                var caller = blocksList
                    .Where(b => b.Depth == SelectedBlock.Depth - 1 && b.StartTimeMs <= SelectedBlock.EndTimeMs && b.EndTimeMs >= SelectedBlock.StartTimeMs)
                    .OrderByDescending(b => b.EndTimeMs - b.StartTimeMs)
                    .FirstOrDefault();
                if (caller != null)
                {
                    SelectedBlock = caller;
                    CenterBlock(caller);
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                double timeCenter = (-OffsetX + width / 2.0) / ZoomScale;
                ZoomScale = Math.Min(1000.0, ZoomScale * 1.25);
                OffsetX = (width / 2.0) - (timeCenter * ZoomScale);
                InvalidateVisual();
                e.Handled = true;
            }
        }
        // Zoom Out / Stack Traverse Down
        else if (e.Key == Key.S || e.Key == Key.Down)
        {
            if (SelectedBlock != null && e.Key == Key.Down)
            {
                var callee = blocksList
                    .Where(b => b.Depth == SelectedBlock.Depth + 1 && b.StartTimeMs <= SelectedBlock.EndTimeMs && b.EndTimeMs >= SelectedBlock.StartTimeMs)
                    .OrderByDescending(b => b.EndTimeMs - b.StartTimeMs)
                    .FirstOrDefault();
                if (callee != null)
                {
                    SelectedBlock = callee;
                    CenterBlock(callee);
                    e.Handled = true;
                    return;
                }
            }
            else
            {
                double timeCenter = (-OffsetX + width / 2.0) / ZoomScale;
                ZoomScale = Math.Max(1.0, ZoomScale * 0.8);
                OffsetX = (width / 2.0) - (timeCenter * ZoomScale);
                InvalidateVisual();
                e.Handled = true;
            }
        }
    }
}
