#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using CDP.Editor.Nodes.ViewModels;
using Chrome.DevTools.Protocol;
using CdpInspectorApp.Services;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.ViewModels;

public class AccessibilityWarning
{
    public string NodeId { get; set; } = "";
    public string Role { get; set; } = "Unknown";
    public string AccessibleName { get; set; } = "";
    public string Issue { get; set; } = "";
    public string Severity { get; set; } = "Warning"; // Warning or Error
}

public class AccessibilityNodeInfo
{
    public string NodeId { get; set; } = "";
    public string Role { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class ScratchAccessibilityNodeViewModel : ScratchNodeViewModelBase, IImportExportNode
{
    private readonly ICdpService? _cdpService;
    private string? _inputNodeId;
    private ScratchNodeViewModelBase? _inputNode;
    private string _inputTitle = "Input";
    private string _a11yTreeJson = "{}";
    private int _warningsCount;
    private int _nodeCount;
    private List<AccessibilityWarning> _warnings = new();
    private List<AccessibilityNodeInfo> _accessibilityNodes = new();
    private string _outputJson = "{}";

    private string _rawJsonData = "";
    private DateTime? _timestamp;
    private bool _isCapturing;
    private JsonNode? _parsedJsonNode;

    public string? InputNodeId
    {
        get => _inputNodeId;
        set => RaiseAndSetIfChanged(ref _inputNodeId, value);
    }

    public ScratchNodeViewModelBase? InputNode
    {
        get => _inputNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _inputNode, value))
            {
                if (_inputNodeId != value?.Id)
                {
                    _inputNodeId = value?.Id;
                    OnPropertyChanged(nameof(InputNodeId));
                }
                EvaluateAccessibility();
            }
        }
    }

    public string InputTitle
    {
        get => _inputTitle;
        set => RaiseAndSetIfChanged(ref _inputTitle, value);
    }

    public string A11yTreeJson
    {
        get => _a11yTreeJson;
        set => RaiseAndSetIfChanged(ref _a11yTreeJson, value);
    }

    public int WarningsCount
    {
        get => _warningsCount;
        set
        {
            if (RaiseAndSetIfChanged(ref _warningsCount, value))
            {
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(HasNoWarnings));
                OnPropertyChanged(nameof(DataSummary));
            }
        }
    }

    public int NodeCount
    {
        get => _nodeCount;
        set
        {
            if (RaiseAndSetIfChanged(ref _nodeCount, value))
            {
                OnPropertyChanged(nameof(DataSummary));
            }
        }
    }

    public string DataSummary => $"{NodeCount} nodes, {WarningsCount} warnings";

    public List<AccessibilityWarning> Warnings
    {
        get => _warnings;
        set => RaiseAndSetIfChanged(ref _warnings, value);
    }

    public List<AccessibilityNodeInfo> AccessibilityNodes
    {
        get => _accessibilityNodes;
        set => RaiseAndSetIfChanged(ref _accessibilityNodes, value);
    }

    public bool HasWarnings => WarningsCount > 0;
    public bool HasNoWarnings => WarningsCount == 0;

    public string RawJsonData
    {
        get => _rawJsonData;
        set
        {
            if (RaiseAndSetIfChanged(ref _rawJsonData, value))
            {
                _parsedJsonNode = null;
                OnPropertyChanged(nameof(OutputJsonNode));
                EvaluateAccessibility();
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

    public override string OutputJson => _outputJson;

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
                    // Ignore
                }
            }
            return _parsedJsonNode;
        }
    }

    public ICommand CaptureCommand { get; }
    public ICommand ImportPayloadCommand { get; }
    public ICommand ExportPayloadCommand { get; }

    public Func<Task<string?>>? PayloadImportHandler { get; set; }
    public Func<Task>? PayloadExportHandler { get; set; }

    public ScratchAccessibilityNodeViewModel() : this(null)
    {
    }

    public ScratchAccessibilityNodeViewModel(ICdpService? cdpService)
    {
        _cdpService = cdpService;

        TitleBackground = Avalonia.Media.Brush.Parse("#b04600"); // Burnt orange for Accessibility Node title bar
        BorderBrush = Avalonia.Media.Brush.Parse("#e06000");

        AddInputPin("dom", "DOM Input");
        AddOutputPin("a11y", "A11y Data");

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

        LinkSelectedNodeCommand = new RelayCommand(async () =>
        {
            var elementsVm = MainWindowViewModel.Instance?.Elements;
            if (elementsVm != null)
            {
                var selected = elementsVm.SelectedAxNode;
                if (selected != null)
                {
                    LinkedElementId = selected.NodeId;
                    LinkedElementName = selected.DisplayName;
                    await CaptureDataAsync();
                }
            }
        });

        ShowInTreeCommand = new RelayCommand(() =>
        {
            var mainVm = MainWindowViewModel.Instance;
            if (mainVm != null && !string.IsNullOrEmpty(LinkedElementId))
            {
                var elementsBox = mainVm.FindBoxNodeByViewName(mainVm.LayoutRoot, "Elements");
                if (elementsBox != null)
                {
                    elementsBox.SelectedViewName = "Elements";
                }
                
                var axBox = mainVm.FindBoxNodeByViewName(mainVm.Elements.LayoutRoot, "AxTree");
                if (axBox != null)
                {
                    axBox.SelectedViewName = "AxTree";
                }

                var node = FindAxNode(mainVm.Elements.AxRootNodes, LinkedElementId);
                if (node != null)
                {
                    mainVm.Elements.SelectedAxNode = node;
                }
            }
        });

        EvaluateAccessibility();
    }

    private AxNodeModel? FindAxNode(IEnumerable<AxNodeModel> nodes, string nodeId)
    {
        foreach (var node in nodes)
        {
            if (node.NodeId == nodeId) return node;
            var found = FindAxNode(node.Children, nodeId);
            if (found != null) return found;
        }
        return null;
    }

    public void UpdateAccessibility(Func<string, ScratchNodeViewModelBase?> getNodeById, IEnumerable<CDP.Editor.Nodes.ViewModels.ConnectionViewModel> connections)
    {
        var incoming = connections
            .Where(c => c.ToNode == this && c.FromNode is ScratchNodeViewModelBase)
            .ToList();

        string? resolvedInputId = InputNodeId;

        if (string.IsNullOrEmpty(resolvedInputId) && incoming.Count > 0)
        {
            resolvedInputId = incoming[0].FromNode?.Id;
        }

        var inputNode = !string.IsNullOrEmpty(resolvedInputId) ? getNodeById(resolvedInputId) : null;

        bool inputChanged = _inputNode != inputNode;

        if (inputChanged)
        {
            _inputNode = inputNode;
            _inputNodeId = inputNode?.Id;
            OnPropertyChanged(nameof(InputNode));
            OnPropertyChanged(nameof(InputNodeId));
        }

        InputTitle = inputNode != null ? $"{inputNode.Name}" : "Input (No Connection)";
        
        EvaluateAccessibility();
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
            JsonObject? result = null;
            if (!string.IsNullOrEmpty(LinkedElementId) && int.TryParse(LinkedElementId, out var nodeId))
            {
                result = await _cdpService.SendCommandAsync("Accessibility.getPartialAXTree", new JsonObject
                {
                    ["nodeId"] = nodeId,
                    ["fetchRelatives"] = true
                });
            }
            else
            {
                result = await _cdpService.SendCommandAsync("Accessibility.getFullAXTree");
            }

            if (result != null)
            {
                RawJsonData = result.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                RawJsonData = "{}";
            }

            Timestamp = DateTime.Now;
        }
        catch (Exception ex)
        {
            RawJsonData = $"{{\n  \"error\": \"Failed to capture accessibility tree.\",\n  \"details\": \"{ex.Message.Replace("\"", "\\\"")}\"\n}}";
            Timestamp = DateTime.Now;
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private void EvaluateAccessibility()
    {
        string jsonText;
        JsonNode? rootNode = null;

        if (InputNode != null)
        {
            rootNode = InputNode.OutputJsonNode;
            jsonText = InputNode.OutputJson;
        }
        else
        {
            rootNode = OutputJsonNode;
            jsonText = RawJsonData;
        }

        if (rootNode == null)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                UpdateOutput(0, 0, new List<AccessibilityWarning>(), new List<AccessibilityNodeInfo>(), "{}");
                return;
            }

            try
            {
                rootNode = JsonNode.Parse(jsonText);
            }
            catch
            {
                UpdateOutput(0, 0, new List<AccessibilityWarning>(), new List<AccessibilityNodeInfo>(), "{}");
                return;
            }
        }

        JsonArray? nodesArray = null;
        if (rootNode is JsonObject obj && obj.TryGetPropertyValue("nodes", out var nodesVal) && nodesVal is JsonArray arr)
        {
            nodesArray = arr;
        }
        else if (rootNode is JsonArray directArr)
        {
            nodesArray = directArr;
        }

        if (nodesArray == null)
        {
            UpdateOutput(0, 0, new List<AccessibilityWarning>(), new List<AccessibilityNodeInfo>(), "{}");
            return;
        }

        int nodeCount = 0;
        var warnings = new List<AccessibilityWarning>();
        var accessibilityNodes = new List<AccessibilityNodeInfo>();

        foreach (var nodeItem in nodesArray)
        {
            if (nodeItem is not JsonObject nodeObj) continue;

            nodeCount++;

            string nodeId = nodeObj["nodeId"]?.GetValue<string>() ?? "";
            bool ignored = nodeObj["ignored"]?.GetValue<bool>() ?? false;
            
            var roleObj = nodeObj["role"] as JsonObject;
            string role = roleObj?["value"]?.GetValue<string>() ?? "Unknown";

            var nameObj = nodeObj["name"] as JsonObject;
            string name = nameObj?["value"]?.GetValue<string>() ?? "";

            var descObj = nodeObj["description"] as JsonObject;
            string description = descObj?["value"]?.GetValue<string>() ?? "";

            accessibilityNodes.Add(new AccessibilityNodeInfo
            {
                NodeId = nodeId,
                Role = role,
                Name = name,
                Description = description
            });

            bool isFocusable = false;
            if (nodeObj.TryGetPropertyValue("properties", out var propsNode) && propsNode is JsonArray propsArray)
            {
                foreach (var propItem in propsArray)
                {
                    if (propItem is JsonObject propObj)
                    {
                        var propName = propObj["name"]?.GetValue<string>();
                        if (propName == "focusable")
                        {
                            isFocusable = propObj["value"]?["value"]?.GetValue<bool>() ?? false;
                            break;
                        }
                    }
                }
            }

            if (!ignored)
            {
                bool needsName = isFocusable || 
                                 role == "button" || 
                                 role == "checkbox" || 
                                 role == "combobox" || 
                                 role == "textbox" || 
                                 role == "slider" || 
                                 role == "radio" || 
                                 role == "tree" ||
                                 role == "list" ||
                                 role == "link";

                if (needsName && string.IsNullOrWhiteSpace(name))
                {
                    warnings.Add(new AccessibilityWarning
                    {
                        NodeId = nodeId,
                        Role = role,
                        AccessibleName = name,
                        Issue = $"Interactive element '{role}' (Node ID: {nodeId}) lacks a defined accessibility name.",
                        Severity = "Error"
                    });
                }
            }
            else
            {
                if (isFocusable)
                {
                    warnings.Add(new AccessibilityWarning
                    {
                        NodeId = nodeId,
                        Role = role,
                        AccessibleName = name,
                        Issue = $"Element (Node ID: {nodeId}) is keyboard-focusable but ignored/hidden from accessibility view.",
                        Severity = "Warning"
                    });
                }
            }
        }

        var outputObj = new JsonObject
        {
            ["nodeCount"] = nodeCount,
            ["warningsCount"] = warnings.Count,
            ["warnings"] = new JsonArray(warnings.Select(w => new JsonObject
            {
                ["nodeId"] = w.NodeId,
                ["role"] = w.Role,
                ["name"] = w.AccessibleName,
                ["issue"] = w.Issue,
                ["severity"] = w.Severity
            }).ToArray())
        };

        string outputJsonStr = outputObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        UpdateOutput(nodeCount, warnings.Count, warnings, accessibilityNodes, outputJsonStr);
    }

    private void UpdateOutput(int nodeCount, int warningsCount, List<AccessibilityWarning> warnings, List<AccessibilityNodeInfo> accessibilityNodes, string outputJsonStr)
    {
        NodeCount = nodeCount;
        WarningsCount = warningsCount;
        Warnings = warnings;
        AccessibilityNodes = accessibilityNodes;
        _outputJson = outputJsonStr;
        A11yTreeJson = outputJsonStr;

        OnPropertyChanged(nameof(OutputJson));
        OnPropertyChanged(nameof(OutputJsonNode));
        OnPropertyChanged(nameof(A11yTreeJson));
        OnPropertyChanged(nameof(WarningsCount));
        OnPropertyChanged(nameof(NodeCount));
        OnPropertyChanged(nameof(Warnings));
        OnPropertyChanged(nameof(AccessibilityNodes));
        OnPropertyChanged(nameof(HasWarnings));
    }
}

public class ScratchAccessibilityNodeData
{
    public string RawJsonData { get; set; } = "";
    public DateTime? Timestamp { get; set; }

    public ScratchAccessibilityNodeData Clone()
    {
        return new ScratchAccessibilityNodeData
        {
            RawJsonData = this.RawJsonData,
            Timestamp = this.Timestamp
        };
    }
}
