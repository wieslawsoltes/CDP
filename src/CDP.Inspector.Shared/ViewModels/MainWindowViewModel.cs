using System;
using System.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using CdpInspectorApp.Services;
using CDP.Editor.Splits.Models;

namespace CdpInspectorApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public ICdpService CdpService { get; }
    public ConnectionViewModel Connection { get; }
    public ElementsViewModel Elements { get; }
    public ConsoleViewModel Console { get; }
    public SourcesViewModel Sources { get; }
    public NetworkViewModel Network { get; }
    public PerformanceViewModel Performance { get; }
    public MemoryViewModel Memory { get; }
    public ApplicationViewModel Application { get; }
    public AuditsViewModel Audits { get; }
    public SimulationViewModel Simulation { get; }
    public RecorderViewModel Recorder { get; }

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



    public MainWindowViewModel(ICdpService? cdpService = null)
    {
        CdpService = cdpService ?? new CdpService();

        Connection = new ConnectionViewModel(CdpService);
        Elements = new ElementsViewModel(CdpService);
        Console = new ConsoleViewModel(CdpService, () => Recorder?.TestStudio);
        Sources = new SourcesViewModel(CdpService);
        Network = new NetworkViewModel(CdpService);
        Performance = new PerformanceViewModel(CdpService);
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

        // Set up the default split layout
        ResetLayout();
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

        // Choose a next view sequentially that is not already visible, or a default fallback
        string[] views = { "Console", "Recorder", "Sources", "Network", "Memory", "Application", "Audits" };
        string viewName = "Console";
        foreach (var v in views)
        {
            if (FindBoxNodeByViewName(LayoutRoot, v) == null)
            {
                viewName = v;
                break;
            }
        }

        var newBox = new BoxNode
        {
            BackgroundTint = "#292a2d"
        };
        newBox.AddTab(viewName, GetIconKeyForView(viewName), viewName);

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

    private void ResetLayout()
    {
        var simulationPane = new BoxNode
        {
            BackgroundTint = "#292a2d" // Neutral dark background for screencast simulation
        };
        simulationPane.AddTab("Simulation Preview", "PreviewLinkIcon", "Simulation");

        var elementsPane = new BoxNode
        {
            BackgroundTint = "#292a2d"
        };
        elementsPane.AddTab("Elements", "CodeIcon", "Elements");

        var consolePane = new BoxNode
        {
            BackgroundTint = "#292a2d"
        };
        consolePane.AddTab("Console", "TerminalIcon", "Console");

        var rightSplit = new SplitContainerNode(
            Avalonia.Layout.Orientation.Vertical,
            elementsPane,
            consolePane
        ) { SplitterRatio = 0.65 };

        LayoutRoot = new SplitContainerNode(
            Avalonia.Layout.Orientation.Horizontal,
            simulationPane,
            rightSplit
        ) { SplitterRatio = 0.35 };

        SelectedPane = simulationPane;
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
            "Memory" => "DeveloperBoardIcon",
            "Application" => "AppsIcon",
            "Audits" => "EyeIcon",
            "Recorder" => "RecordIcon",
            "Window" => "WindowMultipleIcon",
            _ => "DocumentIcon"
        };
    }
}
