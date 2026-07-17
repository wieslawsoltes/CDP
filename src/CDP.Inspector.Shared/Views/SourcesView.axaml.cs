using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using CDP.Xaml.LanguageServer;
using CDP.CSharp.LanguageServer;
using CDP.Markdown.Editor;
using CDP.Document.Editor;
using Avalonia.Controls.Primitives;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class SourcesView : UserControl
{
    public DataGrid TreeWorkspaceFiles => treeWorkspaceFiles;
    public TextBlock LblSourceFileName => lblSourceFileName;
    public TextEditor TxtSourceContent => txtSourceContent;
    public ToggleButton? BtnToggleMarkdownMode => this.FindControl<ToggleButton>("btnToggleMarkdownMode");
    public ToggleButton? BtnToggleDocumentMode => this.FindControl<ToggleButton>("btnToggleDocumentMode");
    public MarkdownEditor? MdVisualEditor => this.FindControl<MarkdownEditor>("mdVisualEditor");
    public DocumentEditor? DocVisualEditor => this.FindControl<DocumentEditor>("docVisualEditor");
    public TreeView TreeOutline => treeOutline;
    public Button BtnThemeSelector => btnThemeSelector;

    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;
    private int? _pendingScrollLine;
    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();
    private readonly XamlLanguageServer _xamlLsp = new();
    private readonly CSharpLanguageServer _csharpLsp = new();
    private readonly LspDiagnosticColorizer _diagnosticColorizer = new();
    private CompletionWindow? _completionWindow;

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

            editor.TextArea.TextEntered += TextArea_TextEntered;
            editor.TextArea.KeyDown += TextArea_KeyDown;
            editor.PointerMoved += TxtSourceContent_PointerMoved;
            editor.TextArea.TextView.LineTransformers.Add(_diagnosticColorizer);
            editor.TextChanged += (s, e) => UpdateDiagnostics();
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

        var toggleMd = BtnToggleMarkdownMode;
        if (toggleMd != null)
        {
            // Toggling is handled via data binding on Sources.IsMarkdownPreviewMode
        }

        var mdVisual = MdVisualEditor;
        if (mdVisual != null)
        {
            mdVisual.PropertyChanged += (s, e) =>
            {
                if (e.Property == MarkdownEditor.TextProperty)
                {
                    var toggle = BtnToggleMarkdownMode;
                    var editor = txtSourceContent;
                    var text = mdVisual.Text;
                    if (toggle != null && toggle.IsChecked == true && editor != null && text != null)
                    {
                        if (DataContext is MainWindowViewModel vm && vm.Sources.IsLoadingContent)
                        {
                            return;
                        }
                        if (editor.Text != text)
                        {
                            editor.Text = text;
                        }
                        SaveCurrentFile();
                    }
                }
            };
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
                    UpdateDiagnostics();
                    var editor = txtSourceContent;
                    var mdVisual = MdVisualEditor;
                    var docVisual = DocVisualEditor;
                    if (editor != null && mdVisual != null)
                    {
                        editor.IsVisible = !vm.Sources.IsMarkdownPreviewMode && !vm.Sources.IsDocumentPreviewMode;
                        mdVisual.IsVisible = vm.Sources.IsMarkdownPreviewMode;
                    }
                    if (docVisual != null)
                    {
                        docVisual.IsVisible = vm.Sources.IsDocumentPreviewMode;
                    }
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
                    UpdateDiagnostics();
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
                else if (e.PropertyName == nameof(SourcesViewModel.IsMarkdownPreviewMode))
                {
                    var editor = txtSourceContent;
                    var mdVisual = MdVisualEditor;
                    var docVisual = DocVisualEditor;
                    if (editor != null && mdVisual != null)
                    {
                        editor.IsVisible = !vm.Sources.IsMarkdownPreviewMode && !vm.Sources.IsDocumentPreviewMode;
                        mdVisual.IsVisible = vm.Sources.IsMarkdownPreviewMode;
                    }
                    if (docVisual != null)
                    {
                        docVisual.IsVisible = vm.Sources.IsDocumentPreviewMode;
                    }
                }
                else if (e.PropertyName == nameof(SourcesViewModel.IsDocumentPreviewMode))
                {
                    var editor = txtSourceContent;
                    var mdVisual = MdVisualEditor;
                    var docVisual = DocVisualEditor;
                    if (editor != null)
                    {
                        editor.IsVisible = vm.Sources.IsSourceEditorVisible;
                    }
                    if (mdVisual != null)
                    {
                        mdVisual.IsVisible = vm.Sources.IsMarkdownPreviewMode;
                    }
                    if (docVisual != null)
                    {
                        docVisual.IsVisible = vm.Sources.IsDocumentPreviewMode;
                    }
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

                // Swapping visibility and auto-loading text properties are handled dynamically by MVVM bindings.
                // txtSourceContent is updated procedurally here for syntax highlighting / diagnostics context.
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
            if (vm.Sources.IsDocumentFile)
            {
                return;
            }

            string editorText;
            var mdVisual = MdVisualEditor;
            var toggle = BtnToggleMarkdownMode;
            if (toggle != null && toggle.IsChecked == true && mdVisual != null && mdVisual.IsVisible)
            {
                mdVisual.Flush();
                editorText = mdVisual.Text;
            }
            else
            {
                editorText = txtSourceContent.Text;
            }

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

    private void TextArea_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowCompletion(explicitInvocation: true);
            e.Handled = true;
        }
    }

    private void TextArea_TextEntered(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        char trigger = e.Text[^1];

        if (_completionWindow != null) return;

        if (trigger == '<' || trigger == '.' || trigger == ' ')
        {
            ShowCompletion(explicitInvocation: false);
        }
    }

    private void ShowCompletion(bool explicitInvocation)
    {
        var editor = txtSourceContent;
        if (editor == null || editor.Document == null) return;

        var vm = DataContext as MainWindowViewModel;
        if (vm == null) return;

        var fileName = vm.Sources.SelectedFileName;
        if (string.IsNullOrEmpty(fileName)) return;

        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext != ".xaml" && ext != ".axaml" && ext != ".cs") return;

        string text = editor.Text ?? "";
        int caretOffset = editor.CaretOffset;

        var loc = editor.Document.GetLocation(caretOffset);
        int line = loc.Line;
        int col = loc.Column;

        List<string> suggestions = new();

        if (ext == ".xaml" || ext == ".axaml")
        {
            _xamlLsp.OpenDocument(fileName, text);
            var comps = _xamlLsp.GetCompletions(fileName, line, col);
            suggestions.AddRange(comps.Select(c => c.Label));
        }
        else if (ext == ".cs")
        {
            _csharpLsp.OpenDocument(fileName, text);
            var comps = _csharpLsp.GetCompletions(fileName, line, col);
            suggestions.AddRange(comps.Select(c => c.Label));
        }

        if (suggestions.Count == 0)
        {
            CloseCompletionWindow();
            return;
        }

        CloseCompletionWindow();

        var completionWindow = new CompletionWindow(editor.TextArea)
        {
            CloseAutomatically = true,
            CloseWhenCaretAtBeginning = false
        };

        var wordBoundary = GetWordBoundary(text, caretOffset);
        completionWindow.StartOffset = wordBoundary.start;
        completionWindow.EndOffset = wordBoundary.end;

        completionWindow.CompletionList.IsFiltering = true;
        foreach (var suggestion in suggestions)
        {
            completionWindow.CompletionList.CompletionData.Add(new LspCompletionData(suggestion));
        }

        completionWindow.Closed += (s, e) => _completionWindow = null;
        _completionWindow = completionWindow;
        completionWindow.Show();
    }

    private void CloseCompletionWindow()
    {
        if (_completionWindow != null)
        {
            _completionWindow.Close();
            _completionWindow = null;
        }
    }

    private (int start, int end) GetWordBoundary(string text, int offset)
    {
        int start = offset;
        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }
        int end = offset;
        while (end < text.Length && IsWordChar(text[end]))
        {
            end++;
        }
        return (start, end);
    }

    private bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    private void TxtSourceContent_PointerMoved(object? sender, PointerEventArgs e)
    {
        var editor = txtSourceContent;
        if (editor == null || editor.Document == null) return;

        var pos = e.GetPosition(editor.TextArea.TextView);
        var position = editor.TextArea.TextView.GetPosition(pos + editor.TextArea.TextView.ScrollOffset);
        if (position.HasValue)
        {
            int offset = editor.Document.GetOffset(position.Value.Location);
            var loc = editor.Document.GetLocation(offset);
            
            var vm = DataContext as MainWindowViewModel;
            if (vm != null && !string.IsNullOrEmpty(vm.Sources.SelectedFileName))
            {
                var fileName = vm.Sources.SelectedFileName;
                string ext = Path.GetExtension(fileName).ToLowerInvariant();
                
                string? contents = null;
                if (ext == ".xaml" || ext == ".axaml")
                {
                    _xamlLsp.OpenDocument(fileName, editor.Text);
                    var hover = _xamlLsp.GetHover(fileName, loc.Line, loc.Column);
                    contents = hover?.Contents;
                }
                else if (ext == ".cs")
                {
                    _csharpLsp.OpenDocument(fileName, editor.Text);
                    var hover = _csharpLsp.GetHover(fileName, loc.Line, loc.Column);
                    contents = hover?.Contents;
                }

                if (!string.IsNullOrEmpty(contents))
                {
                    ToolTip.SetTip(editor, contents);
                    ToolTip.SetIsOpen(editor, true);
                    return;
                }
            }
        }
        ToolTip.SetIsOpen(editor, false);
    }

    private void UpdateDiagnostics()
    {
        var editor = txtSourceContent;
        if (editor == null || editor.Document == null) return;

        var vm = DataContext as MainWindowViewModel;
        if (vm == null || string.IsNullOrEmpty(vm.Sources.SelectedFileName)) return;

        var fileName = vm.Sources.SelectedFileName;
        string ext = Path.GetExtension(fileName).ToLowerInvariant();

        List<LspDiagnosticColorizer.DiagnosticRange> diags = new();

        if (ext == ".xaml" || ext == ".axaml")
        {
            _xamlLsp.OpenDocument(fileName, editor.Text);
            var xamlDiags = _xamlLsp.GetDiagnostics(fileName);
            foreach (var d in xamlDiags)
            {
                diags.Add(new LspDiagnosticColorizer.DiagnosticRange(d.StartLine, d.StartColumn, d.EndLine, d.EndColumn));
            }
        }
        else if (ext == ".cs")
        {
            _csharpLsp.OpenDocument(fileName, editor.Text);
            var csDiags = _csharpLsp.GetDiagnostics(fileName);
            foreach (var d in csDiags)
            {
                diags.Add(new LspDiagnosticColorizer.DiagnosticRange(d.StartLine, d.StartColumn, d.EndLine, d.EndColumn));
            }
        }

        _diagnosticColorizer.Diagnostics = diags;
        editor.TextArea.TextView.Redraw();
    }
}

public class LspDiagnosticColorizer : DocumentColorizingTransformer
{
    public record DiagnosticRange(int StartLine, int StartColumn, int EndLine, int EndColumn);

    public List<DiagnosticRange> Diagnostics { get; set; } = new();

    protected override void ColorizeLine(DocumentLine line)
    {
        foreach (var diag in Diagnostics)
        {
            if (line.LineNumber >= diag.StartLine && line.LineNumber <= diag.EndLine)
            {
                int startOffset = line.Offset;
                int endOffset = line.EndOffset;

                if (line.LineNumber == diag.StartLine)
                {
                    startOffset = line.Offset + diag.StartColumn - 1;
                }
                if (line.LineNumber == diag.EndLine)
                {
                    endOffset = line.Offset + diag.EndColumn - 1;
                }

                if (startOffset < line.Offset) startOffset = line.Offset;
                if (endOffset > line.EndOffset) endOffset = line.EndOffset;

                if (startOffset < endOffset)
                {
                    ChangeLinePart(
                        startOffset,
                        endOffset,
                        visualLine =>
                        {
                            visualLine.BackgroundBrush = Brushes.DarkRed;
                            visualLine.TextRunProperties.SetForegroundBrush(Brushes.White);
                        });
                }
            }
        }
    }
}
