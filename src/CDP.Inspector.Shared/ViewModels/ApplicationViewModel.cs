using System;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Avalonia.Controls.DataGridHierarchical;

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

    public bool IsPlaceholderVisible => !IsResourceEditorVisible && !IsStorageEditorVisible && !IsCookieEditorVisible && !IsDatabaseViewerVisible;

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
            if (IsCookieEditorVisible)
            {
                _ = RefreshCookiesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enabling domains: {ex.Message}");
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

    // Cookie CRUD Helpers
    public async Task RefreshCookiesAsync()
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            var response = await _cdpService.SendCommandAsync("Page.getCookies");
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
            Console.WriteLine($"Error refreshing cookies: {ex.Message}");
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
            await _cdpService.SendCommandAsync("Page.setCookie", p);
            
            CookieNameInput = "";
            CookieValueInput = "";
            CookieDomainInput = "";
            CookiePathInput = "";
            CookieExpiresInput = "-1";
            
            await RefreshCookiesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving cookie: {ex.Message}");
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
            await _cdpService.SendCommandAsync("Page.deleteCookie", p);
            await RefreshCookiesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting cookie: {ex.Message}");
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
            Console.WriteLine($"Error refreshing databases: {ex.Message}");
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
            Console.WriteLine($"Error loading database tables: {ex.Message}");
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
            Console.WriteLine($"Error executing SQL: {ex.Message}");
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
}
