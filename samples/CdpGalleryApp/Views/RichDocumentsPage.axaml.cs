using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CdpGalleryApp.Views;

public partial class RichDocumentsPage : UserControl
{
    public RichDocumentsPage()
    {
        InitializeComponent();
    }

    private async void BtnLoadCustom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open Document",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("All Supported Documents")
                    {
                        Patterns = new[] { "*.docx", "*.xlsx", "*.pptx", "*.rtf" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("Word Documents")
                    {
                        Patterns = new[] { "*.docx" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("Excel Spreadsheets")
                    {
                        Patterns = new[] { "*.xlsx" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("PowerPoint Presentations")
                    {
                        Patterns = new[] { "*.pptx" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("Rich Text Format")
                    {
                        Patterns = new[] { "*.rtf" }
                    }
                }
            });

            if (files == null || files.Count == 0) return;

            var file = files[0];
            var localPath = file.Path.LocalPath;

            if (DataContext is ViewModels.RichDocumentsPageViewModel vm)
            {
                vm.FilePath = localPath;
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load custom document: {ex.Message}");
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void BtnBold_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var editor = this.FindControl<CDP.Document.Editor.DocumentEditor>("DocEditor");
        editor?.ToggleBold();
    }

    private void BtnItalic_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var editor = this.FindControl<CDP.Document.Editor.DocumentEditor>("DocEditor");
        editor?.ToggleItalic();
    }

    private void BtnUnderline_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var editor = this.FindControl<CDP.Document.Editor.DocumentEditor>("DocEditor");
        editor?.ToggleUnderline();
    }

    private void ComboFontSize_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && double.TryParse(item.Content?.ToString(), out double size))
        {
            var editor = this.FindControl<CDP.Document.Editor.DocumentEditor>("DocEditor");
            editor?.SetFontSize(size);
        }
    }

    private void BtnColor_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hexColor)
        {
            var editor = this.FindControl<CDP.Document.Editor.DocumentEditor>("DocEditor");
            editor?.SetFontColor(hexColor);
        }
    }
}
