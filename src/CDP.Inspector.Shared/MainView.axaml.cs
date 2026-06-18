using Avalonia.Controls;
using Avalonia.Threading;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        
        var vm = new MainWindowViewModel();
        DataContext = vm;

        // Scan targets on load
        Dispatcher.UIThread.Post(() => vm.Connection.RefreshTargetsCommand.Execute(null));
    }
}
