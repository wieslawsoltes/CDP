using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CdpGalleryApp.Views;

public partial class MarkdownPage : UserControl
{
    public MarkdownPage()
    {
        InitializeComponent();
    }

    private async void BtnLoadMarkdown_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open Markdown File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Markdown Files")
                    {
                        Patterns = new[] { "*.md", "*.markdown", "*.txt" }
                    }
                }
            });

            if (files == null || files.Count == 0) return;

            var file = files[0];
            await using var stream = await file.OpenReadAsync();
            using var reader = new System.IO.StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            if (DataContext is ViewModels.MarkdownPageViewModel vm)
            {
                vm.Text = content;
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load markdown: {ex.Message}");
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
