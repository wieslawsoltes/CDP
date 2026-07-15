namespace CDP.Markdown.Renderer.Rendering;

using System;
using SkiaSharp;

public class RenderResources : IDisposable
{
    public SKTypeface SansTypeface { get; }
    public SKTypeface MonospaceTypeface { get; }
    private readonly SKTypeface _boldStyle;
    private readonly SKTypeface _italicStyle;
    private readonly SKTypeface _boldItalicStyle;
    private readonly SKPathEffect _imageDashPathEffect;

    public SKFont TextFont { get; }
    public SKFont TextBoldFont { get; }
    public SKFont TextItalicFont { get; }
    public SKFont TextBoldItalicFont { get; }
    public SKFont CodeFont { get; }
    
    private readonly SKFont[] _headingFonts = new SKFont[6];

    public SKPaint TextPaint { get; }
    public SKPaint CodeTextPaint { get; }
    public SKPaint LinkTextPaint { get; }
    public SKPaint HeadingBorderPaint { get; }
    public SKPaint ThematicBreakPaint { get; }
    public SKPaint CheckboxBorderPaint { get; }
    public SKPaint CheckboxFillPaint { get; }
    public SKPaint CheckboxCheckPaint { get; }
    public SKPaint BulletPaint { get; }
    public SKPaint BulletOpenPaint { get; }
    public SKPaint QuoteBarPaint { get; }
    public SKPaint ImagePlaceholderBorderPaint { get; }
    public SKPaint CodeBackgroundPaint { get; }
    public SKPaint CodeKeywordPaint { get; }
    public SKPaint CodeCommentPaint { get; }
    public SKPaint CodeStringPaint { get; }
    public SKPaint CodeNumberPaint { get; }
    public SKPaint CodePlainPaint { get; }

    public static readonly SKPaint DefaultPaint = new SKPaint();

    public RenderResources(string sansFamily = "Arial", string monoFamily = "Courier New", float baseFontSize = 14.0f)
    {
        SansTypeface = SKTypeface.FromFamilyName(sansFamily, SKFontStyle.Normal);
        _boldStyle = SKTypeface.FromFamilyName(sansFamily, SKFontStyle.Bold);
        _italicStyle = SKTypeface.FromFamilyName(sansFamily, SKFontStyle.Italic);
        _boldItalicStyle = SKTypeface.FromFamilyName(sansFamily, SKFontStyle.BoldItalic);
        MonospaceTypeface = SKTypeface.FromFamilyName(monoFamily, SKFontStyle.Normal);

        TextFont = new SKFont(SansTypeface, baseFontSize);
        TextBoldFont = new SKFont(_boldStyle, baseFontSize);
        TextItalicFont = new SKFont(_italicStyle, baseFontSize);
        TextBoldItalicFont = new SKFont(_boldItalicStyle, baseFontSize);
        CodeFont = new SKFont(MonospaceTypeface, baseFontSize);

        _headingFonts[0] = new SKFont(_boldStyle, baseFontSize * 2.00f); // H1
        _headingFonts[1] = new SKFont(_boldStyle, baseFontSize * 1.50f); // H2
        _headingFonts[2] = new SKFont(_boldStyle, baseFontSize * 1.25f); // H3
        _headingFonts[3] = new SKFont(_boldStyle, baseFontSize * 1.15f); // H4
        _headingFonts[4] = new SKFont(_boldStyle, baseFontSize * 1.00f); // H5
        _headingFonts[5] = new SKFont(_boldStyle, baseFontSize * 0.85f); // H6

        TextPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        CodeTextPaint = new SKPaint
        {
            Color = new SKColor(199, 37, 78),
            IsAntialias = true
        };

        LinkTextPaint = new SKPaint
        {
            Color = new SKColor(3, 102, 214),
            IsAntialias = true
        };

        HeadingBorderPaint = new SKPaint
        {
            Color = new SKColor(225, 228, 232),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            IsAntialias = true
        };

        ThematicBreakPaint = new SKPaint
        {
            Color = new SKColor(225, 228, 232),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.0f,
            IsAntialias = true
        };

        CheckboxBorderPaint = new SKPaint
        {
            Color = new SKColor(100, 100, 100),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        };

        CheckboxFillPaint = new SKPaint
        {
            Color = new SKColor(3, 102, 214),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        CheckboxCheckPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.0f,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };

        BulletPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        BulletOpenPaint = new SKPaint
        {
            Color = BulletPaint.Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            IsAntialias = true
        };

        QuoteBarPaint = new SKPaint
        {
            Color = new SKColor(223, 226, 229),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _imageDashPathEffect = SKPathEffect.CreateDash(new float[] { 4f, 4f }, 0f);
        ImagePlaceholderBorderPaint = new SKPaint
        {
            Color = new SKColor(209, 213, 218),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            PathEffect = _imageDashPathEffect,
            IsAntialias = true
        };

        CodeBackgroundPaint = new SKPaint
        {
            Color = new SKColor(246, 248, 250),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        CodeKeywordPaint = new SKPaint
        {
            Color = new SKColor(215, 58, 73),
            IsAntialias = true
        };

        CodeCommentPaint = new SKPaint
        {
            Color = new SKColor(106, 115, 125),
            IsAntialias = true
        };

        CodeStringPaint = new SKPaint
        {
            Color = new SKColor(3, 47, 98),
            IsAntialias = true
        };

        CodeNumberPaint = new SKPaint
        {
            Color = new SKColor(0, 92, 197),
            IsAntialias = true
        };

        CodePlainPaint = new SKPaint
        {
            Color = new SKColor(36, 41, 46),
            IsAntialias = true
        };
    }

    public SKFont GetHeadingFont(int level)
    {
        int index = level - 1;
        if (index < 0) index = 0;
        if (index > 5) index = 5;
        return _headingFonts[index];
    }

    public void Dispose()
    {
        SansTypeface?.Dispose();
        MonospaceTypeface?.Dispose();
        _boldStyle?.Dispose();
        _italicStyle?.Dispose();
        _boldItalicStyle?.Dispose();
        _imageDashPathEffect?.Dispose();
        
        TextFont?.Dispose();
        TextBoldFont?.Dispose();
        TextItalicFont?.Dispose();
        TextBoldItalicFont?.Dispose();
        CodeFont?.Dispose();

        for (int i = 0; i < _headingFonts.Length; i++)
        {
            _headingFonts[i]?.Dispose();
        }

        TextPaint?.Dispose();
        CodeTextPaint?.Dispose();
        LinkTextPaint?.Dispose();
        HeadingBorderPaint?.Dispose();
        ThematicBreakPaint?.Dispose();
        CheckboxBorderPaint?.Dispose();
        CheckboxFillPaint?.Dispose();
        CheckboxCheckPaint?.Dispose();
        BulletPaint?.Dispose();
        BulletOpenPaint?.Dispose();
        QuoteBarPaint?.Dispose();
        ImagePlaceholderBorderPaint?.Dispose();
        CodeBackgroundPaint?.Dispose();
        CodeKeywordPaint?.Dispose();
        CodeCommentPaint?.Dispose();
        CodeStringPaint?.Dispose();
        CodeNumberPaint?.Dispose();
        CodePlainPaint?.Dispose();
    }
}
