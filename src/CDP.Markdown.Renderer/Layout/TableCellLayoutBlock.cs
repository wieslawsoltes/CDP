namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public class TableCellLayoutBlock : ILayoutBlock
{
    public TableCellBlock Node { get; }
    MarkdownBlock ILayoutBlock.Node => Node;
    public SKRect Bounds { get; private set; }
    public List<ILayoutBlock> InnerBlocks { get; } = new();

    public TableCellLayoutBlock(TableCellBlock node)
    {
        Node = node;
        foreach (var child in node.Children)
        {
            if (child is MarkdownBlock block)
            {
                InnerBlocks.Add(LayoutBlockFactory.Create(block));
            }
        }
    }

    public void Layout(LayoutContext context)
    {
        float startX = context.CurrentX;
        float startY = context.CurrentY;
        float currentY = startY;

        foreach (var block in InnerBlocks)
        {
            var itemContext = new LayoutContext(context.MaxWidth, context.Measurer, context.Resources, currentY);
            block.Layout(itemContext);
            currentY = itemContext.CurrentY;
        }

        Bounds = new SKRect(startX, startY, startX + context.MaxWidth, currentY);
        context.CurrentY = currentY;
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        canvas.Save();
        canvas.Translate(Bounds.Left, 0);

        foreach (var block in InnerBlocks)
        {
            block.Render(canvas, context);
        }

        canvas.Restore();
        canvas.DrawRect(Bounds, context.Resources.HeadingBorderPaint);
    }

    public int HitTest(SKPoint docPoint)
    {
        if (InnerBlocks.Count == 0) return Node.Span.Start;
        var shiftedPoint = new SKPoint(docPoint.X - Bounds.Left, docPoint.Y);
        float y = shiftedPoint.Y;

        if (y < InnerBlocks[0].Bounds.Top) return InnerBlocks[0].HitTest(shiftedPoint);
        if (y >= InnerBlocks[^1].Bounds.Bottom) return InnerBlocks[^1].HitTest(shiftedPoint);

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
                return new SKRect(rect.Left + Bounds.Left, rect.Top, rect.Right + Bounds.Left, rect.Bottom);
            }
        }
        if (InnerBlocks.Count > 0)
        {
            var rect = InnerBlocks[^1].GetCaretBounds(offset);
            return new SKRect(rect.Left + Bounds.Left, rect.Top, rect.Right + Bounds.Left, rect.Bottom);
        }
        return new SKRect(Bounds.Left, Bounds.Top, Bounds.Left + 2f, Bounds.Top + 20f);
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
            result[i] = new SKRect(rect.Left + Bounds.Left, rect.Top, rect.Right + Bounds.Left, rect.Bottom);
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
