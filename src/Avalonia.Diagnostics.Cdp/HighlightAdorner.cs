using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Avalonia.Diagnostics.Cdp;

public class HighlightAdorner : Control
{
    private readonly Visual _adornedVisual;

    public Visual AdornedVisual => _adornedVisual;

    public HighlightAdorner(Visual adornedVisual)
    {
        _adornedVisual = adornedVisual;
        IsHitTestVisible = false;
        ClipToBounds = false;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
    }

    public override void Render(DrawingContext context)
    {
        var topLevel = TopLevel.GetTopLevel(_adornedVisual);
        if (topLevel == null) return;

        double w = _adornedVisual.Bounds.Width;
        double h = _adornedVisual.Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var pStart = _adornedVisual.TranslatePoint(new Point(0, 0), topLevel);
        if (!pStart.HasValue) return;

        double x = pStart.Value.X;
        double y = pStart.Value.Y;

        var bounds = new Rect(x, y, w, h);

        // Content box: blue semi-transparent fill
        var contentBrush = new SolidColorBrush(Color.FromArgb(64, 120, 170, 240));
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 120, 170, 240)), 1.5);
        context.DrawRectangle(contentBrush, borderPen, bounds);

        // Draw tooltip: "Type | Width x Height"
        string label = $"{_adornedVisual.GetType().Name} | {w:0}x{h:0}";
        var text = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            11,
            Brushes.White
        );

        // Position tooltip above the element, or inside/below if too close to the top
        double tooltipX = x;
        double tooltipY = y - text.Height - 6;

        if (tooltipY < 0)
        {
            tooltipY = y + h + 6; // Draw below the element if there is no space above
        }

        var textRect = new Rect(tooltipX, tooltipY - 2, text.Width + 8, text.Height + 4);
        var bgBrush = new SolidColorBrush(Color.FromArgb(220, 33, 33, 33));
        
        context.DrawRectangle(bgBrush, null, textRect);
        context.DrawText(text, new Point(tooltipX + 4, tooltipY));
    }
}
