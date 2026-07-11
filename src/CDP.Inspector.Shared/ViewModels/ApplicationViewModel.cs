using System;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Avalonia.Controls.DataGridHierarchical;
using Microsoft.Extensions.Logging;
using Chrome.DevTools.Protocol;
using Avalonia.Layout;
using CDP.Editor.Splits.Models;

namespace CdpInspectorApp.ViewModels;

public class ApplicationViewModel : ViewModelBase, IStateProvider
{
    private SplitNode? _layoutRoot;
    private BoxNode? _selectedPane;

    public SplitNode? LayoutRoot
    {
        get => _layoutRoot;
        set => RaiseAndSetIfChanged(ref _layoutRoot, value);
    }

    public BoxNode? SelectedPane
    {
        get => _selectedPane;
        set => RaiseAndSetIfChanged(ref _selectedPane, value);
    }

    private static readonly ILogger Logger = CdpLogging.CreateLogger<ApplicationViewModel>();
    private readonly ICdpService _cdpService;
    private ObservableCollection<ResourceEntryModel> _resources = new();
    private ResourceEntryModel? _selectedResource;
    private string _resourceKeyInput = "";
    private string _resourceValueInput = "";
    private ObservableCollection<AppNavNode> _navigationNodes = new();
    private AppNavNode? _selectedNode;
    private bool _isResourceEditorVisible;
    private object? _selectedNavigationNodeNode;

    public HierarchicalModel<AppNavNode> HierarchicalNavigationNodes { get; }

    public object? SelectedNavigationNodeNode
    {
        get => _selectedNavigationNodeNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedNavigationNodeNode, value))
            {
                var target = value is HierarchicalNode<AppNavNode> node ? node.Item : (value as AppNavNode);
                if (SelectedNode != target)
                {
                    SelectedNode = target;
                }
            }
        }
    }

    // Storage Editor Fields
    private ObservableCollection<StorageEntryModel> _storageItems = new();
    private StorageEntryModel? _selectedStorageItem;
    private string _storageKeyInput = "";
    private string _storageValueInput = "";
    private bool _isStorageEditorVisible;
    private string _storageTitle = "Local Storage";

    // Cookie Editor Fields
    private ObservableCollection<CookieEntryModel> _cookies = new();
    private CookieEntryModel? _selectedCookie;
    private string _cookieNameInput = "";
    private string _cookieValueInput = "";
    private string _cookieDomainInput = "";
    private string _cookiePathInput = "";
    private string _cookieExpiresInput = "-1";
    private bool _isCookieEditorVisible;

    // Database Viewer Fields
    private AppNavNode? _sqliteRootNode;
    private bool _isDatabaseViewerVisible;
    private string? _selectedDatabasePath;
    private ObservableCollection<string> _databaseTables = new();
    private string? _selectedTableName;
    private ObservableCollection<string> _tableColumns = new();
    private ObservableCollection<string?[]> _tableRows = new();
    private ObservableCollection<string> _consoleColumns = new();
    private ObservableCollection<string?[]> _consoleRows = new();
    private string _customSqlQuery = "SELECT * FROM sqlite_master;";

    // Background Services Fields
    private bool _isBackgroundServicesVisible;
    private string _selectedBackgroundService = "notifications";
    private bool _isBackgroundRecording;
    private ObservableCollection<BackgroundServiceEventModel> _backgroundEvents = new();
    private BackgroundServiceEventModel? _selectedBackgroundEvent;
    private ObservableCollection<BackgroundServiceMetadataModel> _selectedBackgroundEventMetadata = new();

    // IndexedDB Fields
    private AppNavNode? _indexedDbRootNode;
    private bool _isIndexedDBVisible;
    private string? _selectedIndexedDBDatabase;
    private string? _selectedIndexedDBObjectStore;
    private ObservableCollection<string> _indexedDBDatabases = new();
    private ObservableCollection<string> _indexedDBObjectStores = new();
    private ObservableCollection<string> _indexedDBColumns = new();
    private ObservableCollection<string?[]> _indexedDBRows = new();

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

    // Cookie Editor Properties
    public ObservableCollection<CookieEntryModel> Cookies => _cookies;

    public CookieEntryModel? SelectedCookie
    {
        get => _selectedCookie;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedCookie, value))
            {
                if (_selectedCookie != null)
                {
                    CookieNameInput = _selectedCookie.Name;
                    CookieValueInput = _selectedCookie.Value;
                    CookieDomainInput = _selectedCookie.Domain;
                    CookiePathInput = _selectedCookie.Path;
                    CookieExpiresInput = _selectedCookie.Expires.ToString();
                }
            }
        }
    }

    public string CookieNameInput
    {
        get => _cookieNameInput;
        set => RaiseAndSetIfChanged(ref _cookieNameInput, value);
    }

    public string CookieValueInput
    {
        get => _cookieValueInput;
        set => RaiseAndSetIfChanged(ref _cookieValueInput, value);
    }

    public string CookieDomainInput
    {
        get => _cookieDomainInput;
        set => RaiseAndSetIfChanged(ref _cookieDomainInput, value);
    }

    public string CookiePathInput
    {
        get => _cookiePathInput;
        set => RaiseAndSetIfChanged(ref _cookiePathInput, value);
    }

    public string CookieExpiresInput
    {
        get => _cookieExpiresInput;
        set => RaiseAndSetIfChanged(ref _cookieExpiresInput, value);
    }

    public bool IsCookieEditorVisible
    {
        get => _isCookieEditorVisible;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isCookieEditorVisible, value))
            {
                OnPropertyChanged(nameof(IsPlaceholderVisible));
            }
        }
    }

    // Database Viewer Properties
    public bool IsDatabaseViewerVisible
    {
        get => _isDatabaseViewerVisible;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isDatabaseViewerVisible, value))
            {
                OnPropertyChanged(nameof(IsPlaceholderVisible));
            }
        }
    }

    public string? SelectedDatabasePath
    {
        get => _selectedDatabasePath;
        set => RaiseAndSetIfChanged(ref _selectedDatabasePath, value);
    }

    public ObservableCollection<string> DatabaseTables => _databaseTables;

    public string? SelectedTableName
    {
        get => _selectedTableName;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedTableName, value))
            {
                if (!string.IsNullOrEmpty(_selectedTableName))
                {
                    _ = LoadTableDataAsync(_selectedTableName);
                }
            }
        }
    }

    public ObservableCollection<string> TableColumns => _tableColumns;
    public ObservableCollection<string?[]> TableRows => _tableRows;

    public ObservableCollection<string> ConsoleColumns => _consoleColumns;
    public ObservableCollection<string?[]> ConsoleRows => _consoleRows;

    public string CustomSqlQuery
    {
        get => _customSqlQuery;
        set => RaiseAndSetIfChanged(ref _customSqlQuery, value);
    }

    // Background Services Properties
    public static string[] AvailableBackgroundServices { get; } = new[]
    {
        "notifications",
        "pushMessaging",
        "backgroundFetch",
        "backgroundSync",
        "periodicBackgroundSync"
    };

    public bool IsBackgroundServicesVisible
    {
        get => _isBackgroundServicesVisible;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isBackgroundServicesVisible, value))
            {
                OnPropertyChanged(nameof(IsPlaceholderVisible));
            }
        }
    }

    public string SelectedBackgroundService
    {
        get => _selectedBackgroundService;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedBackgroundService, value))
            {
                if (IsBackgroundRecording && _cdpService.IsConnected && !string.IsNullOrEmpty(value))
                {
                    _ = RestartObservingAsync(value);
                }
            }
        }
    }

    public bool IsBackgroundRecording
    {
        get => _isBackgroundRecording;
        private set => RaiseAndSetIfChanged(ref _isBackgroundRecording, value);
    }

    public ObservableCollection<BackgroundServiceEventModel> BackgroundEvents => _backgroundEvents;

    public BackgroundServiceEventModel? SelectedBackgroundEvent
    {
        get => _selectedBackgroundEvent;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedBackgroundEvent, value))
            {
                SelectedBackgroundEventMetadata.Clear();
                if (_selectedBackgroundEvent != null)
                {
                    foreach (var pair in _selectedBackgroundEvent.Metadata)
                    {
                        SelectedBackgroundEventMetadata.Add(pair);
                    }
                }
            }
        }
    }

    public ObservableCollection<BackgroundServiceMetadataModel> SelectedBackgroundEventMetadata => _selectedBackgroundEventMetadata;

    // IndexedDB Properties
    public bool IsIndexedDBVisible
    {
        get => _isIndexedDBVisible;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isIndexedDBVisible, value))
            {
                OnPropertyChanged(nameof(IsPlaceholderVisible));
            }
        }
    }

    public ObservableCollection<string> IndexedDBDatabases => _indexedDBDatabases;

    public string? SelectedIndexedDBDatabase
    {
        get => _selectedIndexedDBDatabase;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedIndexedDBDatabase, value))
            {
                if (!string.IsNullOrEmpty(_selectedIndexedDBDatabase))
                {
                    _ = LoadIndexedDBObjectStoresAsync(_selectedIndexedDBDatabase);
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        IndexedDBObjectStores.Clear();
                        SelectedIndexedDBObjectStore = null;
                        IndexedDBColumns.Clear();
                        IndexedDBRows.Clear();
                    });
                }
            }
        }
    }

    public ObservableCollection<string> IndexedDBObjectStores => _indexedDBObjectStores;

    public string? SelectedIndexedDBObjectStore
    {
        get => _selectedIndexedDBObjectStore;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedIndexedDBObjectStore, value))
            {
                if (!string.IsNullOrEmpty(_selectedIndexedDBObjectStore) && !string.IsNullOrEmpty(SelectedIndexedDBDatabase))
                {
                    _ = LoadIndexedDBDataAsync(SelectedIndexedDBDatabase, _selectedIndexedDBObjectStore);
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        IndexedDBColumns.Clear();
                        IndexedDBRows.Clear();
                    });
                }
            }
        }
    }

    public ObservableCollection<string> IndexedDBColumns => _indexedDBColumns;
    public ObservableCollection<string?[]> IndexedDBRows => _indexedDBRows;

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
                IsCookieEditorVisible = _selectedNode != null && _selectedNode.Name == "Cookies";
                IsDatabaseViewerVisible = _selectedNode != null &&
                    (_selectedNode.Name == "SQLite Databases" || _selectedNode.NodeType == "Database");
                IsBackgroundServicesVisible = _selectedNode != null && _selectedNode.Name == "Background Services";
                IsIndexedDBVisible = _selectedNode != null &&
                    (_selectedNode.Name == "IndexedDB" || _selectedNode.NodeType == "IndexedDBDatabase" || _selectedNode.NodeType == "IndexedDBStore");

                if (IsStorageEditorVisible)
                {
                    StorageTitle = _selectedNode!.Name;
                    _ = RefreshStorageAsync();
                }
                if (IsCookieEditorVisible)
                {
                    _ = RefreshCookiesAsync();
                }
                if (_selectedNode != null && _selectedNode.Name == "SQLite Databases")
                {
                    _ = RefreshDatabasesAsync();
                }
                if (_selectedNode != null && _selectedNode.NodeType == "Database")
                {
                    SelectedDatabasePath = _selectedNode.DatabasePath;
                    _ = LoadDatabaseTablesAsync(SelectedDatabasePath);
                }
                if (_selectedNode != null && _selectedNode.Name == "IndexedDB")
                {
                    _ = RefreshIndexedDBAsync();
                }
                if (_selectedNode != null && _selectedNode.NodeType == "IndexedDBDatabase")
                {
                    SelectedIndexedDBDatabase = _selectedNode.DatabasePath;
                }
                if (_selectedNode != null && _selectedNode.NodeType == "IndexedDBStore")
                {
                    SelectedIndexedDBDatabase = _selectedNode.DatabasePath;
                    SelectedIndexedDBObjectStore = _selectedNode.Name;
                }

                if (IsResourceEditorVisible) NavigateToView("ResourceEditor");
                else if (IsStorageEditorVisible) NavigateToView("StorageEditor");
                else if (IsCookieEditorVisible) NavigateToView("CookieEditor");
                else if (IsDatabaseViewerVisible) NavigateToView("DatabaseViewer");
                else if (IsBackgroundServicesVisible) NavigateToView("BackgroundServices");
                else if (IsIndexedDBVisible) NavigateToView("IndexedDBExplorer");
                else NavigateToView("Simulator");

                if (value == null)
                {
                    SelectedNavigationNodeNode = null;
                }
                else
                {
                    var node = HierarchicalNavigationNodes.FindNode(value);
                    if (!Equals(SelectedNavigationNodeNode, node))
                    {
                        SelectedNavigationNodeNode = node;
                    }
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

    public bool IsPlaceholderVisible => !IsResourceEditorVisible && !IsStorageEditorVisible && !IsCookieEditorVisible && !IsDatabaseViewerVisible && !IsBackgroundServicesVisible && !IsIndexedDBVisible;

    public ICommand RefreshResourcesCommand { get; }
    public ICommand AddResourceCommand { get; }
    public ICommand SaveResourceCommand { get; }
    public ICommand DeleteResourceCommand { get; }

    // Storage Commands
    public ICommand RefreshStorageCommand { get; }
    public ICommand AddStorageItemCommand { get; }
    public ICommand SaveStorageItemCommand { get; }
    public ICommand DeleteStorageItemCommand { get; }

    // Cookie Commands
    public ICommand RefreshCookiesCommand { get; }
    public ICommand AddCookieCommand { get; }
    public ICommand SaveCookieCommand { get; }
    public ICommand DeleteCookieCommand { get; }

    // Database Commands
    public ICommand ExecuteSQLCommand { get; }

    // Background Services Commands
    public ICommand ToggleBackgroundRecordingCommand { get; }
    public ICommand ClearBackgroundEventsCommand { get; }

    // IndexedDB Commands
    public ICommand RefreshIndexedDBCommand { get; }
    public ICommand ClearIndexedDBObjectStoreCommand { get; }

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

        RefreshCookiesCommand = new RelayCommand(async () => await RefreshCookiesAsync(), () => _cdpService.IsConnected);
        AddCookieCommand = new RelayCommand(AddCookie, () => _cdpService.IsConnected);
        SaveCookieCommand = new RelayCommand(async () => await SaveCookieAsync(), () => _cdpService.IsConnected);
        DeleteCookieCommand = new RelayCommand<CookieEntryModel>(async (cookie) => await DeleteCookieAsync(cookie), (cookie) => _cdpService.IsConnected);

        ExecuteSQLCommand = new RelayCommand<string?>(async (sql) => await ExecuteSQLAsync(sql, isConsole: true), (sql) => _cdpService.IsConnected);

        ToggleBackgroundRecordingCommand = new RelayCommand(async () => await ToggleBackgroundRecordingAsync(), () => _cdpService.IsConnected);
        ClearBackgroundEventsCommand = new RelayCommand(async () => await ClearBackgroundEventsAsync(), () => _cdpService.IsConnected);

        RefreshIndexedDBCommand = new RelayCommand(async () => await RefreshIndexedDBAsync(), () => _cdpService.IsConnected);
        ClearIndexedDBObjectStoreCommand = new RelayCommand(async () => await ClearIndexedDBObjectStoreAsync(), () => _cdpService.IsConnected && !string.IsNullOrEmpty(SelectedIndexedDBDatabase) && !string.IsNullOrEmpty(SelectedIndexedDBObjectStore));

        _cdpService.EventReceived += CdpService_EventReceived;

        var options = new HierarchicalOptions<AppNavNode>
        {
            ChildrenSelector = node => node.Children,
            IsLeafSelector = node => node.Children == null || node.Children.Count == 0,
            IsExpandedSelector = node => node.IsExpanded,
            IsExpandedSetter = (node, value) => node.IsExpanded = value,
            IsExpandedPropertyPath = nameof(AppNavNode.IsExpanded),
            AutoExpandRoot = true
        };
        HierarchicalNavigationNodes = new HierarchicalModel<AppNavNode>(options);
        HierarchicalNavigationNodes.SetRoots(NavigationNodes);

        InitializeNavigationTree();
        ResetLayout();
    }

    public void ResetLayout()
    {
        var left = new BoxNode();
        left.AddTab("Navigation", "FolderIcon", "Navigation");

        var right = new BoxNode();
        right.AddTab("Global Resources", "SettingsIcon", "ResourceEditor");
        right.AddTab("Storage Editor", "TableIcon", "StorageEditor");
        right.AddTab("Cookie Editor", "GlobeIcon", "CookieEditor");
        right.AddTab("Database Viewer", "TerminalIcon", "DatabaseViewer");
        right.AddTab("Background Services", "TimerIcon", "BackgroundServices");
        right.AddTab("IndexedDB Explorer", "AppsIcon", "IndexedDBExplorer");
        right.AddTab("Simulator", "EyeIcon", "Simulator");

        LayoutRoot = new SplitContainerNode(Orientation.Horizontal, left, right) { SplitterRatio = 0.25 };
        SelectedPane = left;
    }

    private BoxNode? FindBoxNodeByViewName(SplitNode? node, string viewName)
    {
        if (node == null) return null;
        if (node is BoxNode box)
        {
            foreach (var tab in box.Tabs)
            {
                if (tab.SelectedViewName == viewName) return box;
            }
        }
        if (node is SplitContainerNode container)
        {
            var found = FindBoxNodeByViewName(container.Child1, viewName);
            if (found != null) return found;
            return FindBoxNodeByViewName(container.Child2, viewName);
        }
        return null;
    }

    public void NavigateToView(string viewName)
    {
        var box = FindBoxNodeByViewName(LayoutRoot, viewName);
        if (box != null)
        {
            var tab = box.Tabs.FirstOrDefault(t => t.SelectedViewName == viewName);
            if (tab != null)
            {
                box.ActiveTab = tab;
            }
        }
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
            await _cdpService.SendCommandAsync("IndexedDB.enable");
            _ = RefreshResourcesAsync();
            if (IsStorageEditorVisible)
            {
                _ = RefreshStorageAsync();
            }
            if (IsCookieEditorVisible)
            {
                _ = RefreshCookiesAsync();
            }
            if (IsIndexedDBVisible)
            {
                _ = RefreshIndexedDBAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error enabling domains", ex);
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

        ((RelayCommand)RefreshCookiesCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddCookieCommand).RaiseCanExecuteChanged();
        ((RelayCommand)SaveCookieCommand).RaiseCanExecuteChanged();
        ((RelayCommand<CookieEntryModel>)DeleteCookieCommand).RaiseCanExecuteChanged();

        ((RelayCommand<string?>)ExecuteSQLCommand).RaiseCanExecuteChanged();

        ((RelayCommand)ToggleBackgroundRecordingCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ClearBackgroundEventsCommand).RaiseCanExecuteChanged();

        ((RelayCommand)RefreshIndexedDBCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ClearIndexedDBObjectStoreCommand).RaiseCanExecuteChanged();
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

        _sqliteRootNode = new AppNavNode("SQLite Databases") { NodeType = "Folder" };
        storageRoot.Children.Add(_sqliteRootNode);

        _indexedDbRootNode = new AppNavNode("IndexedDB") { NodeType = "Folder" };
        storageRoot.Children.Add(_indexedDbRootNode);

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
            Logger.LogWarningMessage("ApplicationViewModel", "Error refreshing resources", ex);
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
            Logger.LogWarningMessage("ApplicationViewModel", "Error saving resource", ex);
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
            Logger.LogWarningMessage("ApplicationViewModel", "Error deleting resource", ex);
        }
    }

    // Storage CRUD Helpers
    private async Task<string> ResolveSecurityOriginAsync()
    {
        if (_cdpService.IsConnected)
        {
            try
            {
                var response = await _cdpService.SendCommandAsync("Page.getFrameTree");
                if (response != null && response["frameTree"] is JsonObject frameTreeObj)
                {
                    var frameObj = frameTreeObj["frame"] as JsonObject;
                    if (frameObj != null)
                    {
                        var origin = frameObj["securityOrigin"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(origin) && origin != "://")
                        {
                            return origin;
                        }

                        var url = frameObj["url"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(url))
                        {
                            try
                            {
                                var uri = new Uri(url);
                                return $"{uri.Scheme}://{uri.Authority}";
                            }
                            catch
                            {
                                // ignore parse error
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarningMessage("ApplicationViewModel", "Error resolving security origin", ex);
            }
        }

        var fallbackHost = _cdpService.ConnectedHost;
        if (!string.IsNullOrEmpty(fallbackHost))
        {
            if (fallbackHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                fallbackHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return fallbackHost;
            }
            return $"http://{fallbackHost}";
        }

        return "http://localhost:9222";
    }

    private async Task<JsonObject> GetCurrentStorageIdAsync()
    {
        bool isLocal = SelectedNode?.Name == "Local Storage";
        var origin = await ResolveSecurityOriginAsync();
        return new JsonObject
        {
            ["securityOrigin"] = origin,
            ["isLocalStorage"] = isLocal
        };
    }

    public async Task RefreshStorageAsync()
    {
        if (!_cdpService.IsConnected || SelectedNode == null) return;

        try
        {
            var p = new JsonObject { ["storageId"] = await GetCurrentStorageIdAsync() };
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
            Logger.LogWarningMessage("ApplicationViewModel", "Error refreshing DOM storage", ex);
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
                ["storageId"] = await GetCurrentStorageIdAsync(),
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
            Logger.LogWarningMessage("ApplicationViewModel", "Error saving DOM storage item", ex);
        }
    }

    public async Task DeleteStorageItemAsync(string key)
    {
        if (string.IsNullOrEmpty(key) || SelectedNode == null) return;

        try
        {
            var p = new JsonObject
            {
                ["storageId"] = await GetCurrentStorageIdAsync(),
                ["key"] = key
            };
            await _cdpService.SendCommandAsync("DOMStorage.removeDOMStorageItem", p);
            await RefreshStorageAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error deleting DOM storage item", ex);
        }
    }

    // Cookie CRUD Helpers
    public async Task RefreshCookiesAsync()
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            JsonObject? response = null;
            try
            {
                response = await _cdpService.SendCommandAsync("Network.getCookies");
            }
            catch (Exception ex)
            {
                Logger.LogWarningMessage("ApplicationViewModel", "Network.getCookies failed, falling back to Page.getCookies", ex);
                response = await _cdpService.SendCommandAsync("Page.getCookies");
            }

            if (response != null)
            {
                var cookies = response["cookies"] as JsonArray;
                if (cookies != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Cookies.Clear();
                        foreach (var cookieNode in cookies)
                        {
                            if (cookieNode is JsonObject cookieObj)
                            {
                                Cookies.Add(new CookieEntryModel
                                {
                                    Name = cookieObj["name"]?.GetValue<string>() ?? "",
                                    Value = cookieObj["value"]?.GetValue<string>() ?? "",
                                    Domain = cookieObj["domain"]?.GetValue<string>() ?? "",
                                    Path = cookieObj["path"]?.GetValue<string>() ?? "",
                                    Expires = cookieObj["expires"]?.GetValue<double>() ?? -1
                                });
                            }
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error refreshing cookies", ex);
        }
    }

    private void AddCookie()
    {
        CookieNameInput = "NewCookie";
        CookieValueInput = "Value";
        CookieDomainInput = "localhost";
        CookiePathInput = "/";
        CookieExpiresInput = "-1";
    }

    public async Task SaveCookieAsync()
    {
        if (string.IsNullOrEmpty(CookieNameInput)) return;

        try
        {
            double.TryParse(CookieExpiresInput, out double expiresVal);
            var p = new JsonObject
            {
                ["name"] = CookieNameInput,
                ["value"] = CookieValueInput,
                ["domain"] = CookieDomainInput,
                ["path"] = CookiePathInput,
                ["expires"] = expiresVal
            };
            try
            {
                await _cdpService.SendCommandAsync("Network.setCookie", p);
            }
            catch (Exception ex)
            {
                Logger.LogWarningMessage("ApplicationViewModel", "Network.setCookie failed, falling back to Page.setCookie", ex);
                await _cdpService.SendCommandAsync("Page.setCookie", p);
            }
            
            CookieNameInput = "";
            CookieValueInput = "";
            CookieDomainInput = "";
            CookiePathInput = "";
            CookieExpiresInput = "-1";
            
            await RefreshCookiesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error saving cookie", ex);
        }
    }

    public async Task DeleteCookieAsync(CookieEntryModel? cookie)
    {
        if (cookie == null || string.IsNullOrEmpty(cookie.Name)) return;

        try
        {
            var p = new JsonObject
            {
                ["name"] = cookie.Name,
                ["domain"] = cookie.Domain,
                ["path"] = cookie.Path
            };
            try
            {
                await _cdpService.SendCommandAsync("Network.deleteCookies", p);
            }
            catch (Exception ex)
            {
                Logger.LogWarningMessage("ApplicationViewModel", "Network.deleteCookies failed, falling back to Page.deleteCookie", ex);
                await _cdpService.SendCommandAsync("Page.deleteCookie", p);
            }
            await RefreshCookiesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error deleting cookie", ex);
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

            Cookies.Clear();
            SelectedCookie = null;
            CookieNameInput = "";
            CookieValueInput = "";
            CookieDomainInput = "";
            CookiePathInput = "";
            CookieExpiresInput = "-1";

            if (_sqliteRootNode != null)
            {
                _sqliteRootNode.Children.Clear();
            }
            SelectedDatabasePath = null;
            DatabaseTables.Clear();
            SelectedTableName = null;
            TableColumns.Clear();
            TableRows.Clear();
            ConsoleColumns.Clear();
            ConsoleRows.Clear();

            BackgroundEvents.Clear();
            SelectedBackgroundEvent = null;
            SelectedBackgroundEventMetadata.Clear();
            IsBackgroundRecording = false;

            if (_indexedDbRootNode != null)
            {
                _indexedDbRootNode.Children.Clear();
            }
            SelectedIndexedDBDatabase = null;
            IndexedDBDatabases.Clear();
            SelectedIndexedDBObjectStore = null;
            IndexedDBObjectStores.Clear();
            IndexedDBColumns.Clear();
            IndexedDBRows.Clear();
        });
    }

    public async Task RefreshDatabasesAsync()
    {
        if (!_cdpService.IsConnected || _sqliteRootNode == null) return;

        try
        {
            var response = await _cdpService.SendCommandAsync("Application.getDatabases");
            if (response != null && response["databases"] is JsonArray dbArray)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _sqliteRootNode.Children.Clear();
                    foreach (var dbVal in dbArray)
                    {
                        var dbPath = dbVal?.GetValue<string>();
                        if (!string.IsNullOrEmpty(dbPath))
                        {
                            var fileName = System.IO.Path.GetFileName(dbPath);
                            var dbNode = new AppNavNode(fileName)
                            {
                                NodeType = "Database",
                                DatabasePath = dbPath
                            };
                            _sqliteRootNode.Children.Add(dbNode);
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error refreshing databases", ex);
        }
    }

    public async Task LoadDatabaseTablesAsync(string? dbPath)
    {
        if (string.IsNullOrEmpty(dbPath)) return;

        try
        {
            var p = new JsonObject { ["databasePath"] = dbPath };
            var response = await _cdpService.SendCommandAsync("Application.getDatabaseTableNames", p);
            if (response != null && response["tables"] is JsonArray tablesArray)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DatabaseTables.Clear();
                    TableColumns.Clear();
                    TableRows.Clear();
                    SelectedTableName = null;
                    
                    foreach (var tbl in tablesArray)
                    {
                        var name = tbl?.GetValue<string>();
                        if (!string.IsNullOrEmpty(name))
                        {
                            DatabaseTables.Add(name);
                        }
                    }

                    if (DatabaseTables.Count > 0)
                    {
                        SelectedTableName = DatabaseTables[0];
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error loading database tables", ex);
        }
    }

    public async Task LoadTableDataAsync(string tableName)
    {
        if (string.IsNullOrEmpty(tableName)) return;
        await ExecuteSQLAsync($"SELECT * FROM \"{tableName}\";", isConsole: false);
    }

    public async Task ExecuteSQLAsync(string? sql, bool isConsole = true)
    {
        var queryStr = sql ?? CustomSqlQuery;
        if (string.IsNullOrEmpty(queryStr) || string.IsNullOrEmpty(SelectedDatabasePath)) return;

        try
        {
            var p = new JsonObject
            {
                ["databasePath"] = SelectedDatabasePath,
                ["query"] = queryStr
            };
            var response = await _cdpService.SendCommandAsync("Application.executeSQL", p);
            if (response != null)
            {
                var cols = response["columns"] as JsonArray;
                var rows = response["rows"] as JsonArray;

                Dispatcher.UIThread.Post(() =>
                {
                    var targetCols = isConsole ? ConsoleColumns : TableColumns;
                    var targetRows = isConsole ? ConsoleRows : TableRows;

                    targetCols.Clear();
                    if (cols != null)
                    {
                        foreach (var col in cols)
                        {
                            targetCols.Add(col?.GetValue<string>() ?? "");
                        }
                    }

                    targetRows.Clear();
                    if (rows != null)
                    {
                        foreach (var rowVal in rows)
                        {
                            if (rowVal is JsonArray rowArr)
                            {
                                var arr = new string?[rowArr.Count];
                                for (int i = 0; i < rowArr.Count; i++)
                                {
                                    arr[i] = rowArr[i]?.ToString();
                                }
                                targetRows.Add(arr);
                            }
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("ApplicationViewModel", "Error executing SQL", ex);
            if (isConsole)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ConsoleColumns.Clear();
                    ConsoleRows.Clear();
                    ConsoleColumns.Add("Error");
                    ConsoleRows.Add(new string?[] { ex.Message });
                });
            }
        }
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (e.Method == "BackgroundService.backgroundServiceEventReceived" && e.Params != null)
        {
            var eventObj = e.Params["backgroundServiceEvent"] as JsonObject;
            if (eventObj != null)
            {
                double timestamp = eventObj["timestamp"]?.GetValue<double>() ?? 0;
                string origin = eventObj["origin"]?.GetValue<string>() ?? "";
                string serviceWorkerRegistrationId = eventObj["serviceWorkerRegistrationId"]?.GetValue<string>() ?? "";
                string service = eventObj["service"]?.GetValue<string>() ?? "";
                string eventName = eventObj["eventName"]?.GetValue<string>() ?? "";
                string instanceId = eventObj["instanceId"]?.GetValue<string>() ?? "";

                var metadata = new List<BackgroundServiceMetadataModel>();
                if (eventObj["eventMetadata"] is JsonArray metadataArray)
                {
                    foreach (var node in metadataArray)
                    {
                        if (node is JsonObject metadataObj)
                        {
                            string key = metadataObj["key"]?.GetValue<string>() ?? "";
                            string val = metadataObj["value"]?.GetValue<string>() ?? "";
                            metadata.Add(new BackgroundServiceMetadataModel { Key = key, Value = val });
                        }
                    }
                }

                var dt = DateTimeOffset.FromUnixTimeSeconds((long)timestamp).LocalDateTime;
                string timestampString = dt.ToString("yyyy-MM-dd HH:mm:ss.fff");

                var model = new BackgroundServiceEventModel
                {
                    Timestamp = timestamp,
                    TimestampString = timestampString,
                    Origin = origin,
                    ServiceWorkerRegistrationId = serviceWorkerRegistrationId,
                    Service = service,
                    EventName = eventName,
                    InstanceId = instanceId,
                    Metadata = metadata
                };

                Dispatcher.UIThread.Post(() =>
                {
                    BackgroundEvents.Add(model);
                });
            }
        }
    }

    public async Task ToggleBackgroundRecordingAsync()
    {
        if (!_cdpService.IsConnected || string.IsNullOrEmpty(SelectedBackgroundService)) return;

        try
        {
            if (IsBackgroundRecording)
            {
                var pRecord = new JsonObject
                {
                    ["shouldRecord"] = false,
                    ["service"] = SelectedBackgroundService
                };
                await _cdpService.SendCommandAsync("BackgroundService.setRecording", pRecord);

                var pObserve = new JsonObject
                {
                    ["service"] = SelectedBackgroundService
                };
                await _cdpService.SendCommandAsync("BackgroundService.stopObserving", pObserve);

                IsBackgroundRecording = false;
            }
            else
            {
                var pObserve = new JsonObject
                {
                    ["service"] = SelectedBackgroundService
                };
                await _cdpService.SendCommandAsync("BackgroundService.startObserving", pObserve);

                var pRecord = new JsonObject
                {
                    ["shouldRecord"] = true,
                    ["service"] = SelectedBackgroundService
                };
                await _cdpService.SendCommandAsync("BackgroundService.setRecording", pRecord);

                IsBackgroundRecording = true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error toggling background recording", ex);
        }
    }

    public async Task ClearBackgroundEventsAsync()
    {
        if (IsBackgroundRecording && _cdpService.IsConnected && !string.IsNullOrEmpty(SelectedBackgroundService))
        {
            try
            {
                var p = new JsonObject
                {
                    ["service"] = SelectedBackgroundService
                };
                await _cdpService.SendCommandAsync("BackgroundService.clearEvents", p);
            }
            catch (Exception ex)
            {
                Logger.LogWarningMessage("ApplicationViewModel", "Error clearing background events", ex);
            }
        }
        BackgroundEvents.Clear();
        SelectedBackgroundEvent = null;
        SelectedBackgroundEventMetadata.Clear();
    }

    private async Task RestartObservingAsync(string service)
    {
        try
        {
            await _cdpService.SendCommandAsync("BackgroundService.stopObserving", new JsonObject());
            var pObserve = new JsonObject
            {
                ["service"] = service
            };
            await _cdpService.SendCommandAsync("BackgroundService.startObserving", pObserve);
            var pRecord = new JsonObject
            {
                ["shouldRecord"] = true,
                ["service"] = service
            };
            await _cdpService.SendCommandAsync("BackgroundService.setRecording", pRecord);
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error restarting background service observation", ex);
        }
    }

    public async Task RefreshIndexedDBAsync()
    {
        if (!_cdpService.IsConnected || _indexedDbRootNode == null) return;

        try
        {
            var securityOrigin = await ResolveSecurityOriginAsync();
            var p = new JsonObject { ["securityOrigin"] = securityOrigin };
            var response = await _cdpService.SendCommandAsync("IndexedDB.requestDatabaseNames", p);
            if (response != null && response["databaseNames"] is JsonArray dbArray)
            {
                var dbNamesList = new System.Collections.Generic.List<string>();
                foreach (var dbVal in dbArray)
                {
                    var name = dbVal?.GetValue<string>();
                    if (!string.IsNullOrEmpty(name))
                    {
                        dbNamesList.Add(name);
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    IndexedDBDatabases.Clear();
                    foreach (var name in dbNamesList)
                    {
                        IndexedDBDatabases.Add(name);
                    }

                    _indexedDbRootNode.Children.Clear();
                    foreach (var dbName in dbNamesList)
                    {
                        var dbNode = new AppNavNode(dbName)
                        {
                            NodeType = "IndexedDBDatabase",
                            DatabasePath = dbName
                        };
                        _indexedDbRootNode.Children.Add(dbNode);
                        
                        _ = LoadDatabaseStoresForTreeAsync(dbNode);
                    }

                    if (IndexedDBDatabases.Count > 0)
                    {
                        SelectedIndexedDBDatabase = IndexedDBDatabases[0];
                    }
                    else
                    {
                        SelectedIndexedDBDatabase = null;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error refreshing IndexedDB databases", ex);
        }
    }

    private async Task LoadDatabaseStoresForTreeAsync(AppNavNode dbNode)
    {
        try
        {
            var securityOrigin = await ResolveSecurityOriginAsync();
            var p = new JsonObject
            {
                ["securityOrigin"] = securityOrigin,
                ["databaseName"] = dbNode.DatabasePath
            };
            var response = await _cdpService.SendCommandAsync("IndexedDB.requestDatabase", p);
            if (response != null && response["databaseWithObjectStores"] is JsonObject dbWithStores)
            {
                if (dbWithStores["objectStores"] is JsonArray storesArray)
                {
                    var storesList = new System.Collections.Generic.List<string>();
                    foreach (var storeVal in storesArray)
                    {
                        var name = storeVal?["name"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(name))
                        {
                            storesList.Add(name);
                        }
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        dbNode.Children.Clear();
                        foreach (var storeName in storesList)
                        {
                            var storeNode = new AppNavNode(storeName)
                            {
                                NodeType = "IndexedDBStore",
                                DatabasePath = dbNode.DatabasePath
                            };
                            dbNode.Children.Add(storeNode);
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", $"Error loading database stores for tree node {dbNode.Name}", ex);
        }
    }

    public async Task LoadIndexedDBObjectStoresAsync(string databaseName)
    {
        if (string.IsNullOrEmpty(databaseName)) return;

        try
        {
            var securityOrigin = await ResolveSecurityOriginAsync();
            var p = new JsonObject
            {
                ["securityOrigin"] = securityOrigin,
                ["databaseName"] = databaseName
            };
            var response = await _cdpService.SendCommandAsync("IndexedDB.requestDatabase", p);
            if (response != null && response["databaseWithObjectStores"] is JsonObject dbWithStores)
            {
                if (dbWithStores["objectStores"] is JsonArray storesArray)
                {
                    var storesList = new System.Collections.Generic.List<string>();
                    foreach (var storeVal in storesArray)
                    {
                        var name = storeVal?["name"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(name))
                        {
                            storesList.Add(name);
                        }
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        IndexedDBObjectStores.Clear();
                        foreach (var name in storesList)
                        {
                            IndexedDBObjectStores.Add(name);
                        }

                        if (IndexedDBObjectStores.Count > 0)
                        {
                            SelectedIndexedDBObjectStore = IndexedDBObjectStores[0];
                        }
                        else
                        {
                            SelectedIndexedDBObjectStore = null;
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", $"Error loading IndexedDB object stores for {databaseName}", ex);
        }
    }

    public async Task LoadIndexedDBDataAsync(string databaseName, string objectStoreName)
    {
        if (string.IsNullOrEmpty(databaseName) || string.IsNullOrEmpty(objectStoreName)) return;

        try
        {
            var securityOrigin = await ResolveSecurityOriginAsync();
            var p = new JsonObject
            {
                ["securityOrigin"] = securityOrigin,
                ["databaseName"] = databaseName,
                ["objectStoreName"] = objectStoreName,
                ["skipCount"] = 0,
                ["pageSize"] = 100
            };
            var response = await _cdpService.SendCommandAsync("IndexedDB.requestData", p);
            if (response != null && response["objectStoreDataEntries"] is JsonArray entriesArray)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IndexedDBColumns.Clear();
                    IndexedDBColumns.Add("Key");
                    IndexedDBColumns.Add("Primary Key");
                    IndexedDBColumns.Add("Value");

                    IndexedDBRows.Clear();
                    foreach (var entryVal in entriesArray)
                    {
                        if (entryVal is JsonObject entryObj)
                        {
                            var keyObj = entryObj["key"] as JsonObject;
                            var primKeyObj = entryObj["primaryKey"] as JsonObject;
                            var valObj = entryObj["value"] as JsonObject;

                            var keyStr = keyObj?["value"]?.ToString() ?? "";
                            var primKeyStr = primKeyObj?["value"]?.ToString() ?? "";
                            var valStr = valObj?["value"]?.ToString() ?? "";

                            IndexedDBRows.Add(new string?[] { keyStr, primKeyStr, valStr });
                        }
                    }

                    ((RelayCommand)ClearIndexedDBObjectStoreCommand).RaiseCanExecuteChanged();
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error loading IndexedDB data", ex);
        }
    }

    public async Task ClearIndexedDBObjectStoreAsync()
    {
        if (string.IsNullOrEmpty(SelectedIndexedDBDatabase) || string.IsNullOrEmpty(SelectedIndexedDBObjectStore)) return;

        try
        {
            var securityOrigin = await ResolveSecurityOriginAsync();
            var p = new JsonObject
            {
                ["securityOrigin"] = securityOrigin,
                ["databaseName"] = SelectedIndexedDBDatabase,
                ["objectStoreName"] = SelectedIndexedDBObjectStore
            };
            await _cdpService.SendCommandAsync("IndexedDB.clearObjectStore", p);
            await LoadIndexedDBDataAsync(SelectedIndexedDBDatabase, SelectedIndexedDBObjectStore);
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ApplicationViewModel", "Error clearing IndexedDB object store", ex);
        }
    }

    #region IStateProvider Implementation

    public string StateKey => "application";

    public JsonNode? SaveState()
    {
        var root = new JsonObject();
        root["customSqlQuery"] = CustomSqlQuery;
        root["selectedBackgroundService"] = SelectedBackgroundService;
        return root;
    }

    public void LoadState(JsonNode? stateNode)
    {
        if (stateNode is not JsonObject json) return;

        if (json.TryGetPropertyValue("customSqlQuery", out var queryNode) && queryNode != null)
        {
            CustomSqlQuery = (string?)queryNode ?? "SELECT * FROM sqlite_master;";
        }
        if (json.TryGetPropertyValue("selectedBackgroundService", out var serviceNode) && serviceNode != null)
        {
            SelectedBackgroundService = (string?)serviceNode ?? "notifications";
        }
    }

    #endregion
}
