using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;

namespace XamlPlayground.Editor.Minimap.Inline;

public sealed class EditorInlineOverlayPanel : Panel, ICustomHitTest
{
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(finalSize));
        }

        return finalSize;
    }

    public bool HitTest(Point point)
    {
        return EditorInlineHitTesting.HitsVisibleChild(Children.OfType<Control>(), point);
    }
}
