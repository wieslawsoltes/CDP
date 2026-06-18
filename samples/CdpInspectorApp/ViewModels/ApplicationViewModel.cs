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

    // Storage Editor Fields
    private ObservableCollection<StorageEntryModel> _storageItems = new();
    private StorageEntryModel? _selectedStorageItem;
    private string _storageKeyInput = "";
    private string _storageValueInput = "";
    private bool _isStorageEditorVisible;
    private string _storageTitle = "Local Storage";

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

    // Storage Editor Properties
    public ObservableCollection<StorageEntryModel> StorageItems => _storageItems;

    public StorageEntryModel? SelectedStorageItem
    {
        get => _selectedStorageItem;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedStorageItem, value))
            {
                if (_selectedStorageItem != null)
                {
                    StorageKeyInput = _selectedStorageItem.Key;
                    StorageValueInput = _selectedStorageItem.Value;
                }
            }
        }
    }

    public string StorageKeyInput
    {
        get => _storageKeyInput;
        set => RaiseAndSetIfChanged(ref _storageKeyInput, value);
    }

    public string StorageValueInput
    {
        get => _storageValueInput;
        set => RaiseAndSetIfChanged(ref _storageValueInput, value);
    }

    public bool IsStorageEditorVisible
    {
        get => _isStorageEditorVisible;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isStorageEditorVisible, value))
            {
                OnPropertyChanged(nameof(IsPlaceholderVisible));
            }
        }
    }

    public string StorageTitle
    {
        get => _storageTitle;
        private set => RaiseAndSetIfChanged(ref _storageTitle, value);
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
                IsStorageEditorVisible = _selectedNode != null && (_selectedNode.Name == "Local Storage" || _selectedNode.Name == "Session Storage");
                if (IsStorageEditorVisible)
                {
                    StorageTitle = _selectedNode!.Name;
                    _ = RefreshStorageAsync();
                }
            }
        }
    }

    public bool IsResourceEditorVisible
    {
        get => _isResourceEditorVisible;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isResourceEditorVisible, value))
            {
                OnPropertyChanged(nameof(IsPlaceholderVisible));
            }
        }
    }

    public bool IsPlaceholderVisible => !IsResourceEditorVisible && !IsStorageEditorVisible;

    public ICommand RefreshResourcesCommand { get; }
    public ICommand AddResourceCommand { get; }
    public ICommand SaveResourceCommand { get; }
    public ICommand DeleteResourceCommand { get; }

    // Storage Commands
    public ICommand RefreshStorageCommand { get; }
    public ICommand AddStorageItemCommand { get; }
    public ICommand SaveStorageItemCommand { get; }
    public ICommand DeleteStorageItemCommand { get; }

    public ApplicationViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        RefreshResourcesCommand = new RelayCommand(async () => await RefreshResourcesAsync(), () => _cdpService.IsConnected);
        AddResourceCommand = new RelayCommand(AddResource, () => _cdpService.IsConnected);
        SaveResourceCommand = new RelayCommand(async () => await SaveResourceAsync(), () => _cdpService.IsConnected);
        DeleteResourceCommand = new RelayCommand<string>(async (key) => await DeleteResourceAsync(key), (key) => _cdpService.IsConnected);

        RefreshStorageCommand = new RelayCommand(async () => await RefreshStorageAsync(), () => _cdpService.IsConnected);
        AddStorageItemCommand = new RelayCommand(AddStorageItem, () => _cdpService.IsConnected);
        SaveStorageItemCommand = new RelayCommand(async () => await SaveStorageItemAsync(), () => _cdpService.IsConnected);
        DeleteStorageItemCommand = new RelayCommand<string>(async (key) => await DeleteStorageItemAsync(key), (key) => _cdpService.IsConnected);

        InitializeNavigationTree();
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService.IsConnected)
            {
                _ = InitializeDomainAsync();
            }
            else
            {
                ClearData();
            }
            RaiseCanExecuteChangedForAll();
        }
    }

    private async Task InitializeDomainAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("DOMStorage.enable");
            _ = RefreshResourcesAsync();
            if (IsStorageEditorVisible)
            {
                _ = RefreshStorageAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enabling DOMStorage: {ex.Message}");
        }
    }

    private void RaiseCanExecuteChangedForAll()
    {
        ((RelayCommand)RefreshResourcesCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddResourceCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SaveResourceCommand).RaiseCanExecuteChanged();
        ((RelayCommand<string>)DeleteResourceCommand).RaiseCanExecuteChanged();

        ((RelayCommand)RefreshStorageCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddStorageItemCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SaveStorageItemCommand).RaiseCanExecuteChanged();
        ((RelayCommand<string>)DeleteStorageItemCommand).RaiseCanExecuteChanged();
    }

    private void InitializeNavigationTree()
    {
        var appRoot = new AppNavNode("Application");
        var resNode = new AppNavNode("Global Resources");
        appRoot.Children.Add(resNode);
        
        var storageRoot = new AppNavNode("Storage");
        var localStorageNode = new AppNavNode("Local Storage");
        var sessionStorageNode = new AppNavNode("Session Storage");
        storageRoot.Children.Add(localStorageNode);
        storageRoot.Children.Add(sessionStorageNode);
        appRoot.Children.Add(storageRoot);

        appRoot.Children.Add(new AppNavNode("Cookies"));
        appRoot.Children.Add(new AppNavNode("Background Services"));

        _navigationNodes.Clear();
        _navigationNodes.Add(appRoot);
        appRoot.IsExpanded = true;
        storageRoot.IsExpanded = true;
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

    // Storage CRUD Helpers
    private JsonObject GetCurrentStorageId()
    {
        bool isLocal = SelectedNode?.Name == "Local Storage";
        return new JsonObject
        {
            ["securityOrigin"] = "http://localhost:9222",
            ["isLocalStorage"] = isLocal
        };
    }

    public async Task RefreshStorageAsync()
    {
        if (!_cdpService.IsConnected || SelectedNode == null) return;

        try
        {
            var p = new JsonObject { ["storageId"] = GetCurrentStorageId() };
            var response = await _cdpService.SendCommandAsync("DOMStorage.getDOMStorageItems", p);
            if (response != null)
            {
                var entries = response["entries"] as JsonArray;
                Dispatcher.UIThread.Post(() =>
                {
                    StorageItems.Clear();
                    if (entries != null)
                    {
                        foreach (var entry in entries)
                        {
                            if (entry is JsonArray entryArr && entryArr.Count >= 2)
                            {
                                StorageItems.Add(new StorageEntryModel
                                {
                                    Key = entryArr[0]?.GetValue<string>() ?? "",
                                    Value = entryArr[1]?.GetValue<string>() ?? ""
                                });
                            }
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing DOM storage: {ex.Message}");
        }
    }

    private void AddStorageItem()
    {
        StorageKeyInput = "NewStorageKey";
        StorageValueInput = "";
    }

    public async Task SaveStorageItemAsync()
    {
        if (string.IsNullOrEmpty(StorageKeyInput) || SelectedNode == null) return;

        try
        {
            var p = new JsonObject
            {
                ["storageId"] = GetCurrentStorageId(),
                ["key"] = StorageKeyInput,
                ["value"] = StorageValueInput
            };
            await _cdpService.SendCommandAsync("DOMStorage.setDOMStorageItem", p);
            
            StorageKeyInput = "";
            StorageValueInput = "";
            
            await RefreshStorageAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving DOM storage item: {ex.Message}");
        }
    }

    public async Task DeleteStorageItemAsync(string key)
    {
        if (string.IsNullOrEmpty(key) || SelectedNode == null) return;

        try
        {
            var p = new JsonObject
            {
                ["storageId"] = GetCurrentStorageId(),
                ["key"] = key
            };
            await _cdpService.SendCommandAsync("DOMStorage.removeDOMStorageItem", p);
            await RefreshStorageAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting DOM storage item: {ex.Message}");
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

            StorageItems.Clear();
            SelectedStorageItem = null;
            StorageKeyInput = "";
            StorageValueInput = "";
        });
    }
}
