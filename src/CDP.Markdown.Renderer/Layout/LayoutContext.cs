namespace CDP.Markdown.Renderer.Layout;

using CDP.Markdown.Renderer.Rendering;

public class LayoutContext
{
    public float MaxWidth { get; }
    public ITextMeasurer Measurer { get; }
    public RenderResources? Resources { get; }
    public float CurrentY { get; set; }
    public float CurrentX { get; set; }

    public string? MarkdownText { get; }

    public LayoutContext(float maxWidth, ITextMeasurer measurer, RenderResources? resources = null, float startY = 0, string? markdownText = null, float startX = 0)
    {
        MaxWidth = maxWidth;
        Measurer = measurer;
        Resources = resources;
        CurrentY = startY;
        CurrentX = startX;
        MarkdownText = markdownText;
    }
}
