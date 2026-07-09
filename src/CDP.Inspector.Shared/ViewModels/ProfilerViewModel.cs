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

public class ProfilerViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private bool _isProfilingActive;
    private string _statusText = "Idle - Connect target to record or load a profile";
    private double _totalDurationMs;
    private int _totalSamplesCount;
    private double _zoomScale = 1.0;
    private double _offsetX = 0.0;
    private string? _searchText;
    private FlameBlock? _hoveredBlock;
    
    private ObservableCollection<FlameBlock> _blocks = new();
    private ObservableCollection<ProfileMethodStats> _methodStats = new();

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

    public int TotalSamplesCount
    {
        get => _totalSamplesCount;
        private set => RaiseAndSetIfChanged(ref _totalSamplesCount, value);
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

    private FlameBlock? _selectedBlock;
    public FlameBlock? SelectedBlock
    {
        get => _selectedBlock;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedBlock, value))
            {
                OnPropertyChanged(nameof(ActiveDetailBlock));
            }
        }
    }

    public FlameBlock? ActiveDetailBlock => SelectedBlock ?? HoveredBlock;

    public ObservableCollection<FlameBlock> Blocks => _blocks;
    public ObservableCollection<ProfileMethodStats> MethodStats => _methodStats;

    public Func<string, Task>? SaveFileCallback { get; set; }
    public Func<Task<string?>>? OpenFileCallback { get; set; }

    public ICommand StartProfilerCommand { get; }
    public ICommand StopProfilerCommand { get; }
    public ICommand LoadProfileCommand { get; }
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
        
        ZoomInCommand = new RelayCommand(() => ZoomScale = Math.Min(1000.0, ZoomScale * 1.5));
        ZoomOutCommand = new RelayCommand(() => ZoomScale = Math.Max(1.0, ZoomScale / 1.5));
        ResetViewCommand = new RelayCommand(() => { ZoomScale = 1.0; OffsetX = 0.0; });
        NextSearchMatchCommand = new RelayCommand(NextSearchMatch, () => HasMatches);
        PrevSearchMatchCommand = new RelayCommand(PrevSearchMatch, () => HasMatches);
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
            StatusText = "Profiling CPU Activity...";
            
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
            StatusText = "Stopping Profiler and fetching CPU profile...";
            var res = await _cdpService.SendCommandAsync("Profiler.stop");
            IsProfilingActive = false;
            
            ((RelayCommand)StartProfilerCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopProfilerCommand).RaiseCanExecuteChanged();

            var profileNode = res["profile"]?.AsObject();
            if (profileNode != null)
            {
                var profileJson = profileNode.ToString();
                LoadProfileFromJson(profileJson);
                StatusText = "CPU Profile successfully loaded and rendered!";

                if (SaveFileCallback != null)
                {
                    await SaveFileCallback(profileJson);
                }
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
                StatusText = "External CPU Profile loaded and visualized successfully!";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Load profile failed: {ex.Message}";
        }
    }

    public void LoadProfileFromJson(string json)
    {
        try
        {
            var root = JsonNode.Parse(json)?.AsObject();
            if (root == null) return;

            var nodesArray = root["nodes"]?.AsArray();
            var samplesArray = root["samples"]?.AsArray();
            var timeDeltasArray = root["timeDeltas"]?.AsArray();
            double startTime = root["startTime"]?.GetValue<double>() ?? 0.0;
            double endTime = root["endTime"]?.GetValue<double>() ?? 0.0;

            if (nodesArray == null || samplesArray == null || timeDeltasArray == null)
            {
                StatusText = "Invalid V8 profile format: missing nodes, samples, or timeDeltas.";
                return;
            }

            // Parse nodes
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

                    var node = new Chrome.DevTools.Protocol.Domains.V8ProfileNode(id, funcName, url, line, col);
                    node.HitCount = hitCount;
                    
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
            var timeDeltas = timeDeltasArray.Select(t => t?.GetValue<int>() ?? 0).ToList();

            Dispatcher.UIThread.Post(() =>
            {
                UpdateProfileData(nodes, nodeMap, samples, timeDeltas, startTime, endTime);
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Parse profile failed: {ex.Message}";
        }
    }

    private void UpdateProfileData(
        List<Chrome.DevTools.Protocol.Domains.V8ProfileNode> nodes,
        Dictionary<int, Chrome.DevTools.Protocol.Domains.V8ProfileNode> nodeMap,
        List<int> samples,
        List<int> timeDeltas,
        double startTime,
        double endTime)
    {
        TotalSamplesCount = samples.Count;

        // Build parent links map
        var parentMap = new Dictionary<int, int>();
        foreach (var node in nodes)
        {
            foreach (var childId in node.Children)
            {
                parentMap[childId] = node.Id;
            }
        }

        // Calculate chronological timeline and blocks
        var newBlocks = new List<FlameBlock>();
        var activeBlocks = new Dictionary<int, FlameBlock>();

        double currentTimeMs = 0.0;
        for (int i = 0; i < samples.Count; i++)
        {
            int nodeId = samples[i];
            double durationMs = (i < timeDeltas.Count ? timeDeltas[i] : 0) / 1000.0;

            var stack = GetStack(nodeId, nodeMap, parentMap);

            for (int depth = 0; depth < stack.Count; depth++)
            {
                var node = stack[depth];
                if (activeBlocks.TryGetValue(depth, out var active) && active.Name == node.FunctionName && active.Url == node.Url)
                {
                    active.EndTimeMs = currentTimeMs + durationMs;
                }
                else
                {
                    if (active != null)
                    {
                        newBlocks.Add(active);
                    }
                    var newBlock = new FlameBlock
                    {
                        Name = node.FunctionName,
                        Url = node.Url,
                        StartTimeMs = currentTimeMs,
                        EndTimeMs = currentTimeMs + durationMs,
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

            currentTimeMs += durationMs;
        }

        foreach (var active in activeBlocks.Values)
        {
            newBlocks.Add(active);
        }

        TotalDurationMs = currentTimeMs;

        // Compute bottom-up stats
        var statsMap = new Dictionary<string, ProfileMethodStats>();
        double totalTimeSumUs = timeDeltas.Sum();

        for (int i = 0; i < samples.Count; i++)
        {
            int nodeId = samples[i];
            double dt = i < timeDeltas.Count ? timeDeltas[i] : 0;

            var stack = GetStack(nodeId, nodeMap, parentMap);
            if (stack.Count == 0) continue;

            var leaf = stack[^1];
            string leafKey = $"{leaf.FunctionName}@{leaf.Url}";
            if (!statsMap.TryGetValue(leafKey, out var leafStats))
            {
                leafStats = new ProfileMethodStats { MethodName = leaf.FunctionName, ModuleName = leaf.Url };
                statsMap[leafKey] = leafStats;
            }
            leafStats.SelfTimeMs += dt / 1000.0;
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
                stats.TotalTimeMs += dt / 1000.0;
            }
        }

        double totalTimeMs = totalTimeSumUs / 1000.0;
        foreach (var stats in statsMap.Values)
        {
            stats.SelfTimePct = totalTimeMs > 0 ? (stats.SelfTimeMs / totalTimeMs) * 100.0 : 0.0;
            stats.TotalTimePct = totalTimeMs > 0 ? (stats.TotalTimeMs / totalTimeMs) * 100.0 : 0.0;
        }

        // Populate Blocks (resolving aggregated times from statsMap)
        Blocks.Clear();
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
            Blocks.Add(block);
        }

        MethodStats.Clear();
        var sortedStats = statsMap.Values.OrderByDescending(s => s.TotalTimeMs).ToList();
        foreach (var stat in sortedStats)
        {
            MethodStats.Add(stat);
        }

        // Reset view scale and offsets
        ZoomScale = 1.0;
        OffsetX = 0.0;
        SelectedBlock = null;
        UpdateSearchMatches();
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
}
