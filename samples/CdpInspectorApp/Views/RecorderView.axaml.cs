using Avalonia.Controls;

namespace CdpInspectorApp.Views;

public partial class RecorderView : UserControl
{
    public Button BtnToggleRecord => btnToggleRecord;
    public Button BtnReplay => btnReplay;
    public Button BtnClear => btnClear;
    public Button BtnLoad => btnLoad;
    public Button BtnExportPuppeteer => btnExportPuppeteer;
    public Button BtnExportJson => btnExportJson;
    public ListBox LstRecordedSteps => lstRecordedSteps;
    public TextBox TxtGeneratedCode => txtGeneratedCode;

    public RecorderView()
    {
        InitializeComponent();
    }
}
