using System.Collections.Generic;
using SkiaSharp;
using CDP.Document.Parser.AST;

namespace CDP.Document.Renderer;

/// <summary>
/// Represents a laid-out block of document content that can be rendered onto a SkiaSharp canvas.
/// </summary>
public interface IDocumentLayoutBlock
{
    DocumentNode Node { get; }
    SKRect Bounds { get; set; }
    void Layout(LayoutContext context);
    void Render(SKCanvas canvas, RenderContext context);
    int HitTest(SKPoint point);
    SKRect GetCaretBounds(int offset);
    void GetSelectionBounds(int start, int end, IList<SKRect> result);
}
