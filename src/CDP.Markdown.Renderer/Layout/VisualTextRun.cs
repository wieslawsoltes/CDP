namespace CDP.Markdown.Renderer.Layout;

using SkiaSharp;
using CDP.Markdown.Parser.AST;

using System;

public class VisualTextRun : IDisposable
{
    public string Text { get; }
    public SKTextBlob? TextBlob { get; }
    public SKRect LocalBounds { get; }
    public SKPaint Paint { get; }
    public SourceSpan Span { get; }
    public TextStyle Style { get; }
    public float[] CharacterPositions { get; }
    
    public bool IsImage { get; }
    public string? ImageUrl { get; }
    public string? AltText { get; }

    public VisualTextRun(
        string text,
        SKTextBlob? textBlob,
        SKRect localBounds,
        SKPaint paint,
        SourceSpan span,
        TextStyle style,
        float[] characterPositions,
        bool isImage = false,
        string? imageUrl = null,
        string? altText = null)
    {
        Text = text;
        TextBlob = textBlob;
        LocalBounds = localBounds;
        Paint = paint;
        Span = span;
        Style = style;
        CharacterPositions = characterPositions;
        IsImage = isImage;
        ImageUrl = imageUrl;
        AltText = altText;
    }

    public void Dispose()
    {
        TextBlob?.Dispose();
    }
}
