using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CdpInspectorApp.Controls;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class ProfilerView : UserControl
{
    public Button BtnStartProfiler => btnStartProfiler;
    public Button BtnStopProfiler => btnStopProfiler;
    public Button BtnLoadProfile => btnLoadProfile;
    public DataGrid DgMethodStats => dgMethodStats;

    public ProfilerView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel mainVM)
        {
            mainVM.Profiler.SaveFileCallback = async (json) =>
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
                        using var writer = new StreamWriter(stream);
                        await writer.WriteAsync(json);
                    }
                }
            };

            mainVM.Profiler.OpenFileCallback = async () =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Load CPU Profile",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("V8 CPU Profile")
                            {
                                Patterns = new[] { "*.cpuprofile" }
                            }
                        }
                    });
                    if (files != null && files.Count > 0)
                    {
                        using var stream = await files[0].OpenReadAsync();
                        using var reader = new StreamReader(stream);
                        return await reader.ReadToEndAsync();
                    }
                }
                return null;
            };
        }
    }
}
