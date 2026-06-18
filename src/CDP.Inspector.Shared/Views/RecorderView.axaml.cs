using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CdpInspectorApp.ViewModels;

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

    private async void BtnLoad_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load Recording",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("JSON/Puppeteer Recordings")
                    {
                        Patterns = new[] { "*.json", "*.js" }
                    }
                }
            });

            if (files == null || files.Count == 0) return;

            var file = files[0];
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            string content = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(content)) return;

            if (DataContext is MainWindowViewModel vm)
            {
                vm.Recorder.LoadScriptContent(content);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading script: {ex.Message}");
        }
    }

    private async void BtnExportPuppeteer_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Puppeteer Script",
                DefaultExtension = "js",
                SuggestedFileName = "recording.js"
            });
            if (file != null)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    await using var stream = await file.OpenWriteAsync();
                    await using var writer = new StreamWriter(stream);
                    await writer.WriteAsync(vm.Recorder.GeneratedCode);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting Puppeteer script: {ex.Message}");
        }
    }

    private async void BtnExportJson_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save JSON Recording",
                DefaultExtension = "json",
                SuggestedFileName = "recording.json"
            });
            if (file != null)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    string jsonContent = vm.Recorder.GetJsonRecording();
                    await using var stream = await file.OpenWriteAsync();
                    await using var writer = new StreamWriter(stream);
                    await writer.WriteAsync(jsonContent);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting JSON: {ex.Message}");
        }
    }
}
