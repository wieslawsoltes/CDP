namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public interface ILayoutBlock : IDisposable
{
    MarkdownBlock Node { get; }
    SKRect Bounds { get; }
    void Layout(LayoutContext context);
    void Render(SKCanvas canvas, RenderContext context);
    
    int HitTest(SKPoint docPoint);
    SKRect GetCaretBounds(int offset);
    void GetSelectionBounds(int start, int end, IList<SKRect> result);
}
