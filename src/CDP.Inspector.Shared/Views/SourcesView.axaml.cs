using System;
using System.IO;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class SourcesView : UserControl
{
    public DataGrid TreeWorkspaceFiles => treeWorkspaceFiles;
    public TextBlock LblSourceFileName => lblSourceFileName;
    public TextEditor TxtSourceContent => txtSourceContent;

    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;
    private int? _pendingScrollLine;
    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();

    private Control GetOrCreateViewInstance(string viewName, CDP.Editor.Splits.Controls.SuperSplitBox? targetBox = null)
    {
        string cacheKey = viewName;
        if (viewName == "SourcesFiles") cacheKey = "pnlSourcesFiles";
        else if (viewName == "SourcesSearch") cacheKey = "pnlSourcesSearch";
        else if (viewName == "CodeViewer") cacheKey = "pnlCodeViewer";
        else if (viewName == "Debugger") cacheKey = "pnlDebugger";

        if (_viewsCache.TryGetValue(cacheKey, out var cached))
        {
            if (targetBox == null || cached.Parent != targetBox)
            {
                DetachControl(cached);
            }
            return cached;
        }
        return new TextBlock { Text = $"View {viewName} not found", Margin = new Thickness(10) };
    }

    private void DetachControl(Control control)
    {
        if (control.Parent is CDP.Editor.Splits.Controls.SuperSplitBox splitBox)
        {
            splitBox.InnerContent = null;
            splitBox.UpdateLayout();
        }
        else if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }

        var visualParent = control.GetVisualParent();
        if (visualParent is ContentPresenter presenter)
        {
            presenter.Content = null;
        }
        else if (visualParent is Panel visualPanel)
        {
            visualPanel.Children.Remove(control);
        }
    }

    public SourcesView()
    {
        InitializeComponent();

        // Cache control references in local variables explicitly before detaching
        var wFiles = treeWorkspaceFiles;
        var sContent = txtSourceContent;
        var sFileName = lblSourceFileName;

        // Initialize view cache
        var hiddenPanel = this.FindControl<Grid>("HiddenPanel");
        if (hiddenPanel != null)
        {
            var children = System.Linq.Enumerable.ToList(hiddenPanel.Children);
            foreach (var child in children)
            {
                if (child is Control ctrl && !string.IsNullOrEmpty(ctrl.Name))
                {
                    hiddenPanel.Children.Remove(ctrl);
                    _viewsCache[ctrl.Name] = ctrl;
                }
            }
        }

        SplitControl.ViewResolver = (viewName, targetBox) => GetOrCreateViewInstance(viewName, targetBox);
        
        var editor = txtSourceContent;
        if (editor != null)
        {
            if (!OperatingSystem.IsBrowser())
            {
                try
                {
                    _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                    _textMateInstallation = editor.InstallTextMate(_registryOptions);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SourcesView] Failed to initialize TextMate: {ex.Message}");
                }
            }
        }

        var btnSave = this.FindControl<Button>("btnSaveFile");
        if (btnSave != null)
        {
            btnSave.Click += (sender, args) => SaveCurrentFile();
        }

        var btnToggleBp = this.FindControl<Button>("btnToggleBreakpoint");
        if (btnToggleBp != null)
        {
            btnToggleBp.Click += (sender, args) => ToggleBreakpointAtCaret();
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
                    if (vm.Sources.PendingScrollLine.HasValue && 
                        vm.Sources.SelectedFileContent != "Loading content..." && 
                        !string.IsNullOrEmpty(vm.Sources.SelectedFileContent))
                    {
                        ScrollToAndSelectLine(vm.Sources.PendingScrollLine.Value);
                        vm.Sources.PendingScrollLine = null;
                    }
                    else if (vm.Sources.ActiveDebugLine.HasValue && 
                        vm.Sources.SelectedFileContent != "Loading content..." && 
                        !string.IsNullOrEmpty(vm.Sources.SelectedFileContent))
                    {
                        ScrollToAndSelectLine(vm.Sources.ActiveDebugLine.Value);
                    }
                    else if (_pendingScrollLine.HasValue && 
                        vm.Sources.SelectedFileContent != "Loading content..." && 
                        !string.IsNullOrEmpty(vm.Sources.SelectedFileContent))
                    {
                        ScrollToAndSelectLine(_pendingScrollLine.Value);
                    }
                }
                else if (e.PropertyName == nameof(SourcesViewModel.SelectedFileName))
                {
                    UpdateHighlighting(vm.Sources.SelectedFileName);
                }
                else if (e.PropertyName == nameof(SourcesViewModel.PendingScrollLine))
                {
                    if (vm.Sources.PendingScrollLine.HasValue && 
                        vm.Sources.SelectedFileContent != "Loading content..." && 
                        !string.IsNullOrEmpty(vm.Sources.SelectedFileContent))
                    {
                        ScrollToAndSelectLine(vm.Sources.PendingScrollLine.Value);
                        vm.Sources.PendingScrollLine = null;
                    }
                }
                else if (e.PropertyName == nameof(SourcesViewModel.ActiveDebugLine))
                {
                    if (vm.Sources.ActiveDebugLine.HasValue && 
                        vm.Sources.SelectedFileContent != "Loading content..." && 
                        !string.IsNullOrEmpty(vm.Sources.SelectedFileContent))
                    {
                        ScrollToAndSelectLine(vm.Sources.ActiveDebugLine.Value);
                    }
                }
            }
        });
    }

    private void ToggleBreakpointAtCaret()
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var editor = txtSourceContent;
            if (editor != null && editor.Document != null)
            {
                int currentLine = editor.TextArea.Caret.Line;
                if (vm.Sources.ToggleBreakpointCommand.CanExecute(currentLine))
                {
                    vm.Sources.ToggleBreakpointCommand.Execute(currentLine);
                }
            }
        }
    }

    private void OnSearchResultDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is DataGrid dg && dg.SelectedItem is SearchResultModel match)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                var node = vm.Sources.FindFileByPath(match.Path);
                if (node != null)
                {
                    _pendingScrollLine = match.LineNumber;
                    if (vm.Sources.SelectedFile == node)
                    {
                        if (vm.Sources.SelectedFileContent != "Loading content..." && 
                            !string.IsNullOrEmpty(vm.Sources.SelectedFileContent))
                        {
                            ScrollToAndSelectLine(match.LineNumber);
                        }
                    }
                    else
                    {
                        vm.Sources.SelectedFile = node;
                    }
                }
            }
        }
    }

    private void ScrollToAndSelectLine(int lineNumber)
    {
        if (lineNumber <= 0) return;
        var editor = txtSourceContent;
        if (editor != null && editor.Document != null)
        {
            if (lineNumber <= editor.Document.LineCount)
            {
                try
                {
                    editor.ScrollToLine(lineNumber);
                    var line = editor.Document.GetLineByNumber(lineNumber);
                    editor.Select(line.Offset, line.Length);
                    _pendingScrollLine = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SourcesView] ScrollToLine failed: {ex.Message}");
                }
            }
        }
    }

    private void UpdateEditorText(string? text)
    {
        var editor = txtSourceContent;
        if (editor != null)
        {
            if (editor.Text != text)
            {
                editor.Text = text ?? "";
            }

            if (DataContext is MainWindowViewModel vm)
            {
                var isFileLoaded = vm.Sources.SelectedFile != null && !vm.Sources.SelectedFile.IsDirectory;
                editor.IsReadOnly = !isFileLoaded;
            }
            else
            {
                editor.IsReadOnly = true;
            }
        }
    }

    private void SaveCurrentFile()
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var editorText = txtSourceContent.Text;
            if (vm.Sources.SaveFileCommand.CanExecute(editorText))
            {
                vm.Sources.SaveFileCommand.Execute(editorText);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            SaveCurrentFile();
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
