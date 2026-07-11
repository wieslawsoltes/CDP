using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CdpInspectorApp.Controls;

public class MetricMeterCard : Control
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MetricMeterCard, string>(nameof(Title), "CPU");

    public static readonly StyledProperty<string> ValueTextProperty =
        AvaloniaProperty.Register<MetricMeterCard, string>(nameof(ValueText), "0.0 %");

    public static readonly StyledProperty<double> FillPercentageProperty =
        AvaloniaProperty.Register<MetricMeterCard, double>(nameof(FillPercentage), 0.0);

    public static readonly StyledProperty<Color> ThemeColorProperty =
        AvaloniaProperty.Register<MetricMeterCard, Color>(nameof(ThemeColor), Colors.Orange);

    public static readonly StyledProperty<IEnumerable<double>?> HistoryDataProperty =
        AvaloniaProperty.Register<MetricMeterCard, IEnumerable<double>?>(nameof(HistoryData));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string ValueText
    {
        get => GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public double FillPercentage
    {
        get => GetValue(FillPercentageProperty);
        set => SetValue(FillPercentageProperty, value);
    }

    public Color ThemeColor
    {
        get => GetValue(ThemeColorProperty);
        set => SetValue(ThemeColorProperty, value);
    }

    public IEnumerable<double>? HistoryData
    {
        get => GetValue(HistoryDataProperty);
        set => SetValue(HistoryDataProperty, value);
    }

    static MetricMeterCard()
    {
        AffectsRender<MetricMeterCard>(
            TitleProperty,
            ValueTextProperty,
            FillPercentageProperty,
            ThemeColorProperty,
            HistoryDataProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // 1. Draw Card Background with subtle border
        var bgBrush = new SolidColorBrush(Color.Parse("#202124"));
        var borderPen = new Pen(new SolidColorBrush(Color.Parse("#3c4043")), 1.0);
        context.DrawRectangle(bgBrush, borderPen, new Rect(0, 0, w, h), 4, 4);

        // 2. Render Sparkline in background (behind text) if history is available
        var historyList = HistoryData?.ToList();
        if (historyList != null && historyList.Count > 1)
        {
            double sparkMax = historyList.Max();
            double sparkMin = historyList.Min();
            double range = sparkMax - sparkMin;
            if (range <= 0.0001) range = 1.0;

            // Sparkline area: horizontal bounds are [10, w - 10], vertical bounds are [30, h - 20]
            double startX = 10.0;
            double endX = w - 10.0;
            double startY = 32.0;
            double endY = h - 20.0;
            double usableW = endX - startX;
            double usableH = endY - startY;

            double stepX = usableW / (historyList.Count - 1);
            var sparkGeom = new StreamGeometry();
            using (var ctx = sparkGeom.Open())
            {
                for (int i = 0; i < historyList.Count; i++)
                {
                    double valNorm = (historyList[i] - sparkMin) / range;
                    double sx = startX + (i * stepX);
                    double sy = endY - (valNorm * usableH);

                    if (i == 0) ctx.BeginFigure(new Point(sx, sy), false);
                    else ctx.LineTo(new Point(sx, sy));
                }
                ctx.EndFigure(false);
            }

            var sparkColor = Color.FromArgb(0x38, ThemeColor.R, ThemeColor.G, ThemeColor.B); // ~22% opacity
            var sparkPen = new Pen(new SolidColorBrush(sparkColor), 1.5);
            context.DrawGeometry(null, sparkPen, sparkGeom);
        }

        // 3. Render Title
        var titleText = new FormattedText(
            Title.ToUpperInvariant(),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            9.0,
            new SolidColorBrush(Color.Parse("#9aa0a6"))
        );
        context.DrawText(titleText, new Point(10.0, 6.0));

        // 4. Render Large Value
        var valText = new FormattedText(
            ValueText,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
            18.0,
            new SolidColorBrush(Color.Parse("#e8eaed"))
        );
        context.DrawText(valText, new Point(10.0, 18.0));

        // 5. Draw Progress Bar Gauge (Bottom)
        double progressY = h - 10.0;
        double progressH = 3.0;
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#2d2e31")), null, new Rect(10.0, progressY, w - 20.0, progressH), 1.5, 1.5);

        double fillW = (w - 20.0) * Math.Clamp(FillPercentage, 0.0, 1.0);
        if (fillW > 0)
        {
            context.DrawRectangle(new SolidColorBrush(ThemeColor), null, new Rect(10.0, progressY, fillW, progressH), 1.5, 1.5);
        }
    }
}
