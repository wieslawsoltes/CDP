using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CDP.Profiling.Analysis;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CdpInspectorApp.Controls;
using CdpInspectorApp.Services;
using Avalonia.Layout;
using CDP.Editor.Splits.Models;

namespace CdpInspectorApp.ViewModels;

public class ProfileMethodStats : ViewModelBase
{
    public string MethodName { get; set; } = "";
    public string ModuleName { get; set; } = "";
    public double SelfTimeMs { get; set; }
    public double SelfTimePct { get; set; }
    public double TotalTimeMs { get; set; }
    public double TotalTimePct { get; set; }
    public int HitCount { get; set; }
}

public class CallTreeNodeModel : ViewModelBase
{
    public string Name { get; set; } = "";
    public double SelfTimeMs { get; set; }
    public double SelfTimePct { get; set; }
    public double TotalTimeMs { get; set; }
    public double TotalTimePct { get; set; }
    public int HitCount { get; set; }
    public ObservableCollection<CallTreeNodeModel> Children { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => RaiseAndSetIfChanged(ref _isSelected, value);
    }
}

public class ProfileMemoryStats : ViewModelBase
{
    private string _typeName = "";
    private long _allocatedBytes;
    private double _sizePct;
    private int _allocationCount;
    private double _countPct;

    public string TypeName
    {
        get => _typeName;
        set => RaiseAndSetIfChanged(ref _typeName, value);
    }

    public long AllocatedBytes
    {
        get => _allocatedBytes;
        set => RaiseAndSetIfChanged(ref _allocatedBytes, value);
    }

    public double SizePct
    {
        get => _sizePct;
        set => RaiseAndSetIfChanged(ref _sizePct, value);
    }

    public int AllocationCount
    {
        get => _allocationCount;
        set => RaiseAndSetIfChanged(ref _allocationCount, value);
    }

    public double CountPct
    {
        get => _countPct;
        set => RaiseAndSetIfChanged(ref _countPct, value);
    }
}

public class ThreadGroup : ViewModelBase
{
    private string _name = "";
    private int _id;
    private bool _isVisible = true;
    private ProfilerViewModel? _parent;

    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(ref _name, value);
    }

    public int Id
    {
        get => _id;
        set => RaiseAndSetIfChanged(ref _id, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public ProfilerViewModel? Parent
    {
        get => _parent;
        set => RaiseAndSetIfChanged(ref _parent, value);
    }

    public ObservableCollection<FlameBlock> Blocks { get; } = new();
}

public class ProfileCallItem : ViewModelBase
{
    private string _methodName = "";
    private string _moduleName = "";
    private double _timeMs;
    private double _percentage;
    private int _hitCount;

    public string MethodName
    {
        get => _methodName;
        set => RaiseAndSetIfChanged(ref _methodName, value);
    }

    public string ModuleName
    {
        get => _moduleName;
        set => RaiseAndSetIfChanged(ref _moduleName, value);
    }

    public double TimeMs
    {
        get => _timeMs;
        set => RaiseAndSetIfChanged(ref _timeMs, value);
    }

    public double Percentage
    {
        get => _percentage;
        set => RaiseAndSetIfChanged(ref _percentage, value);
    }

    public int HitCount
    {
        get => _hitCount;
        set => RaiseAndSetIfChanged(ref _hitCount, value);
    }
}

public class ProfileSessionModel : ViewModelBase
{
    private string _name = "";
    private DateTime _timestamp;
    private double _totalDurationMs;
    private double _totalAllocatedBytes;
    private int _totalSamplesCount;
    private int _totalAllocationsCount;

    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(ref _name, value);
    }

    public DateTime Timestamp
    {
        get => _timestamp;
        set => RaiseAndSetIfChanged(ref _timestamp, value);
    }

    public double TotalDurationMs
    {
        get => _totalDurationMs;
        set => RaiseAndSetIfChanged(ref _totalDurationMs, value);
    }

    public double TotalAllocatedBytes
    {
        get => _totalAllocatedBytes;
        set => RaiseAndSetIfChanged(ref _totalAllocatedBytes, value);
    }

    public int TotalSamplesCount
    {
        get => _totalSamplesCount;
        set => RaiseAndSetIfChanged(ref _totalSamplesCount, value);
    }

    public int TotalAllocationsCount
    {
        get => _totalAllocationsCount;
        set => RaiseAndSetIfChanged(ref _totalAllocationsCount, value);
    }

    public ObservableCollection<CallTreeNodeModel> CallTreeRoots { get; } = new();
    public ObservableCollection<ThreadGroup> ThreadGroups { get; } = new();
    public ObservableCollection<FlameBlock> Blocks { get; } = new();
    public ObservableCollection<FlameBlock> MemoryBlocks { get; } = new();
    public ObservableCollection<ProfileMethodStats> MethodStats { get; } = new();
    public ObservableCollection<ProfileMemoryStats> MemoryStats { get; } = new();
    public string RawJson { get; set; } = "";

    // Keep raw/parsed V8 node info for caller/callee traversal
    public List<int>? CpuSamples { get; set; }
    public List<double>? CpuTimeDeltas { get; set; }
    public Dictionary<int, Chrome.DevTools.Protocol.Domains.V8ProfileNode>? CpuNodeMap { get; set; }
    public Dictionary<int, int>? CpuParentMap { get; set; }
}

public class ProfilerViewModel : ViewModelBase, IStateProvider
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

    private readonly ICdpService _cdpService;
    private bool _isProfilingActive;
    private string _statusText = "Idle - Connect target to record or load a profile";
    private double _totalDurationMs;
    private double _totalAllocatedBytes;
    private int _totalSamplesCount;
    private int _totalAllocationsCount;
    private double _zoomScale = 1.0;
    private double _offsetX = 0.0;
    private string? _searchText;
    private FlameBlock? _hoveredBlock;
    private FlameBlock? _selectedBlock;
    private FlameBlock? _hoveredMemoryBlock;
    private FlameBlock? _selectedMemoryBlock;
    private int _sessionCounter = 1;
    
    private readonly ObservableCollection<ProfileSessionModel> _sessions = new();
    private ProfileSessionModel? _selectedSession;
    
    private readonly ObservableCollection<FlameBlock> _blocks = new();
    private readonly ObservableCollection<FlameBlock> _memoryBlocks = new();
    private readonly ObservableCollection<ProfileMethodStats> _methodStats = new();
    private readonly ObservableCollection<ProfileMemoryStats> _memoryStats = new();
    
    private readonly ObservableCollection<CallTreeNodeModel> _callTreeRoots = new();
    private readonly ObservableCollection<ThreadGroup> _threadGroups = new();

    private readonly ObservableCollection<ProfileCallItem> _callerMethods = new();
    private readonly ObservableCollection<ProfileCallItem> _calleeMethods = new();
    private string _selectedMethodHeader = "No method selected";
    private ProfileMethodStats? _selectedMethod;
    private bool _isUpdatingSelection;

    public bool IsProfilingActive
    {
        get => _isProfilingActive;
        private set => RaiseAndSetIfChanged(ref _isProfilingActive, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => RaiseAndSetIfChanged(ref _statusText, value);
    }

    public double TotalDurationMs
    {
        get => _totalDurationMs;
        private set => RaiseAndSetIfChanged(ref _totalDurationMs, value);
    }

    public double TotalAllocatedBytes
    {
        get => _totalAllocatedBytes;
        private set => RaiseAndSetIfChanged(ref _totalAllocatedBytes, value);
    }

    public int TotalSamplesCount
    {
        get => _totalSamplesCount;
        private set => RaiseAndSetIfChanged(ref _totalSamplesCount, value);
    }

    public int TotalAllocationsCount
    {
        get => _totalAllocationsCount;
        private set => RaiseAndSetIfChanged(ref _totalAllocationsCount, value);
    }

    public double ZoomScale
    {
        get => _zoomScale;
        set => RaiseAndSetIfChanged(ref _zoomScale, value);
    }

    public double OffsetX
    {
        get => _offsetX;
        set => RaiseAndSetIfChanged(ref _offsetX, value);
    }

    public string? SearchText
    {
        get => _searchText;
        set
        {
            if (RaiseAndSetIfChanged(ref _searchText, value))
            {
                UpdateSearchMatches();
            }
        }
    }

    private string _currentMatchIndexText = "";
    private bool _hasMatches;
    private readonly List<FlameBlock> _matchingSearchBlocks = new();
    private int _currentMatchIndex = -1;

    public string CurrentMatchIndexText
    {
        get => _currentMatchIndexText;
        private set => RaiseAndSetIfChanged(ref _currentMatchIndexText, value);
    }

    public bool HasMatches
    {
        get => _hasMatches;
        private set => RaiseAndSetIfChanged(ref _hasMatches, value);
    }

    public FlameBlock? HoveredBlock
    {
        get => _hoveredBlock;
        set
        {
            if (RaiseAndSetIfChanged(ref _hoveredBlock, value))
            {
                OnPropertyChanged(nameof(ActiveDetailBlock));
            }
        }
    }

    public FlameBlock? SelectedBlock
    {
        get => _selectedBlock;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedBlock, value))
            {
                OnPropertyChanged(nameof(ActiveDetailBlock));
                if (!_isUpdatingSelection)
                {
                    _isUpdatingSelection = true;
                    try
                    {
                        if (value != null)
                        {
                            SelectedMethod = null;
                            UpdateCallerCallee(value.Name, value.Url);
                        }
                        else
                        {
                            if (SelectedMethod == null)
                            {
                                UpdateCallerCallee(null, null);
                            }
                        }
                    }
                    finally
                    {
                        _isUpdatingSelection = false;
                    }
                }
            }
        }
    }

    public ProfileMethodStats? SelectedMethod
    {
        get => _selectedMethod;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedMethod, value))
            {
                ((RelayCommand)FindInFlameChartCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ShowMethodInCallTreeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ShowMethodInCallerCalleeCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SearchMethodCommand).RaiseCanExecuteChanged();

                if (!_isUpdatingSelection)
                {
                    _isUpdatingSelection = true;
                    try
                    {
                        if (value != null)
                        {
                            SelectedBlock = null;
                            UpdateCallerCallee(value.MethodName, value.ModuleName);
                        }
                        else
                        {
                            if (SelectedBlock == null)
                            {
                                UpdateCallerCallee(null, null);
                            }
                        }
                    }
                    finally
                    {
                        _isUpdatingSelection = false;
                    }
                }
            }
        }
    }

    public FlameBlock? ActiveDetailBlock => SelectedBlock ?? HoveredBlock;

    public FlameBlock? HoveredMemoryBlock
    {
        get => _hoveredMemoryBlock;
        set
        {
            if (RaiseAndSetIfChanged(ref _hoveredMemoryBlock, value))
            {
                OnPropertyChanged(nameof(ActiveDetailMemoryBlock));
            }
        }
    }

    public FlameBlock? SelectedMemoryBlock
    {
        get => _selectedMemoryBlock;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedMemoryBlock, value))
            {
                OnPropertyChanged(nameof(ActiveDetailMemoryBlock));
            }
        }
    }

    public FlameBlock? ActiveDetailMemoryBlock => SelectedMemoryBlock ?? HoveredMemoryBlock;

    public ObservableCollection<ProfileSessionModel> Sessions => _sessions;

    public ProfileSessionModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedSession, value))
            {
                OnSessionSelected(value);
            }
        }
    }

    public ObservableCollection<CallTreeNodeModel> CallTreeRoots => _callTreeRoots;
    public ObservableCollection<ThreadGroup> ThreadGroups => _threadGroups;
    public ObservableCollection<FlameBlock> Blocks => _blocks;
    public ObservableCollection<FlameBlock> MemoryBlocks => _memoryBlocks;
    public ObservableCollection<ProfileMethodStats> MethodStats => _methodStats;
    public ObservableCollection<ProfileMemoryStats> MemoryStats => _memoryStats;

    public ObservableCollection<ProfileCallItem> CallerMethods => _callerMethods;
    public ObservableCollection<ProfileCallItem> CalleeMethods => _calleeMethods;

    public string SelectedMethodHeader
    {
        get => _selectedMethodHeader;
        set => RaiseAndSetIfChanged(ref _selectedMethodHeader, value);
    }

    private string _selectedEngine = "eventpipe";
    private readonly string[] _availableEngines = new[] { "eventpipe", "simulated", "dottrace", "dotmemory" };

    public string[] AvailableEngines => _availableEngines;

    public string SelectedEngine
    {
        get => _selectedEngine;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedEngine, value))
            {
                _ = SetActiveProfilingEngineAsync(value);
            }
        }
    }

    public Func<string, Task>? SaveFileCallback { get; set; }
    public Func<Task<string?>>? OpenFileCallback { get; set; }

    public ICommand FindInFlameChartCommand { get; }
    public ICommand ShowMethodInCallTreeCommand { get; }
    public ICommand ShowMethodInCallerCalleeCommand { get; }
    public ICommand SearchMethodCommand { get; }

    public ICommand StartProfilerCommand { get; }
    public ICommand StopProfilerCommand { get; }
    public ICommand LoadProfileCommand { get; }
    public ICommand ExportProfileCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetViewCommand { get; }
    public ICommand NextSearchMatchCommand { get; }
    public ICommand PrevSearchMatchCommand { get; }
    public ObservableCollection<string> ProfilerLogs { get; } = new();
    public ICommand ClearLogsCommand { get; }

    public ProfilerViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        FindInFlameChartCommand = new RelayCommand(() =>
        {
            if (SelectedMethod == null) return;
            var name = SelectedMethod.MethodName;
            var block = _blocks.FirstOrDefault(b => b.Name == name || b.Name.Contains(name));
            if (block != null)
            {
                SelectedBlock = block;
            }
            else
            {
                var memBlock = _memoryBlocks.FirstOrDefault(b => b.Name == name || b.Name.Contains(name));
                if (memBlock != null)
                {
                    SelectedMemoryBlock = memBlock;
                }
            }
        }, () => SelectedMethod != null);

        ShowMethodInCallTreeCommand = new RelayCommand(() =>
        {
            if (SelectedMethod == null) return;
            ActivatePane("CallTree");
            foreach (var root in CallTreeRoots)
            {
                var path = new List<CallTreeNodeModel>();
                if (FindNodePath(root, SelectedMethod.MethodName, path))
                {
                    foreach (var parent in path) parent.IsExpanded = true;
                    path.Last().IsSelected = true;
                    break;
                }
            }
        }, () => SelectedMethod != null);

        ShowMethodInCallerCalleeCommand = new RelayCommand(() =>
        {
            if (SelectedMethod == null) return;
            ActivatePane("CallerCallee");
            UpdateCallerCallee(SelectedMethod.MethodName, SelectedMethod.ModuleName);
        }, () => SelectedMethod != null);

        SearchMethodCommand = new RelayCommand(() =>
        {
            if (SelectedMethod == null) return;
            SearchText = SelectedMethod.MethodName;
        }, () => SelectedMethod != null);

        StartProfilerCommand = new RelayCommand(async () => await StartProfilerAsync(), () => _cdpService.IsConnected && !IsProfilingActive);
        StopProfilerCommand = new RelayCommand(async () => await StopProfilerAsync(), () => _cdpService.IsConnected && IsProfilingActive);
        LoadProfileCommand = new RelayCommand(async () => await LoadProfileAsync());
        ExportProfileCommand = new RelayCommand(async () => await ExportProfileAsync(), () => SelectedSession != null);
        
        ZoomInCommand = new RelayCommand(() => ZoomScale = Math.Min(1000.0, ZoomScale * 1.5));
        ZoomOutCommand = new RelayCommand(() => ZoomScale = Math.Max(1.0, ZoomScale / 1.5));
        ResetViewCommand = new RelayCommand(() => { ZoomScale = 1.0; OffsetX = 0.0; });
        NextSearchMatchCommand = new RelayCommand(NextSearchMatch, () => HasMatches);
        PrevSearchMatchCommand = new RelayCommand(PrevSearchMatch, () => HasMatches);
        ClearLogsCommand = new RelayCommand(() => ProfilerLogs.Clear());
        ResetLayout();

        if (_cdpService.IsConnected)
        {
            _ = LoadActiveProfilingEngineAsync();
        }
    }

    public void LogProgress(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProfilerLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (ProfilerLogs.Count > 500)
            {
                ProfilerLogs.RemoveAt(0);
            }
        });
    }

    public void ResetLayout()
    {
        var left = new BoxNode();
        left.AddTab("Sessions List", "TableIcon", "SessionsList");

        var right = new BoxNode();
        right.AddTab("Flame Charts", "CodeIcon", "FlameCharts");
        right.AddTab("Call Tree", "CodeIcon", "CallTree");
        right.AddTab("Bottom-Up Calls", "TerminalIcon", "BottomUpCalls");
        right.AddTab("Caller / Callee", "SwapVertIcon", "CallerCallee");
        right.AddTab("Memory Allocations", "SaveIcon", "MemoryAllocations");

        var mainSplit = new SplitContainerNode(Orientation.Horizontal, left, right)
        {
            SplitterRatio = 0.28
        };

        var bottom = new BoxNode();
        bottom.AddTab("Profiler Log", "TerminalIcon", "ProfilerLog");

        var rootSplit = new SplitContainerNode(Orientation.Vertical, mainSplit, bottom)
        {
            SplitterRatio = 0.82
        };

        LayoutRoot = rootSplit;
        SelectedPane = left;
    }

    private void OnSessionSelected(ProfileSessionModel? session)
    {
        _blocks.Clear();
        _memoryBlocks.Clear();
        _methodStats.Clear();
        _memoryStats.Clear();
        _callTreeRoots.Clear();
        _threadGroups.Clear();
        _callerMethods.Clear();
        _calleeMethods.Clear();
        SelectedMethodHeader = "No method selected";

        if (session != null)
        {
            foreach (var b in session.Blocks) _blocks.Add(b);
            foreach (var b in session.MemoryBlocks) _memoryBlocks.Add(b);
            foreach (var s in session.MethodStats) _methodStats.Add(s);
            foreach (var s in session.MemoryStats) _memoryStats.Add(s);
            foreach (var r in session.CallTreeRoots) _callTreeRoots.Add(r);
            foreach (var tg in session.ThreadGroups)
            {
                tg.Parent = this;
                _threadGroups.Add(tg);
            }

            TotalDurationMs = session.TotalDurationMs;
            TotalAllocatedBytes = session.TotalAllocatedBytes;
            TotalSamplesCount = session.TotalSamplesCount;
            TotalAllocationsCount = session.TotalAllocationsCount;
        }
        else
        {
            TotalDurationMs = 0;
            TotalAllocatedBytes = 0;
            TotalSamplesCount = 0;
            TotalAllocationsCount = 0;
        }

        SelectedBlock = null;
        SelectedMethod = null;
        SelectedMemoryBlock = null;
        HoveredBlock = null;
        HoveredMemoryBlock = null;

        ((RelayCommand)ExportProfileCommand).RaiseCanExecuteChanged();
        UpdateSearchMatches();
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_cdpService.IsConnected)
                {
                    IsProfilingActive = false;
                }
                else
                {
                    _ = LoadActiveProfilingEngineAsync();
                }
                ((RelayCommand)StartProfilerCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopProfilerCommand).RaiseCanExecuteChanged();
            });
        }
    }

    public async Task StartProfilerAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            LogProgress($"Initializing profiling engine: '{SelectedEngine}'...");
            StatusText = "Initializing Profiler...";
            await _cdpService.SendCommandAsync("Profiler.enable");
            await _cdpService.SendCommandAsync("Profiler.start");
            IsProfilingActive = true;
            StatusText = "Profiling CPU & Memory Activity...";
            LogProgress("Profiling started. CPU & Memory activity is being recorded.");
            
            ((RelayCommand)StartProfilerCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopProfilerCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusText = $"Start profiler failed: {ex.Message}";
            LogProgress($"Start profiler failed: {ex.Message}");
        }
    }

    public async Task StopProfilerAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            LogProgress("Requesting to stop profiling session...");
            StatusText = "Stopping Profiler and fetching CPU & Memory profiles...";
            var res = await _cdpService.SendCommandAsync("Profiler.stop");
            IsProfilingActive = false;
            
            ((RelayCommand)StartProfilerCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopProfilerCommand).RaiseCanExecuteChanged();

            if (res != null)
            {
                LogProgress("Stop response received. Processing profile data...");
                var jsonStr = res.ToJsonString();
                LoadProfileFromJson(jsonStr);
                StatusText = "Session profile successfully loaded and rendered!";
                LogProgress("Session profile successfully loaded, parsed and rendered in UI.");
            }
            else
            {
                StatusText = "Failed to retrieve profile data from stopping the session.";
                LogProgress("Error: Stop response is empty.");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Stop profiler failed: {ex.Message}";
            LogProgress($"Stop profiler failed: {ex.Message}");
            IsProfilingActive = false;
            ((RelayCommand)StartProfilerCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopProfilerCommand).RaiseCanExecuteChanged();
        }
    }

    public async Task LoadProfileAsync()
    {
        if (OpenFileCallback == null) return;
        try
        {
            var filePath = await OpenFileCallback();
            if (!string.IsNullOrEmpty(filePath))
            {
                LogProgress($"Importing profile file: '{System.IO.Path.GetFileName(filePath)}'...");
                StatusText = $"Loading profile file: {System.IO.Path.GetFileName(filePath)}...";
                
                if (filePath.EndsWith(".cpuprofile", StringComparison.OrdinalIgnoreCase) ||
                    filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    LogProgress("Loading standard V8 CPU profile (.cpuprofile/.json)...");
                    string json = await System.IO.File.ReadAllTextAsync(filePath);
                    LoadProfileFromJson(json);
                    StatusText = "External CPU & Memory Profile loaded successfully!";
                    LogProgress("External CPU & Memory profile loaded and rendered.");
                }
                else if (filePath.EndsWith(".dtp", StringComparison.OrdinalIgnoreCase))
                {
                    LogProgress("Loading dotTrace performance trace (.dtp) using DtpTraceAnalyzer...");
                    var cpuSession = DtpTraceAnalyzer.LoadTrace(filePath);
                    if (cpuSession != null)
                    {
                        var session = new ProfileSessionModel
                        {
                            Name = cpuSession.Name,
                            Timestamp = DateTime.Now,
                            TotalDurationMs = cpuSession.TotalDurationMs,
                            TotalSamplesCount = cpuSession.TotalSamplesCount,
                            RawJson = System.Text.Json.JsonSerializer.Serialize(cpuSession, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
                        };

                        foreach (var b in cpuSession.Blocks)
                        {
                            session.Blocks.Add(new FlameBlock
                            {
                                Name = b.Name,
                                Url = b.Url,
                                StartTimeMs = b.StartTimeMs,
                                EndTimeMs = b.EndTimeMs,
                                Depth = b.Depth
                            });
                        }

                        var tg = new ThreadGroup { Name = "Main Thread", Id = 1 };
                        foreach (var b in session.Blocks) tg.Blocks.Add(b);
                        session.ThreadGroups.Add(tg);

                        foreach (var s in cpuSession.MethodStats)
                        {
                            session.MethodStats.Add(new ProfileMethodStats
                            {
                                MethodName = s.MethodName,
                                ModuleName = s.ModuleName,
                                SelfTimeMs = s.SelfTimeMs,
                                SelfTimePct = s.SelfTimePct,
                                TotalTimeMs = s.TotalTimeMs,
                                TotalTimePct = s.TotalTimePct,
                                HitCount = s.HitCount
                            });
                        }

                        foreach (var rootNode in cpuSession.CallTreeRoots)
                        {
                            var mappedRoot = MapCallTreeNode(rootNode);
                            if (mappedRoot != null)
                            {
                                session.CallTreeRoots.Add(mappedRoot);
                            }
                        }

                        Dispatcher.UIThread.Post(() =>
                        {
                            Sessions.Add(session);
                            SelectedSession = session;
                        });
                        StatusText = "dotTrace CPU Profile loaded successfully!";
                        LogProgress($"Loaded dotTrace performance trace: '{cpuSession.Name}' ({cpuSession.MethodStats.Count} methods, {cpuSession.TotalSamplesCount} samples).");
                    }
                }
                else if (filePath.EndsWith(".dmw", StringComparison.OrdinalIgnoreCase))
                {
                    LogProgress("Loading dotMemory workspace (.dmw) using DmwSnapshotAnalyzer...");
                    var memSession = DmwSnapshotAnalyzer.LoadWorkspace(filePath);
                    if (memSession != null)
                    {
                        var session = new ProfileSessionModel
                        {
                            Name = memSession.Name,
                            Timestamp = DateTime.Now,
                            TotalAllocatedBytes = memSession.TotalAllocatedBytes,
                            TotalAllocationsCount = memSession.TotalAllocationsCount,
                            RawJson = System.Text.Json.JsonSerializer.Serialize(memSession, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
                        };

                        foreach (var s in memSession.MemoryStats)
                        {
                            session.MemoryStats.Add(new ProfileMemoryStats
                            {
                                TypeName = s.TypeName,
                                AllocatedBytes = s.AllocatedBytes,
                                SizePct = s.SizePct,
                                AllocationCount = s.AllocationCount,
                                CountPct = s.CountPct
                            });
                        }

                        Dispatcher.UIThread.Post(() =>
                        {
                            Sessions.Add(session);
                            SelectedSession = session;
                        });
                        StatusText = "dotMemory workspace profile loaded successfully!";
                        LogProgress($"Loaded dotMemory workspace snapshot: '{memSession.Name}' ({memSession.MemoryStats.Count} type allocations, {memSession.TotalAllocatedBytes} bytes).");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Load profile failed: {ex.Message}";
            LogProgress($"Load profile failed: {ex.Message}");
        }
    }

    private CallTreeNodeModel? MapCallTreeNode(AnalyzedCallTreeNode node)
    {
        if (node == null) return null;
        var mapped = new CallTreeNodeModel
        {
            Name = node.Name,
            SelfTimeMs = node.SelfTimeMs,
            SelfTimePct = node.SelfTimePct,
            TotalTimeMs = node.TotalTimeMs,
            TotalTimePct = node.TotalTimePct,
            HitCount = node.HitCount
        };
        foreach (var child in node.Children)
        {
            var mappedChild = MapCallTreeNode(child);
            if (mappedChild != null)
            {
                mapped.Children.Add(mappedChild);
            }
        }
        return mapped;
    }

    public async Task ExportProfileAsync()
    {
        if (SelectedSession == null || SaveFileCallback == null) return;
        try
        {
            await SaveFileCallback(SelectedSession.RawJson);
            StatusText = $"Session '{SelectedSession.Name}' exported successfully.";
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed: {ex.Message}";
        }
    }

    public void LoadProfileFromJson(string json)
    {
        try
        {
            var root = JsonNode.Parse(json)?.AsObject();
            if (root == null) return;

            var wrapper = root;
            if (root.ContainsKey("profile") && root["profile"] is JsonObject innerWrapper && innerWrapper.ContainsKey("profile"))
            {
                wrapper = innerWrapper;
            }

            string? jetbrainsTracePath = null;
            if (wrapper.ContainsKey("jetbrainsTracePath"))
            {
                jetbrainsTracePath = wrapper["jetbrainsTracePath"]?.GetValue<string>();
            }
            else if (root.ContainsKey("profile") && root["profile"] is JsonObject pObj && pObj.ContainsKey("jetbrainsTracePath"))
            {
                jetbrainsTracePath = pObj["jetbrainsTracePath"]?.GetValue<string>();
            }

            if (!string.IsNullOrEmpty(jetbrainsTracePath))
            {
                if (System.IO.File.Exists(jetbrainsTracePath))
                {
                    LogProgress($"Found real dotTrace snapshot file at: {jetbrainsTracePath}");
                    DtpTraceAnalyzer.LastError = null;
                    var cpuSession = DtpTraceAnalyzer.LoadTrace(jetbrainsTracePath);
                    if (cpuSession != null)
                    {
                        var jetbrainsSession = new ProfileSessionModel
                        {
                            Name = $"Profile {_sessionCounter++} (dotTrace)",
                            Timestamp = DateTime.UtcNow,
                            TotalDurationMs = cpuSession.TotalDurationMs,
                            TotalSamplesCount = cpuSession.TotalSamplesCount,
                            RawJson = json
                        };

                        foreach (var b in cpuSession.Blocks)
                        {
                            jetbrainsSession.Blocks.Add(new FlameBlock
                            {
                                Name = b.Name,
                                Url = b.Url,
                                StartTimeMs = b.StartTimeMs,
                                EndTimeMs = b.EndTimeMs,
                                Depth = b.Depth
                            });
                        }

                        foreach (var s in cpuSession.MethodStats)
                        {
                            jetbrainsSession.MethodStats.Add(new ProfileMethodStats
                            {
                                MethodName = s.MethodName,
                                ModuleName = s.ModuleName,
                                SelfTimeMs = s.SelfTimeMs,
                                SelfTimePct = s.SelfTimePct,
                                TotalTimeMs = s.TotalTimeMs,
                                TotalTimePct = s.TotalTimePct,
                                HitCount = s.HitCount
                            });
                        }

                        foreach (var rootNode in cpuSession.CallTreeRoots)
                        {
                            var mappedRoot = MapCallTreeNode(rootNode);
                            if (mappedRoot != null)
                            {
                                jetbrainsSession.CallTreeRoots.Add(mappedRoot);
                            }
                        }

                        var mainTg = new ThreadGroup { Name = "Main Thread", Id = 1 };
                        foreach (var b in jetbrainsSession.Blocks) mainTg.Blocks.Add(b);
                        jetbrainsSession.ThreadGroups.Add(mainTg);

                        Dispatcher.UIThread.Post(() =>
                        {
                            Sessions.Add(jetbrainsSession);
                            SelectedSession = jetbrainsSession;
                        });
                        LogProgress($"Successfully loaded and parsed dotTrace performance trace ({cpuSession.MethodStats.Count} methods, {cpuSession.TotalSamplesCount} samples).");
                        return;
                    }
                    else
                    {
                        var err = DtpTraceAnalyzer.LastError ?? "DtpTraceAnalyzer.LoadTrace returned null.";
                        LogProgress($"Warning: Could not parse real dotTrace file via reflection SDK. Fallback to simulated profile. Detail: {err}");
                    }
                }
                else
                {
                    bool hasEmbeddedProfile = wrapper.ContainsKey("nodes") || 
                                              (root.ContainsKey("profile") && root["profile"] is JsonObject pObj && pObj.ContainsKey("nodes"));
                    if (!hasEmbeddedProfile)
                    {
                        throw new FileNotFoundException($"The dotTrace performance trace file '{jetbrainsTracePath}' was not found, and no embedded V8 profile is present in the imported JSON.");
                    }
                    LogProgress($"dotTrace file '{jetbrainsTracePath}' not found. Falling back to embedded V8 profile.");
                }
            }

            string? jetbrainsMemorySnapshotPath = null;
            if (wrapper.ContainsKey("jetbrainsMemorySnapshotPath"))
            {
                jetbrainsMemorySnapshotPath = wrapper["jetbrainsMemorySnapshotPath"]?.GetValue<string>();
            }
            else if (root.ContainsKey("profile") && root["profile"] is JsonObject pObj2 && pObj2.ContainsKey("jetbrainsMemorySnapshotPath"))
            {
                jetbrainsMemorySnapshotPath = pObj2["jetbrainsMemorySnapshotPath"]?.GetValue<string>();
            }

            if (!string.IsNullOrEmpty(jetbrainsMemorySnapshotPath))
            {
                if (System.IO.File.Exists(jetbrainsMemorySnapshotPath))
                {
                    LogProgress($"Found real dotMemory snapshot file at: {jetbrainsMemorySnapshotPath}");
                    DmwSnapshotAnalyzer.LastError = null;
                    var memSession = DmwSnapshotAnalyzer.LoadWorkspace(jetbrainsMemorySnapshotPath);
                    if (memSession != null)
                    {
                        var jetbrainsSession = new ProfileSessionModel
                        {
                            Name = $"Profile {_sessionCounter++} (dotMemory)",
                            Timestamp = DateTime.UtcNow,
                            TotalAllocatedBytes = memSession.TotalAllocatedBytes,
                            TotalAllocationsCount = memSession.TotalAllocationsCount,
                            RawJson = json
                        };

                        foreach (var s in memSession.MemoryStats)
                        {
                            jetbrainsSession.MemoryStats.Add(new ProfileMemoryStats
                            {
                                TypeName = s.TypeName,
                                AllocatedBytes = s.AllocatedBytes,
                                SizePct = s.SizePct,
                                AllocationCount = s.AllocationCount,
                                CountPct = s.CountPct
                            });
                        }

                        Dispatcher.UIThread.Post(() =>
                        {
                            Sessions.Add(jetbrainsSession);
                            SelectedSession = jetbrainsSession;
                        });
                        LogProgress($"Successfully loaded and parsed dotMemory workspace snapshot ({memSession.MemoryStats.Count} type allocations, {memSession.TotalAllocatedBytes} bytes).");
                        return;
                    }
                    else
                    {
                        var err = DmwSnapshotAnalyzer.LastError ?? "DmwSnapshotAnalyzer.LoadWorkspace returned null.";
                        LogProgress($"Warning: Could not parse real dotMemory workspace via reflection SDK. Fallback to simulated profile. Detail: {err}");
                    }
                }
                else
                {
                    bool hasEmbeddedMemory = wrapper.ContainsKey("memoryProfile") || wrapper.ContainsKey("memoryAllocations");
                    if (!hasEmbeddedMemory)
                    {
                        throw new FileNotFoundException($"The dotMemory workspace file '{jetbrainsMemorySnapshotPath}' was not found, and no embedded memory profile is present in the imported JSON.");
                    }
                    LogProgress($"dotMemory file '{jetbrainsMemorySnapshotPath}' not found. Falling back to embedded memory profile.");
                }
            }

            var session = new ProfileSessionModel
            {
                Name = $"Profile {_sessionCounter++}",
                Timestamp = DateTime.Now,
                RawJson = json
            };

            var cpuProfileObj = wrapper["profile"]?.AsObject();
            var memProfileObj = wrapper["memoryProfile"]?.AsObject();
            var memAllocArray = wrapper["memoryAllocations"]?.AsArray();

            var threadsArray = wrapper["threads"]?.AsArray() ?? cpuProfileObj?["threads"]?.AsArray();
            if (threadsArray != null && threadsArray.Count > 0)
            {
                double maxDuration = 0;
                int totalSamples = 0;

                foreach (var tNode in threadsArray)
                {
                    if (tNode is JsonObject tObj)
                    {
                        string tName = tObj["name"]?.GetValue<string>() ?? tObj["threadName"]?.GetValue<string>() ?? "Thread";
                        int tId = tObj["id"]?.GetValue<int>() ?? tObj["tid"]?.GetValue<int>() ?? 1;

                        var tg = new ThreadGroup { Name = tName, Id = tId };

                        var tempProfile = new JsonObject();
                        if (tObj.ContainsKey("nodes"))
                        {
                            tempProfile["nodes"] = tObj["nodes"]?.DeepClone();
                        }
                        else if (cpuProfileObj != null && cpuProfileObj.ContainsKey("nodes"))
                        {
                            tempProfile["nodes"] = cpuProfileObj["nodes"]?.DeepClone();
                        }
                        else if (wrapper.ContainsKey("nodes"))
                        {
                            tempProfile["nodes"] = wrapper["nodes"]?.DeepClone();
                        }

                        tempProfile["samples"] = tObj["samples"]?.DeepClone();
                        tempProfile["timeDeltas"] = tObj["timeDeltas"]?.DeepClone();

                        var threadStats = new ObservableCollection<ProfileMethodStats>();
                        ProcessV8Profile(session, tempProfile, tg.Blocks, threadStats, false, out double tDur, out int tSamples);

                        session.ThreadGroups.Add(tg);
                        if (tDur > maxDuration) maxDuration = tDur;
                        totalSamples += tSamples;

                        if (session.Blocks.Count == 0)
                        {
                            foreach (var b in tg.Blocks) session.Blocks.Add(b);
                        }

                        // Accumulate threadStats into session.MethodStats
                        foreach (var tStat in threadStats)
                        {
                            var existing = session.MethodStats.FirstOrDefault(s => 
                                string.Equals(s.MethodName, tStat.MethodName, StringComparison.OrdinalIgnoreCase) && 
                                string.Equals(s.ModuleName, tStat.ModuleName, StringComparison.OrdinalIgnoreCase));
                            if (existing != null)
                            {
                                existing.SelfTimeMs += tStat.SelfTimeMs;
                                existing.TotalTimeMs += tStat.TotalTimeMs;
                                existing.HitCount += tStat.HitCount;
                            }
                            else
                            {
                                session.MethodStats.Add(tStat);
                            }
                        }
                    }
                }

                // Recalculate percentages for session.MethodStats based on total accumulated time
                double totalCpuTimeAcrossThreads = session.MethodStats.Sum(s => s.SelfTimeMs);
                foreach (var s in session.MethodStats)
                {
                    s.SelfTimePct = totalCpuTimeAcrossThreads > 0 ? (s.SelfTimeMs / totalCpuTimeAcrossThreads) * 100.0 : 0.0;
                    s.TotalTimePct = totalCpuTimeAcrossThreads > 0 ? (s.TotalTimeMs / totalCpuTimeAcrossThreads) * 100.0 : 0.0;
                }

                session.TotalDurationMs = maxDuration;
                session.TotalSamplesCount = totalSamples;
                BuildV8CallTreeFromProfile(cpuProfileObj ?? wrapper, session);
            }
            else
            {
                if (cpuProfileObj != null)
                {
                    ProcessV8Profile(session, cpuProfileObj, session.Blocks, session.MethodStats, false, out double cpuDur, out int cpuSamples);
                    session.TotalDurationMs = cpuDur;
                    session.TotalSamplesCount = cpuSamples;
                    BuildV8CallTreeFromProfile(cpuProfileObj, session);
                }
                else
                {
                    ProcessV8Profile(session, wrapper, session.Blocks, session.MethodStats, false, out double cpuDur, out int cpuSamples);
                    session.TotalDurationMs = cpuDur;
                    session.TotalSamplesCount = cpuSamples;
                    BuildV8CallTreeFromProfile(wrapper, session);
                }

                var tg = new ThreadGroup { Name = "Main Thread", Id = 1 };
                foreach (var b in session.Blocks) tg.Blocks.Add(b);
                session.ThreadGroups.Add(tg);
            }

            if (memProfileObj != null)
            {
                ProcessV8Profile(session, memProfileObj, session.MemoryBlocks, new ObservableCollection<ProfileMethodStats>(), true, out double memDur, out int memSamples);
                session.TotalAllocatedBytes = memDur;
                session.TotalAllocationsCount = memSamples;
            }

            if (memAllocArray != null)
            {
                long totalBytes = 0;
                int totalCount = 0;
                var list = new List<ProfileMemoryStats>();

                foreach (var item in memAllocArray)
                {
                    if (item is JsonObject obj)
                    {
                        string typeName = obj["typeName"]?.GetValue<string>() ?? "Unknown";
                        long bytes = obj["bytes"]?.GetValue<long>() ?? 0;
                        int count = obj["count"]?.GetValue<int>() ?? 0;

                        list.Add(new ProfileMemoryStats
                        {
                            TypeName = typeName,
                            AllocatedBytes = bytes,
                            AllocationCount = count
                        });

                        totalBytes += bytes;
                        totalCount += count;
                    }
                }

                foreach (var stat in list)
                {
                    stat.SizePct = totalBytes > 0 ? (stat.AllocatedBytes / (double)totalBytes) * 100.0 : 0.0;
                    stat.CountPct = totalCount > 0 ? (stat.AllocationCount / (double)totalCount) * 100.0 : 0.0;
                    session.MemoryStats.Add(stat);
                }

                session.TotalAllocatedBytes = totalBytes;
                session.TotalAllocationsCount = totalCount;
            }

            LogProgress($"Successfully processed V8 profile ({session.TotalSamplesCount} samples, {session.TotalDurationMs:F2} ms).");

            Dispatcher.UIThread.Post(() =>
            {
                Sessions.Add(session);
                SelectedSession = session;
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Parse profile failed: {ex.Message}";
            LogProgress($"Parse profile failed: {ex.Message}");
        }
    }

    private void BuildV8CallTreeFromProfile(JsonObject profileObj, ProfileSessionModel session)
    {
        var nodesArray = profileObj["nodes"]?.AsArray();
        if (nodesArray == null) return;

        var nodeMap = new Dictionary<int, JsonObject>();
        foreach (var n in nodesArray)
        {
            if (n is JsonObject nObj)
            {
                int id = nObj["id"]?.GetValue<int>() ?? 0;
                nodeMap[id] = nObj;
            }
        }

        var statsMap = new Dictionary<string, ProfileMethodStats>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in session.MethodStats)
        {
            string key = $"{s.MethodName}@{s.ModuleName}";
            statsMap[key] = s;
        }

        CallTreeNodeModel? BuildNode(int nodeId, HashSet<int> visited)
        {
            if (!nodeMap.TryGetValue(nodeId, out var nObj)) return null;
            if (visited.Contains(nodeId)) return null;
            visited.Add(nodeId);

            var cfObj = nObj["callFrame"] as JsonObject;
            string funcName = cfObj?["functionName"]?.GetValue<string>() ?? "";
            string url = cfObj?["url"]?.GetValue<string>() ?? "";
            string key = $"{funcName}@{url}";
            
            double selfTime = 0;
            double selfPct = 0;
            double totalTime = 0;
            double totalPct = 0;
            int hitCount = nObj["hitCount"]?.GetValue<int>() ?? 0;

            if (statsMap.TryGetValue(key, out var stats))
            {
                selfTime = stats.SelfTimeMs;
                selfPct = stats.SelfTimePct;
                totalTime = stats.TotalTimeMs;
                totalPct = stats.TotalTimePct;
            }

            var model = new CallTreeNodeModel
            {
                Name = funcName,
                SelfTimeMs = selfTime,
                SelfTimePct = selfPct,
                TotalTimeMs = totalTime,
                TotalTimePct = totalPct,
                HitCount = hitCount
            };

            var children = nObj["children"]?.AsArray();
            if (children != null)
            {
                foreach (var childIdNode in children)
                {
                    int childId = childIdNode?.GetValue<int>() ?? 0;
                    var childModel = BuildNode(childId, visited);
                    if (childModel != null)
                    {
                        model.Children.Add(childModel);
                    }
                }
            }

            visited.Remove(nodeId);
            return model;
        }

        var visitedSet = new HashSet<int>();
        var rootModel = BuildNode(1, visitedSet);
        if (rootModel != null)
        {
            session.CallTreeRoots.Add(rootModel);
        }
    }

    private void ProcessV8Profile(
        ProfileSessionModel session,
        JsonObject profileObj,
        ObservableCollection<FlameBlock> targetBlocks,
        ObservableCollection<ProfileMethodStats> targetStats,
        bool isMemoryMode,
        out double totalDuration,
        out int samplesCount)
    {
        totalDuration = 0;
        samplesCount = 0;

        var nodesArray = profileObj["nodes"]?.AsArray();
        var samplesArray = profileObj["samples"]?.AsArray();
        var timeDeltasArray = profileObj["timeDeltas"]?.AsArray();

        if (nodesArray == null || samplesArray == null || timeDeltasArray == null) return;

        var nodes = new List<Chrome.DevTools.Protocol.Domains.V8ProfileNode>();
        var nodeMap = new Dictionary<int, Chrome.DevTools.Protocol.Domains.V8ProfileNode>();
        foreach (var n in nodesArray)
        {
            if (n is JsonObject nObj)
            {
                int id = nObj["id"]?.GetValue<int>() ?? 0;
                int hitCount = nObj["hitCount"]?.GetValue<int>() ?? 0;
                var cfObj = nObj["callFrame"] as JsonObject;
                string funcName = cfObj?["functionName"]?.GetValue<string>() ?? "";
                string url = cfObj?["url"]?.GetValue<string>() ?? "";
                int line = cfObj?["lineNumber"]?.GetValue<int>() ?? 0;
                int col = cfObj?["columnNumber"]?.GetValue<int>() ?? 0;

                var node = new Chrome.DevTools.Protocol.Domains.V8ProfileNode(id, funcName, url, line, col)
                {
                    HitCount = hitCount
                };

                var children = nObj["children"]?.AsArray();
                if (children != null)
                {
                    foreach (var cId in children)
                    {
                        node.Children.Add(cId?.GetValue<int>() ?? 0);
                    }
                }
                nodes.Add(node);
                nodeMap[id] = node;
            }
        }

        var samples = samplesArray.Select(s => s?.GetValue<int>() ?? 0).ToList();
        var timeDeltas = timeDeltasArray.Select(t => t?.GetValue<double>() ?? 0.0).ToList();

        samplesCount = samples.Count;

        var parentMap = new Dictionary<int, int>();
        foreach (var node in nodes)
        {
            foreach (var childId in node.Children)
            {
                parentMap[childId] = node.Id;
            }
        }

        if (!isMemoryMode)
        {
            session.CpuSamples = samples;
            session.CpuTimeDeltas = timeDeltas;
            session.CpuNodeMap = nodeMap;
            session.CpuParentMap = parentMap;
        }

        var newBlocks = new List<FlameBlock>();
        var activeBlocks = new Dictionary<int, FlameBlock>();

        double currentValue = 0.0;
        for (int i = 0; i < samples.Count; i++)
        {
            int nodeId = samples[i];
            double delta = i < timeDeltas.Count ? timeDeltas[i] : 0;
            double duration = isMemoryMode ? delta : (delta / 1000.0);

            var stack = GetStack(nodeId, nodeMap, parentMap);

            for (int depth = 0; depth < stack.Count; depth++)
            {
                var node = stack[depth];
                if (activeBlocks.TryGetValue(depth, out var active) && active.Name == node.FunctionName && active.Url == node.Url)
                {
                    active.EndTimeMs = currentValue + duration;
                }
                else
                {
                    var maxActiveDepth = activeBlocks.Keys.DefaultIfEmpty(-1).Max();
                    for (int d = depth; d <= maxActiveDepth; d++)
                    {
                        if (activeBlocks.TryGetValue(d, out var deeperActive))
                        {
                            newBlocks.Add(deeperActive);
                            activeBlocks.Remove(d);
                        }
                    }

                    var newBlock = new FlameBlock
                    {
                        Name = node.FunctionName,
                        Url = node.Url,
                        StartTimeMs = currentValue,
                        EndTimeMs = currentValue + duration,
                        Depth = depth
                    };
                    activeBlocks[depth] = newBlock;
                }
            }

            var depthsToRemove = activeBlocks.Keys.Where(d => d >= stack.Count).ToList();
            foreach (var depth in depthsToRemove)
            {
                newBlocks.Add(activeBlocks[depth]);
                activeBlocks.Remove(depth);
            }

            currentValue += duration;
        }

        foreach (var active in activeBlocks.Values)
        {
            newBlocks.Add(active);
        }

        totalDuration = currentValue;

        var statsMap = new Dictionary<string, ProfileMethodStats>();
        double totalDeltaSum = timeDeltas.Sum();

        for (int i = 0; i < samples.Count; i++)
        {
            int nodeId = samples[i];
            double dt = i < timeDeltas.Count ? timeDeltas[i] : 0;
            double val = isMemoryMode ? dt : (dt / 1000.0);

            var stack = GetStack(nodeId, nodeMap, parentMap);
            if (stack.Count == 0) continue;

            var leaf = stack[^1];
            string leafKey = $"{leaf.FunctionName}@{leaf.Url}";
            if (!statsMap.TryGetValue(leafKey, out var leafStats))
            {
                leafStats = new ProfileMethodStats { MethodName = leaf.FunctionName, ModuleName = leaf.Url };
                statsMap[leafKey] = leafStats;
            }
            leafStats.SelfTimeMs += val;
            leafStats.HitCount++;

            var uniqueKeys = stack.Select(s => $"{s.FunctionName}@{s.Url}").Distinct();
            foreach (var key in uniqueKeys)
            {
                if (!statsMap.TryGetValue(key, out var stats))
                {
                    var node = stack.First(s => $"{s.FunctionName}@{s.Url}" == key);
                    stats = new ProfileMethodStats { MethodName = node.FunctionName, ModuleName = node.Url };
                    statsMap[key] = stats;
                }
                stats.TotalTimeMs += val;
            }
        }

        double totalVal = isMemoryMode ? totalDeltaSum : (totalDeltaSum / 1000.0);
        foreach (var stats in statsMap.Values)
        {
            stats.SelfTimePct = totalVal > 0 ? (stats.SelfTimeMs / totalVal) * 100.0 : 0.0;
            stats.TotalTimePct = totalVal > 0 ? (stats.TotalTimeMs / totalVal) * 100.0 : 0.0;
        }

        targetBlocks.Clear();
        foreach (var block in newBlocks)
        {
            string key = $"{block.Name}@{block.Url}";
            if (statsMap.TryGetValue(key, out var stats))
            {
                block.SelfTimeMs = stats.SelfTimeMs;
                block.SelfTimePct = stats.SelfTimePct;
                block.TotalTimeMs = stats.TotalTimeMs;
                block.TotalTimePct = stats.TotalTimePct;
            }
            targetBlocks.Add(block);
        }

        targetStats.Clear();
        foreach (var stats in statsMap.Values.OrderByDescending(s => s.SelfTimeMs))
        {
            targetStats.Add(stats);
        }
    }

    private void UpdateSearchMatches()
    {
        _matchingSearchBlocks.Clear();
        if (!string.IsNullOrEmpty(SearchText))
        {
            foreach (var block in _blocks)
            {
                if (block.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                {
                    _matchingSearchBlocks.Add(block);
                }
            }
        }

        if (_matchingSearchBlocks.Count > 0)
        {
            _currentMatchIndex = 0;
            HasMatches = true;
            CurrentMatchIndexText = $"{_currentMatchIndex + 1} of {_matchingSearchBlocks.Count}";
            SelectedBlock = _matchingSearchBlocks[_currentMatchIndex];
        }
        else
        {
            _currentMatchIndex = -1;
            HasMatches = false;
            CurrentMatchIndexText = string.IsNullOrEmpty(SearchText) ? "" : "0 of 0";
        }

        if (NextSearchMatchCommand is RelayCommand cmdNext) cmdNext.RaiseCanExecuteChanged();
        if (PrevSearchMatchCommand is RelayCommand cmdPrev) cmdPrev.RaiseCanExecuteChanged();
    }

    private void NextSearchMatch()
    {
        if (_matchingSearchBlocks.Count == 0) return;
        _currentMatchIndex = (_currentMatchIndex + 1) % _matchingSearchBlocks.Count;
        CurrentMatchIndexText = $"{_currentMatchIndex + 1} of {_matchingSearchBlocks.Count}";
        SelectedBlock = _matchingSearchBlocks[_currentMatchIndex];
    }

    private void PrevSearchMatch()
    {
        if (_matchingSearchBlocks.Count == 0) return;
        _currentMatchIndex = _currentMatchIndex - 1;
        if (_currentMatchIndex < 0) _currentMatchIndex = _matchingSearchBlocks.Count - 1;
        CurrentMatchIndexText = $"{_currentMatchIndex + 1} of {_matchingSearchBlocks.Count}";
        SelectedBlock = _matchingSearchBlocks[_currentMatchIndex];
    }

    private static List<Chrome.DevTools.Protocol.Domains.V8ProfileNode> GetStack(
        int nodeId,
        Dictionary<int, Chrome.DevTools.Protocol.Domains.V8ProfileNode> nodeMap,
        Dictionary<int, int> parentMap)
    {
        var stack = new List<Chrome.DevTools.Protocol.Domains.V8ProfileNode>();
        int currentId = nodeId;
        while (currentId != 0)
        {
            if (nodeMap.TryGetValue(currentId, out var node))
            {
                stack.Insert(0, node);
                if (parentMap.TryGetValue(currentId, out int parentId))
                {
                    currentId = parentId;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }
        return stack;
    }

    public void UpdateCallerCallee(string? methodName, string? moduleName)
    {
        _callerMethods.Clear();
        _calleeMethods.Clear();

        if (string.IsNullOrEmpty(methodName))
        {
            SelectedMethodHeader = "No method selected";
            return;
        }

        SelectedMethodHeader = $"{methodName} ({moduleName})";

        var session = SelectedSession;
        if (session == null || session.CpuSamples == null || session.CpuTimeDeltas == null || session.CpuNodeMap == null || session.CpuParentMap == null)
        {
            return;
        }

        var callersDict = new Dictionary<string, (string name, string url, double time, int hits)>();
        var calleesDict = new Dictionary<string, (string name, string url, double time, int hits)>();
        double totalSelectedMethodTime = 0.0;

        for (int i = 0; i < session.CpuSamples.Count; i++)
        {
            int nodeId = session.CpuSamples[i];
            double dt = i < session.CpuTimeDeltas.Count ? session.CpuTimeDeltas[i] : 0.0;
            double val = dt / 1000.0; // convert to ms

            var stack = GetStack(nodeId, session.CpuNodeMap, session.CpuParentMap);
            if (stack.Count == 0) continue;

            // Find occurrences of this method in the stack
            for (int j = 0; j < stack.Count; j++)
            {
                var node = stack[j];
                if (node.FunctionName == methodName && node.Url == moduleName)
                {
                    totalSelectedMethodTime += val;

                    if (j > 0)
                    {
                        var caller = stack[j - 1];
                        string key = $"{caller.FunctionName}@{caller.Url}";
                        if (!callersDict.TryGetValue(key, out var entry))
                        {
                            entry = (caller.FunctionName, caller.Url, 0.0, 0);
                        }
                        entry.time += val;
                        entry.hits++;
                        callersDict[key] = entry;
                    }
                    else
                    {
                        string key = "[Root]@[System]";
                        if (!callersDict.TryGetValue(key, out var entry))
                        {
                            entry = ("[Root]", "[System]", 0.0, 0);
                        }
                        entry.time += val;
                        entry.hits++;
                        callersDict[key] = entry;
                    }

                    if (j < stack.Count - 1)
                    {
                        var callee = stack[j + 1];
                        string key = $"{callee.FunctionName}@{callee.Url}";
                        if (!calleesDict.TryGetValue(key, out var entry))
                        {
                            entry = (callee.FunctionName, callee.Url, 0.0, 0);
                        }
                        entry.time += val;
                        entry.hits++;
                        calleesDict[key] = entry;
                    }
                }
            }
        }

        // Now populate the observable collections
        foreach (var entry in callersDict.Values.OrderByDescending(c => c.time))
        {
            _callerMethods.Add(new ProfileCallItem
            {
                MethodName = entry.name,
                ModuleName = entry.url,
                TimeMs = entry.time,
                Percentage = totalSelectedMethodTime > 0 ? (entry.time / totalSelectedMethodTime) * 100.0 : 0.0,
                HitCount = entry.hits
            });
        }

        foreach (var entry in calleesDict.Values.OrderByDescending(c => c.time))
        {
            _calleeMethods.Add(new ProfileCallItem
            {
                MethodName = entry.name,
                ModuleName = entry.url,
                TimeMs = entry.time,
                Percentage = totalSelectedMethodTime > 0 ? (entry.time / totalSelectedMethodTime) * 100.0 : 0.0,
                HitCount = entry.hits
            });
        }
    }

    private async Task LoadActiveProfilingEngineAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            var res = await _cdpService.SendCommandAsync("Profiler.getProfilingEngine");
            if (res != null && res["engineName"] != null)
            {
                var engineName = res["engineName"]!.GetValue<string>();
                _selectedEngine = engineName;
                OnPropertyChanged(nameof(SelectedEngine));
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to get profiling engine: {ex.Message}";
        }
    }

    private async Task SetActiveProfilingEngineAsync(string engineName)
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            LogProgress($"Switching profiling engine to: '{engineName}'...");
            StatusText = $"Switching profiling engine to {engineName}...";
            await _cdpService.SendCommandAsync("Profiler.setProfilingEngine", new JsonObject
            {
                ["engineName"] = engineName
            });
            StatusText = $"Profiling engine set to {engineName}.";
            LogProgress($"Profiling engine successfully switched to: '{engineName}'.");
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to set profiling engine to {engineName}: {ex.Message}";
            LogProgress($"Failed to set profiling engine to '{engineName}': {ex.Message}");
            // Try to revert locally to actual engine
            _ = LoadActiveProfilingEngineAsync();
        }
    }

    private bool FindNodePath(CallTreeNodeModel current, string name, List<CallTreeNodeModel> path)
    {
        path.Add(current);
        if (current.Name == name || current.Name.Contains(name))
        {
            return true;
        }

        foreach (var child in current.Children)
        {
            if (FindNodePath(child, name, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    private BoxNode? FindBoxWithViewName(SplitNode? node, string viewName)
    {
        if (node is BoxNode box)
        {
            if (box.Tabs.Any(t => t.SelectedViewName == viewName))
            {
                return box;
            }
        }
        else if (node is SplitContainerNode container)
        {
            var res1 = FindBoxWithViewName(container.Child1, viewName);
            if (res1 != null) return res1;
            return FindBoxWithViewName(container.Child2, viewName);
        }
        return null;
    }

    public void ActivatePane(string viewName)
    {
        var box = FindBoxWithViewName(LayoutRoot, viewName);
        if (box != null)
        {
            var tab = box.Tabs.FirstOrDefault(t => t.SelectedViewName == viewName);
            if (tab != null)
            {
                box.ActiveTab = tab;
            }
            SelectedPane = box;
        }
    }

    public string StateKey => "profiler";

    public JsonNode? SaveState()
    {
        return null;
    }

    public void LoadState(JsonNode state)
    {
    }
}
