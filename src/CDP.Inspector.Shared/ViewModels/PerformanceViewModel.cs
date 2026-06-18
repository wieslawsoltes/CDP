using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public class PerformanceViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private string _perfNodesText = "--";
    private string _perfMemoryText = "--";
    private string _perfGcText = "--";
    private string _perfDocumentsText = "--";
    private string _perfPidText = "--";
    private string _perfOsText = "--";
    private ObservableCollection<ControlCountModel> _liveControls = new();
    private List<double>? _memoryHistory = new();

    public string PerfNodesText
    {
        get => _perfNodesText;
        private set => RaiseAndSetIfChanged(ref _perfNodesText, value);
    }

    public string PerfMemoryText
    {
        get => _perfMemoryText;
        private set => RaiseAndSetIfChanged(ref _perfMemoryText, value);
    }

    public string PerfGcText
    {
        get => _perfGcText;
        private set => RaiseAndSetIfChanged(ref _perfGcText, value);
    }

    public string PerfDocumentsText
    {
        get => _perfDocumentsText;
        private set => RaiseAndSetIfChanged(ref _perfDocumentsText, value);
    }

    public string PerfPidText
    {
        get => _perfPidText;
        private set => RaiseAndSetIfChanged(ref _perfPidText, value);
    }

    public string PerfOsText
    {
        get => _perfOsText;
        private set => RaiseAndSetIfChanged(ref _perfOsText, value);
    }

    public ObservableCollection<ControlCountModel> LiveControls => _liveControls;

    public List<double>? MemoryHistory
    {
        get => _memoryHistory;
        private set => RaiseAndSetIfChanged(ref _memoryHistory, value);
    }

    public ICommand RefreshMetricsCommand { get; }
    public ICommand CollectGarbageCommand { get; }
    public ICommand CloseTargetCommand { get; }

    public PerformanceViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;

        RefreshMetricsCommand = new RelayCommand(async () => await RefreshMetricsAsync(), () => _cdpService.IsConnected);
        CollectGarbageCommand = new RelayCommand(async () => await CollectGarbageAsync(), () => _cdpService.IsConnected);
        CloseTargetCommand = new RelayCommand(async () => await CloseTargetAsync(), () => _cdpService.IsConnected);
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService.IsConnected)
            {
                _ = InitializePerformanceAsync();
            }
            else
            {
                ClearData();
            }
            ((RelayCommand)RefreshMetricsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CollectGarbageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CloseTargetCommand).RaiseCanExecuteChanged();
        }
    }

    private async Task InitializePerformanceAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("Performance.enable");
            await _cdpService.SendCommandAsync("Memory.enable");
            await RefreshMetricsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enabling performance domains: {ex.Message}");
        }
    }

    public async Task RefreshMetricsAsync()
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            var perfRes = await _cdpService.SendCommandAsync("Performance.getMetrics");
            var metrics = perfRes["metrics"] as JsonArray;
            if (metrics != null)
            {
                var newHistory = MemoryHistory != null ? new List<double>(MemoryHistory) : new List<double>();
                foreach (var m in metrics)
                {
                    string name = m?["name"]?.GetValue<string>() ?? "";
                    double val = m?["value"]?.GetValue<double>() ?? 0;
                    if (name == "Nodes")
                    {
                        PerfNodesText = val.ToString("0");
                    }
                    else if (name == "JSHeapUsedSize")
                    {
                        PerfMemoryText = $"{(val / 1024 / 1024):F2} MB";
                        newHistory.Add(val / 1024 / 1024);
                        if (newHistory.Count > 30) newHistory.RemoveAt(0);
                    }
                    else if (name == "JSHeapTotalSize")
                    {
                        PerfGcText = $"{(val / 1024 / 1024):F2} MB";
                    }
                }
                MemoryHistory = newHistory;
            }

            try
            {
                var memRes = await _cdpService.SendCommandAsync("Memory.getDOMCounters");
                int docs = memRes["documents"]?.GetValue<int>() ?? 0;
                PerfDocumentsText = docs.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Memory counters failed: {ex.Message}");
            }

            try
            {
                var sysRes = await _cdpService.SendCommandAsync("SystemInfo.getProcessInfo");
                var processInfo = sysRes["processInfo"] as JsonArray;
                if (processInfo != null && processInfo.Count > 0)
                {
                    var proc = processInfo[0] as JsonObject;
                    int pid = proc?["id"]?.GetValue<int>() ?? 0;
                    PerfPidText = pid.ToString();
                }
            }
            catch { }

            try
            {
                var infoRes = await _cdpService.SendCommandAsync("SystemInfo.getInfo");
                PerfOsText = $"{infoRes["modelName"]?.GetValue<string>()} {infoRes["modelVersion"]?.GetValue<string>()}";
            }
            catch { }

            // Query live visual controls
            try
            {
                var liveRes = await _cdpService.SendCommandAsync("Memory.getLiveControls");
                var controls = liveRes["controls"] as JsonArray;
                if (controls != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        LiveControls.Clear();
                        foreach (var cNode in controls)
                        {
                            if (cNode is JsonObject cObj)
                            {
                                string type = cObj["type"]?.GetValue<string>() ?? "";
                                int count = cObj["count"]?.GetValue<int>() ?? 0;
                                LiveControls.Add(new ControlCountModel { Type = type, Count = count });
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Live controls failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Refresh metrics failed: {ex.Message}");
        }
    }

    public async Task CollectGarbageAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("Memory.collectGarbage");
            await RefreshMetricsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error collecting garbage: {ex.Message}");
        }
    }

    public async Task CloseTargetAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("Browser.close");
            await _cdpService.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Target shutdown failed: {ex.Message}");
        }
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            PerfNodesText = "--";
            PerfMemoryText = "--";
            PerfGcText = "--";
            PerfDocumentsText = "--";
            PerfPidText = "--";
            PerfOsText = "--";
            LiveControls.Clear();
            MemoryHistory = null;
        });
    }
}
