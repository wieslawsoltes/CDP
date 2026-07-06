using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CdpInspectorApp.Controls;

public class CpuPieChart : Control
{
    public static readonly StyledProperty<double> CpuScriptingProperty =
        AvaloniaProperty.Register<CpuPieChart, double>(nameof(CpuScripting), 0.0);

    public static readonly StyledProperty<double> CpuRenderingProperty =
        AvaloniaProperty.Register<CpuPieChart, double>(nameof(CpuRendering), 0.0);

    public static readonly StyledProperty<double> CpuLayoutProperty =
        AvaloniaProperty.Register<CpuPieChart, double>(nameof(CpuLayout), 0.0);

    public static readonly StyledProperty<double> CpuSystemProperty =
        AvaloniaProperty.Register<CpuPieChart, double>(nameof(CpuSystem), 0.0);

    public static readonly StyledProperty<double> CpuIdleProperty =
        AvaloniaProperty.Register<CpuPieChart, double>(nameof(CpuIdle), 0.0);

    public double CpuScripting
    {
        get => GetValue(CpuScriptingProperty);
        set => SetValue(CpuScriptingProperty, value);
    }

    public double CpuRendering
    {
        get => GetValue(CpuRenderingProperty);
        set => SetValue(CpuRenderingProperty, value);
    }

    public double CpuLayout
    {
        get => GetValue(CpuLayoutProperty);
        set => SetValue(CpuLayoutProperty, value);
    }

    public double CpuSystem
    {
        get => GetValue(CpuSystemProperty);
        set => SetValue(CpuSystemProperty, value);
    }

    public double CpuIdle
    {
        get => GetValue(CpuIdleProperty);
        set => SetValue(CpuIdleProperty, value);
    }

    static CpuPieChart()
    {
        AffectsRender<CpuPieChart>(
            CpuScriptingProperty,
            CpuRenderingProperty,
            CpuLayoutProperty,
            CpuSystemProperty,
            CpuIdleProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        double scriptingVal = CpuScripting;
        double renderingVal = CpuRendering;
        double layoutVal = CpuLayout;
        double systemVal = CpuSystem;
        double idleVal = CpuIdle;

        double total = scriptingVal + renderingVal + layoutVal + systemVal + idleVal;
        if (total <= 0)
        {
            idleVal = 100.0;
            total = 100.0;
        }

        double pScripting = (scriptingVal / total) * 100.0;
        double pRendering = (renderingVal / total) * 100.0;
        double pLayout = (layoutVal / total) * 100.0;
        double pSystem = (systemVal / total) * 100.0;
        double pIdle = (idleVal / total) * 100.0;

        double aScripting = (pScripting / 100.0) * 2 * Math.PI;
        double aRendering = (pRendering / 100.0) * 2 * Math.PI;
        double aLayout = (pLayout / 100.0) * 2 * Math.PI;
        double aSystem = (pSystem / 100.0) * 2 * Math.PI;
        double aIdle = (pIdle / 100.0) * 2 * Math.PI;

        // Position donut chart on the left, legend on the right.
        // The donut chart is centered in a square of height x height.
        double cx = height / 2.0;
        double cy = height / 2.0;
        double padding = 10.0;
        double diameter = height - (padding * 2.0);
        if (diameter <= 0) return;

        double outerRadius = diameter / 2.0;
        double innerRadius = outerRadius * 0.55;

        var center = new Point(cx, cy);
        double startAngle = -Math.PI / 2.0;

        if (aScripting > 0)
        {
            var geom = CreateDonutSlice(center, innerRadius, outerRadius, startAngle, startAngle + aScripting);
            context.DrawGeometry(new SolidColorBrush(Color.Parse("#fdd663")), null, geom);
            startAngle += aScripting;
        }

        if (aRendering > 0)
        {
            var geom = CreateDonutSlice(center, innerRadius, outerRadius, startAngle, startAngle + aRendering);
            context.DrawGeometry(new SolidColorBrush(Color.Parse("#c5a5c5")), null, geom);
            startAngle += aRendering;
        }

        if (aLayout > 0)
        {
            var geom = CreateDonutSlice(center, innerRadius, outerRadius, startAngle, startAngle + aLayout);
            context.DrawGeometry(new SolidColorBrush(Color.Parse("#8ab4f8")), null, geom);
            startAngle += aLayout;
        }

        if (aSystem > 0)
        {
            var geom = CreateDonutSlice(center, innerRadius, outerRadius, startAngle, startAngle + aSystem);
            context.DrawGeometry(new SolidColorBrush(Color.Parse("#9aa0a6")), null, geom);
            startAngle += aSystem;
        }

        if (aIdle > 0)
        {
            var geom = CreateDonutSlice(center, innerRadius, outerRadius, startAngle, startAngle + aIdle);
            context.DrawGeometry(new SolidColorBrush(Color.Parse("#81c995")), null, geom);
            startAngle += aIdle;
        }

        // Draw side legend
        double legendX = height + 15.0;
        var legendItems = new[]
        {
            (Name: "Scripting", Color: "#fdd663", Percent: pScripting),
            (Name: "Rendering", Color: "#c5a5c5", Percent: pRendering),
            (Name: "Layout", Color: "#8ab4f8", Percent: pLayout),
            (Name: "System", Color: "#9aa0a6", Percent: pSystem),
            (Name: "Idle", Color: "#81c995", Percent: pIdle)
        };

        double rowHeight = 22.0;
        double startY = (height - (5 * rowHeight)) / 2.0;
        if (startY < 0) startY = 0;

        for (int i = 0; i < legendItems.Length; i++)
        {
            var item = legendItems[i];
            double y = startY + i * rowHeight;

            // Draw small rounded circle indicator
            var brush = new SolidColorBrush(Color.Parse(item.Color));
            context.DrawEllipse(brush, null, new Point(legendX + 6.0, y + 10.0), 5.0, 5.0);

            // Draw text
            string txt = $"{item.Name}: {item.Percent:F1}%";
            var formattedText = new FormattedText(
                txt,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11.0,
                new SolidColorBrush(Color.Parse("#e8eaed"))
            );
            context.DrawText(formattedText, new Point(legendX + 18.0, y + 3.0));
        }
    }

    private Geometry CreateDonutSlice(Point center, double innerRadius, double outerRadius, double startAngleRad, double endAngleRad)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            double angleDiff = endAngleRad - startAngleRad;
            if (angleDiff <= 0) return geometry;

            if (angleDiff >= 2.0 * Math.PI - 0.0001)
            {
                angleDiff = 2.0 * Math.PI - 0.0001;
                endAngleRad = startAngleRad + angleDiff;
            }

            bool isLargeArc = angleDiff > Math.PI;

            double cosStart = Math.Cos(startAngleRad);
            double sinStart = Math.Sin(startAngleRad);
            double cosEnd = Math.Cos(endAngleRad);
            double sinEnd = Math.Sin(endAngleRad);

            Point pInnerStart = new Point(center.X + innerRadius * cosStart, center.Y + innerRadius * sinStart);
            Point pOuterStart = new Point(center.X + outerRadius * cosStart, center.Y + outerRadius * sinStart);
            Point pOuterEnd = new Point(center.X + outerRadius * cosEnd, center.Y + outerRadius * sinEnd);
            Point pInnerEnd = new Point(center.X + innerRadius * cosEnd, center.Y + innerRadius * sinEnd);

            context.BeginFigure(pInnerStart, true);
            context.LineTo(pOuterStart);

            context.ArcTo(
                pOuterEnd,
                new Size(outerRadius, outerRadius),
                0.0,
                isLargeArc,
                SweepDirection.Clockwise);

            context.LineTo(pInnerEnd);

            context.ArcTo(
                pInnerStart,
                new Size(innerRadius, innerRadius),
                0.0,
                isLargeArc,
                SweepDirection.CounterClockwise);

            context.EndFigure(true);
        }
        return geometry;
    }
}
