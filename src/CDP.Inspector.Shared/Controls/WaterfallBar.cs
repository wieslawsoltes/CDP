using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CdpInspectorApp.Controls;

public class WaterfallBar : Control
{
    public static readonly StyledProperty<double> StartOffsetPercentProperty =
        AvaloniaProperty.Register<WaterfallBar, double>(nameof(StartOffsetPercent), 0.0);

    public static readonly StyledProperty<double> TtfbPercentProperty =
        AvaloniaProperty.Register<WaterfallBar, double>(nameof(TtfbPercent), 0.0);

    public static readonly StyledProperty<double> DownloadPercentProperty =
        AvaloniaProperty.Register<WaterfallBar, double>(nameof(DownloadPercent), 0.0);

    public double StartOffsetPercent
    {
        get => GetValue(StartOffsetPercentProperty);
        set => SetValue(StartOffsetPercentProperty, value);
    }

    public double TtfbPercent
    {
        get => GetValue(TtfbPercentProperty);
        set => SetValue(TtfbPercentProperty, value);
    }

    public double DownloadPercent
    {
        get => GetValue(DownloadPercentProperty);
        set => SetValue(DownloadPercentProperty, value);
    }

    static WaterfallBar()
    {
        AffectsRender<WaterfallBar>(
            StartOffsetPercentProperty,
            TtfbPercentProperty,
            DownloadPercentProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        using (context.PushClip(new Rect(0, 0, width, height)))
        {
            double centerY = height / 2.0;
            double left = StartOffsetPercent * width;
            double ttfbWidth = TtfbPercent * width;
            double downloadWidth = DownloadPercent * width;

            // Render TTFB: Thin bar (height 4 pixels, centered vertically)
            if (ttfbWidth > 0)
            {
                var ttfbBrush = new SolidColorBrush(Color.FromRgb(0xa1, 0xc2, 0xfa));
                var ttfbRect = new Rect(left, centerY - 2.0, ttfbWidth, 4.0);
                context.DrawRectangle(ttfbBrush, null, ttfbRect);
            }

            // Render Content Download: Thicker bar (height 8 pixels, centered vertically)
            if (downloadWidth > 0)
            {
                var downloadBrush = new SolidColorBrush(Color.FromRgb(0x4d, 0x90, 0xfe));
                var downloadRect = new Rect(left + ttfbWidth, centerY - 4.0, downloadWidth, 8.0);
                context.DrawRectangle(downloadBrush, null, downloadRect);
            }
        }
    }
}
