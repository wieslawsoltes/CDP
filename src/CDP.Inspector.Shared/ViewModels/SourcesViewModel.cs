using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public class SourcesViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private ObservableCollection<WorkspaceFileNode> _workspaceFiles = new();
    private string _selectedFileName = "Select a file from workspace";
    private string _selectedFileContent = "";
    private WorkspaceFileNode? _selectedFile;

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
            }
        }
    }

    public SourcesViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;
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
