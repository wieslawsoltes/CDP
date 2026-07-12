using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public Func<string, Task>? SaveFileCallback { get; set; }
    public Func<Task<string?>>? OpenFileCallback { get; set; }

    public ICommand StartProfilerCommand { get; }
    public ICommand StopProfilerCommand { get; }
    public ICommand LoadProfileCommand { get; }
    public ICommand ExportProfileCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetViewCommand { get; }
    public ICommand NextSearchMatchCommand { get; }
    public ICommand PrevSearchMatchCommand { get; }

    public ProfilerViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        StartProfilerCommand = new RelayCommand(async () => await StartProfilerAsync(), () => _cdpService.IsConnected && !IsProfilingActive);
        StopProfilerCommand = new RelayCommand(async () => await StopProfilerAsync(), () => _cdpService.IsConnected && IsProfilingActive);
        LoadProfileCommand = new RelayCommand(async () => await LoadProfileAsync());
        ExportProfileCommand = new RelayCommand(async () => await ExportProfileAsync(), () => SelectedSession != null);
        
        ZoomInCommand = new RelayCommand(() => ZoomScale = Math.Min(1000.0, ZoomScale * 1.5));
        ZoomOutCommand = new RelayCommand(() => ZoomScale = Math.Max(1.0, ZoomScale / 1.5));
        ResetViewCommand = new RelayCommand(() => { ZoomScale = 1.0; OffsetX = 0.0; });
        NextSearchMatchCommand = new RelayCommand(NextSearchMatch, () => HasMatches);
        PrevSearchMatchCommand = new RelayCommand(PrevSearchMatch, () => HasMatches);
        ResetLayout();
    }

    public void ResetLayout()
    {
        var left = new BoxNode();
        left.AddTab("Sessions List", "TableIcon", "SessionsList");

        var right = new BoxNode();
        right.AddTab("Flame Charts", "CodeIcon", "FlameCharts");
        right.AddTab("Bottom-Up Calls", "TerminalIcon", "BottomUpCalls");
        right.AddTab("Caller / Callee", "SwapVertIcon", "CallerCallee");
        right.AddTab("Memory Allocations", "SaveIcon", "MemoryAllocations");

        LayoutRoot = new SplitContainerNode(Orientation.Horizontal, left, right) { SplitterRatio = 0.35 };
        SelectedPane = left;
    }

    private void OnSessionSelected(ProfileSessionModel? session)
    {
        _blocks.Clear();
        _memoryBlocks.Clear();
        _methodStats.Clear();
        _memoryStats.Clear();
        _callerMethods.Clear();
        _calleeMethods.Clear();
        SelectedMethodHeader = "No method selected";

        if (session != null)
        {
            foreach (var b in session.Blocks) _blocks.Add(b);
            foreach (var b in session.MemoryBlocks) _memoryBlocks.Add(b);
            foreach (var s in session.MethodStats) _methodStats.Add(s);
            foreach (var s in session.MemoryStats) _memoryStats.Add(s);

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
            StatusText = "Initializing Profiler...";
            await _cdpService.SendCommandAsync("Profiler.enable");
            await _cdpService.SendCommandAsync("Profiler.start");
            IsProfilingActive = true;
            StatusText = "Profiling CPU & Memory Activity...";
            
            ((RelayCommand)StartProfilerCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopProfilerCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusText = $"Start profiler failed: {ex.Message}";
        }
    }

    public async Task StopProfilerAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            StatusText = "Stopping Profiler and fetching CPU & Memory profiles...";
            var res = await _cdpService.SendCommandAsync("Profiler.stop");
            IsProfilingActive = false;
            
            ((RelayCommand)StartProfilerCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopProfilerCommand).RaiseCanExecuteChanged();

            if (res != null)
            {
                var jsonStr = res.ToJsonString();
                LoadProfileFromJson(jsonStr);
                StatusText = "Session profile successfully loaded and rendered!";
            }
            else
            {
                StatusText = "Failed to retrieve profile data from stopping the session.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Stop profiler failed: {ex.Message}";
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
            var json = await OpenFileCallback();
            if (!string.IsNullOrEmpty(json))
            {
                StatusText = "Parsing selected profile JSON...";
                LoadProfileFromJson(json);
                StatusText = "External CPU & Memory Profile loaded successfully!";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Load profile failed: {ex.Message}";
        }
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

            var session = new ProfileSessionModel
            {
                Name = $"Profile {_sessionCounter++}",
                Timestamp = DateTime.Now,
                RawJson = json
            };

            var cpuProfileObj = wrapper["profile"]?.AsObject();
            var memProfileObj = wrapper["memoryProfile"]?.AsObject();
            var memAllocArray = wrapper["memoryAllocations"]?.AsArray();

            if (cpuProfileObj != null)
            {
                ProcessV8Profile(session, cpuProfileObj, session.Blocks, session.MethodStats, false, out double cpuDur, out int cpuSamples);
                session.TotalDurationMs = cpuDur;
                session.TotalSamplesCount = cpuSamples;
            }
            else
            {
                ProcessV8Profile(session, wrapper, session.Blocks, session.MethodStats, false, out double cpuDur, out int cpuSamples);
                session.TotalDurationMs = cpuDur;
                session.TotalSamplesCount = cpuSamples;
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

            Dispatcher.UIThread.Post(() =>
            {
                Sessions.Add(session);
                SelectedSession = session;
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Parse profile failed: {ex.Message}";
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

    private void UpdateCallerCallee(string? methodName, string? moduleName)
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

    public string StateKey => "profiler";

    public JsonNode? SaveState()
    {
        return null;
    }

    public void LoadState(JsonNode state)
    {
    }
}
