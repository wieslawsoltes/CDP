using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

public partial class TestStudioView : UserControl
{
    private bool _isUpdatingText = false;
    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;

    public TestStudioView()
    {
        InitializeComponent();
        
        var editor = this.FindControl<TextEditor>("txtYamlCode");
        if (editor != null)
        {
            // 1. Initialize TextMate with Dark+ theme
            _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            _textMateInstallation = editor.InstallTextMate(_registryOptions);
            try
            {
                var yamlLanguage = _registryOptions.GetLanguageByExtension(".yaml");
                if (yamlLanguage != null)
                {
                    _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(yamlLanguage.Id));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TestStudioView] Failed to initialize TextMate grammar: {ex.Message}");
            }

            // 2. Synchronize editor edits back to ViewModel
            editor.TextChanged += (s, e) =>
            {
                if (_isUpdatingText) return;
                if (DataContext is MainWindowViewModel vm)
                {
                    _isUpdatingText = true;
                    try
                    {
                        vm.Recorder.TestStudio.YamlCode = editor.Text;
                    }
                    finally
                    {
                        _isUpdatingText = false;
                    }
                }
            };
        }

        // 3. Synchronize ViewModel changes to editor
        DataContextChanged += (sender, args) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.Recorder.TestStudio.PropertyChanged -= TestStudio_PropertyChanged;
                    vm.Recorder.TestStudio.PropertyChanged += TestStudio_PropertyChanged;
                    UpdateEditorText(vm.Recorder.TestStudio.YamlCode);
                }
            });
        };
    }

    private void TestStudio_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TestStudioViewModel.YamlCode))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    UpdateEditorText(vm.Recorder.TestStudio.YamlCode);
                }
            });
        }
    }

    private void UpdateEditorText(string? text)
    {
        if (_isUpdatingText) return;
        _isUpdatingText = true;
        try
        {
            var editor = this.FindControl<TextEditor>("txtYamlCode");
            if (editor != null && editor.Text != text)
            {
                editor.Text = text ?? "";
            }
        }
        finally
        {
            _isUpdatingText = false;
        }
    }
}
