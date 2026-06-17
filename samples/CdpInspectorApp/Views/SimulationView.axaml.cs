using Avalonia.Controls;

namespace CdpInspectorApp.Views;

public partial class SimulationView : UserControl
{
    public Button BtnClick => btnClick;
    public TextBox TxtScrollDeltaY => txtScrollDeltaY;
    public Button BtnScroll => btnScroll;
    public TextBox TxtInputSim => txtInputSim;
    public Button BtnSendText => btnSendText;
    public ComboBox CbKeys => cbKeys;
    public Button BtnSendKey => btnSendKey;

    public TextBox TxtWidth => txtWidth;
    public TextBox TxtHeight => txtHeight;
    public Button BtnResize => btnResize;
    public Button BtnResizeReset => btnResizeReset;
    public Button BtnCaptureScreenshot => btnCaptureScreenshot;
    public Image ImgScreenshot => imgScreenshot;

    public SimulationView()
    {
        InitializeComponent();
    }
}
