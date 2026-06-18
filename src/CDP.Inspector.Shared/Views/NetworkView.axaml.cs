using Avalonia.Controls;

namespace CdpInspectorApp.Views;

public partial class NetworkView : UserControl
{
    public Button BtnClearNetwork => btnClearNetwork;
    public ListBox LstNetworkRequests => lstNetworkRequests;
    public TextBlock LblNetUrl => lblNetUrl;
    public TextBox TxtNetReqHeaders => txtNetReqHeaders;
    public TextBox TxtNetResHeaders => txtNetResHeaders;
    public TextBox TxtNetBody => txtNetBody;

    public NetworkView()
    {
        InitializeComponent();
    }
}
