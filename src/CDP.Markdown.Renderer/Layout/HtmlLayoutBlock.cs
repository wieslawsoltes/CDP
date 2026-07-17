namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;
using CDP.Html.Parser;
using CDP.Css.Parser;
using CDP.Html.Renderer;
using CDP.Html.Renderer.Layout;
using CDP.Html.Renderer.Style;

public class HtmlLayoutBlock : ILayoutBlock
{
    public HtmlBlock Node { get; }
    MarkdownBlock ILayoutBlock.Node => Node;
    public SKRect Bounds { get; private set; }

    private LayoutBox? _rootBox;
    private const float MarginBottom = 10.0f;

    public HtmlLayoutBlock(HtmlBlock node)
    {
        Node = node;
    }

    public void Layout(LayoutContext context)
    {
        float startY = context.CurrentY;

        var doc = HtmlParser.Parse(Node.Html ?? string.Empty);
        var stylesheet = CssParser.Parse(string.Empty);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        _rootBox = LayoutTreeBuilder.Build(doc, styles);

        LayoutEngine.Layout(_rootBox, context.MaxWidth, float.PositiveInfinity);

        float blockHeight = _rootBox.Height + MarginBottom;
        Bounds = new SKRect(0, startY, context.MaxWidth, startY + blockHeight);
        context.CurrentY = startY + blockHeight;
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        if (_rootBox == null) return;
        HtmlRenderer.Render(_rootBox, canvas, Bounds.Left, Bounds.Top);
    }

    public int HitTest(SKPoint docPoint)
    {
        return Node.Span.Start;
    }

    public SKRect GetCaretBounds(int offset)
    {
        return new SKRect(Bounds.Left, Bounds.Top, Bounds.Left + 2f, Bounds.Bottom - MarginBottom);
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        int blockStart = Node.Span.Start;
        int blockEnd = Node.Span.Start + Node.Span.Length;
        if (Math.Max(start, blockStart) < Math.Min(end, blockEnd))
        {
            result.Add(new SKRect(Bounds.Left, Bounds.Top, Bounds.Right, Bounds.Bottom - MarginBottom));
        }
    }

    public void Dispose()
    {
    }
}
