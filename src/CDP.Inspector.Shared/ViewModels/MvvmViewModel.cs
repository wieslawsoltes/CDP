using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CdpInspectorApp.Services;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.ViewModels;

public class InspectorPropertyModel : ViewModelBase
{
    private string _name = "";
    private string _type = "";
    private string _displayValue = "";
    private bool _isWritable;
    private readonly string _viewModelId;
    private readonly ICdpService _cdpService;

    public InspectorPropertyModel(string viewModelId, ICdpService cdpService)
    {
        _viewModelId = viewModelId;
        _cdpService = cdpService;
        UpdateValueCommand = new RelayCommand<string>(async (newVal) => await UpdateValueAsync(newVal));
    }

    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(ref _name, value);
    }

    public string Type
    {
        get => _type;
        set => RaiseAndSetIfChanged(ref _type, value);
    }

    public string DisplayValue
    {
        get => _displayValue;
        set => RaiseAndSetIfChanged(ref _displayValue, value);
    }

    public bool IsWritable
    {
        get => _isWritable;
        set => RaiseAndSetIfChanged(ref _isWritable, value);
    }

    public ICommand UpdateValueCommand { get; }

    private async Task UpdateValueAsync(string? newVal)
    {
        if (newVal == null || !_isWritable || !_cdpService.IsConnected) return;

        try
        {
            JsonNode? jsonVal;
            if (Type == "System.String")
            {
                jsonVal = JsonValue.Create(newVal);
            }
            else if (Type == "System.Boolean" && bool.TryParse(newVal, out var b))
            {
                jsonVal = JsonValue.Create(b);
            }
            else if ((Type == "System.Int32" || Type == "System.Int64") && long.TryParse(newVal, out var l))
            {
                jsonVal = JsonValue.Create(l);
            }
            else if ((Type == "System.Double" || Type == "System.Single" || Type == "System.Decimal") && double.TryParse(newVal, out var d))
            {
                jsonVal = JsonValue.Create(d);
            }
            else
            {
                jsonVal = JsonValue.Create(newVal);
            }

            await _cdpService.SendCommandAsync("Mvvm.setPropertyValue", new JsonObject
            {
                ["viewModelId"] = _viewModelId,
                ["propertyName"] = Name,
                ["value"] = jsonVal
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting property: {ex.Message}");
        }
    }
}

public class InspectorViewModelNode : ViewModelBase
{
    private bool _isExpanded;
    private string _id = "";
    private string _type = "";
    private string _controlType = "";
    private string _controlName = "";
    private ObservableCollection<InspectorPropertyModel> _properties = new();
    private ObservableCollection<InspectorViewModelNode> _children = new();

    public string Id
    {
        get => _id;
        set => RaiseAndSetIfChanged(ref _id, value);
    }

    public string Type
    {
        get => _type;
        set => RaiseAndSetIfChanged(ref _type, value);
    }

    public string ControlType
    {
        get => _controlType;
        set => RaiseAndSetIfChanged(ref _controlType, value);
    }

    public string ControlName
    {
        get => _controlName;
        set => RaiseAndSetIfChanged(ref _controlName, value);
    }

    public string DisplayName => string.IsNullOrEmpty(ControlName)
        ? $"{ControlType} ({Type.Substring(Type.LastIndexOf('.') + 1)})"
        : $"{ControlType} [Name='{ControlName}'] ({Type.Substring(Type.LastIndexOf('.') + 1)})";

    public ObservableCollection<InspectorPropertyModel> Properties
    {
        get => _properties;
        set => RaiseAndSetIfChanged(ref _properties, value);
    }

    public ObservableCollection<InspectorViewModelNode> Children
    {
        get => _children;
        set => RaiseAndSetIfChanged(ref _children, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => RaiseAndSetIfChanged(ref _isExpanded, value);
    }
}

public class CommandExecutedModel : ViewModelBase
{
    private string _viewModelId = "";
    private string _viewModelType = "";
    private string _commandName = "";
    private string _result = "";
    private string _timestamp = "";

    public string ViewModelId
    {
        get => _viewModelId;
        set => RaiseAndSetIfChanged(ref _viewModelId, value);
    }

    public string ViewModelType
    {
        get => _viewModelType;
        set => RaiseAndSetIfChanged(ref _viewModelType, value);
    }

    public string CommandName
    {
        get => _commandName;
        set => RaiseAndSetIfChanged(ref _commandName, value);
    }

    public string Result
    {
        get => _result;
        set => RaiseAndSetIfChanged(ref _result, value);
    }

    public string Timestamp
    {
        get => _timestamp;
        set => RaiseAndSetIfChanged(ref _timestamp, value);
    }
}

public class MvvmViewModel : ViewModelBase, IStateProvider
{
    private readonly ICdpService _cdpService;
    private InspectorViewModelNode? _selectedNode;
    private ObservableCollection<InspectorViewModelNode> _mvvmTree = new();
    private ObservableCollection<CommandExecutedModel> _commandHistory = new();

    public ObservableCollection<InspectorViewModelNode> MvvmTree
    {
        get => _mvvmTree;
        set => RaiseAndSetIfChanged(ref _mvvmTree, value);
    }

    public InspectorViewModelNode? SelectedNode
    {
        get => _selectedNode;
        set => RaiseAndSetIfChanged(ref _selectedNode, value);
    }

    public ObservableCollection<CommandExecutedModel> CommandHistory
    {
        get => _commandHistory;
        set => RaiseAndSetIfChanged(ref _commandHistory, value);
    }

    public ICommand RefreshTreeCommand { get; }
    public ICommand ClearHistoryCommand { get; }

    public MvvmViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService;
        _cdpService.PropertyChanged += CdpService_PropertyChanged;
        _cdpService.EventReceived += CdpService_EventReceived;

        RefreshTreeCommand = new RelayCommand(async () => await RefreshMvvmTreeAsync(), () => _cdpService.IsConnected);
        ClearHistoryCommand = new RelayCommand(() => CommandHistory.Clear());

        if (_cdpService.IsConnected)
        {
            _ = InitializeMvvmAsync();
        }
    }

    private void CdpService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService.IsConnected)
            {
                _ = InitializeMvvmAsync();
            }
            else
            {
                ClearData();
            }
            ((RelayCommand)RefreshTreeCommand).RaiseCanExecuteChanged();
        }
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (e.Method == "Mvvm.propertyChanged" && e.Params != null)
        {
            string vmId = e.Params["viewModelId"]?.GetValue<string>() ?? "";
            string propName = e.Params["propertyName"]?.GetValue<string>() ?? "";
            var valNode = e.Params["value"];
            string valStr = valNode == null ? "null" : valNode.ToString();

            Dispatcher.UIThread.Post(() => HandlePropertyChanged(vmId, propName, valStr));
        }
        else if (e.Method == "Mvvm.commandExecuted" && e.Params != null)
        {
            var historyItem = new CommandExecutedModel
            {
                ViewModelId = e.Params["viewModelId"]?.GetValue<string>() ?? "",
                ViewModelType = e.Params["viewModelType"]?.GetValue<string>() ?? "",
                CommandName = e.Params["commandName"]?.GetValue<string>() ?? "",
                Result = e.Params["result"]?.ToString() ?? "",
                Timestamp = e.Params["timestamp"]?.GetValue<string>() ?? DateTime.Now.ToString("T")
            };

            Dispatcher.UIThread.Post(() => CommandHistory.Insert(0, historyItem));
        }
    }

    private async Task InitializeMvvmAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("Mvvm.enable");
            await RefreshMvvmTreeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enabling MVVM: {ex.Message}");
        }
    }

    public async Task RefreshMvvmTreeAsync()
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            var result = await _cdpService.SendCommandAsync("Mvvm.getViewModelTree");
            var tree = result["tree"] as JsonArray;
            if (tree != null)
            {
                Dispatcher.UIThread.Post(() => UpdateTree(tree));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing MVVM tree: {ex.Message}");
        }
    }

    private void ClearData()
    {
        MvvmTree.Clear();
        SelectedNode = null;
        CommandHistory.Clear();
    }

    private InspectorViewModelNode? FindNodeById(IEnumerable<InspectorViewModelNode> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id) return node;
            var found = FindNodeById(node.Children, id);
            if (found != null) return found;
        }
        return null;
    }

    private void HandlePropertyChanged(string vmId, string propName, string displayVal)
    {
        var node = FindNodeById(MvvmTree, vmId);
        if (node != null)
        {
            foreach (var prop in node.Properties)
            {
                if (prop.Name == propName)
                {
                    prop.DisplayValue = displayVal;
                    break;
                }
            }
        }
    }

    private void UpdateTree(JsonArray treeArray)
    {
        var expandedIds = new HashSet<string>();
        GetExpandedIds(MvvmTree, expandedIds);

        string? selectedId = SelectedNode?.Id;

        MvvmTree.Clear();
        foreach (var item in treeArray)
        {
            if (item is JsonObject obj)
            {
                MvvmTree.Add(ParseNode(obj, expandedIds));
            }
        }

        if (selectedId != null)
        {
            SelectedNode = FindNodeById(MvvmTree, selectedId);
        }
    }

    private void GetExpandedIds(IEnumerable<InspectorViewModelNode> nodes, HashSet<string> expandedIds)
    {
        foreach (var node in nodes)
        {
            if (node.IsExpanded) expandedIds.Add(node.Id);
            GetExpandedIds(node.Children, expandedIds);
        }
    }

    private InspectorViewModelNode ParseNode(JsonObject obj, HashSet<string> expandedIds)
    {
        string id = obj["id"]?.GetValue<string>() ?? "";
        var node = new InspectorViewModelNode
        {
            Id = id,
            Type = obj["type"]?.GetValue<string>() ?? "",
            ControlType = obj["controlType"]?.GetValue<string>() ?? "",
            ControlName = obj["controlName"]?.GetValue<string>() ?? "",
            IsExpanded = expandedIds.Contains(id)
        };

        var props = obj["properties"] as JsonArray;
        if (props != null)
        {
            foreach (var propItem in props)
            {
                if (propItem is JsonObject pObj)
                {
                    var valNode = pObj["value"];
                    var valStr = valNode == null ? "null" : valNode.ToString();
                    node.Properties.Add(new InspectorPropertyModel(id, _cdpService)
                    {
                        Name = pObj["name"]?.GetValue<string>() ?? "",
                        Type = pObj["type"]?.GetValue<string>() ?? "",
                        DisplayValue = valStr,
                        IsWritable = pObj["isWritable"]?.GetValue<bool>() ?? false
                    });
                }
            }
        }

        var children = obj["children"] as JsonArray;
        if (children != null)
        {
            foreach (var childItem in children)
            {
                if (childItem is JsonObject cObj)
                {
                    node.Children.Add(ParseNode(cObj, expandedIds));
                }
            }
        }

        return node;
    }

    #region IStateProvider Implementation
    public string StateKey => "mvvm";

    public JsonNode? SaveState()
    {
        return null;
    }

    public void LoadState(JsonNode? stateNode)
    {
    }
    #endregion
}
