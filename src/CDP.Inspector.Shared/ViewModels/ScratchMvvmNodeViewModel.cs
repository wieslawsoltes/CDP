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

namespace CdpInspectorApp.ViewModels;
public class ScratchMvvmNodeData
{
    public string? InputNodeId { get; set; }
    public string InputTitle { get; set; } = "";
    public string MvvmTreeJson { get; set; } = "";
    public string CurrentVmType { get; set; } = "";
    public int PropertiesCount { get; set; }
    public DateTime? Timestamp { get; set; }

    public ScratchMvvmNodeData Clone()
    {
        return new ScratchMvvmNodeData
        {
            InputNodeId = this.InputNodeId,
            InputTitle = this.InputTitle,
            MvvmTreeJson = this.MvvmTreeJson,
            CurrentVmType = this.CurrentVmType,
            PropertiesCount = this.PropertiesCount,
            Timestamp = this.Timestamp
        };
    }
}

public class ScratchMvvmNodeViewModel : ScratchNodeViewModelBase, IImportExportNode
{
    private string? _inputNodeId;
    private ScratchNodeViewModelBase? _inputNode;
    private string _inputTitle = "Input (No Connection)";
    private string _mvvmTreeJson = "";
    private string _currentVmType = "None";
    private int _propertiesCount;
    private string _mvvmHierarchyText = "No MVVM Data";
    private DateTime? _timestamp;
    private bool _isCapturing;
    private string _dataSummary = "Empty";
    private JsonNode? _parsedJsonNode;

    private readonly ICdpService? _cdpService;

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
                UpdateFromInputNode();
            }
        }
    }

    public string InputTitle
    {
        get => _inputTitle;
        set => RaiseAndSetIfChanged(ref _inputTitle, value);
    }

    public string RawJsonData
    {
        get => MvvmTreeJson;
        set
        {
            if (MvvmTreeJson != value)
            {
                MvvmTreeJson = value;
                OnPropertyChanged(nameof(RawJsonData));
            }
        }
    }

    public string MvvmTreeJson
    {
        get => _mvvmTreeJson;
        set
        {
            if (RaiseAndSetIfChanged(ref _mvvmTreeJson, value))
            {
                _parsedJsonNode = null;
                OnPropertyChanged(nameof(OutputJson));
                OnPropertyChanged(nameof(OutputJsonNode));
                OnPropertyChanged(nameof(RawJsonData));
                UpdateSummary();
                ParseMvvmJson();
            }
        }
    }

    public string CurrentVmType
    {
        get => _currentVmType;
        set
        {
            if (RaiseAndSetIfChanged(ref _currentVmType, value))
            {
                OnPropertyChanged(nameof(ShortVmType));
            }
        }
    }

    public int PropertiesCount
    {
        get => _propertiesCount;
        set => RaiseAndSetIfChanged(ref _propertiesCount, value);
    }

    public string MvvmHierarchyText
    {
        get => _mvvmHierarchyText;
        set
        {
            if (RaiseAndSetIfChanged(ref _mvvmHierarchyText, value))
            {
                OnPropertyChanged(nameof(MvvmTreeText));
            }
        }
    }

    public string MvvmTreeText
    {
        get => MvvmHierarchyText;
        set
        {
            if (MvvmHierarchyText != value)
            {
                MvvmHierarchyText = value;
                OnPropertyChanged(nameof(MvvmTreeText));
            }
        }
    }

    public string ShortVmType => string.IsNullOrEmpty(CurrentVmType)
        ? "None"
        : (CurrentVmType.Contains('.') ? CurrentVmType.Substring(CurrentVmType.LastIndexOf('.') + 1) : CurrentVmType);

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

    public ICommand CaptureCommand { get; }
    public ICommand ImportPayloadCommand { get; }
    public ICommand ExportPayloadCommand { get; }

    public Func<Task<string?>>? PayloadImportHandler { get; set; }
    public Func<Task>? PayloadExportHandler { get; set; }

    public override string OutputJson => MvvmTreeJson;

    public override JsonNode? OutputJsonNode
    {
        get
        {
            if (_parsedJsonNode == null && !string.IsNullOrEmpty(MvvmTreeJson))
            {
                try
                {
                    _parsedJsonNode = JsonNode.Parse(MvvmTreeJson);
                }
                catch
                {
                    // Ignore malformed JSON
                }
            }
            return _parsedJsonNode;
        }
    }

    public ScratchMvvmNodeViewModel() : this(null)
    {
    }

    public ScratchMvvmNodeViewModel(ICdpService? cdpService)
    {
        _cdpService = cdpService;

        TitleBackground = Avalonia.Media.Brush.Parse("#137333");
        BorderBrush = Avalonia.Media.Brush.Parse("#1e8e3e");

        AddInputPin("parent_vm", "Parent VM");
        AddOutputPin("vm", "VM Data");

        CaptureCommand = new RelayCommand(async () => await CaptureDataAsync());

        ImportPayloadCommand = new RelayCommand(async () =>
        {
            if (PayloadImportHandler != null)
            {
                var content = await PayloadImportHandler();
                if (content != null)
                {
                    MvvmTreeJson = content;
                    Timestamp = DateTime.Now;
                }
            }
        });

        ExportPayloadCommand = new RelayCommand(async () =>
        {
            if (PayloadExportHandler != null && !string.IsNullOrEmpty(MvvmTreeJson))
            {
                await PayloadExportHandler();
            }
        });

        LinkSelectedNodeCommand = new RelayCommand(async () =>
        {
            var mvvmVm = MainWindowViewModel.Instance?.Mvvm;
            if (mvvmVm != null)
            {
                var selected = mvvmVm.SelectedNode;
                if (selected != null)
                {
                    LinkedElementId = selected.Id;
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
                var mvvmBox = mainVm.FindBoxNodeByViewName(mainVm.LayoutRoot, "Mvvm");
                if (mvvmBox != null)
                {
                    mvvmBox.SelectedViewName = "Mvvm";
                }

                var node = FindMvvmNode(mainVm.Mvvm.MvvmTree, LinkedElementId);
                if (node != null)
                {
                    mainVm.Mvvm.SelectedNode = node;
                }
            }
        });

        ParseMvvmJson();
    }

    private InspectorViewModelNode? FindMvvmNode(IEnumerable<InspectorViewModelNode> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id) return node;
            var found = FindMvvmNode(node.Children, id);
            if (found != null) return found;
        }
        return null;
    }

    private JsonNode? FindNodeInJson(JsonNode? node, string id)
    {
        if (node == null) return null;
        if (node is JsonObject obj)
        {
            if (obj["id"]?.ToString() == id)
            {
                return obj;
            }
            if (obj["children"] is JsonArray arr)
            {
                foreach (var child in arr)
                {
                    var found = FindNodeInJson(child, id);
                    if (found != null) return found;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                var found = FindNodeInJson(item, id);
                if (found != null) return found;
            }
        }
        return null;
    }

    public void UpdateMvvm(Func<string, ScratchNodeViewModelBase?> getNodeById, IEnumerable<CDP.Editor.Nodes.ViewModels.ConnectionViewModel> connections)
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
        UpdateFromInputNode();
    }

    private void UpdateFromInputNode()
    {
        if (InputNode != null)
        {
            MvvmTreeJson = InputNode.OutputJson;
        }
    }

    private async Task CaptureDataAsync()
    {
        if (_cdpService == null || !_cdpService.IsConnected)
        {
            MvvmTreeJson = "{\"error\": \"CDP client is not connected.\"}";
            Timestamp = DateTime.Now;
            return;
        }

        IsCapturing = true;
        try
        {
            JsonObject? result = await _cdpService.SendCommandAsync("Mvvm.getViewModelTree");

            if (result != null)
            {
                if (!string.IsNullOrEmpty(LinkedElementId))
                {
                    var rootNode = result["tree"] ?? result;
                    var foundNode = FindNodeInJson(rootNode, LinkedElementId);
                    if (foundNode != null)
                    {
                        var wrapper = new JsonObject();
                        wrapper["tree"] = foundNode.DeepClone();
                        result = wrapper;
                    }
                }
                MvvmTreeJson = result.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                MvvmTreeJson = "{}";
            }

            Timestamp = DateTime.Now;
        }
        catch (Exception ex)
        {
            MvvmTreeJson = $"{{\n  \"error\": \"Failed to capture data.\",\n  \"details\": \"{ex.Message.Replace("\"", "\\\"")}\"\n}}";
            Timestamp = DateTime.Now;
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private void UpdateSummary()
    {
        if (string.IsNullOrEmpty(MvvmTreeJson))
        {
            DataSummary = "Empty";
            return;
        }

        try
        {
            var node = JsonNode.Parse(MvvmTreeJson);
            if (node is JsonArray array)
            {
                DataSummary = $"{array.Count} items";
            }
            else if (node is JsonObject obj)
            {
                DataSummary = $"{obj.Count} properties";
            }
            else
            {
                DataSummary = $"{MvvmTreeJson.Length} chars";
            }
        }
        catch
        {
            DataSummary = $"{MvvmTreeJson.Length} chars (Invalid JSON)";
        }
    }

    private void ParseMvvmJson()
    {
        if (string.IsNullOrEmpty(MvvmTreeJson))
        {
            CurrentVmType = "None";
            PropertiesCount = 0;
            MvvmHierarchyText = "No MVVM Data";
            return;
        }

        try
        {
            var node = JsonNode.Parse(MvvmTreeJson);
            if (node == null)
            {
                CurrentVmType = "None";
                PropertiesCount = 0;
                MvvmHierarchyText = "No MVVM Data";
                return;
            }

            JsonNode? treeNode = null;
            
            if (node is JsonObject obj)
            {
                if (obj.TryGetPropertyValue("result", out var resVal) && resVal is JsonObject resObj)
                {
                    if (resObj.TryGetPropertyValue("tree", out var treeVal))
                    {
                        treeNode = treeVal;
                    }
                }
                else if (obj.TryGetPropertyValue("tree", out var treeVal))
                {
                    treeNode = treeVal;
                }
                else if (obj.TryGetPropertyValue("type", out var typeVal))
                {
                    treeNode = obj;
                }
            }
            else if (node is JsonArray arr)
            {
                treeNode = arr;
            }

            if (treeNode == null)
            {
                CurrentVmType = "Unknown";
                PropertiesCount = 0;
                MvvmHierarchyText = "Invalid MVVM Data Structure";
                return;
            }

            JsonObject? vmNode = null;
            if (treeNode is JsonArray treeArr && treeArr.Count > 0)
            {
                vmNode = treeArr[0] as JsonObject;
            }
            else if (treeNode is JsonObject treeObj)
            {
                vmNode = treeObj;
            }

            if (vmNode == null)
            {
                CurrentVmType = "Unknown";
                PropertiesCount = 0;
                MvvmHierarchyText = "Empty MVVM Tree";
                return;
            }

            CurrentVmType = (string?)vmNode["type"] ?? "Unknown";
            
            if (vmNode.TryGetPropertyValue("properties", out var propsVal) && propsVal is JsonArray propsArr)
            {
                PropertiesCount = propsArr.Count;
            }
            else
            {
                PropertiesCount = 0;
            }

            var sb = new System.Text.StringBuilder();
            FormatMvvmNode(vmNode, sb, "", true);
            MvvmHierarchyText = sb.ToString();
        }
        catch (Exception ex)
        {
            CurrentVmType = "Error";
            PropertiesCount = 0;
            MvvmHierarchyText = $"Error parsing MVVM Tree: {ex.Message}";
        }
    }

    private void FormatMvvmNode(JsonNode node, System.Text.StringBuilder sb, string indent, bool isLast)
    {
        if (node is not JsonObject obj) return;

        string type = (string?)obj["type"] ?? "Unknown";
        string controlType = (string?)obj["controlType"] ?? "";
        string controlName = (string?)obj["controlName"] ?? "";

        string controlStr = !string.IsNullOrEmpty(controlType)
            ? (!string.IsNullOrEmpty(controlName) ? $" ({controlType} [Name='{controlName}'])" : $" ({controlType})")
            : "";

        int propsCount = 0;
        if (obj.TryGetPropertyValue("properties", out var propsVal) && propsVal is JsonArray propsArr)
        {
            propsCount = propsArr.Count;
        }

        sb.AppendLine($"{indent}{(indent == "" ? "" : (isLast ? "└─ " : "├─ "))}{type}{controlStr} [{propsCount} properties]");

        if (obj.TryGetPropertyValue("children", out var childrenNode) && childrenNode is JsonArray childrenArray && childrenArray.Count > 0)
        {
            string nextIndent = indent + (indent == "" ? "" : (isLast ? "   " : "│  "));
            for (int i = 0; i < childrenArray.Count; i++)
            {
                var child = childrenArray[i];
                if (child != null)
                {
                    FormatMvvmNode(child, sb, nextIndent, i == childrenArray.Count - 1);
                }
            }
        }
    }
}
