using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Avalonia.Controls.DataGridHierarchical;

namespace CdpInspectorApp.ViewModels;

public class SourcesViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private ObservableCollection<WorkspaceFileNode> _workspaceFiles = new();
    private string _selectedFileName = "Select a file from workspace";
    private string _selectedFileContent = "";
    private WorkspaceFileNode? _selectedFile;
    private object? _selectedFileNode;

    public HierarchicalModel<WorkspaceFileNode> HierarchicalWorkspaceFiles { get; }

    public object? SelectedFileNode
    {
        get => _selectedFileNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedFileNode, value))
            {
                var target = value is HierarchicalNode<WorkspaceFileNode> node ? node.Item : (value as WorkspaceFileNode);
                if (SelectedFile != target)
                {
                    SelectedFile = target;
                }
            }
        }
    }

    public bool IsFileSelected => SelectedFile != null && !SelectedFile.IsDirectory;

    public System.Windows.Input.ICommand SaveFileCommand { get; }

    public ObservableCollection<WorkspaceFileNode> WorkspaceFiles => _workspaceFiles;

    public string SelectedFileName
    {
        get => _selectedFileName;
        set => RaiseAndSetIfChanged(ref _selectedFileName, value);
    }

    public string SelectedFileContent
    {
        get => _selectedFileContent;
        set => RaiseAndSetIfChanged(ref _selectedFileContent, value);
    }

    public WorkspaceFileNode? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedFile, value))
            {
                _ = LoadFileContentAsync();

                if (value == null)
                {
                    SelectedFileNode = null;
                }
                else
                {
                    var node = HierarchicalWorkspaceFiles.FindNode(value);
                    if (!Equals(SelectedFileNode, node))
                    {
                        SelectedFileNode = node;
                    }
                }
                OnPropertyChanged(nameof(IsFileSelected));
                ((RelayCommand<string>)SaveFileCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public SourcesViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        SaveFileCommand = new RelayCommand<string>(
            async (text) => await SaveFileAsync(text),
            (text) => _cdpService.IsConnected && SelectedFile != null && !SelectedFile.IsDirectory
        );

        var options = new HierarchicalOptions<WorkspaceFileNode>
        {
            ChildrenSelector = node => node.Children,
            IsLeafSelector = node => !node.IsDirectory || node.Children == null || node.Children.Count == 0,
            AutoExpandRoot = true
        };
        HierarchicalWorkspaceFiles = new HierarchicalModel<WorkspaceFileNode>(options);
        HierarchicalWorkspaceFiles.SetRoots(WorkspaceFiles);
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService.IsConnected)
            {
                _ = InitializeWorkspaceAsync();
            }
            else
            {
                ClearData();
            }
            ((RelayCommand<string>)SaveFileCommand).RaiseCanExecuteChanged();
        }
    }

    private async Task InitializeWorkspaceAsync()
    {
        try
        {
            var sourcesRes = await _cdpService.SendCommandAsync("Sources.getWorkspaceFiles");
            var files = sourcesRes["files"] as JsonArray;
            if (files != null)
            {
                Dispatcher.UIThread.Post(() => LoadWorkspaceFiles(files));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sources failed: {ex.Message}");
        }
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            WorkspaceFiles.Clear();
            SelectedFileName = "Select a file from workspace";
            SelectedFileContent = "";
            SelectedFile = null;
        });
    }

    private void LoadWorkspaceFiles(JsonArray filesArray)
    {
        var root = new WorkspaceFileNode { Name = "Workspace", Path = "", IsDirectory = true };
        foreach (var fileNode in filesArray)
        {
            if (fileNode is not JsonObject fileObj) continue;
            string relPath = fileObj["path"]?.GetValue<string>() ?? "";
            string name = fileObj["name"]?.GetValue<string>() ?? "";
            
            string[] parts = relPath.Split('/');
            var current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                bool isLast = (i == parts.Length - 1);
                
                var existing = current.Children.FirstOrDefault(c => c.Name == part);
                if (existing == null)
                {
                    var newNode = new WorkspaceFileNode
                    {
                        Name = part,
                        Path = string.Join('/', parts, 0, i + 1),
                        IsDirectory = !isLast
                    };
                    current.Children.Add(newNode);
                    current = newNode;
                }
                else
                {
                    current = existing;
                }
            }
        }
        
        WorkspaceFiles.Clear();
        foreach (var child in root.Children)
        {
            WorkspaceFiles.Add(child);
        }
    }

    private async Task SaveFileAsync(string content)
    {
        if (SelectedFile == null || SelectedFile.IsDirectory || !_cdpService.IsConnected)
        {
            return;
        }

        try
        {
            var p = new JsonObject 
            { 
                ["path"] = SelectedFile.Path,
                ["content"] = content
            };
            var response = await _cdpService.SendCommandAsync("Sources.setFileContent", p);
            if (response != null && response["success"]?.GetValue<bool>() == true)
            {
                SelectedFileContent = content;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Save file failed: {ex.Message}");
        }
    }

    private async Task LoadFileContentAsync()
    {
        if (SelectedFile == null || SelectedFile.IsDirectory)
        {
            return;
        }

        SelectedFileName = SelectedFile.Name;
        SelectedFileContent = "Loading content...";

        try
        {
            var p = new JsonObject { ["path"] = SelectedFile.Path };
            var response = await _cdpService.SendCommandAsync("Sources.getFileContent", p);
            if (response != null)
            {
                string content = response["content"]?.GetValue<string>() ?? "";
                SelectedFileContent = content;
            }
        }
        catch (Exception ex)
        {
            SelectedFileContent = $"Error loading content: {ex.Message}";
        }
    }
}
