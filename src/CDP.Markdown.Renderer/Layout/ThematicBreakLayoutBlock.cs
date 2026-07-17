namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public class ThematicBreakLayoutBlock : ILayoutBlock
{
    public ThematicBreakBlock Node { get; }
    MarkdownBlock ILayoutBlock.Node => Node;
    public SKRect Bounds { get; private set; }

    public ThematicBreakLayoutBlock(ThematicBreakBlock node)
    {
        Node = node;
    }

    public void Layout(LayoutContext context)
    {
        float startY = context.CurrentY;
        float height = 16.0f;
        Bounds = new SKRect(0, startY, context.MaxWidth, startY + height);
        context.CurrentY = startY + height;
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        float centerY = Bounds.Top + Bounds.Height / 2.0f;
        canvas.DrawLine(Bounds.Left, centerY, Bounds.Right, centerY, context.Resources.ThematicBreakPaint);
    }

    public int HitTest(SKPoint docPoint)
    {
        return Node.Span.Start;
    }

    public SKRect GetCaretBounds(int offset)
    {
        return new SKRect(Bounds.Left, Bounds.Top, Bounds.Left + 2f, Bounds.Bottom);
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        int blockStart = Node.Span.Start;
        int blockEnd = Node.Span.Start + Node.Span.Length;
        if (Math.Max(start, blockStart) < Math.Min(end, blockEnd))
        {
            result.Add(Bounds);
        }
    }

    public void Dispose()
    {
    }
}
