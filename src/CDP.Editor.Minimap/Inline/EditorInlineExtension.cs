using System;
using System.Threading;
using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace XamlPlayground.Editor.Minimap.Inline;

public interface IEditorInlineExtension
{
    IDisposable Attach(EditorInlineExtensionContext context);
}

public sealed class EditorInlineExtensionContext
{
    private readonly EditorInlineFeatureHost _host;

    internal EditorInlineExtensionContext(MinimapTextEditor editor, EditorInlineFeatureHost host)
    {
        Editor = editor;
        _host = host;
    }

    public MinimapTextEditor Editor { get; }

    public ObservableCollection<EditorViewZone> ViewZones => _host.ViewZones;

    public ObservableCollection<EditorInlineControl> InlineControls => _host.InlineControls;

    public ObservableCollection<EditorCodeAnnotation> Annotations => _host.Annotations;

    public EditorViewZone ShowPeek(
        int lineNumber,
        string title,
        string? subtitle,
        string text,
        string? language = null,
        double height = 240)
    {
        return _host.ShowPeek(lineNumber, title, subtitle, text, language, height);
    }

    public void ClosePeek()
    {
        _host.ClosePeek();
    }

    public EditorViewZone AddViewZone(
        int lineNumber,
        EditorInlinePlacement placement,
        double height,
        Control content,
        EditorInlineZoneKind kind = EditorInlineZoneKind.Custom)
    {
        var zone = new EditorViewZone
        {
            LineNumber = lineNumber,
            Placement = placement,
            Height = height,
            Kind = kind,
            Content = content
        };

        ViewZones.Add(zone);
        return zone;
    }

    public void Invalidate()
    {
        _host.Invalidate();
    }
}

public sealed class EditorInlineRegistration : IDisposable
{
    private Action? _dispose;

    public EditorInlineRegistration(Action dispose)
    {
        _dispose = dispose;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
