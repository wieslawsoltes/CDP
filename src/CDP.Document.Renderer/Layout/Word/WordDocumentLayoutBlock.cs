using System.Collections.Generic;
using SkiaSharp;
using CDP.Document.Parser.AST;
using CDP.Document.Renderer.Layout.Word;

namespace CDP.Document.Renderer.Layout.Word;

/// <summary>
/// Top-level layout block for an entire Word document, containing paginated pages.
/// </summary>
public class WordDocumentLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }
    public List<PageLayoutBlock> Pages { get; } = new();

    public WordDocumentLayoutBlock(WordDocument doc)
    {
        Node = doc;
    }

    public void Layout(LayoutContext context)
    {
        // Layout is driven by WordLayoutManager.Layout()
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        foreach (var page in Pages)
        {
            page.Render(canvas, context);
        }
    }

    public int HitTest(SKPoint point)
    {
        foreach (var page in Pages)
        {
            if (page.Bounds.Contains(point))
            {
                return page.HitTest(point);
            }
        }
        return -1;
    }

    public SKRect GetCaretBounds(int offset)
    {
        foreach (var page in Pages)
        {
            var rect = page.GetCaretBounds(offset);
            if (rect != default) return rect;
        }
        return default;
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        foreach (var page in Pages)
        {
            page.GetSelectionBounds(start, end, result);
        }
    }
}
