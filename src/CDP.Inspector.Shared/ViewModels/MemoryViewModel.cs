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

namespace CdpInspectorApp.ViewModels;

public class MemoryViewModel : ViewModelBase, IStateProvider
{
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
    private int _snapshotCounter = 1;
    private List<double>? _allocationHistory;

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
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (!_cdpService.IsConnected)
            {
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
            _snapshotCounter = 1;
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
}
