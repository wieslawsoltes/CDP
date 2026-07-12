#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using CDP.Editor.Nodes.ViewModels;
using Chrome.DevTools.Protocol;
using CdpInspectorApp.Services;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.ViewModels;



public class ScratchDomNodeViewModel : ScratchNodeViewModelBase, IImportExportNode
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
    private string _rawJsonData = "";
    private DateTime? _timestamp;
    private bool _isCapturing;
    private string _dataSummary = "Empty";
    private string _searchTerm = "";

    private int _elementCount;

    public int ElementCount
    {
        get => _elementCount;
        private set => RaiseAndSetIfChanged(ref _elementCount, value);
    }

    public string DomTreeJson => RawJsonData;

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
                OnPropertyChanged(nameof(SemanticDomText));
                OnPropertyChanged(nameof(SearchResults));
                OnPropertyChanged(nameof(DomTreeJson));
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

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (RaiseAndSetIfChanged(ref _searchTerm, value))
            {
                OnPropertyChanged(nameof(SearchResults));
                OnPropertyChanged(nameof(SearchQuery));
            }
        }
    }

    public string SearchQuery
    {
        get => SearchTerm;
        set
        {
            SearchTerm = value;
        }
    }

    public ICommand CaptureCommand { get; }
    public ICommand ImportPayloadCommand { get; }
    public ICommand ExportPayloadCommand { get; }

    public Func<Task<string?>>? PayloadImportHandler { get; set; }
    public Func<Task>? PayloadExportHandler { get; set; }

    public ScratchDomNodeViewModel() : this(null)
    {
    }

    public ScratchDomNodeViewModel(ICdpService? cdpService)
    {
        _cdpService = cdpService;

        TitleBackground = Avalonia.Media.Brush.Parse("#1a73e8");
        BorderBrush = Avalonia.Media.Brush.Parse("#4285f4");

        AddOutputPin("dom", "DOM Data");

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
                var selected = elementsVm.SelectedNode;
                if (selected != null)
                {
                    LinkedElementId = selected.NodeId.ToString();
                    LinkedElementName = selected.DisplayName;
                    await CaptureDataAsync();
                }
            }
        });

        ShowInTreeCommand = new RelayCommand(() =>
        {
            var mainVm = MainWindowViewModel.Instance;
            if (mainVm != null && !string.IsNullOrEmpty(LinkedElementId) && int.TryParse(LinkedElementId, out var nodeId))
            {
                var elementsBox = mainVm.FindBoxNodeByViewName(mainVm.LayoutRoot, "Elements");
                if (elementsBox != null)
                {
                    elementsBox.SelectedViewName = "Elements";
                }
                
                var domBox = mainVm.FindBoxNodeByViewName(mainVm.Elements.LayoutRoot, "DomTree");
                if (domBox != null)
                {
                    domBox.SelectedViewName = "DomTree";
                }

                var node = FindDomNode(mainVm.Elements.RootNodes, nodeId);
                if (node != null)
                {
                    mainVm.Elements.SelectedNode = node;
                }
            }
        });
    }

    private DomNodeModel? FindDomNode(IEnumerable<DomNodeModel> nodes, int nodeId)
    {
        foreach (var node in nodes)
        {
            if (node.NodeId == nodeId) return node;
            var found = FindDomNode(node.Children, nodeId);
            if (found != null) return found;
        }
        return null;
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
                var htmlResult = await _cdpService.SendCommandAsync("DOM.getOuterHTML", new JsonObject { ["nodeId"] = nodeId });
                var descResult = await _cdpService.SendCommandAsync("DOM.describeNode", new JsonObject { ["nodeId"] = nodeId });
                var combined = new JsonObject();
                if (htmlResult != null)
                {
                    combined["outerHTML"] = htmlResult["outerHTML"]?.DeepClone();
                }
                if (descResult != null)
                {
                    combined["node"] = descResult["node"]?.DeepClone();
                }
                result = combined;
            }
            else
            {
                result = await _cdpService.SendCommandAsync("DOM.getDocument", new JsonObject
                {
                    ["depth"] = -1,
                    ["pierce"] = true
                });
            }

            if (result != null)
            {
                RawJsonData = result.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                RawJsonData = "{}";
            }

            Timestamp = DateTime.Now;
        }
        catch (Exception ex)
        {
            RawJsonData = $"{{\n  \"error\": \"Failed to capture DOM data.\",\n  \"details\": \"{ex.Message.Replace("\"", "\\\"")}\"\n}}";
            Timestamp = DateTime.Now;
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private int CountElements(JsonNode? node)
    {
        if (node is not JsonObject obj) return 0;
        int count = 1;
        if (obj.TryGetPropertyValue("children", out var childrenNode) && childrenNode is JsonArray childrenArray)
        {
            foreach (var child in childrenArray)
            {
                count += CountElements(child);
            }
        }
        return count;
    }

    private void UpdateSummary()
    {
        if (string.IsNullOrEmpty(RawJsonData))
        {
            DataSummary = "Empty";
            ElementCount = 0;
            return;
        }

        try
        {
            var node = JsonNode.Parse(RawJsonData);
            if (node is JsonObject obj && obj.TryGetPropertyValue("root", out var rootNode) && rootNode != null)
            {
                ElementCount = CountElements(rootNode);
                DataSummary = $"{ElementCount} elements";
            }
            else
            {
                ElementCount = 0;
                DataSummary = "Invalid DOM payload";
            }
        }
        catch
        {
            ElementCount = 0;
            DataSummary = "Invalid JSON";
        }
    }

    public string SemanticDomText
    {
        get
        {
            if (string.IsNullOrEmpty(RawJsonData)) return "";
            try
            {
                var node = JsonNode.Parse(RawJsonData);
                if (node is JsonObject obj && obj.TryGetPropertyValue("root", out var rootNode) && rootNode != null)
                {
                    var sb = new System.Text.StringBuilder();
                    FormatDomNode(rootNode, sb, 0);
                    return sb.ToString();
                }
                return "No root node found in DOM payload.";
            }
            catch (Exception ex)
            {
                return $"Error parsing DOM: {ex.Message}";
            }
        }
    }

    private void FormatDomNode(JsonNode node, System.Text.StringBuilder sb, int indent)
    {
        if (node is not JsonObject obj) return;
        
        string nodeName = (string?)obj["nodeName"] ?? "unknown";
        string nodeValue = (string?)obj["nodeValue"] ?? "";
        
        var attrsList = new List<string>();
        if (obj.TryGetPropertyValue("attributes", out var attrsNode) && attrsNode is JsonArray attrsArray)
        {
            for (int i = 0; i < attrsArray.Count; i += 2)
            {
                if (i + 1 < attrsArray.Count)
                {
                    attrsList.Add($"{attrsArray[i]}=\"{attrsArray[i+1]}\"");
                }
            }
        }
        
        string attrsStr = attrsList.Count > 0 ? " " + string.Join(" ", attrsList) : "";
        string indentStr = new string(' ', indent * 2);
        
        if (obj.TryGetPropertyValue("children", out var childrenNode) && childrenNode is JsonArray childrenArray && childrenArray.Count > 0)
        {
            sb.AppendLine($"{indentStr}<{nodeName}{attrsStr}>");
            foreach (var child in childrenArray)
            {
                if (child != null)
                {
                    FormatDomNode(child, sb, indent + 1);
                }
            }
            sb.AppendLine($"{indentStr}</{nodeName}>");
        }
        else
        {
            if (!string.IsNullOrEmpty(nodeValue))
            {
                sb.AppendLine($"{indentStr}<{nodeName}{attrsStr}>{nodeValue}</{nodeName}>");
            }
            else
            {
                sb.AppendLine($"{indentStr}<{nodeName}{attrsStr} />");
            }
        }
    }

    public List<string> SearchResults
    {
        get
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(SearchTerm) || string.IsNullOrEmpty(RawJsonData)) return results;
            try
            {
                var node = JsonNode.Parse(RawJsonData);
                if (node is JsonObject obj && obj.TryGetPropertyValue("root", out var rootNode) && rootNode != null)
                {
                    FindMatchingNodes(rootNode, SearchTerm, results);
                }
            }
            catch {}
            return results;
        }
    }

    private void FindMatchingNodes(JsonNode node, string term, List<string> results)
    {
        if (node is not JsonObject obj) return;
        string nodeName = (string?)obj["nodeName"] ?? "";
        string nodeId = (string?)obj["nodeId"] ?? "";
        
        var attrsList = new List<string>();
        bool matchesAttr = false;
        if (obj.TryGetPropertyValue("attributes", out var attrsNode) && attrsNode is JsonArray attrsArray)
        {
            for (int i = 0; i < attrsArray.Count; i += 2)
            {
                if (i + 1 < attrsArray.Count)
                {
                    string attrName = attrsArray[i]?.ToString() ?? "";
                    string attrValue = attrsArray[i+1]?.ToString() ?? "";
                    attrsList.Add($"{attrName}=\"{attrValue}\"");
                    if (attrValue.Contains(term, StringComparison.OrdinalIgnoreCase) || attrName.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                         matchesAttr = true;
                    }
                }
            }
        }

        if (nodeName.Contains(term, StringComparison.OrdinalIgnoreCase) || matchesAttr)
        {
            results.Add($"<{nodeName}{(attrsList.Count > 0 ? " " + string.Join(" ", attrsList) : "")}> (nodeId: {nodeId})");
        }

        if (obj.TryGetPropertyValue("children", out var childrenNode) && childrenNode is JsonArray childrenArray)
        {
            foreach (var child in childrenArray)
            {
                if (child != null)
                {
                    FindMatchingNodes(child, term, results);
                }
            }
        }
    }
}
