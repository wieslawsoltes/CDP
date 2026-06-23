using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;
using Avalonia.VisualTree;

namespace XamlPlayground.Editor.Minimap.Inline;

internal sealed class EditorViewZoneLayer : Panel, ICustomHitTest
{
    private readonly EditorInlineFeatureHost _host;
    private IReadOnlyList<EditorInlineZoneSnapshot> _zones = [];

    public EditorViewZoneLayer(EditorInlineFeatureHost host)
    {
        _host = host;
        ClipToBounds = false;
    }

    public void SetZones(IReadOnlyList<EditorInlineZoneSnapshot> zones)
    {
        _zones = zones;
        SynchronizeChildren();
        InvalidateMeasure();
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        SynchronizeChildren();
        foreach (var zone in _zones)
        {
            var x = GetZoneLeft(zone);
            zone.Content.Measure(new Size(Math.Max(0, availableSize.Width - x), zone.Height));
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

        foreach (var group in _zones.GroupBy(zone => new { zone.InsertionOffset, zone.InsertionLineNumber }))
        {
            var y = GetZoneTop(group.Key.InsertionLineNumber);
            foreach (var zone in group)
            {
                var x = GetZoneLeft(zone);
                zone.Content.Arrange(new Rect(x, y, Math.Max(0, finalSize.Width - x), zone.Height));
                y += zone.Height;
            }
        }

        return finalSize;
    }

    public bool HitTest(Point point)
    {
        return EditorInlineHitTesting.HitsVisibleChild(Children.OfType<Control>(), point);
    }

    private double GetZoneTop(int insertionLineNumber)
    {
        if (_host.TextView is not { } textView || textView.Document is null)
        {
            return 0;
        }

        var line = Math.Clamp(insertionLineNumber, 1, Math.Max(1, textView.Document.LineCount));
        try
        {
            var y = textView.GetVisualTopByDocumentLine(line) - textView.ScrollOffset.Y;
            return _host.OverlayLayer is { } overlay &&
                   textView.TranslatePoint(new Point(0, y), overlay) is { } translated
                ? translated.Y
                : y;
        }
        catch
        {
            return 0;
        }
    }

    private double GetZoneLeft(EditorInlineZoneSnapshot zone)
    {
        if (zone.Kind != EditorInlineZoneKind.Annotation ||
            _host.TextView is not { } textView ||
            _host.OverlayLayer is not { } overlay)
        {
            return 0;
        }

        var textViewLeft = textView.TranslatePoint(default, overlay)?.X ?? 0;
        var indentLeft = GetAnnotationIndentLeft(textView, overlay, zone.InsertionLineNumber);
        return Math.Max(0, Math.Max(textViewLeft, indentLeft));
    }

    private static double GetAnnotationIndentLeft(
        AvaloniaEdit.Rendering.TextView textView,
        Visual overlay,
        int lineNumber)
    {
        if (textView.Document is not { } document || document.LineCount == 0)
        {
            return textView.TranslatePoint(default, overlay)?.X ?? 0;
        }

        lineNumber = Math.Clamp(lineNumber, 1, document.LineCount);
        var line = document.GetLineByNumber(lineNumber);
        var text = document.GetText(line.Offset, line.Length);
        var firstCodeColumn = GetFirstCodeColumn(text);
        var visualPosition = textView.GetVisualPosition(
            new AvaloniaEdit.TextViewPosition(lineNumber, firstCodeColumn),
            AvaloniaEdit.Rendering.VisualYPosition.TextTop);
        var textViewPoint = new Point(
            visualPosition.X - textView.ScrollOffset.X,
            0);

        return textView.TranslatePoint(textViewPoint, overlay)?.X ??
               textView.TranslatePoint(default, overlay)?.X ??
               0;
    }

    private static int GetFirstCodeColumn(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return index + 1;
            }
        }

        return 1;
    }

    private void SynchronizeChildren()
    {
        var desired = _zones
            .Select(zone => zone.Content)
            .Where(control => control.Parent is null || ReferenceEquals(control.Parent, this))
            .Distinct()
            .ToArray();

        for (var i = Children.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(Children[i]))
            {
                Children.RemoveAt(i);
            }
        }

        foreach (var child in desired)
        {
            if (!Children.Contains(child))
            {
                Children.Add(child);
            }
        }
    }
}
