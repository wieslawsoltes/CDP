#nullable enable

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using CDP.Editor.Nodes.ViewModels;
using Chrome.DevTools.Protocol;
using CdpInspectorApp.Services;
using Microsoft.Extensions.Logging;

namespace CdpInspectorApp.ViewModels;

public class ScratchApplicationNodeViewModel : ScratchNodeViewModelBase, IImportExportNode
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<ScratchApplicationNodeViewModel>();
    private readonly ICdpService? _cdpService;
    private string _appResourcesJson = "[]";
    private int _windowCount;
    private int _stylesheetCount;
    private DateTime? _timestamp;
    private bool _isCapturing;

    public string AppResourcesJson
    {
        get => _appResourcesJson;
        set
        {
            if (RaiseAndSetIfChanged(ref _appResourcesJson, value))
            {
                OnPropertyChanged(nameof(RawJsonData));
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public int WindowCount
    {
        get => _windowCount;
        set
        {
            if (RaiseAndSetIfChanged(ref _windowCount, value))
            {
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public int StylesheetCount
    {
        get => _stylesheetCount;
        set
        {
            if (RaiseAndSetIfChanged(ref _stylesheetCount, value))
            {
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
            }
        }
    }

    public string RawJsonData
    {
        get => AppResourcesJson;
        set => AppResourcesJson = value;
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

    public Func<Task<string?>>? PayloadImportHandler { get; set; }
    public Func<Task>? PayloadExportHandler { get; set; }

    public ICommand CaptureCommand { get; }

    public override string OutputJson
    {
        get
        {
            var obj = new JsonObject
            {
                ["windowCount"] = WindowCount,
                ["stylesheetCount"] = StylesheetCount,
                ["appResources"] = string.IsNullOrEmpty(AppResourcesJson) ? null : JsonNode.Parse(AppResourcesJson)
            };
            return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public override JsonNode? OutputJsonNode => new JsonObject
    {
        ["windowCount"] = WindowCount,
        ["stylesheetCount"] = StylesheetCount,
        ["appResources"] = string.IsNullOrEmpty(AppResourcesJson) ? null : JsonNode.Parse(AppResourcesJson)
    };

    public ScratchApplicationNodeViewModel() : this(null)
    {
    }

    public ScratchApplicationNodeViewModel(ICdpService? cdpService)
    {
        _cdpService = cdpService;
        TitleBackground = Avalonia.Media.Brush.Parse("#9c27b0"); // Purple background for title/header
        BorderBrush = Avalonia.Media.Brush.Parse("#ba68c8");     // Purple border highlight

        AddOutputPin("data", "App Data");

        CaptureCommand = new RelayCommand(async () => await CaptureMetadataAsync());
    }

    private async Task CaptureMetadataAsync()
    {
        if (_cdpService == null || !_cdpService.IsConnected)
        {
            return;
        }

        IsCapturing = true;
        try
        {
            // 1. Query Window Count
            int winCount = 1;
            if (!string.IsNullOrEmpty(_cdpService.ConnectedHost))
            {
                var targets = await _cdpService.GetTargetsAsync(_cdpService.ConnectedHost);
                if (targets != null && targets.Count > 0)
                {
                    winCount = targets.Count;
                }
            }
            WindowCount = winCount;

            // 2. Query Stylesheet Count
            int styleCount = 0;
            try
            {
                var styleRes = await _cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
                {
                    ["expression"] = "window.visual.Styles.Count",
                    ["returnByValue"] = true
                });
                if (styleRes != null && styleRes.TryGetPropertyValue("result", out var styleResultNode) && styleResultNode is JsonObject styleResultObj)
                {
                    if (styleResultObj.TryGetPropertyValue("value", out var valNode) && valNode != null)
                    {
                        styleCount = valNode.GetValue<int>();
                    }
                    else if (styleResultObj.TryGetPropertyValue("description", out var descNode) && descNode != null)
                    {
                        int.TryParse(descNode.GetValue<string>(), out styleCount);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogErrorMessage("ScratchAppNode", "Stylesheet count query failed", ex);
            }
            StylesheetCount = styleCount;

            // 3. Query Application Resources
            try
            {
                var res = await _cdpService.SendCommandAsync("Application.getResources");
                var resources = res?["resources"];
                AppResourcesJson = resources?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "[]";
            }
            catch (Exception ex)
            {
                Logger.LogErrorMessage("ScratchAppNode", "Resources query failed", ex);
                AppResourcesJson = "[]";
            }

            Timestamp = DateTime.Now;
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("ScratchAppNode", "Capture failed", ex);
        }
        finally
        {
            IsCapturing = false;
        }
    }
}
