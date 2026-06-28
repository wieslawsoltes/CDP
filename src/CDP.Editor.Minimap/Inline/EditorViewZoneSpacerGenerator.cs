using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit.Rendering;

namespace XamlPlayground.Editor.Minimap.Inline;

internal sealed class EditorViewZoneSpacerGenerator : VisualLineElementGenerator
{
    private readonly EditorInlineFeatureHost _host;

    public EditorViewZoneSpacerGenerator(EditorInlineFeatureHost host)
    {
        _host = host;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        return _host.Zones
            .Select(zone => zone.InsertionOffset)
            .Where(offset => offset >= startOffset)
            .DefaultIfEmpty(-1)
            .Min();
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var metrics = _host.GetSpacerMetrics(offset);
        return metrics.Height <= 0
            ? null
            : new InlineObjectElement(0, new EditorViewZoneSpacer(metrics));
    }

    public void Invalidate()
    {
    }
}

internal sealed class EditorViewZoneSpacer : Control
{
    private readonly EditorInlineSpacerMetrics _metrics;

    public EditorViewZoneSpacer(EditorInlineSpacerMetrics metrics)
    {
        _metrics = new EditorInlineSpacerMetrics(Math.Max(0, metrics.Height), Math.Max(0, metrics.Baseline));
        Focusable = false;
        IsHitTestVisible = false;
        Opacity = 0;
        TextBlock.SetBaselineOffset(this, _metrics.Baseline);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(0, _metrics.Height);
    }
}
