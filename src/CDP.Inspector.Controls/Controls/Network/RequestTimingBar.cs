using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CdpInspectorApp.Controls;

public class RequestTimingBar : Control
{
    public static readonly StyledProperty<double> StartOffsetPercentProperty =
        AvaloniaProperty.Register<RequestTimingBar, double>(nameof(StartOffsetPercent), 0.0);

    public static readonly StyledProperty<double> PercentProperty =
        AvaloniaProperty.Register<RequestTimingBar, double>(nameof(Percent), 0.0);

    public static readonly StyledProperty<IBrush> BarBrushProperty =
        AvaloniaProperty.Register<RequestTimingBar, IBrush>(nameof(BarBrush), Brushes.Blue);

    public double StartOffsetPercent
    {
        get => GetValue(StartOffsetPercentProperty);
        set => SetValue(StartOffsetPercentProperty, value);
    }

    public double Percent
    {
        get => GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public IBrush BarBrush
    {
        get => GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    static RequestTimingBar()
    {
        AffectsRender<RequestTimingBar>(
            StartOffsetPercentProperty,
            PercentProperty,
            BarBrushProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        using (context.PushClip(new Rect(0, 0, width, height)))
        {
            // Draw background track (subtle dark line or track)
            var trackBrush = new SolidColorBrush(Color.FromRgb(0x2f, 0x30, 0x32));
            context.DrawRectangle(trackBrush, null, new Rect(0, 0, width, height));

            // Draw vertical grid lines (e.g. at 25%, 50%, 75%)
            var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x3c, 0x40, 0x43)), 1.0);
            context.DrawLine(gridPen, new Point(width * 0.25, 0), new Point(width * 0.25, height));
            context.DrawLine(gridPen, new Point(width * 0.50, 0), new Point(width * 0.50, height));
            context.DrawLine(gridPen, new Point(width * 0.75, 0), new Point(width * 0.75, height));

            double left = StartOffsetPercent * width;
            double barWidth = Percent * width;

            if (barWidth > 0)
            {
                context.DrawRectangle(BarBrush, null, new Rect(left, 0, barWidth, height));
            }
        }
    }
}
