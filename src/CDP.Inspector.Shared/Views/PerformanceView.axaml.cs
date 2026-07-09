using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CdpInspectorApp.Controls;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
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
    public DataGrid LstLiveControls => lstLiveControls;

    public PerformanceView()
    {
        InitializeComponent();
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
