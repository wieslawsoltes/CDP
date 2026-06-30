using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace CdpInspectorApp.Views;

public partial class ConnectionToolbar : UserControl
{
    public AutoCompleteBox TxtHost => txtHost;
    public ComboBox CbTargets => cbTargets;
    public Button BtnRefreshTargets => btnRefreshTargets;
    public Button BtnConnect => btnConnect;
    public Button BtnDisconnect => btnDisconnect;
    public ToggleButton BtnInspect => btnInspect;
    public Button BtnReload => btnReload;
    public TextBlock TxtConnectionStatus => txtConnectionStatus;

    public ConnectionToolbar()
    {
        InitializeComponent();
    }
}
