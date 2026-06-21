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

namespace CdpInspectorApp.ViewModels;

public class MemoryViewModel : ViewModelBase
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
    private int _snapshotCounter = 1;

    public ObservableCollection<DetachedControlModel> DetachedControls => _detachedControls;

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

    public ICommand TakeSnapshotCommand { get; }
    public ICommand ClearSnapshotsCommand { get; }
    public ICommand CollectGarbageCommand { get; }
    public ICommand ExportSnapshotCommand { get; }
    public Func<string, Task>? SaveFileCallback { get; set; }

    public MemoryViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        TakeSnapshotCommand = new RelayCommand(async () => await TakeSnapshotAsync(), () => _cdpService.IsConnected);
        ClearSnapshotsCommand = new RelayCommand(ClearSnapshots);
        CollectGarbageCommand = new RelayCommand(async () => await CollectGarbageAsync(), () => _cdpService.IsConnected);
        ExportSnapshotCommand = new RelayCommand(async () => await ExportSnapshotAsync(), () => _cdpService.IsConnected);
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (!_cdpService.IsConnected)
            {
                ClearSnapshots();
            }
            ((RelayCommand)TakeSnapshotCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CollectGarbageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ExportSnapshotCommand).RaiseCanExecuteChanged();
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
            _snapshotCounter = 1;
        });
    }

    private void UpdateDisplayEntries()
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentEntries.Clear();
            DetachedControls.Clear();
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
}
