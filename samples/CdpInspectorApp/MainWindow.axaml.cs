using Avalonia.Controls;
using Avalonia.Threading;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        var vm = new MainWindowViewModel();
        DataContext = vm;

        // Scan targets on load
        Dispatcher.UIThread.Post(() => vm.Connection.RefreshTargetsCommand.Execute(null));
    }

    public void LoadScriptContent(string content)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Recorder.LoadScriptContent(content);
        }
    }
}
