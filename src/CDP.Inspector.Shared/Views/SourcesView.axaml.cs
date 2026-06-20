using System;
using System.IO;
using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

public partial class SourcesView : UserControl
{
    public TreeView TreeWorkspaceFiles => treeWorkspaceFiles;
    public TextBlock LblSourceFileName => lblSourceFileName;
    public TextEditor TxtSourceContent => txtSourceContent;

    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;

    public SourcesView()
    {
        InitializeComponent();
        
        var editor = txtSourceContent;
        if (editor != null)
        {
            _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            _textMateInstallation = editor.InstallTextMate(_registryOptions);
        }

        DataContextChanged += (sender, args) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.Sources.PropertyChanged -= Sources_PropertyChanged;
                    vm.Sources.PropertyChanged += Sources_PropertyChanged;
                    UpdateEditorText(vm.Sources.SelectedFileContent);
                    UpdateHighlighting(vm.Sources.SelectedFileName);
                }
            });
        };
    }

    private void Sources_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (e.PropertyName == nameof(SourcesViewModel.SelectedFileContent))
                {
                    UpdateEditorText(vm.Sources.SelectedFileContent);
                }
                else if (e.PropertyName == nameof(SourcesViewModel.SelectedFileName))
                {
                    UpdateHighlighting(vm.Sources.SelectedFileName);
                }
            }
        });
    }

    private void UpdateEditorText(string? text)
    {
        var editor = txtSourceContent;
        if (editor != null && editor.Text != text)
        {
            editor.Text = text ?? "";
        }
    }

    private void UpdateHighlighting(string? fileName)
    {
        if (_textMateInstallation == null || _registryOptions == null || string.IsNullOrEmpty(fileName))
        {
            return;
        }

        try
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext == ".axaml")
            {
                ext = ".xml"; // Fallback to XML highlighting for Avalonia XAML
            }

            var language = _registryOptions.GetLanguageByExtension(ext);
            if (language != null)
            {
                _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(language.Id));
            }
            else
            {
                _textMateInstallation.SetGrammar(null);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SourcesView] Failed to update TextMate grammar for '{fileName}': {ex.Message}");
        }
    }
}
