using System;
using System.Collections.Generic;
using SkiaSharp;

namespace CDP.Html.Renderer.Style;

public class ComputedStyle
{
    public Dictionary<string, string> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Inherited properties
    public SKColor Color { get; set; } = SKColors.Black;
    public string FontFamily { get; set; } = "Arial";
    public float FontSize { get; set; } = 16f;
    public SKFontStyleWeight FontWeight { get; set; } = SKFontStyleWeight.Normal;
    public SKFontStyleSlant FontStyle { get; set; } = SKFontStyleSlant.Upright;
    public float? LineHeight { get; set; }
    public TextAlignType TextAlign { get; set; } = TextAlignType.Left;

    // Non-inherited properties
    public DisplayType Display { get; set; } = DisplayType.Inline;
    public PositionType Position { get; set; } = PositionType.Static;
    public CssLength Width { get; set; } = CssLength.Auto;
    public CssLength Height { get; set; } = CssLength.Auto;
    public CssLength MinWidth { get; set; } = CssLength.Auto;
    public CssLength MaxWidth { get; set; } = CssLength.Auto;
    public CssLength MinHeight { get; set; } = CssLength.Auto;
    public CssLength MaxHeight { get; set; } = CssLength.Auto;

    public CssLength Top { get; set; } = CssLength.Auto;
    public CssLength Right { get; set; } = CssLength.Auto;
    public CssLength Bottom { get; set; } = CssLength.Auto;
    public CssLength Left { get; set; } = CssLength.Auto;

    public CssLength MarginLeft { get; set; } = CssLength.Zero;
    public CssLength MarginRight { get; set; } = CssLength.Zero;
    public CssLength MarginTop { get; set; } = CssLength.Zero;
    public CssLength MarginBottom { get; set; } = CssLength.Zero;

    public CssLength PaddingLeft { get; set; } = CssLength.Zero;
    public CssLength PaddingRight { get; set; } = CssLength.Zero;
    public CssLength PaddingTop { get; set; } = CssLength.Zero;
    public CssLength PaddingBottom { get; set; } = CssLength.Zero;

    public float BorderLeftWidth { get; set; } = 0f;
    public float BorderRightWidth { get; set; } = 0f;
    public float BorderTopWidth { get; set; } = 0f;
    public float BorderBottomWidth { get; set; } = 0f;

    public SKColor BorderLeftColor { get; set; } = SKColors.Black;
    public SKColor BorderRightColor { get; set; } = SKColors.Black;
    public SKColor BorderTopColor { get; set; } = SKColors.Black;
    public SKColor BorderBottomColor { get; set; } = SKColors.Black;

    public FlexDirection FlexDirection { get; set; } = FlexDirection.Row;
    public FlexWrap FlexWrap { get; set; } = FlexWrap.NoWrap;
    public JustifyContent JustifyContent { get; set; } = JustifyContent.FlexStart;
    public AlignItems AlignItems { get; set; } = AlignItems.Stretch;
    public float FlexGrow { get; set; } = 0f;
    public float FlexShrink { get; set; } = 1f;
    public CssLength FlexBasis { get; set; } = CssLength.Auto;

    public SKColor? BackgroundColor { get; set; }

    public FloatType Float { get; set; } = FloatType.None;
    public ClearType Clear { get; set; } = ClearType.None;

    public ComputedStyle InheritFrom(ComputedStyle parent)
    {
        Color = parent.Color;
        FontFamily = parent.FontFamily;
        FontSize = parent.FontSize;
        FontWeight = parent.FontWeight;
        FontStyle = parent.FontStyle;
        LineHeight = parent.LineHeight;
        TextAlign = parent.TextAlign;

        foreach (var kvp in parent.CustomProperties)
        {
            CustomProperties[kvp.Key] = kvp.Value;
        }

        return this;
    }

    public ComputedStyle Clone()
    {
        var clone = (ComputedStyle)this.MemberwiseClone();
        clone.CustomProperties = new Dictionary<string, string>(this.CustomProperties, StringComparer.OrdinalIgnoreCase);
        return clone;
    }
}
