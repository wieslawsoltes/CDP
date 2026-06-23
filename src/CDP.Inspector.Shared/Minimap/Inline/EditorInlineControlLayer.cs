using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace XamlPlayground.Editor.Minimap.Inline;

internal sealed class EditorInlineControlLayer : Panel, ICustomHitTest
{
    private readonly EditorInlineFeatureHost _host;
    private IReadOnlyList<EditorInlineControl> _controls = [];
    private readonly Dictionary<EditorInlineControl, Control> _realizedControls = new();

    public EditorInlineControlLayer(EditorInlineFeatureHost host)
    {
        _host = host;
        ClipToBounds = true;
    }

    public void SetControls(IReadOnlyList<EditorInlineControl> controls)
    {
        _controls = controls;
        SynchronizeChildren();
        InvalidateMeasure();
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        SynchronizeChildren();
        foreach (var child in Children.OfType<Control>())
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_host.TextView is null)
        {
            return finalSize;
        }

        SynchronizeChildren();

        foreach (var group in _controls.GroupBy(static control => control.Offset))
        {
            var x = 0d;
            foreach (var control in group)
            {
                if (!_realizedControls.TryGetValue(control, out var child))
                {
                    continue;
                }

                var desired = child.DesiredSize;
                var location = GetControlLocation(control.Offset, desired);
                child.Arrange(new Rect(new Point(location.X + x, location.Y), desired));
                x += desired.Width;
            }
        }

        return finalSize;
    }

    public bool HitTest(Point point)
    {
        return EditorInlineHitTesting.HitsVisibleChild(Children.OfType<Control>(), point);
    }

    private Point GetControlLocation(int offset, Size desiredSize)
    {
        if (_host.TextView is not { } textView ||
            textView.Document is not { } document ||
            _host.OverlayLayer is not { } overlay)
        {
            return default;
        }

        offset = Math.Clamp(offset, 0, document.TextLength);
        var location = document.GetLocation(offset);
        var position = new TextViewPosition(location);
        var visualPosition = textView.GetVisualPosition(position, VisualYPosition.TextMiddle);
        var pointInTextView = new Point(
            visualPosition.X - textView.ScrollOffset.X,
            visualPosition.Y - textView.ScrollOffset.Y - desiredSize.Height / 2);

        return textView.TranslatePoint(pointInTextView, overlay) is { } point
            ? point
            : pointInTextView;
    }

    private void SynchronizeChildren()
    {
        var desired = _controls
            .Select(control => (Model: control, Control: RealizeControl(control)))
            .Where(static item => item.Control is not null)
            .Select(static item => (item.Model, Control: item.Control!))
            .ToArray();

        for (var i = Children.Count - 1; i >= 0; i--)
        {
            if (!desired.Any(item => ReferenceEquals(item.Control, Children[i])))
            {
                Children.RemoveAt(i);
            }
        }

        foreach (var (model, control) in desired)
        {
            _realizedControls[model] = control;
            if (!Children.Contains(control))
            {
                Children.Add(control);
            }
        }

        foreach (var model in _realizedControls.Keys.ToArray())
        {
            if (!_controls.Contains(model))
            {
                _realizedControls.Remove(model);
            }
        }
    }

    private Control? RealizeControl(EditorInlineControl control)
    {
        if (_realizedControls.TryGetValue(control, out var realized) &&
            (realized.Parent is null || ReferenceEquals(realized.Parent, this)))
        {
            return realized;
        }

        var child = control.ControlFactory?.Invoke() ?? control.Control;
        if (child is null)
        {
            return null;
        }

        if (child.Parent is Panel oldPanel && !ReferenceEquals(oldPanel, this))
        {
            oldPanel.Children.Remove(child);
        }

        return child.Parent is null || ReferenceEquals(child.Parent, this)
            ? child
            : null;
    }
}
