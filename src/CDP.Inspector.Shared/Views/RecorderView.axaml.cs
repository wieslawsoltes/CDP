using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CdpInspectorApp.ViewModels;

using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

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
    public TextEditor TxtGeneratedCode => txtGeneratedCode;

    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;

    public RecorderView()
    {
        InitializeComponent();
        
        var editor = txtGeneratedCode;
        if (editor != null)
        {
            _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            _textMateInstallation = editor.InstallTextMate(_registryOptions);
            try
            {
                var jsLanguage = _registryOptions.GetLanguageByExtension(".js");
                if (jsLanguage != null)
                {
                    _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(jsLanguage.Id));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecorderView] Failed to initialize TextMate grammar: {ex.Message}");
            }
        }

        DataContextChanged += (sender, args) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.Recorder.PropertyChanged -= Recorder_PropertyChanged;
                    vm.Recorder.PropertyChanged += Recorder_PropertyChanged;
                    UpdateEditorText(vm.Recorder.GeneratedCode);
                }
            });
        };
    }

    private void Recorder_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecorderViewModel.GeneratedCode))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    UpdateEditorText(vm.Recorder.GeneratedCode);
                }
            });
        }
    }

    private void UpdateEditorText(string? text)
    {
        var editor = txtGeneratedCode;
        if (editor != null && editor.Text != text)
        {
            editor.Text = text ?? "";
        }
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
            if (DataContext is not MainWindowViewModel vm) return;

            string title = "Save Puppeteer Script";
            string extension = "js";
            string suggestedName = "recording.js";

            if (vm.Recorder.SelectedFormat == RecordingFormat.PlaywrightTest)
            {
                title = "Save Playwright Script";
                extension = "spec.js";
                suggestedName = "recording.spec.js";
            }
            else if (vm.Recorder.SelectedFormat == RecordingFormat.SeleniumCSharp)
            {
                title = "Save Selenium C# Script";
                extension = "cs";
                suggestedName = "SeleniumTests.cs";
            }
            else if (vm.Recorder.SelectedFormat == RecordingFormat.AppiumCSharp)
            {
                title = "Save Appium C# Script";
                extension = "cs";
                suggestedName = "AppiumTests.cs";
            }
            else if (vm.Recorder.SelectedFormat == RecordingFormat.AvaloniaHeadlessXUnit)
            {
                title = "Save Avalonia Headless Test";
                extension = "cs";
                suggestedName = "HeadlessTests.cs";
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                DefaultExtension = extension,
                SuggestedFileName = suggestedName
            });
            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(vm.Recorder.GeneratedCode);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting script: {ex.Message}");
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
