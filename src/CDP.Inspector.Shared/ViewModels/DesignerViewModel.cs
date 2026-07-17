using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using CdpInspectorApp.Services;
using CdpInspectorApp.Models;
using Chrome.DevTools.Protocol;
using Microsoft.Extensions.Logging;

namespace CdpInspectorApp.ViewModels;

/// <summary>
/// View model for the WYSIWYG Designer feature.
/// Provides overlay rendering, element selection, property editing,
/// and drag-and-drop toolbox integration for live UI editing.
/// </summary>
public class DesignerViewModel : ViewModelBase, IStateProvider
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<DesignerViewModel>();
    private readonly ICdpService _cdpService;
    private readonly Func<ElementsViewModel> _elementsFunc;
    private bool _isDesignModeActive;
    private bool _isOverlayVisible;
    private string? _selectedElementSelector;
    private double _selectedElementX;
    private double _selectedElementY;
    private double _selectedElementWidth;
    private double _selectedElementHeight;
    private double _selectedMarginLeft;
    private double _selectedMarginTop;
    private double _selectedMarginRight;
    private double _selectedMarginBottom;
    private double _selectedPaddingLeft;
    private double _selectedPaddingTop;
    private double _selectedPaddingRight;
    private double _selectedPaddingBottom;
    private string? _selectedHorizontalAlignment;
    private string? _selectedVerticalAlignment;
    private int _selectedNodeId;
    private string _detectedPlatform = "Avalonia";
    private string? _selectedToolboxCategory;

    // Container specific properties
    private int _gridRow;
    private int _gridColumn;
    private int _gridRowSpan = 1;
    private int _gridColumnSpan = 1;
    private double _canvasLeft;
    private double _canvasTop;
    private string _rowDefinitions = "";
    private string _columnDefinitions = "";
    private string? _selectedElementBadge;
    private string? _stackPanelOrientation;
    private string? _parentContainerType;
    private string? _selectedNodeName;

    public DesignerViewModel(ICdpService cdpService, Func<ElementsViewModel> elementsFunc)
    {
        _cdpService = cdpService;
        _elementsFunc = elementsFunc;

        SelectElementCommand = new RelayCommand<string>(async selector => await SelectElementAsync(selector));
        DeleteElementCommand = new RelayCommand(async () => await DeleteElementAsync());
        ApplyMarginCommand = new RelayCommand(async () => await ApplyMarginAsync());
        ApplyPaddingCommand = new RelayCommand(async () => await ApplyPaddingAsync());
        ApplySizeCommand = new RelayCommand(async () => await ApplySizeAsync());
        ApplyAlignmentCommand = new RelayCommand<string>(async alignment => await ApplyAlignmentAsync(alignment));
        DropElementCommand = new RelayCommand<string>(async xaml => await DropElementAsync(xaml));

        ApplyGridAttachmentCommand = new RelayCommand(async () => await ApplyGridAttachmentAsync());
        ApplyGridDefinitionsCommand = new RelayCommand(async () => await ApplyGridDefinitionsAsync());
        ApplyCanvasAttachmentCommand = new RelayCommand(async () => await ApplyCanvasAttachmentAsync());
        MoveChildUpCommand = new RelayCommand(async () => await MoveChildUpDownAsync(true));
        MoveChildDownCommand = new RelayCommand(async () => await MoveChildUpDownAsync(false));

        ToolboxCategories = new ObservableCollection<string>();
        ToolboxItems = new ObservableCollection<ToolboxItemViewModel>();

        InitializeToolbox();

        // Wire up Elements selected node changed notification
        Elements.PropertyChanged += async (s, e) =>
        {
            if (e.PropertyName == nameof(ElementsViewModel.SelectedNode))
            {
                await OnElementsSelectedNodeChangedAsync();
            }
        };
    }

    public string StateKey => "Designer";

    public JsonNode? SaveState()
    {
        var obj = new JsonObject
        {
            ["isDesignModeActive"] = _isDesignModeActive,
        };
        return obj;
    }

    public void LoadState(JsonNode? stateNode)
    {
        if (stateNode is JsonObject obj)
        {
            IsDesignModeActive = obj["isDesignModeActive"]?.GetValue<bool>() ?? false;
        }
    }

    public bool IsDesignModeActive
    {
        get => _isDesignModeActive;
        set => RaiseAndSetIfChanged(ref _isDesignModeActive, value);
    }

    public bool IsOverlayVisible
    {
        get => _isOverlayVisible;
        set => RaiseAndSetIfChanged(ref _isOverlayVisible, value);
    }

    public string? SelectedElementSelector
    {
        get => _selectedElementSelector;
        set => RaiseAndSetIfChanged(ref _selectedElementSelector, value);
    }

    public double SelectedElementX
    {
        get => _selectedElementX;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedElementX, value))
            {
                OnPropertyChanged(nameof(SelectedBounds));
            }
        }
    }

    public double SelectedElementY
    {
        get => _selectedElementY;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedElementY, value))
            {
                OnPropertyChanged(nameof(SelectedBounds));
            }
        }
    }

    public double SelectedElementWidth
    {
        get => _selectedElementWidth;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedElementWidth, value))
            {
                OnPropertyChanged(nameof(SelectedBounds));
            }
        }
    }

    public double SelectedElementHeight
    {
        get => _selectedElementHeight;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedElementHeight, value))
            {
                OnPropertyChanged(nameof(SelectedBounds));
            }
        }
    }

    public double SelectedMarginLeft
    {
        get => _selectedMarginLeft;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedMarginLeft, value))
            {
                OnPropertyChanged(nameof(SelectedMarginThickness));
            }
        }
    }

    public double SelectedMarginTop
    {
        get => _selectedMarginTop;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedMarginTop, value))
            {
                OnPropertyChanged(nameof(SelectedMarginThickness));
            }
        }
    }

    public double SelectedMarginRight
    {
        get => _selectedMarginRight;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedMarginRight, value))
            {
                OnPropertyChanged(nameof(SelectedMarginThickness));
            }
        }
    }

    public double SelectedMarginBottom
    {
        get => _selectedMarginBottom;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedMarginBottom, value))
            {
                OnPropertyChanged(nameof(SelectedMarginThickness));
            }
        }
    }

    public double SelectedPaddingLeft
    {
        get => _selectedPaddingLeft;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedPaddingLeft, value))
            {
                OnPropertyChanged(nameof(SelectedPaddingThickness));
            }
        }
    }

    public double SelectedPaddingTop
    {
        get => _selectedPaddingTop;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedPaddingTop, value))
            {
                OnPropertyChanged(nameof(SelectedPaddingThickness));
            }
        }
    }

    public double SelectedPaddingRight
    {
        get => _selectedPaddingRight;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedPaddingRight, value))
            {
                OnPropertyChanged(nameof(SelectedPaddingThickness));
            }
        }
    }

    public double SelectedPaddingBottom
    {
        get => _selectedPaddingBottom;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedPaddingBottom, value))
            {
                OnPropertyChanged(nameof(SelectedPaddingThickness));
            }
        }
    }

    public string? SelectedHorizontalAlignment
    {
        get => _selectedHorizontalAlignment;
        set => RaiseAndSetIfChanged(ref _selectedHorizontalAlignment, value);
    }

    public string? SelectedVerticalAlignment
    {
        get => _selectedVerticalAlignment;
        set => RaiseAndSetIfChanged(ref _selectedVerticalAlignment, value);
    }

    public int SelectedNodeId
    {
        get => _selectedNodeId;
        set => RaiseAndSetIfChanged(ref _selectedNodeId, value);
    }

    public string DetectedPlatform
    {
        get => _detectedPlatform;
        set
        {
            if (RaiseAndSetIfChanged(ref _detectedPlatform, value))
            {
                InitializeToolbox();
            }
        }
    }

    public string? SelectedToolboxCategory
    {
        get => _selectedToolboxCategory;
        set => RaiseAndSetIfChanged(ref _selectedToolboxCategory, value);
    }

    // Container Specific Getters/Setters
    public int GridRow
    {
        get => _gridRow;
        set => RaiseAndSetIfChanged(ref _gridRow, value);
    }

    public int GridColumn
    {
        get => _gridColumn;
        set => RaiseAndSetIfChanged(ref _gridColumn, value);
    }

    public int GridRowSpan
    {
        get => _gridRowSpan;
        set => RaiseAndSetIfChanged(ref _gridRowSpan, value);
    }

    public int GridColumnSpan
    {
        get => _gridColumnSpan;
        set => RaiseAndSetIfChanged(ref _gridColumnSpan, value);
    }

    public double CanvasLeft
    {
        get => _canvasLeft;
        set => RaiseAndSetIfChanged(ref _canvasLeft, value);
    }

    public double CanvasTop
    {
        get => _canvasTop;
        set => RaiseAndSetIfChanged(ref _canvasTop, value);
    }

    public string RowDefinitions
    {
        get => _rowDefinitions;
        set => RaiseAndSetIfChanged(ref _rowDefinitions, value);
    }

    public string ColumnDefinitions
    {
        get => _columnDefinitions;
        set => RaiseAndSetIfChanged(ref _columnDefinitions, value);
    }

    public string? SelectedElementBadge
    {
        get => _selectedElementBadge;
        set => RaiseAndSetIfChanged(ref _selectedElementBadge, value);
    }

    public string? StackPanelOrientation
    {
        get => _stackPanelOrientation;
        set => RaiseAndSetIfChanged(ref _stackPanelOrientation, value);
    }

    public string? ParentContainerType
    {
        get => _parentContainerType;
        set
        {
            if (RaiseAndSetIfChanged(ref _parentContainerType, value))
            {
                OnPropertyChanged(nameof(ShowGridAttachmentEditor));
                OnPropertyChanged(nameof(ShowStackPanelReorderEditor));
                OnPropertyChanged(nameof(ShowCanvasAttachmentEditor));
            }
        }
    }

    public string? SelectedNodeName
    {
        get => _selectedNodeName;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedNodeName, value))
            {
                OnPropertyChanged(nameof(ShowGridDefinitionsEditor));
            }
        }
    }

    public bool ShowGridDefinitionsEditor => SelectedNodeName == "Grid";
    public bool ShowGridAttachmentEditor => ParentContainerType == "Grid";
    public bool ShowStackPanelReorderEditor => ParentContainerType == "StackPanel";
    public bool ShowCanvasAttachmentEditor => ParentContainerType == "Canvas";

    public Rect SelectedBounds => new Rect(SelectedElementX, SelectedElementY, SelectedElementWidth, SelectedElementHeight);
    public Thickness SelectedMarginThickness => new Thickness(SelectedMarginLeft, SelectedMarginTop, SelectedMarginRight, SelectedMarginBottom);
    public Thickness SelectedPaddingThickness => new Thickness(SelectedPaddingLeft, SelectedPaddingTop, SelectedPaddingRight, SelectedPaddingBottom);

    public System.Collections.Generic.IEnumerable<Rect> MultiSelectedBounds => 
        SelectedElements.Select(x => x.Bounds).ToList();

    public ObservableCollection<SelectedElementInfo> SelectedElements { get; } = new();
    public ObservableCollection<BreadcrumbItemViewModel> Breadcrumbs { get; } = new();

    public ObservableCollection<string> ToolboxCategories { get; }
    public ObservableCollection<ToolboxItemViewModel> ToolboxItems { get; }

    public ElementsViewModel Elements => _elementsFunc();

    // Commands
    public ICommand SelectElementCommand { get; }
    public ICommand DeleteElementCommand { get; }
    public ICommand ApplyMarginCommand { get; }
    public ICommand ApplyPaddingCommand { get; }
    public ICommand ApplySizeCommand { get; }
    public ICommand ApplyAlignmentCommand { get; }
    public ICommand DropElementCommand { get; }

    public ICommand ApplyGridAttachmentCommand { get; }
    public ICommand ApplyGridDefinitionsCommand { get; }
    public ICommand ApplyCanvasAttachmentCommand { get; }
    public ICommand MoveChildUpCommand { get; }
    public ICommand MoveChildDownCommand { get; }

    public void InitializeToolbox()
    {
        ToolboxItems.Clear();
        ToolboxCategories.Clear();

        var controls = CDP.Inspector.Wysiwyg.Models.ToolboxCatalog.GetControlsForPlatform(DetectedPlatform);
        var categories = new HashSet<string>();

        foreach (var ctrl in controls)
        {
            categories.Add(ctrl.Category);
            ToolboxItems.Add(new ToolboxItemViewModel
            {
                Name = ctrl.Name,
                Category = ctrl.Category,
                DefaultXaml = ctrl.DefaultXaml
            });
        }

        foreach (var cat in categories)
        {
            ToolboxCategories.Add(cat);
        }

        if (ToolboxCategories.Count > 0)
        {
            SelectedToolboxCategory = ToolboxCategories[0];
        }
    }

    private void TriggerWorkspaceSync()
    {
        if (MainWindowViewModel.Instance?.Sources != null)
        {
            _ = MainWindowViewModel.Instance.Sources.RefreshSelectedFileContentAsync();
        }
    }

    private void UpdateBreadcrumbs(DomNodeModel? node)
    {
        Breadcrumbs.Clear();
        if (node == null) return;

        var list = new List<BreadcrumbItemViewModel>();
        var current = node;
        var useAutomation = MainWindowViewModel.Instance?.Connection?.UseAutomationSelectors == true;
        var generator = ClientSelectorRegistry.GetGenerator(useAutomation ? "automation" : "dom");

        while (current != null)
        {
            if (current.NodeName != "#document")
            {
                var selector = generator.GenerateSelector(current) ?? "";
                list.Insert(0, new BreadcrumbItemViewModel
                {
                    Name = current.DisplayName ?? current.NodeName,
                    NodeId = current.NodeId,
                    Selector = selector
                });
            }
            current = current.Parent;
        }

        foreach (var item in list)
        {
            Breadcrumbs.Add(item);
        }
    }

    private async Task OnElementsSelectedNodeChangedAsync()
    {
        var node = Elements.SelectedNode;
        if (node == null)
        {
            IsOverlayVisible = false;
            SelectedNodeId = 0;
            SelectedElementSelector = null;
            SelectedElements.Clear();
            Breadcrumbs.Clear();
            ParentContainerType = null;
            SelectedNodeName = null;
            SelectedElementBadge = null;
            StackPanelOrientation = null;
            OnPropertyChanged(nameof(MultiSelectedBounds));
            return;
        }

        var useAutomation = MainWindowViewModel.Instance?.Connection?.UseAutomationSelectors == true;
        var generator = ClientSelectorRegistry.GetGenerator(useAutomation ? "automation" : "dom");
        var selector = generator.GenerateSelector(node);
        if (string.IsNullOrEmpty(selector)) return;

        SelectedElementSelector = selector;
        SelectedNodeId = node.NodeId;
        SelectedNodeName = node.NodeName;
        ParentContainerType = node.Parent?.NodeName;

        if (SelectedElements.All(x => x.NodeId != node.NodeId))
        {
            SelectedElements.Clear();
            var info = new SelectedElementInfo { NodeId = node.NodeId, Selector = selector };
            SelectedElements.Add(info);
        }

        // Accessibility Badge
        var badge = node.AttributesList.FirstOrDefault(a => 
            a.Name.Equals("AutomationProperties.AutomationId", StringComparison.OrdinalIgnoreCase) || 
            a.Name.Equals("AutomationId", StringComparison.OrdinalIgnoreCase) || 
            a.Name.Equals("AccessibilityId", StringComparison.OrdinalIgnoreCase))?.Value;

        if (string.IsNullOrEmpty(badge))
        {
            badge = node.AttributesList.FirstOrDefault(a => 
                a.Name.Equals("Name", StringComparison.OrdinalIgnoreCase) || 
                a.Name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))?.Value;
        }

        if (string.IsNullOrEmpty(badge))
        {
            badge = node.NodeName;
        }
        SelectedElementBadge = badge;

        // StackPanel Orientation
        string? orientation = null;
        if (node.NodeName == "StackPanel")
        {
            orientation = node.AttributesList.FirstOrDefault(a => a.Name.Equals("Orientation", StringComparison.OrdinalIgnoreCase))?.Value;
        }
        else if (node.Parent?.NodeName == "StackPanel")
        {
            orientation = node.Parent.AttributesList.FirstOrDefault(a => a.Name.Equals("Orientation", StringComparison.OrdinalIgnoreCase))?.Value;
        }
        if (orientation == null && (node.NodeName == "StackPanel" || node.Parent?.NodeName == "StackPanel"))
        {
            orientation = "Vertical";
        }
        StackPanelOrientation = orientation;

        // Container Attributes (Grid / Canvas / StackPanel)
        var rowVal = node.AttributesList.FirstOrDefault(a => a.Name.Equals("Grid.Row", StringComparison.OrdinalIgnoreCase))?.Value;
        var colVal = node.AttributesList.FirstOrDefault(a => a.Name.Equals("Grid.Column", StringComparison.OrdinalIgnoreCase))?.Value;
        var rowSpanVal = node.AttributesList.FirstOrDefault(a => a.Name.Equals("Grid.RowSpan", StringComparison.OrdinalIgnoreCase))?.Value;
        var colSpanVal = node.AttributesList.FirstOrDefault(a => a.Name.Equals("Grid.ColumnSpan", StringComparison.OrdinalIgnoreCase))?.Value;
        var leftVal = node.AttributesList.FirstOrDefault(a => a.Name.Equals("Canvas.Left", StringComparison.OrdinalIgnoreCase))?.Value;
        var topVal = node.AttributesList.FirstOrDefault(a => a.Name.Equals("Canvas.Top", StringComparison.OrdinalIgnoreCase))?.Value;
        var rowDefs = node.AttributesList.FirstOrDefault(a => a.Name.Equals("RowDefinitions", StringComparison.OrdinalIgnoreCase))?.Value;
        var colDefs = node.AttributesList.FirstOrDefault(a => a.Name.Equals("ColumnDefinitions", StringComparison.OrdinalIgnoreCase))?.Value;

        GridRow = int.TryParse(rowVal, out var r) ? r : 0;
        GridColumn = int.TryParse(colVal, out var c) ? c : 0;
        GridRowSpan = int.TryParse(rowSpanVal, out var rs) ? rs : 1;
        GridColumnSpan = int.TryParse(colSpanVal, out var cs) ? cs : 1;
        CanvasLeft = double.TryParse(leftVal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cl) ? cl : 0;
        CanvasTop = double.TryParse(topVal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ct) ? ct : 0;
        RowDefinitions = rowDefs ?? "";
        ColumnDefinitions = colDefs ?? "";

        await RefreshElementOverlayAndStylesAsync(node.NodeId, selector);
        UpdateBreadcrumbs(node);
    }

    private async Task RefreshElementOverlayAndStylesAsync(int nodeId, string selector)
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            var boxResult = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject
            {
                ["nodeId"] = nodeId
            });

            var model = boxResult["model"];
            if (model != null)
            {
                var border = model["border"] ?? model["content"];
                if (border is JsonArray borderArray && borderArray.Count >= 8)
                {
                    var x = borderArray[0]?.GetValue<double>() ?? 0;
                    var y = borderArray[1]?.GetValue<double>() ?? 0;
                    var x2 = borderArray[2]?.GetValue<double>() ?? 0;
                    var y2 = borderArray[5]?.GetValue<double>() ?? 0;

                    if (nodeId == SelectedNodeId)
                    {
                        _selectedElementX = x;
                        _selectedElementY = y;
                        _selectedElementWidth = x2 - x;
                        _selectedElementHeight = y2 - y;
                        OnPropertyChanged(nameof(SelectedElementX));
                        OnPropertyChanged(nameof(SelectedElementY));
                        OnPropertyChanged(nameof(SelectedElementWidth));
                        OnPropertyChanged(nameof(SelectedElementHeight));
                        OnPropertyChanged(nameof(SelectedBounds));
                    }

                    var target = SelectedElements.FirstOrDefault(el => el.NodeId == nodeId);
                    if (target != null)
                    {
                        target.Bounds = new Rect(x, y, x2 - x, y2 - y);
                    }
                }
            }

            var styleResult = await _cdpService.SendCommandAsync("CSS.getComputedStyleForNode", new JsonObject
            {
                ["nodeId"] = nodeId
            });

            var computedStyle = styleResult["computedStyle"];
            if (computedStyle is JsonArray styleArray)
            {
                Thickness margin = default;
                Thickness padding = default;

                foreach (var item in styleArray)
                {
                    var name = item?["name"]?.GetValue<string>();
                    var value = item?["value"]?.GetValue<string>();
                    if (name == null || value == null) continue;

                    if (double.TryParse(
                        value.Replace("px", ""),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var numVal))
                    {
                        if (nodeId == SelectedNodeId)
                        {
                            switch (name)
                            {
                                case "margin-left": _selectedMarginLeft = numVal; OnPropertyChanged(nameof(SelectedMarginLeft)); break;
                                case "margin-top": _selectedMarginTop = numVal; OnPropertyChanged(nameof(SelectedMarginTop)); break;
                                case "margin-right": _selectedMarginRight = numVal; OnPropertyChanged(nameof(SelectedMarginRight)); break;
                                case "margin-bottom": _selectedMarginBottom = numVal; OnPropertyChanged(nameof(SelectedMarginBottom)); break;
                                case "padding-left": _selectedPaddingLeft = numVal; OnPropertyChanged(nameof(SelectedPaddingLeft)); break;
                                case "padding-top": _selectedPaddingTop = numVal; OnPropertyChanged(nameof(SelectedPaddingTop)); break;
                                case "padding-right": _selectedPaddingRight = numVal; OnPropertyChanged(nameof(SelectedPaddingRight)); break;
                                case "padding-bottom": _selectedPaddingBottom = numVal; OnPropertyChanged(nameof(SelectedPaddingBottom)); break;
                            }
                        }

                        switch (name)
                        {
                            case "margin-left": margin = new Thickness(numVal, margin.Top, margin.Right, margin.Bottom); break;
                            case "margin-top": margin = new Thickness(margin.Left, numVal, margin.Right, margin.Bottom); break;
                            case "margin-right": margin = new Thickness(margin.Left, margin.Top, numVal, margin.Bottom); break;
                            case "margin-bottom": margin = new Thickness(margin.Left, margin.Top, margin.Right, numVal); break;
                            case "padding-left": padding = new Thickness(numVal, padding.Top, padding.Right, padding.Bottom); break;
                            case "padding-top": padding = new Thickness(padding.Left, numVal, padding.Right, padding.Bottom); break;
                            case "padding-right": padding = new Thickness(padding.Left, padding.Top, numVal, padding.Bottom); break;
                            case "padding-bottom": padding = new Thickness(padding.Left, padding.Top, padding.Right, numVal); break;
                        }
                    }

                    if (nodeId == SelectedNodeId)
                    {
                        switch (name)
                        {
                            case "horizontal-alignment": SelectedHorizontalAlignment = value; break;
                            case "vertical-alignment": SelectedVerticalAlignment = value; break;
                        }
                    }
                }

                if (nodeId == SelectedNodeId)
                {
                    OnPropertyChanged(nameof(SelectedMarginThickness));
                    OnPropertyChanged(nameof(SelectedPaddingThickness));
                }

                var target = SelectedElements.FirstOrDefault(el => el.NodeId == nodeId);
                if (target != null)
                {
                    target.Margin = margin;
                    target.Padding = padding;
                }
            }

            IsOverlayVisible = true;
        }
        catch
        {
            IsOverlayVisible = false;
        }
    }

    public async Task RefreshAllSelectedElementsAsync()
    {
        foreach (var el in SelectedElements.ToList())
        {
            await RefreshElementOverlayAndStylesAsync(el.NodeId, el.Selector);
        }
        OnPropertyChanged(nameof(MultiSelectedBounds));
    }

    public async Task SelectElementAsync(string selector)
    {
        Console.WriteLine($"[DEBUG] SelectElementAsync starting for selector: {selector}. IsConnected: {_cdpService.IsConnected}");
        if (string.IsNullOrEmpty(selector) || !_cdpService.IsConnected)
            return;

        SelectedElementSelector = selector;

        try
        {
            var docResult = await _cdpService.SendCommandAsync("DOM.getDocument", new JsonObject
            {
                ["depth"] = 0
            });
            var rootNodeId = docResult["root"]?["nodeId"]?.GetValue<int>() ?? 0;
            Console.WriteLine($"[DEBUG] docResult rootNodeId: {rootNodeId}");
            if (rootNodeId == 0) return;

            var queryResult = await _cdpService.SendCommandAsync("DOM.querySelector", new JsonObject
            {
                ["nodeId"] = rootNodeId,
                ["selector"] = selector
            });
            var nodeId = queryResult["nodeId"]?.GetValue<int>() ?? 0;
            Console.WriteLine($"[DEBUG] queryResult nodeId: {nodeId}");
            if (nodeId == 0) return;

            SelectedNodeId = nodeId;

            // Make sure Elements ViewModel is in sync
            Elements.SelectNodeById(nodeId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] SelectElementAsync exception: {ex.Message}\n{ex.StackTrace}");
            IsOverlayVisible = false;
            Logger.LogErrorMessage("DesignerVM", $"SelectElementAsync failed for selector {selector}", ex);
        }
    }

    public async Task SelectElementAtLocationAsync(double x, double y, bool ctrlPressed)
    {
        if (!_cdpService.IsConnected) return;

        try
        {
            var nodeRes = await _cdpService.SendCommandAsync("DOM.getNodeForLocation", new JsonObject
            {
                ["x"] = (int)x,
                ["y"] = (int)y
            });
            var nodeId = nodeRes["nodeId"]?.GetValue<int>() ?? 0;
            if (nodeId > 0)
            {
                var node = Elements.FindDomNode(nodeId);
                if (node != null)
                {
                    var useAutomation = MainWindowViewModel.Instance?.Connection?.UseAutomationSelectors == true;
                    var generator = ClientSelectorRegistry.GetGenerator(useAutomation ? "automation" : "dom");
                    var selector = generator.GenerateSelector(node);
                    if (!string.IsNullOrEmpty(selector))
                    {
                        if (ctrlPressed)
                        {
                            var existing = SelectedElements.FirstOrDefault(el => el.NodeId == nodeId);
                            if (existing != null)
                            {
                                SelectedElements.Remove(existing);
                                if (SelectedElements.Count > 0)
                                {
                                    var primary = SelectedElements.Last();
                                    await SelectElementAsync(primary.Selector);
                                }
                                else
                                {
                                    IsOverlayVisible = false;
                                    SelectedNodeId = 0;
                                    SelectedElementSelector = null;
                                }
                            }
                            else
                            {
                                var info = new SelectedElementInfo { NodeId = nodeId, Selector = selector };
                                SelectedElements.Add(info);
                                await SelectElementAsync(selector);
                            }
                        }
                        else
                        {
                            SelectedElements.Clear();
                            var info = new SelectedElementInfo { NodeId = nodeId, Selector = selector };
                            SelectedElements.Add(info);
                            await SelectElementAsync(selector);
                        }
                        OnPropertyChanged(nameof(MultiSelectedBounds));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("DesignerVM", $"Error selecting element at ({x}, {y})", ex);
        }
    }

    public void UpdateSelectedBoundsRealTime(double x, double y, double w, double h)
    {
        _selectedElementX = x;
        _selectedElementY = y;
        _selectedElementWidth = w;
        _selectedElementHeight = h;
        OnPropertyChanged(nameof(SelectedElementX));
        OnPropertyChanged(nameof(SelectedElementY));
        OnPropertyChanged(nameof(SelectedElementWidth));
        OnPropertyChanged(nameof(SelectedElementHeight));
        OnPropertyChanged(nameof(SelectedBounds));

        var primary = SelectedElements.FirstOrDefault(el => el.NodeId == SelectedNodeId);
        if (primary != null)
        {
            primary.Bounds = new Rect(x, y, w, h);
        }
        OnPropertyChanged(nameof(MultiSelectedBounds));
    }

    public async Task ApplyDragFinishedAsync()
    {
        if (SelectedNodeId == 0 || !_cdpService.IsConnected) return;

        try
        {
            if (SelectedElementWidth > 0)
            {
                await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
                {
                    ["nodeId"] = SelectedNodeId,
                    ["name"] = "Width",
                    ["value"] = SelectedElementWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            }
            if (SelectedElementHeight > 0)
            {
                await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
                {
                    ["nodeId"] = SelectedNodeId,
                    ["name"] = "Height",
                    ["value"] = SelectedElementHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            }

            if (ShowCanvasAttachmentEditor)
            {
                await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
                {
                    ["nodeId"] = SelectedNodeId,
                    ["name"] = "Canvas.Left",
                    ["value"] = CanvasLeft.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
                await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
                {
                    ["nodeId"] = SelectedNodeId,
                    ["name"] = "Canvas.Top",
                    ["value"] = CanvasTop.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            }
            else
            {
                var marginValue = FormattableString.Invariant(
                    $"{SelectedMarginLeft},{SelectedMarginTop},{SelectedMarginRight},{SelectedMarginBottom}");
                await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
                {
                    ["nodeId"] = SelectedNodeId,
                    ["name"] = "Margin",
                    ["value"] = marginValue
                });
            }

            await Task.Delay(200);
            await RefreshAllSelectedElementsAsync();
            TriggerWorkspaceSync();
        }
        catch { }
    }

    private async Task DeleteElementAsync()
    {
        if (SelectedNodeId == 0 || !_cdpService.IsConnected)
            return;

        try
        {
            await _cdpService.SendCommandAsync("DOM.removeNode", new JsonObject
            {
                ["nodeId"] = SelectedNodeId
            });

            IsOverlayVisible = false;
            SelectedNodeId = 0;
            SelectedElementSelector = null;
            SelectedElements.Clear();
            OnPropertyChanged(nameof(MultiSelectedBounds));
            TriggerWorkspaceSync();
        }
        catch
        {
            // Mutation failed
        }
    }

    private async Task ApplyMarginAsync()
    {
        if (SelectedElements.Count == 0 || !_cdpService.IsConnected)
            return;

        var tasks = SelectedElements.Select(async element =>
        {
            var marginValue = FormattableString.Invariant(
                $"{SelectedMarginLeft},{SelectedMarginTop},{SelectedMarginRight},{SelectedMarginBottom}");
            try
            {
                await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
                {
                    ["nodeId"] = element.NodeId,
                    ["name"] = "Margin",
                    ["value"] = marginValue
                });
            }
            catch { }
        });

        await Task.WhenAll(tasks);
        await RefreshAllSelectedElementsAsync();
        TriggerWorkspaceSync();
    }

    private async Task ApplyPaddingAsync()
    {
        if (SelectedElements.Count == 0 || !_cdpService.IsConnected)
            return;

        var tasks = SelectedElements.Select(async element =>
        {
            var paddingValue = FormattableString.Invariant(
                $"{SelectedPaddingLeft},{SelectedPaddingTop},{SelectedPaddingRight},{SelectedPaddingBottom}");
            try
            {
                await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
                {
                    ["nodeId"] = element.NodeId,
                    ["name"] = "Padding",
                    ["value"] = paddingValue
                });
            }
            catch { }
        });

        await Task.WhenAll(tasks);
        await RefreshAllSelectedElementsAsync();
        TriggerWorkspaceSync();
    }

    private async Task ApplySizeAsync()
    {
        if (SelectedElements.Count == 0 || !_cdpService.IsConnected)
            return;

        var tasks = SelectedElements.Select(async element =>
        {
            try
            {
                if (SelectedElementWidth > 0)
                {
                    await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
                    {
                        ["nodeId"] = element.NodeId,
                        ["name"] = "Width",
                        ["value"] = SelectedElementWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });
                }

                if (SelectedElementHeight > 0)
                {
                    await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
                    {
                        ["nodeId"] = element.NodeId,
                        ["name"] = "Height",
                        ["value"] = SelectedElementHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });
                }
            }
            catch { }
        });

        await Task.WhenAll(tasks);
        await RefreshAllSelectedElementsAsync();
        TriggerWorkspaceSync();
    }

    private async Task ApplyAlignmentAsync(string alignment)
    {
        if (string.IsNullOrEmpty(alignment) || SelectedNodeId == 0 || !_cdpService.IsConnected)
            return;

        var parts = alignment.Split(':');
        if (parts.Length != 2) return;

        var attrName = parts[0] == "H" ? "HorizontalAlignment" : "VerticalAlignment";
        var attrValue = parts[1];

        try
        {
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNodeId,
                ["name"] = attrName,
                ["value"] = attrValue
            });

            if (parts[0] == "H")
                SelectedHorizontalAlignment = attrValue;
            else
                SelectedVerticalAlignment = attrValue;

            await RefreshAllSelectedElementsAsync();
            TriggerWorkspaceSync();
        }
        catch
        {
            // Mutation failed
        }
    }

    private async Task DropElementAsync(string xamlFragment)
    {
        if (string.IsNullOrEmpty(xamlFragment) || SelectedNodeId == 0 || !_cdpService.IsConnected)
            return;

        try
        {
            var outerResult = await _cdpService.SendCommandAsync("DOM.getOuterHTML", new JsonObject
            {
                ["nodeId"] = SelectedNodeId
            });

            var currentHtml = outerResult["outerHTML"]?.GetValue<string>();
            if (string.IsNullOrEmpty(currentHtml))
                return;

            var closingTagIndex = currentHtml.LastIndexOf("</", StringComparison.Ordinal);
            string newHtml;
            if (closingTagIndex >= 0)
            {
                newHtml = currentHtml.Insert(closingTagIndex, xamlFragment);
            }
            else
            {
                var selfClose = currentHtml.LastIndexOf("/>", StringComparison.Ordinal);
                if (selfClose >= 0)
                {
                    var tagEnd = currentHtml.IndexOfAny(new[] { ' ', '/' }, 1);
                    var tagName = currentHtml.Substring(1, tagEnd - 1);
                    newHtml = currentHtml.Substring(0, selfClose) + ">" + xamlFragment + "</" + tagName + ">";
                }
                else
                {
                    return;
                }
            }

            await _cdpService.SendCommandAsync("DOM.setOuterHTML", new JsonObject
            {
                ["nodeId"] = SelectedNodeId,
                ["outerHTML"] = newHtml
            });

            TriggerWorkspaceSync();
        }
        catch
        {
            // Mutation failed
        }
    }

    public async Task DropElementAtLocationAsync(string xamlFragment, double x, double y)
    {
        if (string.IsNullOrEmpty(xamlFragment) || !_cdpService.IsConnected)
            return;

        try
        {
            var nodeRes = await _cdpService.SendCommandAsync("DOM.getNodeForLocation", new JsonObject
            {
                ["x"] = (int)x,
                ["y"] = (int)y
            });
            var nodeId = nodeRes["nodeId"]?.GetValue<int>() ?? 0;
            if (nodeId == 0) return;

            var node = Elements.FindDomNode(nodeId);
            if (node == null) return;

            var containers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Grid", "StackPanel", "Canvas", "Border", "DockPanel", "WrapPanel", "ScrollViewer", "TabControl", "Expander"
            };

            var containerNode = node;
            while (containerNode != null && !containers.Contains(containerNode.NodeName))
            {
                containerNode = containerNode.Parent;
            }

            if (containerNode == null) return;

            if (containerNode.NodeName.Equals("Canvas", StringComparison.OrdinalIgnoreCase))
            {
                var boxResult = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject
                {
                    ["nodeId"] = containerNode.NodeId
                });

                var model = boxResult["model"];
                double canvasX = 0;
                double canvasY = 0;
                if (model != null)
                {
                    var content = model["content"];
                    if (content is JsonArray contentArray && contentArray.Count >= 8)
                    {
                        canvasX = contentArray[0]?.GetValue<double>() ?? 0;
                        canvasY = contentArray[1]?.GetValue<double>() ?? 0;
                    }
                }

                double localX = x - canvasX;
                double localY = y - canvasY;

                double snapX = Math.Round(localX / 8.0) * 8.0;
                double snapY = Math.Round(localY / 8.0) * 8.0;

                var trimmedFragment = xamlFragment.Trim();
                if (trimmedFragment.StartsWith("<"))
                {
                    var tagEnd = trimmedFragment.IndexOfAny(new[] { ' ', '/', '>' }, 1);
                    if (tagEnd > 0)
                    {
                        var leftAttr = $" Canvas.Left=\"{snapX.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"";
                        var topAttr = $" Canvas.Top=\"{snapY.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"";
                        var injection = leftAttr + topAttr;
                        xamlFragment = trimmedFragment.Insert(tagEnd, injection);
                    }
                }
            }

            var outerResult = await _cdpService.SendCommandAsync("DOM.getOuterHTML", new JsonObject
            {
                ["nodeId"] = containerNode.NodeId
            });

            var currentHtml = outerResult["outerHTML"]?.GetValue<string>();
            if (string.IsNullOrEmpty(currentHtml))
                return;

            var closingTagIndex = currentHtml.LastIndexOf("</", StringComparison.Ordinal);
            string newHtml;
            if (closingTagIndex >= 0)
            {
                newHtml = currentHtml.Insert(closingTagIndex, xamlFragment);
            }
            else
            {
                var selfClose = currentHtml.LastIndexOf("/>", StringComparison.Ordinal);
                if (selfClose >= 0)
                {
                    var tagEnd = currentHtml.IndexOfAny(new[] { ' ', '/' }, 1);
                    var tagName = currentHtml.Substring(1, tagEnd - 1);
                    newHtml = currentHtml.Substring(0, selfClose) + ">" + xamlFragment + "</" + tagName + ">";
                }
                else
                {
                    return;
                }
            }

            await _cdpService.SendCommandAsync("DOM.setOuterHTML", new JsonObject
            {
                ["nodeId"] = containerNode.NodeId,
                ["outerHTML"] = newHtml
            });

            TriggerWorkspaceSync();

            var useAutomation = MainWindowViewModel.Instance?.Connection?.UseAutomationSelectors == true;
            var generator = ClientSelectorRegistry.GetGenerator(useAutomation ? "automation" : "dom");
            var containerSelector = generator.GenerateSelector(containerNode);
            if (!string.IsNullOrEmpty(containerSelector))
            {
                await SelectElementAsync(containerSelector);
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorMessage("DesignerVM", $"Error dropping element at location ({x}, {y})", ex);
        }
    }

    public async Task ApplyGridAttachmentAsync()
    {
        if (SelectedNodeId == 0 || !_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNodeId,
                ["name"] = "Grid.Row",
                ["value"] = GridRow.ToString()
            });
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNodeId,
                ["name"] = "Grid.Column",
                ["value"] = GridColumn.ToString()
            });
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNodeId,
                ["name"] = "Grid.RowSpan",
                ["value"] = GridRowSpan.ToString()
            });
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNodeId,
                ["name"] = "Grid.ColumnSpan",
                ["value"] = GridColumnSpan.ToString()
            });
            
            await RefreshAllSelectedElementsAsync();
            TriggerWorkspaceSync();
        }
        catch { }
    }

    public async Task ApplyGridDefinitionsAsync()
    {
        if (SelectedNodeId == 0 || !_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNodeId,
                ["name"] = "RowDefinitions",
                ["value"] = RowDefinitions
            });
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNodeId,
                ["name"] = "ColumnDefinitions",
                ["value"] = ColumnDefinitions
            });
            
            await RefreshAllSelectedElementsAsync();
            TriggerWorkspaceSync();
        }
        catch { }
    }

    public async Task ApplyCanvasAttachmentAsync()
    {
        if (SelectedNodeId == 0 || !_cdpService.IsConnected) return;
        try
        {
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNodeId,
                ["name"] = "Canvas.Left",
                ["value"] = CanvasLeft.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNodeId,
                ["name"] = "Canvas.Top",
                ["value"] = CanvasTop.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
            
            await RefreshAllSelectedElementsAsync();
            TriggerWorkspaceSync();
        }
        catch { }
    }

    public async Task MoveChildUpDownAsync(bool up)
    {
        var node = Elements.SelectedNode;
        var parentNode = node?.Parent;
        if (node == null || parentNode == null || !_cdpService.IsConnected) return;

        var children = parentNode.Children.ToList();
        var index = children.FindIndex(c => c.NodeId == node.NodeId);
        if (index == -1) return;

        var targetIndex = up ? index - 1 : index + 1;
        if (targetIndex < 0 || targetIndex >= children.Count) return;

        try
        {
            var parentHtmlResult = await _cdpService.SendCommandAsync("DOM.getOuterHTML", new JsonObject
            {
                ["nodeId"] = parentNode.NodeId
            });
            var parentHtml = parentHtmlResult["outerHTML"]?.GetValue<string>();
            if (string.IsNullOrEmpty(parentHtml)) return;

            var childHtmls = new List<string>();
            foreach (var child in children)
            {
                var childResult = await _cdpService.SendCommandAsync("DOM.getOuterHTML", new JsonObject
                {
                    ["nodeId"] = child.NodeId
                });
                var childHtml = childResult["outerHTML"]?.GetValue<string>() ?? "";
                childHtmls.Add(childHtml);
            }

            int currentPos = 0;
            var childRanges = new List<(int Start, int Length)>();
            for (int i = 0; i < children.Count; i++)
            {
                var childHtml = childHtmls[i];
                var idxOf = parentHtml.IndexOf(childHtml, currentPos);
                if (idxOf == -1)
                {
                    idxOf = parentHtml.IndexOf(childHtml);
                }
                if (idxOf == -1)
                {
                    return;
                }
                childRanges.Add((idxOf, childHtml.Length));
                currentPos = idxOf + childHtml.Length;
            }

            int firstIdx = Math.Min(index, targetIndex);
            int secondIdx = Math.Max(index, targetIndex);

            var r1 = childRanges[firstIdx];
            var r2 = childRanges[secondIdx];

            var part1 = parentHtml.Substring(0, r1.Start);
            var part2 = parentHtml.Substring(r1.Start + r1.Length, r2.Start - (r1.Start + r1.Length));
            var part3 = parentHtml.Substring(r2.Start + r2.Length);

            var newParentHtml = part1 + childHtmls[secondIdx] + part2 + childHtmls[firstIdx] + part3;

            await _cdpService.SendCommandAsync("DOM.setOuterHTML", new JsonObject
            {
                ["nodeId"] = parentNode.NodeId,
                ["outerHTML"] = newParentHtml
            });

            // Re-select element to refresh state after outerHTML reloads
            var savedSelector = SelectedElementSelector;
            if (!string.IsNullOrEmpty(savedSelector))
            {
                await Task.Delay(200); // Wait slightly for DOM to rebuild
                await SelectElementAsync(savedSelector);
            }

            TriggerWorkspaceSync();
        }
        catch { }
    }
}

/// <summary>
/// View model representing an individual selected element's bounds and margins.
/// </summary>
public class SelectedElementInfo : ViewModelBase
{
    private int _nodeId;
    private string _selector = string.Empty;
    private Rect _bounds;
    private Thickness _margin;
    private Thickness _padding;

    public int NodeId
    {
        get => _nodeId;
        set => RaiseAndSetIfChanged(ref _nodeId, value);
    }

    public string Selector
    {
        get => _selector;
        set => RaiseAndSetIfChanged(ref _selector, value);
    }

    public Rect Bounds
    {
        get => _bounds;
        set => RaiseAndSetIfChanged(ref _bounds, value);
    }

    public Thickness Margin
    {
        get => _margin;
        set => RaiseAndSetIfChanged(ref _margin, value);
    }

    public Thickness Padding
    {
        get => _padding;
        set => RaiseAndSetIfChanged(ref _padding, value);
    }
}

/// <summary>
/// View model representing a breadcrumb path node.
/// </summary>
public class BreadcrumbItemViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private int _nodeId;
    private string _selector = string.Empty;

    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(ref _name, value);
    }

    public int NodeId
    {
        get => _nodeId;
        set => RaiseAndSetIfChanged(ref _nodeId, value);
    }

    public string Selector
    {
        get => _selector;
        set => RaiseAndSetIfChanged(ref _selector, value);
    }
}

/// <summary>
/// View model for a single toolbox item displayed in the designer.
/// </summary>
public class ToolboxItemViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _category = string.Empty;
    private string _defaultXaml = string.Empty;

    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(ref _name, value);
    }

    public string Category
    {
        get => _category;
        set => RaiseAndSetIfChanged(ref _category, value);
    }

    public string DefaultXaml
    {
        get => _defaultXaml;
        set => RaiseAndSetIfChanged(ref _defaultXaml, value);
    }
}

