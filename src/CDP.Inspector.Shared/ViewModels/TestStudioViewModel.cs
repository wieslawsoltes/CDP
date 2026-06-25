using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Jint;
using ProDataGrid;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;

namespace CdpInspectorApp.ViewModels;

public class TestStudioViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private ObservableCollection<TestStudioStepModel> _steps = new();
    private string _yamlCode = "";
    private ObservableCollection<string> _logs = new();
    private bool _isExecuting;
    private bool _isPaused;
    private string _selectedElementSelector = "";
    private string _inputSimText = "";
    private string _selectedCommandName = "tapOn";
    private int _delayMs = 1000;
    private TestStudioStepModel? _selectedStep;
    private object? _selectedStepNode;

    private int _currentStepIndex = 0;
    private CancellationTokenSource? _executionCts;
    private string _appId = "";
    private string _description = "";
    private bool _isUpdatingYaml = false;

    private string? _workspaceRootPath;
    private string? _currentFlowFilePath;
    private readonly Stack<string> _executingFileStack = new();

    private bool _isSidebarCollapsed;
    private ObservableCollection<WorkspaceItemModel> _workspaceFiles = new();
    private WorkspaceItemModel? _selectedWorkspaceItem;
    private int _suitePassCount;
    private int _suiteFailCount;
    private bool _isSuiteExecuting;

    private ObservableCollection<TestEnvironmentModel> _environments = new();
    private TestEnvironmentModel? _selectedEnvironment;
    private bool _isManageEnvironmentsVisible;
    private List<string> _flowTags = new();
    private Dictionary<string, string> _flowEnv = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<TestEnvironmentModel> Environments
    {
        get => _environments;
        set => RaiseAndSetIfChanged(ref _environments, value);
    }

    public TestEnvironmentModel? SelectedEnvironment
    {
        get => _selectedEnvironment;
        set => RaiseAndSetIfChanged(ref _selectedEnvironment, value);
    }

    public bool IsManageEnvironmentsVisible
    {
        get => _isManageEnvironmentsVisible;
        set => RaiseAndSetIfChanged(ref _isManageEnvironmentsVisible, value);
    }

    public List<string> FlowTags
    {
        get => _flowTags;
        set => RaiseAndSetIfChanged(ref _flowTags, value);
    }

    public Dictionary<string, string> FlowEnv
    {
        get => _flowEnv;
        set => RaiseAndSetIfChanged(ref _flowEnv, value);
    }

    public string? WorkspaceRootPath
    {
        get => _workspaceRootPath;
        set
        {
            if (RaiseAndSetIfChanged(ref _workspaceRootPath, value))
            {
                LoadWorkspaceTree();
                LoadEnvironments();
            }
        }
    }

    public string? CurrentFlowFilePath
    {
        get => _currentFlowFilePath;
        set
        {
            if (RaiseAndSetIfChanged(ref _currentFlowFilePath, value))
            {
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    private GridLength _sidebarColumnWidth = new GridLength(250, GridUnitType.Pixel);
    private GridLength _sidebarSplitterWidth = new GridLength(4, GridUnitType.Pixel);
    private GridLength _cachedSidebarColumnWidth = new GridLength(250, GridUnitType.Pixel);

    public GridLength SidebarColumnWidth
    {
        get => _sidebarColumnWidth;
        set
        {
            if (RaiseAndSetIfChanged(ref _sidebarColumnWidth, value))
            {
                if (!_isSidebarCollapsed && value.Value > 0)
                {
                    _cachedSidebarColumnWidth = value;
                }
            }
        }
    }

    public GridLength SidebarSplitterWidth
    {
        get => _sidebarSplitterWidth;
        set => RaiseAndSetIfChanged(ref _sidebarSplitterWidth, value);
    }

    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set
        {
            if (RaiseAndSetIfChanged(ref _isSidebarCollapsed, value))
            {
                if (value)
                {
                    if (SidebarColumnWidth.Value > 0)
                    {
                        _cachedSidebarColumnWidth = SidebarColumnWidth;
                    }
                    SidebarColumnWidth = new GridLength(0, GridUnitType.Pixel);
                    SidebarSplitterWidth = new GridLength(0, GridUnitType.Pixel);
                }
                else
                {
                    SidebarColumnWidth = _cachedSidebarColumnWidth;
                    SidebarSplitterWidth = new GridLength(4, GridUnitType.Pixel);
                }
                LoadWorkspaceTree();
            }
        }
    }

    private bool _isNamePromptVisible;
    private string _namePromptTitle = "";
    private string _namePromptValue = "";
    private Action<string>? _namePromptCallback;

    public bool IsNamePromptVisible
    {
        get => _isNamePromptVisible;
        set => RaiseAndSetIfChanged(ref _isNamePromptVisible, value);
    }

    public string NamePromptTitle
    {
        get => _namePromptTitle;
        set => RaiseAndSetIfChanged(ref _namePromptTitle, value);
    }

    public string NamePromptValue
    {
        get => _namePromptValue;
        set => RaiseAndSetIfChanged(ref _namePromptValue, value);
    }

    public Action<string>? NamePromptCallback
    {
        get => _namePromptCallback;
        set => _namePromptCallback = value;
    }

    private object? _selectedWorkspaceNode;

    public object? SelectedWorkspaceNode
    {
        get => _selectedWorkspaceNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedWorkspaceNode, value))
            {
                var targetModel = value is HierarchicalNode<WorkspaceItemModel> node ? node.Item : (value as WorkspaceItemModel);
                if (_selectedWorkspaceItem != targetModel)
                {
                    SelectedWorkspaceItem = targetModel;
                }
            }
        }
    }

    public ObservableCollection<WorkspaceItemModel> WorkspaceFiles
    {
        get => _workspaceFiles;
        set => RaiseAndSetIfChanged(ref _workspaceFiles, value);
    }

    public HierarchicalModel<WorkspaceItemModel> HierarchicalWorkspace { get; }

    public WorkspaceItemModel? SelectedWorkspaceItem
    {
        get => _selectedWorkspaceItem;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedWorkspaceItem, value))
            {
                if (value == null)
                {
                    SelectedWorkspaceNode = null;
                }
            }
        }
    }

    public int SuitePassCount
    {
        get => _suitePassCount;
        set => RaiseAndSetIfChanged(ref _suitePassCount, value);
    }

    public int SuiteFailCount
    {
        get => _suiteFailCount;
        set => RaiseAndSetIfChanged(ref _suiteFailCount, value);
    }

    public bool IsSuiteExecuting
    {
        get => _isSuiteExecuting;
        set
        {
            if (RaiseAndSetIfChanged(ref _isSuiteExecuting, value))
            {
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public ICommand ToggleSidebarCommand { get; }
    public ICommand BrowseWorkspaceRootCommand { get; }
    public ICommand CreateFileCommand { get; }
    public ICommand CreateFolderCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SubmitNamePromptCommand { get; }
    public ICommand CancelNamePromptCommand { get; }
    public ICommand RunSuiteCommand { get; }
    public ICommand SaveYamlCommand { get; }

    private bool _isRecordVideoEnabled = true;
    private bool _isGenerateReportEnabled = true;
    private string _outputDirectory = "TestReports";
    private bool _hasLastRunRecording = false;
    private string _lastReportPath = "";
    private string _lastPdfReportPath = "";

    private readonly List<VideoFrameItem> _lastRunVideoFrames = new();
    private readonly List<byte[]> _lastRunRawFrameBytes = new();
    private readonly List<double> _lastRunFrameTimestamps = new();
    private readonly List<StepReportItem> _lastRunSteps = new();
    private bool _isRecordingVideo = false;
    private bool _isAirplaneModeEnabled = false;
    private DateTime _playbackStartTime = DateTime.MinValue;
    private readonly Dictionary<int, StepReportItem> _stepReports = new();
    private bool _isFinalizing = false;

    public bool IsRecordVideoEnabled
    {
        get => _isRecordVideoEnabled;
        set => RaiseAndSetIfChanged(ref _isRecordVideoEnabled, value);
    }

    public bool IsGenerateReportEnabled
    {
        get => _isGenerateReportEnabled;
        set => RaiseAndSetIfChanged(ref _isGenerateReportEnabled, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => RaiseAndSetIfChanged(ref _outputDirectory, value);
    }

    public bool HasLastRunRecording
    {
        get => _hasLastRunRecording;
        set
        {
            if (RaiseAndSetIfChanged(ref _hasLastRunRecording, value))
            {
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public string LastReportPath
    {
        get => _lastReportPath;
        set
        {
            if (RaiseAndSetIfChanged(ref _lastReportPath, value))
            {
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public string LastPdfReportPath
    {
        get => _lastPdfReportPath;
        set
        {
            if (RaiseAndSetIfChanged(ref _lastPdfReportPath, value))
            {
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public ICommand OpenLastReportCommand { get; }
    public ICommand OpenLastPdfReportCommand { get; }
    public ICommand ReplayLastVideoCommand { get; }
    public Action<ReplayIndicatorInfo?>? OnStepIndicatorChanged { get; set; }

    public ObservableCollection<TestStudioStepModel> Steps
    {
        get => _steps;
        set => RaiseAndSetIfChanged(ref _steps, value);
    }

    public HierarchicalModel<TestStudioStepModel> HierarchicalSteps { get; }

    public string YamlCode
    {
        get => _yamlCode;
        set => RaiseAndSetIfChanged(ref _yamlCode, value);
    }

    public ObservableCollection<string> Logs => _logs;

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isExecuting, value))
            {
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isPaused, value))
            {
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public string SelectedElementSelector
    {
        get => _selectedElementSelector;
        set => RaiseAndSetIfChanged(ref _selectedElementSelector, value);
    }

    public string InputSimText
    {
        get => _inputSimText;
        set => RaiseAndSetIfChanged(ref _inputSimText, value);
    }

    public string SelectedCommandName
    {
        get => _selectedCommandName;
        set => RaiseAndSetIfChanged(ref _selectedCommandName, value);
    }

    public ObservableCollection<string> CommandSuggestions { get; } = new(FlowCommandCatalog.PublicCommands.Select(c => c.Name));
    public ObservableCollection<string> ValueSuggestions { get; } = new(new[]
    {
        "true", "false", "DOWN", "UP", "LEFT", "RIGHT", "PORTRAIT", "LANDSCAPE_LEFT", "LANDSCAPE_RIGHT",
        "15000", "30000", "path: \"screenshot\"", "query: \"Describe the value to extract\"",
        "assertion: \"The screen has no overlapping text\"", "permissions: { all: allow }",
        "point: \"50%, 50%\"", "text: \"Visible text\"", "id: \"automation_id\""
    });

    public int DelayMs
    {
        get => _delayMs;
        set => RaiseAndSetIfChanged(ref _delayMs, value);
    }

    public TestStudioStepModel? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedStep, value))
            {
                if (value == null)
                {
                    SelectedStepNode = null;
                }
            }
        }
    }

    public object? SelectedStepNode
    {
        get => _selectedStepNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedStepNode, value))
            {
                var targetModel = value is HierarchicalNode<TestStudioStepModel> node ? node.Item : (value as TestStudioStepModel);
                if (_selectedStep != targetModel)
                {
                    SelectedStep = targetModel;
                }
            }
        }
    }

    public ICommand ManageEnvironmentsCommand { get; }
    public ICommand CreateEnvironmentCommand { get; }
    public ICommand DeleteEnvironmentCommand { get; }
    public ICommand AddVariableCommand { get; }
    public ICommand DeleteVariableCommand { get; }
    public ICommand SaveEnvironmentsCommand { get; }
    public ICommand CancelEnvironmentsCommand { get; }

    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand StepOverCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand AddTapCommand { get; }
    public ICommand AddDoubleTapCommand { get; }
    public ICommand AddLongPressCommand { get; }
    public ICommand AddInputCommand { get; }
    public ICommand AddAssertVisibleCommand { get; }
    public ICommand AddAssertNotVisibleCommand { get; }
    public ICommand AddClearTextCommand { get; }
    public ICommand AddPasteTextCommand { get; }
    public ICommand AddEraseTextCommand { get; }
    public ICommand AddSwipeCommand { get; }
    public ICommand AddDelayCommand { get; }
    public ICommand AddLaunchAppCommand { get; }
    public ICommand AddStopAppCommand { get; }
    public ICommand AddKillAppCommand { get; }
    public ICommand AddClearStateCommand { get; }
    public ICommand AddSetOrientationCommand { get; }
    public ICommand AddSetLocationCommand { get; }
    public ICommand AddTakeScreenshotCommand { get; }
    public ICommand AddAssertTrueCommand { get; }
    public ICommand AddAssertFalseCommand { get; }
    public ICommand AddSetAirplaneModeCommand { get; }
    public ICommand AddRepeatCommand { get; }
    public ICommand AddRetryCommand { get; }
    public ICommand AddRunFlowCommand { get; }
    public ICommand AddEvalScriptCommand { get; }
    public ICommand AddBackCommand { get; }
    public ICommand AddScrollCommand { get; }
    public ICommand AddOpenLinkCommand { get; }
    public ICommand AddCopyTextFromCommand { get; }
    public ICommand AddSelectedCommandCommand { get; }
    public ICommand DeleteStepCommand { get; }
    public ICommand MoveUpStepCommand { get; }
    public ICommand MoveDownStepCommand { get; }
    public ICommand ApplyYamlCommand { get; }

    public TestStudioViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        Steps.CollectionChanged += OnStepsCollectionChanged;

        var options = new HierarchicalOptions<TestStudioStepModel>
        {
            ChildrenSelector = step => step.NestedSteps,
            IsLeafSelector = step => step.NestedSteps == null,
            AutoExpandRoot = true
        };
        HierarchicalSteps = new HierarchicalModel<TestStudioStepModel>(options);
        HierarchicalSteps.SetRoots(Steps);

        ManageEnvironmentsCommand = new RelayCommand(ManageEnvironments);
        CreateEnvironmentCommand = new RelayCommand(CreateEnvironment);
        DeleteEnvironmentCommand = new RelayCommand(DeleteEnvironment);
        AddVariableCommand = new RelayCommand(AddVariable);
        DeleteVariableCommand = new RelayCommand<EnvironmentVariableModel>(DeleteVariable);
        SaveEnvironmentsCommand = new RelayCommand(SaveEnvironmentsAction);
        CancelEnvironmentsCommand = new RelayCommand(CancelEnvironmentsAction);

        PlayCommand = new RelayCommand(async () => await PlayAsync(), () => _cdpService.IsConnected && Steps.Count > 0 && (!IsExecuting || IsPaused) && !IsSuiteExecuting);
        PauseCommand = new RelayCommand(Pause, () => IsExecuting && !IsPaused && !_isFinalizing);
        StopCommand = new RelayCommand(Stop, () => IsExecuting && !_isFinalizing);
        StepOverCommand = new RelayCommand(async () => await StepOverAsync(), () => _cdpService.IsConnected && Steps.Count > 0 && (!IsExecuting || IsPaused) && !IsSuiteExecuting);
        ClearCommand = new RelayCommand(ClearAll, () => !IsSuiteExecuting);

        AddTapCommand = new RelayCommand(async () => await AddTapAsync());
        AddDoubleTapCommand = new RelayCommand(async () => await AddDoubleTapAsync());
        AddLongPressCommand = new RelayCommand(async () => await AddLongPressAsync());
        AddInputCommand = new RelayCommand(async () => await AddInputAsync());
        AddAssertVisibleCommand = new RelayCommand(async () => await AddAssertVisibleAsync());
        AddAssertNotVisibleCommand = new RelayCommand(async () => await AddAssertNotVisibleAsync());
        AddClearTextCommand = new RelayCommand(async () => await AddClearTextAsync());
        AddPasteTextCommand = new RelayCommand(async () => await AddPasteTextAsync());
        AddEraseTextCommand = new RelayCommand(async () => await AddEraseTextAsync());
        AddSwipeCommand = new RelayCommand(async () => await AddSwipeAsync());
        AddDelayCommand = new RelayCommand(AddDelay);
        AddLaunchAppCommand = new RelayCommand(AddLaunchApp);
        AddStopAppCommand = new RelayCommand(AddStopApp);
        AddKillAppCommand = new RelayCommand(AddKillApp);
        AddClearStateCommand = new RelayCommand(async () => await AddClearStateAsync());
        AddSetOrientationCommand = new RelayCommand(async () => await AddSetOrientationAsync());
        AddSetLocationCommand = new RelayCommand(async () => await AddSetLocationAsync());
        AddTakeScreenshotCommand = new RelayCommand(async () => await AddTakeScreenshotAsync());
        AddAssertTrueCommand = new RelayCommand(async () => await AddAssertTrueAsync());
        AddAssertFalseCommand = new RelayCommand(async () => await AddAssertFalseAsync());
        AddSetAirplaneModeCommand = new RelayCommand(async () => await AddSetAirplaneModeAsync());
        AddRepeatCommand = new RelayCommand(AddRepeat);
        AddRetryCommand = new RelayCommand(AddRetry);
        AddRunFlowCommand = new RelayCommand(AddRunFlow);
        AddEvalScriptCommand = new RelayCommand(async () => await AddEvalScriptAsync());
        AddBackCommand = new RelayCommand(async () => await AddBackAsync());
        AddScrollCommand = new RelayCommand(async () => await AddScrollAsync());
        AddOpenLinkCommand = new RelayCommand(async () => await AddOpenLinkAsync());
        AddCopyTextFromCommand = new RelayCommand(async () => await AddCopyTextFromAsync());
        AddSelectedCommandCommand = new RelayCommand(AddSelectedCommand);

        DeleteStepCommand = new RelayCommand<TestStudioStepModel>(DeleteStep);
        MoveUpStepCommand = new RelayCommand<TestStudioStepModel>(MoveStepUp);
        MoveDownStepCommand = new RelayCommand<TestStudioStepModel>(MoveStepDown);

        ApplyYamlCommand = new RelayCommand(ApplyYaml, () => !IsExecuting);

        OpenLastReportCommand = new RelayCommand(OpenLastReport, () => HasLastRunRecording && !string.IsNullOrEmpty(LastReportPath) && File.Exists(LastReportPath));
        OpenLastPdfReportCommand = new RelayCommand(OpenLastPdfReport, () => HasLastRunRecording && !string.IsNullOrEmpty(LastPdfReportPath) && File.Exists(LastPdfReportPath));
        ReplayLastVideoCommand = new RelayCommand<object>(ReplayLastVideo, _ => HasLastRunRecording && _lastRunRawFrameBytes.Count > 0);

        var workspaceOptions = new HierarchicalOptions<WorkspaceItemModel>
        {
            ChildrenSelector = item => item.Children,
            IsLeafSelector = item => item.Children == null || item.Children.Count == 0,
            AutoExpandRoot = true
        };
        HierarchicalWorkspace = new HierarchicalModel<WorkspaceItemModel>(workspaceOptions);
        HierarchicalWorkspace.SetRoots(WorkspaceFiles);

        ToggleSidebarCommand = new RelayCommand(() => IsSidebarCollapsed = !IsSidebarCollapsed);
        BrowseWorkspaceRootCommand = new RelayCommand(async () => await BrowseWorkspaceRootAsync());
        CreateFileCommand = new RelayCommand<string>(name => CreateFile(name));
        CreateFolderCommand = new RelayCommand<string>(name => CreateFolder(name));
        RenameCommand = new RelayCommand<string>(name => RenameItem(name));
        DeleteCommand = new RelayCommand<string>(path => DeleteItem(path));
        RunSuiteCommand = new RelayCommand<string>(async path => await RunSuite(path), _ => !IsSuiteExecuting && !IsExecuting);
        SaveYamlCommand = new RelayCommand(SaveYaml, () => !string.IsNullOrEmpty(CurrentFlowFilePath));

        SubmitNamePromptCommand = new RelayCommand(() =>
        {
            IsNamePromptVisible = false;
            _namePromptCallback?.Invoke(NamePromptValue);
        });
        CancelNamePromptCommand = new RelayCommand(() =>
        {
            IsNamePromptVisible = false;
        });

        _cdpService.EventReceived += CdpService_EventReceived;
    }

    private void CdpService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (!_cdpService.IsConnected && IsExecuting)
            {
                Stop();
            }
            RaiseCommandCanExecuteChanged();
        }
    }

    private void RaiseCommandCanExecuteChanged()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ((RelayCommand)PlayCommand).RaiseCanExecuteChanged();
            ((RelayCommand)PauseCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StepOverCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ApplyYamlCommand).RaiseCanExecuteChanged();
            if (OpenLastReportCommand is RelayCommand olr) olr.RaiseCanExecuteChanged();
            if (OpenLastPdfReportCommand is RelayCommand olp) olp.RaiseCanExecuteChanged();
            if (ReplayLastVideoCommand is RelayCommand<object> rlv) rlv.RaiseCanExecuteChanged();
            if (SaveYamlCommand is RelayCommand sy) sy.RaiseCanExecuteChanged();
            if (RunSuiteCommand is RelayCommand<string> rs) rs.RaiseCanExecuteChanged();
        });
    }

    private void OnStepsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (TestStudioStepModel oldStep in e.OldItems)
            {
                UnsubscribeStep(oldStep);
            }
        }
        if (e.NewItems != null)
        {
            foreach (TestStudioStepModel newStep in e.NewItems)
            {
                SubscribeStep(newStep);
            }
        }
        UpdateYaml();
        RaiseCommandCanExecuteChanged();
    }

    private void SubscribeStep(TestStudioStepModel step)
    {
        step.PropertyChanged -= OnStepPropertyChanged;
        step.PropertyChanged += OnStepPropertyChanged;
        if (step.NestedSteps != null)
        {
            step.NestedSteps.CollectionChanged -= OnNestedStepsCollectionChanged;
            step.NestedSteps.CollectionChanged += OnNestedStepsCollectionChanged;
            foreach (var nested in step.NestedSteps)
            {
                SubscribeStep(nested);
            }
        }
    }

    private void UnsubscribeStep(TestStudioStepModel step)
    {
        step.PropertyChanged -= OnStepPropertyChanged;
        if (step.NestedSteps != null)
        {
            step.NestedSteps.CollectionChanged -= OnNestedStepsCollectionChanged;
            foreach (var nested in step.NestedSteps)
            {
                UnsubscribeStep(nested);
            }
        }
    }

    private void OnNestedStepsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (TestStudioStepModel oldStep in e.OldItems)
            {
                UnsubscribeStep(oldStep);
            }
        }
        if (e.NewItems != null)
        {
            foreach (TestStudioStepModel newStep in e.NewItems)
            {
                SubscribeStep(newStep);
            }
        }
        UpdateYaml();
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TestStudioStepModel.Action) ||
            e.PropertyName == nameof(TestStudioStepModel.Selector) ||
            e.PropertyName == nameof(TestStudioStepModel.Value))
        {
            UpdateYaml();
        }
    }

    private void UpdateYaml()
    {
        if (_isUpdatingYaml) return;
        _isUpdatingYaml = true;
        try
        {
            YamlCode = TestStudioYamlParser.Generate(Steps.Select(s => s.ToCoreStep()).ToList(), _appId, _description, FlowTags, FlowEnv);
            // Re-parse the generated YAML to resolve line coordinates for recorded steps
            var parsed = TestStudioYamlParser.Parse(YamlCode, out _, out _);
            for (int i = 0; i < Math.Min(Steps.Count, parsed.Count); i++)
            {
                Steps[i].StartLine = parsed[i].StartLine;
                Steps[i].EndLine = parsed[i].EndLine;
            }
        }
        finally
        {
            _isUpdatingYaml = false;
        }
    }

    public void ApplyYaml()
    {
        if (IsExecuting) return;

        try
        {
            var parsedSteps = TestStudioYamlParser.Parse(YamlCode, out var appId, out var desc, out var tags, out var env);
            var parsed = parsedSteps.Select(TestStudioStepModel.FromCoreStep).ToList();
            _appId = appId;
            _description = desc;
            FlowTags = tags;
            FlowEnv = env;

            _isUpdatingYaml = true;
            try
            {
                // Unsubscribe from old steps
                foreach (var step in Steps)
                {
                    UnsubscribeStep(step);
                }

                Steps.Clear();
                foreach (var step in parsed)
                {
                    SubscribeStep(step);
                    Steps.Add(step);
                }
            }
            finally
            {
                _isUpdatingYaml = false;
            }

            Log($"Successfully imported {Steps.Count} steps from YAML.");
        }
        catch (Exception ex)
        {
            Log($"Error parsing YAML: {ex.Message}");
        }
    }

    public async Task PlayAsync()
    {
        if (IsExecuting && !IsPaused) return;

        if (!IsPaused)
        {
            foreach (var step in Steps)
            {
                step.Status = StepStatus.Pending;
                step.ErrorMessage = null;
                step.IsCurrent = false;
            }
            _currentStepIndex = 0;

            // Start Recording Session
            _lastRunVideoFrames.Clear();
            lock (_lastRunRawFrameBytes)
            {
                _lastRunRawFrameBytes.Clear();
                _lastRunFrameTimestamps.Clear();
            }
            lock (_stepReports)
            {
                _stepReports.Clear();
            }
            _playbackStartTime = DateTime.UtcNow;

            if (IsRecordVideoEnabled && _cdpService.IsConnected)
            {
                _isRecordingVideo = true;
                try
                {
                    Log("Starting video recording...");
                    await _cdpService.SendCommandAsync("Page.startScreencast", new JsonObject
                    {
                        ["format"] = "jpeg",
                        ["quality"] = 80,
                        ["everyNthFrame"] = 1
                    });
                }
                catch (Exception ex)
                {
                    Log($"Warning: Failed to start screencast: {ex.Message}");
                }
            }
        }

        IsExecuting = true;
        IsPaused = false;

        _executionCts = new CancellationTokenSource();
        var token = _executionCts.Token;

        try
        {
            var combinedEnv = GetCombinedEnvironment();
            await RunLoopAsync(combinedEnv, token);
        }
        catch (OperationCanceledException)
        {
            Log("Execution paused or stopped.");
        }
        catch (Exception ex)
        {
            Log($"Execution error: {ex.Message}");
        }
        finally
        {
            if (!IsPaused)
            {
                _isFinalizing = true;
                RaiseCommandCanExecuteChanged();
                try
                {
                    await FinalizeRecordingAndGenerateReportsAsync();
                }
                finally
                {
                    _isFinalizing = false;
                    IsExecuting = false;
                }
            }
            RaiseCommandCanExecuteChanged();
        }
    }

    public void Pause()
    {
        if (!IsExecuting || IsPaused) return;
        IsPaused = true;
        _executionCts?.Cancel();
        RaiseCommandCanExecuteChanged();
    }

    public void Stop()
    {
        if (IsSuiteExecuting)
        {
            IsSuiteExecuting = false;
            Log("Stopping suite execution...");
        }
        bool wasPaused = IsPaused;
        IsPaused = false;
        _executionCts?.Cancel();
        _currentStepIndex = 0;
        foreach (var step in Steps)
        {
            step.Status = StepStatus.Pending;
            step.ErrorMessage = null;
            step.IsCurrent = false;
        }
        Log("Execution stopped.");

        if (wasPaused)
        {
            _isFinalizing = true;
            RaiseCommandCanExecuteChanged();
            _ = Task.Run(async () =>
            {
                try
                {
                    await FinalizeRecordingAndGenerateReportsAsync();
                }
                finally
                {
                    _isFinalizing = false;
                    IsExecuting = false;
                    RaiseCommandCanExecuteChanged();
                }
            });
        }
        else
        {
            RaiseCommandCanExecuteChanged();
        }
    }

    public async Task StepOverAsync()
    {
        if (IsExecuting && !IsPaused) return;

        if (!IsExecuting)
        {
            foreach (var step in Steps)
            {
                step.Status = StepStatus.Pending;
                step.ErrorMessage = null;
                step.IsCurrent = false;
            }
            _currentStepIndex = 0;
            IsExecuting = true;
        }

        IsPaused = true;

        if (_currentStepIndex >= Steps.Count)
        {
            IsExecuting = false;
            IsPaused = false;
            Log("No more steps to execute.");
            return;
        }

        var stepToExecute = Steps[_currentStepIndex];
        try
        {
            stepToExecute.IsCurrent = true;
            stepToExecute.Status = StepStatus.Running;
            Log($"Running step {_currentStepIndex + 1}: {stepToExecute.ActionDisplay}...");

            var combinedEnv = GetCombinedEnvironment();
            await ExecuteSingleStepAsync(stepToExecute, combinedEnv, CancellationToken.None);

            stepToExecute.Status = StepStatus.Passed;
            Log($"Step {_currentStepIndex + 1} passed.");
            _currentStepIndex++;
        }
        catch (Exception ex)
        {
            stepToExecute.Status = StepStatus.Failed;
            stepToExecute.ErrorMessage = ex.Message;
            Log($"Step {_currentStepIndex + 1} failed: {ex.Message}");
            _currentStepIndex++;
        }
        finally
        {
            stepToExecute.IsCurrent = false;
            if (_currentStepIndex >= Steps.Count)
            {
                IsExecuting = false;
                IsPaused = false;
                Log("Execution finished.");
            }
            RaiseCommandCanExecuteChanged();
        }
    }

    private Dictionary<string, string> GetCombinedEnvironment()
    {
        var combinedEnv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (FlowEnv != null)
        {
            foreach (var kv in FlowEnv)
            {
                combinedEnv[kv.Key] = kv.Value;
            }
        }
        if (SelectedEnvironment != null)
        {
            foreach (var v in SelectedEnvironment.Variables)
            {
                if (!string.IsNullOrEmpty(v.Key))
                {
                    combinedEnv[v.Key] = v.Value;
                }
            }
        }
        return combinedEnv;
    }

    private async Task RunLoopAsync(Dictionary<string, string> env, CancellationToken token)
    {
        bool pushed = false;
        if (!string.IsNullOrEmpty(CurrentFlowFilePath))
        {
            _executingFileStack.Push(CurrentFlowFilePath);
            pushed = true;
        }

        try
        {
            while (_currentStepIndex < Steps.Count)
            {
                token.ThrowIfCancellationRequested();

                var step = Steps[_currentStepIndex];
                step.IsCurrent = true;
                step.Status = StepStatus.Running;
                Log($"Running step {_currentStepIndex + 1}: {step.ActionDisplay}...");

                var stepStartTime = DateTime.UtcNow;

                try
                {
                    await ExecuteSingleStepAsync(step, env, token);
                    var duration = (DateTime.UtcNow - stepStartTime).TotalMilliseconds;
                    var relativeStartMs = (stepStartTime - _playbackStartTime).TotalMilliseconds;
                    step.Status = StepStatus.Passed;
                    Log($"Step {_currentStepIndex + 1} passed.");
                    
                    if ((IsGenerateReportEnabled || IsRecordVideoEnabled) && _cdpService.IsConnected)
                    {
                        await CaptureStepDetailsAsync(step, _currentStepIndex, duration, relativeStartMs);
                    }

                    _currentStepIndex++;
                }
                catch (OperationCanceledException)
                {
                    step.Status = StepStatus.Pending;
                    Log($"Step {_currentStepIndex + 1} paused.");
                    throw;
                }
                catch (Exception ex)
                {
                    var duration = (DateTime.UtcNow - stepStartTime).TotalMilliseconds;
                    var relativeStartMs = (stepStartTime - _playbackStartTime).TotalMilliseconds;
                    step.Status = StepStatus.Failed;
                    step.ErrorMessage = ex.Message;
                    Log($"Step {_currentStepIndex + 1} failed: {ex.Message}");
                    
                    if ((IsGenerateReportEnabled || IsRecordVideoEnabled) && _cdpService.IsConnected)
                    {
                        await CaptureStepDetailsAsync(step, _currentStepIndex, duration, relativeStartMs);
                    }

                    throw;
                }
                finally
                {
                    step.IsCurrent = false;
                }
            }
        }
        finally
        {
            if (pushed)
            {
                _executingFileStack.Pop();
            }
        }

        Log("Execution finished successfully.");
        IsExecuting = false;
        IsPaused = false;
    }

    public async Task ExecuteSingleStepAsync(TestStudioStepModel step, CancellationToken token)
    {
        await ExecuteSingleStepAsync(step, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), token);
    }

    public async Task ExecuteSingleStepAsync(TestStudioStepModel step, Dictionary<string, string> env, CancellationToken token)
    {
        var action = step.Action;
        if (string.IsNullOrEmpty(action)) return;

        var interpolatedStep = InterpolateStep(step, env);

        var indicator = new ReplayIndicatorInfo
        {
            Action = interpolatedStep.Action,
            Selector = interpolatedStep.Selector ?? "",
            Value = interpolatedStep.Value ?? "",
            Status = ReplayIndicatorStatus.Running
        };

        // Notify start of step
        OnStepIndicatorChanged?.Invoke(indicator);

        try
        {
            await ExecuteSingleStepInternalAsync(interpolatedStep, env, indicator, token);
            indicator.Status = ReplayIndicatorStatus.Passed;
            OnStepIndicatorChanged?.Invoke(indicator);
            await Task.Delay(500, token); // Keep it visible for 500ms
        }
        catch (Exception ex)
        {
            indicator.Status = ReplayIndicatorStatus.Failed;
            indicator.ErrorMessage = ex.Message;
            OnStepIndicatorChanged?.Invoke(indicator);
            await Task.Delay(1000, token); // Keep error visible for 1000ms
            throw;
        }
        finally
        {
            OnStepIndicatorChanged?.Invoke(null);
        }
    }

    private async Task ExecuteSingleStepInternalAsync(TestStudioStepModel step, Dictionary<string, string> env, ReplayIndicatorInfo indicator, CancellationToken token)
    {
        var action = step.Action;
        if (string.IsNullOrEmpty(action)) return;

        switch (action)
        {
            case "launchApp":
                {
                    string targetUrl = _cdpService.ConnectedHost;
                    var appTarget = GetStepValue(step, "appId");
                    if (!string.IsNullOrEmpty(appTarget) &&
                        (appTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         appTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        targetUrl = appTarget;
                    }
                    else if (!string.IsNullOrEmpty(_appId) &&
                             (_appId.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                              _appId.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        targetUrl = _appId;
                    }
                    if (string.IsNullOrEmpty(targetUrl))
                    {
                        targetUrl = "http://localhost:9222/";
                    }
                    Log($"Launching app / navigating to: {targetUrl}");
                    await _cdpService.SendCommandAsync("Page.navigate", new JsonObject { ["url"] = targetUrl });
                    await Task.Delay(500, token);
                    break;
                }
            case "tapOn":
                {
                    var (x, y, nodeId) = await ResolveCoordinatesAsync(step, token);
                    indicator.X = x;
                    indicator.Y = y;
                    OnStepIndicatorChanged?.Invoke(indicator);
                    if (nodeId > 0)
                    {
                        try
                        {
                            await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                            await Task.Delay(100, token);
                        }
                        catch { }
                    }
                    Log($"Tapping at coordinate ({x:F1}, {y:F1})");
                    var repeat = Math.Max(1, GetStepInt(step, 1, "repeat"));
                    var delay = Math.Max(0, GetStepInt(step, 0, "delay"));
                    for (var tapIndex = 0; tapIndex < repeat; tapIndex++)
                    {
                        await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                        {
                            ["type"] = "mousePressed",
                            ["x"] = x,
                            ["y"] = y,
                            ["button"] = "left",
                            ["clickCount"] = 1,
                            ["modifiers"] = 0
                        });
                        await Task.Delay(50, token);
                        await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                        {
                            ["type"] = "mouseReleased",
                            ["x"] = x,
                            ["y"] = y,
                            ["button"] = "left",
                            ["clickCount"] = 1,
                            ["modifiers"] = 0
                        });
                        if (tapIndex < repeat - 1 && delay > 0)
                        {
                            await Task.Delay(delay, token);
                        }
                    }
                    await Task.Delay(200, token);
                    break;
                }
            case "doubleTapOn":
                {
                    var (x, y, nodeId) = await ResolveCoordinatesAsync(step, token);
                    indicator.X = x;
                    indicator.Y = y;
                    OnStepIndicatorChanged?.Invoke(indicator);
                    if (nodeId > 0)
                    {
                        try
                        {
                            await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                            await Task.Delay(100, token);
                        }
                        catch { }
                    }
                    Log($"Double tapping at coordinate ({x:F1}, {y:F1})");
                    var doubleTapDelay = Math.Max(0, GetStepInt(step, 100, "delay"));
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mousePressed", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1, ["modifiers"] = 0 });
                    await Task.Delay(50, token);
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseReleased", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1, ["modifiers"] = 0 });
                    await Task.Delay(doubleTapDelay, token);
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mousePressed", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 2, ["modifiers"] = 0 });
                    await Task.Delay(50, token);
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseReleased", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 2, ["modifiers"] = 0 });
                    await Task.Delay(200, token);
                    break;
                }
            case "longPressOn":
                {
                    var (x, y, nodeId) = await ResolveCoordinatesAsync(step, token);
                    indicator.X = x;
                    indicator.Y = y;
                    OnStepIndicatorChanged?.Invoke(indicator);
                    if (nodeId > 0)
                    {
                        try
                        {
                            await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                            await Task.Delay(100, token);
                        }
                        catch { }
                    }
                    Log($"Long pressing at coordinate ({x:F1}, {y:F1})");
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mousePressed", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1, ["modifiers"] = 0 });
                    await Task.Delay(3000, token);
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseReleased", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1, ["modifiers"] = 0 });
                    await Task.Delay(200, token);
                    break;
                }

            case "inputText":
                {
                    var runtimeSelector = GetRuntimeSelector(step);
                    if (!string.IsNullOrEmpty(runtimeSelector))
                    {
                        int retryCount = 0;
                        bool success = false;
                        while (retryCount < 3 && !success)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                Log($"Waiting for element '{runtimeSelector}' to be visible...");
                                int nodeId = await WaitForElementVisibleAsync(runtimeSelector, token);

                                try
                                {
                                    var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = nodeId });
                                    var model = boxRes["model"] as JsonObject;
                                    indicator.BoxModel = model;
                                    var content = model?["content"] as JsonArray;
                                    if (content != null && content.Count >= 8)
                                    {
                                        double x1 = content[0]!.GetValue<double>();
                                        double y1 = content[1]!.GetValue<double>();
                                        double x2 = content[4]!.GetValue<double>();
                                        double y2 = content[5]!.GetValue<double>();
                                        indicator.X = x1 + (x2 - x1) / 2.0;
                                        indicator.Y = y1 + (y2 - y1) / 2.0;
                                    }
                                    OnStepIndicatorChanged?.Invoke(indicator);
                                }
                                catch {}

                                Log($"Focusing element and typing '{step.Value}'");
                                await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                                await Task.Delay(100, token);
                                success = true;
                            }
                            catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) && retryCount < 2)
                            {
                                Log($"Focus failed due to Node ID invalidation: {ex.Message}. Retrying...");
                                retryCount++;
                                await Task.Delay(100, token);
                            }
                        }
                    }
                    else
                    {
                        Log($"Typing '{step.Value}' on currently focused control...");
                    }
                    await _cdpService.SendCommandAsync("Input.insertText", new JsonObject { ["text"] = step.Value ?? "" });
                    await Task.Delay(200, token);
                    break;
                }
            case "clearText":
                {
                    var runtimeSelector = GetRuntimeSelector(step);
                    if (!string.IsNullOrEmpty(runtimeSelector))
                    {
                        int retryCount = 0;
                        bool success = false;
                        while (retryCount < 3 && !success)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                Log($"Waiting for element '{runtimeSelector}' to be visible...");
                                int nodeId = await WaitForElementVisibleAsync(runtimeSelector, token);

                                try
                                {
                                    var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = nodeId });
                                    var model = boxRes["model"] as JsonObject;
                                    indicator.BoxModel = model;
                                    var content = model?["content"] as JsonArray;
                                    if (content != null && content.Count >= 8)
                                    {
                                        double x1 = content[0]!.GetValue<double>();
                                        double y1 = content[1]!.GetValue<double>();
                                        double x2 = content[4]!.GetValue<double>();
                                        double y2 = content[5]!.GetValue<double>();
                                        indicator.X = x1 + (x2 - x1) / 2.0;
                                        indicator.Y = y1 + (y2 - y1) / 2.0;
                                    }
                                    OnStepIndicatorChanged?.Invoke(indicator);
                                }
                                catch {}

                                Log("Focusing element and clearing text...");
                                await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                                await Task.Delay(100, token);
                                success = true;
                            }
                            catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) && retryCount < 2)
                            {
                                Log($"Focus failed due to Node ID invalidation: {ex.Message}. Retrying...");
                                retryCount++;
                                await Task.Delay(100, token);
                            }
                        }
                    }
                    else
                    {
                        Log("Clearing text on currently focused control...");
                    }

                    // Ctrl+A
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "rawKeyDown",
                        ["key"] = "a",
                        ["modifiers"] = 2
                    });
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "keyUp",
                        ["key"] = "a",
                        ["modifiers"] = 2
                    });

                    // Cmd+A
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "rawKeyDown",
                        ["key"] = "a",
                        ["modifiers"] = 4
                    });
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "keyUp",
                        ["key"] = "a",
                        ["modifiers"] = 4
                    });

                    await Task.Delay(50, token);

                    // Backspace
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "rawKeyDown",
                        ["key"] = "Backspace"
                    });
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "keyUp",
                        ["key"] = "Backspace"
                    });
                    await Task.Delay(200, token);
                    break;
                }
            case "assertVisible":
                {
                    var runtimeSelector = GetRuntimeSelector(step);
                    if (string.IsNullOrEmpty(runtimeSelector))
                    {
                        throw new Exception("assertVisible step requires a Selector.");
                    }
                    Log($"Asserting visibility of element '{runtimeSelector}'...");
                    int nodeId = await WaitForElementVisibleAsync(runtimeSelector, token);
                    try
                    {
                        var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = nodeId });
                        var model = boxRes["model"] as JsonObject;
                        indicator.BoxModel = model;
                        var content = model?["content"] as JsonArray;
                        if (content != null && content.Count >= 8)
                        {
                            double x1 = content[0]!.GetValue<double>();
                            double y1 = content[1]!.GetValue<double>();
                            double x2 = content[4]!.GetValue<double>();
                            double y2 = content[5]!.GetValue<double>();
                            indicator.X = x1 + (x2 - x1) / 2.0;
                            indicator.Y = y1 + (y2 - y1) / 2.0;
                        }
                        OnStepIndicatorChanged?.Invoke(indicator);
                    }
                    catch {}
                    Log("Assertion passed: Element is visible.");
                    break;
                }
            case "assertNotVisible":
                {
                    var runtimeSelector = GetRuntimeSelector(step);
                    if (string.IsNullOrEmpty(runtimeSelector))
                    {
                        throw new Exception("assertNotVisible step requires a Selector.");
                    }
                    Log($"Asserting element '{runtimeSelector}' is NOT visible...");
                    await WaitForElementNotVisibleAsync(runtimeSelector, token);
                    Log("Assertion passed: Element is not visible.");
                    break;
                }
            case "delay":
                {
                    int dMs = 1000;
                    if (int.TryParse(step.Value, out int parsedVal))
                    {
                        dMs = parsedVal;
                    }
                    Log($"Delaying for {dMs} ms...");
                    await Task.Delay(dMs, token);
                    break;
                }
            case "openLink":
                {
                    var link = GetStepValue(step, "link");
                    if (string.IsNullOrEmpty(link)) link = step.Value ?? "";
                    if (string.IsNullOrEmpty(link)) throw new Exception("openLink step requires a URL value.");
                    Log($"Opening link: '{link}'");
                    await _cdpService.SendCommandAsync("Page.navigate", new JsonObject { ["url"] = link });
                    await Task.Delay(1000, token);
                    break;
                }
            case "copyTextFrom":
                {
                    var runtimeSelector = GetRuntimeSelector(step);
                    if (string.IsNullOrEmpty(runtimeSelector)) throw new Exception("copyTextFrom step requires a Selector.");
                    Log($"Copying text from element '{runtimeSelector}'...");
                    int nodeId = await WaitForElementVisibleAsync(runtimeSelector, token);
                    var resolveRes = await _cdpService.SendCommandAsync("DOM.resolveNode", new JsonObject { ["nodeId"] = nodeId });
                    var objectNode = resolveRes["object"] as JsonObject;
                    string objectId = objectNode?["objectId"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrEmpty(objectId)) throw new Exception($"Could not resolve remote object for element '{runtimeSelector}'.");

                    var callParams = new JsonObject
                    {
                        ["objectId"] = objectId,
                        ["functionDeclaration"] = "function() { return this.Content || this.Text || this.value || this.textContent || this.innerText || ''; }",
                        ["returnByValue"] = true
                    };
                    var callRes = await _cdpService.SendCommandAsync("Runtime.callFunctionOn", callParams);
                    var resultNode = callRes["result"] as JsonObject;
                    string text = resultNode?["value"]?.GetValue<string>() ?? "";

                    Log($"Copied text: '{text}'");

                    Avalonia.Input.Platform.IClipboard? clipboard = null;
                    if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        clipboard = desktop.MainWindow?.Clipboard ?? desktop.Windows.FirstOrDefault(w => w.Clipboard != null)?.Clipboard;
                    }
                    else if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView)
                    {
                        clipboard = Avalonia.Controls.TopLevel.GetTopLevel(singleView.MainView)?.Clipboard;
                    }
                    if (clipboard != null)
                    {
                        await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, text);
                    }
                    break;
                }
            case "back":
                {
                    Log("Navigating back in history...");
                    var historyRes = await _cdpService.SendCommandAsync("Page.getNavigationHistory");
                    int currentIndex = historyRes["currentIndex"]?.GetValue<int>() ?? -1;
                    var entries = historyRes["entries"] as JsonArray;
                    if (entries != null && currentIndex > 0 && currentIndex < entries.Count)
                    {
                        var prevEntry = entries[currentIndex - 1] as JsonObject;
                        int entryId = prevEntry?["id"]?.GetValue<int>() ?? 0;
                        if (entryId > 0)
                        {
                            await _cdpService.SendCommandAsync("Page.navigateToHistoryEntry", new JsonObject { ["entryId"] = entryId });
                            await Task.Delay(500, token);
                        }
                        else
                        {
                            throw new Exception("Could not find previous history entry ID.");
                        }
                    }
                    else
                    {
                        throw new Exception("No back history available.");
                    }
                    break;
                }
            case "dragAndDrop":
                {
                    var runtimeSelector = GetRuntimeSelector(step);
                    if (string.IsNullOrEmpty(runtimeSelector))
                    {
                        throw new Exception("dragAndDrop step requires a Selector.");
                    }

                    string targetSelector = "";
                    double offsetX = 0;
                    double offsetY = 0;
                    double targetOffsetX = 0;
                    double targetOffsetY = 0;

                    if (!string.IsNullOrEmpty(step.Value))
                    {
                        var props = ParseKeyValuePairs(step.Value);
                        if (props.TryGetValue("targetselector", out var ts)) targetSelector = ts;
                        else if (props.TryGetValue("targetSelector", out var ts2)) targetSelector = ts2;

                        if (props.TryGetValue("offsetx", out var ox) && double.TryParse(ox, out double oxVal)) offsetX = oxVal;
                        else if (props.TryGetValue("offsetX", out var ox2) && double.TryParse(ox2, out double oxVal2)) offsetX = oxVal2;

                        if (props.TryGetValue("offsety", out var oy) && double.TryParse(oy, out double oyVal)) offsetY = oyVal;
                        else if (props.TryGetValue("offsetY", out var oy2) && double.TryParse(oy2, out double oyVal2)) offsetY = oyVal2;

                        if (props.TryGetValue("targetoffsetx", out var tox) && double.TryParse(tox, out double toxVal)) targetOffsetX = toxVal;
                        else if (props.TryGetValue("targetOffsetX", out var tox2) && double.TryParse(tox2, out double toxVal2)) targetOffsetX = toxVal2;

                        if (props.TryGetValue("targetoffsety", out var toy) && double.TryParse(toy, out double toyVal)) targetOffsetY = toyVal;
                        else if (props.TryGetValue("targetOffsetY", out var toy2) && double.TryParse(toy2, out double toyVal2)) targetOffsetY = toyVal2;
                    }

                    if (string.IsNullOrEmpty(targetSelector))
                    {
                        throw new Exception("dragAndDrop step requires a targetSelector.");
                    }

                    Log($"Waiting for drag source element '{runtimeSelector}' to be visible...");
                    var sourceNodeId = await WaitForElementVisibleAsync(runtimeSelector, token);

                    Log($"Waiting for drag target element '{targetSelector}' to be visible...");
                    var targetNodeId = await WaitForElementVisibleAsync(targetSelector, token);

                    var srcBoxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = sourceNodeId });
                    var srcModel = srcBoxRes["model"] as JsonObject;
                    var srcContent = srcModel?["content"] as JsonArray;
                    var srcBorder = srcModel?["border"] as JsonArray ?? srcContent;

                    var tgtBoxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = targetNodeId });
                    var tgtModel = tgtBoxRes["model"] as JsonObject;
                    var tgtContent = tgtModel?["content"] as JsonArray;
                    var tgtBorder = tgtModel?["border"] as JsonArray ?? tgtContent;

                    if (srcContent == null || srcContent.Count < 8 || tgtContent == null || tgtContent.Count < 8)
                    {
                        throw new Exception("Failed to retrieve box model content for source or target element.");
                    }

                    double srcX = (offsetX != 0.0 || offsetY != 0.0) 
                        ? srcBorder[0]!.GetValue<double>() + offsetX 
                        : srcContent[0]!.GetValue<double>() + (srcContent[4]!.GetValue<double>() - srcContent[0]!.GetValue<double>()) / 2.0;
                    double srcY = (offsetX != 0.0 || offsetY != 0.0) 
                        ? srcBorder[1]!.GetValue<double>() + offsetY 
                        : srcContent[1]!.GetValue<double>() + (srcContent[5]!.GetValue<double>() - srcContent[1]!.GetValue<double>()) / 2.0;

                    double tgtX = (targetOffsetX != 0.0 || targetOffsetY != 0.0) 
                        ? tgtBorder[0]!.GetValue<double>() + targetOffsetX 
                        : tgtContent[0]!.GetValue<double>() + (tgtContent[4]!.GetValue<double>() - tgtContent[0]!.GetValue<double>()) / 2.0;
                    double tgtY = (targetOffsetX != 0.0 || targetOffsetY != 0.0) 
                        ? tgtBorder[1]!.GetValue<double>() + targetOffsetY 
                        : tgtContent[1]!.GetValue<double>() + (tgtContent[5]!.GetValue<double>() - tgtContent[1]!.GetValue<double>()) / 2.0;

                    Log($"Dragging from ({srcX:F1}, {srcY:F1}) to ({tgtX:F1}, {tgtY:F1})");
                    indicator.X = srcX;
                    indicator.Y = srcY;
                    indicator.EndX = tgtX;
                    indicator.EndY = tgtY;
                    OnStepIndicatorChanged?.Invoke(indicator);

                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mouseMoved",
                        ["x"] = srcX,
                        ["y"] = srcY,
                        ["button"] = "none",
                        ["modifiers"] = 0
                    });
                    await Task.Delay(100, token);

                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mousePressed",
                        ["x"] = srcX,
                        ["y"] = srcY,
                        ["button"] = "left",
                        ["clickCount"] = 1,
                        ["modifiers"] = 0
                    });
                    await Task.Delay(200, token);

                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mouseMoved",
                        ["x"] = tgtX,
                        ["y"] = tgtY,
                        ["button"] = "left",
                        ["modifiers"] = 0
                    });
                    await Task.Delay(200, token);

                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mouseReleased",
                        ["x"] = tgtX,
                        ["y"] = tgtY,
                        ["button"] = "left",
                        ["clickCount"] = 1,
                        ["modifiers"] = 0
                    });
                    await Task.Delay(300, token);
                    break;
                }
            case "scroll":
                {
                    double scrollX = 400;
                    double scrollY = 300;
                    var runtimeSelector = GetRuntimeSelector(step);

                    if (!string.IsNullOrEmpty(runtimeSelector))
                    {
                        Log($"Waiting for scroll target element '{runtimeSelector}' to be visible...");
                        var nodeId = await WaitForElementVisibleAsync(runtimeSelector, token);
                        var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = nodeId });
                        var model = boxRes["model"] as JsonObject;
                        var content = model?["content"] as JsonArray;
                        if (content != null && content.Count >= 8)
                        {
                            double x1 = content[0]!.GetValue<double>();
                            double y1 = content[1]!.GetValue<double>();
                            double x2 = content[4]!.GetValue<double>();
                            double y2 = content[5]!.GetValue<double>();
                            scrollX = x1 + (x2 - x1) / 2.0;
                            scrollY = y1 + (y2 - y1) / 2.0;
                        }
                    }
                    else
                    {
                        try
                        {
                            var metrics = await _cdpService.SendCommandAsync("Page.getLayoutMetrics");
                            var viewport = metrics["cssVisualViewport"] as JsonObject;
                            if (viewport != null)
                            {
                                double w = viewport["width"]?.GetValue<double>() ?? 800;
                                double h = viewport["height"]?.GetValue<double>() ?? 600;
                                scrollX = w / 2.0;
                                scrollY = h / 2.0;
                            }
                        }
                        catch { }
                    }

                    string direction = "down";
                    double amount = 100;

                    if (!string.IsNullOrEmpty(step.Value))
                    {
                        var props = ParseKeyValuePairs(step.Value);
                        if (props.TryGetValue("direction", out var dir))
                        {
                            direction = dir;
                        }
                        if (props.TryGetValue("amount", out var amt) && double.TryParse(amt, out double a))
                        {
                            amount = a;
                        }
                        else if (double.TryParse(step.Value, out double singleAmt))
                        {
                            amount = singleAmt;
                        }
                    }

                    double deltaX = 0;
                    double deltaY = 0;
                    if (direction.Equals("down", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaY = -amount;
                    }
                    else if (direction.Equals("up", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaY = amount;
                    }
                    else if (direction.Equals("left", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaX = amount;
                    }
                    else if (direction.Equals("right", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaX = -amount;
                    }

                    Log($"Scrolling at ({scrollX:F1}, {scrollY:F1}) with deltaX={deltaX}, deltaY={deltaY}");
                    indicator.X = scrollX;
                    indicator.Y = scrollY;
                    OnStepIndicatorChanged?.Invoke(indicator);
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                    {
                        ["type"] = "mouseWheel",
                        ["x"] = scrollX,
                        ["y"] = scrollY,
                        ["deltaX"] = deltaX,
                        ["deltaY"] = deltaY,
                        ["button"] = "none",
                        ["modifiers"] = 0
                    });
                    await Task.Delay(200, token);
                    break;
                }
            case "pressKey":
                {
                    var keyValue = GetStepValue(step, "key", "value");
                    if (string.IsNullOrEmpty(keyValue)) keyValue = step.Value ?? "";
                    if (string.IsNullOrEmpty(keyValue))
                    {
                        throw new Exception("pressKey step requires a Value (key).");
                    }
                    var runtimeSelector = GetRuntimeSelector(step);
                    if (!string.IsNullOrEmpty(runtimeSelector))
                    {
                        Log($"Focusing element '{runtimeSelector}' before pressing key...");
                        int nodeId = await WaitForElementVisibleAsync(runtimeSelector, token);
                        await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = nodeId });
                        await Task.Delay(100, token);
                    }
                    Log($"Pressing key '{keyValue}'");
                    string keyName = keyValue.Trim();
                    if (keyName.Equals("enter", StringComparison.OrdinalIgnoreCase)) keyName = "Enter";
                    else if (keyName.Equals("backspace", StringComparison.OrdinalIgnoreCase)) keyName = "Backspace";
                    else if (keyName.Equals("escape", StringComparison.OrdinalIgnoreCase)) keyName = "Escape";
                    else if (keyName.Equals("tab", StringComparison.OrdinalIgnoreCase)) keyName = "Tab";
                    else if (keyName.Equals("space", StringComparison.OrdinalIgnoreCase)) keyName = "Space";
                    else if (keyName.Equals("delete", StringComparison.OrdinalIgnoreCase)) keyName = "Delete";
                    else if (keyName.Equals("home", StringComparison.OrdinalIgnoreCase)) keyName = "Home";
                    else if (keyName.Equals("end", StringComparison.OrdinalIgnoreCase)) keyName = "End";

                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "rawKeyDown",
                        ["key"] = keyName,
                        ["code"] = keyName,
                        ["text"] = "",
                        ["modifiers"] = 0
                    });
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
                    {
                        ["type"] = "keyUp",
                        ["key"] = keyName,
                        ["code"] = keyName,
                        ["text"] = "",
                        ["modifiers"] = 0
                    });
                    await Task.Delay(200, token);
                    break;
                }
            case "pasteText":
                {
                    string clipboardText = step.Value ?? "";
                    if (string.IsNullOrEmpty(clipboardText))
                    {
                        try
                        {
                            Avalonia.Input.Platform.IClipboard? clipboard = null;
                            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                            {
                                clipboard = desktop.MainWindow?.Clipboard;
                            }
                            else if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView)
                            {
                                clipboard = Avalonia.Controls.TopLevel.GetTopLevel(singleView.MainView)?.Clipboard;
                            }
                            if (clipboard != null)
                            {
                                clipboardText = await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard) ?? "";
                            }
                        }
                        catch { }
                    }
                    Log($"Pasting text: '{clipboardText}'");
                    await _cdpService.SendCommandAsync("Input.insertText", new JsonObject { ["text"] = clipboardText });
                    await Task.Delay(200, token);
                    break;
                }
            case "eraseText":
                {
                    int count = GetStepInt(step, 50, "characters", "amount", "value");
                    if (int.TryParse(step.Value, out int parsed)) count = parsed;
                    count = Math.Clamp(count, 1, 100);
                    Log($"Erasing {count} characters...");
                    for (int i = 0; i < count; i++)
                    {
                        await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject { ["type"] = "rawKeyDown", ["key"] = "Backspace", ["code"] = "Backspace", ["windowsVirtualKeyCode"] = 8 });
                        await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject { ["type"] = "keyUp", ["key"] = "Backspace", ["code"] = "Backspace", ["windowsVirtualKeyCode"] = 8 });
                        await Task.Delay(50, token);
                    }
                    await Task.Delay(150, token);
                    break;
                }
            case "swipe":
                {
                    double startX = 400, startY = 300, endX = 200, endY = 300;
                    double width = 800, height = 600;
                    try
                    {
                        var metrics = await _cdpService.SendCommandAsync("Page.getLayoutMetrics");
                        var viewport = metrics["cssVisualViewport"] as JsonObject;
                        if (viewport != null)
                        {
                            width = viewport["width"]?.GetValue<double>() ?? width;
                            height = viewport["height"]?.GetValue<double>() ?? height;
                        }
                    }
                    catch { }

                    string direction = "left";
                    var props = ParseKeyValuePairs(step.Value);
                    var parameterDirection = GetParameterString(step, "direction");
                    if (!string.IsNullOrEmpty(parameterDirection)) props["direction"] = parameterDirection;
                    var parameterStart = GetParameterString(step, "start");
                    if (!string.IsNullOrEmpty(parameterStart)) props["start"] = parameterStart;
                    var parameterEnd = GetParameterString(step, "end");
                    if (!string.IsNullOrEmpty(parameterEnd)) props["end"] = parameterEnd;
                    if (props.TryGetValue("direction", out var sDir))
                    {
                        direction = sDir;
                    }

                    if (props.TryGetValue("start", out var startStr))
                    {
                        var sc = ParseCoordinates(startStr);
                        if (sc.HasValue) { startX = sc.Value.x; startY = sc.Value.y; }
                    }
                    else
                    {
                        if (direction.Equals("left", StringComparison.OrdinalIgnoreCase)) { startX = width * 0.8; startY = height * 0.5; }
                        else if (direction.Equals("right", StringComparison.OrdinalIgnoreCase)) { startX = width * 0.2; startY = height * 0.5; }
                        else if (direction.Equals("up", StringComparison.OrdinalIgnoreCase)) { startX = width * 0.5; startY = height * 0.8; }
                        else if (direction.Equals("down", StringComparison.OrdinalIgnoreCase)) { startX = width * 0.5; startY = height * 0.2; }
                    }

                    if (TryGetParameter(step, "from", out var fromValue) && fromValue != null)
                    {
                        var fromSelector = fromValue is IReadOnlyDictionary<string, object?> fromMap
                            ? FlowCommandCatalog.BuildRuntimeSelector(fromMap, "")
                            : FlowCommandCatalog.ScalarToString(fromValue);
                        if (!string.IsNullOrEmpty(fromSelector))
                        {
                            var fromStep = new TestStudioStepModel { Action = "swipe", Selector = fromSelector };
                            var fromPoint = await ResolveCoordinatesAsync(fromStep, token);
                            startX = fromPoint.x;
                            startY = fromPoint.y;
                        }
                    }

                    if (props.TryGetValue("end", out var endStr))
                    {
                        var ec = ParseCoordinates(endStr);
                        if (ec.HasValue) { endX = ec.Value.x; endY = ec.Value.y; }
                    }
                    else
                    {
                        if (direction.Equals("left", StringComparison.OrdinalIgnoreCase)) { endX = width * 0.2; endY = height * 0.5; }
                        else if (direction.Equals("right", StringComparison.OrdinalIgnoreCase)) { endX = width * 0.8; endY = height * 0.5; }
                        else if (direction.Equals("up", StringComparison.OrdinalIgnoreCase)) { endX = width * 0.5; endY = height * 0.2; }
                        else if (direction.Equals("down", StringComparison.OrdinalIgnoreCase)) { endX = width * 0.5; endY = height * 0.8; }
                    }

                    Log($"Swiping from ({startX:F1}, {startY:F1}) to ({endX:F1}, {endY:F1})");
                    indicator.X = startX;
                    indicator.Y = startY;
                    indicator.EndX = endX;
                    indicator.EndY = endY;
                    OnStepIndicatorChanged?.Invoke(indicator);
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mousePressed", ["x"] = startX, ["y"] = startY, ["button"] = "left", ["clickCount"] = 1 });
                    int duration = GetStepInt(step, 400, "duration");
                    int stepsCount = Math.Clamp(duration / 40, 4, 30);
                    for (int i = 1; i <= stepsCount; i++)
                    {
                        double t = (double)i / stepsCount;
                        double curX = startX + (endX - startX) * t;
                        double curY = startY + (endY - startY) * t;
                        await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseMoved", ["x"] = curX, ["y"] = curY, ["button"] = "left" });
                        await Task.Delay(Math.Max(1, duration / stepsCount), token);
                    }
                    await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject { ["type"] = "mouseReleased", ["x"] = endX, ["y"] = endY, ["button"] = "left", ["clickCount"] = 1 });
                    await Task.Delay(200, token);
                    break;
                }
            case "stopApp":
            case "killApp":
                {
                    var appId = GetStepValue(step, "appId");
                    Log(string.IsNullOrEmpty(appId) ? "Closing target application target connection..." : $"Closing target application '{appId}'...");
                    try
                    {
                        await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject { ["expression"] = "Avalonia.Application.Current?.Shutdown()" });
                    }
                    catch { }
                    await Task.Delay(200, token);
                    break;
                }
            case "clearState":
                {
                    var appId = GetStepValue(step, "appId");
                    Log(string.IsNullOrEmpty(appId) ? "Reloading target application page/view to reset state..." : $"Reloading target '{appId}' to reset state...");
                    await _cdpService.SendCommandAsync("Page.reload", new JsonObject());
                    await Task.Delay(500, token);
                    break;
                }
            case "setOrientation":
                {
                    string orientation = GetStepValue(step, "orientation");
                    if (string.IsNullOrEmpty(orientation)) orientation = step.Value ?? "";
                    orientation = orientation.Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(orientation)) orientation = "portrait";
                    Log($"Setting device metrics override to orientation: {orientation}");
                    bool isLandscape = orientation.Contains("landscape", StringComparison.OrdinalIgnoreCase);
                    int w = isLandscape ? 1280 : 800;
                    int h = isLandscape ? 800 : 1280;
                    try
                    {
                        await _cdpService.SendCommandAsync("Emulation.setDeviceMetricsOverride", new JsonObject
                        {
                            ["width"] = w,
                            ["height"] = h,
                            ["deviceScaleFactor"] = 1.0,
                            ["mobile"] = true,
                            ["screenOrientation"] = new JsonObject { ["type"] = isLandscape ? "landscapePrimary" : "portraitPrimary", ["angle"] = isLandscape ? 90 : 0 }
                        });
                    }
                    catch { }
                    await Task.Delay(200, token);
                    break;
                }
            case "setLocation":
                {
                    double lat = 37.7749;
                    double lon = -122.4194;
                    var props = ParseKeyValuePairs(step.Value);
                    var parameterLat = GetParameterString(step, "latitude");
                    var parameterLon = GetParameterString(step, "longitude");
                    if (!string.IsNullOrEmpty(parameterLat)) props["latitude"] = parameterLat;
                    if (!string.IsNullOrEmpty(parameterLon)) props["longitude"] = parameterLon;
                    if (props.TryGetValue("latitude", out var latStr)) double.TryParse(latStr, out lat);
                    if (props.TryGetValue("longitude", out var lonStr)) double.TryParse(lonStr, out lon);
                    Log($"Setting geolocation override mock: Lat={lat}, Lon={lon}");
                    try
                    {
                        await _cdpService.SendCommandAsync("Emulation.setGeolocationOverride", new JsonObject
                        {
                            ["latitude"] = lat,
                            ["longitude"] = lon,
                            ["accuracy"] = 100
                        });
                    }
                    catch { }
                    await Task.Delay(200, token);
                    break;
                }
            case "takeScreenshot":
                {
                    string filename = GetStepValue(step, "path");
                    if (string.IsNullOrEmpty(filename)) filename = step.Value?.Trim() ?? "";
                    if (string.IsNullOrEmpty(filename)) filename = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    if (!filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        filename += ".png";
                    }
                    Log($"Capturing page screenshot as {filename}...");
                    try
                    {
                        var res = await _cdpService.SendCommandAsync("Page.captureScreenshot", new JsonObject());
                        string base64Data = res["data"]?.GetValue<string>() ?? "";
                        if (!string.IsNullOrEmpty(base64Data))
                        {
                            byte[] bytes = Convert.FromBase64String(base64Data);
                            await System.IO.File.WriteAllBytesAsync(filename, bytes);
                            Log($"Screenshot written to file: {System.IO.Path.GetFullPath(filename)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to take screenshot: {ex.Message}");
                    }
                    break;
                }
            case "assertTrue":
                {
                    if (string.IsNullOrEmpty(step.Value)) throw new Exception("assertTrue requires a value containing the expression.");
                    Log($"Asserting expression evaluates to true: {step.Value}");
                    var evalRes = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject { ["expression"] = step.Value });
                    if (evalRes["exceptionDetails"] != null)
                    {
                        var exceptionText = evalRes["exceptionDetails"]?["exception"]?["description"]?.GetValue<string>() ?? "Unknown evaluation error";
                        throw new Exception($"Expression evaluation failed with exception: {exceptionText}");
                    }
                    var resultNode = evalRes["result"] as JsonObject;
                    var val = resultNode?["value"]?.GetValue<object>()?.ToString();
                    if (val != "True" && val != "true" && val != "1")
                    {
                        throw new Exception($"Assertion failed: Expression '{step.Value}' evaluated to '{val ?? "null"}' (not true).");
                    }
                    Log("Assertion check passed successfully.");
                    break;
                }
            case "assertFalse":
                {
                    if (string.IsNullOrEmpty(step.Value)) throw new Exception("assertFalse requires a value containing the expression.");
                    Log($"Asserting expression evaluates to false: {step.Value}");
                    var evalRes = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject { ["expression"] = step.Value });
                    if (evalRes["exceptionDetails"] != null)
                    {
                        var exceptionText = evalRes["exceptionDetails"]?["exception"]?["description"]?.GetValue<string>() ?? "Unknown evaluation error";
                        throw new Exception($"Expression evaluation failed with exception: {exceptionText}");
                    }
                    var resultNode = evalRes["result"] as JsonObject;
                    var val = resultNode?["value"]?.GetValue<object>()?.ToString();
                    if (val == "True" || val == "true" || val == "1")
                    {
                        throw new Exception($"Assertion failed: Expression '{step.Value}' evaluated to '{val ?? "null"}' (not false).");
                    }
                    Log("Assertion check passed successfully.");
                    break;
                }
            case "setAirplaneMode":
                {
                    string mode = GetStepValue(step, "enabled");
                    if (string.IsNullOrEmpty(mode))
                    {
                        mode = step.Value?.Trim().ToLower() ?? "off";
                    }
                    bool offline = mode == "on" || mode == "true" || mode == "1";
                    _isAirplaneModeEnabled = offline;
                    Log($"Setting network offline/airplane mode to: {offline}");
                    await SetNetworkOfflineAsync(offline);
                    break;
                }
            case "evalScript":
            case "runScript":
                {
                    var script = step.Value ?? "";
                    if (action == "runScript")
                    {
                        var scriptFile = GetStepValue(step, "file");
                        if (!string.IsNullOrEmpty(scriptFile))
                        {
                            if (!File.Exists(scriptFile))
                            {
                                throw new FileNotFoundException($"Script file not found: {scriptFile}", scriptFile);
                            }
                            script = await File.ReadAllTextAsync(scriptFile, token);
                        }
                    }
                    if (string.IsNullOrEmpty(script)) throw new Exception($"{action} requires a script value.");
                    Log($"Evaluating script on target: {script}");
                    var evalRes = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject { ["expression"] = script });
                    var resultNode = evalRes["result"] as JsonObject;
                    Log($"Script execution result: {resultNode?["value"]?.ToString() ?? "void"}");
                    break;
                }
            case "repeat":
                {
                    int times = 1;
                    if (int.TryParse(step.Value, out int t)) times = t;
                    Log($"Repeating block up to {times} times...");

                    for (int i = 0; i < times; i++)
                    {
                        token.ThrowIfCancellationRequested();

                        if (!string.IsNullOrEmpty(step.WhileConditionType) && !string.IsNullOrEmpty(step.WhileConditionValue))
                        {
                            bool conditionMet = await EvaluateConditionAsync(step.WhileConditionType, step.WhileConditionValue, token);
                            if (!conditionMet)
                            {
                                Log($"Condition '{step.WhileConditionType}: {step.WhileConditionValue}' not met. Breaking repeat loop at iteration {i + 1}.");
                                break;
                            }
                        }

                        Log($"Repeat loop iteration {i + 1}/{times}...");
                        if (step.NestedSteps != null && step.NestedSteps.Count > 0)
                        {
                            foreach (var nestedStep in step.NestedSteps)
                            {
                                await ExecuteSingleStepAsync(nestedStep, env, token);
                            }
                        }
                    }
                    break;
                }
            case "retry":
                {
                    int maxRetries = 1;
                    if (int.TryParse(step.Value, out int r)) maxRetries = r;
                    Log($"Executing retry block (max retries={maxRetries})...");

                    Exception? lastException = null;
                    bool success = false;

                    for (int attempt = 1; attempt <= 1 + maxRetries; attempt++)
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            if (attempt > 1)
                            {
                                Log($"Retrying block: attempt {attempt - 1}/{maxRetries}...");
                            }

                            if (step.NestedSteps != null && step.NestedSteps.Count > 0)
                            {
                                foreach (var nestedStep in step.NestedSteps)
                                {
                                    nestedStep.Status = StepStatus.Pending;
                                    nestedStep.ErrorMessage = null;
                                }

                                foreach (var nestedStep in step.NestedSteps)
                                {
                                    nestedStep.Status = StepStatus.Running;
                                    await ExecuteSingleStepAsync(nestedStep, env, token);
                                    nestedStep.Status = StepStatus.Passed;
                                }
                            }
                            success = true;
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            Log($"Block execution failed on attempt {attempt}: {ex.Message}");
                            if (step.NestedSteps != null)
                            {
                                foreach (var nestedStep in step.NestedSteps)
                                {
                                    if (nestedStep.Status == StepStatus.Running)
                                    {
                                        nestedStep.Status = StepStatus.Failed;
                                        nestedStep.ErrorMessage = ex.Message;
                                    }
                                }
                            }
                        }
                    }

                    if (!success)
                    {
                        throw new Exception($"Retry block failed after {1 + maxRetries} attempts. Last error: {lastException?.Message}", lastException);
                    }
                    break;
                }
            case "runFlow":
                {
                    var localEnv = new Dictionary<string, string>(env, StringComparer.OrdinalIgnoreCase);
                    if (step.Parameters.TryGetValue("env", out var envObj) && envObj != null)
                    {
                        if (envObj is System.Collections.IDictionary dictEnv)
                        {
                            foreach (System.Collections.DictionaryEntry entry in dictEnv)
                            {
                                var key = entry.Key?.ToString();
                                if (key != null)
                                {
                                    localEnv[key] = entry.Value?.ToString() ?? "";
                                }
                            }
                        }
                    }

                    if (step.NestedSteps != null && step.NestedSteps.Count > 0)
                    {
                        Log($"Executing inline flow with {step.NestedSteps.Count} commands...");
                        foreach (var subStep in step.NestedSteps)
                        {
                            await ExecuteSingleStepAsync(subStep, localEnv, token);
                        }
                        break;
                    }

                    string flowPath = GetStepValue(step, "file");
                    if (string.IsNullOrEmpty(flowPath)) flowPath = step.Value?.Trim() ?? "";
                    if (string.IsNullOrEmpty(flowPath)) throw new Exception("runFlow requires a path to a YAML flow file.");

                    string? currentFlowPath = _executingFileStack.Count > 0 ? _executingFileStack.Peek() : CurrentFlowFilePath;
                    string resolvedPath = ResolveFlowPath(flowPath, currentFlowPath);

                    if (_executingFileStack.Any(p => string.Equals(p, resolvedPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        Log("circular dependency");
                        throw new InvalidOperationException("circular dependency");
                    }

                    Log($"Running nested flow: {resolvedPath}...");
                    _executingFileStack.Push(resolvedPath);
                    try
                    {
                        string subYaml = await System.IO.File.ReadAllTextAsync(resolvedPath);
                        var subSteps = TestStudioYamlParser.Parse(subYaml, out _, out _).Select(TestStudioStepModel.FromCoreStep).ToList();
                        Log($"Executing {subSteps.Count} steps recursively from nested flow: {resolvedPath}");
                        foreach (var subStep in subSteps)
                        {
                            await ExecuteSingleStepAsync(subStep, localEnv, token);
                        }
                    }
                    finally
                    {
                        _executingFileStack.Pop();
                    }
                    Log($"Completed nested flow: {resolvedPath}");
                    break;
                }
            case "scrollUntilVisible":
                {
                    var runtimeSelector = GetRuntimeSelector(step);
                    if (string.IsNullOrEmpty(runtimeSelector))
                    {
                        throw new Exception("scrollUntilVisible step requires a Selector.");
                    }

                    string direction = "down";
                    int maxScrolls = 10;
                    double amount = 150;
                    double timeoutSeconds = Math.Max(1.0, GetStepDouble(step, 10.0, "timeout") / 1000.0);

                    if (!string.IsNullOrEmpty(step.Value))
                    {
                        var props = ParseKeyValuePairs(step.Value);
                        if (props.TryGetValue("direction", out var dir))
                        {
                            direction = dir;
                        }
                        if (props.TryGetValue("maxscrolls", out var msStr) && int.TryParse(msStr, out int ms))
                        {
                            maxScrolls = ms;
                        }
                    }

                    double scrollX = 400;
                    double scrollY = 300;
                    try
                    {
                        var metrics = await _cdpService.SendCommandAsync("Page.getLayoutMetrics");
                        var viewport = metrics["cssVisualViewport"] as JsonObject;
                        if (viewport != null)
                        {
                            double w = viewport["width"]?.GetValue<double>() ?? 800;
                            double h = viewport["height"]?.GetValue<double>() ?? 600;
                            scrollX = w / 2.0;
                            scrollY = h / 2.0;
                        }
                    }
                    catch { }

                    double deltaX = 0;
                    double deltaY = 0;
                    if (direction.Equals("down", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaY = -amount;
                    }
                    else if (direction.Equals("up", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaY = amount;
                    }
                    else if (direction.Equals("left", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaX = amount;
                    }
                    else if (direction.Equals("right", StringComparison.OrdinalIgnoreCase))
                    {
                        deltaX = -amount;
                    }

                    int scrollCount = 0;
                    bool visible = false;
                    var scrollStart = DateTime.UtcNow;
                    while (scrollCount <= maxScrolls)
                    {
                        token.ThrowIfCancellationRequested();

                        var nodeId = await CheckElementVisibleAsync(runtimeSelector);
                        if (nodeId.HasValue)
                        {
                            visible = true;
                            Log($"Element '{runtimeSelector}' is visible after {scrollCount} scrolls.");
                            break;
                        }

                        if (scrollCount == maxScrolls || (DateTime.UtcNow - scrollStart).TotalSeconds > timeoutSeconds)
                        {
                            break;
                        }

                        Log($"Element '{runtimeSelector}' not visible. Scrolling ({scrollCount + 1}/{maxScrolls}) at ({scrollX:F1}, {scrollY:F1}) with deltaX={deltaX}, deltaY={deltaY}...");
                        await _cdpService.SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                        {
                            ["type"] = "mouseWheel",
                            ["x"] = scrollX,
                            ["y"] = scrollY,
                            ["deltaX"] = deltaX,
                            ["deltaY"] = deltaY,
                            ["button"] = "none",
                            ["modifiers"] = 0
                        });

                        scrollCount++;
                        await Task.Delay(300, token);
                    }

                    if (!visible)
                    {
                        throw new Exception($"Element '{runtimeSelector}' did not become visible after {maxScrolls} scrolls.");
                    }
                    break;
                }
            case "extendedWaitUntil":
                {
                    var timeoutMs = GetStepInt(step, 30000, "timeout");
                    var timeoutSeconds = Math.Max(1.0, timeoutMs / 1000.0);
                    if (TryGetParameter(step, "visible", out var visibleSelector) && visibleSelector != null)
                    {
                        var selector = visibleSelector is IReadOnlyDictionary<string, object?> visibleMap
                            ? FlowCommandCatalog.BuildRuntimeSelector(visibleMap, "")
                            : FlowCommandCatalog.ScalarToString(visibleSelector);
                        Log($"Waiting up to {timeoutMs} ms for '{selector}' to become visible...");
                        await WaitForElementVisibleAsync(selector, token, timeoutSeconds);
                        break;
                    }
                    if (TryGetParameter(step, "notVisible", out var notVisibleSelector) && notVisibleSelector != null)
                    {
                        var selector = notVisibleSelector is IReadOnlyDictionary<string, object?> notVisibleMap
                            ? FlowCommandCatalog.BuildRuntimeSelector(notVisibleMap, "")
                            : FlowCommandCatalog.ScalarToString(notVisibleSelector);
                        Log($"Waiting up to {timeoutMs} ms for '{selector}' to become not visible...");
                        await WaitForElementNotVisibleAsync(selector, token, timeoutSeconds);
                        break;
                    }
                    throw new Exception("extendedWaitUntil requires visible or notVisible.");
                }
            case "hideKeyboard":
                {
                    Log("Dismissing keyboard/focus with Escape.");
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject { ["type"] = "rawKeyDown", ["key"] = "Escape", ["code"] = "Escape" });
                    await _cdpService.SendCommandAsync("Input.dispatchKeyEvent", new JsonObject { ["type"] = "keyUp", ["key"] = "Escape", ["code"] = "Escape" });
                    await Task.Delay(100, token);
                    break;
                }
            case "setClipboard":
                {
                    var clipboardText = GetStepValue(step, "text", "value");
                    if (string.IsNullOrEmpty(clipboardText))
                    {
                        clipboardText = step.Value ?? "";
                    }
                    await SetClipboardTextAsync(clipboardText);
                    Log("Clipboard text set.");
                    break;
                }
            case "toggleAirplaneMode":
                {
                    _isAirplaneModeEnabled = !_isAirplaneModeEnabled;
                    await SetNetworkOfflineAsync(_isAirplaneModeEnabled);
                    Log($"Toggled offline/airplane mode to: {_isAirplaneModeEnabled}");
                    break;
                }
            case "clearKeychain":
                {
                    Log("clearKeychain is a mobile secure-storage reset; no desktop CDP action is required.");
                    break;
                }
            case "setPermissions":
                {
                    Log("setPermissions accepted for flow parity; desktop Avalonia target has no mobile permission surface to mutate.");
                    break;
                }
            case "addMedia":
                {
                    var mediaItems = new List<string>();
                    if (TryGetParameter(step, "items", out var items) && items is IReadOnlyList<object?> list)
                    {
                        mediaItems.AddRange(list.Select(FlowCommandCatalog.ScalarToString));
                    }
                    else if (!string.IsNullOrEmpty(step.Value))
                    {
                        mediaItems.Add(step.Value);
                    }
                    Log($"Registered media inputs: {string.Join(", ", mediaItems)}");
                    break;
                }
            case "startRecording":
                {
                    Log("Starting command-level screen recording.");
                    _isRecordingVideo = true;
                    await _cdpService.SendCommandAsync("Page.startScreencast", new JsonObject
                    {
                        ["format"] = "jpeg",
                        ["quality"] = 80,
                        ["everyNthFrame"] = 1
                    });
                    break;
                }
            case "stopRecording":
                {
                    Log("Stopping command-level screen recording.");
                    _isRecordingVideo = false;
                    try
                    {
                        await _cdpService.SendCommandAsync("Page.stopScreencast");
                    }
                    catch { }
                    break;
                }
            case "waitForAnimationToEnd":
                {
                    var timeout = GetStepInt(step, 15000, "timeout");
                    var settleDelay = Math.Min(timeout, 1000);
                    Log($"Waiting for UI to settle for {settleDelay} ms.");
                    await Task.Delay(settleDelay, token);
                    break;
                }
            case "assertScreenshot":
                {
                    var path = GetStepValue(step, "path");
                    if (string.IsNullOrEmpty(path))
                    {
                        path = step.Value ?? "";
                    }
                    if (string.IsNullOrEmpty(path))
                    {
                        throw new Exception("assertScreenshot requires a reference path.");
                    }
                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException($"Reference screenshot not found: {path}", path);
                    }
                    await _cdpService.SendCommandAsync("Page.captureScreenshot", new JsonObject());
                    Log($"Reference screenshot exists: {path}");
                    break;
                }
            case "assertWithAI":
            case "assertNoDefectsWithAI":
            case "extractTextWithAI":
                {
                    if (!IsOptionalAiStep(step))
                    {
                        throw new NotSupportedException($"{action} requires an AI analysis provider for non-optional execution.");
                    }
                    Log($"{action} accepted as optional; no AI analysis provider is configured in the desktop inspector.");
                    break;
                }
            case "travel":
                {
                    Log("travel accepted; desktop target receives no route simulation beyond setLocation support.");
                    break;
                }
            case "inputRandomEmail":
            case "inputRandomPersonName":
            case "inputRandomNumber":
            case "inputRandomText":
            case "inputRandomCityName":
            case "inputRandomCountryName":
            case "inputRandomColorName":
                {
                    var randomText = GenerateRandomInput(action, step);
                    Log($"Inputting generated text for {action}.");
                    await _cdpService.SendCommandAsync("Input.insertText", new JsonObject { ["text"] = randomText });
                    await Task.Delay(150, token);
                    break;
                }
            default:
                throw new NotSupportedException($"Step action '{action}' is not supported.");
        }
    }

    private async Task<int> QuerySelectorWithFallbackAsync(int rootNodeId, string selector)
    {
        try
        {
            var qParams = new JsonObject { ["nodeId"] = rootNodeId, ["selector"] = selector };
            var qRes = await _cdpService.SendCommandAsync("DOM.querySelector", qParams);
            int nodeId = qRes["nodeId"]?.GetValue<int>() ?? 0;
            if (nodeId > 0)
            {
                return nodeId;
            }
        }
        catch
        {
            // querySelector failed, fallback to JS
        }

        return await QueryNodeViaJsAsync(selector);
    }

    private async Task<int> QueryNodeViaJsAsync(string selector)
    {
        var escapedSelector = selector.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        var jsExpression = $$"""
        (function() {
            var selector = "{{escapedSelector}}";
            function findElement(sel) {
                var containsMatch = sel.match(/:contains\("((?:[^"\\]|\\.)*)"\)/) || sel.match(/:contains\('((?:[^'\\]|\\.)*)'\)/);
                if (!containsMatch) {
                    try {
                        var el = document.querySelector(sel);
                        if (el) return el;
                    } catch (e) {}
                    var accMatch = sel.match(/\[AccessibilityId=["'](.*?)["']\]/);
                    if (accMatch) {
                        var val = accMatch[1];
                        return document.querySelector('[AccessibilityId="' + val + '"]') || 
                               document.querySelector('[AutomationId="' + val + '"]') ||
                               document.querySelector('[id="' + val + '"]') ||
                               document.querySelector('[name="' + val + '"]');
                    }
                    return null;
                }
                
                var containsPart = containsMatch[0];
                var text = containsMatch[1].replace(/\\(.)/g, '$1').toLowerCase();
                var index = sel.indexOf(containsPart);
                var before = sel.substring(0, index).trim() || "*";
                var after = sel.substring(index + containsPart.length).trim();
                
                if (before.indexOf('[AccessibilityId=') >= 0) {
                    var accMatch = before.match(/\[AccessibilityId=["'](.*?)["']\]/);
                    if (accMatch) {
                        var val = accMatch[1];
                        var candidates = [];
                        var selectors = [
                            '[AccessibilityId="' + val + '"]', 
                            '[AutomationId="' + val + '"]', 
                            '[id="' + val + '"]',
                            '[name="' + val + '"]'
                        ];
                        for (var i = 0; i < selectors.length; i++) {
                            try {
                                var nodes = document.querySelectorAll(selectors[i]);
                                if (nodes.length > 0) {
                                    candidates = nodes;
                                    break;
                                }
                            } catch(e) {}
                        }
                        return findInCandidates(candidates, text, after);
                    }
                }
                
                try {
                    var candidates = document.querySelectorAll(before);
                    return findInCandidates(candidates, text, after);
                } catch(e) {
                    return null;
                }
            }
            
            function findInCandidates(candidates, text, after) {
                for (var i = 0; i < candidates.length; i++) {
                    var el = candidates[i];
                    var elText = el.textContent || el.innerText || "";
                    if (elText.toLowerCase().indexOf(text) >= 0) {
                        if (!after) {
                            return el;
                        } else {
                            var subEl = el.querySelector(after);
                            if (subEl) return subEl;
                        }
                    }
                }
                return null;
            }
            return findElement(selector);
        })()
        """;

        try
        {
            var evalParams = new JsonObject
            {
                ["expression"] = jsExpression,
                ["returnByValue"] = false
            };
            var evalRes = await _cdpService.SendCommandAsync("Runtime.evaluate", evalParams);
            var exceptionDetails = evalRes["exceptionDetails"];
            if (exceptionDetails != null)
            {
                return 0;
            }

            var resultObj = evalRes["result"] as JsonObject;
            var objectId = resultObj?["objectId"]?.GetValue<string>();
            if (string.IsNullOrEmpty(objectId))
            {
                return 0;
            }

            var reqNodeParams = new JsonObject { ["objectId"] = objectId };
            var reqNodeRes = await _cdpService.SendCommandAsync("DOM.requestNode", reqNodeParams);
            int nodeId = reqNodeRes["nodeId"]?.GetValue<int>() ?? 0;
            return nodeId;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<int> WaitForElementVisibleAsync(string selector, CancellationToken token, double timeoutSeconds = 5.0)
    {
        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalSeconds < timeoutSeconds)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var docRes = await _cdpService.SendCommandAsync("DOM.getDocument", new JsonObject { ["pierce"] = true });
                var root = docRes["root"] as JsonObject;
                int rootNodeId = root?["nodeId"]?.GetValue<int>() ?? 1;

                int nodeId = await QuerySelectorWithFallbackAsync(rootNodeId, selector);
                if (nodeId > 0)
                {
                    var (w, h) = await GetElementSizeAsync(nodeId);
                    if (w > 0 && h > 0)
                    {
                        return nodeId;
                    }
                }
            }
            catch
            {
                // Ignore and retry
            }
            await Task.Delay(200, token);
        }
        throw new TimeoutException($"Element with selector '{selector}' was not visible within {timeoutSeconds:0.#} seconds.");
    }

    private async Task WaitForElementNotVisibleAsync(string selector, CancellationToken token, double timeoutSeconds = 5.0)
    {
        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalSeconds < timeoutSeconds)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var docRes = await _cdpService.SendCommandAsync("DOM.getDocument", new JsonObject { ["pierce"] = true });
                var root = docRes["root"] as JsonObject;
                int rootNodeId = root?["nodeId"]?.GetValue<int>() ?? 1;

                int nodeId = await QuerySelectorWithFallbackAsync(rootNodeId, selector);
                if (nodeId == 0)
                {
                    return;
                }
                var (w, h) = await GetElementSizeAsync(nodeId);
                if (w <= 0 || h <= 0)
                {
                    return;
                }
            }
            catch
            {
                return;
            }
            await Task.Delay(200, token);
        }
        throw new TimeoutException($"Element with selector '{selector}' was still visible after {timeoutSeconds:0.#} seconds.");
    }

    private async Task<int?> CheckElementVisibleAsync(string selector)
    {
        try
        {
            var docRes = await _cdpService.SendCommandAsync("DOM.getDocument", new JsonObject { ["pierce"] = true });
            var root = docRes["root"] as JsonObject;
            int rootNodeId = root?["nodeId"]?.GetValue<int>() ?? 1;

            int nodeId = await QuerySelectorWithFallbackAsync(rootNodeId, selector);
            if (nodeId > 0)
            {
                var (w, h) = await GetElementSizeAsync(nodeId);
                if (w > 0 && h > 0)
                {
                    return nodeId;
                }
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }

    private async Task<(double width, double height)> GetElementSizeAsync(int nodeId)
    {
        try
        {
            var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = nodeId });
            var model = boxRes["model"] as JsonObject;
            var content = model?["content"] as JsonArray;
            if (content != null && content.Count >= 8)
            {
                double x1 = content[0]!.GetValue<double>();
                double y1 = content[1]!.GetValue<double>();
                double x2 = content[4]!.GetValue<double>();
                double y2 = content[5]!.GetValue<double>();
                return (x2 - x1, y2 - y1);
            }
        }
        catch
        {
            // Ignore
        }
        return (0, 0);
    }

    public async Task AddInteractiveStepAsync(TestStudioStepModel step)
    {
        // 1. Mark as running & current
        step.Status = StepStatus.Running;
        step.IsCurrent = true;

        try
        {
            // 2. Immediately execute the action in real-time
            await ExecuteSingleStepAsync(step, CancellationToken.None);

            // 3. Mark as passed
            step.Status = StepStatus.Passed;
            Log($"Added and executed step: {step.ActionDisplay} (Passed)");
        }
        catch (Exception ex)
        {
            // 4. If it fails, log error and show it to user
            step.Status = StepStatus.Failed;
            step.ErrorMessage = ex.Message;
            Log($"Failed to execute step {step.ActionDisplay} immediately: {ex.Message}");
        }
        finally
        {
            step.IsCurrent = false;
            // 5. Append to step list
            Steps.Add(step);
        }
    }

    public async Task RunSingleStepAsync(TestStudioStepModel step)
    {
        if (IsExecuting) return;

        IsExecuting = true;
        step.Status = StepStatus.Running;
        step.IsCurrent = true;
        step.ErrorMessage = null;

        try
        {
            await ExecuteSingleStepAsync(step, CancellationToken.None);
            step.Status = StepStatus.Passed;
        }
        catch (Exception ex)
        {
            step.Status = StepStatus.Failed;
            step.ErrorMessage = ex.Message;
            Log($"Failed to execute step {step.ActionDisplay}: {ex.Message}");
        }
        finally
        {
            step.IsCurrent = false;
            IsExecuting = false;
        }
    }

    public async Task AddTapAsync()
    {
        var step = new TestStudioStepModel { Action = "tapOn", Selector = SelectedElementSelector, Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddDoubleTapAsync()
    {
        var step = new TestStudioStepModel { Action = "doubleTapOn", Selector = SelectedElementSelector, Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddLongPressAsync()
    {
        var step = new TestStudioStepModel { Action = "longPressOn", Selector = SelectedElementSelector, Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddInputAsync()
    {
        var step = new TestStudioStepModel { Action = "inputText", Selector = SelectedElementSelector, Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddAssertVisibleAsync()
    {
        var step = new TestStudioStepModel { Action = "assertVisible", Selector = SelectedElementSelector, Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddAssertNotVisibleAsync()
    {
        var step = new TestStudioStepModel { Action = "assertNotVisible", Selector = SelectedElementSelector, Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddClearTextAsync()
    {
        var step = new TestStudioStepModel { Action = "clearText", Selector = SelectedElementSelector, Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddPasteTextAsync()
    {
        var step = new TestStudioStepModel { Action = "pasteText", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddEraseTextAsync()
    {
        var step = new TestStudioStepModel { Action = "eraseText", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddSwipeAsync()
    {
        var step = new TestStudioStepModel { Action = "swipe", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public void AddDelay()
    {
        Steps.Add(new TestStudioStepModel { Action = "delay", Selector = "", Value = string.IsNullOrEmpty(InputSimText) ? DelayMs.ToString() : InputSimText });
    }

    public void AddLaunchApp()
    {
        Steps.Add(new TestStudioStepModel { Action = "launchApp", Selector = "", Value = InputSimText });
    }

    public void AddStopApp()
    {
        Steps.Add(new TestStudioStepModel { Action = "stopApp", Selector = "", Value = InputSimText });
    }

    public void AddKillApp()
    {
        Steps.Add(new TestStudioStepModel { Action = "killApp", Selector = "", Value = InputSimText });
    }

    public async Task AddClearStateAsync()
    {
        var step = new TestStudioStepModel { Action = "clearState", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddSetOrientationAsync()
    {
        var step = new TestStudioStepModel { Action = "setOrientation", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddSetLocationAsync()
    {
        var step = new TestStudioStepModel { Action = "setLocation", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddTakeScreenshotAsync()
    {
        var step = new TestStudioStepModel { Action = "takeScreenshot", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddOpenLinkAsync()
    {
        var step = new TestStudioStepModel { Action = "openLink", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddCopyTextFromAsync()
    {
        var step = new TestStudioStepModel { Action = "copyTextFrom", Selector = SelectedElementSelector, Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddAssertTrueAsync()
    {
        var step = new TestStudioStepModel { Action = "assertTrue", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddAssertFalseAsync()
    {
        var step = new TestStudioStepModel { Action = "assertFalse", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddSetAirplaneModeAsync()
    {
        var step = new TestStudioStepModel { Action = "setAirplaneMode", Selector = "", Value = string.IsNullOrEmpty(InputSimText) ? "on" : InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public void AddRepeat()
    {
        Steps.Add(new TestStudioStepModel { Action = "repeat", Selector = "", Value = InputSimText });
    }

    public void AddRetry()
    {
        Steps.Add(new TestStudioStepModel { Action = "retry", Selector = "", Value = InputSimText });
    }

    private async Task<bool> EvaluateConditionAsync(string conditionType, string conditionValue, CancellationToken token)
    {
        switch (conditionType.ToLowerInvariant())
        {
            case "visible":
                {
                    try
                    {
                        var nodeId = await CheckElementVisibleAsync(conditionValue);
                        return nodeId.HasValue;
                    }
                    catch
                    {
                        return false;
                    }
                }
            case "notvisible":
                {
                    try
                    {
                        var nodeId = await CheckElementVisibleAsync(conditionValue);
                        return !nodeId.HasValue;
                    }
                    catch
                    {
                        return true;
                    }
                }
            case "asserttrue":
                {
                    try
                    {
                        var evalRes = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject { ["expression"] = conditionValue });
                        var resultNode = evalRes["result"] as JsonObject;
                        var val = resultNode?["value"]?.GetValue<object>()?.ToString();
                        return val == "True" || val == "true" || val == "1";
                    }
                    catch
                    {
                        return false;
                    }
                }
            case "assertfalse":
                {
                    try
                    {
                        var evalRes = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject { ["expression"] = conditionValue });
                        var resultNode = evalRes["result"] as JsonObject;
                        var val = resultNode?["value"]?.GetValue<object>()?.ToString();
                        return val != "True" && val != "true" && val != "1";
                    }
                    catch
                    {
                        return true;
                    }
                }
            default:
                throw new NotSupportedException($"Condition type '{conditionType}' is not supported in repeat loops.");
        }
    }

    public void AddRunFlow()
    {
        Steps.Add(new TestStudioStepModel { Action = "runFlow", Selector = "", Value = InputSimText });
    }

    public async Task AddEvalScriptAsync()
    {
        var step = new TestStudioStepModel { Action = "evalScript", Selector = "", Value = InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddBackAsync()
    {
        var step = new TestStudioStepModel { Action = "back", Selector = "", Value = "" };
        await AddInteractiveStepAsync(step);
    }

    public async Task AddScrollAsync()
    {
        var step = new TestStudioStepModel { Action = "scroll", Selector = SelectedElementSelector, Value = string.IsNullOrEmpty(InputSimText) ? "direction: down, amount: 100" : InputSimText };
        await AddInteractiveStepAsync(step);
    }

    public void AddSelectedCommand()
    {
        var action = FlowCommandCatalog.CanonicalizeAction(SelectedCommandName.Trim().TrimEnd(':'));
        if (string.IsNullOrWhiteSpace(action))
        {
            return;
        }

        var command = FlowCommandCatalog.Find(action);
        var step = new TestStudioStepModel
        {
            Action = action,
            Selector = command?.AcceptsSelector == true ? SelectedElementSelector : "",
            Value = InputSimText
        };

        if (command?.ValueKind == FlowCommandValueKind.None)
        {
            step.Value = "";
        }

        Steps.Add(step);
        Log($"Added command: {step.ActionDisplay}");
    }

    private void DeleteStep(TestStudioStepModel? step)
    {
        if (step == null) return;
        var parentCollection = FindParentCollection(step, Steps);
        if (parentCollection != null && parentCollection.Contains(step))
        {
            parentCollection.Remove(step);
        }
    }

    private void MoveStepUp(TestStudioStepModel? step)
    {
        if (step == null) return;
        var parentCollection = FindParentCollection(step, Steps);
        if (parentCollection == null) return;
        int idx = parentCollection.IndexOf(step);
        if (idx > 0)
        {
            _isUpdatingYaml = true;
            try
            {
                parentCollection.RemoveAt(idx);
                parentCollection.Insert(idx - 1, step);
            }
            finally
            {
                _isUpdatingYaml = false;
            }
            UpdateYaml();
        }
    }

    private void MoveStepDown(TestStudioStepModel? step)
    {
        if (step == null) return;
        var parentCollection = FindParentCollection(step, Steps);
        if (parentCollection == null) return;
        int idx = parentCollection.IndexOf(step);
        if (idx >= 0 && idx < parentCollection.Count - 1)
        {
            _isUpdatingYaml = true;
            try
            {
                parentCollection.RemoveAt(idx);
                parentCollection.Insert(idx + 1, step);
            }
            finally
            {
                _isUpdatingYaml = false;
            }
            UpdateYaml();
        }
    }

    private ObservableCollection<TestStudioStepModel>? FindParentCollection(TestStudioStepModel target, ObservableCollection<TestStudioStepModel> currentList)
    {
        if (currentList.Contains(target))
        {
            return currentList;
        }

        foreach (var step in currentList)
        {
            if (step.NestedSteps != null)
            {
                var found = FindParentCollection(target, step.NestedSteps);
                if (found != null) return found;
            }
        }

        return null;
    }

    private void ClearAll()
    {
        Steps.Clear();
        Logs.Clear();
        _currentStepIndex = 0;
    }

    public void Log(string message)
    {
        var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Logs.Add(formatted);
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Logs.Add(formatted);
            });
        }
    }

    private static string GetRuntimeSelector(TestStudioStepModel step)
    {
        return FlowCommandCatalog.BuildRuntimeSelector(step.Parameters, step.Selector);
    }

    private static bool TryGetParameter(TestStudioStepModel step, string key, out object? value)
    {
        if (step.Parameters.TryGetValue(key, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static string GetParameterString(TestStudioStepModel step, string key, string fallback = "")
    {
        return TryGetParameter(step, key, out var value) ? FlowCommandCatalog.ScalarToString(value) : fallback;
    }

    private static string GetStepValue(TestStudioStepModel step, params string[] parameterKeys)
    {
        foreach (var key in parameterKeys)
        {
            var value = GetParameterString(step, key);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return step.Value ?? "";
    }

    private static int GetStepInt(TestStudioStepModel step, int fallback, params string[] parameterKeys)
    {
        var value = GetStepValue(step, parameterKeys);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static double GetStepDouble(TestStudioStepModel step, double fallback, params string[] parameterKeys)
    {
        var value = GetStepValue(step, parameterKeys);
        return double.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static bool GetStepBool(TestStudioStepModel step, bool fallback, params string[] parameterKeys)
    {
        var value = GetStepValue(step, parameterKeys);
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        if (value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("allow", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("deny", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fallback;
    }

    private static bool IsOptionalAiStep(TestStudioStepModel step)
    {
        return GetStepBool(step, true, "optional");
    }

    private async Task SetClipboardTextAsync(string text)
    {
        Avalonia.Input.Platform.IClipboard? clipboard = null;
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            clipboard = desktop.MainWindow?.Clipboard ?? desktop.Windows.FirstOrDefault(w => w.Clipboard != null)?.Clipboard;
        }
        else if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView)
        {
            clipboard = Avalonia.Controls.TopLevel.GetTopLevel(singleView.MainView)?.Clipboard;
        }

        if (clipboard != null)
        {
            await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, text);
        }
    }

    private async Task SetNetworkOfflineAsync(bool offline)
    {
        try
        {
            await _cdpService.SendCommandAsync("Network.enable", new JsonObject());
            await _cdpService.SendCommandAsync("Network.emulateNetworkConditions", new JsonObject
            {
                ["offline"] = offline,
                ["latency"] = 0,
                ["downloadThroughput"] = -1,
                ["uploadThroughput"] = -1
            });
        }
        catch (Exception ex)
        {
            Log($"Warning: Network emulation not fully supported: {ex.Message}");
        }
    }

    private static string GenerateRandomInput(string action, TestStudioStepModel step)
    {
        var length = Math.Clamp(GetStepInt(step, 8, "length"), 1, 128);
        return action switch
        {
            "inputRandomEmail" => $"user{Random.Shared.Next(1000, 9999)}@example.com",
            "inputRandomPersonName" => "Alex Morgan",
            "inputRandomNumber" => string.Concat(Enumerable.Range(0, length).Select(_ => Random.Shared.Next(0, 10).ToString())),
            "inputRandomText" => RandomAlpha(length),
            "inputRandomCityName" => "Seattle",
            "inputRandomCountryName" => "United States",
            "inputRandomColorName" => "Blue",
            _ => RandomAlpha(length)
        };
    }

    private static string RandomAlpha(int length)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz";
        return string.Concat(Enumerable.Range(0, length).Select(_ => alphabet[Random.Shared.Next(alphabet.Length)]));
    }

    private async Task<(double x, double y)?> ResolvePointParameterAsync(TestStudioStepModel step)
    {
        if (!TryGetParameter(step, "point", out var pointValue) || pointValue == null)
        {
            return null;
        }

        var pointText = FlowCommandCatalog.ScalarToString(pointValue);
        var parsed = await ParsePointAsync(pointText);
        return parsed;
    }

    private async Task<(double x, double y)?> ParsePointAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        double width = 800;
        double height = 600;
        if (parts[0].Contains('%') || parts[1].Contains('%'))
        {
            try
            {
                var metrics = await _cdpService.SendCommandAsync("Page.getLayoutMetrics");
                var viewport = metrics["cssVisualViewport"] as JsonObject;
                if (viewport != null)
                {
                    width = viewport["width"]?.GetValue<double>() ?? width;
                    height = viewport["height"]?.GetValue<double>() ?? height;
                }
            }
            catch { }
        }

        bool TryParseCoordinate(string input, double total, out double result)
        {
            input = input.Trim();
            if (input.EndsWith("%", StringComparison.Ordinal))
            {
                if (double.TryParse(input.TrimEnd('%'), out var percentage))
                {
                    result = total * percentage / 100.0;
                    return true;
                }
            }

            return double.TryParse(input, out result);
        }

        if (TryParseCoordinate(parts[0], width, out var x) &&
            TryParseCoordinate(parts[1], height, out var y))
        {
            return (x, y);
        }

        return null;
    }

    private async Task<(double x, double y, int nodeId)> ResolveCoordinatesAsync(TestStudioStepModel step, CancellationToken token)
    {
        int retryCount = 0;
        while (retryCount < 3)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                double? targetX = null;
                double? targetY = null;
                int nodeId = 0;
                var point = await ResolvePointParameterAsync(step);
                if (point.HasValue)
                {
                    return (point.Value.x, point.Value.y, 0);
                }

                var runtimeSelector = GetRuntimeSelector(step);
                if (!string.IsNullOrEmpty(runtimeSelector))
                {
                    Log($"Waiting for element '{runtimeSelector}' to be visible...");
                    nodeId = await WaitForElementVisibleAsync(runtimeSelector, token);
                    Log($"Element resolved. Fetching box model...");
                    var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = nodeId });
                    var model = boxRes["model"] as JsonObject;
                    var content = model?["content"] as JsonArray;
                    if (content != null && content.Count >= 8)
                    {
                        double x1 = content[0]!.GetValue<double>();
                        double y1 = content[1]!.GetValue<double>();
                        double x2 = content[4]!.GetValue<double>();
                        double y2 = content[5]!.GetValue<double>();
                        targetX = x1 + (x2 - x1) / 2.0;
                        targetY = y1 + (y2 - y1) / 2.0;
                    }
                    else
                    {
                        throw new Exception("Could not retrieve content box from box model.");
                    }
                }
                else if (!string.IsNullOrEmpty(step.Value))
                {
                    var coords = ParseCoordinates(step.Value);
                    if (coords.HasValue)
                    {
                        targetX = coords.Value.x;
                        targetY = coords.Value.y;
                    }
                    else
                    {
                        throw new Exception($"Invalid coordinates value: {step.Value}");
                    }
                }
                else
                {
                    throw new Exception($"{step.Action} step requires either a Selector or coordinates Value.");
                }

                return (targetX.Value, targetY.Value, nodeId);
            }
            catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) && retryCount < 2)
            {
                Log($"Resolution failed due to Node ID invalidation: {ex.Message}. Retrying...");
                retryCount++;
                await Task.Delay(100, token);
            }
        }
        throw new Exception($"Failed to resolve coordinates for step.");
    }

    private static (double x, double y)? ParseCoordinates(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var parts = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            var xPart = parts[0].Replace("x:", "").Replace("x=", "").Trim(' ', '"', '\'');
            var yPart = parts[1].Replace("y:", "").Replace("y=", "").Trim(' ', '"', '\'');
            if (double.TryParse(xPart, out double x) && double.TryParse(yPart, out double y))
            {
                return (x, y);
            }
        }
        return null;
    }

    private static Dictionary<string, string> ParseKeyValuePairs(string? value)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(value)) return dict;

        var parts = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var idx = part.IndexOfAny(new[] { ':', '=' });
            if (idx > 0)
            {
                var key = part.Substring(0, idx).Trim();
                var val = part.Substring(idx + 1).Trim();
                dict[key] = CleanValue(val);
            }
        }
        return dict;
    }

    private static string CleanValue(string val)
    {
        if (val == null) return "";
        val = val.Trim();
        if (val.StartsWith("\"") && val.EndsWith("\""))
        {
            val = val.Substring(1, val.Length - 2);
        }
        else if (val.StartsWith("'") && val.EndsWith("'"))
        {
            val = val.Substring(1, val.Length - 2);
        }
        return val;
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (e.Method == "Page.screencastFrame" && _isRecordingVideo)
        {
            try
            {
                var base64 = e.Params["data"]?.GetValue<string>() ?? "";
                var sessionId = e.Params["sessionId"]?.GetValue<int>() ?? 0;
                if (!string.IsNullOrEmpty(base64))
                {
                    byte[] bytes = Convert.FromBase64String(base64);
                    double relTimeMs = (DateTime.UtcNow - _playbackStartTime).TotalMilliseconds;

                    lock (_lastRunRawFrameBytes)
                    {
                        _lastRunRawFrameBytes.Add(bytes);
                        _lastRunFrameTimestamps.Add(relTimeMs);
                    }
                }

                // Send ACK to keep target sending frames only if preview screencast is not already doing it
                if (!_cdpService.IsPreviewScreencastActive && sessionId != 0)
                {
                    _ = _cdpService.SendCommandAsync("Page.screencastFrameAck", new JsonObject { ["sessionId"] = sessionId });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error capturing frame: {ex.Message}");
            }
        }
    }

    private async Task CaptureStepDetailsAsync(TestStudioStepModel step, int index, double durationMs, double relativeStartMs)
    {
        try
        {
            await Task.Delay(200);

            string url = "";
            try
            {
                var evalRes = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject { ["expression"] = "window.location.href" });
                url = evalRes["result"]?["value"]?.GetValue<string>() ?? "";
            }
            catch { }

            string screenshotBase64 = "";
            try
            {
                var res = await _cdpService.SendCommandAsync("Page.captureScreenshot", new JsonObject());
                screenshotBase64 = res["data"]?.GetValue<string>() ?? "";
                if (string.IsNullOrEmpty(screenshotBase64))
                {
                    throw new Exception("Screenshot data is empty.");
                }
            }
            catch (Exception ex)
            {
                Log($"Warning: Failed to capture step screenshot: {ex.Message}");
            }

            var reportItem = new StepReportItem
            {
                Index = index + 1,
                Action = step.Action,
                ActionDisplay = step.ActionDisplay,
                Selector = step.Selector ?? "",
                Value = step.Value ?? "",
                Status = step.Status.ToString(),
                DurationMs = durationMs,
                ErrorMessage = step.ErrorMessage ?? "",
                Url = url,
                ScreenshotFileName = screenshotBase64, // Store base64 temporarily
                RelativeStartMs = relativeStartMs
            };

            lock (_stepReports)
            {
                _stepReports[index] = reportItem;
            }
        }
        catch (Exception ex)
        {
            Log($"Warning: Failed to capture step details: {ex.Message}");
        }
    }

    private async Task FinalizeRecordingAndGenerateReportsAsync()
    {
        _isRecordingVideo = false;
        if (IsRecordVideoEnabled && _cdpService.IsConnected)
        {
            try
            {
                if (_cdpService.IsPreviewScreencastActive)
                {
                    Log("Restoring simulator preview screencast...");
                    // Restore PNG format for the simulator preview
                    await _cdpService.SendCommandAsync("Page.startScreencast", new JsonObject
                    {
                        ["format"] = "png",
                        ["everyNthFrame"] = 1
                    });
                }
                else
                {
                    Log("Stopping video recording...");
                    await _cdpService.SendCommandAsync("Page.stopScreencast");
                }
            }
            catch (Exception ex)
            {
                Log($"Warning: Failed to manage screencast: {ex.Message}");
            }
        }

        // Extract step reports from dictionary
        List<StepReportItem> tempReports;
        lock (_stepReports)
        {
            tempReports = _stepReports.OrderBy(k => k.Key).Select(k => k.Value).ToList();
            _stepReports.Clear();
        }

        // Cache steps in memory for replay window
        lock (_lastRunSteps)
        {
            _lastRunSteps.Clear();
            _lastRunSteps.AddRange(tempReports);
        }

        // If video frames were recorded, mark as available for native replay regardless of HTML/PDF report option
        if (IsRecordVideoEnabled && _lastRunRawFrameBytes.Count > 0)
        {
            HasLastRunRecording = true;
        }

        if (!IsGenerateReportEnabled) return;

        try
        {
            Log("Generating test run reports...");
            var runFolder = $"Run_{DateTime.Now:yyyyMMdd_HHmmss_fff}";
            var outputFolder = Path.Combine(OutputDirectory, runFolder);
            Directory.CreateDirectory(outputFolder);
            var imagesFolder = Path.Combine(outputFolder, "images");
            Directory.CreateDirectory(imagesFolder);

            // 1. Save step screenshots and build report items list
            var stepReportItems = new List<StepReportItem>();
            lock (_lastRunSteps)
            {
                foreach (var item in _lastRunSteps)
                {
                    if (!string.IsNullOrEmpty(item.ScreenshotFileName) && !item.ScreenshotFileName.StartsWith("images/"))
                    {
                        try
                        {
                            var filename = $"step_{item.Index}_screenshot.png";
                            var filepath = Path.Combine(imagesFolder, filename);
                            byte[] bytes = Convert.FromBase64String(item.ScreenshotFileName);
                            File.WriteAllBytes(filepath, bytes);
                            item.ScreenshotFileName = $"images/{filename}"; // relative path
                        }
                        catch
                        {
                            // Keep base64 or empty
                        }
                    }
                    stepReportItems.Add(item);
                }
            }


            // 2. Save video frames
            var videoFrameItems = new List<VideoFrameItem>();
            lock (_lastRunRawFrameBytes)
            {
                for (int i = 0; i < _lastRunRawFrameBytes.Count; i++)
                {
                    try
                    {
                        var filename = $"frame_{i}.jpg";
                        var filepath = Path.Combine(imagesFolder, filename);
                        File.WriteAllBytes(filepath, _lastRunRawFrameBytes[i]);
                        videoFrameItems.Add(new VideoFrameItem
                        {
                            FileName = $"images/{filename}", // relative path
                            TimestampMs = _lastRunFrameTimestamps[i]
                        });
                    }
                    catch { }
                }
            }

            // 3. Generate HTML report
            var endTime = DateTime.UtcNow;
            var testName = string.IsNullOrEmpty(_description) ? "Test Studio Run" : _description;
            
            TestStudioReportGenerator.GenerateHtmlReport(
                outputFolder,
                testName,
                _description,
                _appId,
                stepReportItems,
                videoFrameItems,
                _playbackStartTime,
                endTime
            );

            // 4. Generate PDF report
            var pdfPath = Path.Combine(outputFolder, "report.pdf");
            TestStudioReportGenerator.GeneratePdfReport(pdfPath, testName, stepReportItems);

            LastReportPath = Path.GetFullPath(Path.Combine(outputFolder, "index.html"));
            LastPdfReportPath = Path.GetFullPath(pdfPath);
            HasLastRunRecording = true;

            Log($"HTML Report generated: {LastReportPath}");
            Log($"PDF Report generated: {LastPdfReportPath}");
        }
        catch (Exception ex)
        {
            Log($"Failed to generate reports: {ex.Message}");
        }
    }

    public void OpenLastReport()
    {
        if (string.IsNullOrEmpty(LastReportPath) || !File.Exists(LastReportPath)) return;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = LastReportPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log($"Failed to open report: {ex.Message}");
        }
    }

    public void OpenLastPdfReport()
    {
        if (string.IsNullOrEmpty(LastPdfReportPath) || !File.Exists(LastPdfReportPath)) return;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = LastPdfReportPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log($"Failed to open PDF report: {ex.Message}");
        }
    }

    public void ReplayLastVideo(object? ownerWindow)
    {
        var playbackFrames = new List<CdpInspectorApp.Views.PlaybackFrame>();
        lock (_lastRunRawFrameBytes)
        {
            for (int i = 0; i < _lastRunRawFrameBytes.Count; i++)
            {
                playbackFrames.Add(new CdpInspectorApp.Views.PlaybackFrame
                {
                    Data = _lastRunRawFrameBytes[i],
                    TimestampMs = _lastRunFrameTimestamps[i]
                });
            }
        }

        if (playbackFrames.Count == 0)
        {
            Log("No video frames available to replay.");
            return;
        }

        List<StepReportItem> playbackSteps;
        lock (_lastRunSteps)
        {
            playbackSteps = _lastRunSteps.ToList();
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var win = new CdpInspectorApp.Views.VideoPlaybackWindow();
                win.SetFramesAndSteps(playbackFrames, playbackSteps, LastPdfReportPath);
                
                if (ownerWindow is Avalonia.Controls.Window parentWin)
                {
                    win.Show(parentWin);
                }
                else
                {
                    win.Show();
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to open video playback window: {ex.Message}");
            }
        });
    }

    private static readonly System.Text.RegularExpressions.Regex PlaceholderRegex = 
        new(@"\$\{([^}]+)\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    private string? ReplacePlaceholders(string? input, Dictionary<string, string> env)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return InterpolateVariablesInternal(input, env, false);
    }

    private object? InterpolateParameterValue(object? val, Dictionary<string, string> env)
    {
        if (val == null) return null;

        if (val is string str)
        {
            return ReplacePlaceholders(str, env);
        }
        if (val is Dictionary<string, object?> dict)
        {
            var clonedDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dict)
            {
                clonedDict[kv.Key] = InterpolateParameterValue(kv.Value, env);
            }
            return clonedDict;
        }
        if (val is System.Collections.IDictionary idict)
        {
            var clonedDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in idict)
            {
                var key = entry.Key?.ToString() ?? "";
                clonedDict[key] = InterpolateParameterValue(entry.Value, env);
            }
            return clonedDict;
        }
        if (val is System.Collections.IList list)
        {
            var clonedList = new List<object?>();
            foreach (var item in list)
            {
                clonedList.Add(InterpolateParameterValue(item, env));
            }
            return clonedList;
        }

        return val;
    }

    private TestStudioStepModel InterpolateStep(TestStudioStepModel step, Dictionary<string, string> env)
    {
        var cloned = new TestStudioStepModel
        {
            Original = step,
            Action = ReplacePlaceholders(step.Action, env) ?? "",
            Selector = ReplacePlaceholders(step.Selector, env),
            Value = ReplacePlaceholders(step.Value, env),
            WhileConditionType = ReplacePlaceholders(step.WhileConditionType, env),
            WhileConditionValue = ReplacePlaceholders(step.WhileConditionValue, env),
            StartLine = step.StartLine,
            EndLine = step.EndLine
        };

        if (step.Parameters != null)
        {
            var clonedParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in step.Parameters)
            {
                clonedParams[kv.Key] = InterpolateParameterValue(kv.Value, env);
            }
            cloned.Parameters = clonedParams;
        }

        if (step.NestedSteps != null)
        {
            var clonedNested = new ObservableCollection<TestStudioStepModel>();
            foreach (var nested in step.NestedSteps)
            {
                var clonedChild = InterpolateStep(nested, env);
                clonedNested.Add(clonedChild);
            }
            cloned.NestedSteps = clonedNested;
        }

        return cloned;
    }

    public Func<Task<string?>>? FolderPickerHandler { get; set; }

    private async Task BrowseWorkspaceRootAsync()
    {
        if (FolderPickerHandler != null)
        {
            var path = await FolderPickerHandler();
            if (!string.IsNullOrEmpty(path))
            {
                WorkspaceRootPath = path;
            }
        }
    }

    public void LoadWorkspaceTree()
    {
        WorkspaceFiles.Clear();
        if (IsSidebarCollapsed)
        {
            return;
        }
        if (string.IsNullOrEmpty(WorkspaceRootPath))
        {
            return;
        }
        if (!Directory.Exists(WorkspaceRootPath))
        {
            Log($"Error: Workspace path '{WorkspaceRootPath}' does not exist.");
            return;
        }

        try
        {
            var dir = new DirectoryInfo(WorkspaceRootPath);
            var rootItems = BuildTree(dir);
            foreach (var item in rootItems)
            {
                WorkspaceFiles.Add(item);
            }
        }
        catch (Exception ex)
        {
            Log($"Error loading workspace tree: {ex.Message}");
        }
    }

    private List<WorkspaceItemModel> BuildTree(DirectoryInfo dir)
    {
        var items = new List<WorkspaceItemModel>();
        try
        {
            var directories = dir.GetDirectories().OrderBy(d => d.Name);
            foreach (var d in directories)
            {
                var folderModel = new WorkspaceItemModel
                {
                    Name = d.Name,
                    Path = d.FullName,
                    IsFolder = true
                };
                var children = BuildTree(d);
                foreach (var child in children)
                {
                    folderModel.Children.Add(child);
                }
                items.Add(folderModel);
            }

            var files = dir.GetFiles("*.yaml").OrderBy(f => f.Name);
            foreach (var f in files)
            {
                items.Add(new WorkspaceItemModel
                {
                    Name = f.Name,
                    Path = f.FullName,
                    IsFolder = false
                });
            }
        }
        catch (Exception ex)
        {
            Log($"Error building tree for '{dir.FullName}': {ex.Message}");
        }
        return items;
    }

    private string? GetTargetParentDirectory()
    {
        if (string.IsNullOrEmpty(WorkspaceRootPath)) return null;
        if (SelectedWorkspaceItem == null) return WorkspaceRootPath;
        return SelectedWorkspaceItem.IsFolder ? SelectedWorkspaceItem.Path : Path.GetDirectoryName(SelectedWorkspaceItem.Path);
    }

    public void CreateFile(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            NamePromptTitle = "Create File";
            NamePromptValue = "";
            _namePromptCallback = (val) => CreateFile(val);
            IsNamePromptVisible = true;
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.IndexOfAny(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
        {
            Log("Error: Invalid characters in file name.");
            return;
        }

        if (!name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            name += ".yaml";
        }

        var parentDir = GetTargetParentDirectory();
        if (string.IsNullOrEmpty(parentDir))
        {
            Log("Error: No workspace root path set.");
            return;
        }

        var fullPath = Path.Combine(parentDir, name);
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            Log($"Error: File or folder '{name}' already exists.");
            return;
        }

        try
        {
            File.WriteAllText(fullPath, "appId: \"\"\ndescription: \"\"\nsteps: []\n");
            LoadWorkspaceTree();
        }
        catch (Exception ex)
        {
            Log($"Error creating file: {ex.Message}");
        }
    }

    public void CreateFolder(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            NamePromptTitle = "Create Folder";
            NamePromptValue = "";
            _namePromptCallback = (val) => CreateFolder(val);
            IsNamePromptVisible = true;
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || name.Contains("/") || name.Contains("\\"))
        {
            Log("Error: Invalid characters in folder name.");
            return;
        }

        var parentDir = GetTargetParentDirectory();
        if (string.IsNullOrEmpty(parentDir))
        {
            Log("Error: No workspace root path set.");
            return;
        }

        var fullPath = Path.Combine(parentDir, name);
        if (Directory.Exists(fullPath) || File.Exists(fullPath))
        {
            Log($"Error: Folder or file '{name}' already exists.");
            return;
        }

        try
        {
            Directory.CreateDirectory(fullPath);
            LoadWorkspaceTree();
        }
        catch (Exception ex)
        {
            Log($"Error creating folder: {ex.Message}");
        }
    }

    public void RenameItem(string? newNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(newNameOrPath))
        {
            NamePromptTitle = "Rename";
            NamePromptValue = SelectedWorkspaceItem?.Name ?? (string.IsNullOrEmpty(CurrentFlowFilePath) ? "" : Path.GetFileName(CurrentFlowFilePath));
            _namePromptCallback = (val) => RenameItem(val);
            IsNamePromptVisible = true;
            return;
        }

        string? oldPath = SelectedWorkspaceItem?.Path ?? CurrentFlowFilePath;
        if (string.IsNullOrEmpty(oldPath))
        {
            Log("Error: No item selected to rename.");
            return;
        }

        string newPath;
        if (Path.IsPathRooted(newNameOrPath) || newNameOrPath.Contains("/") || newNameOrPath.Contains("\\"))
        {
            newPath = newNameOrPath;
        }
        else
        {
            var dir = Path.GetDirectoryName(oldPath);
            if (string.IsNullOrEmpty(dir))
            {
                newPath = newNameOrPath;
            }
            else
            {
                newPath = Path.Combine(dir, newNameOrPath);
            }
        }

        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (File.Exists(newPath) || Directory.Exists(newPath))
        {
            Log($"Error: Destination '{newPath}' already exists.");
            return;
        }

        try
        {
            if (File.Exists(oldPath))
            {
                File.Move(oldPath, newPath);
                if (string.Equals(CurrentFlowFilePath, oldPath, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentFlowFilePath = newPath;
                }
            }
            else if (Directory.Exists(oldPath))
            {
                bool rebaseFlowFile = false;
                string relativePath = "";
                if (!string.IsNullOrEmpty(CurrentFlowFilePath))
                {
                    var normalizedOld = oldPath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? oldPath : oldPath + Path.DirectorySeparatorChar;
                    if (CurrentFlowFilePath.StartsWith(normalizedOld, StringComparison.OrdinalIgnoreCase))
                    {
                        rebaseFlowFile = true;
                        relativePath = Path.GetRelativePath(oldPath, CurrentFlowFilePath);
                    }
                }

                Directory.Move(oldPath, newPath);

                if (rebaseFlowFile)
                {
                    CurrentFlowFilePath = Path.Combine(newPath, relativePath);
                }
            }
            else
            {
                if (string.Equals(CurrentFlowFilePath, oldPath, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentFlowFilePath = newPath;
                }
            }
            LoadWorkspaceTree();
        }
        catch (UnauthorizedAccessException)
        {
            Log("Error: Permission denied.");
        }
        catch (Exception ex)
        {
            Log($"Error renaming item: {ex.Message}");
        }
    }

    public void DeleteItem(string? path)
    {
        string? targetPath = path;
        if (string.IsNullOrEmpty(targetPath))
        {
            targetPath = SelectedWorkspaceItem?.Path ?? CurrentFlowFilePath;
        }

        if (string.IsNullOrEmpty(targetPath))
        {
            Log("Error: No item selected to delete.");
            return;
        }

        try
        {
            bool isCurrentFlowAffected = false;
            if (!string.IsNullOrEmpty(CurrentFlowFilePath))
            {
                if (string.Equals(CurrentFlowFilePath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    isCurrentFlowAffected = true;
                }
                else if (Directory.Exists(targetPath))
                {
                    var normalizedTarget = targetPath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? targetPath : targetPath + Path.DirectorySeparatorChar;
                    if (CurrentFlowFilePath.StartsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        isCurrentFlowAffected = true;
                    }
                }
            }

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            else if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
            }

            if (isCurrentFlowAffected)
            {
                CurrentFlowFilePath = null;
                Steps.Clear();
            }

            LoadWorkspaceTree();
        }
        catch (UnauthorizedAccessException)
        {
            Log("Error: Permission denied.");
        }
        catch (Exception ex)
        {
            Log($"Error deleting item: {ex.Message}");
        }
    }



    public void SaveYaml()
    {
        if (string.IsNullOrEmpty(CurrentFlowFilePath))
        {
            Log("Error: No current flow file path to save to.");
            return;
        }

        try
        {
            File.WriteAllText(CurrentFlowFilePath, YamlCode);
            Log($"Successfully saved YAML to {CurrentFlowFilePath}");
        }
        catch (Exception ex)
        {
            Log($"Error saving YAML: {ex.Message}");
        }
    }

    public void LoadFlowFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Log("Error: Path cannot be empty.");
            return;
        }

        try
        {
            if (!File.Exists(path))
            {
                Log($"Error: Flow file '{path}' does not exist.");
                throw new FileNotFoundException($"Flow file not found: {path}");
            }

            Steps.Clear();
            var content = File.ReadAllText(path);
            CurrentFlowFilePath = path;
            YamlCode = content;

            var parsed = TestStudioYamlParser.Parse(content, out var appId, out var desc, out var tags, out var env);
            _appId = appId;
            _description = desc;
            FlowTags = tags;
            FlowEnv = env;

            ApplyYaml();
            Log($"Successfully loaded flow file: {path}");
        }
        catch (Exception ex)
        {
            Log($"Error loading flow file '{path}': {ex.Message}");
            throw;
        }
    }

    public async Task RunSuite(string? folderPath)
    {
        if (IsSuiteExecuting || IsExecuting)
        {
            return;
        }

        string? targetPath = folderPath;
        if (string.IsNullOrEmpty(targetPath))
        {
            targetPath = WorkspaceRootPath;
        }

        if (string.IsNullOrEmpty(targetPath))
        {
            Log("Error: Folder path cannot be empty.");
            return;
        }

        if (!Directory.Exists(targetPath))
        {
            Log($"Error: Folder '{targetPath}' does not exist.");
            return;
        }

        IsSuiteExecuting = true;
        SuitePassCount = 0;
        SuiteFailCount = 0;
        Log($"Starting suite execution for folder: {targetPath}");

        try
        {
            var yamlFiles = Directory.GetFiles(targetPath, "*.yaml", SearchOption.AllDirectories)
                                     .OrderBy(f => f)
                                     .ToList();

            if (yamlFiles.Count == 0)
            {
                Log("No .yaml flow files found in the folder.");
                return;
            }

            var activeEnv = SelectedEnvironment;
            var includedList = activeEnv?.IncludedTags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList() ?? new List<string>();
            var excludedList = activeEnv?.ExcludedTags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList() ?? new List<string>();

            foreach (var file in yamlFiles)
            {
                if (!IsSuiteExecuting)
                {
                    Log("Suite execution stopped.");
                    break;
                }

                try
                {
                    var flowContent = File.ReadAllText(file);
                    TestStudioYamlParser.Parse(flowContent, out _, out _, out var flowTags, out _);

                    bool shouldSkip = flowTags.Any(t => excludedList.Contains(t, StringComparer.OrdinalIgnoreCase));
                    if (shouldSkip)
                    {
                        Log($"Skipping flow '{Path.GetFileName(file)}' (matches excluded tag).");
                        continue;
                    }

                    if (includedList.Count > 0)
                    {
                        bool matchesInclude = flowTags.Any(t => includedList.Contains(t, StringComparer.OrdinalIgnoreCase));
                        if (!matchesInclude)
                        {
                            Log($"Skipping flow '{Path.GetFileName(file)}' (does not match included tags).");
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Warning: Failed to parse tags for flow '{Path.GetFileName(file)}': {ex.Message}. Running anyway.");
                }

                Log($"Executing flow: {Path.GetFileName(file)}");
                try
                {
                    LoadFlowFile(file);

                    await PlayAsync();

                    bool allPassed = Steps.Count > 0 && Steps.All(s => s.Status == StepStatus.Passed);
                    if (allPassed)
                    {
                        SuitePassCount++;
                        Log($"Flow passed: {Path.GetFileName(file)}");
                    }
                    else
                    {
                        SuiteFailCount++;
                        Log($"Flow failed: {Path.GetFileName(file)}");
                    }
                }
                catch (Exception ex)
                {
                    SuiteFailCount++;
                    Log($"Flow failed with error: {ex.Message}");
                }
            }

            Log($"Suite execution finished. Passed: {SuitePassCount}, Failed: {SuiteFailCount}");
        }
        catch (Exception ex)
        {
            Log($"Error executing suite: {ex.Message}");
        }
        finally
        {
            IsSuiteExecuting = false;
            RaiseCommandCanExecuteChanged();
        }
    }

    public string ResolveFlowPath(string flowPath, string? currentFlowPath)
    {
        if (string.IsNullOrEmpty(flowPath))
        {
            throw new ArgumentException("Flow path cannot be empty.", nameof(flowPath));
        }

        // Normalize separators (e.g. cross-platform)
        string normalizedFlowPath = flowPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        string? resolvedPath = null;

        // 1. Try relative to currentFlowPath
        if (!string.IsNullOrEmpty(currentFlowPath))
        {
            var currentFlowDir = Path.GetDirectoryName(currentFlowPath);
            if (!string.IsNullOrEmpty(currentFlowDir))
            {
                var relativePath = Path.Combine(currentFlowDir, normalizedFlowPath);
                if (File.Exists(relativePath))
                {
                    resolvedPath = Path.GetFullPath(relativePath);
                }
            }
        }

        // 2. Try relative to WorkspaceRootPath
        if (resolvedPath == null && !string.IsNullOrEmpty(WorkspaceRootPath))
        {
            var workspacePath = Path.Combine(WorkspaceRootPath, normalizedFlowPath);
            if (File.Exists(workspacePath))
            {
                resolvedPath = Path.GetFullPath(workspacePath);
            }
        }

        // 3. Try directly
        if (resolvedPath == null && File.Exists(normalizedFlowPath))
        {
            resolvedPath = Path.GetFullPath(normalizedFlowPath);
        }

        // If not found, throw FileNotFoundException
        if (resolvedPath == null)
        {
            throw new FileNotFoundException($"Flow file not found: {flowPath}");
        }

        // Empty check for InvalidDataException
        if (new FileInfo(resolvedPath).Length == 0)
        {
            throw new InvalidDataException($"Flow file is empty: {resolvedPath}");
        }

        return resolvedPath;
    }

    public string InterpolateVariables(string input, Dictionary<string, string>? localEnv = null)
    {
        return InterpolateVariablesInternal(input, localEnv, true);
    }

    private string InterpolateVariablesInternal(string input, Dictionary<string, string>? localEnv, bool throwOnNotFound)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var env = localEnv ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return PlaceholderRegex.Replace(input, match =>
        {
            var expression = match.Groups[1].Value.Trim();

            // First: quick dictionary check for simple key lookup (e.g. ${email})
            if (env.TryGetValue(expression, out var val))
            {
                return val ?? "";
            }
            var key = env.Keys.FirstOrDefault(k => string.Equals(k, expression, StringComparison.OrdinalIgnoreCase));
            if (key != null)
            {
                return env[key] ?? "";
            }

            var systemVal = Environment.GetEnvironmentVariable(expression);
            if (systemVal != null)
            {
                return systemVal;
            }

            // Second: Evaluate as JS using Jint sandbox
            try
            {
                var engine = new Jint.Engine();
                // Inject environment variables
                foreach (var kv in env)
                {
                    engine.SetValue(kv.Key, kv.Value);
                }
                // Inject system environment variables
                var systemVars = Environment.GetEnvironmentVariables();
                foreach (System.Collections.DictionaryEntry entry in systemVars)
                {
                    var sKey = entry.Key.ToString();
                    if (sKey != null && !env.ContainsKey(sKey))
                    {
                        engine.SetValue(sKey, entry.Value?.ToString() ?? "");
                    }
                }

                var evaluated = engine.Evaluate(expression).ToString();
                return evaluated;
            }
            catch (Exception ex)
            {
                if (throwOnNotFound)
                {
                    throw new KeyNotFoundException($"Expression '{expression}' could not be resolved: {ex.Message}");
                }
                return match.Value;
            }
        });
    }

    private void LoadEnvironments()
    {
        Environments.Clear();
        SelectedEnvironment = null;

        if (string.IsNullOrEmpty(WorkspaceRootPath) || !Directory.Exists(WorkspaceRootPath))
        {
            // Default "None" environment
            var defaultEnv = new TestEnvironmentModel { Name = "None" };
            Environments.Add(defaultEnv);
            SelectedEnvironment = defaultEnv;
            return;
        }

        var envFilePath = Path.Combine(WorkspaceRootPath, "environments.json");
        if (File.Exists(envFilePath))
        {
            try
            {
                var json = File.ReadAllText(envFilePath);
                var envs = System.Text.Json.JsonSerializer.Deserialize<List<TestEnvironmentModel>>(json);
                if (envs != null)
                {
                    foreach (var env in envs)
                    {
                        Environments.Add(env);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error loading environments.json: {ex.Message}");
            }
        }

        if (Environments.Count == 0)
        {
            var defaultEnv = new TestEnvironmentModel { Name = "None" };
            Environments.Add(defaultEnv);
        }

        SelectedEnvironment = Environments[0];
    }

    private void SaveEnvironments()
    {
        if (string.IsNullOrEmpty(WorkspaceRootPath) || !Directory.Exists(WorkspaceRootPath))
        {
            return;
        }

        try
        {
            var envFilePath = Path.Combine(WorkspaceRootPath, "environments.json");
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(Environments.ToList(), options);
            File.WriteAllText(envFilePath, json);
        }
        catch (Exception ex)
        {
            Log($"Error saving environments.json: {ex.Message}");
        }
    }

    private void ManageEnvironments()
    {
        IsManageEnvironmentsVisible = true;
    }

    private void CreateEnvironment()
    {
        NamePromptTitle = "Create Environment";
        NamePromptValue = "";
        _namePromptCallback = name =>
        {
            if (!string.IsNullOrEmpty(name))
            {
                if (Environments.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    Log($"Environment '{name}' already exists.");
                    return;
                }
                var env = new TestEnvironmentModel { Name = name };
                Environments.Add(env);
                SelectedEnvironment = env;
            }
        };
        IsNamePromptVisible = true;
    }

    private void DeleteEnvironment()
    {
        if (SelectedEnvironment == null || SelectedEnvironment.Name == "None")
        {
            Log("Cannot delete default 'None' environment.");
            return;
        }

        var toDelete = SelectedEnvironment;
        SelectedEnvironment = Environments.FirstOrDefault(e => e != toDelete);
        Environments.Remove(toDelete);

        if (Environments.Count == 0)
        {
            var defaultEnv = new TestEnvironmentModel { Name = "None" };
            Environments.Add(defaultEnv);
            SelectedEnvironment = defaultEnv;
        }
    }

    private void AddVariable()
    {
        if (SelectedEnvironment == null) return;
        SelectedEnvironment.Variables.Add(new EnvironmentVariableModel { Key = "KEY", Value = "VALUE" });
    }

    private void DeleteVariable(EnvironmentVariableModel? variable)
    {
        if (SelectedEnvironment == null || variable == null) return;
        SelectedEnvironment.Variables.Remove(variable);
    }

    private void SaveEnvironmentsAction()
    {
        SaveEnvironments();
        IsManageEnvironmentsVisible = false;
    }

    private void CancelEnvironmentsAction()
    {
        LoadEnvironments();
        IsManageEnvironmentsVisible = false;
    }
}
