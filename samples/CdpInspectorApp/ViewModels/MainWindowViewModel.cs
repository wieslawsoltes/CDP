using System;
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
    public ApplicationViewModel Application { get; }
    public SimulationViewModel Simulation { get; }
    public RecorderViewModel Recorder { get; }

    public MainWindowViewModel(ICdpService? cdpService = null)
    {
        CdpService = cdpService ?? new CdpService();

        Connection = new ConnectionViewModel(CdpService);
        Elements = new ElementsViewModel(CdpService);
        Console = new ConsoleViewModel(CdpService);
        Sources = new SourcesViewModel(CdpService);
        Network = new NetworkViewModel(CdpService);
        Performance = new PerformanceViewModel(CdpService);
        Application = new ApplicationViewModel(CdpService);
        Simulation = new SimulationViewModel(CdpService, () => Elements.SelectedNode);
        Recorder = new RecorderViewModel(CdpService, () => Connection.HostAddress);

        // Notify simulation command availability when element selection changes
        Elements.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ElementsViewModel.SelectedNode))
            {
                Simulation.RaiseCanExecuteChangedForAll();
            }
        };
    }
}
