using System;
using SkiaSharp;
using CDP.Document.Parser.AST;
using CDP.Document.Renderer.Layout.Word;
using CDP.Document.Renderer.Layout.Presentation;
using CDP.Document.Renderer.Layout.Spreadsheet;

namespace CDP.Document.Renderer;

/// <summary>
/// Main entry point for rendering document ASTs to SkiaSharp canvases.
/// Takes a parsed DocumentRoot and produces a layout block tree that can be rendered.
/// </summary>
public class DocumentRenderer
{
    private IDocumentLayoutBlock? _layoutBlock;
    private LayoutContext _layoutContext = new();

    /// <summary>
    /// Gets the current layout block after a successful layout pass.
    /// </summary>
    public IDocumentLayoutBlock? LayoutBlock => _layoutBlock;

    /// <summary>
    /// Gets the total document bounds after layout.
    /// </summary>
    public SKRect DocumentBounds => _layoutBlock?.Bounds ?? SKRect.Empty;

    /// <summary>
    /// Performs layout on the document AST and caches the result.
    /// </summary>
    public IDocumentLayoutBlock Layout(DocumentRoot document)
    {
        _layoutContext = new LayoutContext();
        _layoutBlock = LayoutDocument(document, _layoutContext);
        return _layoutBlock;
    }

    /// <summary>
    /// Performs layout with custom context settings.
    /// </summary>
    public IDocumentLayoutBlock Layout(DocumentRoot document, LayoutContext context)
    {
        _layoutContext = context;
        _layoutBlock = LayoutDocument(document, context);
        return _layoutBlock;
    }

    /// <summary>
    /// Renders the previously laid-out document onto the given canvas.
    /// </summary>
    public void Render(SKCanvas canvas, RenderContext context)
    {
        if (_layoutBlock == null) return;
        _layoutBlock.Render(canvas, context);
    }

    /// <summary>
    /// Renders the previously laid-out document onto the given canvas with default render context.
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        Render(canvas, new RenderContext());
    }

    /// <summary>
    /// Performs hit testing at the given point, returning the character offset or -1.
    /// </summary>
    public int HitTest(SKPoint point)
    {
        if (_layoutBlock == null) return -1;
        return _layoutBlock.HitTest(point);
    }

    /// <summary>
    /// Gets the caret bounds for the given character offset.
    /// </summary>
    public SKRect GetCaretBounds(int offset)
    {
        if (_layoutBlock == null) return default;
        return _layoutBlock.GetCaretBounds(offset);
    }

    /// <summary>
    /// Layouts a document based on its concrete type.
    /// </summary>
    private static IDocumentLayoutBlock LayoutDocument(DocumentRoot document, LayoutContext context)
    {
        if (document is WordDocument wordDoc)
        {
            return WordLayoutManager.Layout(wordDoc, context);
        }
        else if (document is PresentationDocument presDoc)
        {
            return PresentationLayoutManager.Layout(presDoc);
        }
        else if (document is SpreadsheetDocument spreadDoc)
        {
            return SpreadsheetLayoutManager.Layout(spreadDoc);
        }
        else
        {
            throw new NotSupportedException($"Document type '{document.GetType().Name}' is not supported for rendering.");
        }
    }
}
