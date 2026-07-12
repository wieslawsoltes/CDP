using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Models;

public class OpenEditorModel : ViewModelBase
{
    private string _filePath = "";
    private string _displayName = "";
    private string _originalContent = "";
    private string _currentContent = "";
    private bool _isDirty;
    private bool _isActive;

    public string FilePath
    {
        get => _filePath;
        set => RaiseAndSetIfChanged(ref _filePath, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => RaiseAndSetIfChanged(ref _displayName, value);
    }

    public string OriginalContent
    {
        get => _originalContent;
        set => RaiseAndSetIfChanged(ref _originalContent, value);
    }

    public string CurrentContent
    {
        get => _currentContent;
        set => RaiseAndSetIfChanged(ref _currentContent, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        set => RaiseAndSetIfChanged(ref _isDirty, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => RaiseAndSetIfChanged(ref _isActive, value);
    }
}
