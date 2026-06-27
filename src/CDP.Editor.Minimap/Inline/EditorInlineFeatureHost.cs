using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;

namespace XamlPlayground.Editor.Minimap.Inline;

internal sealed class EditorInlineFeatureHost
{
    private readonly MinimapTextEditor _editor;
    private readonly EditorInlineExtensionContext _context;
    private readonly EditorViewZoneSpacerGenerator _viewZoneSpacerGenerator;
    private readonly EditorViewZoneLayer _viewZoneLayer;
    private readonly EditorInlineControlGenerator _inlineControlGenerator;
    private readonly EditorInlineControlLayer _inlineControlLayer;
    private readonly Dictionary<IEditorInlineExtension, IDisposable> _extensionSubscriptions = new();
    private TextView? _attachedTextView;
    private Panel? _overlayLayer;
    private IReadOnlyList<EditorInlineZoneSnapshot> _zones = [];
    private string _foldingSignature = string.Empty;

    public EditorInlineFeatureHost(MinimapTextEditor editor)
    {
        _editor = editor;
        _context = new EditorInlineExtensionContext(editor, this);
        _viewZoneSpacerGenerator = new EditorViewZoneSpacerGenerator(this);
        _viewZoneLayer = new EditorViewZoneLayer(this);
        _inlineControlGenerator = new EditorInlineControlGenerator();
        _inlineControlLayer = new EditorInlineControlLayer(this);

        ViewZones.CollectionChanged += ViewZonesOnCollectionChanged;
        InlineControls.CollectionChanged += InlineControlsOnCollectionChanged;
        Annotations.CollectionChanged += AnnotationsOnCollectionChanged;
        Extensions.CollectionChanged += ExtensionsOnCollectionChanged;
    }

    public ObservableCollection<EditorViewZone> ViewZones { get; } = new();

    public ObservableCollection<EditorInlineControl> InlineControls { get; } = new();

    public ObservableCollection<EditorCodeAnnotation> Annotations { get; } = new();

    public EditorInlineExtensionCollection Extensions { get; } = new();

    public TextView? TextView => _attachedTextView;

    public Panel? OverlayLayer => _overlayLayer;

    public IReadOnlyList<EditorInlineZoneSnapshot> Zones => _zones;

    public IReadOnlyList<EditorInlineControl> InlineControlSnapshot =>
        InlineControls
            .Where(control => control.IsVisible && (control.Control is not null || control.ControlFactory is not null))
            .Where(control => IsOffsetVisible(control.Offset))
            .OrderBy(control => control.Offset)
            .ToArray();

    public void Attach()
    {
        var textView = _editor.TextArea.TextView;
        if (ReferenceEquals(_attachedTextView, textView))
        {
            return;
        }

        Detach();
        _attachedTextView = textView;

        if (!textView.ElementGenerators.Contains(_viewZoneSpacerGenerator))
        {
            textView.ElementGenerators.Insert(0, _viewZoneSpacerGenerator);
        }

        if (!textView.ElementGenerators.Contains(_inlineControlGenerator))
        {
            textView.ElementGenerators.Add(_inlineControlGenerator);
        }

        AttachOverlayLayers();

        textView.VisualLinesChanged += TextViewOnLayoutChanged;
        textView.ScrollOffsetChanged += TextViewOnLayoutChanged;
        _editor.DocumentChanged += EditorOnDocumentChanged;
        Invalidate();
    }

    public void Detach()
    {
        if (_attachedTextView is not { } textView)
        {
            return;
        }

        textView.VisualLinesChanged -= TextViewOnLayoutChanged;
        textView.ScrollOffsetChanged -= TextViewOnLayoutChanged;
        _editor.DocumentChanged -= EditorOnDocumentChanged;

        textView.ElementGenerators.Remove(_viewZoneSpacerGenerator);
        textView.ElementGenerators.Remove(_inlineControlGenerator);
        DetachOverlayLayers();
        _viewZoneLayer.SetZones([]);
        _inlineControlGenerator.SetControls([]);
        _inlineControlLayer.SetControls([]);

        _attachedTextView = null;
    }

    public void SetOverlayLayer(Panel? overlayLayer)
    {
        if (ReferenceEquals(_overlayLayer, overlayLayer))
        {
            return;
        }

        DetachOverlayLayers();
        _overlayLayer = overlayLayer;
        AttachOverlayLayers();
        Invalidate();
    }

    public EditorViewZone ShowPeek(
        int lineNumber,
        string title,
        string? subtitle,
        string text,
        string? language,
        double height)
    {
        ClosePeek();

        var peek = new EditorInlinePeekControl(
            _editor,
            title,
            subtitle,
            text,
            language,
            () => ClosePeek());

        var zone = new EditorViewZone
        {
            LineNumber = lineNumber,
            Placement = EditorInlinePlacement.AfterLine,
            Height = Math.Max(120, height),
            MinHeight = 120,
            Kind = EditorInlineZoneKind.Peek,
            Content = peek
        };

        ViewZones.Add(zone);
        Dispatcher.UIThread.Post(
            () => peek.Focus(),
            DispatcherPriority.Input);
        return zone;
    }

    public void ClosePeek()
    {
        for (var i = ViewZones.Count - 1; i >= 0; i--)
        {
            if (ViewZones[i].Kind == EditorInlineZoneKind.Peek)
            {
                ViewZones.RemoveAt(i);
            }
        }
    }

    public void Invalidate()
    {
        _foldingSignature = GetFoldingSignature();
        _zones = BuildZoneSnapshot();
        _viewZoneSpacerGenerator.Invalidate();
        _inlineControlGenerator.SetControls(InlineControlSnapshot);
        _inlineControlLayer.SetControls(InlineControlSnapshot);
        _viewZoneLayer.SetZones(_zones);

        if (_attachedTextView is { } textView)
        {
            textView.Redraw();
            textView.InvalidateMeasure();
            textView.InvalidateVisual();
        }
    }

    public EditorInlineSpacerMetrics GetSpacerMetrics(int insertionOffset)
    {
        var zoneHeight = _zones
            .Where(zone => zone.InsertionOffset == insertionOffset)
            .Where(IsZoneVisible)
            .Sum(zone => zone.Height);

        if (zoneHeight <= 0)
        {
            return EditorInlineSpacerMetrics.Empty;
        }

        var defaultLineHeight = _attachedTextView?.DefaultLineHeight ?? 18;
        var defaultBaseline = _attachedTextView?.DefaultBaseline ?? 14;
        return new EditorInlineSpacerMetrics(zoneHeight + defaultLineHeight, zoneHeight + defaultBaseline);
    }

    public bool IsLineVisible(int lineNumber)
    {
        return _editor.Document is not { } document ||
               !IsLineHiddenByFold(document, Math.Clamp(lineNumber, 1, Math.Max(1, document.LineCount)));
    }

    public bool IsOffsetVisible(int offset)
    {
        if (_editor.Document is not { } document || document.LineCount == 0)
        {
            return true;
        }

        offset = Math.Clamp(offset, 0, document.TextLength);
        return IsLineVisible(document.GetLineByOffset(offset).LineNumber);
    }

    private IReadOnlyList<EditorInlineZoneSnapshot> BuildZoneSnapshot()
    {
        if (_editor.Document is not { } document)
        {
            return [];
        }

        var snapshots = new List<EditorInlineZoneSnapshot>();
        foreach (var zone in ViewZones.Where(zone => zone.IsVisible))
        {
            if (zone.Content is null)
            {
                continue;
            }

            if (CreateSnapshot(document, zone.LineNumber, zone.Placement, Math.Max(zone.MinHeight, zone.Height), zone.Kind, zone.Content) is { } snapshot)
            {
                snapshots.Add(snapshot);
            }
        }

        foreach (var group in Annotations
                     .Where(annotation => annotation.IsVisible && !string.IsNullOrWhiteSpace(annotation.Text))
                     .Where(annotation => IsLineVisible(annotation.LineNumber))
                     .GroupBy(annotation => annotation.LineNumber)
                     .OrderBy(group => group.Key))
        {
            var annotations = group
                .OrderBy(annotation => annotation.Priority)
                .ThenBy(annotation => annotation.Text, StringComparer.Ordinal)
                .ToArray();

            var bar = EditorCodeAnnotationBar.Create(_editor, annotations);
            if (CreateSnapshot(document, group.Key, EditorInlinePlacement.BeforeLine, 22, EditorInlineZoneKind.Annotation, bar) is { } snapshot)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots
            .OrderBy(snapshot => snapshot.InsertionOffset)
            .ThenBy(snapshot => snapshot.Kind)
            .ToArray();
    }

    private void AttachOverlayLayers()
    {
        if (_overlayLayer is null)
        {
            return;
        }

        AddOverlayChild(_viewZoneLayer);
        AddOverlayChild(_inlineControlLayer);
    }

    private void AddOverlayChild(Control child)
    {
        if (_overlayLayer is null || ReferenceEquals(child.Parent, _overlayLayer))
        {
            return;
        }

        if (child.Parent is Panel oldPanel)
        {
            oldPanel.Children.Remove(child);
        }

        _overlayLayer.Children.Add(child);
    }

    private void DetachOverlayLayers()
    {
        RemoveOverlayChild(_viewZoneLayer);
        RemoveOverlayChild(_inlineControlLayer);
    }

    private static void RemoveOverlayChild(Control child)
    {
        if (child.Parent is Panel parent)
        {
            parent.Children.Remove(child);
        }
    }

    private EditorInlineZoneSnapshot? CreateSnapshot(
        TextDocument document,
        int lineNumber,
        EditorInlinePlacement placement,
        double height,
        EditorInlineZoneKind kind,
        Control content)
    {
        var anchorLineNumber = Math.Clamp(lineNumber, 1, Math.Max(1, document.LineCount));
        if (!IsLineVisible(anchorLineNumber))
        {
            return null;
        }

        var (insertionLineNumber, insertionOffset) = GetInsertionPoint(document, anchorLineNumber, placement);
        return new EditorInlineZoneSnapshot(anchorLineNumber, insertionLineNumber, insertionOffset, height, kind, content);
    }

    private void ViewZonesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Subscribe(e.OldItems?.OfType<EditorViewZone>(), static (zone, handler) => zone.PropertyChanged -= handler);
        Subscribe(e.NewItems?.OfType<EditorViewZone>(), static (zone, handler) => zone.PropertyChanged += handler);
        Invalidate();
    }

    private void InlineControlsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Subscribe(e.OldItems?.OfType<EditorInlineControl>(), static (control, handler) => control.PropertyChanged -= handler);
        Subscribe(e.NewItems?.OfType<EditorInlineControl>(), static (control, handler) => control.PropertyChanged += handler);
        Invalidate();
    }

    private void AnnotationsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Subscribe(e.OldItems?.OfType<EditorCodeAnnotation>(), static (annotation, handler) => annotation.PropertyChanged -= handler);
        Subscribe(e.NewItems?.OfType<EditorCodeAnnotation>(), static (annotation, handler) => annotation.PropertyChanged += handler);
        Invalidate();
    }

    private void ExtensionsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var extension in e.OldItems.OfType<IEditorInlineExtension>())
            {
                if (_extensionSubscriptions.Remove(extension, out var subscription))
                {
                    subscription.Dispose();
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var extension in e.NewItems.OfType<IEditorInlineExtension>())
            {
                _extensionSubscriptions[extension] = extension.Attach(_context);
            }
        }

        Invalidate();
    }

    private void Subscribe<T>(IEnumerable<T>? items, Action<T, PropertyChangedEventHandler> action)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            action(item, ItemOnPropertyChanged);
        }
    }

    private void ItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Invalidate();
    }

    private void TextViewOnLayoutChanged(object? sender, EventArgs e)
    {
        var foldingSignature = GetFoldingSignature();
        if (!string.Equals(_foldingSignature, foldingSignature, StringComparison.Ordinal))
        {
            Invalidate();
            return;
        }

        _viewZoneLayer.InvalidateArrange();
        _inlineControlLayer.InvalidateArrange();
    }

    private void EditorOnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        Invalidate();
    }

    private bool IsZoneVisible(EditorInlineZoneSnapshot zone)
    {
        return IsLineVisible(zone.AnchorLineNumber);
    }

    private (int LineNumber, int Offset) GetInsertionPoint(
        TextDocument document,
        int anchorLineNumber,
        EditorInlinePlacement placement)
    {
        if (placement == EditorInlinePlacement.BeforeLine)
        {
            return (anchorLineNumber, document.GetLineByNumber(anchorLineNumber).Offset);
        }

        if (anchorLineNumber >= document.LineCount)
        {
            return (document.LineCount, document.TextLength);
        }

        for (var lineNumber = anchorLineNumber + 1; lineNumber <= document.LineCount; lineNumber++)
        {
            if (!IsLineHiddenByFold(document, lineNumber))
            {
                return (lineNumber, document.GetLineByNumber(lineNumber).Offset);
            }
        }

        return (document.LineCount, document.TextLength);
    }

    private bool IsLineHiddenByFold(TextDocument document, int lineNumber)
    {
        var foldingManager = GetFoldingManager();
        if (foldingManager is null)
        {
            return false;
        }

        foreach (var folding in foldingManager.AllFoldings)
        {
            if (!folding.IsFolded)
            {
                continue;
            }

            var startLine = document.GetLineByOffset(folding.StartOffset).LineNumber;
            var endOffset = Math.Max(folding.StartOffset, folding.EndOffset - 1);
            var endLine = document.GetLineByOffset(endOffset).LineNumber;
            if (lineNumber > startLine && lineNumber <= endLine)
            {
                return true;
            }
        }

        return false;
    }

    private string GetFoldingSignature()
    {
        var foldingManager = GetFoldingManager();
        if (foldingManager is null)
        {
            return string.Empty;
        }

        return string.Join(
            ";",
            foldingManager.AllFoldings
                .Where(static folding => folding.IsFolded)
                .OrderBy(static folding => folding.StartOffset)
                .ThenBy(static folding => folding.EndOffset)
                .Select(static folding => $"{folding.StartOffset}:{folding.EndOffset}"));
    }

    private FoldingManager? GetFoldingManager()
    {
        return _attachedTextView?.ElementGenerators
            .OfType<FoldingElementGenerator>()
            .Select(static generator => generator.FoldingManager)
            .FirstOrDefault(manager => manager is not null);
    }
}

internal sealed record EditorInlineZoneSnapshot(
    int AnchorLineNumber,
    int InsertionLineNumber,
    int InsertionOffset,
    double Height,
    EditorInlineZoneKind Kind,
    Control Content);

internal readonly record struct EditorInlineSpacerMetrics(double Height, double Baseline)
{
    public static EditorInlineSpacerMetrics Empty { get; } = new(0, 0);
}
