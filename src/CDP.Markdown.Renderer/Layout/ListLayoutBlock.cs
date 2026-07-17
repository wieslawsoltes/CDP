namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public class ListLayoutBlock : ILayoutBlock
{
    public ListBlock Node { get; }
    MarkdownBlock ILayoutBlock.Node => Node;
    public SKRect Bounds { get; private set; }
    public List<ListItemLayoutBlock> Items { get; } = new();

    public ListLayoutBlock(ListBlock node)
    {
        Node = node;
        foreach (var child in node.Children)
        {
            if (child is ListItemBlock listItem)
            {
                Items.Add(new ListItemLayoutBlock(listItem));
            }
        }
    }

    public void Layout(LayoutContext context)
    {
        float startY = context.CurrentY;
        int listIndex = Node.StartIndex;

        foreach (var item in Items)
        {
            item.Index = listIndex++;
            item.IsOrdered = Node.IsOrdered;
            item.BulletChar = Node.BulletChar;
            item.Layout(context);
        }

        Bounds = new SKRect(0, startY, context.MaxWidth, context.CurrentY);
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        foreach (var item in Items)
        {
            if (item.Bounds.Bottom >= context.Viewport.Top && item.Bounds.Top <= context.Viewport.Bottom)
            {
                item.Render(canvas, context);
            }
        }
    }

    public int HitTest(SKPoint docPoint)
    {
        if (Items.Count == 0) return Node.Span.Start;

        float y = docPoint.Y;

        if (y < Items[0].Bounds.Top) return Items[0].HitTest(docPoint);
        if (y >= Items[^1].Bounds.Bottom) return Items[^1].HitTest(docPoint);

        foreach (var item in Items)
        {
            if (y >= item.Bounds.Top && y < item.Bounds.Bottom)
            {
                return item.HitTest(docPoint);
            }
        }

        return Items[^1].HitTest(docPoint);
    }

    public SKRect GetCaretBounds(int offset)
    {
        foreach (var item in Items)
        {
            int start = item.Node.Span.Start;
            int end = item.Node.Span.Start + item.Node.Span.Length;
            if (offset >= start && offset <= end)
            {
                return item.GetCaretBounds(offset);
            }
        }

        if (Items.Count > 0)
        {
            return Items[^1].GetCaretBounds(offset);
        }

        return new SKRect(Bounds.Left, Bounds.Top, Bounds.Left + 2f, Bounds.Top + 20f);
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        foreach (var item in Items)
        {
            item.GetSelectionBounds(start, end, result);
        }
    }

    public void Dispose()
    {
        foreach (var item in Items)
        {
            item.Dispose();
        }
        Items.Clear();
    }
}
