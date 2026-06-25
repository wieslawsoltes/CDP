using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var topLevel = TopLevel.GetTopLevel(_adornedVisual) ?? TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.LayoutUpdated += OnLayoutUpdated;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        var topLevel = TopLevel.GetTopLevel(_adornedVisual) ?? TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.LayoutUpdated -= OnLayoutUpdated;
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        InvalidateVisual();
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

        Thickness margin = default;
        Thickness padding = default;
        Thickness borderThickness = default;

        if (_adornedVisual is Avalonia.Layout.Layoutable layoutable)
        {
            margin = layoutable.Margin;
        }

        var propBorder = _adornedVisual.GetType().GetProperty("BorderThickness");
        if (propBorder != null && propBorder.PropertyType == typeof(Thickness))
        {
            borderThickness = (Thickness)propBorder.GetValue(_adornedVisual)!;
        }

        var propPadding = _adornedVisual.GetType().GetProperty("Padding");
        if (propPadding != null && propPadding.PropertyType == typeof(Thickness))
        {
            padding = (Thickness)propPadding.GetValue(_adornedVisual)!;
        }

        // 1. Draw Margin Rect (Orange)
        if (margin != default)
        {
            var marginRect = new Rect(x - margin.Left, y - margin.Top, w + margin.Left + margin.Right, h + margin.Top + margin.Bottom);
            var marginBrush = new SolidColorBrush(Color.FromArgb(32, 246, 178, 107));
            context.DrawRectangle(marginBrush, null, marginRect);
        }

        // 2. Draw Border Rect (Yellow-green)
        var borderRect = new Rect(x, y, w, h);
        var borderBrush = new SolidColorBrush(Color.FromArgb(32, 255, 229, 153));
        context.DrawRectangle(borderBrush, null, borderRect);

        // 3. Draw Padding Rect (Green)
        double innerX = x + borderThickness.Left;
        double innerY = y + borderThickness.Top;
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
        var peer = _adornedVisual is Control control ? ControlAutomationPeer.CreatePeerForElement(control) : null;
        string? axRole = peer?.GetAutomationControlType().ToString();
        string? axName = peer?.GetName();
        if (string.IsNullOrEmpty(axName))
        {
            axName = AutomationProperties.GetName(_adornedVisual);
        }

        string label = $"{_adornedVisual.GetType().Name} | {w:0}x{h:0}";
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
