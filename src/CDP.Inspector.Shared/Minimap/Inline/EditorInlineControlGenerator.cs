using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit.Rendering;

namespace XamlPlayground.Editor.Minimap.Inline;

internal sealed class EditorInlineControlGenerator : VisualLineElementGenerator
{
    private IReadOnlyList<EditorInlineControl> _controls = [];

    public void SetControls(IReadOnlyList<EditorInlineControl> controls)
    {
        _controls = controls;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        return _controls
            .Select(static control => control.Offset)
            .Where(offset => offset >= startOffset)
            .DefaultIfEmpty(-1)
            .Min();
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var width = _controls
            .Where(control => control.Offset == offset && (control.Control is not null || control.ControlFactory is not null))
            .Select(GetReservedWidth)
            .Where(static reservedWidth => reservedWidth > 0)
            .Sum();
        if (width <= 0)
        {
            return null;
        }

        return new InlineObjectElement(0, new EditorInlineControlSpacer(width));
    }

    private static double GetReservedWidth(EditorInlineControl control)
    {
        var child = control.ControlFactory?.Invoke() ?? control.Control;
        if (child is null)
        {
            return 0;
        }

        child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Ceiling(child.DesiredSize.Width);
    }

    private sealed class EditorInlineControlSpacer : Control
    {
        private readonly double _width;

        public EditorInlineControlSpacer(double width)
        {
            _width = Math.Max(0, width);
            Focusable = false;
            IsHitTestVisible = false;
            Opacity = 0;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(_width, 0);
        }
    }
}
