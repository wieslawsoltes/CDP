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
    private string _perfCpuText = "--";
    private string _perfFpsText = "--";
    private string _perfFrameDurationText = "--";
    private string _perfLayoutCountText = "--";
    private string _perfLayoutDurationText = "--";
    private string _perfQueueDelayText = "--";
    private string _perfBlockingTimeText = "--";
    private ObservableCollection<ControlCountModel> _liveControls = new();
    private List<double>? _memoryHistory = new();
    private List<double>? _cpuHistory = new();
    private List<double>? _fpsHistory = new();

    private double _latestCpuUsage = 0.0;
    private double _latestLayoutDuration = 0.0;
    private double _latestFrameDuration = 0.0;
    private double _latestDispatcherQueueDelay = 0.0;

    private double _cpuScripting = 0.0;
    private double _cpuRendering = 0.0;
    private double _cpuLayout = 0.0;
    private double _cpuSystem = 0.0;
    private double _cpuIdle = 100.0;


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

    public string PerfCpuText
    {
        get => _perfCpuText;
        private set => RaiseAndSetIfChanged(ref _perfCpuText, value);
    }

    public string PerfFpsText
    {
        get => _perfFpsText;
        private set => RaiseAndSetIfChanged(ref _perfFpsText, value);
    }

    public string PerfFrameDurationText
    {
        get => _perfFrameDurationText;
        private set => RaiseAndSetIfChanged(ref _perfFrameDurationText, value);
    }

    public string PerfLayoutCountText
    {
        get => _perfLayoutCountText;
        private set => RaiseAndSetIfChanged(ref _perfLayoutCountText, value);
    }

    public string PerfLayoutDurationText
    {
        get => _perfLayoutDurationText;
        private set => RaiseAndSetIfChanged(ref _perfLayoutDurationText, value);
    }

    public string PerfQueueDelayText
    {
        get => _perfQueueDelayText;
        private set => RaiseAndSetIfChanged(ref _perfQueueDelayText, value);
    }

    public string PerfBlockingTimeText
    {
        get => _perfBlockingTimeText;
        private set => RaiseAndSetIfChanged(ref _perfBlockingTimeText, value);
    }

    public ObservableCollection<ControlCountModel> LiveControls => _liveControls;

    public List<double>? MemoryHistory
    {
        get => _memoryHistory;
        private set => RaiseAndSetIfChanged(ref _memoryHistory, value);
    }

    public List<double>? CpuHistory
    {
        get => _cpuHistory;
        private set => RaiseAndSetIfChanged(ref _cpuHistory, value);
    }

    public List<double>? FpsHistory
    {
        get => _fpsHistory;
        private set => RaiseAndSetIfChanged(ref _fpsHistory, value);
    }

    public double CpuScripting
    {
        get => _cpuScripting;
        private set => RaiseAndSetIfChanged(ref _cpuScripting, value);
    }

    public double CpuRendering
    {
        get => _cpuRendering;
        private set => RaiseAndSetIfChanged(ref _cpuRendering, value);
    }

    public double CpuLayout
    {
        get => _cpuLayout;
        private set => RaiseAndSetIfChanged(ref _cpuLayout, value);
    }

    public double CpuSystem
    {
        get => _cpuSystem;
        private set => RaiseAndSetIfChanged(ref _cpuSystem, value);
    }

    public double CpuIdle
    {
        get => _cpuIdle;
        private set => RaiseAndSetIfChanged(ref _cpuIdle, value);
    }


    public ICommand RefreshMetricsCommand { get; }
    public ICommand CollectGarbageCommand { get; }
    public ICommand CloseTargetCommand { get; }

    public PerformanceViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;
        _cdpService.EventReceived += CdpService_EventReceived;

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

    private void UpdateMetrics(JsonArray metrics)
    {
        var newHistory = MemoryHistory != null ? new List<double>(MemoryHistory) : new List<double>();
        var newHistoryCpu = CpuHistory != null ? new List<double>(CpuHistory) : new List<double>();
        var newHistoryFps = FpsHistory != null ? new List<double>(FpsHistory) : new List<double>();

        double cpuUsage = _latestCpuUsage;
        double layoutDuration = _latestLayoutDuration;
        double frameDuration = _latestFrameDuration;
        double queueDelay = _latestDispatcherQueueDelay;

        foreach (var m in metrics)
        {
            string name = m?["name"]?.GetValue<string>() ?? "";
            double val = GetDouble(m?["value"]);
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
            else if (name == "CPUUsage")
            {
                cpuUsage = val;
                PerfCpuText = $"{val:F1} %";
                newHistoryCpu.Add(val);
                if (newHistoryCpu.Count > 30) newHistoryCpu.RemoveAt(0);
            }
            else if (name == "LayoutCount")
            {
                PerfLayoutCountText = val.ToString("0");
            }
            else if (name == "LayoutDuration")
            {
                layoutDuration = val;
                PerfLayoutDurationText = $"{val:F3} s";
            }
            else if (name == "FPS")
            {
                PerfFpsText = $"{val:F1} FPS";
                newHistoryFps.Add(val);
                if (newHistoryFps.Count > 30) newHistoryFps.RemoveAt(0);
            }
            else if (name == "FrameDuration")
            {
                frameDuration = val;
                PerfFrameDurationText = $"{val:F3} s";
            }
            else if (name == "DispatcherQueueDelay")
            {
                queueDelay = val;
                PerfQueueDelayText = $"{val:F3} s";
            }
            else if (name == "UIThreadBlockingTime")
            {
                PerfBlockingTimeText = $"{val:F3} s";
            }
        }
        MemoryHistory = newHistory;
        CpuHistory = newHistoryCpu;
        FpsHistory = newHistoryFps;

        _latestCpuUsage = cpuUsage;
        _latestLayoutDuration = layoutDuration;
        _latestFrameDuration = frameDuration;
        _latestDispatcherQueueDelay = queueDelay;

        double activeCpu = Math.Max(0.0, cpuUsage);
        double cpuIdle = Math.Max(0.0, 100.0 - activeCpu);

        double layoutPct = layoutDuration * 100.0;
        double renderingPct = Math.Max(0.0, frameDuration - layoutDuration) * 100.0;
        double scriptingPct = queueDelay * 100.0;

        double sumActive = scriptingPct + layoutPct + renderingPct;
        if (sumActive > activeCpu && sumActive > 0)
        {
            double scale = activeCpu / sumActive;
            scriptingPct *= scale;
            layoutPct *= scale;
            renderingPct *= scale;
        }

        double systemPct = Math.Max(0.0, activeCpu - (scriptingPct + layoutPct + renderingPct));

        CpuIdle = cpuIdle;
        CpuScripting = scriptingPct;
        CpuRendering = renderingPct;
        CpuLayout = layoutPct;
        CpuSystem = systemPct;
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
                UpdateMetrics(metrics);
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
            PerfCpuText = "--";
            PerfFpsText = "--";
            PerfFrameDurationText = "--";
            PerfLayoutCountText = "--";
            PerfLayoutDurationText = "--";
            PerfQueueDelayText = "--";
            PerfBlockingTimeText = "--";
            LiveControls.Clear();
            MemoryHistory = null;
            CpuHistory = null;
            FpsHistory = null;
            CpuScripting = 0.0;
            CpuRendering = 0.0;
            CpuLayout = 0.0;
            CpuSystem = 0.0;
            CpuIdle = 100.0;
            _latestCpuUsage = 0.0;
            _latestLayoutDuration = 0.0;
            _latestFrameDuration = 0.0;
            _latestDispatcherQueueDelay = 0.0;
        });
    }

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
