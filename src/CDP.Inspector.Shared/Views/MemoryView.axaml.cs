using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

public partial class MemoryView : UserControl
{
    public MemoryView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel mainVM)
        {
            mainVM.Memory.SaveFileCallback = async (json) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Export Heap Snapshot",
                        DefaultExtension = "heapsnapshot",
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("V8 Heap Snapshot")
                            {
                                Patterns = new[] { "*.heapsnapshot" }
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
