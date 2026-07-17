namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public class TableLayoutBlock : ILayoutBlock
{
    public TableBlock Node { get; }
    MarkdownBlock ILayoutBlock.Node => Node;
    public SKRect Bounds { get; private set; }
    public List<ILayoutBlock> Rows { get; } = new();

    public TableLayoutBlock(TableBlock node)
    {
        Node = node;
        foreach (var child in node.Children)
        {
            if (child is MarkdownBlock block)
            {
                Rows.Add(LayoutBlockFactory.Create(block));
            }
        }
    }

    public void Layout(LayoutContext context)
    {
        float startY = context.CurrentY;
        float currentY = startY;

        foreach (var row in Rows)
        {
            row.Layout(new LayoutContext(context.MaxWidth, context.Measurer, context.Resources, currentY, context.MarkdownText));
            currentY = row.Bounds.Bottom;
        }

        Bounds = new SKRect(0, startY, context.MaxWidth, currentY);
        context.CurrentY = currentY;
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        foreach (var row in Rows)
        {
            if (row.Bounds.Bottom >= context.Viewport.Top && row.Bounds.Top <= context.Viewport.Bottom)
            {
                row.Render(canvas, context);
            }
        }
    }

    public int HitTest(SKPoint docPoint)
    {
        if (Rows.Count == 0) return Node.Span.Start;
        float y = docPoint.Y;

        if (y < Rows[0].Bounds.Top) return Rows[0].HitTest(docPoint);
        if (y >= Rows[^1].Bounds.Bottom) return Rows[^1].HitTest(docPoint);

        foreach (var row in Rows)
        {
            if (y >= row.Bounds.Top && y < row.Bounds.Bottom)
            {
                return row.HitTest(docPoint);
            }
        }
        return Rows[^1].HitTest(docPoint);
    }

    public SKRect GetCaretBounds(int offset)
    {
        foreach (var row in Rows)
        {
            int start = row.Node.Span.Start;
            int end = row.Node.Span.Start + row.Node.Span.Length;
            if (offset >= start && offset <= end)
            {
                return row.GetCaretBounds(offset);
            }
        }
        if (Rows.Count > 0)
        {
            return Rows[^1].GetCaretBounds(offset);
        }
        return new SKRect(Bounds.Left, Bounds.Top, Bounds.Left + 2f, Bounds.Top + 20f);
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        foreach (var row in Rows)
        {
            row.GetSelectionBounds(start, end, result);
        }
    }

    public void Dispose()
    {
        foreach (var row in Rows)
        {
            row.Dispose();
        }
        Rows.Clear();
    }
}
