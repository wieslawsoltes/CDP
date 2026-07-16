using System;
using SkiaSharp;
using CDP.Html.Parser;
using CDP.Css.Parser;
using CDP.Html.Renderer.Layout;
using CDP.Html.Renderer.Style;

namespace CDP.Html.Renderer;

public static class HtmlRenderer
{
    public static void Render(SKCanvas canvas, HtmlDocument doc, CssStyleSheet stylesheet, SKRect bounds)
    {
        if (canvas == null) throw new ArgumentNullException(nameof(canvas));
        if (doc == null) throw new ArgumentNullException(nameof(doc));

        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(rootBox, bounds.Width, bounds.Height);
        Render(rootBox, canvas, bounds.Left, bounds.Top);
    }

    public static void Render(LayoutBox box, SKCanvas canvas, float offsetX, float offsetY)
    {
        if (box.Style.Display == DisplayType.None)
            return;

        float absX = offsetX + box.X;
        float absY = offsetY + box.Y;

        // 1. Render Background
        if (box.Style.BackgroundColor.HasValue && box.Style.BackgroundColor.Value.Alpha > 0)
        {
            using var paint = new SKPaint
            {
                Color = box.Style.BackgroundColor.Value,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(absX, absY, box.Width, box.Height, paint);
        }

        // 2. Render Borders
        DrawBorders(canvas, absX, absY, box);

        // 3. Render Text line boxes
        if (box is LayoutBlockBox blockBox && blockBox.LineBoxes.Count > 0)
        {
            using var paint = new SKPaint { IsAntialias = true };
            foreach (var line in blockBox.LineBoxes)
            {
                foreach (var frag in line.Fragments)
                {
                    if (frag.Text != null)
                    {
                        var style = frag.Box.Style;
                        using (var tf = SKTypeface.FromFamilyName(style.FontFamily, style.FontWeight, SKFontStyleWidth.Normal, style.FontStyle))
                        {
                            paint.Typeface = tf;
                            paint.TextSize = style.FontSize;
                            paint.Color = style.Color;

                            float textX = offsetX + frag.X;
                            float textY = offsetY + frag.Y + frag.BaselineOffset;
                            canvas.DrawText(frag.Text, textX, textY, paint);
                        }
                    }
                }
            }
        }

        // 4. Render children recursively (only block-level, inline text is drawn in step 3)
        foreach (var child in box.Children)
        {
            if (child.IsBlockLevel)
            {
                Render(child, canvas, absX, absY);
            }
        }
    }

    private static void DrawBorders(SKCanvas canvas, float x, float y, LayoutBox box)
    {
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke };

        // Top border
        if (box.BorderTop > 0)
        {
            paint.Color = box.Style.BorderTopColor;
            paint.StrokeWidth = box.BorderTop;
            canvas.DrawLine(x, y + box.BorderTop / 2f, x + box.Width, y + box.BorderTop / 2f, paint);
        }
        // Right border
        if (box.BorderRight > 0)
        {
            paint.Color = box.Style.BorderRightColor;
            paint.StrokeWidth = box.BorderRight;
            canvas.DrawLine(x + box.Width - box.BorderRight / 2f, y, x + box.Width - box.BorderRight / 2f, y + box.Height, paint);
        }
        // Bottom border
        if (box.BorderBottom > 0)
        {
            paint.Color = box.Style.BorderBottomColor;
            paint.StrokeWidth = box.BorderBottom;
            canvas.DrawLine(x, y + box.Height - box.BorderBottom / 2f, x + box.Width, y + box.Height - box.BorderBottom / 2f, paint);
        }
        // Left border
        if (box.BorderLeft > 0)
        {
            paint.Color = box.Style.BorderLeftColor;
            paint.StrokeWidth = box.BorderLeft;
            canvas.DrawLine(x + box.BorderLeft / 2f, y, x + box.BorderLeft / 2f, y + box.Height, paint);
        }
    }
}
