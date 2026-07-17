namespace CDP.Document.Renderer;

/// <summary>
/// Provides page and margin dimensions for layout computation.
/// </summary>
public class LayoutContext
{
    /// <summary>US Letter width in points (8.5 in × 72 dpi).</summary>
    public float PageWidth { get; set; } = 612f;

    /// <summary>US Letter height in points (11 in × 72 dpi).</summary>
    public float PageHeight { get; set; } = 792f;

    /// <summary>Left margin in points (0.75 in).</summary>
    public float MarginLeft { get; set; } = 54f;

    /// <summary>Top margin in points (0.75 in).</summary>
    public float MarginTop { get; set; } = 54f;

    /// <summary>Right margin in points (0.75 in).</summary>
    public float MarginRight { get; set; } = 54f;

    /// <summary>Bottom margin in points (0.75 in).</summary>
    public float MarginBottom { get; set; } = 54f;

    /// <summary>Usable content width inside margins.</summary>
    public float ContentWidth => PageWidth - MarginLeft - MarginRight;

    /// <summary>Usable content height inside margins.</summary>
    public float ContentHeight => PageHeight - MarginTop - MarginBottom;

    /// <summary>Current vertical position within the current page.</summary>
    public float CurrentY { get; set; }

    /// <summary>Current page index (zero-based).</summary>
    public int CurrentPageIndex { get; set; }
}
