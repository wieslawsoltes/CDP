using Avalonia.Controls;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

public partial class ConsoleView : UserControl
{
    public Button BtnClearLogs => btnClearLogs;
    public ListBox ListLogs => listLogs;
    public ListBox ListConsole => listConsole;
    public TextBox TxtConsoleInput => txtConsoleInput;
    public Button BtnSendConsole => btnSendConsole;

    public ConsoleView()
    {
        InitializeComponent();
        txtConsoleInput.KeyDown += (sender, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.Console.EvaluateCommand.Execute(null);
                    e.Handled = true;
                }
            }
        };
    }
}
