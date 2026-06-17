using System;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public class ApplicationViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private ObservableCollection<ResourceEntryModel> _resources = new();
    private ResourceEntryModel? _selectedResource;
    private string _resourceKeyInput = "";
    private string _resourceValueInput = "";
    private ObservableCollection<AppNavNode> _navigationNodes = new();
    private AppNavNode? _selectedNode;
    private bool _isResourceEditorVisible;

    public ObservableCollection<ResourceEntryModel> Resources => _resources;

    public ResourceEntryModel? SelectedResource
    {
        get => _selectedResource;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedResource, value))
            {
                if (_selectedResource != null)
                {
                    ResourceKeyInput = _selectedResource.Key;
                    ResourceValueInput = _selectedResource.Value;
                }
            }
        }
    }

    public string ResourceKeyInput
    {
        get => _resourceKeyInput;
        set => RaiseAndSetIfChanged(ref _resourceKeyInput, value);
    }

    public string ResourceValueInput
    {
        get => _resourceValueInput;
        set => RaiseAndSetIfChanged(ref _resourceValueInput, value);
    }

    public ObservableCollection<AppNavNode> NavigationNodes => _navigationNodes;

    public AppNavNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedNode, value))
            {
                IsResourceEditorVisible = _selectedNode != null && _selectedNode.Name == "Global Resources";
            }
        }
    }

    public bool IsResourceEditorVisible
    {
        get => _isResourceEditorVisible;
        private set => RaiseAndSetIfChanged(ref _isResourceEditorVisible, value);
    }

    public ICommand RefreshResourcesCommand { get; }
    public ICommand AddResourceCommand { get; }
    public ICommand SaveResourceCommand { get; }
    public ICommand DeleteResourceCommand { get; }

    public ApplicationViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        RefreshResourcesCommand = new RelayCommand(async () => await RefreshResourcesAsync(), () => _cdpService.IsConnected);
        AddResourceCommand = new RelayCommand(AddResource, () => _cdpService.IsConnected);
        SaveResourceCommand = new RelayCommand(async () => await SaveResourceAsync(), () => _cdpService.IsConnected);
        DeleteResourceCommand = new RelayCommand<string>(async (key) => await DeleteResourceAsync(key), (key) => _cdpService.IsConnected);

        InitializeNavigationTree();
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService.IsConnected)
            {
                _ = RefreshResourcesAsync();
            }
            else
            {
                ClearData();
            }
            ((RelayCommand)RefreshResourcesCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddResourceCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SaveResourceCommand).RaiseCanExecuteChanged();
            ((RelayCommand<string>)DeleteResourceCommand).RaiseCanExecuteChanged();
        }
    }

    private void InitializeNavigationTree()
    {
        var appRoot = new AppNavNode("Application");
        var resNode = new AppNavNode("Global Resources");
        appRoot.Children.Add(resNode);
        appRoot.Children.Add(new AppNavNode("Preferences & Themes"));

        _navigationNodes.Clear();
        _navigationNodes.Add(appRoot);
        appRoot.IsExpanded = true;
        resNode.IsSelected = true;
        SelectedNode = resNode;
    }

    public async Task RefreshResourcesAsync()
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            var response = await _cdpService.SendCommandAsync("Application.getResources");
            if (response != null)
            {
                var resources = response["resources"] as JsonArray;
                if (resources != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Resources.Clear();
                        foreach (var resNode in resources)
                        {
                            if (resNode is JsonObject resObj)
                            {
                                Resources.Add(new ResourceEntryModel
                                {
                                    Key = resObj["key"]?.GetValue<string>() ?? "",
                                    Type = resObj["type"]?.GetValue<string>() ?? "",
                                    Value = resObj["value"]?.GetValue<string>() ?? ""
                                });
                            }
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing resources: {ex.Message}");
        }
    }

    private void AddResource()
    {
        ResourceKeyInput = "NewKey";
        ResourceValueInput = "";
    }

    public async Task SaveResourceAsync()
    {
        if (string.IsNullOrEmpty(ResourceKeyInput)) return;

        try
        {
            var p = new JsonObject
            {
                ["key"] = ResourceKeyInput,
                ["value"] = ResourceValueInput
            };
            await _cdpService.SendCommandAsync("Application.setResource", p);
            await RefreshResourcesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving resource: {ex.Message}");
        }
    }

    public async Task DeleteResourceAsync(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        try
        {
            var p = new JsonObject { ["key"] = key };
            await _cdpService.SendCommandAsync("Application.deleteResource", p);
            await RefreshResourcesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting resource: {ex.Message}");
        }
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Resources.Clear();
            SelectedResource = null;
            ResourceKeyInput = "";
            ResourceValueInput = "";
        });
    }
}
