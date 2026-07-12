#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using CDP.Editor.Nodes.ViewModels;
using Chrome.DevTools.Protocol;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public class ScratchConsoleNodeData
{
    public string RawJsonData { get; set; } = "";
    public DateTime? Timestamp { get; set; }
    public string ConsoleLogsJson { get; set; } = "[]";
    public int ErrorsCount { get; set; }
    public int WarningsCount { get; set; }

    public ScratchConsoleNodeData Clone()
    {
        return new ScratchConsoleNodeData
        {
            RawJsonData = this.RawJsonData,
            Timestamp = this.Timestamp,
            ConsoleLogsJson = this.ConsoleLogsJson,
            ErrorsCount = this.ErrorsCount,
            WarningsCount = this.WarningsCount
        };
    }
}
public class ScratchConsoleNodeViewModel : ScratchNodeViewModelBase, IImportExportNode
{
    private JsonNode? _parsedJsonNode;
    public override string OutputJson => RawJsonData;

    public override JsonNode? OutputJsonNode
    {
        get
        {
            if (_parsedJsonNode == null && !string.IsNullOrEmpty(RawJsonData))
            {
                try
                {
                    _parsedJsonNode = JsonNode.Parse(RawJsonData);
                }
                catch
                {
                    // Ignore malformed JSON
                }
            }
            return _parsedJsonNode;
        }
    }

    private readonly ICdpService? _cdpService;
    private readonly ConsoleViewModel? _consoleViewModel;
    private string _rawJsonData = "";
    private DateTime? _timestamp;
    private bool _isCapturing;
    private string _dataSummary = "Empty";
    private string _consoleLogsJson = "[]";
    private int _errorsCount;
    private int _warningsCount;

    public string RawJsonData
    {
        get => _rawJsonData;
        set
        {
            if (RaiseAndSetIfChanged(ref _rawJsonData, value))
            {
                _parsedJsonNode = null;
                OnPropertyChanged(nameof(OutputJsonNode));
                UpdateSummary();
                OnPropertyChanged(nameof(ConsoleLogs));
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

    public string ConsoleLogsJson
    {
        get => _consoleLogsJson;
        set
        {
            if (RaiseAndSetIfChanged(ref _consoleLogsJson, value))
            {
                RawJsonData = value;
            }
        }
    }

    public int ErrorsCount
    {
        get => _errorsCount;
        set => RaiseAndSetIfChanged(ref _errorsCount, value);
    }

    public int WarningsCount
    {
        get => _warningsCount;
        set => RaiseAndSetIfChanged(ref _warningsCount, value);
    }

    public ICommand CaptureCommand { get; }
    public ICommand ImportPayloadCommand { get; }
    public ICommand ExportPayloadCommand { get; }
    public ICommand ClearLogsCommand { get; }

    public Func<Task<string?>>? PayloadImportHandler { get; set; }
    public Func<Task>? PayloadExportHandler { get; set; }

    public ScratchConsoleNodeViewModel() : this(null, null)
    {
    }

    public ScratchConsoleNodeViewModel(ICdpService? cdpService) : this(cdpService, null)
    {
    }

    public ScratchConsoleNodeViewModel(ICdpService? cdpService, ConsoleViewModel? consoleViewModel)
    {
        _cdpService = cdpService;
        _consoleViewModel = consoleViewModel;

        TitleBackground = Avalonia.Media.Brush.Parse("#1a73e8");
        BorderBrush = Avalonia.Media.Brush.Parse("#4285f4");

        AddOutputPin("data", "Console Data");

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

        ClearLogsCommand = new RelayCommand(() =>
        {
            RawJsonData = "[]";
            ConsoleLogsJson = "[]";
            ErrorsCount = 0;
            WarningsCount = 0;
            Timestamp = null;
        });
    }

    private async Task CaptureDataAsync()
    {
        IsCapturing = true;
        try
        {
            string? rawResult = null;
            int errs = 0;
            int warns = 0;
            if (_consoleViewModel != null)
            {
                var logsArray = new JsonArray();
                foreach (var log in _consoleViewModel.Logs)
                {
                    logsArray.Add(new JsonObject
                    {
                        ["timestamp"] = log.Timestamp.ToString("o"),
                        ["level"] = log.Level,
                        ["text"] = log.Text
                    });
                    if (log.Level?.Equals("error", StringComparison.OrdinalIgnoreCase) == true) errs++;
                    if (log.Level?.Equals("warning", StringComparison.OrdinalIgnoreCase) == true) warns++;
                }
                rawResult = logsArray.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            else if (_cdpService != null && _cdpService.IsConnected)
            {
                await _cdpService.SendCommandAsync("Log.enable");
                rawResult = "{\"status\": \"Log domain enabled. Future logs will be captured by the console view.\"}";
            }
            else
            {
                rawResult = "{\"error\": \"CDP client is not connected.\"}";
            }

            ConsoleLogsJson = rawResult ?? "[]";
            ErrorsCount = errs;
            WarningsCount = warns;
            Timestamp = DateTime.Now;
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
                DataSummary = $"{array.Count} logs";
            }
            else if (node is JsonObject obj)
            {
                DataSummary = $"{obj.Count} properties";
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

    public List<SemanticConsoleLog> ConsoleLogs
    {
        get
        {
            var list = new List<SemanticConsoleLog>();
            if (string.IsNullOrEmpty(RawJsonData)) return list;
            try
            {
                var node = JsonNode.Parse(RawJsonData);
                if (node is JsonArray array)
                {
                    foreach (var item in array)
                    {
                        if (item is JsonObject obj)
                        {
                            list.Add(new SemanticConsoleLog
                            {
                                Timestamp = (string?)obj["timestamp"] ?? "",
                                Level = (string?)obj["level"] ?? "",
                                Text = (string?)obj["text"] ?? ""
                            });
                        }
                    }
                }
            }
            catch {}
            return list;
        }
    }
}
