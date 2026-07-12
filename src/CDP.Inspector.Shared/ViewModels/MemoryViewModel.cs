using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Layout;
using CDP.Editor.Splits.Models;
using System.Text.Json.Serialization;

namespace CdpInspectorApp.ViewModels;

public class MemoryViewModel : ViewModelBase, IStateProvider
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
    private ObservableCollection<MemorySnapshotModel> _snapshots = new();
    private MemorySnapshotModel? _selectedSnapshot;
    private ObservableCollection<MemorySnapshotModel> _comparisonBaselines = new();
    private MemorySnapshotModel? _selectedBaseline;
    private ObservableCollection<ControlCountModel> _currentEntries = new();
    private ObservableCollection<MemoryComparisonModel> _comparisonEntries = new();
    private ObservableCollection<DetachedControlModel> _detachedControls = new();
    private bool _isComparisonMode;
    private DetachedControlModel? _selectedDetachedControl;
    private ObservableCollection<RetainerNodeModel> _retainerRoots = new();
    private ObservableCollection<DominatorNodeModel> _dominatorRoots = new();
    private int _snapshotCounter = 1;
    private List<double>? _allocationHistory;
    private ObservableCollection<MemoryInspectionModel> _inspections = new();
    public ObservableCollection<MemoryInspectionModel> Inspections => _inspections;
    private long _gen0Size;
    private long _gen1Size;
    private long _gen2Size;
    private long _lohSize;
    private readonly DispatcherTimer? _heapInfoTimer;

    public ObservableCollection<DetachedControlModel> DetachedControls => _detachedControls;

    public DetachedControlModel? SelectedDetachedControl
    {
        get => _selectedDetachedControl;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedDetachedControl, value))
            {
                _ = FetchRetainersAsync();
            }
        }
    }

    public ObservableCollection<RetainerNodeModel> RetainerRoots => _retainerRoots;
    public HierarchicalModel<RetainerNodeModel> HierarchicalRetainers { get; }
    public ObservableCollection<DominatorNodeModel> DominatorRoots => _dominatorRoots;
    public HierarchicalModel<DominatorNodeModel> HierarchicalDominators { get; }

    private async Task FetchRetainersAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            RetainerRoots.Clear();
            if (_selectedDetachedControl == null) return;

            try
            {
                var @params = new JsonObject
                {
                    ["hashCode"] = _selectedDetachedControl.HashCode
                };
                var response = await _cdpService.SendCommandAsync("Memory.getRetainers", @params);
                if (response != null)
                {
                    var rootNode = ParseRetainerNode(response);
                    if (rootNode != null)
                    {
                        RetainerRoots.Add(rootNode);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching retainers: {ex.Message}");
            }
        });
    }

    private RetainerNodeModel? ParseRetainerNode(JsonObject obj)
    {
        if (obj == null) return null;

        var name = obj["name"]?.GetValue<string>() ?? "";
        var type = obj["type"]?.GetValue<string>() ?? "";
        var hashCode = obj["hashCode"]?.GetValue<int>() ?? 0;

        var node = new RetainerNodeModel
        {
            Name = name,
            Type = type,
            HashCode = hashCode
        };

        var retainersArray = obj["retainers"] as JsonArray;
        if (retainersArray != null)
        {
            foreach (var item in retainersArray)
            {
                if (item is JsonObject childObj)
                {
                    var childNode = ParseRetainerNode(childObj);
                    if (childNode != null)
                    {
                        node.Retainers.Add(childNode);
                    }
                }
            }
        }

        return node;
    }

    public ObservableCollection<MemorySnapshotModel> Snapshots => _snapshots;

    public MemorySnapshotModel? SelectedSnapshot
    {
        get => _selectedSnapshot;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedSnapshot, value))
            {
                UpdateDisplayEntries();
                UpdateComparisonBaselines();
                UpdateDominatorTree();
                _ = RunInspectionsAsync();
            }
        }
    }

    public ObservableCollection<MemorySnapshotModel> ComparisonBaselines => _comparisonBaselines;

    public MemorySnapshotModel? SelectedBaseline
    {
        get => _selectedBaseline;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedBaseline, value))
            {
                UpdateComparisonEntries();
            }
        }
    }

    public ObservableCollection<ControlCountModel> CurrentEntries => _currentEntries;
    public ObservableCollection<MemoryComparisonModel> ComparisonEntries => _comparisonEntries;

    public bool IsComparisonMode
    {
        get => _isComparisonMode;
        set
        {
            if (RaiseAndSetIfChanged(ref _isComparisonMode, value))
            {
                UpdateDisplayEntries();
            }
        }
    }

    public List<double>? AllocationHistory
    {
        get => _allocationHistory;
        private set => RaiseAndSetIfChanged(ref _allocationHistory, value);
    }

    public long Gen0Size
    {
        get => _gen0Size;
        set => RaiseAndSetIfChanged(ref _gen0Size, value);
    }

    public long Gen1Size
    {
        get => _gen1Size;
        set => RaiseAndSetIfChanged(ref _gen1Size, value);
    }

    public long Gen2Size
    {
        get => _gen2Size;
        set => RaiseAndSetIfChanged(ref _gen2Size, value);
    }

    public long LohSize
    {
        get => _lohSize;
        set => RaiseAndSetIfChanged(ref _lohSize, value);
    }

    public ICommand TakeSnapshotCommand { get; }
    public ICommand ClearSnapshotsCommand { get; }
    public ICommand CollectGarbageCommand { get; }
    public ICommand ExportSnapshotCommand { get; }
    public Func<string, Task>? SaveFileCallback { get; set; }

    public MemoryViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;
        _cdpService.EventReceived += CdpService_EventReceived;

        TakeSnapshotCommand = new RelayCommand(async () => await TakeSnapshotAsync(), () => _cdpService.IsConnected);
        ClearSnapshotsCommand = new RelayCommand(ClearSnapshots);
        CollectGarbageCommand = new RelayCommand(async () => await CollectGarbageAsync(), () => _cdpService.IsConnected);
        ExportSnapshotCommand = new RelayCommand(async () => await ExportSnapshotAsync(), () => _cdpService.IsConnected);

        var retainerOptions = new HierarchicalOptions<RetainerNodeModel>
        {
            ChildrenSelector = node => node.Retainers,
            IsLeafSelector = node => node.Retainers == null || node.Retainers.Count == 0,
            AutoExpandRoot = true
        };
        HierarchicalRetainers = new HierarchicalModel<RetainerNodeModel>(retainerOptions);
        HierarchicalRetainers.SetRoots(RetainerRoots);

        var dominatorOptions = new HierarchicalOptions<DominatorNodeModel>
        {
            ChildrenSelector = node => node.Children,
            IsLeafSelector = node => node.Children == null || node.Children.Count == 0,
            AutoExpandRoot = true
        };
        HierarchicalDominators = new HierarchicalModel<DominatorNodeModel>(dominatorOptions);
        HierarchicalDominators.SetRoots(DominatorRoots);

        ResetLayout();

        _heapInfoTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };
        _heapInfoTimer.Tick += async (sender, e) => await UpdateHeapInfoAsync();

        if (_cdpService.IsConnected)
        {
            _heapInfoTimer.Start();
            _ = UpdateHeapInfoAsync();
        }
    }

    public void ResetLayout()
    {
        var left = new BoxNode();
        left.AddTab("Snapshots List", "TableIcon", "SnapshotsList");

        var right = new BoxNode();
        right.AddTab("Snapshot Overview", "CodeIcon", "SnapshotOverview");
        right.AddTab("Detached Controls", "DocumentIcon", "DetachedControls");
        right.AddTab("Dominator Tree", "FlowchartIcon", "DominatorTree");
        right.AddTab("Inspections", "WarningIcon", "InspectionsPanel");

        LayoutRoot = new SplitContainerNode(Orientation.Horizontal, left, right) { SplitterRatio = 0.35 };
        SelectedPane = left;
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService.IsConnected)
            {
                _heapInfoTimer?.Start();
                _ = UpdateHeapInfoAsync();
            }
            else
            {
                _heapInfoTimer?.Stop();
                ClearData();
            }
            ((RelayCommand)TakeSnapshotCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CollectGarbageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ExportSnapshotCommand).RaiseCanExecuteChanged();
        }
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (e.Method == "Performance.metrics" && e.Params != null)
        {
            var metrics = e.Params["metrics"] as JsonArray;
            if (metrics != null)
            {
                Dispatcher.UIThread.Post(() => UpdateMetrics(metrics));
            }
        }
    }

    private void UpdateMetrics(JsonArray metrics)
    {
        var newHistory = AllocationHistory != null ? new List<double>(AllocationHistory) : new List<double>();
        bool updated = false;
        foreach (var m in metrics)
        {
            string name = m?["name"]?.GetValue<string>() ?? "";
            double val = GetDouble(m?["value"]);
            if (name == "MemoryAllocations")
            {
                newHistory.Add(val);
                if (newHistory.Count > 30) newHistory.RemoveAt(0);
                updated = true;
            }
        }
        if (updated)
        {
            AllocationHistory = newHistory;
        }
    }

    public async Task TakeSnapshotAsync()
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            var response = await _cdpService.SendCommandAsync("Memory.getLiveControls");
            var detachedResponse = await _cdpService.SendCommandAsync("Memory.getDetachedControls");

            var entries = new List<ControlCountModel>();
            if (response != null)
            {
                var controls = response["controls"] as JsonArray;
                if (controls != null)
                {
                    foreach (var node in controls)
                    {
                        if (node is JsonObject obj)
                        {
                            entries.Add(new ControlCountModel
                            {
                                Type = obj["type"]?.GetValue<string>() ?? "",
                                Count = obj["count"]?.GetValue<int>() ?? 0
                            });
                        }
                    }
                }
            }

            var detachedEntries = new List<DetachedControlModel>();
            if (detachedResponse != null)
            {
                var controls = detachedResponse["detachedControls"] as JsonArray;
                if (controls != null)
                {
                    foreach (var node in controls)
                    {
                        if (node is JsonObject obj)
                        {
                            detachedEntries.Add(new DetachedControlModel
                            {
                                Id = obj["id"]?.GetValue<string>() ?? "",
                                Type = obj["type"]?.GetValue<string>() ?? "",
                                Name = obj["name"]?.GetValue<string>() ?? "",
                                HashCode = obj["hashCode"]?.GetValue<int>() ?? 0,
                                DetachedDurationMs = obj["detachedDurationMs"]?.GetValue<long>() ?? 0,
                                HasDataContext = obj["hasDataContext"]?.GetValue<bool>() ?? false,
                                DataContextType = obj["dataContextType"]?.GetValue<string>() ?? ""
                            });
                        }
                    }
                }
            }

            // Sort by count desc
            entries = entries.OrderByDescending(e => e.Count).ToList();

            var snapshot = new MemorySnapshotModel
            {
                Name = $"Snapshot {_snapshotCounter++}",
                Timestamp = DateTime.Now,
                Entries = entries,
                DetachedEntries = detachedEntries
            };

            Dispatcher.UIThread.Post(() =>
            {
                Snapshots.Add(snapshot);
                SelectedSnapshot = snapshot;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error taking heap snapshot: {ex.Message}");
        }
    }

    public async Task ExportSnapshotAsync()
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            var response = await _cdpService.SendCommandAsync("Memory.takeHeapSnapshot");
            if (response != null && SaveFileCallback != null)
            {
                string snapshotJson = response.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await SaveFileCallback(snapshotJson);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting heap snapshot: {ex.Message}");
        }
    }

    public async Task CollectGarbageAsync()
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            await _cdpService.SendCommandAsync("Memory.collectGarbage");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GC failed: {ex.Message}");
        }
    }

    private void ClearSnapshots()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Snapshots.Clear();
            SelectedSnapshot = null;
            ComparisonBaselines.Clear();
            SelectedBaseline = null;
            CurrentEntries.Clear();
            ComparisonEntries.Clear();
            DetachedControls.Clear();
            SelectedDetachedControl = null;
            RetainerRoots.Clear();
            DominatorRoots.Clear();
            _snapshotCounter = 1;
        });
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            AllocationHistory = null;
            Snapshots.Clear();
            SelectedSnapshot = null;
            ComparisonBaselines.Clear();
            SelectedBaseline = null;
            CurrentEntries.Clear();
            ComparisonEntries.Clear();
            DetachedControls.Clear();
            SelectedDetachedControl = null;
            RetainerRoots.Clear();
            DominatorRoots.Clear();
            _snapshotCounter = 1;
            Gen0Size = 0;
            Gen1Size = 0;
            Gen2Size = 0;
            LohSize = 0;
        });
    }

    private void UpdateDisplayEntries()
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentEntries.Clear();
            DetachedControls.Clear();
            SelectedDetachedControl = null;
            RetainerRoots.Clear();
            if (SelectedSnapshot != null && !IsComparisonMode)
            {
                foreach (var entry in SelectedSnapshot.Entries)
                {
                    CurrentEntries.Add(entry);
                }
                foreach (var entry in SelectedSnapshot.DetachedEntries)
                {
                    DetachedControls.Add(entry);
                }
            }
        });
    }

    private void UpdateComparisonBaselines()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ComparisonBaselines.Clear();
            if (SelectedSnapshot != null)
            {
                // Baseline can be any snapshot created before the selected one
                int selectedIndex = Snapshots.IndexOf(SelectedSnapshot);
                for (int i = 0; i < selectedIndex; i++)
                {
                    ComparisonBaselines.Add(Snapshots[i]);
                }
                
                if (ComparisonBaselines.Count > 0)
                {
                    SelectedBaseline = ComparisonBaselines.Last();
                }
                else
                {
                    SelectedBaseline = null;
                }
            }
            else
            {
                SelectedBaseline = null;
            }
        });
    }

    private void UpdateComparisonEntries()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ComparisonEntries.Clear();
            if (SelectedSnapshot == null || SelectedBaseline == null) return;

            var currentDict = SelectedSnapshot.Entries.ToDictionary(e => e.Type, e => e.Count);
            var baselineDict = SelectedBaseline.Entries.ToDictionary(e => e.Type, e => e.Count);

            var allTypes = currentDict.Keys.Union(baselineDict.Keys).OrderBy(t => t);

            foreach (var type in allTypes)
            {
                int currentVal = currentDict.TryGetValue(type, out int c) ? c : 0;
                int baselineVal = baselineDict.TryGetValue(type, out int b) ? b : 0;

                if (currentVal != baselineVal) // only show delta differences, typical of heap diffing
                {
                    ComparisonEntries.Add(new MemoryComparisonModel
                    {
                        Type = type,
                        BaselineCount = baselineVal,
                        SnapshotCount = currentVal
                    });
                }
            }
        });
    }

    #region IStateProvider Implementation

    public string StateKey => "memory";

    public JsonNode? SaveState()
    {
        var root = new JsonObject();
        root["isComparisonMode"] = IsComparisonMode;
        return root;
    }

    public void LoadState(JsonNode? stateNode)
    {
        if (stateNode is not JsonObject json) return;

        if (json.TryGetPropertyValue("isComparisonMode", out var compNode) && compNode != null)
        {
            IsComparisonMode = (bool?)compNode ?? false;
        }
    }

    #endregion

    private static double GetDouble(JsonNode? node)
    {
        if (node == null) return 0.0;
        if (node is JsonValue jsonVal)
        {
            if (jsonVal.TryGetValue<double>(out double d)) return d;
            if (jsonVal.TryGetValue<int>(out int i)) return i;
            if (jsonVal.TryGetValue<long>(out long l)) return l;
            if (jsonVal.TryGetValue<float>(out float f)) return f;
        }
        return 0.0;
    }

<<<<<<< HEAD
    private void UpdateDominatorTree()
    {
        Dispatcher.UIThread.Post(() =>
        {
            DominatorRoots.Clear();
            var entries = SelectedSnapshot?.Entries;
            if (entries == null || entries.Count == 0) return;

            long totalSize = 0;
            var leafNodes = new List<(DominatorNodeModel Node, string Category)>();

            foreach (var entry in entries)
            {
                long bytesPerInstance = 64;
                string category = "Other Allocations";

                string typeLower = entry.Type.ToLowerInvariant();
                if (typeLower.Contains("window") || typeLower.Contains("toplevel"))
                {
                    bytesPerInstance = 2048;
                    category = "Windows & Roots";
                }
                else if (typeLower.Contains("panel") || typeLower.Contains("grid") || typeLower.Contains("stackpanel") || 
                         typeLower.Contains("wrappanel") || typeLower.Contains("dockpanel") || typeLower.Contains("canvas") || 
                         typeLower.Contains("scrollviewer"))
                {
                    bytesPerInstance = 512;
                    category = "Layout Panels";
                }
                else if (typeLower.Contains("button") || typeLower.Contains("border") || typeLower.Contains("contentcontrol") || 
                         typeLower.Contains("template") || typeLower.Contains("presenter"))
                {
                    bytesPerInstance = 256;
                    category = "Content Controls";
                }
                else if (typeLower.Contains("text") || typeLower.Contains("textbox") || typeLower.Contains("textblock") || 
                         typeLower.Contains("label") || typeLower.Contains("input"))
                {
                    bytesPerInstance = 128;
                    category = "Text & Input Controls";
                }

                long size = entry.Count * bytesPerInstance;
                totalSize += size;

                var node = new DominatorNodeModel
                {
                    TypeName = entry.Type,
                    InstanceCount = entry.Count,
                    RetainedSize = size,
                    RetainedPct = 0.0
                };
                leafNodes.Add((node, category));
            }

            if (totalSize == 0) totalSize = 1;

            var categories = new Dictionary<string, DominatorNodeModel>();
            foreach (var item in leafNodes)
            {
                item.Node.RetainedPct = (double)item.Node.RetainedSize / totalSize * 100.0;
                if (!categories.TryGetValue(item.Category, out var catNode))
                {
                    catNode = new DominatorNodeModel
                    {
                        TypeName = item.Category,
                        RetainedSize = 0,
                        RetainedPct = 0.0,
                        InstanceCount = 0,
                        Children = new List<DominatorNodeModel>()
                    };
                    categories[item.Category] = catNode;
                }
                catNode.Children.Add(item.Node);
                catNode.RetainedSize += item.Node.RetainedSize;
                catNode.InstanceCount += item.Node.InstanceCount;
            }

            var root = new DominatorNodeModel
            {
                TypeName = "GC Roots",
                RetainedSize = totalSize,
                RetainedPct = 100.0,
                InstanceCount = 0,
                Children = new List<DominatorNodeModel>()
            };

            foreach (var cat in categories.Values.OrderByDescending(c => c.RetainedSize))
            {
                cat.RetainedPct = (double)cat.RetainedSize / totalSize * 100.0;
                cat.Children = cat.Children.OrderByDescending(c => c.RetainedSize).ToList();
                root.Children.Add(cat);
                root.InstanceCount += cat.InstanceCount;
            }

            DominatorRoots.Add(root);
        });
    }

    private async Task RunInspectionsAsync()
    {
        var localSnapshot = SelectedSnapshot;
        if (localSnapshot == null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Inspections.Clear());
            return;
        }

        var results = await AuditSnapshotAsync(localSnapshot);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Inspections.Clear();
            foreach (var res in results)
            {
                Inspections.Add(res);
            }
        });
    }

    private async Task<List<MemoryInspectionModel>> AuditSnapshotAsync(MemorySnapshotModel snapshot)
    {
        var list = new List<MemoryInspectionModel>();

        // 1. Event Leaks / Detached Controls Audit
        if (snapshot.DetachedEntries != null && snapshot.DetachedEntries.Count > 0)
        {
            var groupedDetached = snapshot.DetachedEntries.GroupBy(e => e.Type).ToList();
            foreach (var group in groupedDetached)
            {
                int count = group.Count();
                var dataContexts = group.Where(e => e.HasDataContext && !string.IsNullOrEmpty(e.DataContextType))
                                       .Select(e => e.DataContextType)
                                       .Distinct()
                                       .ToList();
                
                string details = $"Found {count} detached visual control instance(s) of '{group.Key}' still held in memory.";
                if (dataContexts.Count > 0)
                {
                    details += $" Warning: Detached controls are holding references to DataContext view models: {string.Join(", ", dataContexts)}. This is a classic event handler or subscription leak.";
                }
                else
                {
                    details += " These detached controls might be kept alive by strong event handlers or direct reference leaks.";
                }

                list.Add(new MemoryInspectionModel
                {
                    Title = "Event Leak: Detached Control",
                    Description = $"Detached control '{group.Key.Split('.').Last()}' is still alive in memory.",
                    Severity = "Error",
                    Count = count,
                    Details = details
                });
            }
        }

        // 2. Large Structure / Visual Bloating Audit
        if (snapshot.Entries != null)
        {
            var largeEntries = snapshot.Entries.Where(e => e.Count > 30).ToList();
            foreach (var entry in largeEntries)
            {
                list.Add(new MemoryInspectionModel
                {
                    Title = "Large Structure / Visual Bloating",
                    Description = $"High count of '{entry.Type}' ({entry.Count} instances).",
                    Severity = "Warning",
                    Count = entry.Count,
                    Details = $"The visual tree contains {entry.Count} instances of '{entry.Type}'. A high number of UI elements degrades layout and rendering performance. Consider enabling DataGrid virtualization or optimizing your layout panels."
                });
            }
        }

        // 3. Duplicate String Value Audit
        if (_cdpService.IsConnected)
        {
            try
            {
                var evalScript = @"
                    (function() {
                        var all = document.querySelectorAll('*');
                        var map = {};
                        for (var i = 0; i < all.length; i++) {
                            var txt = all[i].textContent || all[i].text;
                            if (txt && txt.trim().length > 0) {
                                var key = txt.trim();
                                map[key] = (map[key] || 0) + 1;
                            }
                        }
                        var result = [];
                        for (var k in map) {
                            if (map[k] > 5) {
                                result.push({ text: k.substring(0, 50), count: map[k] });
                            }
                        }
                        result.sort(function(a, b) { return b.count - a.count; });
                        return JSON.stringify(result.slice(0, 10));
                    })()";

                var @params = new JsonObject
                {
                    ["expression"] = evalScript,
                    ["returnByValue"] = true
                };

                var evalResult = await _cdpService.SendCommandAsync("Runtime.evaluate", @params);
                if (evalResult != null && evalResult.TryGetPropertyValue("result", out var resNode) && resNode is JsonObject resObj)
                {
                    if (resObj.TryGetPropertyValue("value", out var valNode) && valNode != null)
                    {
                        var jsonString = valNode.GetValue<string>();
                        if (!string.IsNullOrEmpty(jsonString))
                        {
                            var duplicates = System.Text.Json.JsonSerializer.Deserialize<List<StringDuplicateDto>>(jsonString);
                            if (duplicates != null && duplicates.Count > 0)
                            {
                                foreach (var dup in duplicates)
                                {
                                    list.Add(new MemoryInspectionModel
                                    {
                                        Title = "Duplicate String Value",
                                        Description = $"String '{dup.Text}' duplicated {dup.Count} times in visual tree.",
                                        Severity = "Warning",
                                        Count = dup.Count,
                                        Details = $"The string value '{dup.Text}' appears {dup.Count} times in visual elements text properties. If these are static text headers or labels, consider resource/style reuse or localized resource lookup to reduce heap allocations."
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running duplicate strings audit: {ex.Message}");
            }
        }

        return list;
    }

    private async Task UpdateHeapInfoAsync()
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            var response = await _cdpService.SendCommandAsync("Memory.getHeapInfo");
            if (response != null)
            {
                long gen0 = GetLong(response["gen0Size"]);
                long gen1 = GetLong(response["gen1Size"]);
                long gen2 = GetLong(response["gen2Size"]);
                long loh = GetLong(response["lohSize"]);

                Dispatcher.UIThread.Post(() =>
                {
                    Gen0Size = gen0;
                    Gen1Size = gen1;
                    Gen2Size = gen2;
                    LohSize = loh;
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching heap info: {ex.Message}");
        }
    }

    private static long GetLong(JsonNode? node)
    {
        if (node == null) return 0;
        if (node is JsonValue jsonVal)
        {
            if (jsonVal.TryGetValue<long>(out long l)) return l;
            if (jsonVal.TryGetValue<int>(out int i)) return i;
            if (jsonVal.TryGetValue<double>(out double d)) return (long)d;
        }
        return 0;
    }
}

namespace CdpInspectorApp.Models
{
    public class DominatorNodeModel
    {
        public string TypeName { get; set; } = "";
        public long RetainedSize { get; set; }
        public double RetainedPct { get; set; }
        public int InstanceCount { get; set; }
        public List<DominatorNodeModel> Children { get; set; } = new();
    }
}

public class MemoryInspectionModel
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Severity { get; set; } = "Warning";
    public int Count { get; set; }
    public string Details { get; set; } = "";

    public string SeverityColor
    {
        get
        {
            if (Severity == "Error") return "#f28b82"; // Red
            if (Severity == "Warning") return "#fdd663"; // Yellow
            return "#8ab4f8"; // Blue
        }
    }
}

public class StringDuplicateDto
{
    [System.Text.Json.Serialization.JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("count")]
    public int Count { get; set; }
}
