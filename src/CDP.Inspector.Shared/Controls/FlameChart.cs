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

    private const double RowHeight = 18.0;
    private bool _isDragging;
    private Point _dragStartPoint;
    private double _dragStartOffset;

    private static readonly Color[] Palette = new[]
    {
        Color.FromRgb(244, 67, 54),   // Red
        Color.FromRgb(233, 30, 99),   // Pink
        Color.FromRgb(156, 39, 176),  // Purple
        Color.FromRgb(103, 58, 183),  // Deep Purple
        Color.FromRgb(63, 81, 181),   // Indigo
        Color.FromRgb(33, 150, 243),  // Blue
        Color.FromRgb(0, 188, 212),   // Cyan
        Color.FromRgb(0, 150, 136),   // Teal
        Color.FromRgb(76, 175, 80),   // Green
        Color.FromRgb(139, 195, 74),  // Light Green
        Color.FromRgb(255, 152, 0),   // Orange
        Color.FromRgb(255, 87, 34),   // Deep Orange
        Color.FromRgb(121, 85, 72),   // Brown
        Color.FromRgb(96, 125, 139)   // Blue Grey
    };

    static FlameChart()
    {
        AffectsRender<FlameChart>(
            BlocksProperty,
            ZoomScaleProperty,
            OffsetXProperty,
            SearchTextProperty);
    }

    public FlameChart()
    {
        ClipToBounds = true;
    }

    private static Color GetColorForMethod(string methodName)
    {
        if (string.IsNullOrEmpty(methodName)) return Colors.Gray;
        if (methodName.Contains("(root)", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(60, 60, 60);
        if (methodName.Contains("(idle)", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(45, 45, 45);

        int hash = 0;
        foreach (char c in methodName)
        {
            hash = c + (hash << 6) + (hash << 16) - hash;
        }
        int idx = Math.Abs(hash) % Palette.Length;
        return Palette[idx];
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

        // Draw horizontal grid lines
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)), 1.0);
        int maxDepth = blocksList.Max(b => b.Depth);
        for (int i = 0; i <= maxDepth + 1; i++)
        {
            double y = i * RowHeight;
            context.DrawLine(gridPen, new Point(0, y), new Point(width, y));
        }

        var textBrush = new SolidColorBrush(Colors.White);

        foreach (var block in blocksList)
        {
            double bx = block.StartTimeMs * scaleX * ZoomScale + OffsetX;
            double bw = (block.EndTimeMs - block.StartTimeMs) * scaleX * ZoomScale;
            double by = block.Depth * RowHeight;
            double bh = RowHeight - 1;

            // Viewport culling check
            if (bx + bw < 0 || bx > width) continue;

            // Determine if this block is highlighted by search
            bool matchesSearch = !string.IsNullOrEmpty(SearchText) &&
                                 block.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

            var baseColor = GetColorForMethod(block.Name);
            
            // Dim non-matching blocks if search is active
            if (!string.IsNullOrEmpty(SearchText) && !matchesSearch)
            {
                baseColor = Color.FromArgb(0x33, baseColor.R, baseColor.G, baseColor.B);
            }

            var fillBrush = new SolidColorBrush(baseColor);
            var rect = new Rect(bx, by, bw, bh);

            context.DrawRectangle(fillBrush, null, rect);

            // Highlight matches with a bright border
            if (matchesSearch)
            {
                context.DrawRectangle(null, new Pen(Brushes.Yellow, 1.5), rect);
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
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
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
            int depth = (int)(currentPoint.Y / RowHeight);

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
        _isDragging = false;
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
