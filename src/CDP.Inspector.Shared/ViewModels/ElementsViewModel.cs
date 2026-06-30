using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using Avalonia.Controls.DataGridHierarchical;
using Microsoft.Extensions.Logging;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.ViewModels;

public class ElementsViewModel : ViewModelBase, IStateProvider
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<ElementsViewModel>();
    private readonly ICdpService _cdpService;
    private readonly Dictionary<string, JsonObject> _axNodeDetailsMap = new();
    private ObservableCollection<DomNodeModel> _rootNodes = new();
    private ObservableCollection<AxNodeModel> _axRootNodes = new();
    private object? _selectedNodeNode;
    private object? _selectedAxNodeNode;

    public HierarchicalModel<DomNodeModel> HierarchicalRootNodes { get; }
    public HierarchicalModel<AxNodeModel> HierarchicalAxRootNodes { get; }

    public object? SelectedNodeNode
    {
        get => _selectedNodeNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedNodeNode, value))
            {
                var target = value is HierarchicalNode<DomNodeModel> node ? node.Item : (value as DomNodeModel);
                if (SelectedNode != target)
                {
                    SelectedNode = target;
                }
            }
        }
    }

    public object? SelectedAxNodeNode
    {
        get => _selectedAxNodeNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedAxNodeNode, value))
            {
                var target = value is HierarchicalNode<AxNodeModel> node ? node.Item : (value as AxNodeModel);
                if (SelectedAxNode != target)
                {
                    SelectedAxNode = target;
                }
            }
        }
    }
    private ObservableCollection<AttributeModel> _attributes = new();
    private ObservableCollection<PropertyModel> _properties = new();
    private ObservableCollection<CssPropertyModel> _cssProperties = new();
    private ObservableCollection<CssPropertyModel> _computedStyles = new();
    private ObservableCollection<EventListenerModel> _eventListeners = new();

    private DomNodeModel? _selectedNode;
    private AxNodeModel? _selectedAxNode;
    private PropertyModel? _selectedProperty;
    private AttributeModel? _selectedAttribute;

    private string _selectedNodeIdText = "None";
    private string _selectedPropertyNameText = "None";
    private string _propertyValueInputText = "";
    private string _attributeNameInputText = "";
    private string _attributeValueInputText = "";
    private string _styleTextInputText = "";
    private bool _isHighlightActive;
    private string _searchQuery = "";
    private string _axSearchQuery = "";
    private System.Collections.Generic.List<AxNodeModel> _axSearchResults = new();
    private int _axSearchIndex = -1;
    private string _lastAxSearchQuery = "";
    private bool _showVisualTree = false;
    private int? _lastParsedNodeId;

    // Accessibility Details
    private string _axRoleText = "None";
    private string _axNameText = "None";
    private string _axDescriptionText = "None";
    private string _axIgnoredText = "False";
    private string _axParentIdText = "None";
    private string _axChildIdsText = "None";

    // Layout Details
    private string _layoutMargin = "0,0,0,0";
    private string _layoutPadding = "0,0,0,0";
    private string _layoutBorderThickness = "0,0,0,0";
    private string _layoutWidth = "Auto";
    private string _layoutHeight = "Auto";
    private string _layoutBounds = "0,0,0,0";
    private string _layoutHorizontalAlignment = "Stretch";
    private string _layoutVerticalAlignment = "Stretch";

    private string _boxMarginTop = "0";
    private string _boxMarginRight = "0";
    private string _boxMarginBottom = "0";
    private string _boxMarginLeft = "0";
    private string _boxBorderTop = "0";
    private string _boxBorderRight = "0";
    private string _boxBorderBottom = "0";
    private string _boxBorderLeft = "0";
    private string _boxPaddingTop = "0";
    private string _boxPaddingRight = "0";
    private string _boxPaddingBottom = "0";
    private string _boxPaddingLeft = "0";
    private string _boxWidth = "0";
    private string _boxHeight = "0";

    private bool _isEditingMarginTop;
    private bool _isEditingMarginRight;
    private bool _isEditingMarginBottom;
    private bool _isEditingMarginLeft;
    private bool _isEditingBorderTop;
    private bool _isEditingBorderRight;
    private bool _isEditingBorderBottom;
    private bool _isEditingBorderLeft;
    private bool _isEditingPaddingTop;
    private bool _isEditingPaddingRight;
    private bool _isEditingPaddingBottom;
    private bool _isEditingPaddingLeft;
    private bool _isEditingWidth;
    private bool _isEditingHeight;

    public ObservableCollection<DomNodeModel> RootNodes => _rootNodes;
    public ObservableCollection<AxNodeModel> AxRootNodes => _axRootNodes;
    public ObservableCollection<AttributeModel> Attributes => _attributes;
    public ObservableCollection<PropertyModel> Properties => _properties;
    public ObservableCollection<CssPropertyModel> CssProperties => _cssProperties;
    public ObservableCollection<CssPropertyModel> ComputedStyles => _computedStyles;
    public ObservableCollection<EventListenerModel> EventListeners => _eventListeners;

    private string _propertySearchText = "";
    private string _cssSearchText = "";
    private string _computedSearchText = "";
    private string _attributeSearchText = "";

    private bool _isPseudoStatePanelOpen;
    private bool _isForcedHover;
    private bool _isForcedActive;
    private bool _isForcedFocus;
    private bool _isForcedFocusWithin;
    private bool _isForcedFocusVisible;
    private bool _isForcedDisabled;

    public bool IsPseudoStatePanelOpen
    {
        get => _isPseudoStatePanelOpen;
        set => RaiseAndSetIfChanged(ref _isPseudoStatePanelOpen, value);
    }

    private bool _isClassPanelOpen;
    public bool IsClassPanelOpen
    {
        get => _isClassPanelOpen;
        set => RaiseAndSetIfChanged(ref _isClassPanelOpen, value);
    }

    private ObservableCollection<ClassItemModel> _classes = new();
    public ObservableCollection<ClassItemModel> Classes => _classes;

    private string _newClassNameText = "";
    public string NewClassNameText
    {
        get => _newClassNameText;
        set
        {
            if (RaiseAndSetIfChanged(ref _newClassNameText, value))
            {
                ((RelayCommand)AddClassCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsForcedHover
    {
        get => _isForcedHover;
        set
        {
            if (RaiseAndSetIfChanged(ref _isForcedHover, value))
            {
                _ = UpdateForcedPseudoStateAsync();
            }
        }
    }

    public bool IsForcedActive
    {
        get => _isForcedActive;
        set
        {
            if (RaiseAndSetIfChanged(ref _isForcedActive, value))
            {
                _ = UpdateForcedPseudoStateAsync();
            }
        }
    }

    public bool IsForcedFocus
    {
        get => _isForcedFocus;
        set
        {
            if (RaiseAndSetIfChanged(ref _isForcedFocus, value))
            {
                _ = UpdateForcedPseudoStateAsync();
            }
        }
    }

    public bool IsForcedFocusWithin
    {
        get => _isForcedFocusWithin;
        set
        {
            if (RaiseAndSetIfChanged(ref _isForcedFocusWithin, value))
            {
                _ = UpdateForcedPseudoStateAsync();
            }
        }
    }

    public bool IsForcedFocusVisible
    {
        get => _isForcedFocusVisible;
        set
        {
            if (RaiseAndSetIfChanged(ref _isForcedFocusVisible, value))
            {
                _ = UpdateForcedPseudoStateAsync();
            }
        }
    }

    public bool IsForcedDisabled
    {
        get => _isForcedDisabled;
        set
        {
            if (RaiseAndSetIfChanged(ref _isForcedDisabled, value))
            {
                _ = UpdateForcedPseudoStateAsync();
            }
        }
    }

    private async Task UpdateForcedPseudoStateAsync()
    {
        if (!_cdpService.IsConnected || SelectedNode == null) return;
        var list = new JsonArray();
        if (IsForcedHover) list.Add((JsonNode)JsonValue.Create("hover"));
        if (IsForcedActive) list.Add((JsonNode)JsonValue.Create("active"));
        if (IsForcedFocus) list.Add((JsonNode)JsonValue.Create("focus"));
        if (IsForcedFocusWithin) list.Add((JsonNode)JsonValue.Create("focus-within"));
        if (IsForcedFocusVisible) list.Add((JsonNode)JsonValue.Create("focus-visible"));
        if (IsForcedDisabled) list.Add((JsonNode)JsonValue.Create("disabled"));
        try
        {
            await _cdpService.SendCommandAsync("CSS.forcePseudoState", new JsonObject
            {
                ["nodeId"] = SelectedNode.NodeId,
                ["forcedPseudoClasses"] = list
            });
        }
        catch {}
    }

    public string PropertySearchText
    {
        get => _propertySearchText;
        set
        {
            if (RaiseAndSetIfChanged(ref _propertySearchText, value))
            {
                OnPropertyChanged(nameof(FilteredProperties));
            }
        }
    }

    public string CssSearchText
    {
        get => _cssSearchText;
        set
        {
            if (RaiseAndSetIfChanged(ref _cssSearchText, value))
            {
                OnPropertyChanged(nameof(FilteredCssProperties));
            }
        }
    }

    public string ComputedSearchText
    {
        get => _computedSearchText;
        set
        {
            if (RaiseAndSetIfChanged(ref _computedSearchText, value))
            {
                OnPropertyChanged(nameof(FilteredComputedStyles));
            }
        }
    }

    public string AttributeSearchText
    {
        get => _attributeSearchText;
        set
        {
            if (RaiseAndSetIfChanged(ref _attributeSearchText, value))
            {
                OnPropertyChanged(nameof(FilteredAttributes));
            }
        }
    }

    public IEnumerable<PropertyModel> FilteredProperties
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PropertySearchText))
            {
                return Properties;
            }
            return Properties.Where(p => (p.Name != null && p.Name.Contains(PropertySearchText, StringComparison.OrdinalIgnoreCase))
                                      || (p.Value != null && p.Value.Contains(PropertySearchText, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public IEnumerable<CssPropertyModel> FilteredCssProperties
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CssSearchText))
            {
                return CssProperties;
            }
            return CssProperties.Where(p => (p.Name != null && p.Name.Contains(CssSearchText, StringComparison.OrdinalIgnoreCase))
                                         || (p.Value != null && p.Value.Contains(CssSearchText, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public IEnumerable<CssPropertyModel> FilteredComputedStyles
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ComputedSearchText))
            {
                return ComputedStyles;
            }
            return ComputedStyles.Where(p => (p.Name != null && p.Name.Contains(ComputedSearchText, StringComparison.OrdinalIgnoreCase))
                                          || (p.Value != null && p.Value.Contains(ComputedSearchText, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public IEnumerable<AttributeModel> FilteredAttributes
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AttributeSearchText))
            {
                return Attributes;
            }
            return Attributes.Where(p => (p.Name != null && p.Name.Contains(AttributeSearchText, StringComparison.OrdinalIgnoreCase))
                                      || (p.Value != null && p.Value.Contains(AttributeSearchText, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private bool _isSelectingProgrammatically;

    public DomNodeModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_isSelectingProgrammatically && value == null)
            {
                return;
            }
            if (RaiseAndSetIfChanged(ref _selectedNode, value))
            {
                _ = HandleNodeSelectionChangedAsync();
                SyncAxSelectionFromDom();

                if (value == null)
                {
                    SelectedNodeNode = null;
                }
                else
                {
                    var node = HierarchicalRootNodes.FindNode(value);
                    if (!Equals(SelectedNodeNode, node))
                    {
                        SelectedNodeNode = node;
                    }
                }
            }
        }
    }

    public AxNodeModel? SelectedAxNode
    {
        get => _selectedAxNode;
        set
        {
            if (_isSelectingProgrammatically && value == null)
            {
                return;
            }
            if (RaiseAndSetIfChanged(ref _selectedAxNode, value))
            {
                SyncDomSelectionFromAx();
                UpdateAxDetailsFromSelectedAxNode(value);

                if (value == null)
                {
                    SelectedAxNodeNode = null;
                }
                else
                {
                    var node = HierarchicalAxRootNodes.FindNode(value);
                    if (!Equals(SelectedAxNodeNode, node))
                    {
                        SelectedAxNodeNode = node;
                    }
                }
            }
        }
    }

    public PropertyModel? SelectedProperty
    {
        get => _selectedProperty;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedProperty, value))
            {
                SelectedPropertyNameText = _selectedProperty?.Name ?? "None";
                PropertyValueInputText = _selectedProperty?.Value ?? "";
                ((RelayCommand)ApplyPropertyCommand).RaiseCanExecuteChanged();
            }
        }
    }
 
    public AttributeModel? SelectedAttribute
    {
        get => _selectedAttribute;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedAttribute, value))
            {
                if (value != null)
                {
                    AttributeNameInputText = value.Name;
                    AttributeValueInputText = value.Value;
                }
                ((RelayCommand)DeleteAttributeCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedNodeIdText
    {
        get => _selectedNodeIdText;
        private set => RaiseAndSetIfChanged(ref _selectedNodeIdText, value);
    }

    public string SelectedPropertyNameText
    {
        get => _selectedPropertyNameText;
        private set => RaiseAndSetIfChanged(ref _selectedPropertyNameText, value);
    }

    public string PropertyValueInputText
    {
        get => _propertyValueInputText;
        set => RaiseAndSetIfChanged(ref _propertyValueInputText, value);
    }

    public string AttributeNameInputText
    {
        get => _attributeNameInputText;
        set => RaiseAndSetIfChanged(ref _attributeNameInputText, value);
    }

    public string AttributeValueInputText
    {
        get => _attributeValueInputText;
        set => RaiseAndSetIfChanged(ref _attributeValueInputText, value);
    }

    public string StyleTextInputText
    {
        get => _styleTextInputText;
        set => RaiseAndSetIfChanged(ref _styleTextInputText, value);
    }

    public bool IsHighlightActive
    {
        get => _isHighlightActive;
        set
        {
            if (RaiseAndSetIfChanged(ref _isHighlightActive, value))
            {
                _ = ToggleHighlightAsync();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => RaiseAndSetIfChanged(ref _searchQuery, value);
    }

    public string AxSearchQuery
    {
        get => _axSearchQuery;
        set => RaiseAndSetIfChanged(ref _axSearchQuery, value);
    }

    // Accessibility properties
    public string AxRoleText
    {
        get => _axRoleText;
        set => RaiseAndSetIfChanged(ref _axRoleText, value);
    }
    public string AxNameText
    {
        get => _axNameText;
        set => RaiseAndSetIfChanged(ref _axNameText, value);
    }
    public string AxDescriptionText
    {
        get => _axDescriptionText;
        set => RaiseAndSetIfChanged(ref _axDescriptionText, value);
    }
    public string AxIgnoredText
    {
        get => _axIgnoredText;
        set => RaiseAndSetIfChanged(ref _axIgnoredText, value);
    }
    public string AxParentIdText
    {
        get => _axParentIdText;
        set => RaiseAndSetIfChanged(ref _axParentIdText, value);
    }
    public string AxChildIdsText
    {
        get => _axChildIdsText;
        set => RaiseAndSetIfChanged(ref _axChildIdsText, value);
    }

    // Layout properties
    public string LayoutMargin
    {
        get => _layoutMargin;
        set => RaiseAndSetIfChanged(ref _layoutMargin, value);
    }
    public string LayoutPadding
    {
        get => _layoutPadding;
        set => RaiseAndSetIfChanged(ref _layoutPadding, value);
    }
    public string LayoutBorderThickness
    {
        get => _layoutBorderThickness;
        set => RaiseAndSetIfChanged(ref _layoutBorderThickness, value);
    }
    public string LayoutWidth
    {
        get => _layoutWidth;
        set => RaiseAndSetIfChanged(ref _layoutWidth, value);
    }
    public string LayoutHeight
    {
        get => _layoutHeight;
        set => RaiseAndSetIfChanged(ref _layoutHeight, value);
    }
    public string LayoutBounds
    {
        get => _layoutBounds;
        set => RaiseAndSetIfChanged(ref _layoutBounds, value);
    }

    public bool ShowVisualTree
    {
        get => _showVisualTree;
        set
        {
            if (RaiseAndSetIfChanged(ref _showVisualTree, value))
            {
                _ = RefreshDomTreeAsync();
            }
        }
    }

    public bool IsSelectingProgrammatically => _isSelectingProgrammatically;

    private int _selectedTreeTabIndex = 0;
    public int SelectedTreeTabIndex
    {
        get => _selectedTreeTabIndex;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedTreeTabIndex, value))
            {
                if (_selectedTreeTabIndex == 1)
                {
                    _ = Task.Run(async () =>
                    {
                        await RefreshAxTreeAsync();
                        Dispatcher.UIThread.Post(() => SyncAxSelectionFromDom());
                    });
                }
                else
                {
                    SyncDomSelectionFromAx();
                }
            }
        }
    }
    public string LayoutHorizontalAlignment
    {
        get => _layoutHorizontalAlignment;
        set => RaiseAndSetIfChanged(ref _layoutHorizontalAlignment, value);
    }
    public string LayoutVerticalAlignment
    {
        get => _layoutVerticalAlignment;
        set => RaiseAndSetIfChanged(ref _layoutVerticalAlignment, value);
    }

    public string BoxMarginTop { get => _boxMarginTop; set => RaiseAndSetIfChanged(ref _boxMarginTop, value); }
    public string BoxMarginRight { get => _boxMarginRight; set => RaiseAndSetIfChanged(ref _boxMarginRight, value); }
    public string BoxMarginBottom { get => _boxMarginBottom; set => RaiseAndSetIfChanged(ref _boxMarginBottom, value); }
    public string BoxMarginLeft { get => _boxMarginLeft; set => RaiseAndSetIfChanged(ref _boxMarginLeft, value); }
    public string BoxBorderTop { get => _boxBorderTop; set => RaiseAndSetIfChanged(ref _boxBorderTop, value); }
    public string BoxBorderRight { get => _boxBorderRight; set => RaiseAndSetIfChanged(ref _boxBorderRight, value); }
    public string BoxBorderBottom { get => _boxBorderBottom; set => RaiseAndSetIfChanged(ref _boxBorderBottom, value); }
    public string BoxBorderLeft { get => _boxBorderLeft; set => RaiseAndSetIfChanged(ref _boxBorderLeft, value); }
    public string BoxPaddingTop { get => _boxPaddingTop; set => RaiseAndSetIfChanged(ref _boxPaddingTop, value); }
    public string BoxPaddingRight { get => _boxPaddingRight; set => RaiseAndSetIfChanged(ref _boxPaddingRight, value); }
    public string BoxPaddingBottom { get => _boxPaddingBottom; set => RaiseAndSetIfChanged(ref _boxPaddingBottom, value); }
    public string BoxPaddingLeft { get => _boxPaddingLeft; set => RaiseAndSetIfChanged(ref _boxPaddingLeft, value); }
    public string BoxWidth { get => _boxWidth; set => RaiseAndSetIfChanged(ref _boxWidth, value); }
    public string BoxHeight { get => _boxHeight; set => RaiseAndSetIfChanged(ref _boxHeight, value); }

    public bool IsEditingMarginTop { get => _isEditingMarginTop; set => RaiseAndSetIfChanged(ref _isEditingMarginTop, value); }
    public bool IsEditingMarginRight { get => _isEditingMarginRight; set => RaiseAndSetIfChanged(ref _isEditingMarginRight, value); }
    public bool IsEditingMarginBottom { get => _isEditingMarginBottom; set => RaiseAndSetIfChanged(ref _isEditingMarginBottom, value); }
    public bool IsEditingMarginLeft { get => _isEditingMarginLeft; set => RaiseAndSetIfChanged(ref _isEditingMarginLeft, value); }
    public bool IsEditingBorderTop { get => _isEditingBorderTop; set => RaiseAndSetIfChanged(ref _isEditingBorderTop, value); }
    public bool IsEditingBorderRight { get => _isEditingBorderRight; set => RaiseAndSetIfChanged(ref _isEditingBorderRight, value); }
    public bool IsEditingBorderBottom { get => _isEditingBorderBottom; set => RaiseAndSetIfChanged(ref _isEditingBorderBottom, value); }
    public bool IsEditingBorderLeft { get => _isEditingBorderLeft; set => RaiseAndSetIfChanged(ref _isEditingBorderLeft, value); }
    public bool IsEditingPaddingTop { get => _isEditingPaddingTop; set => RaiseAndSetIfChanged(ref _isEditingPaddingTop, value); }
    public bool IsEditingPaddingRight { get => _isEditingPaddingRight; set => RaiseAndSetIfChanged(ref _isEditingPaddingRight, value); }
    public bool IsEditingPaddingBottom { get => _isEditingPaddingBottom; set => RaiseAndSetIfChanged(ref _isEditingPaddingBottom, value); }
    public bool IsEditingPaddingLeft { get => _isEditingPaddingLeft; set => RaiseAndSetIfChanged(ref _isEditingPaddingLeft, value); }
    public bool IsEditingWidth { get => _isEditingWidth; set => RaiseAndSetIfChanged(ref _isEditingWidth, value); }
    public bool IsEditingHeight { get => _isEditingHeight; set => RaiseAndSetIfChanged(ref _isEditingHeight, value); }

    public ICommand StartEditCommand { get; }
    public ICommand CommitEditCommand { get; }
    public ICommand CancelEditCommand { get; }

    public ICommand FocusSelectedNodeCommand { get; }
    public ICommand DeleteSelectedNodeCommand { get; }
    public ICommand ApplyAttributeCommand { get; }
    public ICommand DeleteAttributeCommand { get; }
    public ICommand ApplyPropertyCommand { get; }
    public ICommand ApplyStyleTextCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand AxSearchCommand { get; }
    public ICommand RefreshAxTreeCommand { get; }
    public ICommand AddClassCommand { get; }

    public ElementsViewModel(ICdpService cdpService)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _cdpService.PropertyChanged += CdpService_PropertyChanged;
        _cdpService.EventReceived += CdpService_EventReceived;

        FocusSelectedNodeCommand = new RelayCommand(async () => await FocusSelectedNodeAsync(), () => SelectedNode != null);
        DeleteSelectedNodeCommand = new RelayCommand(async () => await DeleteSelectedNodeAsync(), () => SelectedNode != null);
        ApplyAttributeCommand = new RelayCommand(async () => await ApplyAttributeAsync(), () => SelectedNode != null);
        DeleteAttributeCommand = new RelayCommand(async () => await DeleteAttributeAsync(), () => SelectedNode != null);
        ApplyPropertyCommand = new RelayCommand(async () => await ApplyPropertyAsync(), () => SelectedNode != null && SelectedProperty != null);
        ApplyStyleTextCommand = new RelayCommand(async () => await ApplyStyleTextAsync(), () => SelectedNode != null);
        SearchCommand = new RelayCommand(async () => await PerformSearchAsync());
        AxSearchCommand = new RelayCommand(async () => await PerformAxSearchAsync());
        RefreshAxTreeCommand = new RelayCommand(async () => await RefreshAxTreeAsync());
        StartEditCommand = new RelayCommand<string>(StartEdit);
        CommitEditCommand = new RelayCommand<string>(async field => await CommitEditAsync(field));
        CancelEditCommand = new RelayCommand(CancelAllEdits);
        AddClassCommand = new RelayCommand(async () => await AddClassAsync(), () => SelectedNode != null && !string.IsNullOrWhiteSpace(NewClassNameText));

        Properties.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FilteredProperties));
        CssProperties.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FilteredCssProperties));
        ComputedStyles.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FilteredComputedStyles));
        Attributes.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FilteredAttributes));

        var domOptions = new HierarchicalOptions<DomNodeModel>
        {
            ChildrenSelector = node => node.Children,
            IsLeafSelector = node => node.Children == null || node.Children.Count == 0,
            IsExpandedSelector = node => node.IsExpanded,
            IsExpandedSetter = (node, value) => node.IsExpanded = value,
            IsExpandedPropertyPath = nameof(DomNodeModel.IsExpanded),
            AutoExpandRoot = true
        };
        HierarchicalRootNodes = new HierarchicalModel<DomNodeModel>(domOptions);
        HierarchicalRootNodes.SetRoots(RootNodes);

        var axOptions = new HierarchicalOptions<AxNodeModel>
        {
            ChildrenSelector = node => node.Children,
            IsLeafSelector = node => node.Children == null || node.Children.Count == 0,
            IsExpandedSelector = node => node.IsExpanded,
            IsExpandedSetter = (node, value) => node.IsExpanded = value,
            IsExpandedPropertyPath = nameof(AxNodeModel.IsExpanded),
            AutoExpandRoot = true
        };
        HierarchicalAxRootNodes = new HierarchicalModel<AxNodeModel>(axOptions);
        HierarchicalAxRootNodes.SetRoots(AxRootNodes);
    }

    private void CdpService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ICdpService.IsConnected))
        {
            if (_cdpService.IsConnected)
            {
                _ = InitializeDomainAsync();
            }
            else
            {
                ClearData();
            }
        }
    }

    private void CdpService_EventReceived(object? sender, CdpEventEventArgs e)
    {
        if (e.Method == "Overlay.inspectNodeRequested")
        {
            int backendNodeId = e.Params["backendNodeId"]?.GetValue<int>() ?? 0;
            if (backendNodeId > 0)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    bool found = false;
                    if (SelectedTreeTabIndex == 1)
                    {
                        found = SelectAxNodeByBackendDomId(backendNodeId);
                    }
                    else
                    {
                        found = SelectNodeById(backendNodeId);
                    }

                    if (!found)
                    {
                        await RefreshDomTreeAsync();
                        await RefreshAxTreeAsync();

                        if (SelectedTreeTabIndex == 1)
                        {
                            SelectAxNodeByBackendDomId(backendNodeId);
                        }
                        else
                        {
                            SelectNodeById(backendNodeId);
                        }
                    }
                });
            }
        }
        else if (e.Method == "DOM.documentUpdated" || e.Method == "DOM.childNodeInserted" || e.Method == "DOM.childNodeRemoved")
        {
            Dispatcher.UIThread.Post(async () =>
            {
                int? savedNodeId = SelectedNode?.NodeId;
                string? savedAxNodeId = SelectedAxNode?.NodeId;

                await RefreshDomTreeAsync();
                await RefreshAxTreeAsync();

                if (savedNodeId.HasValue)
                {
                    SelectNodeById(savedNodeId.Value);
                }
                if (!string.IsNullOrEmpty(savedAxNodeId))
                {
                    SelectAxNodeById(savedAxNodeId);
                }
            });
        }
    }

    private async Task InitializeDomainAsync()
    {
        try
        {
            await _cdpService.SendCommandAsync("DOM.enable");
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ElementsViewModel", "Error enabling DOM domain", ex);
        }

        try
        {
            await _cdpService.SendCommandAsync("CSS.enable");
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ElementsViewModel", "Error enabling CSS domain", ex);
        }

        try
        {
            await _cdpService.SendCommandAsync("DOMDebugger.enable");
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ElementsViewModel", "Error enabling DOMDebugger domain", ex);
        }

        try
        {
            await _cdpService.SendCommandAsync("Accessibility.enable");
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ElementsViewModel", "Error enabling Accessibility domain", ex);
        }

        await RefreshDomTreeAsync();
        await RefreshAxTreeAsync();
    }

    private void ClearData()
    {
        Dispatcher.UIThread.Post(() =>
        {
            RootNodes.Clear();
            AxRootNodes.Clear();
            HierarchicalRootNodes.SetRoots(RootNodes);
            HierarchicalAxRootNodes.SetRoots(AxRootNodes);
            Attributes.Clear();
            Properties.Clear();
            CssProperties.Clear();
            ComputedStyles.Clear();
            EventListeners.Clear();
            SelectedNode = null;
            SelectedProperty = null;
            SelectedNodeIdText = "None";
            StyleTextInputText = "";
            ClearAxDetails();
            ResetLayoutInfo();
            SelectorService.Instance.UpdateSelectors(null);
        });
    }

    public async Task RefreshDomTreeAsync()
    {
        try
        {
            var response = await _cdpService.SendCommandAsync("DOM.getDocument", new JsonObject { ["depth"] = -1, ["pierce"] = ShowVisualTree });
            var root = response["root"] as JsonObject;
            if (root == null) return;

            var rootModel = BuildModel(root);
            if (Dispatcher.UIThread.CheckAccess())
            {
                RootNodes.Clear();
                RootNodes.Add(rootModel);
                HierarchicalRootNodes.SetRoots(RootNodes);
                SelectorService.Instance.UpdateSelectors(rootModel);
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RootNodes.Clear();
                    RootNodes.Add(rootModel);
                    HierarchicalRootNodes.SetRoots(RootNodes);
                    SelectorService.Instance.UpdateSelectors(rootModel);
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarningMessage("ElementsViewModel", "Error refreshing DOM tree", ex);
        }
    }

    private DomNodeModel BuildModel(JsonObject nodeJson, DomNodeModel? parent = null)
    {
        int nodeId = nodeJson["nodeId"]?.GetValue<int>() ?? 0;
        string nodeName = nodeJson["nodeName"]?.GetValue<string>() ?? "";
        string nodeValue = nodeJson["nodeValue"]?.GetValue<string>() ?? "";
        
        var model = new DomNodeModel(nodeId, nodeName) { Parent = parent, NodeValue = nodeValue };

        // Attributes
        var attrsNode = nodeJson["attributes"] as JsonArray;
        if (attrsNode != null)
        {
            for (int i = 0; i < attrsNode.Count; i += 2)
            {
                string name = attrsNode[i]?.GetValue<string>() ?? "";
                string val = attrsNode[i + 1]?.GetValue<string>() ?? "";
                model.AttributesList.Add(new AttributeModel(name, val));
            }
        }

        // Children
        var childrenNode = nodeJson["children"] as JsonArray;
        if (childrenNode != null)
        {
            foreach (var child in childrenNode)
            {
                if (child is JsonObject childObj)
                {
                    model.Children.Add(BuildModel(childObj, model));
                }
            }
        }

        // Setup display name
        string display = nodeName;
        var idAttr = model.AttributesList.FirstOrDefault(a => a.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
        if (idAttr != null) display += $"#{idAttr.Value}";
        var classAttr = model.AttributesList.FirstOrDefault(a => a.Name.Equals("class", StringComparison.OrdinalIgnoreCase));
        if (classAttr != null) display += $".{classAttr.Value.Split(' ').FirstOrDefault()}";
        model.DisplayName = display;

        return model;
    }

    private async Task HandleNodeSelectionChangedAsync()
    {
        var node = SelectedNode;
        _isForcedHover = false;
        _isForcedActive = false;
        _isForcedFocus = false;
        _isForcedFocusWithin = false;
        _isForcedFocusVisible = false;
        _isForcedDisabled = false;
        OnPropertyChanged(nameof(IsForcedHover));
        OnPropertyChanged(nameof(IsForcedActive));
        OnPropertyChanged(nameof(IsForcedFocus));
        OnPropertyChanged(nameof(IsForcedFocusWithin));
        OnPropertyChanged(nameof(IsForcedFocusVisible));
        OnPropertyChanged(nameof(IsForcedDisabled));

        if (node == null)
        {
            SelectedNodeIdText = "None";
            Attributes.Clear();
            Properties.Clear();
            CssProperties.Clear();
            ComputedStyles.Clear();
            EventListeners.Clear();
            StyleTextInputText = "";
            _lastParsedNodeId = null;
            Classes.Clear();
            NewClassNameText = "";
            ((RelayCommand)AddClassCommand).RaiseCanExecuteChanged();
            return;
        }

        SelectedNodeIdText = node.NodeId.ToString();
        ((RelayCommand)FocusSelectedNodeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DeleteSelectedNodeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ApplyAttributeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DeleteAttributeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ApplyStyleTextCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddClassCommand).RaiseCanExecuteChanged();

        if (node.NodeId != _lastParsedNodeId)
        {
            _lastParsedNodeId = node.NodeId;
            ParseClassesForSelectedNode();
        }

        // 1. Load Attributes
        Attributes.Clear();
        foreach (var attr in node.AttributesList)
        {
            Attributes.Add(attr);
        }

        // 2. Select Node in CDP
        try
        {
            await _cdpService.SendCommandAsync("DOM.setInspectedNode", new JsonObject { ["nodeId"] = node.NodeId });
        }
        catch { }

        if (SelectedNode != node) return;

        // 3. Resolve Node properties
        Properties.Clear();
        SelectedProperty = null;

        try
        {
            var resolveRes = await _cdpService.SendCommandAsync("DOM.resolveNode", new JsonObject { ["nodeId"] = node.NodeId });
            if (SelectedNode != node) return;

            var obj = resolveRes["object"] as JsonObject;
            string objectId = obj?["objectId"]?.GetValue<string>() ?? "";
            
            if (!string.IsNullOrEmpty(objectId))
            {
                var propsRes = await _cdpService.SendCommandAsync("Runtime.getProperties", new JsonObject { ["objectId"] = objectId });
                if (SelectedNode != node) return;

                var results = propsRes["result"] as JsonArray;
                if (results != null)
                {
                    var sorted = results
                        .Select(p => {
                            string name = p?["name"]?.GetValue<string>() ?? "";
                            var valObj = p?["value"] as JsonObject;
                            string val = valObj?["value"]?.ToString() ?? valObj?["description"]?.GetValue<string>() ?? "null";
                            string type = valObj?["type"]?.GetValue<string>() ?? "object";
                            return new PropertyModel(name, val, type);
                        })
                        .OrderBy(p => p.Name)
                        .ToList();

                    foreach (var p in sorted)
                    {
                        Properties.Add(p);
                    }
                }

                // 3b. Resolve Event Listeners
                EventListeners.Clear();
                try
                {
                    var listenersRes = await _cdpService.SendCommandAsync("DOMDebugger.getEventListeners", new JsonObject { ["objectId"] = objectId });
                    if (SelectedNode != node) return;

                    var listeners = listenersRes["listeners"] as JsonArray;
                    if (listeners != null)
                    {
                        foreach (var listener in listeners)
                        {
                            if (listener is JsonObject listenerObj)
                            {
                                string typeName = listenerObj["type"]?.GetValue<string>() ?? "";
                                bool useCapture = listenerObj["useCapture"]?.GetValue<bool>() ?? false;
                                var handler = listenerObj["handler"] as JsonObject;
                                string handlerName = handler?["description"]?.GetValue<string>() ?? "Anonymous";

                                EventListeners.Add(new EventListenerModel(typeName, handlerName, useCapture));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching event listeners: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching properties: {ex.Message}");
        }

        if (SelectedNode != node) return;

        // 4. Resolve CSS Styles & Computed Styles
        CssProperties.Clear();
        ComputedStyles.Clear();
        StyleTextInputText = "";

        try
        {
            var cssRes = await _cdpService.SendCommandAsync("CSS.getMatchedStylesForNode", new JsonObject { ["nodeId"] = node.NodeId });
            if (SelectedNode != node) return;

            var inlineStyle = cssRes["inlineStyle"] as JsonObject;
            if (inlineStyle != null)
            {
                var cssProps = inlineStyle["cssProperties"] as JsonArray;
                if (cssProps != null)
                {
                    var fullStyleBuilder = new StringBuilder();
                    foreach (var prop in cssProps)
                    {
                        if (prop is JsonObject propObj)
                        {
                            string name = propObj["name"]?.GetValue<string>() ?? "";
                            string val = propObj["value"]?.GetValue<string>() ?? "";
                            CssProperties.Add(new CssPropertyModel(name, val));
                            fullStyleBuilder.Append($"{name}: {val}; ");
                        }
                    }
                    StyleTextInputText = fullStyleBuilder.ToString().Trim();
                }
            }

            // Fetch Computed styles
            var compRes = await _cdpService.SendCommandAsync("CSS.getComputedStyleForNode", new JsonObject { ["nodeId"] = node.NodeId });
            if (SelectedNode != node) return;

            var compStyles = compRes["computedStyle"] as JsonArray;
            if (compStyles != null)
            {
                var sortedStyles = compStyles
                    .Select(s => {
                        string name = s?["name"]?.GetValue<string>() ?? "";
                        string val = s?["value"]?.GetValue<string>() ?? "";
                        return new CssPropertyModel(name, val);
                    })
                    .OrderBy(s => s.Name)
                    .ToList();

                foreach (var s in sortedStyles)
                {
                    ComputedStyles.Add(s);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching CSS styles: {ex.Message}");
        }

        if (SelectedNode != node) return;

        // 5. Fetch Accessibility details for the selected node
        ClearAxDetails();
        try
        {
            var axRes = await _cdpService.SendCommandAsync("Accessibility.getAXNode", new JsonObject { ["nodeId"] = node.NodeId });
            if (SelectedNode != node) return;

            var axNodes = axRes["nodes"] as JsonArray;
            if (axNodes != null && axNodes.Count > 0)
            {
                var matchedNode = axNodes.FirstOrDefault(n => n?["backendDOMNodeId"]?.GetValue<int>() == node.NodeId) as JsonObject;
                if (matchedNode == null)
                {
                    matchedNode = axNodes[0] as JsonObject;
                }

                if (matchedNode != null)
                {
                    var roleObj = matchedNode["role"] as JsonObject;
                    AxRoleText = roleObj?["value"]?.GetValue<string>() ?? "Unknown";
                    
                    var nameObj = matchedNode["name"] as JsonObject;
                    AxNameText = nameObj?["value"]?.GetValue<string>() ?? "None";

                    var descObj = matchedNode["description"] as JsonObject;
                    AxDescriptionText = descObj?["value"]?.GetValue<string>() ?? "None";

                    AxIgnoredText = (matchedNode["ignored"]?.GetValue<bool>() ?? false) ? "True" : "False";
                    AxParentIdText = matchedNode["parentId"]?.GetValue<string>() ?? "None";

                    var childIds = matchedNode["childIds"] as JsonArray;
                    if (childIds != null && childIds.Count > 0)
                    {
                        AxChildIdsText = string.Join(", ", childIds.Select(c => c?.GetValue<string>() ?? ""));
                    }
                    else
                    {
                        AxChildIdsText = "None";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching AX details: {ex.Message}");
        }

        if (SelectedNode != node) return;

        // 6. Populate Layout info from properties list
        ResetLayoutInfo();
        if (node != null)
        {
            var marginProp = Properties.FirstOrDefault(p => p.Name.Equals("Margin", StringComparison.OrdinalIgnoreCase));
            if (marginProp != null) LayoutMargin = marginProp.Value;

            var paddingProp = Properties.FirstOrDefault(p => p.Name.Equals("Padding", StringComparison.OrdinalIgnoreCase));
            if (paddingProp != null) LayoutPadding = paddingProp.Value;

            var borderProp = Properties.FirstOrDefault(p => p.Name.Equals("BorderThickness", StringComparison.OrdinalIgnoreCase));
            if (borderProp != null) LayoutBorderThickness = borderProp.Value;

            var widthProp = Properties.FirstOrDefault(p => p.Name.Equals("Width", StringComparison.OrdinalIgnoreCase));
            if (widthProp != null) LayoutWidth = widthProp.Value;

            var heightProp = Properties.FirstOrDefault(p => p.Name.Equals("Height", StringComparison.OrdinalIgnoreCase));
            if (heightProp != null) LayoutHeight = heightProp.Value;

            var boundsProp = Properties.FirstOrDefault(p => p.Name.Equals("Bounds", StringComparison.OrdinalIgnoreCase));
            if (boundsProp != null) LayoutBounds = boundsProp.Value;

            var haProp = Properties.FirstOrDefault(p => p.Name.Equals("HorizontalAlignment", StringComparison.OrdinalIgnoreCase));
            if (haProp != null) LayoutHorizontalAlignment = haProp.Value;

            var vaProp = Properties.FirstOrDefault(p => p.Name.Equals("VerticalAlignment", StringComparison.OrdinalIgnoreCase));
            if (vaProp != null) LayoutVerticalAlignment = vaProp.Value;

            try
            {
                var boxRes = await _cdpService.SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = node.NodeId });
                if (SelectedNode == node)
                {
                    var model = boxRes["model"] as JsonObject;
                    if (model != null)
                    {
                        var marginQuad = model["margin"] as JsonArray;
                        var borderQuad = model["border"] as JsonArray;
                        var paddingQuad = model["padding"] as JsonArray;
                        var contentQuad = model["content"] as JsonArray;

                        if (marginQuad != null && borderQuad != null && paddingQuad != null && contentQuad != null)
                        {
                            double bx = borderQuad[0]?.GetValue<double>() ?? 0;
                            double by = borderQuad[1]?.GetValue<double>() ?? 0;
                            double bxw = borderQuad[2]?.GetValue<double>() ?? 0;
                            double byh = borderQuad[5]?.GetValue<double>() ?? 0;

                            double ml = marginQuad[0]?.GetValue<double>() ?? 0;
                            double mt = marginQuad[1]?.GetValue<double>() ?? 0;
                            double mr = marginQuad[2]?.GetValue<double>() ?? 0;
                            double mb = marginQuad[5]?.GetValue<double>() ?? 0;

                            double pl = paddingQuad[0]?.GetValue<double>() ?? 0;
                            double pt = paddingQuad[1]?.GetValue<double>() ?? 0;
                            double pr = paddingQuad[2]?.GetValue<double>() ?? 0;
                            double pb = paddingQuad[5]?.GetValue<double>() ?? 0;

                            double cl = contentQuad[0]?.GetValue<double>() ?? 0;
                            double ct = contentQuad[1]?.GetValue<double>() ?? 0;
                            double cr = contentQuad[2]?.GetValue<double>() ?? 0;
                            double cb = contentQuad[5]?.GetValue<double>() ?? 0;

                            BoxMarginTop = Math.Round(by - mt, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            BoxMarginRight = Math.Round(mr - bxw, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            BoxMarginBottom = Math.Round(mb - byh, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            BoxMarginLeft = Math.Round(bx - ml, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

                            BoxBorderTop = Math.Round(pt - by, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            BoxBorderRight = Math.Round(bxw - pr, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            BoxBorderBottom = Math.Round(byh - pb, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            BoxBorderLeft = Math.Round(pl - bx, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

                            BoxPaddingTop = Math.Round(ct - pt, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            BoxPaddingRight = Math.Round(pr - cr, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            BoxPaddingBottom = Math.Round(pb - cb, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            BoxPaddingLeft = Math.Round(cl - pl, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

                            BoxWidth = Math.Round(model["width"]?.GetValue<double>() ?? 0, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            BoxHeight = Math.Round(model["height"]?.GetValue<double>() ?? 0, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching box model: {ex.Message}");
            }
        }

        // Update highlight if enabled
        if (IsHighlightActive)
        {
            _ = TriggerHighlightAsync();
        }
    }

    private async Task FocusSelectedNodeAsync()
    {
        if (SelectedNode == null) return;
        try
        {
            await _cdpService.SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = SelectedNode.NodeId });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error focusing node: {ex.Message}");
        }
    }

    private async Task DeleteSelectedNodeAsync()
    {
        if (SelectedNode == null) return;
        try
        {
            await _cdpService.SendCommandAsync("DOM.removeNode", new JsonObject { ["nodeId"] = SelectedNode.NodeId });
            await RefreshDomTreeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting node: {ex.Message}");
        }
    }

    private async Task ApplyAttributeAsync()
    {
        if (SelectedNode == null || string.IsNullOrEmpty(AttributeNameInputText)) return;
        try
        {
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNode.NodeId,
                ["name"] = AttributeNameInputText,
                ["value"] = AttributeValueInputText
            });

            // Update local attribute list
            var existing = SelectedNode.AttributesList.FirstOrDefault(a => a.Name.Equals(AttributeNameInputText, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Value = AttributeValueInputText;
            }
            else
            {
                SelectedNode.AttributesList.Add(new AttributeModel(AttributeNameInputText, AttributeValueInputText));
            }

            // Re-trigger selection load to update displays
            await HandleNodeSelectionChangedAsync();
            AttributeNameInputText = "";
            AttributeValueInputText = "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying attribute: {ex.Message}");
        }
    }

    public async Task DeleteAttributeAsync()
    {
        string name = AttributeNameInputText;
        if (string.IsNullOrEmpty(name) && SelectedAttribute != null)
        {
            name = SelectedAttribute.Name;
        }
        if (SelectedNode == null || string.IsNullOrEmpty(name)) return;
        try
        {
            await _cdpService.SendCommandAsync("DOM.removeAttribute", new JsonObject
            {
                ["nodeId"] = SelectedNode.NodeId,
                ["name"] = name
            });

            var existing = SelectedNode.AttributesList.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                SelectedNode.AttributesList.Remove(existing);
            }

            await HandleNodeSelectionChangedAsync();
            AttributeNameInputText = "";
            AttributeValueInputText = "";
            SelectedAttribute = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting attribute: {ex.Message}");
        }
    }

    private async Task ApplyPropertyAsync()
    {
        if (SelectedNode == null || SelectedProperty == null) return;
        try
        {
            var resolveRes = await _cdpService.SendCommandAsync("DOM.resolveNode", new JsonObject { ["nodeId"] = SelectedNode.NodeId });
            var obj = resolveRes["object"] as JsonObject;
            string objectId = obj?["objectId"]?.GetValue<string>() ?? "";
            
            if (!string.IsNullOrEmpty(objectId))
            {
                string rawValue = PropertyValueInputText;
                JsonNode parsedValue;
                if (SelectedProperty.Type == "number" && double.TryParse(rawValue, out double dVal))
                {
                    parsedValue = JsonValue.Create(dVal);
                }
                else if (SelectedProperty.Type == "boolean" && bool.TryParse(rawValue, out bool bVal))
                {
                    parsedValue = JsonValue.Create(bVal);
                }
                else
                {
                    parsedValue = JsonValue.Create(rawValue);
                }

                await _cdpService.SendCommandAsync("Runtime.callFunctionOn", new JsonObject
                {
                    ["objectId"] = objectId,
                    ["functionDeclaration"] = $"function(val) {{ this.{SelectedProperty.Name} = val; }}",
                    ["arguments"] = new JsonArray { (JsonNode)new JsonObject { ["value"] = parsedValue } }
                });

                await HandleNodeSelectionChangedAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying property: {ex.Message}");
        }
    }

    private async Task ApplyStyleTextAsync()
    {
        if (SelectedNode == null) return;
        try
        {
            var edits = new JsonArray
            {
                (JsonNode)new JsonObject
                {
                    ["styleSheetId"] = SelectedNode.NodeId.ToString(),
                    ["range"] = new JsonObject
                    {
                        ["startLine"] = 0,
                        ["startColumn"] = 0,
                        ["endLine"] = 0,
                        ["endColumn"] = 0
                    },
                    ["text"] = StyleTextInputText ?? ""
                }
            };

            await _cdpService.SendCommandAsync("CSS.setStyleTexts", new JsonObject { ["edits"] = edits });
            await HandleNodeSelectionChangedAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying style texts: {ex.Message}");
        }
    }

    public async Task UpdateAttributeAsync(AttributeModel attr)
    {
        if (SelectedNode == null) return;
        try
        {
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNode.NodeId,
                ["name"] = attr.Name,
                ["value"] = attr.Value
            });

            await HandleNodeSelectionChangedAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating attribute: {ex.Message}");
        }
    }

    public async Task UpdatePropertyAsync(PropertyModel prop)
    {
        if (SelectedNode == null) return;
        try
        {
            var resolveRes = await _cdpService.SendCommandAsync("DOM.resolveNode", new JsonObject { ["nodeId"] = SelectedNode.NodeId });
            var obj = resolveRes["object"] as JsonObject;
            string objectId = obj?["objectId"]?.GetValue<string>() ?? "";
            
            if (!string.IsNullOrEmpty(objectId))
            {
                string rawValue = prop.Value;
                JsonNode parsedValue;
                if (prop.Type == "number" && double.TryParse(rawValue, out double dVal))
                {
                    parsedValue = JsonValue.Create(dVal);
                }
                else if (prop.Type == "boolean" && bool.TryParse(rawValue, out bool bVal))
                {
                    parsedValue = JsonValue.Create(bVal);
                }
                else
                {
                    parsedValue = JsonValue.Create(rawValue);
                }

                await _cdpService.SendCommandAsync("Runtime.callFunctionOn", new JsonObject
                {
                    ["objectId"] = objectId,
                    ["functionDeclaration"] = $"function(val) {{ this.{prop.Name} = val; }}",
                    ["arguments"] = new JsonArray { (JsonNode)new JsonObject { ["value"] = parsedValue } }
                });

                await HandleNodeSelectionChangedAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating property: {ex.Message}");
        }
    }

    public async Task UpdateCssPropertyAsync(CssPropertyModel cssProp)
    {
        if (SelectedNode == null) return;
        try
        {
            var sb = new StringBuilder();
            foreach (var p in CssProperties)
            {
                sb.Append($"{p.Name}: {p.Value}; ");
            }
            string fullText = sb.ToString().Trim();

            var edits = new JsonArray
            {
                (JsonNode)new JsonObject
                {
                    ["styleSheetId"] = SelectedNode.NodeId.ToString(),
                    ["range"] = new JsonObject
                    {
                        ["startLine"] = 0,
                        ["startColumn"] = 0,
                        ["endLine"] = 0,
                        ["endColumn"] = 0
                    },
                    ["text"] = fullText
                }
            };

            await _cdpService.SendCommandAsync("CSS.setStyleTexts", new JsonObject { ["edits"] = edits });
            StyleTextInputText = fullText;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating inline style: {ex.Message}");
        }
    }

    private async Task ToggleHighlightAsync()
    {
        if (!_cdpService.IsConnected) return;
        try
        {
            if (IsHighlightActive)
            {
                await TriggerHighlightAsync();
            }
            else
            {
                await _cdpService.SendCommandAsync("DOM.hideHighlight");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling highlight: {ex.Message}");
        }
    }

    private async Task TriggerHighlightAsync()
    {
        if (!_cdpService.IsConnected || SelectedNode == null) return;
        try
        {
            var highlightParams = new JsonObject
            {
                ["nodeId"] = SelectedNode.NodeId,
                ["highlightConfig"] = new JsonObject
                {
                    ["showInfo"] = true,
                    ["contentColor"] = new JsonObject { ["r"] = 111, ["g"] = 168, ["b"] = 220, ["a"] = 0.4 }
                }
            };
            await _cdpService.SendCommandAsync("DOM.highlightNode", highlightParams);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Highlight error: {ex.Message}");
        }
    }

    private async Task PerformSearchAsync()
    {
        if (!_cdpService.IsConnected || string.IsNullOrEmpty(SearchQuery)) return;
        try
        {
            var searchParams = new JsonObject { ["query"] = SearchQuery };
            var searchRes = await _cdpService.SendCommandAsync("DOM.performSearch", searchParams);
            string searchId = searchRes["searchId"]?.GetValue<string>() ?? "";
            int resultCount = searchRes["resultCount"]?.GetValue<int>() ?? 0;

            if (!string.IsNullOrEmpty(searchId) && resultCount > 0)
            {
                var getResParams = new JsonObject
                {
                    ["searchId"] = searchId,
                    ["fromIndex"] = 0,
                    ["toIndex"] = resultCount
                };
                var getRes = await _cdpService.SendCommandAsync("DOM.getSearchResults", getResParams);
                var nodeIds = getRes["nodeIds"] as JsonArray;
                if (nodeIds != null && nodeIds.Count > 0)
                {
                    int firstNodeId = nodeIds[0]?.GetValue<int>() ?? 0;
                    if (firstNodeId > 0)
                    {
                        SelectNodeById(firstNodeId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Search error: {ex.Message}");
        }
    }

    private async Task PerformAxSearchAsync()
    {
        if (string.IsNullOrEmpty(AxSearchQuery)) return;

        if (AxSearchQuery != _lastAxSearchQuery)
        {
            _axSearchResults.Clear();
            _lastAxSearchQuery = AxSearchQuery;
            _axSearchIndex = -1;

            try
            {
                var response = await _cdpService.SendCommandAsync("Accessibility.queryAXTree", new JsonObject
                {
                    ["accessibleName"] = AxSearchQuery
                });
                var nodes = response["nodes"] as JsonArray;
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        if (node is JsonObject nodeObj)
                        {
                            string nodeId = nodeObj["nodeId"]?.GetValue<string>() ?? "";
                            if (!string.IsNullOrEmpty(nodeId))
                            {
                                var path = new List<AxNodeModel>();
                                if (FindAxNodePathById(AxRootNodes, nodeId, path) && path.Count > 0)
                                {
                                    _axSearchResults.Add(path[^1]);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error doing server AX search: {ex.Message}");
            }

            if (_axSearchResults.Count == 0)
            {
                FindMatchingAxNodes(AxRootNodes, AxSearchQuery, _axSearchResults);
            }
            _axSearchIndex = _axSearchResults.Count > 0 ? 0 : -1;
        }
        else if (_axSearchResults.Count > 0)
        {
            _axSearchIndex = (_axSearchIndex + 1) % _axSearchResults.Count;
        }

        if (_axSearchIndex >= 0 && _axSearchIndex < _axSearchResults.Count)
        {
            var match = _axSearchResults[_axSearchIndex];
            SelectAxNodeById(match.NodeId);
        }
    }

    private void FindMatchingAxNodes(IEnumerable<AxNodeModel> nodes, string query, List<AxNodeModel> matches)
    {
        foreach (var node in nodes)
        {
            if ((node.Role != null && node.Role.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (node.Name != null && node.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                matches.Add(node);
            }
            FindMatchingAxNodes(node.Children, query, matches);
        }
    }

    public void SelectAxNodeById(string nodeId)
    {
        _isSelectingProgrammatically = true;
        try
        {
            DeselectAllAx(AxRootNodes);

            var path = new List<AxNodeModel>();
            if (FindAxNodePathById(AxRootNodes, nodeId, path))
            {
                for (int i = 0; i < path.Count - 1; i++)
                {
                    path[i].IsExpanded = true;
                }
                path[^1].IsSelected = true;
                SelectedAxNode = path[^1];
            }
        }
        finally
        {
            Dispatcher.UIThread.Post(() => _isSelectingProgrammatically = false);
        }
    }

    private bool FindAxNodePathById(IEnumerable<AxNodeModel> nodes, string nodeId, List<AxNodeModel> path)
    {
        foreach (var node in nodes)
        {
            path.Add(node);
            if (node.NodeId == nodeId)
            {
                return true;
            }
            if (FindAxNodePathById(node.Children, nodeId, path))
            {
                return true;
            }
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    public bool SelectNodeById(int nodeId)
    {
        _isSelectingProgrammatically = true;
        try
        {
            DeselectAll(RootNodes);

            var path = new List<DomNodeModel>();
            if (FindNodePath(RootNodes, nodeId, path))
            {
                for (int i = 0; i < path.Count - 1; i++)
                {
                    path[i].IsExpanded = true;
                }
                path[^1].IsSelected = true;
                SelectedNode = path[^1];
                return true;
            }
            return false;
        }
        finally
        {
            Dispatcher.UIThread.Post(() => _isSelectingProgrammatically = false);
        }
    }

    private void DeselectAll(IEnumerable<DomNodeModel> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsSelected = false;
            DeselectAll(node.Children);
        }
    }

    private bool FindNodePath(IEnumerable<DomNodeModel> nodes, int nodeId, List<DomNodeModel> path)
    {
        foreach (var node in nodes)
        {
            path.Add(node);
            if (node.NodeId == nodeId)
            {
                return true;
            }
            if (FindNodePath(node.Children, nodeId, path))
            {
                return true;
            }
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    public async Task RefreshAxTreeAsync()
    {
        try
        {
            var response = await _cdpService.SendCommandAsync("Accessibility.getFullAXTree");
            var nodes = response["nodes"] as JsonArray;
            if (nodes == null) return;

            _axNodeDetailsMap.Clear();
            var nodesMap = new Dictionary<string, AxNodeModel>();
            var rootList = new List<AxNodeModel>();
            var parents = new Dictionary<string, string>();

            foreach (var node in nodes)
            {
                if (node is JsonObject nodeObj)
                {
                    string nodeId = nodeObj["nodeId"]?.GetValue<string>() ?? "";
                    if (!string.IsNullOrEmpty(nodeId))
                    {
                        _axNodeDetailsMap[nodeId] = nodeObj;
                    }
                    var roleObj = nodeObj["role"] as JsonObject;
                    string role = roleObj?["value"]?.GetValue<string>() ?? "Unknown";
                    var nameObj = nodeObj["name"] as JsonObject;
                    string name = nameObj?["value"]?.GetValue<string>() ?? "";
                    bool ignored = nodeObj["ignored"]?.GetValue<bool>() ?? false;
                    int? backendDomNodeId = nodeObj["backendDOMNodeId"]?.GetValue<int>();

                    var model = new AxNodeModel(nodeId, role, name, ignored, backendDomNodeId);
                    nodesMap[nodeId] = model;

                    var childIds = nodeObj["childIds"] as JsonArray;
                    if (childIds != null)
                    {
                        foreach (var childIdNode in childIds)
                        {
                            string childId = childIdNode?.GetValue<string>() ?? "";
                            if (!string.IsNullOrEmpty(childId))
                            {
                                parents[childId] = nodeId;
                            }
                        }
                    }
                }
            }

            foreach (var kvp in nodesMap)
            {
                string nodeId = kvp.Key;
                var nodeModel = kvp.Value;

                if (parents.TryGetValue(nodeId, out string? parentId) && parentId != null && nodesMap.TryGetValue(parentId, out var parentModel))
                {
                    parentModel.Children.Add(nodeModel);
                }
                else
                {
                    rootList.Add(nodeModel);
                }
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                _axRootNodes.Clear();
                foreach (var root in rootList)
                {
                    _axRootNodes.Add(root);
                }
                HierarchicalAxRootNodes.SetRoots(_axRootNodes);
                SyncAxSelectionFromDom();
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _axRootNodes.Clear();
                    foreach (var root in rootList)
                    {
                        _axRootNodes.Add(root);
                    }
                    HierarchicalAxRootNodes.SetRoots(_axRootNodes);
                    SyncAxSelectionFromDom();
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing AX tree: {ex.Message}");
        }
    }

    private void ClearAxDetails()
    {
        AxRoleText = "None";
        AxNameText = "None";
        AxDescriptionText = "None";
        AxIgnoredText = "False";
        AxParentIdText = "None";
        AxChildIdsText = "None";
    }

    private void ResetLayoutInfo()
    {
        LayoutMargin = "0,0,0,0";
        LayoutPadding = "0,0,0,0";
        LayoutBorderThickness = "0,0,0,0";
        LayoutWidth = "Auto";
        LayoutHeight = "Auto";
        LayoutBounds = "0,0,0,0";
        LayoutHorizontalAlignment = "Stretch";
        LayoutVerticalAlignment = "Stretch";

        BoxMarginTop = "0";
        BoxMarginRight = "0";
        BoxMarginBottom = "0";
        BoxMarginLeft = "0";
        BoxBorderTop = "0";
        BoxBorderRight = "0";
        BoxBorderBottom = "0";
        BoxBorderLeft = "0";
        BoxPaddingTop = "0";
        BoxPaddingRight = "0";
        BoxPaddingBottom = "0";
        BoxPaddingLeft = "0";
        BoxWidth = "0";
        BoxHeight = "0";
    }

    public void StartEdit(string field)
    {
        CancelAllEdits();
        switch (field)
        {
            case "MarginTop": IsEditingMarginTop = true; break;
            case "MarginRight": IsEditingMarginRight = true; break;
            case "MarginBottom": IsEditingMarginBottom = true; break;
            case "MarginLeft": IsEditingMarginLeft = true; break;
            case "BorderTop": IsEditingBorderTop = true; break;
            case "BorderRight": IsEditingBorderRight = true; break;
            case "BorderBottom": IsEditingBorderBottom = true; break;
            case "BorderLeft": IsEditingBorderLeft = true; break;
            case "PaddingTop": IsEditingPaddingTop = true; break;
            case "PaddingRight": IsEditingPaddingRight = true; break;
            case "PaddingBottom": IsEditingPaddingBottom = true; break;
            case "PaddingLeft": IsEditingPaddingLeft = true; break;
            case "Width": IsEditingWidth = true; break;
            case "Height": IsEditingHeight = true; break;
        }
    }

    public void CancelAllEdits()
    {
        IsEditingMarginTop = false;
        IsEditingMarginRight = false;
        IsEditingMarginBottom = false;
        IsEditingMarginLeft = false;
        IsEditingBorderTop = false;
        IsEditingBorderRight = false;
        IsEditingBorderBottom = false;
        IsEditingBorderLeft = false;
        IsEditingPaddingTop = false;
        IsEditingPaddingRight = false;
        IsEditingPaddingBottom = false;
        IsEditingPaddingLeft = false;
        IsEditingWidth = false;
        IsEditingHeight = false;
    }

    public async Task CommitEditAsync(string field)
    {
        if (SelectedNode == null) return;

        string value = field switch
        {
            "MarginTop" => BoxMarginTop,
            "MarginRight" => BoxMarginRight,
            "MarginBottom" => BoxMarginBottom,
            "MarginLeft" => BoxMarginLeft,
            "BorderTop" => BoxBorderTop,
            "BorderRight" => BoxBorderRight,
            "BorderBottom" => BoxBorderBottom,
            "BorderLeft" => BoxBorderLeft,
            "PaddingTop" => BoxPaddingTop,
            "PaddingRight" => BoxPaddingRight,
            "PaddingBottom" => BoxPaddingBottom,
            "PaddingLeft" => BoxPaddingLeft,
            "Width" => BoxWidth,
            "Height" => BoxHeight,
            _ => null
        };

        CancelAllEdits();
        if (value == null) return;

        string cssProperty = field switch
        {
            "MarginTop" => "margin-top",
            "MarginRight" => "margin-right",
            "MarginBottom" => "margin-bottom",
            "MarginLeft" => "margin-left",
            "BorderTop" => "border-top-width",
            "BorderRight" => "border-right-width",
            "BorderBottom" => "border-bottom-width",
            "BorderLeft" => "border-left-width",
            "PaddingTop" => "padding-top",
            "PaddingRight" => "padding-right",
            "PaddingBottom" => "padding-bottom",
            "PaddingLeft" => "padding-left",
            "Width" => "width",
            "Height" => "height",
            _ => null
        };

        if (cssProperty == null) return;

        string formattedValue = value.Trim();
        if (double.TryParse(formattedValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _) && 
            !formattedValue.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            formattedValue += "px";
        }

        string styleText = $"{cssProperty}: {formattedValue};";

        try
        {
            await _cdpService.SendCommandAsync("CSS.setStyleTexts", new JsonObject
            {
                ["edits"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["styleSheetId"] = SelectedNode.NodeId.ToString(),
                        ["text"] = styleText
                    }
                }
            });

            _ = HandleNodeSelectionChangedAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error committing box model edit: {ex.Message}");
        }
    }



    private bool _isSyncingSelection = false;

    private void SyncAxSelectionFromDom()
    {
        if (_isSyncingSelection || SelectedNode == null) return;
        _isSyncingSelection = true;
        try
        {
            SelectAxNodeByBackendDomId(SelectedNode.NodeId);
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void SyncDomSelectionFromAx()
    {
        if (_isSyncingSelection || SelectedAxNode == null || !SelectedAxNode.BackendDOMNodeId.HasValue) return;
        _isSyncingSelection = true;
        try
        {
            SelectNodeById(SelectedAxNode.BackendDOMNodeId.Value);
            _ = TriggerHighlightAsync();
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    public bool SelectAxNodeByBackendDomId(int backendDomId)
    {
        _isSelectingProgrammatically = true;
        try
        {
            DeselectAllAx(AxRootNodes);

            var path = new List<AxNodeModel>();
            if (FindAxNodePathByBackendDomId(AxRootNodes, backendDomId, path))
            {
                for (int i = 0; i < path.Count - 1; i++)
                {
                    path[i].IsExpanded = true;
                }
                path[^1].IsSelected = true;
                _selectedAxNode = path[^1];
                OnPropertyChanged(nameof(SelectedAxNode));
                UpdateAxDetailsFromSelectedAxNode(_selectedAxNode);
                return true;
            }
            return false;
        }
        finally
        {
            Dispatcher.UIThread.Post(() => _isSelectingProgrammatically = false);
        }
    }

    private void UpdateAxDetailsFromSelectedAxNode(AxNodeModel? selectedAxNode)
    {
        if (selectedAxNode == null)
        {
            ClearAxDetails();
            return;
        }

        if (_axNodeDetailsMap.TryGetValue(selectedAxNode.NodeId, out var matchedNode) && matchedNode != null)
        {
            var roleObj = matchedNode["role"] as JsonObject;
            AxRoleText = roleObj?["value"]?.GetValue<string>() ?? selectedAxNode.Role;
            
            var nameObj = matchedNode["name"] as JsonObject;
            AxNameText = nameObj?["value"]?.GetValue<string>() ?? selectedAxNode.Name ?? "None";

            var descObj = matchedNode["description"] as JsonObject;
            AxDescriptionText = descObj?["value"]?.GetValue<string>() ?? "None";

            AxIgnoredText = (matchedNode["ignored"]?.GetValue<bool>() ?? false) ? "True" : "False";
            AxParentIdText = matchedNode["parentId"]?.GetValue<string>() ?? "None";

            var childIds = matchedNode["childIds"] as JsonArray;
            if (childIds != null && childIds.Count > 0)
            {
                AxChildIdsText = string.Join(", ", childIds.Select(c => c?.GetValue<string>() ?? ""));
            }
            else
            {
                AxChildIdsText = "None";
            }
        }
        else
        {
            AxRoleText = selectedAxNode.Role;
            AxNameText = selectedAxNode.Name ?? "None";
            AxDescriptionText = "None";
            AxIgnoredText = selectedAxNode.Ignored ? "True" : "False";
            AxParentIdText = "None";
            AxChildIdsText = selectedAxNode.Children.Count > 0 
                ? string.Join(", ", selectedAxNode.Children.Select(c => c.NodeId)) 
                : "None";
        }
    }

    private void DeselectAllAx(IEnumerable<AxNodeModel> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsSelected = false;
            DeselectAllAx(node.Children);
        }
    }

    private bool FindAxNodePathByBackendDomId(IEnumerable<AxNodeModel> nodes, int backendDomId, List<AxNodeModel> path)
    {
        foreach (var node in nodes)
        {
            path.Add(node);
            if (node.BackendDOMNodeId == backendDomId)
            {
                return true;
            }
            if (FindAxNodePathByBackendDomId(node.Children, backendDomId, path))
            {
                return true;
            }
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    public (string? Role, string? Name) FindAxDetails(int backendDomNodeId)
    {
        foreach (var kvp in _axNodeDetailsMap)
        {
            var nodeObj = kvp.Value;
            var backendNodeId = nodeObj["backendDOMNodeId"]?.GetValue<int>();
            if (backendNodeId == backendDomNodeId)
            {
                var roleObj = nodeObj["role"] as JsonObject;
                string? role = roleObj?["value"]?.GetValue<string>();
                var nameObj = nodeObj["name"] as JsonObject;
                string? name = nameObj?["value"]?.GetValue<string>();
                return (role, name);
            }
        }
        return (null, null);
    }

    public DomNodeModel? FindDomNode(int nodeId)
    {
        DomNodeModel? FindNode(DomNodeModel parent)
        {
            if (parent.NodeId == nodeId) return parent;
            foreach (var child in parent.Children)
            {
                var found = FindNode(child);
                if (found != null) return found;
            }
            return null;
        }
        foreach (var root in RootNodes)
        {
            var found = FindNode(root);
            if (found != null) return found;
        }
        return null;
    }

    private void ParseClassesForSelectedNode()
    {
        Classes.Clear();
        var node = SelectedNode;
        if (node == null) return;

        var classAttr = node.AttributesList.FirstOrDefault(a => a.Name.Equals("class", StringComparison.OrdinalIgnoreCase));
        if (classAttr != null && !string.IsNullOrEmpty(classAttr.Value))
        {
            var parts = classAttr.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (!Classes.Any(c => c.Name == part))
                {
                    Classes.Add(new ClassItemModel(part, true, OnClassToggled));
                }
            }
        }
    }

    private void OnClassToggled(ClassItemModel classItem)
    {
        _ = UpdateControlClassesAsync();
    }

    private async Task UpdateControlClassesAsync()
    {
        if (SelectedNode == null) return;
        
        var enabledClasses = Classes.Where(c => c.IsEnabled).Select(c => c.Name);
        string classValue = string.Join(" ", enabledClasses);
        
        try
        {
            await _cdpService.SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = SelectedNode.NodeId,
                ["name"] = "class",
                ["value"] = classValue
            });

            var classAttr = SelectedNode.AttributesList.FirstOrDefault(a => a.Name.Equals("class", StringComparison.OrdinalIgnoreCase));
            if (classAttr != null)
            {
                if (string.IsNullOrEmpty(classValue))
                {
                    SelectedNode.AttributesList.Remove(classAttr);
                }
                else
                {
                    classAttr.Value = classValue;
                }
            }
            else if (!string.IsNullOrEmpty(classValue))
            {
                SelectedNode.AttributesList.Add(new AttributeModel("class", classValue));
            }

            UpdateNodeDisplayName(SelectedNode);

            await HandleNodeSelectionChangedAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating control classes: {ex.Message}");
        }
    }

    private void UpdateNodeDisplayName(DomNodeModel model)
    {
        string display = model.NodeName;
        var idAttr = model.AttributesList.FirstOrDefault(a => a.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
        if (idAttr != null) display += $"#{idAttr.Value}";
        var classAttr = model.AttributesList.FirstOrDefault(a => a.Name.Equals("class", StringComparison.OrdinalIgnoreCase));
        if (classAttr != null) display += $".{classAttr.Value.Split(' ').FirstOrDefault()}";
        model.DisplayName = display;
    }

    private async Task AddClassAsync()
    {
        if (SelectedNode == null || string.IsNullOrWhiteSpace(NewClassNameText)) return;

        string newClass = NewClassNameText.Trim();
        NewClassNameText = "";

        var existing = Classes.FirstOrDefault(c => c.Name.Equals(newClass, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            if (!existing.IsEnabled)
            {
                existing.IsEnabled = true;
            }
            return;
        }

        Classes.Add(new ClassItemModel(newClass, true, OnClassToggled));
        
        await UpdateControlClassesAsync();
    }

    #region IStateProvider Implementation

    public string StateKey => "elements";

    public JsonNode? SaveState()
    {
        var root = new JsonObject();
        root["showVisualTree"] = ShowVisualTree;
        root["selectedTreeTabIndex"] = SelectedTreeTabIndex;
        root["searchQuery"] = SearchQuery;
        root["axSearchQuery"] = AxSearchQuery;
        root["propertySearchText"] = PropertySearchText;
        root["cssSearchText"] = CssSearchText;
        root["computedSearchText"] = ComputedSearchText;
        root["attributeSearchText"] = AttributeSearchText;
        return root;
    }

    public void LoadState(JsonNode? stateNode)
    {
        if (stateNode is not JsonObject json) return;

        if (json.TryGetPropertyValue("showVisualTree", out var showNode) && showNode != null)
        {
            ShowVisualTree = (bool?)showNode ?? false;
        }
        if (json.TryGetPropertyValue("selectedTreeTabIndex", out var tabNode) && tabNode != null)
        {
            SelectedTreeTabIndex = (int?)tabNode ?? 0;
        }
        if (json.TryGetPropertyValue("searchQuery", out var searchNode) && searchNode != null)
        {
            SearchQuery = (string?)searchNode ?? "";
        }
        if (json.TryGetPropertyValue("axSearchQuery", out var axSearchNode) && axSearchNode != null)
        {
            AxSearchQuery = (string?)axSearchNode ?? "";
        }
        if (json.TryGetPropertyValue("propertySearchText", out var propSearchNode) && propSearchNode != null)
        {
            PropertySearchText = (string?)propSearchNode ?? "";
        }
        if (json.TryGetPropertyValue("cssSearchText", out var cssSearchNode) && cssSearchNode != null)
        {
            CssSearchText = (string?)cssSearchNode ?? "";
        }
        if (json.TryGetPropertyValue("computedSearchText", out var compSearchNode) && compSearchNode != null)
        {
            ComputedSearchText = (string?)compSearchNode ?? "";
        }
        if (json.TryGetPropertyValue("attributeSearchText", out var attrSearchNode) && attrSearchNode != null)
        {
            AttributeSearchText = (string?)attrSearchNode ?? "";
        }
    }

    #endregion
}

