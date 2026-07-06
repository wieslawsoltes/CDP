using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CdpInspectorApp.Controls;

public class ChromeTimelineChart : Control
{
    public static readonly StyledProperty<IEnumerable<double>?> HistoryProperty =
        AvaloniaProperty.Register<ChromeTimelineChart, IEnumerable<double>?>(nameof(History));

    public static readonly StyledProperty<Color> FillColorProperty =
        AvaloniaProperty.Register<ChromeTimelineChart, Color>(nameof(FillColor), Colors.DodgerBlue);

    public static readonly StyledProperty<Color> StrokeColorProperty =
        AvaloniaProperty.Register<ChromeTimelineChart, Color>(nameof(StrokeColor), Colors.Blue);

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<ChromeTimelineChart, string>(nameof(Unit), "");

    public static readonly StyledProperty<double?> MaxDisplayValueProperty =
        AvaloniaProperty.Register<ChromeTimelineChart, double?>(nameof(MaxDisplayValue), null);

    public IEnumerable<double>? History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    public Color FillColor
    {
        get => GetValue(FillColorProperty);
        set => SetValue(FillColorProperty, value);
    }

    public Color StrokeColor
    {
        get => GetValue(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public double? MaxDisplayValue
    {
        get => GetValue(MaxDisplayValueProperty);
        set => SetValue(MaxDisplayValueProperty, value);
    }

    static ChromeTimelineChart()
    {
        AffectsRender<ChromeTimelineChart>(
            HistoryProperty,
            FillColorProperty,
            StrokeColorProperty,
            UnitProperty,
            MaxDisplayValueProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        // 1. Background grid: 3 or 4 horizontal dashed/faded lines using semi-transparent gray brush
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)), 1.0);
        context.DrawLine(gridPen, new Point(0, height * 0.25), new Point(width, height * 0.25));
        context.DrawLine(gridPen, new Point(0, height * 0.50), new Point(width, height * 0.50));
        context.DrawLine(gridPen, new Point(0, height * 0.75), new Point(width, height * 0.75));

        // Get history list
        var history = History?.ToList();

        // 2. Scale calculation
        double max = 0.0;
        double min = 0.0;
        if (history != null && history.Count > 0)
        {
            max = history.Max();
            min = history.Min();
        }

        if (max == min)
        {
            max += 1.0;
        }

        if (MaxDisplayValue.HasValue && MaxDisplayValue.Value > max)
        {
            max = MaxDisplayValue.Value;
        }

        // 3. Coordinates generation & Area / Stroke drawing
        if (history != null && history.Count > 0)
        {
            var points = new List<Point>();
            double stepX = history.Count > 1 ? width / (history.Count - 1) : width;

            double padding = 2.0;
            double usableHeight = height - (padding * 2);

            for (int i = 0; i < history.Count; i++)
            {
                double val = history[i];
                double pct = (max - min > 0) ? (val - min) / (max - min) : 0.0;
                double x = i * stepX;
                double y = height - padding - (pct * usableHeight);
                points.Add(new Point(x, y));
            }

            if (points.Count > 0)
            {
                // Area drawing: filled geometry under the line down to the bottom of the bounds using a gradient brush
                var gradientBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0.5, 0.0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0.5, 1.0, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb((byte)(FillColor.A * 0.4), FillColor.R, FillColor.G, FillColor.B), 0.0),
                        new GradientStop(Color.FromArgb(0, FillColor.R, FillColor.G, FillColor.B), 1.0)
                    }
                };

                var geometry = new StreamGeometry();
                using (var geometryContext = geometry.Open())
                {
                    geometryContext.BeginFigure(new Point(points[0].X, height), true);
                    for (int i = 0; i < points.Count; i++)
                    {
                        geometryContext.LineTo(points[i]);
                    }
                    geometryContext.LineTo(new Point(points[^1].X, height));
                    geometryContext.EndFigure(true);
                }

                context.DrawGeometry(gradientBrush, null, geometry);

                // Outline line: Draw solid line of StrokeColor with thickness 1.5
                var strokePen = new Pen(new SolidColorBrush(StrokeColor), 1.5);
                for (int i = 0; i < points.Count - 1; i++)
                {
                    context.DrawLine(strokePen, points[i], points[i + 1]);
                }
            }
        }

        // 4. Overlay text: current value & maximum value using FormattedText
        double curValue = (history != null && history.Count > 0) ? history.Last() : 0.0;
        string textStr = $"{curValue:0.##}{Unit} (Max: {max:0.##}{Unit})";

        var formattedText = new FormattedText(
            textStr,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            10,
            new SolidColorBrush(Color.FromRgb(154, 160, 166))
        );

        context.DrawText(formattedText, new Point(6, 6));
    }
}
