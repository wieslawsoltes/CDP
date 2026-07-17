namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public class ListItemLayoutBlock : ILayoutBlock
{
    public ListItemBlock Node { get; }
    MarkdownBlock ILayoutBlock.Node => Node;
    public SKRect Bounds { get; private set; }
    public List<ILayoutBlock> InnerBlocks { get; } = new();

    public int Index { get; set; }
    public bool IsOrdered { get; set; }
    public string BulletChar { get; set; } = "-";
    
    private const float IndentWidth = 28.0f;

    public ListItemLayoutBlock(ListItemBlock node)
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

    private int GetListDepth()
    {
        int depth = 0;
        var parent = Node.Parent;
        while (parent != null)
        {
            if (parent is ListBlock)
            {
                depth++;
            }
            parent = parent.Parent;
        }
        return Math.Max(0, depth - 1);
    }

    public void Layout(LayoutContext context)
    {
        float startY = context.CurrentY;
        float childWidth = context.MaxWidth - IndentWidth;
        float currentY = startY;

        foreach (var block in InnerBlocks)
        {
            var itemContext = new LayoutContext(childWidth, context.Measurer, context.Resources, currentY);
            block.Layout(itemContext);
            currentY = itemContext.CurrentY;
        }

        Bounds = new SKRect(0, startY, context.MaxWidth, currentY);
        context.CurrentY = currentY;
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        float markerX = Bounds.Left + 8.0f;
        float markerY = Bounds.Top + 14.0f;

        if (Node.IsChecked.HasValue)
        {
            float boxSize = 14f;
            float boxX = Bounds.Left + 8f;
            float boxY = Bounds.Top + 4f;
            var checkboxRect = new SKRoundRect(new SKRect(boxX, boxY, boxX + boxSize, boxY + boxSize), 2f, 2f);

            if (Node.IsChecked.Value)
            {
                canvas.DrawRoundRect(checkboxRect, context.Resources.CheckboxFillPaint);
                canvas.DrawLine(boxX + 3.0f, boxY + 7.0f, boxX + 6.0f, boxY + 10.0f, context.Resources.CheckboxCheckPaint);
                canvas.DrawLine(boxX + 6.0f, boxY + 10.0f, boxX + 11.0f, boxY + 4.0f, context.Resources.CheckboxCheckPaint);
            }
            else
            {
                canvas.DrawRoundRect(checkboxRect, context.Resources.CheckboxBorderPaint);
            }
        }
        else if (IsOrdered)
        {
            string label = $"{Index}.";
            canvas.DrawText(label, markerX, markerY, context.Resources.TextFont, context.Resources.TextPaint);
        }
        else
        {
            int depth = GetListDepth();
            if (depth % 3 == 0)
            {
                canvas.DrawCircle(markerX + 4.0f, Bounds.Top + 10.0f, 3.0f, context.Resources.BulletPaint);
            }
            else if (depth % 3 == 1)
            {
                canvas.DrawCircle(markerX + 4.0f, Bounds.Top + 10.0f, 3.0f, context.Resources.BulletOpenPaint);
            }
            else
            {
                canvas.DrawRect(markerX + 1.0f, Bounds.Top + 7.0f, markerX + 7.0f, Bounds.Top + 13.0f, context.Resources.BulletPaint);
            }
        }

        canvas.Save();
        canvas.Translate(IndentWidth, 0);

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

        var shiftedPoint = new SKPoint(docPoint.X - IndentWidth, docPoint.Y);
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
                return new SKRect(rect.Left + IndentWidth, rect.Top, rect.Right + IndentWidth, rect.Bottom);
            }
        }

        if (InnerBlocks.Count > 0)
        {
            var rect = InnerBlocks[^1].GetCaretBounds(offset);
            return new SKRect(rect.Left + IndentWidth, rect.Top, rect.Right + IndentWidth, rect.Bottom);
        }

        return new SKRect(Bounds.Left + IndentWidth, Bounds.Top, Bounds.Left + IndentWidth + 2f, Bounds.Top + 20f);
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
            result[i] = new SKRect(rect.Left + IndentWidth, rect.Top, rect.Right + IndentWidth, rect.Bottom);
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
