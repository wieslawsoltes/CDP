using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Document.Parser.AST;

namespace CDP.Document.Renderer.Layout.Presentation;

/// <summary>
/// Layout block for an entire PowerPoint presentation, containing slide layout blocks.
/// </summary>
public class PresentationDocumentLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }
    public List<SlideLayoutBlock> Slides { get; } = new();

    public PresentationDocumentLayoutBlock(PresentationDocument doc)
    {
        Node = doc;
    }

    public void Layout(LayoutContext context) { }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        foreach (var slide in Slides)
        {
            slide.Render(canvas, context);
        }
    }

    public int HitTest(SKPoint point)
    {
        foreach (var slide in Slides)
        {
            if (slide.Bounds.Contains(point))
            {
                return slide.HitTest(point);
            }
        }
        return -1;
    }

    public SKRect GetCaretBounds(int offset) => default;
    public void GetSelectionBounds(int start, int end, IList<SKRect> result) { }
}

/// <summary>
/// Layout block for a single slide with shapes rendered onto it.
/// </summary>
public class SlideLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }
    public int SlideIndex { get; set; }
    public List<ShapeLayoutBlock> Shapes { get; } = new();
    public string? BackgroundColor { get; set; }

    public SlideLayoutBlock(SlideNode node)
    {
        Node = node;
    }

    public void Layout(LayoutContext context) { }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        if (context.Viewport != SKRect.Empty && !Bounds.IntersectsWith(context.Viewport))
        {
            return;
        }

        // Draw slide background
        SKColor bgColor = SKColors.White;
        if (!string.IsNullOrEmpty(BackgroundColor) && SKColor.TryParse(BackgroundColor, out var parsedBg))
        {
            bgColor = parsedBg;
        }
        using var bgPaint = new SKPaint { Color = bgColor, Style = SKPaintStyle.Fill };
        canvas.DrawRect(Bounds, bgPaint);

        using var borderPaint = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRect(Bounds, borderPaint);

        // Draw slide number header
        using var headerPaint = new SKPaint
        {
            Color = SKColors.DarkGray,
            TextSize = 9,
            IsAntialias = true,
            TextAlign = SKTextAlign.Left
        };
        canvas.DrawText($"Slide {SlideIndex + 1}", Bounds.Left + 10, Bounds.Top + 16, headerPaint);

        // Draw title if available
        var slideNode = Node as SlideNode;
        if (!string.IsNullOrEmpty(slideNode?.Title))
        {
            using var titlePaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 18,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            };
            canvas.DrawText(slideNode.Title, Bounds.MidX, Bounds.Top + 60, titlePaint);
        }

        // Render shapes
        foreach (var shape in Shapes)
        {
            shape.Render(canvas, context);
        }
    }

    public int HitTest(SKPoint point)
    {
        foreach (var shape in Shapes)
        {
            if (shape.Bounds.Contains(point))
            {
                return shape.HitTest(point);
            }
        }
        return -1;
    }

    public SKRect GetCaretBounds(int offset) => default;
    public void GetSelectionBounds(int start, int end, IList<SKRect> result) { }
}

/// <summary>
/// Layout block for a single shape on a slide (text box, rectangle, picture, etc.).
/// </summary>
public class ShapeLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }

    public ShapeLayoutBlock(ShapeNode node)
    {
        Node = node;
    }

    public void Layout(LayoutContext context) { }

    public static SKBitmap? LoadBase64Image(string base64Data)
    {
        try
        {
            int commaIdx = base64Data.IndexOf(',');
            if (commaIdx >= 0)
            {
                base64Data = base64Data.Substring(commaIdx + 1);
            }
            byte[] bytes = Convert.FromBase64String(base64Data);
            return SKBitmap.Decode(bytes);
        }
        catch
        {
            return null;
        }
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        var shapeNode = (ShapeNode)Node;

        // Render picture if base64 data is present
        if (shapeNode.ShapeType == "Picture" && !string.IsNullOrEmpty(shapeNode.ImageSource))
        {
            var bitmap = LoadBase64Image(shapeNode.ImageSource);
            if (bitmap != null)
            {
                canvas.DrawBitmap(bitmap, Bounds);
                return;
            }
        }

        // Render Chart/GraphicFrame
        if (shapeNode.ShapeType == "GraphicFrame" || shapeNode.ShapeType == "Chart")
        {
            using var bg = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
            canvas.DrawRect(Bounds, bg);
            using var border = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            canvas.DrawRect(Bounds, border);

            using var titlePaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 10,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            };
            canvas.DrawText("Chart Series Data", Bounds.MidX, Bounds.Top + 14, titlePaint);

            bool isPie = shapeNode.Text != null && shapeNode.Text.Contains("pie", StringComparison.OrdinalIgnoreCase);

            if (isPie)
            {
                var center = new SKPoint(Bounds.MidX, Bounds.MidY + 5);
                float radius = Math.Min(Bounds.Width, Bounds.Height) * 0.3f;
                var rect = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);

                float[] angles = { 120f, 90f, 150f };
                SKColor[] colors = { SKColors.Red, SKColors.Green, SKColors.Blue };

                float startAngle = 0f;
                for (int i = 0; i < angles.Length; i++)
                {
                    using var slicePaint = new SKPaint { Color = colors[i], Style = SKPaintStyle.Fill };
                    canvas.DrawArc(rect, startAngle, angles[i], true, slicePaint);
                    startAngle += angles[i];
                }
            }
            else
            {
                float barWidth = Bounds.Width / 6;
                float spacing = barWidth / 2;
                float[] values = { 0.4f, 0.8f, 0.6f, 0.9f };
                SKColor[] colors = { SKColors.Red, SKColors.Green, SKColors.Blue, SKColors.Orange };

                float startX = Bounds.Left + spacing;
                float chartBottom = Bounds.Bottom - 10;
                float maxBarHeight = Bounds.Height - 35;

                for (int i = 0; i < values.Length; i++)
                {
                    float barHeight = maxBarHeight * values[i];
                    var barRect = new SKRect(startX, chartBottom - barHeight, startX + barWidth, chartBottom);
                    using var barPaint = new SKPaint { Color = colors[i], Style = SKPaintStyle.Fill };
                    canvas.DrawRect(barRect, barPaint);
                    startX += barWidth + spacing;
                }
            }
            return;
        }

        // Draw shape rectangle
        using var fillPaint = new SKPaint { Color = new SKColor(230, 240, 255), Style = SKPaintStyle.Fill };
        using var strokePaint = new SKPaint { Color = new SKColor(100, 140, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

        if (shapeNode.ShapeType == "Ellipse" || shapeNode.ShapeType == "ellipse")
        {
            canvas.DrawOval(Bounds, fillPaint);
            canvas.DrawOval(Bounds, strokePaint);
        }
        else
        {
            canvas.DrawRect(Bounds, fillPaint);
            canvas.DrawRect(Bounds, strokePaint);
        }

        // Draw text content if available or if being edited
        if (!string.IsNullOrEmpty(shapeNode.Text) || context.EditingShapeNode == Node)
        {
            using var textPaint = new SKPaint
            {
                Color = !string.IsNullOrEmpty(shapeNode.Color) && SKColor.TryParse(shapeNode.Color, out var clr) ? clr : SKColors.Black,
                TextSize = shapeNode.FontSize.HasValue ? (float)shapeNode.FontSize.Value : 11f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            var weight = (shapeNode.Bold ?? false) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = (shapeNode.Italic ?? false) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            textPaint.Typeface = SKTypeface.FromFamilyName("Arial", weight, SKFontStyleWidth.Normal, slant);

            // Center the text vertically and horizontally
            float textY = Bounds.MidY + textPaint.TextSize / 3;
            canvas.Save();
            canvas.ClipRect(Bounds);
            
            string txt = shapeNode.Text ?? "";
            canvas.DrawText(txt, Bounds.MidX, textY, textPaint);

            // Draw selection and caret if this shape is being edited
            if (context.EditingShapeNode == Node)
            {
                float textWidth = textPaint.MeasureText(txt);
                float startX = Bounds.MidX - textWidth / 2;
                
                // 1. Draw Selection
                if (context.SelectionStart != -1 && context.SelectionEnd != -1 && context.SelectionStart != context.SelectionEnd)
                {
                    int selStart = Math.Min(context.SelectionStart, context.SelectionEnd);
                    int selEnd = Math.Max(context.SelectionStart, context.SelectionEnd);
                    
                    selStart = Math.Clamp(selStart, 0, txt.Length);
                    selEnd = Math.Clamp(selEnd, 0, txt.Length);
                    
                    float x1 = startX + textPaint.MeasureText(txt.Substring(0, selStart));
                    float x2 = startX + textPaint.MeasureText(txt.Substring(0, selEnd));
                    
                    using var selPaint = new SKPaint { Color = new SKColor(0, 120, 215, 80), Style = SKPaintStyle.Fill };
                    canvas.DrawRect(new SKRect(x1, Bounds.MidY - textPaint.TextSize / 2, x2, Bounds.MidY + textPaint.TextSize / 2), selPaint);
                }
                
                // 2. Draw Caret
                if (context.DrawCaret)
                {
                    int caret = Math.Clamp(context.CaretOffset, 0, txt.Length);
                    float x = startX + textPaint.MeasureText(txt.Substring(0, caret));
                    using var caretPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                    canvas.DrawLine(x, Bounds.MidY - textPaint.TextSize / 2, x, Bounds.MidY + textPaint.TextSize / 2, caretPaint);
                }
            }

            canvas.Restore();
        }

        // Draw shape type label in corner
        using var labelPaint = new SKPaint
        {
            Color = new SKColor(120, 120, 120),
            TextSize = 7,
            IsAntialias = true,
            TextAlign = SKTextAlign.Left
        };
        canvas.DrawText(shapeNode.ShapeType ?? "Shape", Bounds.Left + 2, Bounds.Top + 8, labelPaint);
    }

    public int HitTest(SKPoint point) => -1;
    public SKRect GetCaretBounds(int offset) => default;
    public void GetSelectionBounds(int start, int end, IList<SKRect> result) { }
}

/// <summary>
/// Lays out a PresentationDocument AST into slide layout blocks for SkiaSharp rendering.
/// </summary>
public static class PresentationLayoutManager
{
    // Standard slide dimensions (10 in × 7.5 in at 72 dpi)
    private const float SlideWidth = 720f;
    private const float SlideHeight = 540f;
    private const float SlideSpacing = 20f;

    // Scale factor to convert from EMU-based point units to slide-relative coordinates
    private const float CoordinateScale = 1f;

    public static PresentationDocumentLayoutBlock Layout(PresentationDocument doc)
    {
        var docBlock = new PresentationDocumentLayoutBlock(doc);
        float yOffset = 0;

        foreach (var child in doc.Children)
        {
            if (child is SlideNode slideNode)
            {
                var slideBlock = new SlideLayoutBlock(slideNode)
                {
                    SlideIndex = slideNode.SlideIndex,
                    Bounds = new SKRect(0, yOffset, SlideWidth, yOffset + SlideHeight)
                };

                // Layout shapes within the slide
                // Prepend master shapes first
                var master = doc.Masters.Find(m => m.Name == slideNode.MasterName) ?? (doc.Masters.Count > 0 ? doc.Masters[0] : null);
                if (master != null)
                {
                    slideBlock.BackgroundColor = master.BackgroundColor;
                    foreach (var masterShape in master.Children)
                    {
                        if (masterShape is ShapeNode shapeNode)
                        {
                            float sx = (float)(shapeNode.X ?? 10) * CoordinateScale;
                            float sy = (float)(shapeNode.Y ?? 10) * CoordinateScale;
                            float sw = (float)(shapeNode.Width ?? 100) * CoordinateScale;
                            float sh = (float)(shapeNode.Height ?? 40) * CoordinateScale;

                            var shapeBlock = new ShapeLayoutBlock(shapeNode)
                            {
                                Bounds = new SKRect(
                                    slideBlock.Bounds.Left + sx,
                                    slideBlock.Bounds.Top + sy,
                                    slideBlock.Bounds.Left + sx + sw,
                                    slideBlock.Bounds.Top + sy + sh
                                )
                            };
                            slideBlock.Shapes.Add(shapeBlock);
                        }
                    }
                }

                foreach (var shapeChild in slideNode.Children)
                {
                    if (shapeChild is ShapeNode shapeNode)
                    {
                        float sx = (float)(shapeNode.X ?? 10) * CoordinateScale;
                        float sy = (float)(shapeNode.Y ?? 10) * CoordinateScale;
                        float sw = (float)(shapeNode.Width ?? 100) * CoordinateScale;
                        float sh = (float)(shapeNode.Height ?? 40) * CoordinateScale;

                        // Clamp to slide bounds
                        sx = Math.Max(0, Math.Min(sx, SlideWidth - 10));
                        sy = Math.Max(0, Math.Min(sy, SlideHeight - 10));
                        sw = Math.Min(sw, SlideWidth - sx);
                        sh = Math.Min(sh, SlideHeight - sy);

                        var shapeBlock = new ShapeLayoutBlock(shapeNode)
                        {
                            Bounds = new SKRect(
                                slideBlock.Bounds.Left + sx,
                                slideBlock.Bounds.Top + sy,
                                slideBlock.Bounds.Left + sx + sw,
                                slideBlock.Bounds.Top + sy + sh
                            )
                        };
                        slideBlock.Shapes.Add(shapeBlock);
                    }
                    else if (shapeChild is GroupNode groupNode)
                    {
                        // Flatten group children into the slide
                        LayoutGroupShapes(groupNode, slideBlock, slideBlock.Bounds.Left, slideBlock.Bounds.Top);
                    }
                }

                docBlock.Slides.Add(slideBlock);
                yOffset += SlideHeight + SlideSpacing;
            }
        }

        docBlock.Bounds = new SKRect(0, 0, SlideWidth, Math.Max(yOffset - SlideSpacing, SlideHeight));
        return docBlock;
    }

    private static void LayoutGroupShapes(GroupNode group, SlideLayoutBlock slideBlock, float parentX, float parentY)
    {
        float gx = (float)(group.X ?? 0) * CoordinateScale;
        float gy = (float)(group.Y ?? 0) * CoordinateScale;

        foreach (var child in group.Children)
        {
            if (child is ShapeNode shapeNode)
            {
                float sx = (float)(shapeNode.X ?? 0) * CoordinateScale + gx;
                float sy = (float)(shapeNode.Y ?? 0) * CoordinateScale + gy;
                float sw = (float)(shapeNode.Width ?? 80) * CoordinateScale;
                float sh = (float)(shapeNode.Height ?? 30) * CoordinateScale;

                var shapeBlock = new ShapeLayoutBlock(shapeNode)
                {
                    Bounds = new SKRect(
                        parentX + sx,
                        parentY + sy,
                        parentX + sx + sw,
                        parentY + sy + sh
                    )
                };
                slideBlock.Shapes.Add(shapeBlock);
            }
            else if (child is GroupNode nestedGroup)
            {
                LayoutGroupShapes(nestedGroup, slideBlock, parentX + gx, parentY + gy);
            }
        }
    }
}
