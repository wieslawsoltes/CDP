using Avalonia.Controls;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = MainViewControl.DataContext;
    }

    public void LoadScriptContent(string content)
    {
        if (MainViewControl.DataContext is MainWindowViewModel vm)
        {
            vm.Recorder.LoadScriptContent(content);
        }
    }
}
