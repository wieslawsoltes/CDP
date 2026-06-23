using Avalonia.Media;

namespace XamlPlayground.Editor.Minimap;

public sealed class TextMinimapDecoration
{
    public int LineNumber { get; set; }

    public TextMinimapDecorationPlacement Placement { get; set; } = TextMinimapDecorationPlacement.Inline;

    public IBrush? Brush { get; set; }

    public double Thickness { get; set; } = 2;

    public string? SectionHeaderText { get; set; }

    public TextMinimapSectionHeaderStyle SectionHeaderStyle { get; set; } = TextMinimapSectionHeaderStyle.Normal;
}
