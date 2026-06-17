using Avalonia.Controls;

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
    }
}
