using Avalonia.Controls;
using Avalonia.Media;

namespace XamlPlayground.Editor.Minimap.Inline;

public static class EditorInlineTheme
{
    public static IBrush Brush(Control owner, string resourceKey, IBrush fallback)
    {
        return owner.TryFindResource(resourceKey, owner.ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : fallback;
    }
}
