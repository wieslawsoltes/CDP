using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Avalonia.Diagnostics.Cdp;

public class HighlightAdorner : Control
{
    private readonly Visual _adornedVisual;

    public HighlightAdorner(Visual adornedVisual)
    {
        _adornedVisual = adornedVisual;
        AdornerLayer.SetAdornedElement(this, adornedVisual);
    }

    public override void Render(DrawingContext context)
    {
        double w = _adornedVisual.Bounds.Width;
        double h = _adornedVisual.Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var bounds = new Rect(0, 0, w, h);

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

        // Position tooltip above the element, or inside if too close to the top
        double tooltipX = 0;
        double tooltipY = -text.Height - 6;

        if (tooltipY + _adornedVisual.Bounds.Y < 0)
        {
            tooltipY = 6;
        }

        var textRect = new Rect(tooltipX, tooltipY - 2, text.Width + 8, text.Height + 4);
        var bgBrush = new SolidColorBrush(Color.FromArgb(220, 33, 33, 33));
        
        context.DrawRectangle(bgBrush, null, textRect);
        context.DrawText(text, new Point(tooltipX + 4, tooltipY));
    }
}
