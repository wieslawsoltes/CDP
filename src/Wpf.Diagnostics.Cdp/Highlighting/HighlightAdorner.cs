using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Documents;
using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp;

public class HighlightAdorner : Adorner
{
    public Visual AdornedVisual => AdornedElement;

    public HighlightAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);

        double w = AdornedElement.RenderSize.Width;
        double h = AdornedElement.RenderSize.Height;
        if (w <= 0 || h <= 0) return;

        Thickness margin = default;
        Thickness padding = default;
        Thickness borderThickness = default;

        if (AdornedElement is FrameworkElement fe)
        {
            margin = fe.Margin;
        }

        var propBorder = AdornedElement.GetType().GetProperty("BorderThickness");
        if (propBorder != null && propBorder.PropertyType == typeof(Thickness))
        {
            borderThickness = (Thickness)propBorder.GetValue(AdornedElement)!;
        }

        var propPadding = AdornedElement.GetType().GetProperty("Padding");
        if (propPadding != null && propPadding.PropertyType == typeof(Thickness))
        {
            padding = (Thickness)propPadding.GetValue(AdornedElement)!;
        }

        // 1. Draw Margin Rect (Orange)
        if (margin != default)
        {
            var marginRect = new Rect(-margin.Left, -margin.Top, w + margin.Left + margin.Right, h + margin.Top + margin.Bottom);
            var marginBrush = new SolidColorBrush(Color.FromArgb(32, 246, 178, 107));
            context.DrawRectangle(marginBrush, null, marginRect);
        }

        // 2. Draw Border Rect (Yellow-green)
        var borderRect = new Rect(0, 0, w, h);
        var borderBrush = new SolidColorBrush(Color.FromArgb(32, 255, 229, 153));
        context.DrawRectangle(borderBrush, null, borderRect);

        // 3. Draw Padding Rect (Green)
        double innerX = borderThickness.Left;
        double innerY = borderThickness.Top;
        double innerW = Math.Max(0, w - borderThickness.Left - borderThickness.Right);
        double innerH = Math.Max(0, h - borderThickness.Top - borderThickness.Bottom);
        var paddingRect = new Rect(innerX, innerY, innerW, innerH);
        var paddingBrush = new SolidColorBrush(Color.FromArgb(32, 147, 196, 125));
        context.DrawRectangle(paddingBrush, null, paddingRect);

        // 4. Draw Content Rect (Blue with solid border line)
        double contentX = innerX + padding.Left;
        double contentY = innerY + padding.Top;
        double contentW = Math.Max(0, innerW - padding.Left - padding.Right);
        double contentH = Math.Max(0, innerH - padding.Top - padding.Bottom);
        var contentRect = new Rect(contentX, contentY, contentW, contentH);
        var contentBrush = new SolidColorBrush(Color.FromArgb(64, 120, 170, 240));
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 120, 170, 240)), 1.5);
        context.DrawRectangle(contentBrush, borderPen, contentRect);

        // Draw tooltip: "Type | Width x Height | Role: ... Name: ..."
        var peer = UIElementAutomationPeer.CreatePeerForElement(AdornedElement);
        string? axRole = peer?.GetAutomationControlType().ToString();
        string? axName = peer?.GetName();
        if (string.IsNullOrEmpty(axName))
        {
            axName = AutomationProperties.GetName(AdornedElement);
        }

        string label = $"{AdornedElement.GetType().Name} | {w:0}x{h:0}";
        if (peer != null && axRole != null && axRole != "None" && axRole != "Custom")
        {
            label += $" | Role: {axRole.ToLowerInvariant()}";
            if (!string.IsNullOrEmpty(axName))
            {
                label += $" Name: \"{axName}\"";
            }
        }
        else if (!string.IsNullOrEmpty(axName))
        {
            label += $" | Name: \"{axName}\"";
        }

        var text = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip
        );

        // Position tooltip relative to the adorned element (which is the local coordinates)
        double tooltipX = 0;
        double tooltipY = -text.Height - 6;

        // check global position to prevent clipping at the top of the window
        var window = Window.GetWindow(AdornedElement);
        if (window != null)
        {
            try
            {
                var globalPt = AdornedElement.TranslatePoint(new Point(0, 0), window);
                if (globalPt.Y + tooltipY < 0)
                {
                    tooltipY = h + 6; // draw below
                }
            }
            catch { }
        }

        var textRect = new Rect(tooltipX, tooltipY - 2, text.Width + 8, text.Height + 4);
        var bgBrush = new SolidColorBrush(Color.FromArgb(220, 33, 33, 33));
        
        context.DrawRectangle(bgBrush, null, textRect);
        context.DrawText(text, new Point(tooltipX + 4, tooltipY));
    }
}
