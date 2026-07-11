using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using CdpInspectorApp.Services;
using CDP.Editor.Splits.Models;

namespace CdpInspectorApp.ViewModels;

public class MainWindowViewModel : ViewModelBase, IStateProvider
{
    public ICdpService CdpService { get; }
    public IStateService StateService { get; }
    public ConnectionViewModel Connection { get; }
    public ElementsViewModel Elements { get; }
    public ConsoleViewModel Console { get; }
    public SourcesViewModel Sources { get; }
    public NetworkViewModel Network { get; }
    public PerformanceViewModel Performance { get; }
    public ProfilerViewModel Profiler { get; }
    public MemoryViewModel Memory { get; }
    public ApplicationViewModel Application { get; }
    public AuditsViewModel Audits { get; }
    public SimulationViewModel Simulation { get; }
    public RecorderViewModel Recorder { get; }
    public EventsViewModel Events { get; }
    public MvvmViewModel Mvvm { get; }
    public DiffViewModel Diff { get; }

    private SplitNode? _layoutRoot;
    private BoxNode? _selectedPane;
    private bool _isPreviewPanelVisible = true;

    public SplitNode? LayoutRoot
    {
        get => _layoutRoot;
        set => RaiseAndSetIfChanged(ref _layoutRoot, value);
    }

    public BoxNode? SelectedPane
    {
        get => _selectedPane;
        set => RaiseAndSetIfChanged(ref _selectedPane, value);
    }

    public bool IsPreviewPanelVisible
    {
        get => _isPreviewPanelVisible;
        set
        {
            if (RaiseAndSetIfChanged(ref _isPreviewPanelVisible, value))
            {
                if (value)
                {
                    ShowSimulationPreview();
                }
                else
                {
                    HideSimulationPreview();
                }
            }
        }
    }

    public ICommand SplitLeftCommand { get; }
    public ICommand SplitRightCommand { get; }
    public ICommand SplitUpCommand { get; }
    public ICommand SplitDownCommand { get; }
    public ICommand ClosePaneCommand { get; }
    public ICommand ResetLayoutCommand { get; }
    public ICommand FloatPaneCommand { get; }

    private string? _leftDiffPayload;
    private string _leftDiffTitle = "Original / Request";
    private string? _rightDiffPayload;
    private string _rightDiffTitle = "Modified / Response";

    public ICommand SetDiffLeftEventCommand { get; }
    public ICommand SetDiffRightEventCommand { get; }
    public ICommand SetDiffLeftRequestCommand { get; }
    public ICommand SetDiffRightRequestCommand { get; }

    public MainWindowViewModel(ICdpService? cdpService = null, bool loadState = false)
    {
        CdpService = cdpService ?? new CdpService();

        Connection = new ConnectionViewModel(CdpService);
        Elements = new ElementsViewModel(CdpService);
        Console = new ConsoleViewModel(CdpService, () => Recorder?.TestStudio);
        Sources = new SourcesViewModel(CdpService);
        Network = new NetworkViewModel(CdpService);
        Performance = new PerformanceViewModel(CdpService);
        Profiler = new ProfilerViewModel(CdpService);
        Memory = new MemoryViewModel(CdpService);
        Application = new ApplicationViewModel(CdpService);
        Audits = new AuditsViewModel(CdpService, nodeId => Elements.SelectNodeById(nodeId));
        Simulation = new SimulationViewModel(
            CdpService,
            getSelectedNodeFunc: () => Elements.SelectedNode,
            isHighlightActiveFunc: () => Elements.IsHighlightActive,
            getAxDetailsFunc: nodeId => Elements.FindAxDetails(nodeId),
            isInspectModeActiveFunc: () => Connection.IsInspectModeActive,
            getDomNodeFunc: nodeId => Elements.FindDomNode(nodeId),
            useAutomationSelectorsFunc: () => Connection.UseAutomationSelectors
        );
        Recorder = new RecorderViewModel(CdpService, () => Connection.GeneratorHostAddress, () => Connection.UseAutomationSelectors);
        Events = new EventsViewModel(CdpService);
        Mvvm = new MvvmViewModel(CdpService);
        Diff = new DiffViewModel();

        SetDiffLeftEventCommand = new RelayCommand(() =>
        {
            if (Events.SelectedEvent != null)
            {
                _leftDiffTitle = $"Event: {Events.SelectedEvent.Method} ({Events.SelectedEvent.Timestamp})";
                _leftDiffPayload = Events.SelectedEvent.ParamsJson;
                TriggerDiffUpdateIfReady();
            }
        });

        SetDiffRightEventCommand = new RelayCommand(() =>
        {
            if (Events.SelectedEvent != null)
            {
                _rightDiffTitle = $"Event: {Events.SelectedEvent.Method} ({Events.SelectedEvent.Timestamp})";
                _rightDiffPayload = Events.SelectedEvent.ParamsJson;
                TriggerDiffUpdateIfReady();
            }
        });

        SetDiffLeftRequestCommand = new RelayCommand(() =>
        {
            if (Network.SelectedRequest != null)
            {
                _leftDiffTitle = $"Request: {Network.SelectedRequest.Method} {Network.SelectedRequest.Url}";
                _leftDiffPayload = Network.SelectedRequest.ResponseBody;
                TriggerDiffUpdateIfReady();
            }
        });

        SetDiffRightRequestCommand = new RelayCommand(() =>
        {
            if (Network.SelectedRequest != null)
            {
                _rightDiffTitle = $"Request: {Network.SelectedRequest.Method} {Network.SelectedRequest.Url}";
                _rightDiffPayload = Network.SelectedRequest.ResponseBody;
                TriggerDiffUpdateIfReady();
            }
        });

        Recorder.TestStudio.Connection = Connection;
        Connection.TestStudio = Recorder.TestStudio;
        Recorder.TestStudio.OnStepIndicatorChanged = indicator => Simulation.ActiveReplayIndicator = indicator;

        Simulation.InteractionDispatched += (sender, args) =>
        {
            if (Recorder.IsRecording && Recorder.IsClientSideRecording)
            {
                Recorder.AddRecordedStepLocal(args.Step);
            }
        };

        Connection.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ConnectionViewModel.UseAutomationSelectors))
            {
                UpdateSelectedSelector();
            }
            else if (e.PropertyName == nameof(ConnectionViewModel.IsInspectModeActive))
            {
                if (!Connection.IsInspectModeActive)
                {
                    Simulation.ClearInspectHover();
                    _ = Simulation.TriggerHighlightRefreshAsync();
                }
                else
                {
                    Simulation.ResetInspectHoverCache();
                }
            }
        };

        Elements.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ElementsViewModel.SelectedNode) || e.PropertyName == nameof(ElementsViewModel.SelectedAxNode))
            {
                Simulation.RaiseCanExecuteChangedForAll();
                if (Connection.IsInspectModeActive && !Elements.IsSelectingProgrammatically)
                {
                    Connection.IsInspectModeActive = false;
                }
            }

            if (e.PropertyName == nameof(ElementsViewModel.SelectedNode))
            {
                UpdateSelectedSelector();
                _ = Simulation.TriggerHighlightRefreshAsync();
            }
            else if (e.PropertyName == nameof(ElementsViewModel.IsHighlightActive))
            {
                _ = Simulation.TriggerHighlightRefreshAsync();
            }
            else if (e.PropertyName == nameof(ElementsViewModel.ShowVisualTree) || e.PropertyName == nameof(ElementsViewModel.SelectedTreeTabIndex))
            {
                Simulation.ResetInspectHoverCache();
            }
        };

        Sources.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(SourcesViewModel.IsDebuggerPaused))
            {
                if (Sources.IsDebuggerPaused)
                {
                    NavigateToView("Sources");
                }
            }
        };

        // Initialize Split commands
        SplitLeftCommand = new RelayCommand(() => SplitSelected(Avalonia.Layout.Orientation.Horizontal, true));
        SplitRightCommand = new RelayCommand(() => SplitSelected(Avalonia.Layout.Orientation.Horizontal, false));
        SplitUpCommand = new RelayCommand(() => SplitSelected(Avalonia.Layout.Orientation.Vertical, true));
        SplitDownCommand = new RelayCommand(() => SplitSelected(Avalonia.Layout.Orientation.Vertical, false));
        ClosePaneCommand = new RelayCommand(CloseSelected);
        ResetLayoutCommand = new RelayCommand(ResetLayout);
        FloatPaneCommand = new RelayCommand(FloatSelected);

        // Set up the default split layout
        ResetLayout();

        // Initialize state service and load state
        StateService = new StateService();
        StateService.RegisterProvider(Connection);
        StateService.RegisterProvider(Recorder.TestStudio);
        StateService.RegisterProvider(Elements);
        StateService.RegisterProvider(Console);
        StateService.RegisterProvider(Sources);
        StateService.RegisterProvider(Network);
        StateService.RegisterProvider(Memory);
        StateService.RegisterProvider(Application);
        StateService.RegisterProvider(Simulation);
        StateService.RegisterProvider(Mvvm);
        StateService.RegisterProvider(this);
        if (loadState)
        {
            StateService.Load();
        }
    }

    private void UpdateSelectedSelector()
    {
        var node = Elements.SelectedNode;
        if (node != null)
        {
            var generator = ClientSelectorRegistry.GetGenerator(Connection.UseAutomationSelectors ? "automation" : "dom");
            var selector = generator.GenerateSelector(node);
            Recorder.TestStudio.SelectedElementSelector = selector ?? "";
        }
        else
        {
            Recorder.TestStudio.SelectedElementSelector = "";
        }
    }

    private void SplitSelected(Avalonia.Layout.Orientation orientation, bool insertBefore)
    {
        var selected = SelectedPane;
        if (selected == null) return;

        BoxNode newBox;

        if (selected.Tabs.Count > 1 && selected.ActiveTab != null)
        {
            var activeTab = selected.ActiveTab;

            selected.Tabs.Remove(activeTab);
            if (selected.Tabs.Count > 0)
            {
                selected.ActiveTab = selected.Tabs[0];
            }

            newBox = new BoxNode
            {
                BackgroundTint = "#292a2d"
            };
            newBox.Tabs.Add(activeTab);
            newBox.ActiveTab = activeTab;
        }
        else
        {
            // Choose a next view sequentially that is not already visible, or a default fallback
            string[] views = { "Console", "Recorder", "Sources", "Network", "Memory", "Application", "Audits", "Mvvm" };
            string viewName = "Console";
            foreach (var v in views)
            {
                if (FindBoxNodeByViewName(LayoutRoot, v) == null)
                {
                    viewName = v;
                    break;
                }
            }

            newBox = new BoxNode
            {
                BackgroundTint = "#292a2d"
            };
            newBox.AddTab(viewName, GetIconKeyForView(viewName), viewName);
        }

        if (selected == LayoutRoot)
        {
            var newContainer = insertBefore
                ? new SplitContainerNode(orientation, newBox, selected)
                : new SplitContainerNode(orientation, selected, newBox);
            LayoutRoot = newContainer;
        }
        else if (selected.Parent is SplitContainerNode parent)
        {
            var newContainer = insertBefore
                ? new SplitContainerNode(orientation, newBox, selected)
                : new SplitContainerNode(orientation, selected, newBox);

            if (parent.Child1 == selected)
            {
                parent.Child1 = newContainer;
            }
            else
            {
                parent.Child2 = newContainer;
            }
        }

        SelectedPane = newBox;
    }

    private void CloseSelected()
    {
        var selected = SelectedPane;
        if (selected == null || selected == LayoutRoot) return;

        if (selected.SelectedViewName == "Simulation")
        {
            _isPreviewPanelVisible = false;
            OnPropertyChanged(nameof(IsPreviewPanelVisible));
        }

        if (selected.Parent is SplitContainerNode parent)
        {
            var sibling = parent.Child1 == selected ? parent.Child2 : parent.Child1;
            var grandparent = parent.Parent;

            if (parent == LayoutRoot)
            {
                sibling.Parent = null;
                LayoutRoot = sibling;
                if (sibling is BoxNode boxSibling) SelectedPane = boxSibling;
            }
            else if (grandparent is SplitContainerNode gp)
            {
                if (gp.Child1 == parent)
                {
                    gp.Child1 = sibling;
                }
                else
                {
                    gp.Child2 = sibling;
                }
                if (sibling is BoxNode boxSibling) SelectedPane = boxSibling;
            }
        }
    }

    private void FloatSelected()
    {
        var selected = SelectedPane;
        if (selected == null) return;

        BoxNode nodeToFloat;

        if (selected.Tabs.Count > 1 && selected.ActiveTab != null)
        {
            var activeTab = selected.ActiveTab;
            selected.Tabs.Remove(activeTab);
            if (selected.Tabs.Count > 0)
            {
                selected.ActiveTab = selected.Tabs[0];
            }

            nodeToFloat = new BoxNode
            {
                BackgroundTint = "#292a2d"
            };
            nodeToFloat.Tabs.Add(activeTab);
            nodeToFloat.ActiveTab = activeTab;
        }
        else
        {
            nodeToFloat = selected;

            if (selected == LayoutRoot)
            {
                LayoutRoot = null;
                SelectedPane = null;
            }
            else if (selected.Parent is SplitContainerNode parent)
            {
                var sibling = parent.Child1 == selected ? parent.Child2 : parent.Child1;
                var grandparent = parent.Parent;

                if (parent == LayoutRoot)
                {
                    sibling.Parent = null;
                    LayoutRoot = sibling;
                }
                else if (grandparent is SplitContainerNode gp)
                {
                    sibling.Parent = gp;
                    if (gp.Child1 == parent) gp.Child1 = sibling;
                    else gp.Child2 = sibling;
                }

                if (sibling is BoxNode boxSibling) SelectedPane = boxSibling;
                else if (sibling is SplitContainerNode sc) SelectedPane = FindFirstBoxNode(sc);
            }
        }

        CDP.Editor.Splits.Models.SuperSplitDragManager.FloatNodeCallback?.Invoke(null, nodeToFloat);
    }

    private void ResetLayout()
    {
        var simulationPane = new BoxNode
        {
            BackgroundTint = "#292a2d" // Neutral dark background for screencast simulation
        };
        simulationPane.AddTab("Simulation Preview", "PreviewLinkIcon", "Simulation");

        var rightPane = new BoxNode
        {
            BackgroundTint = "#292a2d"
        };
        rightPane.AddTab("Elements", "CodeIcon", "Elements");
        rightPane.AddTab("Console", "TerminalIcon", "Console");
        rightPane.AddTab("Sources", "DocumentIcon", "Sources");
        rightPane.AddTab("Network", "GlobeIcon", "Network");
        rightPane.AddTab("Performance", "TimerIcon", "Performance");
        rightPane.AddTab("Profiler", "SaveIcon", "Profiler");
        rightPane.AddTab("Memory", "DeveloperBoardIcon", "Memory");
        rightPane.AddTab("Application", "AppsIcon", "Application");
        rightPane.AddTab("Audits", "EyeIcon", "Audits");
        rightPane.AddTab("Recorder", "RecordIcon", "Recorder");
        rightPane.AddTab("Window", "WindowMultipleIcon", "Window");
        rightPane.AddTab("Events", "FlowchartIcon", "Events");
        rightPane.AddTab("MVVM", "DiagramIcon", "Mvvm");

        LayoutRoot = new SplitContainerNode(
            Avalonia.Layout.Orientation.Horizontal,
            simulationPane,
            rightPane
        ) { SplitterRatio = 0.35 };

        SelectedPane = rightPane;
        _isPreviewPanelVisible = true;
        OnPropertyChanged(nameof(IsPreviewPanelVisible));
    }

    private void ShowSimulationPreview()
    {
        var sim = FindBoxNodeByViewName(LayoutRoot, "Simulation");
        if (sim != null) return;

        var simNode = new BoxNode
        {
            BackgroundTint = "#292a2d"
        };
        simNode.AddTab("Simulation Preview", "PreviewLinkIcon", "Simulation");

        if (LayoutRoot == null)
        {
            LayoutRoot = simNode;
        }
        else
        {
            LayoutRoot = new SplitContainerNode(Avalonia.Layout.Orientation.Horizontal, simNode, LayoutRoot)
            {
                SplitterRatio = 0.35
            };
        }
    }

    private void HideSimulationPreview()
    {
        var sim = FindBoxNodeByViewName(LayoutRoot, "Simulation");
        if (sim == null) return;

        if (sim == LayoutRoot)
        {
            LayoutRoot = null;
            SelectedPane = null;
        }
        else if (sim.Parent is SplitContainerNode parent)
        {
            var sibling = parent.Child1 == sim ? parent.Child2 : parent.Child1;
            var grandparent = parent.Parent;

            if (parent == LayoutRoot)
            {
                sibling.Parent = null;
                LayoutRoot = sibling;
            }
            else if (grandparent is SplitContainerNode gp)
            {
                if (gp.Child1 == parent)
                {
                    gp.Child1 = sibling;
                }
                else
                {
                    gp.Child2 = sibling;
                }
            }
            if (SelectedPane == sim)
            {
                SelectedPane = sibling as BoxNode;
            }
        }
    }

    private BoxNode? FindBoxNodeByViewName(SplitNode? node, string viewName)
    {
        if (node == null) return null;
        if (node is BoxNode box)
        {
            foreach (var tab in box.Tabs)
            {
                if (tab.SelectedViewName == viewName) return box;
            }
        }
        if (node is SplitContainerNode container)
        {
            var found = FindBoxNodeByViewName(container.Child1, viewName);
            if (found != null) return found;
            return FindBoxNodeByViewName(container.Child2, viewName);
        }
        return null;
    }

    public void NavigateToView(string viewName)
    {
        var box = FindBoxNodeByViewName(LayoutRoot, viewName);
        if (box != null)
        {
            SelectedPane = box;
        }
        else if (SelectedPane != null)
        {
            SelectedPane.SelectedViewName = viewName;
            SelectedPane.Title = viewName;
            SelectedPane.IconKey = GetIconKeyForView(viewName);
        }
    }

    public void NavigateToSource(string filePath, int line)
    {
        var node = Sources.FindFileBySuffix(filePath);
        if (node != null)
        {
            Sources.PendingScrollLine = line;
            Sources.SelectedFile = node;
            NavigateToView("Sources");
        }
    }

    public void SetDiffOperands(string leftTitle, string leftContent, string rightTitle, string rightContent)
    {
        Diff.SetCompareTexts(leftTitle, leftContent, rightTitle, rightContent);
        NavigateToView("Diff");
    }

    private void TriggerDiffUpdateIfReady()
    {
        Diff.SetCompareTexts(_leftDiffTitle, _leftDiffPayload ?? "", _rightDiffTitle, _rightDiffPayload ?? "");
        NavigateToView("Diff");
    }

    public static string GetIconKeyForView(string viewName)
    {
        return viewName switch
        {
            "Simulation" => "PreviewLinkIcon",
            "Elements" => "CodeIcon",
            "Console" => "TerminalIcon",
            "Sources" => "DocumentIcon",
            "Network" => "GlobeIcon",
            "Performance" => "TimerIcon",
            "Profiler" => "SaveIcon",
            "Memory" => "DeveloperBoardIcon",
            "Application" => "AppsIcon",
            "Audits" => "EyeIcon",
            "Recorder" => "RecordIcon",
            "Window" => "WindowMultipleIcon",
            "Events" => "FlowchartIcon",
            "Mvvm" => "DiagramIcon",
            "Diff" => "CodeIcon",
            _ => "DocumentIcon"
        };
    }

    #region IStateProvider Implementation

    public string StateKey => "layout";

    public JsonNode? SaveState()
    {
        var root = new JsonObject();
        root["isPreviewPanelVisible"] = IsPreviewPanelVisible;
        root["layoutRoot"] = SerializeSplitNode(LayoutRoot);
        return root;
    }

    public void LoadState(JsonNode? stateNode)
    {
        if (stateNode is not JsonObject json) return;

        var isPreviewVisible = (bool?)json["isPreviewPanelVisible"] ?? true;
        
        var layoutRootNode = json["layoutRoot"];
        if (layoutRootNode != null)
        {
            var root = DeserializeSplitNode(layoutRootNode);
            if (root != null)
            {
                // Find selected pane in the deserialized tree
                BoxNode? selected = FindSelectedPane(root);
                
                _isPreviewPanelVisible = isPreviewVisible;
                LayoutRoot = root;
                SelectedPane = selected ?? FindFirstBoxNode(root);
                OnPropertyChanged(nameof(IsPreviewPanelVisible));
                return;
            }
        }
    }

    private JsonNode? SerializeSplitNode(SplitNode? node)
    {
        if (node == null) return null;

        var json = new JsonObject();
        if (node is BoxNode box)
        {
            json["type"] = "box";
            json["backgroundTint"] = box.BackgroundTint;
            json["isSelected"] = box.IsSelected;
            
            var tabsArray = new JsonArray();
            foreach (var tab in box.Tabs)
            {
                var tabJson = new JsonObject
                {
                    ["title"] = tab.Title,
                    ["iconKey"] = tab.IconKey,
                    ["selectedViewName"] = tab.SelectedViewName
                };
                tabsArray.Add(tabJson);
            }
            json["tabs"] = tabsArray;
            
            if (box.ActiveTab != null)
            {
                json["activeViewName"] = box.ActiveTab.SelectedViewName;
            }
        }
        else if (node is SplitContainerNode container)
        {
            json["type"] = "container";
            json["orientation"] = container.Orientation.ToString();
            json["splitterRatio"] = container.SplitterRatio;
            json["child1"] = SerializeSplitNode(container.Child1);
            json["child2"] = SerializeSplitNode(container.Child2);
        }

        return json;
    }

    private SplitNode? DeserializeSplitNode(JsonNode? node)
    {
        if (node == null || node is not JsonObject json) return null;

        var type = (string?)json["type"];
        if (type == "box")
        {
            var box = new BoxNode
            {
                BackgroundTint = (string?)json["backgroundTint"],
                IsSelected = (bool?)json["isSelected"] ?? false
            };

            var tabsArray = json["tabs"] as JsonArray;
            if (tabsArray != null)
            {
                foreach (var tabNode in tabsArray)
                {
                    if (tabNode is JsonObject tabJson)
                    {
                        var title = (string?)tabJson["title"] ?? "";
                        var iconKey = (string?)tabJson["iconKey"] ?? "";
                        var viewName = (string?)tabJson["selectedViewName"] ?? "";
                        box.AddTab(title, iconKey, viewName);
                    }
                }
            }

            var activeViewName = (string?)json["activeViewName"];
            if (!string.IsNullOrEmpty(activeViewName))
            {
                foreach (var tab in box.Tabs)
                {
                    if (tab.SelectedViewName == activeViewName)
                    {
                        box.ActiveTab = tab;
                        break;
                    }
                }
            }

            return box;
        }
        else if (type == "container")
        {
            var orientationStr = (string?)json["orientation"] ?? "Horizontal";
            var orientation = Enum.TryParse<Avalonia.Layout.Orientation>(orientationStr, out var orient) ? orient : Avalonia.Layout.Orientation.Horizontal;
            var splitterRatio = (double?)json["splitterRatio"] ?? 0.5;

            var child1 = DeserializeSplitNode(json["child1"]);
            var child2 = DeserializeSplitNode(json["child2"]);

            if (child1 != null && child2 != null)
            {
                var container = new SplitContainerNode(orientation, child1, child2)
                {
                    SplitterRatio = splitterRatio
                };
                return container;
            }
        }

        return null;
    }

    private BoxNode? FindSelectedPane(SplitNode? node)
    {
        if (node == null) return null;
        if (node is BoxNode box && box.IsSelected) return box;
        if (node is SplitContainerNode container)
        {
            var found = FindSelectedPane(container.Child1);
            if (found != null) return found;
            return FindSelectedPane(container.Child2);
        }
        return null;
    }

    private BoxNode? FindFirstBoxNode(SplitNode? node)
    {
        if (node == null) return null;
        if (node is BoxNode box) return box;
        if (node is SplitContainerNode container)
        {
            var found = FindFirstBoxNode(container.Child1);
            if (found != null) return found;
            return FindFirstBoxNode(container.Child2);
        }
        return null;
    }

    #endregion
}
