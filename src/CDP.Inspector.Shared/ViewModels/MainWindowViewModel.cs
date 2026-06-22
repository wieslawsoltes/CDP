using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using CdpInspectorApp.Services;

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

    private bool _isPreviewPanelVisible = true;
    private GridLength _previewColumnWidth = new GridLength(1, GridUnitType.Star);
    private GridLength _splitterColumnWidth = new GridLength(4, GridUnitType.Pixel);

    public bool IsPreviewPanelVisible
    {
        get => _isPreviewPanelVisible;
        set
        {
            if (RaiseAndSetIfChanged(ref _isPreviewPanelVisible, value))
            {
                if (value)
                {
                    PreviewColumnWidth = new GridLength(1, GridUnitType.Star);
                    SplitterColumnWidth = new GridLength(4, GridUnitType.Pixel);
                }
                else
                {
                    PreviewColumnWidth = new GridLength(0, GridUnitType.Pixel);
                    SplitterColumnWidth = new GridLength(0, GridUnitType.Pixel);
                }
            }
        }
    }

    public GridLength PreviewColumnWidth
    {
        get => _previewColumnWidth;
        set => RaiseAndSetIfChanged(ref _previewColumnWidth, value);
    }

    public GridLength SplitterColumnWidth
    {
        get => _splitterColumnWidth;
        set => RaiseAndSetIfChanged(ref _splitterColumnWidth, value);
    }

    public MainWindowViewModel(ICdpService? cdpService = null)
    {
        CdpService = cdpService ?? new CdpService();

        Connection = new ConnectionViewModel(CdpService);
        Elements = new ElementsViewModel(CdpService);
        Console = new ConsoleViewModel(CdpService);
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
            getDomNodeFunc: nodeId => Elements.FindDomNode(nodeId)
        );
        Recorder = new RecorderViewModel(CdpService, () => Connection.HostAddress, () => Connection.UseAutomationSelectors);

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

        // Notify simulation command availability when element selection changes, and exit inspect mode
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
    }

    private void UpdateSelectedSelector()
    {
        var node = Elements.SelectedNode;
        if (node != null)
        {
            string selector = "";
            var visualProp = node.GetType().GetProperty("Visual");
            if (visualProp != null && visualProp.GetValue(node) is Avalonia.Visual visual)
            {
                selector = SelectorEngine.GetSelector(visual, useAutomation: Connection.UseAutomationSelectors);
            }

            if (string.IsNullOrEmpty(selector))
            {
                var generator = ClientSelectorRegistry.GetGenerator(Connection.UseAutomationSelectors ? "automation" : "dom");
                selector = generator.GenerateSelector(node);
            }

            Recorder.TestStudio.SelectedElementSelector = selector ?? "";
        }
        else
        {
            Recorder.TestStudio.SelectedElementSelector = "";
        }
    }
}
