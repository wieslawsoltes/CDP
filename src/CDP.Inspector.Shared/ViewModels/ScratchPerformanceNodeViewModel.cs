#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using CDP.Editor.Nodes.ViewModels;
using Chrome.DevTools.Protocol;
using CdpInspectorApp.Services;
using Microsoft.Extensions.Logging;

namespace CdpInspectorApp.ViewModels;

public class ScratchPerformanceNodeViewModel : ScratchNodeViewModelBase, IImportExportNode
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<ScratchPerformanceNodeViewModel>();
    private readonly ICdpService? _cdpService;
    private double _cpuUsage;
    private double _memoryUsage;
    private int _layoutCount;
    private string _rawJsonData = "";
    private DateTime? _timestamp;
    private bool _isCapturing;
    private string _dataSummary = "Empty";
    private bool _isDisposed;

    public double CpuUsage
    {
        get => _cpuUsage;
        set
        {
            if (RaiseAndSetIfChanged(ref _cpuUsage, value))
            {
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public double MemoryUsage
    {
        get => _memoryUsage;
        set
        {
            if (RaiseAndSetIfChanged(ref _memoryUsage, value))
            {
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public int LayoutCount
    {
        get => _layoutCount;
        set
        {
            if (RaiseAndSetIfChanged(ref _layoutCount, value))
            {
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public string RawJsonData
    {
        get => _rawJsonData;
        set
        {
            if (RaiseAndSetIfChanged(ref _rawJsonData, value))
            {
                UpdateSummary();
                OnPropertyChanged(nameof(PerformanceMetrics));
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public DateTime? Timestamp
    {
        get => _timestamp;
        set => RaiseAndSetIfChanged(ref _timestamp, value);
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        set => RaiseAndSetIfChanged(ref _isCapturing, value);
    }

    public string DataSummary
    {
        get => _dataSummary;
        private set => RaiseAndSetIfChanged(ref _dataSummary, value);
    }

    public override string OutputJson
    {
        get
        {
            var obj = new JsonObject
            {
                ["cpuUsage"] = CpuUsage,
                ["memoryUsage"] = MemoryUsage,
                ["layoutCount"] = LayoutCount,
                ["timestamp"] = Timestamp?.ToString("o"),
                ["rawJsonData"] = RawJsonData
            };
            return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public override JsonNode? OutputJsonNode => new JsonObject
    {
        ["cpuUsage"] = CpuUsage,
        ["memoryUsage"] = MemoryUsage,
        ["layoutCount"] = LayoutCount,
        ["timestamp"] = Timestamp?.ToString("o"),
        ["rawJsonData"] = RawJsonData
    };

    public List<SemanticPerformanceMetric> PerformanceMetrics
    {
        get
        {
            var list = new List<SemanticPerformanceMetric>();
            if (string.IsNullOrEmpty(RawJsonData)) return list;
            try
            {
                var node = JsonNode.Parse(RawJsonData);
                if (node is JsonObject obj && obj.TryGetPropertyValue("metrics", out var metricsNode) && metricsNode is JsonArray array)
                {
                    foreach (var item in array)
                    {
                        if (item is JsonObject mObj)
                        {
                            list.Add(new SemanticPerformanceMetric
                            {
                                Name = (string?)mObj["name"] ?? "",
                                Value = (double?)mObj["value"] ?? 0.0
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }
    }

    public ICommand CaptureCommand { get; }
    public ICommand ImportPayloadCommand { get; }
    public ICommand ExportPayloadCommand { get; }

    public Func<Task<string?>>? PayloadImportHandler { get; set; }
    public Func<Task>? PayloadExportHandler { get; set; }

    public ScratchPerformanceNodeViewModel() : this(null)
    {
    }

    public ScratchPerformanceNodeViewModel(ICdpService? cdpService)
    {
        _cdpService = cdpService;
        TitleBackground = Avalonia.Media.Brush.Parse("#8e24aa");
        BorderBrush = Avalonia.Media.Brush.Parse("#ab47bc");

        AddOutputPin("data", "Perf Data");

        CaptureCommand = new RelayCommand(async () => await CaptureDataAsync());

        ImportPayloadCommand = new RelayCommand(async () =>
        {
            if (PayloadImportHandler != null)
            {
                var content = await PayloadImportHandler();
                if (content != null)
                {
                    RawJsonData = content;
                    Timestamp = DateTime.Now;
                }
            }
        });

        ExportPayloadCommand = new RelayCommand(async () =>
        {
            if (PayloadExportHandler != null && !string.IsNullOrEmpty(RawJsonData))
            {
                await PayloadExportHandler();
            }
        });

        if (_cdpService != null)
        {
            _cdpService.PropertyChanged += CdpService_PropertyChanged;
            _cdpService.EventReceived += CdpService_EventReceived;

            if (_cdpService.IsConnected)
            {
                _ = InitializePerformanceAsync();
            }
        }
    }

    private void CdpService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService != null && _cdpService.IsConnected)
            {
                _ = InitializePerformanceAsync();
            }
        }
    }

    private async Task InitializePerformanceAsync()
    {
        if (_cdpService == null) return;
        try
        {
            await _cdpService.SendCommandAsync("Performance.enable");
            await _cdpService.SendCommandAsync("Memory.enable");

            var perfRes = await _cdpService.SendCommandAsync("Performance.getMetrics");
            var metrics = perfRes?["metrics"] as JsonArray;
            if (metrics != null)
            {
                UpdateMetrics(metrics);
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("ScratchPerfNode", "Error enabling performance domains", ex);
        }
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (e.Method == "Performance.metrics" && e.Params != null)
        {
            var metrics = e.Params["metrics"] as JsonArray;
            if (metrics != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateMetrics(metrics));
            }
        }
    }

    private void UpdateMetrics(JsonArray metrics)
    {
        foreach (var m in metrics)
        {
            if (m == null) continue;
            string name = m["name"]?.GetValue<string>() ?? "";
            double val = GetDouble(m["value"]);
            if (name == "CPUUsage")
            {
                CpuUsage = val;
            }
            else if (name == "JSHeapUsedSize")
            {
                MemoryUsage = val / (1024.0 * 1024.0);
            }
            else if (name == "LayoutCount")
            {
                LayoutCount = (int)val;
            }
        }

        var root = new JsonObject
        {
            ["metrics"] = metrics.DeepClone()
        };
        RawJsonData = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        Timestamp = DateTime.Now;
    }

    private async Task CaptureDataAsync()
    {
        if (_cdpService == null || !_cdpService.IsConnected)
        {
            RawJsonData = "{\"error\": \"CDP client is not connected.\"}";
            Timestamp = DateTime.Now;
            return;
        }

        IsCapturing = true;
        try
        {
            var result = await _cdpService.SendCommandAsync("Performance.getMetrics");
            if (result != null)
            {
                var metrics = result["metrics"] as JsonArray;
                if (metrics != null)
                {
                    UpdateMetrics(metrics);
                }
                else
                {
                    RawJsonData = result.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    Timestamp = DateTime.Now;
                }
            }
            else
            {
                RawJsonData = "{}";
                Timestamp = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            RawJsonData = $"{{\n  \"error\": \"Failed to capture data.\",\n  \"details\": \"{ex.Message.Replace("\"", "\\\"")}\"\n}}";
            Timestamp = DateTime.Now;
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private void UpdateSummary()
    {
        if (string.IsNullOrEmpty(RawJsonData))
        {
            DataSummary = "Empty";
            return;
        }

        try
        {
            var node = JsonNode.Parse(RawJsonData);
            if (node is JsonArray array)
            {
                DataSummary = $"{array.Count} items";
            }
            else if (node is JsonObject obj)
            {
                if (obj.TryGetPropertyValue("metrics", out var metricsNode) && metricsNode is JsonArray mArray)
                {
                    DataSummary = $"{mArray.Count} metrics";
                }
                else
                {
                    DataSummary = $"{obj.Count} properties";
                }
            }
            else
            {
                DataSummary = $"{RawJsonData.Length} chars";
            }
        }
        catch
        {
            DataSummary = $"{RawJsonData.Length} chars (Invalid JSON)";
        }
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

    public override void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_cdpService != null)
        {
            _cdpService.PropertyChanged -= CdpService_PropertyChanged;
            _cdpService.EventReceived -= CdpService_EventReceived;
        }

        base.Dispose();
    }
}
