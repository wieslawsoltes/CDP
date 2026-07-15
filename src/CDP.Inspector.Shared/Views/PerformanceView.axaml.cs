using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CdpInspectorApp.Controls;
using CdpInspectorApp.ViewModels;
using CDP.Editor.Splits.Controls;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class PerformanceView : UserControl
{
    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();

    public Button BtnRefreshMetrics => btnRefreshMetrics;
    public Button BtnCollectGarbage => btnCollectGarbage;
    public TextBlock LblPerfNodes => lblPerfNodes;
    public TextBlock LblPerfDocuments => lblPerfDocuments;
    public TextBlock LblPerfMemory => lblPerfMemory;
    public TextBlock LblPerfGc => lblPerfGc;
    public TextBlock LblPerfPid => lblPerfPid;
    public TextBlock LblPerfOs => lblPerfOs;
    public Button BtnCloseTarget => btnCloseTarget;
    public DataGrid LstLiveControls => lstLiveControls;

    private void DetachControl(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }
        else if (control.Parent is SuperSplitBox splitBox)
        {
            splitBox.InnerContent = null;
        }
    }

    public PerformanceView()
    {
        InitializeComponent();

        var statsPanel = PerformanceStatsPanel;
        var chartPanel = PerformanceChartPanel;
        var controlsPanel = PerformanceControlsPanel;

        HiddenPanel.Children.Clear();

        _viewsCache["PerformanceStats"] = statsPanel;
        _viewsCache["PerformanceChart"] = chartPanel;
        _viewsCache["PerformanceControls"] = controlsPanel;

        SplitControl.ViewResolver = (viewName, targetBox) =>
        {
            if (_viewsCache.TryGetValue(viewName, out var cached))
            {
                if (targetBox == null || cached.Parent != targetBox)
                {
                    DetachControl(cached);
                }
                return cached;
            }
            return new Control();
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel mainVM)
        {
            mainVM.Performance.SaveFileCallback = async (json) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Export CPU Profile",
                        DefaultExtension = "cpuprofile",
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("V8 CPU Profile")
                            {
                                Patterns = new[] { "*.cpuprofile" }
                            }
                        }
                    });
                    if (file != null)
                    {
                        using var stream = await file.OpenWriteAsync();
                        using var writer = new System.IO.StreamWriter(stream);
                        await writer.WriteAsync(json);
                    }
                }
            };
        }
    }
}
