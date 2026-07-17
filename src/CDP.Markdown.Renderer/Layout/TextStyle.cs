namespace CDP.Markdown.Renderer.Layout;

using SkiaSharp;

public struct TextStyle
{
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }
    public bool IsStrikeThrough { get; set; }
    public bool IsMonospace { get; set; }
    public string? LinkUrl { get; set; }
    public int HeadingLevel { get; set; }
    public SKColor? ColorOverride { get; set; }
}
