using Avalonia.Controls;
using CdpGalleryApp.ViewModels;
using System.IO;

namespace CdpGalleryApp.Views;

public partial class PdfPage : UserControl
{
    public PdfPage()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is PdfPageViewModel vm)
        {
            vm.RequestLoad += OnRequestLoad;
            vm.RequestSave += OnRequestSave;
            vm.RequestRotatePage += (deg) => PdfCanvas.RotateCurrentPage(deg);
            vm.RequestInsertPage += () => PdfCanvas.InsertPageAfterCurrent();
            vm.RequestDeletePage += () => PdfCanvas.DeleteCurrentPage();
            vm.RequestFitWidth += () => Avalonia.Threading.Dispatcher.UIThread.Post(() => PdfCanvas.FitToWidth(), Avalonia.Threading.DispatcherPriority.Background);
            vm.RequestFitPage += () => Avalonia.Threading.Dispatcher.UIThread.Post(() => PdfCanvas.FitToPage(), Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private async void OnRequestLoad(string defaultPath)
    {
        var toplevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
        if (toplevel == null) return;

        var files = await toplevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open PDF Document",
            AllowMultiple = false
        });

        if (files != null && files.Count > 0)
        {
            if (DataContext is PdfPageViewModel vm)
            {
                var localPath = files[0].Path.LocalPath;
                if (localPath != null)
                {
                    vm.FilePath = localPath;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => PdfCanvas.FitToWidth(), Avalonia.Threading.DispatcherPriority.Background);
                }
            }
        }
    }

    private async void OnRequestSave(string currentPath)
    {
        var toplevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
        if (toplevel == null) return;

        var file = await toplevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save PDF Document",
            SuggestedFileName = Path.GetFileName(currentPath),
            DefaultExtension = ".pdf"
        });

        if (file != null)
        {
            if (DataContext is PdfPageViewModel vm)
            {
                var localPath = file.Path.LocalPath;
                if (localPath != null)
                {
                    PdfCanvas.SaveDocument(localPath);
                    vm.FilePath = localPath;
                }
            }
        }
    }
}
