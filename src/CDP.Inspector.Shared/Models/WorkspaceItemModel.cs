using System.Collections.ObjectModel;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Models;

public class WorkspaceItemModel : ViewModelBase
{
    private string _name = "";
    private string _path = "";
    private bool _isFolder;
    private bool _isExpanded;
    private bool _isSelected;
    private string _formattedSize = "";
    private string _fileType = "";
    private string _formattedDateModified = "";

    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(ref _name, value);
    }

    public string Path
    {
        get => _path;
        set => RaiseAndSetIfChanged(ref _path, value);
    }

    public bool IsFolder
    {
        get => _isFolder;
        set => RaiseAndSetIfChanged(ref _isFolder, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public string FormattedSize
    {
        get => _formattedSize;
        set => RaiseAndSetIfChanged(ref _formattedSize, value);
    }

    public string FileType
    {
        get => _fileType;
        set => RaiseAndSetIfChanged(ref _fileType, value);
    }

    public string FormattedDateModified
    {
        get => _formattedDateModified;
        set => RaiseAndSetIfChanged(ref _formattedDateModified, value);
    }

    public ObservableCollection<WorkspaceItemModel> Children { get; } = new();
}
