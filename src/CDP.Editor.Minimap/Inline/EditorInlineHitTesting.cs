using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;

namespace XamlPlayground.Editor.Minimap.Inline;

internal static class EditorInlineHitTesting
{
    public static bool HitsVisibleChild(IEnumerable<Control> children, Point point)
    {
        return children
            .Where(static child => child.IsEffectivelyVisible)
            .Any(child => HitsChild(child, point));
    }

    private static bool HitsChild(Control child, Point point)
    {
        if (!child.Bounds.Contains(point))
        {
            return false;
        }

        var localPoint = point - child.Bounds.Position;
        return child is ICustomHitTest customHitTest
            ? customHitTest.HitTest(localPoint)
            : true;
    }
}
