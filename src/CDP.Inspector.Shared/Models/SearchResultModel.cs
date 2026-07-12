using System.Collections.ObjectModel;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Models;

public class SearchResultModel
{
    public string Path { get; set; } = "";
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = "";
}

public class SearchResultItemModel : ViewModelBase
{
    private int _lineNumber;
    private string _lineText = "";
    private string _filePath = "";

    public int LineNumber
    {
        get => _lineNumber;
        set => RaiseAndSetIfChanged(ref _lineNumber, value);
    }

    public string LineText
    {
        get => _lineText;
        set => RaiseAndSetIfChanged(ref _lineText, value);
    }

    public string FilePath
    {
        get => _filePath;
        set => RaiseAndSetIfChanged(ref _filePath, value);
    }
}

public class SearchResultFileModel : ViewModelBase
{
    private string _filePath = "";
    private string _relativePath = "";
    private string _fileName = "";
    private bool _isExpanded = true;

    public string FilePath
    {
        get => _filePath;
        set => RaiseAndSetIfChanged(ref _filePath, value);
    }

    public string RelativePath
    {
        get => _relativePath;
        set => RaiseAndSetIfChanged(ref _relativePath, value);
    }

    public string FileName
    {
        get => _fileName;
        set => RaiseAndSetIfChanged(ref _fileName, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public ObservableCollection<SearchResultItemModel> Matches { get; } = new();
}
