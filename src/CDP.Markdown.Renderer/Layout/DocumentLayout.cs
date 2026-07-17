namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public class DocumentLayout : IDisposable
{
    public List<ILayoutBlock> Blocks { get; } = new();
    public SKRect Bounds { get; private set; }

    public void LoadDocument(MarkdownDocument document)
    {
        foreach (var block in Blocks)
        {
            block.Dispose();
        }
        Blocks.Clear();
        foreach (var child in document.Children)
        {
            if (child is MarkdownBlock block)
            {
                Blocks.Add(LayoutBlockFactory.Create(block));
            }
        }
    }

    public void Layout(LayoutContext context)
    {
        context.CurrentY = 0;
        float maxWidth = context.MaxWidth;

        foreach (var block in Blocks)
        {
            block.Layout(context);
        }

        Bounds = new SKRect(0, 0, maxWidth, context.CurrentY);
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        var viewport = context.Viewport;

        int firstIndex = FindFirstVisibleBlockIndex(viewport.Top);
        if (firstIndex < 0 || firstIndex >= Blocks.Count)
        {
            return;
        }

        for (int i = firstIndex; i < Blocks.Count; i++)
        {
            var block = Blocks[i];

            if (block.Bounds.Top > viewport.Bottom)
            {
                break;
            }

            if (block.Bounds.Bottom >= viewport.Top)
            {
                block.Render(canvas, context);
            }
        }
    }

    private int FindFirstVisibleBlockIndex(float viewportTop)
    {
        if (Blocks.Count == 0) return -1;
        int low = 0;
        int high = Blocks.Count - 1;
        int result = -1;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            var block = Blocks[mid];

            if (block.Bounds.Bottom >= viewportTop)
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return result;
    }

    public int HitTest(SKPoint localPoint)
    {
        if (Blocks.Count == 0) return 0;
        
        float y = localPoint.Y;
        
        if (y < Blocks[0].Bounds.Top) return Blocks[0].HitTest(localPoint);
        if (y >= Blocks[^1].Bounds.Bottom) return Blocks[^1].HitTest(localPoint);
        
        foreach (var block in Blocks)
        {
            if (y >= block.Bounds.Top && y < block.Bounds.Bottom)
            {
                return block.HitTest(localPoint);
            }
        }
        
        return Blocks[^1].HitTest(localPoint);
    }

    public SKRect GetCaretBounds(int offset)
    {
        foreach (var block in Blocks)
        {
            int start = block.Node.Span.Start;
            int end = block.Node.Span.Start + block.Node.Span.Length;
            if (offset >= start && offset <= end)
            {
                return block.GetCaretBounds(offset);
            }
        }
        
        if (Blocks.Count > 0)
        {
            return Blocks[^1].GetCaretBounds(offset);
        }
        
        return new SKRect(0, 0, 2f, 20f);
    }

    public IList<SKRect> GetSelectionBounds(int start, int end)
    {
        var result = new List<SKRect>();
        if (start >= end) return result;

        foreach (var block in Blocks)
        {
            block.GetSelectionBounds(start, end, result);
        }

        return result;
    }

    public void Dispose()
    {
        foreach (var block in Blocks)
        {
            block.Dispose();
        }
        Blocks.Clear();
    }
}
