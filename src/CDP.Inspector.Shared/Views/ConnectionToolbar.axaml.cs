using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace CdpInspectorApp.Views;

public partial class ConnectionToolbar : UserControl
{
    public Controls.EditableComboBox TxtHost => txtHost;
    public ComboBox CbTargets => cbTargets;
    public Button BtnRefreshTargets => btnRefreshTargets;
    public Button BtnConnect => btnConnect;
    public Button BtnDisconnect => btnDisconnect;
    public ToggleButton BtnInspect => btnInspect;
    public Button BtnReload => btnReload;
    public Control TxtConnectionStatus => txtConnectionStatus;

    public ConnectionToolbar()
    {
        InitializeComponent();
    }
}
