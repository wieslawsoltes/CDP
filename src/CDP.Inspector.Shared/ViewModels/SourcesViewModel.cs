using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Layout;
using CDP.Editor.Splits.Models;
using Chrome.DevTools.Protocol;
using Chrome.DevTools.Protocol.Inspector;
using Microsoft.Extensions.Logging;

namespace CdpInspectorApp.ViewModels;

public class SourcesViewModel : ViewModelBase, IStateProvider
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<SourcesViewModel>();
    private const string DebugConsoleObjectGroup = "cdp-inspector-debug-console";
    private const string HoverObjectGroup = "cdp-inspector-hover";
    private const string VariableEditObjectGroup = "cdp-inspector-variable-edit";
    private const string WatchObjectGroup = "cdp-inspector-watch";
    private static readonly V8SourceMutationEngine SourceMutationEngine = new();
    private const int MaximumVariableDepth = 32;
    private SplitNode? _layoutRoot;
    private BoxNode? _selectedPane;
    private SplitNode? _debuggerLayoutRoot;
    private BoxNode? _selectedDebuggerPane;

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

    public SplitNode? DebuggerLayoutRoot
    {
        get => _debuggerLayoutRoot;
        set => RaiseAndSetIfChanged(ref _debuggerLayoutRoot, value);
    }

    public BoxNode? SelectedDebuggerPane
    {
        get => _selectedDebuggerPane;
        set => RaiseAndSetIfChanged(ref _selectedDebuggerPane, value);
    }

    private string? _pendingFilePathToSelect;
    private readonly ICdpService _cdpService;
    private static readonly System.Net.Http.HttpClient SourceMapHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private ObservableCollection<WorkspaceFileNode> _workspaceFiles = new();
    private string _selectedFileName = "Select a file from workspace";
    private string _selectedFileContent = "";
    private WorkspaceFileNode? _selectedFile;
    private object? _selectedFileNode;
    private string _searchQuery = "";
    private bool _searchCaseSensitive = false;
    private string _breakpointCondition = "";
    private string _breakpointLogMessage = "";
    private string _breakpointKind = V8BreakpointKinds.Breakpoint;
    private bool _areBreakpointsActive = true;
    private V8BreakpointModel? _selectedBreakpoint;
    private string _newBlackboxPattern = "";
    private string? _selectedBlackboxPattern;
    private bool _skipAnonymousScripts;
    private string _blackboxStatusText = "";
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
    private bool _isDebuggerEnabled;
    private int? _activeDebugLine;
    private string _debuggerStatusText = "Debugger disconnected";
    private string _pauseReason = "";
    private string _pauseOnExceptionsState = "none";
    private string _debuggerEvaluationExpression = "";
    private string _debuggerEvaluationResult = "";
    private string _liveEditStatus = "";
    private string _newWatchExpression = "";
    private V8WatchExpressionModel? _selectedWatchExpression;
    private V8ScriptModel? _selectedRuntimeScript;
    private V8CallFrameModel? _selectedCallFrame;
    private V8ScopeVariableModel? _selectedScopeVariable;
    private object? _selectedScopeVariableNode;
    private string _newVariableValueExpression = "";
    private int _pauseGeneration;

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

    public bool IsDebuggerEnabled
    {
        get => _isDebuggerEnabled;
        set
        {
            if (RaiseAndSetIfChanged(ref _isDebuggerEnabled, value))
            {
                OnPropertyChanged(nameof(CanEditCurrentSource));
                RaiseDebuggerCommandCanExecuteChanged();
                if (ApplySourceChangesCommand != null) ((RelayCommand<string>)ApplySourceChangesCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string DebuggerStatusText
    {
        get => _debuggerStatusText;
        set => RaiseAndSetIfChanged(ref _debuggerStatusText, value);
    }

    public string PauseReason
    {
        get => _pauseReason;
        set => RaiseAndSetIfChanged(ref _pauseReason, value);
    }

    public string PauseOnExceptionsState
    {
        get => _pauseOnExceptionsState;
        set
        {
            if (RaiseAndSetIfChanged(ref _pauseOnExceptionsState, value) && _cdpService.IsConnected)
            {
                _ = SetPauseOnExceptionsAsync(value);
            }
        }
    }

    public string DebuggerEvaluationExpression
    {
        get => _debuggerEvaluationExpression;
        set
        {
            if (RaiseAndSetIfChanged(ref _debuggerEvaluationExpression, value))
            {
                ((RelayCommand)EvaluateOnCallFrameCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string DebuggerEvaluationResult
    {
        get => _debuggerEvaluationResult;
        set => RaiseAndSetIfChanged(ref _debuggerEvaluationResult, value);
    }

    public string LiveEditStatus
    {
        get => _liveEditStatus;
        set => RaiseAndSetIfChanged(ref _liveEditStatus, value);
    }

    public int? ActiveDebugLine
    {
        get => _activeDebugLine;
        set => RaiseAndSetIfChanged(ref _activeDebugLine, value);
    }

    public ObservableCollection<string> CallStack { get; } = new();
    public ObservableCollection<V8ScopeVariableModel> ScopeVariables { get; } = new();
    public HierarchicalModel<V8ScopeVariableModel> HierarchicalScopeVariables { get; }
    public ObservableCollection<string> Breakpoints { get; } = new();
    public ObservableCollection<V8ScriptModel> RuntimeScripts { get; } = new();
    public ObservableCollection<V8CallFrameModel> CallFrames { get; } = new();
    public ObservableCollection<V8ScopeModel> Scopes { get; } = new();
    public ObservableCollection<V8BreakpointModel> V8Breakpoints { get; } = new();
    public ObservableCollection<V8WatchExpressionModel> WatchExpressions { get; } = new();
    public ObservableCollection<string> PauseOnExceptionsStates { get; } = new() { "none", "uncaught", "caught", "all" };
    public ObservableCollection<string> BreakpointKinds { get; } = new()
    {
        V8BreakpointKinds.Breakpoint,
        V8BreakpointKinds.Conditional,
        V8BreakpointKinds.Logpoint
    };
    public ObservableCollection<string> BlackboxPatterns { get; } = new();

    public V8ScriptModel? SelectedRuntimeScript
    {
        get => _selectedRuntimeScript;
        set
        {
            if (!RaiseAndSetIfChanged(ref _selectedRuntimeScript, value)) return;

            if (value is not null)
            {
                if (_selectedFile is not null)
                {
                    _selectedFile = null;
                    OnPropertyChanged(nameof(SelectedFile));
                    OnPropertyChanged(nameof(IsFileSelected));
                    OnPropertyChanged(nameof(SelectedFilePath));
                }
                _ = LoadRuntimeScriptSourceAsync(value);
            }
            LiveEditStatus = "";
            OnPropertyChanged(nameof(CanEditCurrentSource));
            if (ApplySourceChangesCommand != null) ((RelayCommand<string>)ApplySourceChangesCommand).RaiseCanExecuteChanged();
            RaiseDebuggerCommandCanExecuteChanged();
        }
    }

    public V8CallFrameModel? SelectedCallFrame
    {
        get => _selectedCallFrame;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedCallFrame, value))
            {
                _ = LoadScopesForFrameAsync(value);
                _ = RefreshWatchExpressionsAsync();
                ((RelayCommand)EvaluateOnCallFrameCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RestartFrameCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SetVariableValueCommand).RaiseCanExecuteChanged();
                if (value is not null)
                {
                    _ = NavigateToCallFrameAsync(value);
                }
            }
        }
    }

    public string NewWatchExpression
    {
        get => _newWatchExpression;
        set
        {
            if (RaiseAndSetIfChanged(ref _newWatchExpression, value))
            {
                ((RelayCommand)AddWatchExpressionCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public V8WatchExpressionModel? SelectedWatchExpression
    {
        get => _selectedWatchExpression;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedWatchExpression, value))
            {
                ((RelayCommand)RemoveWatchExpressionCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public V8BreakpointModel? SelectedBreakpoint
    {
        get => _selectedBreakpoint;
        set
        {
            if (!RaiseAndSetIfChanged(ref _selectedBreakpoint, value)) return;
            if (value is not null)
            {
                BreakpointKind = value.Kind;
                BreakpointCondition = value.Condition;
                BreakpointLogMessage = value.LogMessage;
            }
            ((RelayCommand)ToggleSelectedBreakpointEnabledCommand).RaiseCanExecuteChanged();
            ((RelayCommand)UpdateSelectedBreakpointCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RemoveSelectedBreakpointCommand).RaiseCanExecuteChanged();
        }
    }

    public V8ScopeVariableModel? SelectedScopeVariable
    {
        get => _selectedScopeVariable;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedScopeVariable, value))
            {
                if (value is null)
                {
                    if (SelectedScopeVariableNode is not null) SelectedScopeVariableNode = null;
                }
                else
                {
                    var node = HierarchicalScopeVariables.FindNode(value);
                    if (!Equals(SelectedScopeVariableNode, node)) SelectedScopeVariableNode = node;
                }
                ((RelayCommand)SetVariableValueCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public object? SelectedScopeVariableNode
    {
        get => _selectedScopeVariableNode;
        set
        {
            if (!RaiseAndSetIfChanged(ref _selectedScopeVariableNode, value)) return;
            var target = value is HierarchicalNode<V8ScopeVariableModel> node
                ? node.Item
                : value as V8ScopeVariableModel;
            if (!ReferenceEquals(SelectedScopeVariable, target)) SelectedScopeVariable = target;
        }
    }

    public string NewVariableValueExpression
    {
        get => _newVariableValueExpression;
        set
        {
            if (RaiseAndSetIfChanged(ref _newVariableValueExpression, value))
            {
                ((RelayCommand)SetVariableValueCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string NewBlackboxPattern
    {
        get => _newBlackboxPattern;
        set
        {
            if (RaiseAndSetIfChanged(ref _newBlackboxPattern, value))
            {
                ((RelayCommand)AddBlackboxPatternCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string? SelectedBlackboxPattern
    {
        get => _selectedBlackboxPattern;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedBlackboxPattern, value))
            {
                ((RelayCommand)RemoveBlackboxPatternCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool SkipAnonymousScripts
    {
        get => _skipAnonymousScripts;
        set
        {
            if (RaiseAndSetIfChanged(ref _skipAnonymousScripts, value) && _cdpService.IsConnected && IsDebuggerEnabled)
            {
                _ = ApplyBlackboxPatternsAsync();
            }
        }
    }

    public string BlackboxStatusText
    {
        get => _blackboxStatusText;
        set => RaiseAndSetIfChanged(ref _blackboxStatusText, value);
    }

    public System.Windows.Input.ICommand ResumeCommand { get; }
    public System.Windows.Input.ICommand PauseCommand { get; }
    public System.Windows.Input.ICommand StepOverCommand { get; }
    public System.Windows.Input.ICommand StepIntoCommand { get; }
    public System.Windows.Input.ICommand StepOutCommand { get; }
    public System.Windows.Input.ICommand ToggleBreakpointCommand { get; }
    public System.Windows.Input.ICommand EvaluateOnCallFrameCommand { get; }
    public System.Windows.Input.ICommand ApplySourceChangesCommand { get; }
    public System.Windows.Input.ICommand RestartFrameCommand { get; }
    public System.Windows.Input.ICommand AddWatchExpressionCommand { get; }
    public System.Windows.Input.ICommand RemoveWatchExpressionCommand { get; }
    public System.Windows.Input.ICommand RefreshWatchExpressionsCommand { get; }
    public System.Windows.Input.ICommand ToggleSelectedBreakpointEnabledCommand { get; }
    public System.Windows.Input.ICommand UpdateSelectedBreakpointCommand { get; }
    public System.Windows.Input.ICommand RemoveSelectedBreakpointCommand { get; }
    public System.Windows.Input.ICommand SetVariableValueCommand { get; }
    public System.Windows.Input.ICommand AddBlackboxPatternCommand { get; }
    public System.Windows.Input.ICommand RemoveBlackboxPatternCommand { get; }
    public System.Windows.Input.ICommand ApplyBlackboxPatternsCommand { get; }

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

    public string BreakpointLogMessage
    {
        get => _breakpointLogMessage;
        set => RaiseAndSetIfChanged(ref _breakpointLogMessage, value);
    }

    public string BreakpointKind
    {
        get => _breakpointKind;
        set => RaiseAndSetIfChanged(ref _breakpointKind, V8BreakpointKinds.Normalize(value));
    }

    public bool AreBreakpointsActive
    {
        get => _areBreakpointsActive;
        set
        {
            if (RaiseAndSetIfChanged(ref _areBreakpointsActive, value) && _cdpService.IsConnected && IsDebuggerEnabled)
            {
                _ = SetBreakpointsActiveAsync(value);
            }
        }
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

    public bool CanEditCurrentSource =>
        (SelectedFile != null && !SelectedFile.IsDirectory && !IsDocumentFile) ||
        (SelectedRuntimeScript != null && IsDebuggerEnabled &&
            (!SelectedRuntimeScript.IsOriginalSource ||
             SelectedRuntimeScript.SourceMap is not null &&
             !string.IsNullOrWhiteSpace(SelectedRuntimeScript.GeneratedScriptId)));

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
                if (value is not null && _selectedRuntimeScript is not null)
                {
                    _selectedRuntimeScript = null;
                    OnPropertyChanged(nameof(SelectedRuntimeScript));
                }
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
                OnPropertyChanged(nameof(CanEditCurrentSource));
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
                ((RelayCommand<string>)ApplySourceChangesCommand).RaiseCanExecuteChanged();
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
            (text) => _cdpService.IsConnected && SelectedFile != null && !SelectedFile.IsDirectory && !IsDocumentFile
        );

        ApplySourceChangesCommand = new RelayCommand<string>(
            async (text) => await ApplySourceChangesAsync(text),
            (text) => _cdpService.IsConnected && CanEditCurrentSource
        );

        SearchCommand = new RelayCommand(
            async () => await SearchAsync(),
            () => _cdpService.IsConnected && !string.IsNullOrWhiteSpace(SearchQuery)
        );

        ResumeCommand = new RelayCommand(
            async () => await ResumeAsync(),
            () => _cdpService.IsConnected && IsDebuggerPaused
        );

        PauseCommand = new RelayCommand(
            async () => await PauseAsync(),
            () => _cdpService.IsConnected && IsDebuggerEnabled && !IsDebuggerPaused
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
            (line) => _cdpService.IsConnected && IsDebuggerEnabled &&
                ((SelectedFile != null && !SelectedFile.IsDirectory) || SelectedRuntimeScript != null)
        );

        EvaluateOnCallFrameCommand = new RelayCommand(
            async () => await EvaluateOnCallFrameAsync(),
            () => _cdpService.IsConnected && IsDebuggerPaused && SelectedCallFrame?.CanInspect == true &&
                !string.IsNullOrWhiteSpace(DebuggerEvaluationExpression)
        );

        RestartFrameCommand = new RelayCommand(
            async () => await RestartFrameAsync(),
            () => _cdpService.IsConnected && IsDebuggerPaused && SelectedCallFrame?.CanInspect == true
        );

        AddWatchExpressionCommand = new RelayCommand(
            async () => await AddWatchExpressionAsync(),
            () => !string.IsNullOrWhiteSpace(NewWatchExpression)
        );

        RemoveWatchExpressionCommand = new RelayCommand(
            () =>
            {
                if (SelectedWatchExpression is not null) WatchExpressions.Remove(SelectedWatchExpression);
                SelectedWatchExpression = null;
                if (RefreshWatchExpressionsCommand is RelayCommand refresh) refresh.RaiseCanExecuteChanged();
            },
            () => SelectedWatchExpression is not null
        );

        RefreshWatchExpressionsCommand = new RelayCommand(
            async () => await RefreshWatchExpressionsAsync(),
            () => _cdpService.IsConnected && IsDebuggerPaused && SelectedCallFrame != null && WatchExpressions.Count > 0
        );

        ToggleSelectedBreakpointEnabledCommand = new RelayCommand(
            async () =>
            {
                if (SelectedBreakpoint is not null)
                {
                    await SetBreakpointEnabledAsync(SelectedBreakpoint, !SelectedBreakpoint.IsEnabled);
                }
            },
            () => SelectedBreakpoint is not null && _cdpService.IsConnected && IsDebuggerEnabled
        );

        UpdateSelectedBreakpointCommand = new RelayCommand(
            async () => await UpdateSelectedBreakpointAsync(),
            () => SelectedBreakpoint is not null && _cdpService.IsConnected && IsDebuggerEnabled
        );

        RemoveSelectedBreakpointCommand = new RelayCommand(
            async () =>
            {
                if (SelectedBreakpoint is not null) await RemoveBreakpointDefinitionAsync(SelectedBreakpoint);
            },
            () => SelectedBreakpoint is not null
        );

        SetVariableValueCommand = new RelayCommand(
            async () => await SetVariableValueAsync(),
            () => _cdpService.IsConnected && IsDebuggerPaused && SelectedCallFrame?.CanInspect == true &&
                SelectedScopeVariable?.Writable == true && !string.IsNullOrWhiteSpace(NewVariableValueExpression)
        );

        AddBlackboxPatternCommand = new RelayCommand(
            async () =>
            {
                var pattern = NewBlackboxPattern.Trim();
                if (!string.IsNullOrWhiteSpace(pattern) && !BlackboxPatterns.Contains(pattern))
                {
                    BlackboxPatterns.Add(pattern);
                    NewBlackboxPattern = "";
                    await ApplyBlackboxPatternsAsync();
                }
            },
            () => !string.IsNullOrWhiteSpace(NewBlackboxPattern)
        );

        RemoveBlackboxPatternCommand = new RelayCommand(
            async () =>
            {
                if (SelectedBlackboxPattern is null) return;
                BlackboxPatterns.Remove(SelectedBlackboxPattern);
                SelectedBlackboxPattern = null;
                await ApplyBlackboxPatternsAsync();
            },
            () => SelectedBlackboxPattern is not null
        );

        ApplyBlackboxPatternsCommand = new RelayCommand(
            async () => await ApplyBlackboxPatternsAsync(),
            () => _cdpService.IsConnected && IsDebuggerEnabled
        );

        var scopeOptions = new HierarchicalOptions<V8ScopeVariableModel>
        {
            ChildrenSelectorAsync = async (node, cancellationToken) =>
                await node.LoadChildrenForHierarchyAsync(cancellationToken),
            IsLeafSelector = node => !node.IsExpandable,
            AutoExpandRoot = false
        };
        HierarchicalScopeVariables = new HierarchicalModel<V8ScopeVariableModel>(scopeOptions);
        HierarchicalScopeVariables.SetRoots(ScopeVariables);

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
        left.AddTab("Runtime Scripts", "CodeIcon", "SourcesRuntimeScripts");
        left.AddTab("Search", "SearchIcon", "SourcesSearch");

        var mid = new BoxNode();
        mid.AddTab("Source Editor", "CodeIcon", "CodeViewer");

        var right = new BoxNode();
        right.AddTab("Debugger", "DeveloperBoardIcon", "Debugger");

        var rightContainer = new SplitContainerNode(Orientation.Horizontal, mid, right) { SplitterRatio = 0.71 };
        LayoutRoot = new SplitContainerNode(Orientation.Horizontal, left, rightContainer) { SplitterRatio = 0.21 };
        SelectedPane = left;

        var watch = new BoxNode();
        watch.AddTab("Watch", "EyeIcon", "DebuggerWatch");
        watch.AddTab("Ignore List", "FilterIcon", "DebuggerIgnoreList");

        var callStack = new BoxNode("DebuggerCallStack", "Call Stack", "ListIcon");
        var variables = new BoxNode("DebuggerVariables", "Variables", "TableIcon");
        var breakpoints = new BoxNode("DebuggerBreakpoints", "Breakpoints", "BreakpointIcon");

        var variablesAndBreakpoints = new SplitContainerNode(Orientation.Vertical, variables, breakpoints) { SplitterRatio = 0.48 };
        var stackAndDetails = new SplitContainerNode(Orientation.Vertical, callStack, variablesAndBreakpoints) { SplitterRatio = 0.26 };
        DebuggerLayoutRoot = new SplitContainerNode(Orientation.Vertical, watch, stackAndDetails) { SplitterRatio = 0.22 };
        SelectedDebuggerPane = watch;
    }

    private void RaiseDebuggerCommandCanExecuteChanged()
    {
        if (ResumeCommand != null) ((RelayCommand)ResumeCommand).RaiseCanExecuteChanged();
        if (PauseCommand != null) ((RelayCommand)PauseCommand).RaiseCanExecuteChanged();
        if (StepOverCommand != null) ((RelayCommand)StepOverCommand).RaiseCanExecuteChanged();
        if (StepIntoCommand != null) ((RelayCommand)StepIntoCommand).RaiseCanExecuteChanged();
        if (StepOutCommand != null) ((RelayCommand)StepOutCommand).RaiseCanExecuteChanged();
        if (ToggleBreakpointCommand != null) ((RelayCommand<int>)ToggleBreakpointCommand).RaiseCanExecuteChanged();
        if (EvaluateOnCallFrameCommand != null) ((RelayCommand)EvaluateOnCallFrameCommand).RaiseCanExecuteChanged();
        if (ApplySourceChangesCommand != null) ((RelayCommand<string>)ApplySourceChangesCommand).RaiseCanExecuteChanged();
        if (RestartFrameCommand != null) ((RelayCommand)RestartFrameCommand).RaiseCanExecuteChanged();
        if (RefreshWatchExpressionsCommand != null) ((RelayCommand)RefreshWatchExpressionsCommand).RaiseCanExecuteChanged();
        if (ToggleSelectedBreakpointEnabledCommand != null) ((RelayCommand)ToggleSelectedBreakpointEnabledCommand).RaiseCanExecuteChanged();
        if (UpdateSelectedBreakpointCommand != null) ((RelayCommand)UpdateSelectedBreakpointCommand).RaiseCanExecuteChanged();
        if (RemoveSelectedBreakpointCommand != null) ((RelayCommand)RemoveSelectedBreakpointCommand).RaiseCanExecuteChanged();
        if (SetVariableValueCommand != null) ((RelayCommand)SetVariableValueCommand).RaiseCanExecuteChanged();
        if (ApplyBlackboxPatternsCommand != null) ((RelayCommand)ApplyBlackboxPatternsCommand).RaiseCanExecuteChanged();
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
            ((RelayCommand<string>)ApplySourceChangesCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
            RaiseDebuggerCommandCanExecuteChanged();
        }
    }

    private async Task InitializeWorkspaceAsync()
    {
        if (!IsDebuggerPaused) DebuggerStatusText = "Enabling V8 debugger...";
        if (!_cdpService.SupportsDomain("Debugger"))
        {
            IsDebuggerEnabled = false;
            DebuggerStatusText = "Debugger unavailable for this target";
            return;
        }

        try
        {
            if (_cdpService.SupportsDomain("Runtime"))
            {
                await _cdpService.SendCommandAsync("Runtime.enable");
            }
            await _cdpService.SendCommandAsync("Debugger.enable");
            IsDebuggerEnabled = true;
            await TrySendOptionalDebuggerCommandAsync(
                "Debugger.setAsyncCallStackDepth",
                new JsonObject { ["maxDepth"] = 32 });
            await TrySendOptionalDebuggerCommandAsync(
                "Debugger.setPauseOnExceptions",
                new JsonObject { ["state"] = PauseOnExceptionsState });
            await TrySendOptionalDebuggerCommandAsync(
                "Debugger.setBreakpointsActive",
                new JsonObject { ["active"] = AreBreakpointsActive });
            await ApplyBlackboxPatternsAsync();
            await RestoreBreakpointBindingsAsync();
            if (!IsDebuggerPaused)
            {
                DebuggerStatusText = $"Debugger ready ({(_cdpService.ConnectedTargetType.Length == 0 ? "CDP" : _cdpService.ConnectedTargetType)})";
            }
        }
        catch (Exception ex)
        {
            IsDebuggerEnabled = false;
            DebuggerStatusText = $"Debugger unavailable: {ex.Message}";
            Logger.LogErrorMessage("SourcesVM", "Debugger initialization failed", ex);
        }

        if (!_cdpService.SupportsDomain("Sources"))
        {
            return;
        }

        try
        {
            var sourcesRes = await _cdpService.SendCommandAsync("Sources.getWorkspaceFiles");
            if (sourcesRes["files"] is JsonArray files)
            {
                Dispatcher.UIThread.Post(() => LoadWorkspaceFiles(files));
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Target does not provide the optional Sources workspace domain");
        }
    }

    private async Task TrySendOptionalDebuggerCommandAsync(string method, JsonObject parameters)
    {
        try
        {
            await _cdpService.SendCommandAsync(method, parameters);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Target does not provide optional debugger action {DebuggerAction}", method);
        }
    }

    private void ClearData()
    {
        Interlocked.Increment(ref _pauseGeneration);
        Dispatcher.UIThread.Post(() =>
        {
            WorkspaceFiles.Clear();
            SelectedFileName = "Select a file from workspace";
            SelectedFileContent = "";
            SelectedFile = null;
            SearchResults.Clear();
            ActiveDebugLine = null;
            CallStack.Clear();
            CallFrames.Clear();
            Scopes.Clear();
            ScopeVariables.Clear();
            SelectedScopeVariable = null;
            NewVariableValueExpression = "";
            RuntimeScripts.Clear();
            SelectedRuntimeScript = null;
            SelectedCallFrame = null;
            IsDebuggerPaused = false;
            IsDebuggerEnabled = false;
            PauseReason = "";
            DebuggerStatusText = "Debugger disconnected";
            SelectedBreakpoint = null;
            foreach (var breakpoint in V8Breakpoints)
            {
                breakpoint.BreakpointId = "";
                breakpoint.IsResolved = false;
                breakpoint.ResolvedLineNumber = null;
                breakpoint.ResolvedColumnNumber = null;
            }
            RefreshLegacyBreakpoints();
        });
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        switch (e.Method)
        {
            case "Debugger.scriptParsed" when e.Params is not null:
                HandleScriptParsed(e.Params);
                break;
            case "Debugger.scriptFailedToParse" when e.Params is not null:
                HandleScriptParsed(e.Params);
                break;
            case "Debugger.paused" when e.Params is not null:
                HandleDebuggerPaused(e.Params);
                break;
            case "Debugger.resumed":
                Interlocked.Increment(ref _pauseGeneration);
                _ = ReleaseDebuggerObjectGroupsAsync();
                Dispatcher.UIThread.Post(() =>
                {
                    ActiveDebugLine = null;
                    CallStack.Clear();
                    CallFrames.Clear();
                    Scopes.Clear();
                    ScopeVariables.Clear();
                    SelectedScopeVariable = null;
                    SelectedCallFrame = null;
                    foreach (var watch in WatchExpressions) watch.Value = "Not paused";
                    PauseReason = "";
                    DebuggerStatusText = "Debugger running";
                    IsDebuggerPaused = false;
                });
                break;
            case "Debugger.breakpointResolved" when e.Params is not null:
                HandleBreakpointResolved(e.Params);
                break;
            case "Runtime.executionContextsCleared":
                Dispatcher.UIThread.Post(RuntimeScripts.Clear);
                break;
        }
    }

    private void HandleScriptParsed(JsonObject parameters)
    {
        var scriptId = parameters["scriptId"]?.GetValue<string>() ?? "";
        if (scriptId.Length == 0) return;
        var script = new V8ScriptModel
        {
            ScriptId = scriptId,
            Url = parameters["url"]?.GetValue<string>() ?? "",
            Hash = parameters["hash"]?.GetValue<string>() ?? "",
            SourceMapUrl = parameters["sourceMapURL"]?.GetValue<string>() ?? "",
            ExecutionContextId = parameters["executionContextId"]?.GetValue<int>() ?? 0,
            StartLine = parameters["startLine"]?.GetValue<int>() ?? 0,
            StartColumn = parameters["startColumn"]?.GetValue<int>() ?? 0,
            EndLine = parameters["endLine"]?.GetValue<int>() ?? 0,
            EndColumn = parameters["endColumn"]?.GetValue<int>() ?? 0,
            Length = parameters["length"]?.GetValue<int>() ?? 0,
            IsModule = parameters["isModule"]?.GetValue<bool>() ?? false
        };

        Dispatcher.UIThread.Post(() =>
        {
            var existing = RuntimeScripts.FirstOrDefault(item => item.ScriptId == script.ScriptId);
            if (existing is not null) RuntimeScripts.Remove(existing);
            RuntimeScripts.Add(script);
        });
        if (parameters["resolvedBreakpoints"] is JsonArray resolvedBreakpoints)
        {
            foreach (var resolvedBreakpoint in resolvedBreakpoints.OfType<JsonObject>())
            {
                HandleBreakpointResolved(resolvedBreakpoint);
            }
        }
        if (script.HasSourceMap) _ = LoadSourceMapAsync(script);
    }

    private async Task LoadSourceMapAsync(V8ScriptModel generatedScript)
    {
        try
        {
            var (json, mapUri) = await ReadSourceMapAsync(generatedScript.Url, generatedScript.SourceMapUrl);
            var sourceMap = await V8SourceMap.ParseAsync(json, mapUri, ReadSourceMapSectionAsync);
            var originals = new System.Collections.Generic.List<V8ScriptModel>();
            for (var index = 0; index < sourceMap.Sources.Count; index++)
            {
                var sourceUrl = sourceMap.ResolveSourceUrl(index);
                var sourceContent = index < sourceMap.SourcesContent.Count ? sourceMap.SourcesContent[index] : null;
                originals.Add(new V8ScriptModel
                {
                    ScriptId = $"{generatedScript.ScriptId}:source:{index}",
                    Url = sourceUrl,
                    GeneratedScriptId = generatedScript.ScriptId,
                    GeneratedUrl = generatedScript.Url,
                    SourceIndex = index,
                    SourceContent = sourceContent,
                    SourceMap = sourceMap,
                    IsOriginalSource = true,
                    IsIgnoredSource = sourceMap.IsIgnoredSource(index),
                    IsModule = generatedScript.IsModule
                });
            }

            Dispatcher.UIThread.Post(() =>
            {
                foreach (var original in originals)
                {
                    if (!RuntimeScripts.Any(item => item.ScriptId == original.ScriptId)) RuntimeScripts.Add(original);
                }
            });
            await ApplySourceMapBlackboxingAsync(generatedScript.ScriptId, sourceMap);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Unable to load source map {SourceMapUrl} for {ScriptUrl}", generatedScript.SourceMapUrl, generatedScript.Url);
        }
    }

    private static async Task<(string Json, Uri? MapUri)> ReadSourceMapAsync(string scriptUrl, string sourceMapUrl)
    {
        if (sourceMapUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = sourceMapUrl.IndexOf(',');
            if (comma < 0) throw new FormatException("Invalid source-map data URI.");
            var metadata = sourceMapUrl[..comma];
            var payload = sourceMapUrl[(comma + 1)..];
            var baseUri = Uri.TryCreate(scriptUrl, UriKind.Absolute, out var inlineScriptUri) ? inlineScriptUri : null;
            return metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)
                ? (System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload)), baseUri)
                : (Uri.UnescapeDataString(payload), baseUri);
        }

        Uri? mapUri = null;
        if (Uri.TryCreate(sourceMapUrl, UriKind.Absolute, out var absolute)) mapUri = absolute;
        else if (Uri.TryCreate(scriptUrl, UriKind.Absolute, out var scriptUri)) mapUri = new Uri(scriptUri, sourceMapUrl);

        if (mapUri?.IsFile == true) return (await System.IO.File.ReadAllTextAsync(mapUri.LocalPath), mapUri);
        if (mapUri?.Scheme is "http" or "https") return (await SourceMapHttpClient.GetStringAsync(mapUri), mapUri);

        var scriptPath = Uri.TryCreate(scriptUrl, UriKind.Absolute, out var fileScript) && fileScript.IsFile
            ? fileScript.LocalPath
            : scriptUrl;
        var path = System.IO.Path.GetFullPath(sourceMapUrl, System.IO.Path.GetDirectoryName(scriptPath) ?? "");
        return (await System.IO.File.ReadAllTextAsync(path), new Uri(path));
    }

    private static async Task<string> ReadSourceMapSectionAsync(Uri sectionUri, CancellationToken cancellationToken)
    {
        if (sectionUri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
        {
            var value = sectionUri.OriginalString;
            var comma = value.IndexOf(',');
            if (comma < 0) throw new FormatException("Invalid source-map section data URI.");
            var metadata = value[..comma];
            var payload = value[(comma + 1)..];
            return metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload))
                : Uri.UnescapeDataString(payload);
        }
        if (sectionUri.IsFile) return await System.IO.File.ReadAllTextAsync(sectionUri.LocalPath, cancellationToken);
        if (sectionUri.Scheme is "http" or "https") return await SourceMapHttpClient.GetStringAsync(sectionUri, cancellationToken);
        throw new NotSupportedException($"Unsupported source-map section URI scheme '{sectionUri.Scheme}'.");
    }

    private async Task ApplySourceMapBlackboxingAsync(string scriptId, V8SourceMap sourceMap)
    {
        var transitions = sourceMap.GetBlackboxedStateTransitions();
        if (transitions.Count == 0 || !_cdpService.IsConnected || !IsDebuggerEnabled) return;
        var positions = new JsonArray();
        foreach (var transition in transitions)
        {
            positions.Add((JsonNode)new JsonObject
            {
                ["lineNumber"] = transition.LineNumber,
                ["columnNumber"] = transition.ColumnNumber
            });
        }
        await TrySendOptionalDebuggerCommandAsync("Debugger.setBlackboxedRanges", new JsonObject
        {
            ["scriptId"] = scriptId,
            ["positions"] = positions
        });
    }

    private void HandleDebuggerPaused(JsonObject parameters)
    {
        Interlocked.Increment(ref _pauseGeneration);
        _ = ReleaseDebuggerObjectGroupsAsync();
        var frames = new System.Collections.Generic.List<V8CallFrameModel>();
        if (parameters["callFrames"] is JsonArray callFrames)
        {
            foreach (var frameNode in callFrames.OfType<JsonObject>())
            {
                var location = frameNode["location"] as JsonObject;
                var scopes = new System.Collections.Generic.List<V8ScopeModel>();
                if (frameNode["scopeChain"] is JsonArray scopeChain)
                {
                    var scopeIndex = 0;
                    foreach (var scopeNode in scopeChain.OfType<JsonObject>())
                    {
                        var remoteObject = scopeNode["object"] as JsonObject;
                        scopes.Add(new V8ScopeModel
                        {
                            Index = scopeIndex++,
                            Type = scopeNode["type"]?.GetValue<string>() ?? "",
                            Name = scopeNode["name"]?.GetValue<string>() ?? "",
                            ObjectId = remoteObject?["objectId"]?.GetValue<string>() ?? "",
                            Description = remoteObject?["description"]?.GetValue<string>() ?? ""
                        });
                    }
                }

                frames.Add(new V8CallFrameModel
                {
                    CallFrameId = frameNode["callFrameId"]?.GetValue<string>() ?? "",
                    FunctionName = frameNode["functionName"]?.GetValue<string>() ?? "",
                    Url = frameNode["url"]?.GetValue<string>() ?? "",
                    ScriptId = location?["scriptId"]?.GetValue<string>() ?? "",
                    LineNumber = location?["lineNumber"]?.GetValue<int>() ?? 0,
                    ColumnNumber = location?["columnNumber"]?.GetValue<int>() ?? 0,
                    ScopeChain = scopes
                });
            }
        }

        AppendAsyncStackFrames(parameters["asyncStackTrace"] as JsonObject, frames);

        var reason = parameters["reason"]?.GetValue<string>() ?? "other";
        Dispatcher.UIThread.Post(() =>
        {
            CallFrames.Clear();
            CallStack.Clear();
            foreach (var frame in frames)
            {
                CallFrames.Add(frame);
                CallStack.Add(frame.DisplayName);
            }
            PauseReason = reason;
            DebuggerStatusText = $"Paused: {reason}";
            IsDebuggerPaused = true;
            SelectedCallFrame = CallFrames.FirstOrDefault();
        });

        if (parameters["asyncStackTrace"] is null && parameters["asyncStackTraceId"] is JsonObject asyncStackTraceId)
        {
            _ = LoadExternalAsyncStackTraceAsync((JsonObject)asyncStackTraceId.DeepClone());
        }
    }

    private static void AppendAsyncStackFrames(JsonObject? stackTrace, System.Collections.Generic.ICollection<V8CallFrameModel> destination)
    {
        var current = stackTrace;
        while (current is not null)
        {
            var description = current["description"]?.GetValue<string>() ?? "continuation";
            destination.Add(new V8CallFrameModel
            {
                IsAsyncBoundary = true,
                IsAsyncFrame = true,
                AsyncDescription = description
            });
            if (current["callFrames"] is JsonArray callFrames)
            {
                foreach (var frame in callFrames.OfType<JsonObject>())
                {
                    destination.Add(new V8CallFrameModel
                    {
                        FunctionName = frame["functionName"]?.GetValue<string>() ?? "",
                        Url = frame["url"]?.GetValue<string>() ?? "",
                        ScriptId = frame["scriptId"]?.GetValue<string>() ?? "",
                        LineNumber = frame["lineNumber"]?.GetValue<int>() ?? 0,
                        ColumnNumber = frame["columnNumber"]?.GetValue<int>() ?? 0,
                        IsAsyncFrame = true,
                        AsyncDescription = description
                    });
                }
            }
            current = current["parent"] as JsonObject;
        }
    }

    private async Task LoadExternalAsyncStackTraceAsync(JsonObject stackTraceId)
    {
        try
        {
            var frames = new System.Collections.Generic.List<V8CallFrameModel>();
            JsonObject? currentId = stackTraceId;
            while (currentId is not null)
            {
                var response = await _cdpService.SendCommandAsync("Debugger.getStackTrace", new JsonObject
                {
                    ["stackTraceId"] = currentId
                });
                var stackTrace = response["stackTrace"] as JsonObject;
                AppendAsyncStackFrames(stackTrace, frames);
                currentId = stackTrace?["parentId"] is JsonObject parentId
                    ? (JsonObject)parentId.DeepClone()
                    : null;
            }
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsDebuggerPaused) return;
                foreach (var frame in frames)
                {
                    CallFrames.Add(frame);
                    CallStack.Add(frame.DisplayName);
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Unable to resolve external async stack trace");
        }
    }

    private void HandleBreakpointResolved(JsonObject parameters)
    {
        var breakpointId = parameters["breakpointId"]?.GetValue<string>() ?? "";
        var location = parameters["location"] as JsonObject;
        Dispatcher.UIThread.Post(() =>
        {
            var breakpoint = V8Breakpoints.FirstOrDefault(item => item.BreakpointId == breakpointId);
            if (breakpoint is null) return;
            breakpoint.IsResolved = true;
            breakpoint.ResolvedLineNumber = location?["lineNumber"]?.GetValue<int>();
            breakpoint.ResolvedColumnNumber = location?["columnNumber"]?.GetValue<int>();
            RefreshLegacyBreakpoints();
        });
    }

    private async Task LoadRuntimeScriptSourceAsync(V8ScriptModel script)
    {
        if (!_cdpService.IsConnected || !IsDebuggerEnabled) return;
        IsLoadingContent = true;
        SelectedFileName = script.DisplayName;
        SelectedFileContent = "Loading runtime source...";
        try
        {
            if (script.IsOriginalSource)
            {
                SelectedFileContent = script.SourceContent ?? await ReadOriginalSourceAsync(script.Url);
                return;
            }
            var response = await _cdpService.SendCommandAsync("Debugger.getScriptSource", new JsonObject
            {
                ["scriptId"] = script.ScriptId
            });
            if (SelectedRuntimeScript?.ScriptId == script.ScriptId)
            {
                SelectedFileContent = response["scriptSource"]?.GetValue<string>() ?? "";
            }
        }
        catch (Exception ex)
        {
            SelectedFileContent = $"Unable to load script source: {ex.Message}";
            Logger.LogErrorMessage("SourcesVM", "Get script source failed", ex);
        }
        finally
        {
            IsLoadingContent = false;
        }
    }

    private static async Task<string> ReadOriginalSourceAsync(string sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri))
        {
            return "Source map does not embed this original source and its URL cannot be resolved.";
        }
        if (sourceUri.IsFile) return await System.IO.File.ReadAllTextAsync(sourceUri.LocalPath);
        if (sourceUri.Scheme is "http" or "https") return await SourceMapHttpClient.GetStringAsync(sourceUri);
        if (sourceUri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
        {
            var value = sourceUri.OriginalString;
            var comma = value.IndexOf(',');
            if (comma < 0) throw new FormatException("Invalid original-source data URI.");
            var metadata = value[..comma];
            var payload = value[(comma + 1)..];
            return metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload))
                : Uri.UnescapeDataString(payload);
        }
        return $"Source map does not embed this original source; unsupported URI scheme '{sourceUri.Scheme}'.";
    }

    private async Task NavigateToCallFrameAsync(V8CallFrameModel frame)
    {
        foreach (var original in RuntimeScripts.Where(item => item.IsOriginalSource && item.GeneratedScriptId == frame.ScriptId))
        {
            var mapped = original.SourceMap?.FindOriginalLocation(frame.LineNumber, frame.ColumnNumber);
            if (mapped?.SourceIndex != original.SourceIndex) continue;
            SelectedRuntimeScript = original;
            ActiveDebugLine = mapped.OriginalLine + 1;
            PendingScrollLine = mapped.OriginalLine + 1;
            return;
        }

        var script = RuntimeScripts.FirstOrDefault(item => item.ScriptId == frame.ScriptId);
        if (script is not null)
        {
            SelectedRuntimeScript = script;
            ActiveDebugLine = frame.LineNumber + 1;
            PendingScrollLine = frame.LineNumber + 1;
            return;
        }

        var fileNode = FindFileBySuffix(frame.Url);
        if (fileNode is not null)
        {
            SelectedFile = fileNode;
            ActiveDebugLine = frame.LineNumber + 1;
            PendingScrollLine = frame.LineNumber + 1;
        }
        await Task.CompletedTask;
    }

    private async Task LoadScopesForFrameAsync(V8CallFrameModel? frame)
    {
        if (frame is null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Scopes.Clear();
                ScopeVariables.Clear();
                SelectedScopeVariable = null;
            });
            return;
        }

        var generation = Volatile.Read(ref _pauseGeneration);
        var variables = new System.Collections.Generic.List<V8ScopeVariableModel>();
        foreach (var scope in frame.ScopeChain)
        {
            if (string.IsNullOrWhiteSpace(scope.ObjectId)) continue;
            var group = new V8ScopeVariableModel
            {
                ScopeType = scope.Type,
                ScopeNumber = scope.Index,
                Name = GetScopeGroupName(scope),
                ObjectId = scope.ObjectId,
                IsScopeGroup = true,
                Depth = -1,
                PauseGeneration = generation,
                AncestorObjectIds = new HashSet<string>(StringComparer.Ordinal),
                Value = ""
            };
            group.ConfigureChildrenLoader(LoadVariableChildrenAsync);
            variables.Add(group);
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (generation != Volatile.Read(ref _pauseGeneration) || SelectedCallFrame?.CallFrameId != frame.CallFrameId) return;
            Scopes.Clear();
            ScopeVariables.Clear();
            SelectedScopeVariable = null;
            foreach (var scope in frame.ScopeChain)
            {
                Scopes.Add(scope);
                scope.Properties.Clear();
            }
            foreach (var variable in variables) ScopeVariables.Add(variable);
        });
    }

    private async Task<IReadOnlyList<V8ScopeVariableModel>> LoadVariableChildrenAsync(V8ScopeVariableModel parent)
    {
        if (!_cdpService.IsConnected || !IsDebuggerPaused || string.IsNullOrWhiteSpace(parent.ObjectId) ||
            parent.PauseGeneration != Volatile.Read(ref _pauseGeneration))
        {
            return Array.Empty<V8ScopeVariableModel>();
        }

        var response = await _cdpService.SendCommandAsync("Runtime.getProperties", new JsonObject
        {
            ["objectId"] = parent.ObjectId,
            ["ownProperties"] = true,
            ["accessorPropertiesOnly"] = false,
            ["generatePreview"] = true,
            ["nonIndexedPropertiesOnly"] = false
        });
        if (parent.PauseGeneration != Volatile.Read(ref _pauseGeneration)) return Array.Empty<V8ScopeVariableModel>();
        return await CreateVariableNodesAsync(
            response,
            parent.ScopeType,
            parent.ScopeNumber,
            isNested: true,
            parent.Depth + 1,
            canSetVariables: parent.IsScopeGroup,
            parent.PauseGeneration,
            parent.AncestorObjectIds);
    }

    private Task<IReadOnlyList<V8ScopeVariableModel>> CreateVariableNodesAsync(
        JsonObject response,
        string scopeType,
        int scopeNumber,
        bool isNested,
        int depth,
        bool canSetVariables,
        int generation,
        IReadOnlySet<string> ancestors)
    {
        if (response["exceptionDetails"] is JsonObject exception)
        {
            throw new InvalidOperationException(exception["text"]?.GetValue<string>() ?? "Property inspection failed.");
        }

        var nodes = new System.Collections.Generic.List<V8ScopeVariableModel>();
        AddVariableDescriptors(response["result"] as JsonArray, isPrivate: false, isInternal: false);
        AddVariableDescriptors(response["privateProperties"] as JsonArray, isPrivate: true, isInternal: false);
        AddVariableDescriptors(response["internalProperties"] as JsonArray, isPrivate: false, isInternal: true);
        return Task.FromResult<IReadOnlyList<V8ScopeVariableModel>>(nodes);

        void AddVariableDescriptors(JsonArray? descriptors, bool isPrivate, bool isInternal)
        {
            if (descriptors is null) return;
            foreach (var descriptor in descriptors.OfType<JsonObject>())
            {
                nodes.Add(CreateVariableNode(
                    descriptor,
                    scopeType,
                    scopeNumber,
                    isNested,
                    depth,
                    canSetVariables,
                    isPrivate,
                    isInternal,
                    generation,
                    ancestors));
            }
        }
    }

    private V8ScopeVariableModel CreateVariableNode(
        JsonObject descriptor,
        string scopeType,
        int scopeNumber,
        bool isNested,
        int depth,
        bool canSetVariables,
        bool isPrivate,
        bool isInternal,
        int generation,
        IReadOnlySet<string> ancestors)
    {
        var value = descriptor["value"] as JsonObject;
        var getter = descriptor["get"] as JsonObject;
        var isAccessor = value is null && getter is not null;
        var objectId = value?["objectId"]?.GetValue<string>() ?? "";
        // RemoteObjectId values are opaque and some V8 targets issue a new handle
        // for the same object. Use handle equality when available; the depth cap
        // below keeps differently handled cycles finite without injecting identity
        // functions into a live paused target.
        var isCircular = isNested && objectId.Length > 0 && ancestors.Contains(objectId);
        var isDepthLimited = objectId.Length > 0 && depth >= MaximumVariableDepth;
        var childAncestors = new HashSet<string>(ancestors, StringComparer.Ordinal);
        if (objectId.Length > 0) childAncestors.Add(objectId);

        var node = new V8ScopeVariableModel
        {
            ScopeType = scopeType,
            ScopeNumber = scopeNumber,
            Name = descriptor["name"]?.GetValue<string>() ?? "",
            Type = isAccessor ? "accessor" : value?["type"]?.GetValue<string>() ?? "undefined",
            Subtype = value?["subtype"]?.GetValue<string>() ?? "",
            ObjectId = objectId,
            Writable = canSetVariables && !isAccessor && (descriptor["writable"]?.GetValue<bool>() ?? false),
            IsNested = isNested,
            IsAccessor = isAccessor,
            IsCircular = isCircular,
            IsDepthLimited = isDepthLimited,
            Depth = depth,
            IsPrivate = isPrivate,
            IsInternal = isInternal,
            PauseGeneration = generation,
            AncestorObjectIds = childAncestors,
            Value = isCircular ? "[Circular]" : isDepthLimited ? "[Maximum depth]" : isAccessor ? "(…)" : FormatRemoteObject(value)
        };
        if (objectId.Length > 0 && !isCircular && !isDepthLimited) node.ConfigureChildrenLoader(LoadVariableChildrenAsync);
        return node;
    }

    private static string GetScopeGroupName(V8ScopeModel scope)
    {
        var label = scope.Type switch
        {
            "local" => "Local",
            "closure" => "Closure",
            "catch" => "Catch",
            "block" => "Block",
            "script" => "Script",
            "with" => "With",
            "module" => "Module",
            "wasm-expression-stack" => "Wasm Expression Stack",
            "global" => "Global",
            _ when scope.Type.Length > 0 => char.ToUpperInvariant(scope.Type[0]) + scope.Type[1..],
            _ => "Scope"
        };
        return string.IsNullOrWhiteSpace(scope.Name) ? label : $"{label}: {scope.Name}";
    }

    private static string FormatRemoteObject(JsonObject? value)
    {
        if (value is null) return "undefined";
        if (value["unserializableValue"] is JsonNode unserializable) return unserializable.ToString();
        if (value["value"] is JsonNode primitive) return primitive.ToJsonString().Trim('"');
        return value["description"]?.GetValue<string>() ?? value["type"]?.GetValue<string>() ?? "undefined";
    }

    private async Task EvaluateOnCallFrameAsync()
    {
        var frame = SelectedCallFrame;
        if (frame is null || string.IsNullOrWhiteSpace(DebuggerEvaluationExpression)) return;
        try
        {
            var response = await _cdpService.SendCommandAsync("Debugger.evaluateOnCallFrame", new JsonObject
            {
                ["callFrameId"] = frame.CallFrameId,
                ["expression"] = DebuggerEvaluationExpression,
                ["objectGroup"] = DebugConsoleObjectGroup,
                ["includeCommandLineAPI"] = true,
                ["silent"] = false,
                ["returnByValue"] = false,
                ["generatePreview"] = true,
                ["throwOnSideEffect"] = false
            });
            DebuggerEvaluationResult = response["exceptionDetails"] is JsonObject exception
                ? exception["text"]?.GetValue<string>() ?? "Evaluation failed"
                : FormatRemoteObject(response["result"] as JsonObject);
        }
        catch (Exception ex)
        {
            DebuggerEvaluationResult = ex.Message;
            Logger.LogErrorMessage("SourcesVM", "Call-frame evaluation failed", ex);
        }
        finally
        {
            await TryReleaseObjectGroupAsync(DebugConsoleObjectGroup);
        }
    }

    public async Task<string?> EvaluateHoverAsync(string expression)
    {
        var frame = SelectedCallFrame;
        if (!_cdpService.IsConnected || !IsDebuggerPaused || frame?.CanInspect != true || string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        try
        {
            var response = await _cdpService.SendCommandAsync("Debugger.evaluateOnCallFrame", new JsonObject
            {
                ["callFrameId"] = frame.CallFrameId,
                ["expression"] = expression,
                ["objectGroup"] = HoverObjectGroup,
                ["includeCommandLineAPI"] = false,
                ["silent"] = true,
                ["returnByValue"] = false,
                ["generatePreview"] = true,
                ["throwOnSideEffect"] = true
            });
            if (response["exceptionDetails"] is JsonObject) return null;
            return FormatRemoteObject(response["result"] as JsonObject);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Side-effect-free hover evaluation failed for {Expression}", expression);
            return null;
        }
        finally
        {
            await TryReleaseObjectGroupAsync(HoverObjectGroup);
        }
    }

    private async Task SetVariableValueAsync()
    {
        var frame = SelectedCallFrame;
        var variable = SelectedScopeVariable;
        var expression = NewVariableValueExpression.Trim();
        if (frame?.CanInspect != true || variable?.Writable != true || expression.Length == 0) return;

        try
        {
            var evaluation = await _cdpService.SendCommandAsync("Debugger.evaluateOnCallFrame", new JsonObject
            {
                ["callFrameId"] = frame.CallFrameId,
                ["expression"] = expression,
                ["objectGroup"] = VariableEditObjectGroup,
                ["includeCommandLineAPI"] = true,
                ["silent"] = false,
                ["returnByValue"] = false,
                ["generatePreview"] = true,
                ["throwOnSideEffect"] = false
            });
            if (evaluation["exceptionDetails"] is JsonObject exception)
            {
                DebuggerEvaluationResult = exception["text"]?.GetValue<string>() ?? "Variable value evaluation failed";
                return;
            }

            var remoteObject = evaluation["result"] as JsonObject;
            var newValue = CreateCallArgument(remoteObject);
            await _cdpService.SendCommandAsync("Debugger.setVariableValue", new JsonObject
            {
                ["scopeNumber"] = variable.ScopeNumber,
                ["variableName"] = variable.Name,
                ["newValue"] = newValue,
                ["callFrameId"] = frame.CallFrameId
            });
            variable.Value = FormatRemoteObject(remoteObject);
            DebuggerEvaluationResult = $"{variable.Name} = {variable.Value}";
            NewVariableValueExpression = "";
            await LoadScopesForFrameAsync(frame);
            await RefreshWatchExpressionsAsync();
        }
        catch (Exception ex)
        {
            DebuggerEvaluationResult = ex.Message;
            Logger.LogErrorMessage("SourcesVM", $"Unable to set variable {variable.Name}", ex);
        }
        finally
        {
            await TryReleaseObjectGroupAsync(VariableEditObjectGroup);
        }
    }

    private static JsonObject CreateCallArgument(JsonObject? remoteObject)
    {
        if (remoteObject is null) return new JsonObject { ["unserializableValue"] = "undefined" };
        if (remoteObject["objectId"] is JsonNode objectId)
        {
            return new JsonObject { ["objectId"] = objectId.GetValue<string>() };
        }
        if (remoteObject["unserializableValue"] is JsonNode unserializable)
        {
            return new JsonObject { ["unserializableValue"] = unserializable.GetValue<string>() };
        }
        if (remoteObject.TryGetPropertyValue("value", out var value))
        {
            return new JsonObject { ["value"] = value?.DeepClone() };
        }
        return new JsonObject { ["unserializableValue"] = "undefined" };
    }

    private async Task ApplyBlackboxPatternsAsync()
    {
        if (!_cdpService.IsConnected || !IsDebuggerEnabled) return;
        var patterns = new JsonArray();
        foreach (var pattern in BlackboxPatterns) patterns.Add((JsonNode?)JsonValue.Create(pattern));
        try
        {
            await _cdpService.SendCommandAsync("Debugger.setBlackboxPatterns", new JsonObject
            {
                ["patterns"] = patterns,
                ["skipAnonymous"] = SkipAnonymousScripts
            });
            BlackboxStatusText = BlackboxPatterns.Count == 0 && !SkipAnonymousScripts
                ? "No scripts ignored"
                : $"Ignoring {BlackboxPatterns.Count} URL pattern(s){(SkipAnonymousScripts ? " + anonymous scripts" : "")}";
        }
        catch (Exception ex)
        {
            BlackboxStatusText = "Blackboxing unsupported by target";
            Logger.LogDebug(ex, "Target does not support Debugger.setBlackboxPatterns");
        }
    }

    private async Task AddWatchExpressionAsync()
    {
        var expression = NewWatchExpression.Trim();
        if (string.IsNullOrWhiteSpace(expression)) return;
        var watch = WatchExpressions.FirstOrDefault(item => string.Equals(item.Expression, expression, StringComparison.Ordinal));
        if (watch is null)
        {
            watch = new V8WatchExpressionModel { Expression = expression };
            WatchExpressions.Add(watch);
        }
        NewWatchExpression = "";
        ((RelayCommand)RefreshWatchExpressionsCommand).RaiseCanExecuteChanged();
        await EvaluateWatchExpressionAsync(watch);
    }

    private async Task RefreshWatchExpressionsAsync()
    {
        foreach (var watch in WatchExpressions.ToArray())
        {
            await EvaluateWatchExpressionAsync(watch);
        }
    }

    private async Task EvaluateWatchExpressionAsync(V8WatchExpressionModel watch)
    {
        var frame = SelectedCallFrame;
        if (!_cdpService.IsConnected || !IsDebuggerPaused || frame is null)
        {
            watch.Value = "Not paused";
            return;
        }

        try
        {
            var response = await _cdpService.SendCommandAsync("Debugger.evaluateOnCallFrame", new JsonObject
            {
                ["callFrameId"] = frame.CallFrameId,
                ["expression"] = watch.Expression,
                ["objectGroup"] = WatchObjectGroup,
                ["includeCommandLineAPI"] = true,
                ["silent"] = true,
                ["returnByValue"] = false,
                ["generatePreview"] = true,
                ["throwOnSideEffect"] = false
            });
            watch.Value = response["exceptionDetails"] is JsonObject exception
                ? exception["text"]?.GetValue<string>() ?? "Evaluation failed"
                : FormatRemoteObject(response["result"] as JsonObject);
        }
        catch (Exception ex)
        {
            watch.Value = ex.Message;
        }
        finally
        {
            await TryReleaseObjectGroupAsync(WatchObjectGroup);
        }
    }

    private async Task ReleaseDebuggerObjectGroupsAsync()
    {
        await TryReleaseObjectGroupAsync(DebugConsoleObjectGroup);
        await TryReleaseObjectGroupAsync(HoverObjectGroup);
        await TryReleaseObjectGroupAsync(VariableEditObjectGroup);
        await TryReleaseObjectGroupAsync(WatchObjectGroup);
    }

    private async Task TryReleaseObjectGroupAsync(string objectGroup)
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("Runtime.releaseObjectGroup", new JsonObject
            {
                ["objectGroup"] = objectGroup
            });
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Unable to release V8 object group {ObjectGroup}", objectGroup);
        }
    }

    private async Task RestartFrameAsync()
    {
        var frame = SelectedCallFrame;
        if (frame is null) return;
        try
        {
            var response = await _cdpService.SendCommandAsync("Debugger.restartFrame", new JsonObject
            {
                ["callFrameId"] = frame.CallFrameId,
                ["mode"] = "StepInto"
            });
            if (response["callFrames"] is JsonArray callFrames)
            {
                HandleDebuggerPaused(new JsonObject
                {
                    ["reason"] = "restartFrame",
                    ["callFrames"] = callFrames.DeepClone()
                });
            }
        }
        catch (Exception ex)
        {
            DebuggerEvaluationResult = $"Restart frame failed: {ex.Message}";
            Logger.LogErrorMessage("SourcesVM", "Restart frame failed", ex);
        }
    }

    private async Task PauseAsync()
    {
        if (!_cdpService.IsConnected || !IsDebuggerEnabled) return;
        try
        {
            await _cdpService.SendCommandAsync("Debugger.pause");
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SourcesVM", "Pause failed", ex);
        }
    }

    private async Task SetPauseOnExceptionsAsync(string state)
    {
        if (!_cdpService.IsConnected || !IsDebuggerEnabled) return;
        try
        {
            await _cdpService.SendCommandAsync("Debugger.setPauseOnExceptions", new JsonObject { ["state"] = state });
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("SourcesVM", "Set pause on exceptions failed", ex);
        }
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
            Logger.LogErrorMessage("SourcesVM", "Resume failed", ex);
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
            Logger.LogErrorMessage("SourcesVM", "StepOver failed", ex);
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
            Logger.LogErrorMessage("SourcesVM", "StepInto failed", ex);
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
            Logger.LogErrorMessage("SourcesVM", "StepOut failed", ex);
        }
    }

    public async Task ToggleBreakpointAsync(int line)
    {
        if (!_cdpService.IsConnected || !IsDebuggerEnabled ||
            ((SelectedFile == null || SelectedFile.IsDirectory) && SelectedRuntimeScript == null))
        {
            return;
        }

        var script = SelectedRuntimeScript;
        string displayUrl = script?.Url ?? SelectedFile?.Path ?? "";
        string bindingUrl = script?.IsOriginalSource == true ? script.GeneratedUrl : displayUrl;
        string scriptId = script?.ScriptId ?? "";
        int cdpLine = Math.Max(0, line - 1);
        int cdpColumn = 0;
        if (script?.IsOriginalSource == true)
        {
            var generated = script.SourceMap?.FindGeneratedLocation(script.SourceIndex, cdpLine);
            if (generated is null) return;
            scriptId = script.GeneratedScriptId;
            cdpLine = generated.GeneratedLine;
            cdpColumn = generated.GeneratedColumn;
        }
        string key = CreateBreakpointKey(displayUrl, Math.Max(0, line - 1));

        var existing = V8Breakpoints.FirstOrDefault(item => item.Key == key);
        if (existing is not null)
        {
            await RemoveBreakpointDefinitionAsync(existing);
            return;
        }

        var breakpoint = new V8BreakpointModel
        {
            Key = key,
            ScriptId = scriptId,
            Url = displayUrl,
            BindingUrl = bindingUrl,
            LineNumber = cdpLine,
            ColumnNumber = cdpColumn,
            DisplayLineNumber = script?.IsOriginalSource == true ? Math.Max(0, line - 1) : null,
            Kind = BreakpointKind,
            Condition = BreakpointCondition.Trim(),
            LogMessage = BreakpointLogMessage.Trim(),
            IsEnabled = true
        };
        V8Breakpoints.Add(breakpoint);
        SelectedBreakpoint = breakpoint;
        RefreshLegacyBreakpoints();
        await BindBreakpointAsync(breakpoint);
    }

    private async Task RestoreBreakpointBindingsAsync()
    {
        foreach (var breakpoint in V8Breakpoints.ToArray())
        {
            breakpoint.BreakpointId = "";
            breakpoint.IsResolved = false;
            breakpoint.ResolvedLineNumber = null;
            breakpoint.ResolvedColumnNumber = null;
            if (breakpoint.IsEnabled) await BindBreakpointAsync(breakpoint);
        }
        RefreshLegacyBreakpoints();
    }

    private async Task BindBreakpointAsync(V8BreakpointModel breakpoint)
    {
        if (!_cdpService.IsConnected || !IsDebuggerEnabled || !breakpoint.IsEnabled) return;
        if (string.IsNullOrWhiteSpace(breakpoint.BindingUrl) && string.IsNullOrWhiteSpace(breakpoint.ScriptId)) return;

        try
        {
            JsonObject parameters;
            string method;
            if (!string.IsNullOrWhiteSpace(breakpoint.BindingUrl))
            {
                method = "Debugger.setBreakpointByUrl";
                parameters = new JsonObject
                {
                    ["url"] = breakpoint.BindingUrl,
                    ["lineNumber"] = breakpoint.LineNumber,
                    ["columnNumber"] = breakpoint.ColumnNumber
                };
            }
            else
            {
                method = "Debugger.setBreakpoint";
                parameters = new JsonObject
                {
                    ["location"] = new JsonObject
                    {
                        ["scriptId"] = breakpoint.ScriptId,
                        ["lineNumber"] = breakpoint.LineNumber,
                        ["columnNumber"] = breakpoint.ColumnNumber
                    }
                };
            }

            var condition = GetProtocolBreakpointCondition(breakpoint);
            if (!string.IsNullOrWhiteSpace(condition)) parameters["condition"] = condition;

            var response = await _cdpService.SendCommandAsync(method, parameters);
            var resolvedLocation = response["actualLocation"] as JsonObject ??
                (response["locations"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault();
            breakpoint.BreakpointId = response["breakpointId"]?.GetValue<string>() ?? breakpoint.Key;
            breakpoint.IsResolved = resolvedLocation is not null;
            breakpoint.ResolvedLineNumber = resolvedLocation?["lineNumber"]?.GetValue<int>();
            breakpoint.ResolvedColumnNumber = resolvedLocation?["columnNumber"]?.GetValue<int>();
            RefreshLegacyBreakpoints();
        }
        catch (Exception ex)
        {
            breakpoint.BreakpointId = "";
            breakpoint.IsResolved = false;
            Logger.LogErrorMessage("SourcesVM", $"Unable to bind breakpoint {breakpoint.DisplayName}", ex);
        }
    }

    private async Task UnbindBreakpointAsync(V8BreakpointModel breakpoint)
    {
        var breakpointId = breakpoint.BreakpointId;
        breakpoint.BreakpointId = "";
        breakpoint.IsResolved = false;
        breakpoint.ResolvedLineNumber = null;
        breakpoint.ResolvedColumnNumber = null;
        if (!_cdpService.IsConnected || string.IsNullOrWhiteSpace(breakpointId)) return;

        try
        {
            await _cdpService.SendCommandAsync("Debugger.removeBreakpoint", new JsonObject
            {
                ["breakpointId"] = breakpointId
            });
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Unable to remove target breakpoint {BreakpointId}", breakpointId);
        }
    }

    private async Task SetBreakpointEnabledAsync(V8BreakpointModel breakpoint, bool isEnabled)
    {
        if (breakpoint.IsEnabled == isEnabled) return;
        breakpoint.IsEnabled = isEnabled;
        if (isEnabled) await BindBreakpointAsync(breakpoint);
        else await UnbindBreakpointAsync(breakpoint);
        RefreshLegacyBreakpoints();
    }

    private async Task UpdateSelectedBreakpointAsync()
    {
        var breakpoint = SelectedBreakpoint;
        if (breakpoint is null) return;
        await UnbindBreakpointAsync(breakpoint);
        breakpoint.Kind = BreakpointKind;
        breakpoint.Condition = BreakpointCondition.Trim();
        breakpoint.LogMessage = BreakpointLogMessage.Trim();
        if (breakpoint.IsEnabled) await BindBreakpointAsync(breakpoint);
        RefreshLegacyBreakpoints();
    }

    private async Task RemoveBreakpointDefinitionAsync(V8BreakpointModel breakpoint)
    {
        await UnbindBreakpointAsync(breakpoint);
        V8Breakpoints.Remove(breakpoint);
        if (ReferenceEquals(SelectedBreakpoint, breakpoint)) SelectedBreakpoint = null;
        RefreshLegacyBreakpoints();
    }

    private async Task SetBreakpointsActiveAsync(bool active)
    {
        await TrySendOptionalDebuggerCommandAsync("Debugger.setBreakpointsActive", new JsonObject
        {
            ["active"] = active
        });
    }

    private static string GetProtocolBreakpointCondition(V8BreakpointModel breakpoint) => breakpoint.Kind switch
    {
        V8BreakpointKinds.Conditional => breakpoint.Condition,
        V8BreakpointKinds.Logpoint => BuildLogpointCondition(breakpoint.LogMessage),
        _ => ""
    };

    internal static string BuildLogpointCondition(string message)
    {
        var arguments = new System.Collections.Generic.List<string>();
        var literal = new StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length == 0) return;
            arguments.Add(ToJavaScriptStringLiteral(literal.ToString()));
            literal.Clear();
        }

        for (var index = 0; index < message.Length; index++)
        {
            var character = message[index];
            if (character == '{' && index + 1 < message.Length && message[index + 1] == '{')
            {
                literal.Append('{');
                index++;
                continue;
            }
            if (character == '}' && index + 1 < message.Length && message[index + 1] == '}')
            {
                literal.Append('}');
                index++;
                continue;
            }
            if (character == '{')
            {
                var close = message.IndexOf('}', index + 1);
                if (close > index + 1)
                {
                    var expression = message[(index + 1)..close].Trim();
                    if (expression.Length > 0)
                    {
                        FlushLiteral();
                        arguments.Add($"({expression})");
                        index = close;
                        continue;
                    }
                }
            }
            literal.Append(character);
        }
        FlushLiteral();
        return $"console.log({string.Join(", ", arguments)}), false";
    }

    private static string ToJavaScriptStringLiteral(string value) =>
        $"\"{JsonEncodedText.Encode(value)}\"";

    private static string CreateBreakpointKey(string url, int zeroBasedLine) => $"{url}:{zeroBasedLine}";

    private void RefreshLegacyBreakpoints()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshLegacyBreakpoints);
            return;
        }
        Breakpoints.Clear();
        foreach (var breakpoint in V8Breakpoints) Breakpoints.Add(breakpoint.DisplayName);
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
            Logger.LogErrorMessage("SourcesVM", "Save file failed", ex);
        }
        finally
        {
            _isSaving = false;
        }
    }

    public async Task ApplySourceChangesAsync(string content)
    {
        if (!_cdpService.IsConnected) return;

        if (SelectedFile is not null && !SelectedFile.IsDirectory)
        {
            await SaveFileAsync(content);
            LiveEditStatus = "Saved";
            return;
        }

        var script = SelectedRuntimeScript;
        if (script is null) return;

        var generatedScriptId = script.IsOriginalSource ? script.GeneratedScriptId : script.ScriptId;
        if (string.IsNullOrWhiteSpace(generatedScriptId)) return;

        LiveEditStatus = "Applying live edit...";
        try
        {
            var currentSourceResponse = await _cdpService.SendCommandAsync("Debugger.getScriptSource", new JsonObject
            {
                ["scriptId"] = generatedScriptId
            });
            var previousGeneratedSource = currentSourceResponse["scriptSource"]?.GetValue<string>() ?? "";
            var nextGeneratedSource = content;
            V8SourceMap? previousSourceMap = null;
            V8SourceMap? updatedSourceMap = null;
            var previousOriginalSource = script.SourceContent;
            if (script.IsOriginalSource)
            {
                previousSourceMap = script.SourceMap;
                if (previousSourceMap is null)
                {
                    LiveEditStatus = "Live edit unavailable: source map is missing";
                    return;
                }
                var originalSource = previousOriginalSource ?? await ReadOriginalSourceAsync(script.Url);
                var mutation = SourceMutationEngine.CreatePatch(
                    previousSourceMap,
                    script.SourceIndex,
                    originalSource,
                    content,
                    previousGeneratedSource);
                if (!mutation.CanApply)
                {
                    LiveEditStatus = $"Live edit unavailable: {mutation.Message}";
                    return;
                }
                if (!mutation.HasChanges)
                {
                    LiveEditStatus = "Source unchanged";
                    return;
                }
                nextGeneratedSource = mutation.GeneratedSource;
                updatedSourceMap = mutation.UpdatedSourceMap;
            }

            var validation = await SetScriptSourceAsync(generatedScriptId, nextGeneratedSource, dryRun: true);
            var validationFailure = GetLiveEditFailure(validation);
            if (validationFailure is not null)
            {
                LiveEditStatus = $"Live edit validation failed: {validationFailure}";
                return;
            }

            var response = await SetScriptSourceAsync(generatedScriptId, nextGeneratedSource, dryRun: false);
            var applyFailure = GetLiveEditFailure(response);
            if (applyFailure is not null)
            {
                LiveEditStatus = $"Live edit failed: {applyFailure}";
                return;
            }

            if (script.IsOriginalSource && updatedSourceMap is not null)
            {
                foreach (var original in RuntimeScripts.Where(item =>
                    item.IsOriginalSource && item.GeneratedScriptId == generatedScriptId &&
                    ReferenceEquals(item.SourceMap, previousSourceMap)))
                {
                    original.SourceMap = updatedSourceMap;
                    if (original.SourceIndex == script.SourceIndex) original.SourceContent = content;
                }
            }

            var rebound = await RebindBreakpointsAfterLiveEditAsync(generatedScriptId, script.GeneratedUrl.Length > 0
                ? script.GeneratedUrl
                : script.Url);
            if (!rebound)
            {
                await SetScriptSourceAsync(generatedScriptId, previousGeneratedSource, dryRun: false);
                if (script.IsOriginalSource && previousSourceMap is not null)
                {
                    foreach (var original in RuntimeScripts.Where(item =>
                        item.IsOriginalSource && item.GeneratedScriptId == generatedScriptId &&
                        ReferenceEquals(item.SourceMap, updatedSourceMap)))
                    {
                        original.SourceMap = previousSourceMap;
                        if (original.SourceIndex == script.SourceIndex) original.SourceContent = previousOriginalSource;
                    }
                }
                await RebindBreakpointsAfterLiveEditAsync(generatedScriptId,
                    script.GeneratedUrl.Length > 0 ? script.GeneratedUrl : script.Url);
                LiveEditStatus = "Live edit rolled back: a breakpoint could not be rebound";
                return;
            }

            LiveEditStatus = script.IsOriginalSource
                ? "Source-mapped live edit applied"
                : "Live edit applied";
            SelectedFileContent = content;

            if (response["callFrames"] is JsonArray callFrames)
            {
                HandleDebuggerPaused(new JsonObject
                {
                    ["reason"] = "liveEdit",
                    ["callFrames"] = callFrames.DeepClone()
                });
            }
        }
        catch (Exception ex)
        {
            LiveEditStatus = $"Live edit failed: {ex.Message}";
            Logger.LogErrorMessage("SourcesVM", "Set script source failed", ex);
        }
    }

    private Task<JsonObject> SetScriptSourceAsync(string scriptId, string source, bool dryRun) =>
        _cdpService.SendCommandAsync("Debugger.setScriptSource", new JsonObject
        {
            ["scriptId"] = scriptId,
            ["scriptSource"] = source,
            ["dryRun"] = dryRun,
            ["allowTopFrameEditing"] = true
        });

    private static string? GetLiveEditFailure(JsonObject response)
    {
        if (response["exceptionDetails"] is JsonObject exception)
        {
            return exception["text"]?.GetValue<string>() ?? "V8 rejected the source";
        }
        var status = response["status"]?.GetValue<string>() ?? "Ok";
        return status == "Ok" ? null : status;
    }

    private async Task<bool> RebindBreakpointsAfterLiveEditAsync(string generatedScriptId, string generatedUrl)
    {
        var affected = V8Breakpoints.Where(breakpoint =>
            breakpoint.ScriptId == generatedScriptId ||
            string.Equals(breakpoint.BindingUrl, generatedUrl, StringComparison.Ordinal)).ToArray();
        foreach (var breakpoint in affected)
        {
            var original = RuntimeScripts.FirstOrDefault(item => item.IsOriginalSource &&
                item.GeneratedScriptId == generatedScriptId && string.Equals(item.Url, breakpoint.Url, StringComparison.Ordinal));
            if (original?.SourceMap is not null)
            {
                var mapped = original.SourceMap.FindGeneratedLocation(
                    original.SourceIndex,
                    breakpoint.DisplayLineNumber ?? breakpoint.LineNumber,
                    0);
                if (mapped is null) return false;
                breakpoint.LineNumber = mapped.GeneratedLine;
                breakpoint.ColumnNumber = mapped.GeneratedColumn;
            }
            await UnbindBreakpointAsync(breakpoint);
            if (breakpoint.IsEnabled) await BindBreakpointAsync(breakpoint);
        }
        return affected.All(breakpoint => !breakpoint.IsEnabled || !string.IsNullOrWhiteSpace(breakpoint.BreakpointId));
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

                if (base64Encoded)
                {
                    byte[] bytes = Convert.FromBase64String(content);
                    string ext = System.IO.Path.GetExtension(SelectedFileName);
                    string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cdp_preview_{Guid.NewGuid()}{ext}");
                    await System.IO.File.WriteAllBytesAsync(tempFile, bytes);
                    LocalPreviewFilePath = tempFile;
                    SelectedFileContent = $"(Binary file loaded to {tempFile})";
                }
                else if (SelectedFileName != null && SelectedFileName.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase))
                {
                    string ext = System.IO.Path.GetExtension(SelectedFileName);
                    string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cdp_preview_{Guid.NewGuid()}{ext}");
                    await System.IO.File.WriteAllTextAsync(tempFile, content);
                    LocalPreviewFilePath = tempFile;
                    SelectedFileContent = $"(Document file loaded to {tempFile})";
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
            if (!_cdpService.SupportsDomain("Sources") && _cdpService.SupportsDomain("Debugger"))
            {
                var runtimeMatches = new System.Collections.Generic.List<SearchResultModel>();
                var scripts = SelectedRuntimeScript is not null
                    ? new[] { SelectedRuntimeScript }
                    : RuntimeScripts.Where(script => !script.IsOriginalSource && !string.IsNullOrWhiteSpace(script.ScriptId)).ToArray();

                foreach (var script in scripts)
                {
                    var runtimeResponse = await _cdpService.SendCommandAsync("Debugger.searchInContent", new JsonObject
                    {
                        ["scriptId"] = script.ScriptId,
                        ["query"] = SearchQuery,
                        ["caseSensitive"] = SearchCaseSensitive,
                        ["isRegex"] = false
                    });
                    if (runtimeResponse["result"] is not JsonArray results) continue;
                    foreach (var match in results.OfType<JsonObject>())
                    {
                        runtimeMatches.Add(new SearchResultModel
                        {
                            Path = string.IsNullOrWhiteSpace(script.Url) ? script.DisplayName : script.Url,
                            LineNumber = (match["lineNumber"]?.GetValue<int>() ?? 0) + 1,
                            LineContent = match["lineContent"]?.GetValue<string>() ?? ""
                        });
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    SearchResults.Clear();
                    foreach (var match in runtimeMatches) SearchResults.Add(match);
                });
                return;
            }

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
            Logger.LogErrorMessage("SourcesVM", "Search failed", ex);
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
        root["breakpointLogMessage"] = BreakpointLogMessage;
        root["breakpointKind"] = BreakpointKind;
        root["breakpointsActive"] = AreBreakpointsActive;
        root["skipAnonymousScripts"] = SkipAnonymousScripts;
        root["selectedFilePath"] = SelectedFile?.Path;
        var blackboxPatterns = new JsonArray();
        foreach (var pattern in BlackboxPatterns) blackboxPatterns.Add((JsonNode?)JsonValue.Create(pattern));
        root["blackboxPatterns"] = blackboxPatterns;
        var breakpoints = new JsonArray();
        foreach (var breakpoint in V8Breakpoints)
        {
            breakpoints.Add((JsonNode)new JsonObject
            {
                ["key"] = breakpoint.Key,
                ["url"] = breakpoint.Url,
                ["bindingUrl"] = breakpoint.BindingUrl,
                ["scriptId"] = breakpoint.ScriptId,
                ["lineNumber"] = breakpoint.LineNumber,
                ["columnNumber"] = breakpoint.ColumnNumber,
                ["displayLineNumber"] = breakpoint.DisplayLineNumber,
                ["condition"] = breakpoint.Condition,
                ["logMessage"] = breakpoint.LogMessage,
                ["kind"] = breakpoint.Kind,
                ["enabled"] = breakpoint.IsEnabled
            });
        }
        root["breakpoints"] = breakpoints;
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
        if (json.TryGetPropertyValue("breakpointLogMessage", out var logNode) && logNode != null)
        {
            BreakpointLogMessage = (string?)logNode ?? "";
        }
        if (json.TryGetPropertyValue("breakpointKind", out var kindNode) && kindNode != null)
        {
            BreakpointKind = (string?)kindNode ?? V8BreakpointKinds.Breakpoint;
        }
        if (json.TryGetPropertyValue("breakpointsActive", out var activeNode) && activeNode != null)
        {
            AreBreakpointsActive = (bool?)activeNode ?? true;
        }
        if (json.TryGetPropertyValue("skipAnonymousScripts", out var skipAnonymousNode) && skipAnonymousNode != null)
        {
            SkipAnonymousScripts = (bool?)skipAnonymousNode ?? false;
        }
        if (json["blackboxPatterns"] is JsonArray patterns)
        {
            BlackboxPatterns.Clear();
            foreach (var pattern in patterns)
            {
                var value = pattern?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(value) && !BlackboxPatterns.Contains(value)) BlackboxPatterns.Add(value);
            }
            if (_cdpService.IsConnected && IsDebuggerEnabled) _ = ApplyBlackboxPatternsAsync();
        }
        if (json["breakpoints"] is JsonArray breakpoints)
        {
            V8Breakpoints.Clear();
            foreach (var breakpointNode in breakpoints.OfType<JsonObject>())
            {
                var url = breakpointNode["url"]?.GetValue<string>() ?? "";
                var lineNumber = breakpointNode["lineNumber"]?.GetValue<int>() ?? 0;
                var displayLineNumber = breakpointNode["displayLineNumber"]?.GetValue<int?>();
                V8Breakpoints.Add(new V8BreakpointModel
                {
                    Key = breakpointNode["key"]?.GetValue<string>() ?? CreateBreakpointKey(url, displayLineNumber ?? lineNumber),
                    Url = url,
                    BindingUrl = breakpointNode["bindingUrl"]?.GetValue<string>() ?? url,
                    ScriptId = breakpointNode["scriptId"]?.GetValue<string>() ?? "",
                    LineNumber = lineNumber,
                    ColumnNumber = breakpointNode["columnNumber"]?.GetValue<int>() ?? 0,
                    DisplayLineNumber = displayLineNumber,
                    Condition = breakpointNode["condition"]?.GetValue<string>() ?? "",
                    LogMessage = breakpointNode["logMessage"]?.GetValue<string>() ?? "",
                    Kind = breakpointNode["kind"]?.GetValue<string>() ?? V8BreakpointKinds.Breakpoint,
                    IsEnabled = breakpointNode["enabled"]?.GetValue<bool>() ?? true
                });
            }
            RefreshLegacyBreakpoints();
            if (_cdpService.IsConnected && IsDebuggerEnabled) _ = RestoreBreakpointBindingsAsync();
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
