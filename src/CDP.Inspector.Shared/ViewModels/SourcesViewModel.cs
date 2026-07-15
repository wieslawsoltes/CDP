using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Layout;
using CDP.Editor.Splits.Models;

namespace CdpInspectorApp.ViewModels;

public class SourcesViewModel : ViewModelBase, IStateProvider
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

    private string? _pendingFilePathToSelect;
    private readonly ICdpService _cdpService;
    private ObservableCollection<WorkspaceFileNode> _workspaceFiles = new();
    private string _selectedFileName = "Select a file from workspace";
    private string _selectedFileContent = "";
    private WorkspaceFileNode? _selectedFile;
    private object? _selectedFileNode;
    private string _searchQuery = "";
    private bool _searchCaseSensitive = false;
    private string _breakpointCondition = "";
    private ObservableCollection<SearchResultModel> _searchResults = new();
    private bool _isMarkdownPreviewMode;
    private bool _isDocumentPreviewMode;
    private bool _isSaving = false;
    private string? _pendingSaveContent = null;
    private string? _pendingSavePath = null;
    private string? _localPreviewFilePath = null;
    private bool _isLoadingContent = false;

    private int? _pendingScrollLine;
    private bool _isDebuggerPaused;
    private int? _activeDebugLine;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _breakpointIds = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _breakpointDisplayStrings = new();

    public int? PendingScrollLine
    {
        get => _pendingScrollLine;
        set => RaiseAndSetIfChanged(ref _pendingScrollLine, value);
    }

    public bool IsDebuggerPaused
    {
        get => _isDebuggerPaused;
        set
        {
            if (RaiseAndSetIfChanged(ref _isDebuggerPaused, value))
            {
                RaiseDebuggerCommandCanExecuteChanged();
            }
        }
    }

    public int? ActiveDebugLine
    {
        get => _activeDebugLine;
        set => RaiseAndSetIfChanged(ref _activeDebugLine, value);
    }

    public ObservableCollection<string> CallStack { get; } = new();
    public ObservableCollection<System.Collections.Generic.KeyValuePair<string, string>> ScopeVariables { get; } = new();
    public ObservableCollection<string> Breakpoints { get; } = new();

    public System.Windows.Input.ICommand ResumeCommand { get; }
    public System.Windows.Input.ICommand StepOverCommand { get; }
    public System.Windows.Input.ICommand StepIntoCommand { get; }
    public System.Windows.Input.ICommand StepOutCommand { get; }
    public System.Windows.Input.ICommand ToggleBreakpointCommand { get; }

    public HierarchicalModel<WorkspaceFileNode> HierarchicalWorkspaceFiles { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (RaiseAndSetIfChanged(ref _searchQuery, value))
            {
                ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool SearchCaseSensitive
    {
        get => _searchCaseSensitive;
        set => RaiseAndSetIfChanged(ref _searchCaseSensitive, value);
    }

    public string BreakpointCondition
    {
        get => _breakpointCondition;
        set => RaiseAndSetIfChanged(ref _breakpointCondition, value);
    }

    public ObservableCollection<SearchResultModel> SearchResults => _searchResults;

    public System.Windows.Input.ICommand SearchCommand { get; }

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

    public string? SelectedFilePath => SelectedFile?.Path;

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

    public bool IsMarkdownFile => SelectedFileName != null && SelectedFileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    public bool IsBinaryDocumentFile => SelectedFileName != null && (
        SelectedFileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ||
        SelectedFileName.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase) ||
        SelectedFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));

    public string? LocalPreviewFilePath
    {
        get => _localPreviewFilePath;
        set => RaiseAndSetIfChanged(ref _localPreviewFilePath, value);
    }

    public bool IsLoadingContent
    {
        get => _isLoadingContent;
        set => RaiseAndSetIfChanged(ref _isLoadingContent, value);
    }

    private static readonly string[] DocumentExtensions = { ".docx", ".rtf", ".pptx", ".xlsx" };

    public bool IsDocumentFile
    {
        get
        {
            if (SelectedFileName == null) return false;
            var ext = System.IO.Path.GetExtension(SelectedFileName);
            foreach (var de in DocumentExtensions)
            {
                if (ext.Equals(de, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    public bool IsDocumentPreviewMode
    {
        get => _isDocumentPreviewMode;
        set
        {
            if (RaiseAndSetIfChanged(ref _isDocumentPreviewMode, value))
            {
                OnPropertyChanged(nameof(IsSourceEditorVisible));
            }
        }
    }

    public bool IsMarkdownPreviewMode
    {
        get => _isMarkdownPreviewMode;
        set
        {
            if (RaiseAndSetIfChanged(ref _isMarkdownPreviewMode, value))
            {
                OnPropertyChanged(nameof(SelectedFileContent));
                OnPropertyChanged(nameof(IsSourceEditorVisible));
            }
        }
    }

    public bool IsSourceEditorVisible => !IsMarkdownPreviewMode && !IsDocumentPreviewMode;

    public WorkspaceFileNode? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedFile, value))
            {
                if (_localPreviewFilePath != null)
                {
                    try
                    {
                        if (System.IO.File.Exists(_localPreviewFilePath))
                        {
                            System.IO.File.Delete(_localPreviewFilePath);
                        }
                    }
                    catch { }
                    LocalPreviewFilePath = null;
                }

                SelectedFileName = value?.Name ?? "Select a file from workspace";
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
                OnPropertyChanged(nameof(SelectedFilePath));
                OnPropertyChanged(nameof(IsMarkdownFile));
                OnPropertyChanged(nameof(IsDocumentFile));
                if (!IsMarkdownFile)
                {
                    IsMarkdownPreviewMode = false;
                }
                else
                {
                    IsMarkdownPreviewMode = true;
                }
                IsDocumentPreviewMode = IsDocumentFile;
                ((RelayCommand<string>)SaveFileCommand).RaiseCanExecuteChanged();
                if (ToggleBreakpointCommand != null)
                {
                    ((RelayCommand<int>)ToggleBreakpointCommand).RaiseCanExecuteChanged();
                }
            }
        }
    }

    public SourcesViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;
        _cdpService.EventReceived += CdpService_EventReceived;

        SaveFileCommand = new RelayCommand<string>(
            async (text) => await SaveFileAsync(text),
            (text) => _cdpService.IsConnected && SelectedFile != null && !SelectedFile.IsDirectory
        );

        SearchCommand = new RelayCommand(
            async () => await SearchAsync(),
            () => _cdpService.IsConnected && !string.IsNullOrWhiteSpace(SearchQuery)
        );

        ResumeCommand = new RelayCommand(
            async () => await ResumeAsync(),
            () => _cdpService.IsConnected && IsDebuggerPaused
        );

        StepOverCommand = new RelayCommand(
            async () => await StepOverAsync(),
            () => _cdpService.IsConnected && IsDebuggerPaused
        );

        StepIntoCommand = new RelayCommand(
            async () => await StepIntoAsync(),
            () => _cdpService.IsConnected && IsDebuggerPaused
        );

        StepOutCommand = new RelayCommand(
            async () => await StepOutAsync(),
            () => _cdpService.IsConnected && IsDebuggerPaused
        );

        ToggleBreakpointCommand = new RelayCommand<int>(
            async (line) => await ToggleBreakpointAsync(line),
            (line) => _cdpService.IsConnected && SelectedFile != null && !SelectedFile.IsDirectory
        );

        var options = new HierarchicalOptions<WorkspaceFileNode>
        {
            ChildrenSelector = node => node.Children,
            IsLeafSelector = node => !node.IsDirectory || node.Children == null || node.Children.Count == 0,
            AutoExpandRoot = true
        };
        HierarchicalWorkspaceFiles = new HierarchicalModel<WorkspaceFileNode>(options);
        HierarchicalWorkspaceFiles.SetRoots(WorkspaceFiles);
        ResetLayout();
    }

    public void ResetLayout()
    {
        var left = new BoxNode();
        left.AddTab("Files", "FolderIcon", "SourcesFiles");
        left.AddTab("Search", "SearchIcon", "SourcesSearch");

        var mid = new BoxNode();
        mid.AddTab("Source Editor", "CodeIcon", "CodeViewer");

        var right = new BoxNode();
        right.AddTab("Debugger", "DeveloperBoardIcon", "Debugger");

        var rightContainer = new SplitContainerNode(Orientation.Horizontal, mid, right) { SplitterRatio = 0.65 };
        LayoutRoot = new SplitContainerNode(Orientation.Horizontal, left, rightContainer) { SplitterRatio = 0.25 };
        SelectedPane = left;
    }

    private void RaiseDebuggerCommandCanExecuteChanged()
    {
        if (ResumeCommand != null) ((RelayCommand)ResumeCommand).RaiseCanExecuteChanged();
        if (StepOverCommand != null) ((RelayCommand)StepOverCommand).RaiseCanExecuteChanged();
        if (StepIntoCommand != null) ((RelayCommand)StepIntoCommand).RaiseCanExecuteChanged();
        if (StepOutCommand != null) ((RelayCommand)StepOutCommand).RaiseCanExecuteChanged();
        if (ToggleBreakpointCommand != null) ((RelayCommand<int>)ToggleBreakpointCommand).RaiseCanExecuteChanged();
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
            ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
            RaiseDebuggerCommandCanExecuteChanged();
        }
    }

    private async Task InitializeWorkspaceAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("Debugger.enable");
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
            SearchResults.Clear();
            ActiveDebugLine = null;
            CallStack.Clear();
            ScopeVariables.Clear();
            IsDebuggerPaused = false;
            _breakpointIds.Clear();
            _breakpointDisplayStrings.Clear();
            Breakpoints.Clear();
        });
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (e.Method == "Debugger.paused" && e.Params != null)
        {
            var callFrames = e.Params["callFrames"] as JsonArray;
            if (callFrames != null && callFrames.Count > 0)
            {
                var firstFrame = callFrames[0] as JsonObject;
                if (firstFrame != null)
                {
                    string url = firstFrame["url"]?.GetValue<string>() ?? "";
                    var location = firstFrame["location"] as JsonObject;
                    int line = 1;
                    if (location != null)
                    {
                        line = location["lineNumber"]?.GetValue<int>() ?? 0;
                    }

                    var stackList = new System.Collections.Generic.List<string>();
                    foreach (var frameNode in callFrames)
                    {
                        if (frameNode is JsonObject frame)
                        {
                            string funcName = frame["functionName"]?.GetValue<string>() ?? "unknown";
                            string frameUrl = frame["url"]?.GetValue<string>() ?? "";
                            var frameLoc = frame["location"] as JsonObject;
                            int frameLine = frameLoc != null ? (frameLoc["lineNumber"]?.GetValue<int>() ?? 0) : 0;
                            string fileName = System.IO.Path.GetFileName(frameUrl);
                            stackList.Add($"{funcName} ({fileName}:{frameLine})");
                        }
                    }

                    var scopeChain = firstFrame["scopeChain"] as JsonArray;
                    string objectIdToQuery = "";
                    if (scopeChain != null && scopeChain.Count > 0)
                    {
                        var firstScope = scopeChain[0] as JsonObject;
                        var obj = firstScope?["object"] as JsonObject;
                        objectIdToQuery = obj?["objectId"]?.GetValue<string>() ?? "";
                    }

                    _ = UpdateDebuggerPausedStateAsync(url, line, stackList, objectIdToQuery);
                }
            }
        }
        else if (e.Method == "Debugger.resumed")
        {
            Dispatcher.UIThread.Post(() =>
            {
                ActiveDebugLine = null;
                CallStack.Clear();
                ScopeVariables.Clear();
                IsDebuggerPaused = false;
            });
        }
    }

    private async Task UpdateDebuggerPausedStateAsync(string url, int line, System.Collections.Generic.List<string> stackList, string scopeObjectId)
    {
        var scopeVarsList = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();

        if (!string.IsNullOrEmpty(scopeObjectId))
        {
            try
            {
                var propsRes = await _cdpService.SendCommandAsync("Runtime.getProperties", new JsonObject { ["objectId"] = scopeObjectId });
                var results = propsRes?["result"] as JsonArray;
                if (results != null)
                {
                    foreach (var p in results)
                    {
                        if (p is JsonObject propObj)
                        {
                            string name = propObj["name"]?.GetValue<string>() ?? "";
                            var valObj = propObj["value"] as JsonObject;
                            string val = valObj?["value"]?.ToString() ?? valObj?["description"]?.GetValue<string>() ?? "null";
                            scopeVarsList.Add(new System.Collections.Generic.KeyValuePair<string, string>(name, val));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get scope properties: {ex.Message}");
            }
        }

        Dispatcher.UIThread.Post(() =>
        {
            CallStack.Clear();
            foreach (var item in stackList)
            {
                CallStack.Add(item);
            }

            ScopeVariables.Clear();
            foreach (var item in scopeVarsList)
            {
                ScopeVariables.Add(item);
            }

            IsDebuggerPaused = true;

            var fileNode = FindFileBySuffix(url);
            if (fileNode != null)
            {
                ActiveDebugLine = line;
                SelectedFile = fileNode;
                PendingScrollLine = line;
            }
        });
    }

    private async Task ResumeAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Debugger.resume");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Resume failed: {ex.Message}");
        }
    }

    private async Task StepOverAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Debugger.stepOver");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StepOver failed: {ex.Message}");
        }
    }

    private async Task StepIntoAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Debugger.stepInto");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StepInto failed: {ex.Message}");
        }
    }

    private async Task StepOutAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Debugger.stepOut");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StepOut failed: {ex.Message}");
        }
    }

    public async Task ToggleBreakpointAsync(int line)
    {
        if (SelectedFile == null || SelectedFile.IsDirectory || !_cdpService.IsConnected)
        {
            return;
        }

        string url = SelectedFile.Path;
        string key = $"{url}:{line}";

        if (_breakpointIds.TryGetValue(key, out var breakpointId))
        {
            try
            {
                var p = new JsonObject { ["breakpointId"] = breakpointId };
                await _cdpService.SendCommandAsync("Debugger.removeBreakpoint", p);
                _breakpointIds.TryRemove(key, out _);
                if (_breakpointDisplayStrings.TryRemove(key, out var displayStr))
                {
                    Dispatcher.UIThread.Post(() => Breakpoints.Remove(displayStr));
                }
                else
                {
                    string fallbackDisplayStr = $"{SelectedFile.Name}:{line}";
                    Dispatcher.UIThread.Post(() => Breakpoints.Remove(fallbackDisplayStr));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Remove breakpoint failed: {ex.Message}");
            }
        }
        else
        {
            try
            {
                var p = new JsonObject
                {
                    ["url"] = url,
                    ["lineNumber"] = line
                };
                string condition = BreakpointCondition;
                if (!string.IsNullOrWhiteSpace(condition))
                {
                    p["condition"] = condition;
                }
                var response = await _cdpService.SendCommandAsync("Debugger.setBreakpointByUrl", p);
                if (response != null)
                {
                    string returnedId = response["breakpointId"]?.GetValue<string>() ?? key;
                    _breakpointIds[key] = returnedId;
                    string displayStr = $"{SelectedFile.Name}:{line}";
                    if (!string.IsNullOrWhiteSpace(condition))
                    {
                        displayStr += $" (if: {condition})";
                    }
                    _breakpointDisplayStrings[key] = displayStr;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (!Breakpoints.Contains(displayStr))
                        {
                            Breakpoints.Add(displayStr);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Set breakpoint failed: {ex.Message}");
            }
        }
    }

    public WorkspaceFileNode? FindFileBySuffix(string suffixPath)
    {
        if (string.IsNullOrEmpty(suffixPath)) return null;
        var suffix = suffixPath.Replace('\\', '/');
        return FindFileBySuffix(WorkspaceFiles, suffix);
    }

    private WorkspaceFileNode? FindFileBySuffix(ObservableCollection<WorkspaceFileNode> nodes, string suffix)
    {
        foreach (var node in nodes)
        {
            if (!node.IsDirectory)
            {
                var normalizedPath = node.Path.Replace('\\', '/');
                if (normalizedPath.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }
            }
            else
            {
                var found = FindFileBySuffix(node.Children, suffix);
                if (found != null)
                {
                    return found;
                }
            }
        }
        return null;
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

        // Restore pending selected file
        if (!string.IsNullOrEmpty(_pendingFilePathToSelect))
        {
            var file = FindFileByPath(_pendingFilePathToSelect);
            if (file != null)
            {
                SelectedFile = file;
                _pendingFilePathToSelect = null;
            }
        }
    }

    private async Task SaveFileAsync(string content)
    {
        if (SelectedFile == null || SelectedFile.IsDirectory || !_cdpService.IsConnected)
        {
            return;
        }

        string filePath = SelectedFile.Path;

        if (_isSaving)
        {
            _pendingSaveContent = content;
            _pendingSavePath = filePath;
            return;
        }

        _isSaving = true;

        try
        {
            while (true)
            {
                var p = new JsonObject 
                { 
                    ["path"] = filePath,
                    ["content"] = content
                };
                var response = await _cdpService.SendCommandAsync("Sources.setFileContent", p);
                if (response != null && response["success"]?.GetValue<bool>() == true)
                {
                    if (SelectedFile != null && SelectedFile.Path == filePath)
                    {
                        SelectedFileContent = content;
                    }
                }

                if (_pendingSaveContent != null && _pendingSavePath != null)
                {
                    content = _pendingSaveContent;
                    filePath = _pendingSavePath;
                    _pendingSaveContent = null;
                    _pendingSavePath = null;
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Save file failed: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task LoadFileContentAsync()
    {
        if (SelectedFile == null || SelectedFile.IsDirectory)
        {
            return;
        }

        IsLoadingContent = true;
        SelectedFileName = SelectedFile.Name;
        SelectedFileContent = "Loading content...";
        LocalPreviewFilePath = null;

        try
        {
            var p = new JsonObject { ["path"] = SelectedFile.Path };
            var response = await _cdpService.SendCommandAsync("Sources.getFileContent", p);
            if (response != null)
            {
                string content = response["content"]?.GetValue<string>() ?? "";
                bool base64Encoded = response["base64Encoded"]?.GetValue<bool>() ?? false;

                if (base64Encoded || IsBinaryDocumentFile)
                {
                    byte[] bytes = Convert.FromBase64String(content);
                    string ext = System.IO.Path.GetExtension(SelectedFileName);
                    string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cdp_preview_{Guid.NewGuid()}{ext}");
                    await System.IO.File.WriteAllBytesAsync(tempFile, bytes);
                    LocalPreviewFilePath = tempFile;
                    SelectedFileContent = $"(Binary file loaded to {tempFile})";
                }
                else
                {
                    SelectedFileContent = content;
                }
            }
        }
        catch (Exception ex)
        {
            SelectedFileContent = $"Error loading content: {ex.Message}";
        }
        finally
        {
            IsLoadingContent = false;
        }
    }

    public async Task RefreshSelectedFileContentAsync()
    {
        if (SelectedFile != null && !SelectedFile.IsDirectory)
        {
            await LoadFileContentAsync();
        }
    }

    public WorkspaceFileNode? FindFileByPath(string path)
    {
        return FindFileByPath(WorkspaceFiles, path);
    }

    private WorkspaceFileNode? FindFileByPath(ObservableCollection<WorkspaceFileNode> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (!node.IsDirectory && node.Path == path)
            {
                return node;
            }
            if (node.IsDirectory)
            {
                var found = FindFileByPath(node.Children, path);
                if (found != null)
                {
                    return found;
                }
            }
        }
        return null;
    }

    public async Task SearchAsync()
    {
        if (!_cdpService.IsConnected || string.IsNullOrWhiteSpace(SearchQuery))
        {
            return;
        }

        try
        {
            var p = new JsonObject
            {
                ["query"] = SearchQuery,
                ["caseSensitive"] = SearchCaseSensitive
            };

            var response = await _cdpService.SendCommandAsync("Sources.searchInWorkspace", p);
            if (response != null && response["matches"] is JsonArray matches)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    SearchResults.Clear();
                    foreach (var matchNode in matches)
                    {
                        if (matchNode is JsonObject matchObj)
                        {
                            SearchResults.Add(new SearchResultModel
                            {
                                Path = matchObj["path"]?.GetValue<string>() ?? "",
                                LineNumber = matchObj["lineNumber"]?.GetValue<int>() ?? 0,
                                LineContent = matchObj["lineContent"]?.GetValue<string>() ?? ""
                            });
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Search failed: {ex.Message}");
        }
    }

    #region IStateProvider Implementation

    public string StateKey => "sources";

    public JsonNode? SaveState()
    {
        var root = new JsonObject();
        root["searchQuery"] = SearchQuery;
        root["searchCaseSensitive"] = SearchCaseSensitive;
        root["breakpointCondition"] = BreakpointCondition;
        root["selectedFilePath"] = SelectedFile?.Path;
        return root;
    }

    public void LoadState(JsonNode? stateNode)
    {
        if (stateNode is not JsonObject json) return;

        if (json.TryGetPropertyValue("searchQuery", out var searchNode) && searchNode != null)
        {
            SearchQuery = (string?)searchNode ?? "";
        }
        if (json.TryGetPropertyValue("searchCaseSensitive", out var caseNode) && caseNode != null)
        {
            SearchCaseSensitive = (bool?)caseNode ?? false;
        }
        if (json.TryGetPropertyValue("breakpointCondition", out var bpNode) && bpNode != null)
        {
            BreakpointCondition = (string?)bpNode ?? "";
        }
        if (json.TryGetPropertyValue("selectedFilePath", out var pathNode) && pathNode != null)
        {
            _pendingFilePathToSelect = (string?)pathNode;
            if (!string.IsNullOrEmpty(_pendingFilePathToSelect))
            {
                var file = FindFileByPath(_pendingFilePathToSelect);
                if (file != null)
                {
                    SelectedFile = file;
                    _pendingFilePathToSelect = null;
                }
            }
        }
    }

    #endregion
}
