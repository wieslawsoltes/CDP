using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CdpInspectorApp.Controls;

public class RadialGauge : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(Value), 0.0);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(Minimum), 0.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(Maximum), 100.0);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<RadialGauge, IBrush?>(nameof(TrackBrush), Brushes.Gray);

    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<RadialGauge, IBrush?>(nameof(IndicatorBrush), Brushes.Blue);

    public static readonly StyledProperty<double> TrackThicknessProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(TrackThickness), 6.0);

    public static readonly StyledProperty<double> IndicatorThicknessProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(IndicatorThickness), 6.0);

    public static readonly StyledProperty<double> StartAngleProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(StartAngle), -90.0); // 12 o'clock

    public static readonly StyledProperty<double> SweepAngleProperty =
        AvaloniaProperty.Register<RadialGauge, double>(nameof(SweepAngle), 360.0);

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? IndicatorBrush
    {
        get => GetValue(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }

    public double TrackThickness
    {
        get => GetValue(TrackThicknessProperty);
        set => SetValue(TrackThicknessProperty, value);
    }

    public double IndicatorThickness
    {
        get => GetValue(IndicatorThicknessProperty);
        set => SetValue(IndicatorThicknessProperty, value);
    }

    public double StartAngle
    {
        get => GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    public double SweepAngle
    {
        get => GetValue(SweepAngleProperty);
        set => SetValue(SweepAngleProperty, value);
    }

    static RadialGauge()
    {
        AffectsRender<RadialGauge>(
            ValueProperty,
            MinimumProperty,
            MaximumProperty,
            TrackBrushProperty,
            IndicatorBrushProperty,
            TrackThicknessProperty,
            IndicatorThicknessProperty,
            StartAngleProperty,
            SweepAngleProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        double minDim = Math.Min(width, height);
        double maxThick = Math.Max(TrackThickness, IndicatorThickness);
        double radius = (minDim - maxThick) / 2.0;
        if (radius <= 0) return;

        var center = new Point(width / 2.0, height / 2.0);

        // 1. Draw Background Track Ring
        if (TrackBrush != null && TrackThickness > 0)
        {
            var trackPen = new Pen(TrackBrush, TrackThickness);
            if (Math.Abs(SweepAngle) >= 360.0)
            {
                context.DrawEllipse(null, trackPen, center, radius, radius);
            }
            else
            {
                var trackGeom = CreateArcGeometry(center, radius, StartAngle, SweepAngle);
                context.DrawGeometry(null, trackPen, trackGeom);
            }
        }

        // 2. Draw Progress Indicator Arc
        double percentage = Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0.0, 1.0);
        double progressSweep = percentage * SweepAngle;

        if (IndicatorBrush != null && IndicatorThickness > 0 && Math.Abs(progressSweep) > 0.001)
        {
            var indicatorPen = new Pen(IndicatorBrush, IndicatorThickness, lineCap: PenLineCap.Round);
            if (Math.Abs(progressSweep) >= 360.0)
            {
                context.DrawEllipse(null, indicatorPen, center, radius, radius);
            }
            else
            {
                var indicatorGeom = CreateArcGeometry(center, radius, StartAngle, progressSweep);
                context.DrawGeometry(null, indicatorPen, indicatorGeom);
            }
        }
    }

    private Geometry CreateArcGeometry(Point center, double radius, double startAngleDeg, double sweepAngleDeg)
    {
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            double startRad = startAngleDeg * Math.PI / 180.0;
            double endRad = (startAngleDeg + sweepAngleDeg) * Math.PI / 180.0;

            Point startPoint = new Point(
                center.X + radius * Math.Cos(startRad),
                center.Y + radius * Math.Sin(startRad)
            );
            Point endPoint = new Point(
                center.X + radius * Math.Cos(endRad),
                center.Y + radius * Math.Sin(endRad)
            );

            ctx.BeginFigure(startPoint, false);
            ctx.ArcTo(
                endPoint,
                new Size(radius, radius),
                0.0,
                Math.Abs(sweepAngleDeg) > 180.0,
                sweepAngleDeg > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise
            );
            ctx.EndFigure(false);
        }
        return geom;
    }
}
