namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public class QuoteLayoutBlock : ILayoutBlock
{
    public QuoteBlock Node { get; }
    MarkdownBlock ILayoutBlock.Node => Node;
    public SKRect Bounds { get; private set; }
    public List<ILayoutBlock> InnerBlocks { get; } = new();

    public QuoteLayoutBlock(QuoteBlock node)
    {
        Node = node;
        foreach (var child in node.Children)
        {
            if (child is MarkdownBlock childBlock)
            {
                InnerBlocks.Add(LayoutBlockFactory.Create(childBlock));
            }
        }
    }

    public void Layout(LayoutContext context)
    {
        float startY = context.CurrentY;
        float indent = 20.0f;
        float childWidth = context.MaxWidth - indent;
        float currentY = startY;

        foreach (var block in InnerBlocks)
        {
            var itemContext = new LayoutContext(childWidth, context.Measurer, context.Resources, currentY);
            block.Layout(itemContext);
            currentY = itemContext.CurrentY;
        }

        // Add quote spacing margin bottom (10px)
        currentY += 10.0f;

        Bounds = new SKRect(0, startY, context.MaxWidth, currentY);
        context.CurrentY = currentY;
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        float barX = Bounds.Left + 4.0f;
        float barWidth = 4.0f;
        var barRect = new SKRect(barX, Bounds.Top, barX + barWidth, Bounds.Bottom - 10.0f);
        canvas.DrawRect(barRect, context.Resources.QuoteBarPaint);

        canvas.Save();
        canvas.Translate(20.0f, 0);

        foreach (var block in InnerBlocks)
        {
            if (block.Bounds.Bottom >= context.Viewport.Top && block.Bounds.Top <= context.Viewport.Bottom)
            {
                block.Render(canvas, context);
            }
        }

        canvas.Restore();
    }

    public int HitTest(SKPoint docPoint)
    {
        if (InnerBlocks.Count == 0) return Node.Span.Start;
        
        var shiftedPoint = new SKPoint(docPoint.X - 20.0f, docPoint.Y);
        float y = shiftedPoint.Y;
        
        if (y < InnerBlocks[0].Bounds.Top)
        {
            return InnerBlocks[0].HitTest(shiftedPoint);
        }
        if (y >= InnerBlocks[^1].Bounds.Bottom)
        {
            return InnerBlocks[^1].HitTest(shiftedPoint);
        }
        
        foreach (var block in InnerBlocks)
        {
            if (y >= block.Bounds.Top && y < block.Bounds.Bottom)
            {
                return block.HitTest(shiftedPoint);
            }
        }
        
        return InnerBlocks[^1].HitTest(shiftedPoint);
    }

    public SKRect GetCaretBounds(int offset)
    {
        foreach (var block in InnerBlocks)
        {
            int start = block.Node.Span.Start;
            int end = block.Node.Span.Start + block.Node.Span.Length;
            if (offset >= start && offset <= end)
            {
                var rect = block.GetCaretBounds(offset);
                return new SKRect(rect.Left + 20.0f, rect.Top, rect.Right + 20.0f, rect.Bottom);
            }
        }
        
        if (InnerBlocks.Count > 0)
        {
            var rect = InnerBlocks[^1].GetCaretBounds(offset);
            return new SKRect(rect.Left + 20.0f, rect.Top, rect.Right + 20.0f, rect.Bottom);
        }
        
        return new SKRect(Bounds.Left + 20f, Bounds.Top, Bounds.Left + 22f, Bounds.Top + 20f);
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        int beforeCount = result.Count;
        foreach (var block in InnerBlocks)
        {
            block.GetSelectionBounds(start, end, result);
        }
        for (int i = beforeCount; i < result.Count; i++)
        {
            var rect = result[i];
            result[i] = new SKRect(rect.Left + 20.0f, rect.Top, rect.Right + 20.0f, rect.Bottom);
        }
    }

    public void Dispose()
    {
        foreach (var block in InnerBlocks)
        {
            block.Dispose();
        }
        InnerBlocks.Clear();
    }
}
