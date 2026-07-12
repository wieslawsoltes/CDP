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

public class ScratchNetworkNodeData
{
    public string RawJsonData { get; set; } = "";
    public DateTime? Timestamp { get; set; }

    public ScratchNetworkNodeData Clone()
    {
        return new ScratchNetworkNodeData
        {
            RawJsonData = this.RawJsonData,
            Timestamp = this.Timestamp
        };
    }
}

public class ScratchNetworkNodeViewModel : ScratchNodeViewModelBase, IImportExportNode
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
    private readonly NetworkViewModel? _networkViewModel;
    private string _rawJsonData = "";
    private DateTime? _timestamp;
    private bool _isCapturing;
    private string _dataSummary = "Empty";
    private int _totalRequests;
    private int _failedRequests;

    public string RawJsonData
    {
        get => _rawJsonData;
        set
        {
            if (RaiseAndSetIfChanged(ref _rawJsonData, value))
            {
                _parsedJsonNode = null;
                OnPropertyChanged(nameof(OutputJsonNode));
                OnPropertyChanged(nameof(NetworkRequestsJson));
                UpdateSummary();
                OnPropertyChanged(nameof(NetworkRequests));
            }
        }
    }

    public string NetworkRequestsJson
    {
        get => RawJsonData;
        set => RawJsonData = value;
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

    public int TotalRequests
    {
        get => _totalRequests;
        set => RaiseAndSetIfChanged(ref _totalRequests, value);
    }

    public int FailedRequests
    {
        get => _failedRequests;
        set
        {
            if (RaiseAndSetIfChanged(ref _failedRequests, value))
            {
                OnPropertyChanged(nameof(HasFailedRequests));
            }
        }
    }

    public bool HasFailedRequests => FailedRequests > 0;

    public ICommand CaptureCommand { get; }
    public ICommand ImportPayloadCommand { get; }
    public ICommand ExportPayloadCommand { get; }

    public Func<Task<string?>>? PayloadImportHandler { get; set; }
    public Func<Task>? PayloadExportHandler { get; set; }

    public ScratchNetworkNodeViewModel() : this(null, null)
    {
    }

    public ScratchNetworkNodeViewModel(ICdpService? cdpService) : this(cdpService, null)
    {
    }

    public ScratchNetworkNodeViewModel(ICdpService? cdpService, NetworkViewModel? networkViewModel)
    {
        _cdpService = cdpService;
        _networkViewModel = networkViewModel;

        TitleBackground = Avalonia.Media.Brush.Parse("#1a73e8");
        BorderBrush = Avalonia.Media.Brush.Parse("#4285f4");

        AddOutputPin("data", "Network Data");

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
    }

    private async Task CaptureDataAsync()
    {
        IsCapturing = true;
        try
        {
            string? rawResult = null;
            if (_networkViewModel != null)
            {
                var networkArray = new JsonArray();
                foreach (var req in _networkViewModel.NetworkRequests)
                {
                    networkArray.Add(new JsonObject
                    {
                        ["requestId"] = req.RequestId,
                        ["method"] = req.Method,
                        ["url"] = req.Url,
                        ["type"] = req.Type,
                        ["status"] = req.Status,
                        ["time"] = req.Time,
                        ["requestHeaders"] = req.RequestHeaders,
                        ["responseHeaders"] = req.ResponseHeaders,
                        ["responseBody"] = req.ResponseBody
                    });
                }
                rawResult = networkArray.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                rawResult = "{\"status\": \"No network requests captured.\"}";
            }

            RawJsonData = rawResult ?? "[]";
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
            TotalRequests = 0;
            FailedRequests = 0;
            return;
        }

        try
        {
            var node = JsonNode.Parse(RawJsonData);
            if (node is JsonArray array)
            {
                TotalRequests = array.Count;
                int failed = 0;
                foreach (var item in array)
                {
                    if (item is JsonObject obj && obj.TryGetPropertyValue("status", out var statusVal))
                    {
                        var status = (int?)statusVal ?? 0;
                        if (status >= 400 || status == 0)
                        {
                            failed++;
                        }
                    }
                }
                FailedRequests = failed;
                DataSummary = $"{TotalRequests} requests";
            }
            else if (node is JsonObject obj)
            {
                TotalRequests = 0;
                FailedRequests = 0;
                DataSummary = $"{obj.Count} properties";
            }
            else
            {
                TotalRequests = 0;
                FailedRequests = 0;
                DataSummary = $"{RawJsonData.Length} chars";
            }
        }
        catch
        {
            TotalRequests = 0;
            FailedRequests = 0;
            DataSummary = $"{RawJsonData.Length} chars (Invalid JSON)";
        }
    }

    public List<SemanticNetworkRequest> NetworkRequests
    {
        get
        {
            var list = new List<SemanticNetworkRequest>();
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
                            list.Add(new SemanticNetworkRequest
                            {
                                RequestId = (string?)obj["requestId"] ?? "",
                                Method = (string?)obj["method"] ?? "",
                                Url = (string?)obj["url"] ?? "",
                                Type = (string?)obj["type"] ?? "",
                                Status = (int?)obj["status"] ?? 0,
                                Time = (double?)obj["time"] ?? 0.0
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
