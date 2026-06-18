using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CdpInspectorApp.Controls;

public class MemoryChart : Control
{
    public static readonly StyledProperty<IEnumerable<double>?> MemoryHistoryProperty =
        AvaloniaProperty.Register<MemoryChart, IEnumerable<double>?>(nameof(MemoryHistory));

    public IEnumerable<double>? MemoryHistory
    {
        get => GetValue(MemoryHistoryProperty);
        set => SetValue(MemoryHistoryProperty, value);
    }

    static MemoryChart()
    {
        AffectsRender<MemoryChart>(MemoryHistoryProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var history = MemoryHistory?.ToList();
        if (history == null || history.Count < 2) return;

        double max = history.Max();
        double min = history.Min();
        if (max == min) max += 1.0;

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        double stepX = width / (history.Count - 1);
        var points = new List<Point>();

        for (int i = 0; i < history.Count; i++)
        {
            double val = history[i];
            double pct = (val - min) / (max - min);
            double x = i * stepX;
            double y = height - (pct * (height - 30) + 15);
            points.Add(new Point(x, y));
        }

        var pen = new Pen(Brushes.DodgerBlue, 2);
        for (int i = 0; i < points.Count - 1; i++)
        {
            context.DrawLine(pen, points[i], points[i + 1]);
        }
    }
}
