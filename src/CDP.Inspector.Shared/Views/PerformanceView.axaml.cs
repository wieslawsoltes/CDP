using Avalonia.Controls;
using CdpInspectorApp.Controls;

namespace CdpInspectorApp.Views;

public partial class PerformanceView : UserControl
{
    public Button BtnRefreshMetrics => btnRefreshMetrics;
    public Button BtnCollectGarbage => btnCollectGarbage;
    public TextBlock LblPerfNodes => lblPerfNodes;
    public TextBlock LblPerfDocuments => lblPerfDocuments;
    public TextBlock LblPerfMemory => lblPerfMemory;
    public TextBlock LblPerfGc => lblPerfGc;
    public TextBlock LblPerfPid => lblPerfPid;
    public TextBlock LblPerfOs => lblPerfOs;
    public Button BtnCloseTarget => btnCloseTarget;
    public MemoryChart CanvasMemoryChart => canvasMemoryChart;
    public DataGrid LstLiveControls => lstLiveControls;

    public PerformanceView()
    {
        InitializeComponent();
    }
}
