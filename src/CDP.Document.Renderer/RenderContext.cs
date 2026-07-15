namespace CDP.Document.Renderer;

/// <summary>
/// Provides rendering state such as caret offset and selection range for interactive document rendering.
/// </summary>
public class RenderContext
{
    /// <summary>Whether to draw the caret.</summary>
    public bool DrawCaret { get; set; }

    /// <summary>Global character offset of the caret position.</summary>
    public int CaretOffset { get; set; }

    /// <summary>Global character offset for the start of selection (-1 = no selection).</summary>
    public int SelectionStart { get; set; } = -1;

    /// <summary>Global character offset for the end of selection (-1 = no selection).</summary>
    public int SelectionEnd { get; set; } = -1;

    /// <summary>Visible viewport boundary in layout coordinates.</summary>
    public SkiaSharp.SKRect Viewport { get; set; } = SkiaSharp.SKRect.Empty;

    /// <summary>Node of the cell currently being edited.</summary>
    public object? EditingCellNode { get; set; }

    /// <summary>Node of the shape currently being edited.</summary>
    public object? EditingShapeNode { get; set; }
}
