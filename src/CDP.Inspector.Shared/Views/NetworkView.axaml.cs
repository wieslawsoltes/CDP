using Avalonia.Controls;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class NetworkView : UserControl
{
    public Button BtnClearNetwork => btnClearNetwork;
    public DataGrid LstNetworkRequests => lstNetworkRequests;
    public TextBlock LblNetUrl => lblNetUrl;
    public TextBox TxtNetReqHeaders => txtNetReqHeaders;
    public TextBox TxtNetResHeaders => txtNetResHeaders;
    public TextBox TxtNetBody => txtNetBody;

    public NetworkView()
    {
        InitializeComponent();
    }
}
