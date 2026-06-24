using System;
using System.Globalization;
using System.IO;
using Avalonia.Controls;
using Avalonia.Data.Converters;
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
    public DataGrid LstRecordedSteps => lstRecordedSteps;
    public TextEditor TxtGeneratedCode => txtGeneratedCode;

    public static readonly IValueConverter IsSelectorVisibleConverter = new StepVisibilityConverter(
        type => type == "click" || type == "change" || type == "dragAndDrop" || type == "scroll");

    public static readonly IValueConverter IsValueVisibleConverter = new StepVisibilityConverter(
        type => type == "change");

    public static readonly IValueConverter IsUrlVisibleConverter = new StepVisibilityConverter(
        type => type == "navigate");

    public static readonly IValueConverter IsKeyVisibleConverter = new StepVisibilityConverter(
        type => type == "keydown");

    public static readonly IValueConverter IsViewportVisibleConverter = new StepVisibilityConverter(
        type => type == "setViewport");

    public static readonly IValueConverter IsCoordinatesVisibleConverter = new StepVisibilityConverter(
        type => type == "click" || type == "scroll");

    public static readonly IValueConverter CoordinatesLabelConverter = new StepLabelConverter();

    public static readonly IValueConverter IsClickDetailsVisibleConverter = new StepVisibilityConverter(
        type => type == "click");

    public static readonly IValueConverter IsDragDetailsVisibleConverter = new StepVisibilityConverter(
        type => type == "dragAndDrop");

    public static readonly IValueConverter IsModifiersVisibleConverter = new StepVisibilityConverter(
        type => type == "click" || type == "keydown" || type == "scroll" || type == "dragAndDrop");

    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;

    public RecorderView()
    {
        InitializeComponent();
        
        var editor = txtGeneratedCode;
        if (editor != null)
        {
            if (!OperatingSystem.IsBrowser())
            {
                try
                {
                    _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                    _textMateInstallation = editor.InstallTextMate(_registryOptions);
                    var jsLanguage = _registryOptions.GetLanguageByExtension(".js");
                    if (jsLanguage != null)
                    {
                        _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(jsLanguage.Id));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RecorderView] Failed to initialize TextMate: {ex.Message}");
                }
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

public class StepVisibilityConverter : IValueConverter
{
    private readonly Func<string, bool> _check;
    public StepVisibilityConverter(Func<string, bool> check) => _check = check;
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && _check(s);
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class StepLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && s == "scroll" ? "Delta X / Y:" : "Coordinates Offset:";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
