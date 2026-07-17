using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CDP.Inspector.Wysiwyg.Controls;

/// <summary>
/// Identifies which resize handle the user is interacting with.
/// </summary>
public enum ResizeHandle
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left
}

/// <summary>
/// An Avalonia control that renders overlay graphics (selection rectangles,
/// margin/padding indicators, resize handles) on top of the preview image.
/// </summary>
public class DesignerOverlay : Control
{
    public static readonly StyledProperty<Rect> SelectedBoundsProperty =
        AvaloniaProperty.Register<DesignerOverlay, Rect>(nameof(SelectedBounds));

    public static readonly StyledProperty<Thickness> MarginThicknessProperty =
        AvaloniaProperty.Register<DesignerOverlay, Thickness>(nameof(MarginThickness));

    public static readonly StyledProperty<Thickness> PaddingThicknessProperty =
        AvaloniaProperty.Register<DesignerOverlay, Thickness>(nameof(PaddingThickness));

    public static readonly StyledProperty<bool> IsOverlayVisibleProperty =
        AvaloniaProperty.Register<DesignerOverlay, bool>(nameof(IsOverlayVisible));

    public static readonly StyledProperty<double> HandleSizeProperty =
        AvaloniaProperty.Register<DesignerOverlay, double>(nameof(HandleSize), 6.0);

    public static readonly StyledProperty<System.Collections.Generic.IEnumerable<Rect>?> MultiSelectedBoundsProperty =
        AvaloniaProperty.Register<DesignerOverlay, System.Collections.Generic.IEnumerable<Rect>?>(nameof(MultiSelectedBounds));

    public static readonly StyledProperty<string?> StackPanelOrientationProperty =
        AvaloniaProperty.Register<DesignerOverlay, string?>(nameof(StackPanelOrientation));

    public static readonly StyledProperty<string?> SelectedElementBadgeProperty =
        AvaloniaProperty.Register<DesignerOverlay, string?>(nameof(SelectedElementBadge));

    static DesignerOverlay()
    {
        AffectsRender<DesignerOverlay>(
            SelectedBoundsProperty,
            MultiSelectedBoundsProperty,
            StackPanelOrientationProperty,
            SelectedElementBadgeProperty,
            MarginThicknessProperty,
            PaddingThicknessProperty,
            IsOverlayVisibleProperty,
            HandleSizeProperty);
    }

    public Rect SelectedBounds
    {
        get => GetValue(SelectedBoundsProperty);
        set => SetValue(SelectedBoundsProperty, value);
    }

    public Thickness MarginThickness
    {
        get => GetValue(MarginThicknessProperty);
        set => SetValue(MarginThicknessProperty, value);
    }

    public Thickness PaddingThickness
    {
        get => GetValue(PaddingThicknessProperty);
        set => SetValue(PaddingThicknessProperty, value);
    }

    public bool IsOverlayVisible
    {
        get => GetValue(IsOverlayVisibleProperty);
        set => SetValue(IsOverlayVisibleProperty, value);
    }

    public double HandleSize
    {
        get => GetValue(HandleSizeProperty);
        set => SetValue(HandleSizeProperty, value);
    }

    public System.Collections.Generic.IEnumerable<Rect>? MultiSelectedBounds
    {
        get => GetValue(MultiSelectedBoundsProperty);
        set => SetValue(MultiSelectedBoundsProperty, value);
    }

    public string? StackPanelOrientation
    {
        get => GetValue(StackPanelOrientationProperty);
        set => SetValue(StackPanelOrientationProperty, value);
    }

    public string? SelectedElementBadge
    {
        get => GetValue(SelectedElementBadgeProperty);
        set => SetValue(SelectedElementBadgeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Draw a transparent background to make the entire control hit-testable
        context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Bounds.Width, Bounds.Height));

        if (!IsOverlayVisible)
            return;

        var bounds = SelectedBounds;

        // Draw guidelines (dotted lines) first if any element is selected
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 235, 59)), 1)
            {
                DashStyle = DashStyle.Dot
            };
            context.DrawLine(guidePen, new Point(bounds.X, 0), new Point(bounds.X, this.Bounds.Height));
            context.DrawLine(guidePen, new Point(bounds.Right, 0), new Point(bounds.Right, this.Bounds.Height));
            context.DrawLine(guidePen, new Point(0, bounds.Y), new Point(this.Bounds.Width, bounds.Y));
            context.DrawLine(guidePen, new Point(0, bounds.Bottom), new Point(this.Bounds.Width, bounds.Bottom));
        }

        // Draw selection border (blue 2px) for all selected elements
        var selectionPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 33, 150, 243)), 2);
        if (MultiSelectedBounds != null)
        {
            foreach (var rect in MultiSelectedBounds)
            {
                if (rect.Width > 0 && rect.Height > 0 && rect != bounds)
                {
                    context.DrawRectangle(null, selectionPen, rect);
                }
            }
        }

        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var margin = MarginThickness;
        var padding = PaddingThickness;
        var handleSize = HandleSize;

        // Margin area (green semi-transparent bands)
        var marginBrush = new SolidColorBrush(Color.FromArgb(60, 76, 175, 80));
        var outerRect = new Rect(
            bounds.X - margin.Left,
            bounds.Y - margin.Top,
            bounds.Width + margin.Left + margin.Right,
            bounds.Height + margin.Top + margin.Bottom);

        // Top margin band
        context.FillRectangle(marginBrush, new Rect(outerRect.X, outerRect.Y, outerRect.Width, margin.Top));
        // Bottom margin band
        context.FillRectangle(marginBrush, new Rect(outerRect.X, bounds.Bottom, outerRect.Width, margin.Bottom));
        // Left margin band
        context.FillRectangle(marginBrush, new Rect(outerRect.X, bounds.Y, margin.Left, bounds.Height));
        // Right margin band
        context.FillRectangle(marginBrush, new Rect(bounds.Right, bounds.Y, margin.Right, bounds.Height));

        // Padding area (orange semi-transparent bands)
        var paddingBrush = new SolidColorBrush(Color.FromArgb(60, 255, 152, 0));
        // Top padding band
        context.FillRectangle(paddingBrush, new Rect(bounds.X, bounds.Y, bounds.Width, padding.Top));
        // Bottom padding band
        context.FillRectangle(paddingBrush, new Rect(bounds.X, bounds.Bottom - padding.Bottom, bounds.Width, padding.Bottom));
        // Left padding band
        context.FillRectangle(paddingBrush, new Rect(bounds.X, bounds.Y + padding.Top, padding.Left, bounds.Height - padding.Top - padding.Bottom));
        // Right padding band
        context.FillRectangle(paddingBrush, new Rect(bounds.Right - padding.Right, bounds.Y + padding.Top, padding.Right, bounds.Height - padding.Top - padding.Bottom));

        // Selection border for primary element (blue 2px)
        context.DrawRectangle(null, selectionPen, bounds);

        // Spacing rulers (numerical pixel value text next to overlay bands)
        var textBrush = Brushes.White;
        var typeface = new Typeface(FontFamily.Default);

        void DrawRulerText(string text, Point pos)
        {
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                textBrush);
            context.DrawText(formattedText, pos);
        }

        // Draw Margin Rulers
        if (margin.Left > 0)
            DrawRulerText($"{margin.Left:F0}", new Point(bounds.X - margin.Left / 2 - 10, bounds.Y + bounds.Height / 2 - 5));
        if (margin.Top > 0)
            DrawRulerText($"{margin.Top:F0}", new Point(bounds.X + bounds.Width / 2 - 10, bounds.Y - margin.Top / 2 - 5));
        if (margin.Right > 0)
            DrawRulerText($"{margin.Right:F0}", new Point(bounds.Right + margin.Right / 2 - 5, bounds.Y + bounds.Height / 2 - 5));
        if (margin.Bottom > 0)
            DrawRulerText($"{margin.Bottom:F0}", new Point(bounds.X + bounds.Width / 2 - 10, bounds.Bottom + margin.Bottom / 2 - 5));

        // Draw Padding Rulers
        if (padding.Left > 0)
            DrawRulerText($"{padding.Left:F0}", new Point(bounds.X + padding.Left / 2 - 5, bounds.Y + bounds.Height / 2 - 5));
        if (padding.Top > 0)
            DrawRulerText($"{padding.Top:F0}", new Point(bounds.X + bounds.Width / 2 - 10, bounds.Y + padding.Top / 2 - 5));
        if (padding.Right > 0)
            DrawRulerText($"{padding.Right:F0}", new Point(bounds.Right - padding.Right / 2 - 15, bounds.Y + bounds.Height / 2 - 5));
        if (padding.Bottom > 0)
            DrawRulerText($"{padding.Bottom:F0}", new Point(bounds.X + bounds.Width / 2 - 10, bounds.Bottom - padding.Bottom / 2 - 5));

        // Flow direction arrows
        if (!string.IsNullOrEmpty(StackPanelOrientation))
        {
            var arrowPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 33, 150, 243)), 2);
            if (StackPanelOrientation.Equals("Horizontal", StringComparison.OrdinalIgnoreCase))
            {
                var startX = bounds.Right - 30;
                var startY = bounds.Bottom - 15;
                context.DrawLine(arrowPen, new Point(startX, startY), new Point(bounds.Right - 10, startY));
                context.DrawLine(arrowPen, new Point(bounds.Right - 15, startY - 4), new Point(bounds.Right - 10, startY));
                context.DrawLine(arrowPen, new Point(bounds.Right - 15, startY + 4), new Point(bounds.Right - 10, startY));
            }
            else if (StackPanelOrientation.Equals("Vertical", StringComparison.OrdinalIgnoreCase))
            {
                var startX = bounds.Right - 15;
                var startY = bounds.Bottom - 30;
                context.DrawLine(arrowPen, new Point(startX, startY), new Point(startX, bounds.Bottom - 10));
                context.DrawLine(arrowPen, new Point(startX - 4, bounds.Bottom - 15), new Point(startX, bounds.Bottom - 10));
                context.DrawLine(arrowPen, new Point(startX + 4, bounds.Bottom - 15), new Point(startX, bounds.Bottom - 10));
            }
        }

        // Accessibility ID badge
        if (!string.IsNullOrEmpty(SelectedElementBadge))
        {
            var badgeTypeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
            var badgeText = new FormattedText(
                SelectedElementBadge,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                badgeTypeface,
                9,
                Brushes.White);
            
            var badgeRect = new Rect(
                bounds.X,
                bounds.Y - 16 >= 0 ? bounds.Y - 16 : bounds.Y + 2,
                badgeText.Width + 8,
                14);

            var badgeBackground = new SolidColorBrush(Color.FromArgb(220, 33, 150, 243));
            context.FillRectangle(badgeBackground, badgeRect, 2.0f);
            context.DrawText(badgeText, new Point(badgeRect.X + 4, badgeRect.Y + 1));
        }

        // Resize handles (white-filled, blue-bordered squares)
        var handleFill = Brushes.White;
        var handlePen = new Pen(new SolidColorBrush(Color.FromArgb(255, 33, 150, 243)), 1);
        var half = handleSize / 2;

        // Corners
        DrawHandle(context, handleFill, handlePen, bounds.X - half, bounds.Y - half, handleSize);
        DrawHandle(context, handleFill, handlePen, bounds.Right - half, bounds.Y - half, handleSize);
        DrawHandle(context, handleFill, handlePen, bounds.Right - half, bounds.Bottom - half, handleSize);
        DrawHandle(context, handleFill, handlePen, bounds.X - half, bounds.Bottom - half, handleSize);

        // Edge midpoints
        DrawHandle(context, handleFill, handlePen, bounds.X + bounds.Width / 2 - half, bounds.Y - half, handleSize);
        DrawHandle(context, handleFill, handlePen, bounds.Right - half, bounds.Y + bounds.Height / 2 - half, handleSize);
        DrawHandle(context, handleFill, handlePen, bounds.X + bounds.Width / 2 - half, bounds.Bottom - half, handleSize);
        DrawHandle(context, handleFill, handlePen, bounds.X - half, bounds.Y + bounds.Height / 2 - half, handleSize);
    }

    /// <summary>
    /// Hit-tests a point against the resize handles and returns which handle was hit.
    /// </summary>
    public ResizeHandle HitTestHandle(Point point)
    {
        var bounds = SelectedBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return ResizeHandle.None;

        var tolerance = HandleSize + 2;

        if (IsNear(point, bounds.TopLeft, tolerance)) return ResizeHandle.TopLeft;
        if (IsNear(point, new Point(bounds.X + bounds.Width / 2, bounds.Y), tolerance)) return ResizeHandle.Top;
        if (IsNear(point, bounds.TopRight, tolerance)) return ResizeHandle.TopRight;
        if (IsNear(point, new Point(bounds.Right, bounds.Y + bounds.Height / 2), tolerance)) return ResizeHandle.Right;
        if (IsNear(point, bounds.BottomRight, tolerance)) return ResizeHandle.BottomRight;
        if (IsNear(point, new Point(bounds.X + bounds.Width / 2, bounds.Bottom), tolerance)) return ResizeHandle.Bottom;
        if (IsNear(point, bounds.BottomLeft, tolerance)) return ResizeHandle.BottomLeft;
        if (IsNear(point, new Point(bounds.X, bounds.Y + bounds.Height / 2), tolerance)) return ResizeHandle.Left;

        return ResizeHandle.None;
    }

    private static void DrawHandle(DrawingContext context, IBrush fill, IPen pen, double x, double y, double size)
    {
        context.DrawRectangle(fill, pen, new Rect(x, y, size, size));
    }

    private static bool IsNear(Point point, Point target, double tolerance)
    {
        return Math.Abs(point.X - target.X) <= tolerance && Math.Abs(point.Y - target.Y) <= tolerance;
    }
}
