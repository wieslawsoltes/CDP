using System;

namespace CdpGalleryApp.ViewModels;

public class MarkdownPageViewModel : ViewModelBase
{
    private string _text = @"# Markdown Live Editor

This is an interactive showcase of the custom **MarkdownEditor** control in Avalonia.

## Features Supported:
- **Bold**, *Italic*, and __Underline__ text styles.
- Custom font size sizing.
- Code blocks with syntax highlighting.
- Interactive checklist items.

### Code Block Example:
```csharp
public class GalleryDemo
{
    public static void Main(string[] args)
    {
        Console.WriteLine(""Welcome to CDP Gallery!"");
    }
}
```

Try editing this markdown text on the left pane and see the rendered result update instantly on the right!
";
    private bool _isReadOnly;
    private string _editorMode = "Split"; // "Split", "Editor", "Reader"

    public string Text
    {
        get => _text;
        set => RaiseAndSetIfChanged(ref _text, value);
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => RaiseAndSetIfChanged(ref _isReadOnly, value);
    }

    public string EditorMode
    {
        get => _editorMode;
        set => RaiseAndSetIfChanged(ref _editorMode, value);
    }
}
