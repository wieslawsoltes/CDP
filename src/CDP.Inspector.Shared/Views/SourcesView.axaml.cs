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
using CDP.JavaScript.LanguageServer;
using CDP.Markdown.Editor;
using CDP.Document.Editor;
using Avalonia.Controls.Primitives;
using XamlPlayground.Editor.Minimap.Inline;

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
    public Border SourcePalette => pnlSourcePalette;
    public TextBox SourcePaletteQuery => txtSourcePaletteQuery;
    public ListBox SourcePaletteResults => lstSourcePalette;

    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;
    private int? _pendingScrollLine;
    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();
    private readonly XamlLanguageServer _xamlLsp = new();
    private readonly CSharpLanguageServer _csharpLsp = new();
    private readonly JavaScriptLanguageService _javaScriptLanguageService = new();
    private readonly LspDiagnosticColorizer _diagnosticColorizer = new();
    private DebuggerLineColorizer? _debuggerLineColorizer;
    private CompletionWindow? _completionWindow;
    private System.Threading.CancellationTokenSource? _debuggerHoverCancellation;
    private System.Threading.CancellationTokenSource? _languageHoverCancellation;
    private System.Threading.CancellationTokenSource? _languageCompletionCancellation;
    private System.Threading.CancellationTokenSource? _languageDiagnosticsCancellation;
    private System.Threading.CancellationTokenSource? _languageNavigationCancellation;
    private System.Threading.CancellationTokenSource? _languageSignatureCancellation;
    private readonly Dictionary<string, JavaScriptProjectEntry> _javaScriptProject = new(StringComparer.Ordinal);
    private IReadOnlyList<SourcesPaletteItem> _sourcePaletteItems = Array.Empty<SourcesPaletteItem>();
    private int _sourcePaletteRequestVersion;
    private JavaScriptRenameResult? _pendingJavaScriptRename;
    private string _lastDebuggerHoverKey = "";

    private Control GetOrCreateViewInstance(string viewName, CDP.Editor.Splits.Controls.SuperSplitBox? targetBox = null)
    {
        string cacheKey = viewName;
        if (viewName == "SourcesFiles") cacheKey = "pnlSourcesFiles";
        else if (viewName == "SourcesRuntimeScripts") cacheKey = "pnlSourcesRuntimeScripts";
        else if (viewName == "SourcesSearch") cacheKey = "pnlSourcesSearch";
        else if (viewName == "CodeViewer") cacheKey = "pnlCodeViewer";
        else if (viewName == "Debugger") cacheKey = "pnlDebugger";
        else if (viewName == "DebuggerWatch") cacheKey = "pnlDebuggerWatch";
        else if (viewName == "DebuggerCallStack") cacheKey = "pnlDebuggerCallStack";
        else if (viewName == "DebuggerVariables") cacheKey = "pnlDebuggerVariables";
        else if (viewName == "DebuggerBreakpoints") cacheKey = "pnlDebuggerBreakpoints";
        else if (viewName == "DebuggerIgnoreList") cacheKey = "pnlDebuggerIgnoreList";

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
        AddHandler(KeyDownEvent, SourcePaletteShortcut_KeyDown, RoutingStrategies.Tunnel);

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
        DebuggerSplitControl.ViewResolver = (viewName, targetBox) => GetOrCreateViewInstance(viewName, targetBox);
        
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
            _debuggerLineColorizer = new DebuggerLineColorizer(() =>
                (DataContext as MainWindowViewModel)?.Sources.ActiveDebugLine);
            editor.TextArea.TextView.LineTransformers.Add(_debuggerLineColorizer);
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

        var btnRunToCursor = this.FindControl<Button>("btnDebuggerRunToCursor");
        if (btnRunToCursor != null)
        {
            btnRunToCursor.Click += (sender, args) => RunToCursorAtCaret();
        }

        btnSourceSymbols.Click += (_, _) => _ = ShowJavaScriptSymbolsAsync();
        btnSourceReferences.Click += (_, _) => _ = ShowJavaScriptReferencesAsync();
        btnFormatSource.Click += (_, _) => _ = FormatJavaScriptDocumentAsync();
        btnSourceQuickOpen.Click += (_, _) => ShowSourceQuickOpen();
        txtSourcePaletteQuery.TextChanged += (_, _) => FilterSourcePalette();
        txtSourcePaletteQuery.KeyDown += SourcePaletteQuery_KeyDown;
        lstSourcePalette.DoubleTapped += (_, _) => _ = ExecuteSelectedSourcePaletteItemAsync();
        btnApplySourceRename.Click += (_, _) => _ = ApplyPreparedJavaScriptRenameAsync();
        btnCancelSourceRename.Click += (_, _) => CloseJavaScriptRename();
        txtRenameSource.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                _ = ApplyPreparedJavaScriptRenameAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CloseJavaScriptRename();
                e.Handled = true;
            }
        };
        treeOutline.SelectionChanged += (_, _) =>
        {
            if (treeOutline.SelectedItem is JavaScriptOutlineItem item)
            {
                NavigateToJavaScriptItem(item);
            }
        };

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
                    if (!editor.TextArea.LeftMargins.Any(margin => margin is ReplayGutterMargin))
                    {
                        var gutter = new ReplayGutterMargin(new SourcesDebuggerGutterDataProvider(vm.Sources));
                        var insertIndex = 0;
                        for (var i = 0; i < editor.TextArea.LeftMargins.Count; i++)
                        {
                            if (editor.TextArea.LeftMargins[i].GetType().Name.Contains("LineNumberMargin"))
                            {
                                insertIndex = i + 1;
                                break;
                            }
                        }
                        editor.TextArea.LeftMargins.Insert(insertIndex, gutter);
                    }
                    UpdateEditorText(vm.Sources.SelectedFileContent);
                    UpdateHighlighting(vm.Sources.SelectedFileName);
                    UpdateDiagnostics();
                    var currentEditor = txtSourceContent;
                    var mdVisual = MdVisualEditor;
                    var docVisual = DocVisualEditor;
                    if (currentEditor != null && mdVisual != null)
                    {
                        currentEditor.IsVisible = !vm.Sources.IsMarkdownPreviewMode && !vm.Sources.IsDocumentPreviewMode;
                        mdVisual.IsVisible = vm.Sources.IsMarkdownPreviewMode;
                    }
                    if (docVisual != null)
                    {
                        docVisual.IsVisible = vm.Sources.IsDocumentPreviewMode;
                    }
                }
            });
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _debuggerHoverCancellation?.Cancel();
            _languageHoverCancellation?.Cancel();
            _languageCompletionCancellation?.Cancel();
            _languageDiagnosticsCancellation?.Cancel();
            _languageNavigationCancellation?.Cancel();
            _languageSignatureCancellation?.Cancel();
            CloseCompletionWindow();
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
                    txtSourceContent.TextArea.TextView.Redraw();
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

    private void RunToCursorAtCaret()
    {
        if (DataContext is not MainWindowViewModel vm || txtSourceContent.Document is null) return;
        var currentLine = txtSourceContent.TextArea.Caret.Line;
        if (vm.Sources.RunToCursorCommand.CanExecute(currentLine))
        {
            vm.Sources.RunToCursorCommand.Execute(currentLine);
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
                else
                {
                    var script = vm.Sources.RuntimeScripts.FirstOrDefault(item =>
                        string.Equals(item.Url, match.Path, StringComparison.Ordinal) ||
                        string.Equals(item.DisplayName, match.Path, StringComparison.Ordinal));
                    if (script is not null)
                    {
                        _pendingScrollLine = match.LineNumber;
                        vm.Sources.SelectedRuntimeScript = script;
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
                editor.IsReadOnly = !vm.Sources.CanEditCurrentSource;

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

            if (vm.Sources.ApplySourceChangesCommand.CanExecute(editorText))
            {
                vm.Sources.ApplySourceChangesCommand.Execute(editorText);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (TryHandleSourcePaletteShortcut(e))
        {
            return;
        }
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            SaveCurrentFile();
        }
    }

    private void SourcePaletteShortcut_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled) TryHandleSourcePaletteShortcut(e);
    }

    private bool TryHandleSourcePaletteShortcut(KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return false;

        if (e.Key == Key.P && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            ShowSourceCommandPalette();
        }
        else if (e.Key == Key.P)
        {
            ShowSourceQuickOpen();
        }
        else if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _ = ShowCurrentSourceSymbolPaletteAsync();
        }
        else if (e.Key == Key.T && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _ = ShowWorkspaceSourceSymbolPaletteAsync();
        }
        else
        {
            return false;
        }

        e.Handled = true;
        return true;
    }

    private void ShowSourceQuickOpen()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var items = new List<SourcesPaletteItem>();
        var paths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in EnumerateWorkspaceFileNodes(vm.Sources.WorkspaceFiles)
                     .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
        {
            var path = file.Path;
            if (!paths.Add(path)) continue;
            items.Add(new SourcesPaletteItem(
                file.Name,
                path,
                GetPaletteFileKind(path),
                "",
                () =>
                {
                    vm.Sources.SelectedFile = file;
                    return System.Threading.Tasks.Task.CompletedTask;
                }));
        }

        foreach (var script in vm.Sources.RuntimeScripts
                     .OrderBy(script => script.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var path = script.Url;
            if (!paths.Add(path)) continue;
            items.Add(new SourcesPaletteItem(
                script.DisplayName,
                string.IsNullOrWhiteSpace(path) ? script.DetailDisplay : path,
                script.LanguageBadge,
                "runtime",
                () =>
                {
                    vm.Sources.SelectedRuntimeScript = script;
                    return System.Threading.Tasks.Task.CompletedTask;
                }));
        }

        ShowSourcePalette("Go to File", "Search files by name or path", items);
    }

    private void ShowSourceCommandPalette()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var sources = vm.Sources;
        var items = new[]
        {
            new SourcesPaletteItem("Go to File", "Open a workspace or runtime source", "FILE", "Ctrl+P",
                () => { ShowSourceQuickOpen(); return System.Threading.Tasks.Task.CompletedTask; }),
            new SourcesPaletteItem("Go to Symbol in Editor", "Search symbols in the current JavaScript or TypeScript file", "SYMBOL", "Ctrl+Shift+O",
                async () => await ShowCurrentSourceSymbolPaletteAsync()),
            new SourcesPaletteItem("Go to Symbol in Workspace", "Search symbols across the loaded JavaScript or TypeScript project", "SYMBOL", "Ctrl+T",
                async () => await ShowWorkspaceSourceSymbolPaletteAsync()),
            new SourcesPaletteItem("Go to Definition", "Navigate to the definition at the caret", "EDIT", "F12",
                GoToJavaScriptDefinitionAsync),
            new SourcesPaletteItem("Find All References", "Show references for the symbol at the caret", "EDIT", "Shift+F12",
                ShowJavaScriptReferencesAsync),
            new SourcesPaletteItem("Rename Symbol", "Rename across the loaded project", "EDIT", "F2",
                PrepareJavaScriptRenameAsync),
            new SourcesPaletteItem("Format Document", "Apply TypeScript formatting edits", "EDIT", "Shift+Alt+F",
                FormatJavaScriptDocumentAsync),
            new SourcesPaletteItem("Save Source", "Save workspace changes or apply a V8 live edit", "FILE", "Ctrl+S",
                () => { SaveCurrentFile(); return System.Threading.Tasks.Task.CompletedTask; }),
            new SourcesPaletteItem("Toggle Breakpoint", "Add or remove a breakpoint at the caret", "DEBUG", "F9",
                () => { ToggleBreakpointAtCaret(); return System.Threading.Tasks.Task.CompletedTask; }),
            new SourcesPaletteItem("Continue", "Resume the paused debugger", "DEBUG", "F5",
                () => { if (sources.ResumeCommand.CanExecute(null)) sources.ResumeCommand.Execute(null); return System.Threading.Tasks.Task.CompletedTask; }),
            new SourcesPaletteItem("Pause", "Pause JavaScript execution", "DEBUG", "F6",
                () => { if (sources.PauseCommand.CanExecute(null)) sources.PauseCommand.Execute(null); return System.Threading.Tasks.Task.CompletedTask; }),
            new SourcesPaletteItem("Step Over", "Advance over the current statement", "DEBUG", "F10",
                () => { if (sources.StepOverCommand.CanExecute(null)) sources.StepOverCommand.Execute(null); return System.Threading.Tasks.Task.CompletedTask; }),
            new SourcesPaletteItem("Step Into", "Enter the current function call", "DEBUG", "F11",
                () => { if (sources.StepIntoCommand.CanExecute(null)) sources.StepIntoCommand.Execute(null); return System.Threading.Tasks.Task.CompletedTask; }),
            new SourcesPaletteItem("Step Out", "Leave the current function", "DEBUG", "Shift+F11",
                () => { if (sources.StepOutCommand.CanExecute(null)) sources.StepOutCommand.Execute(null); return System.Threading.Tasks.Task.CompletedTask; }),
            new SourcesPaletteItem("Run to Cursor", "Continue to the source line at the caret", "DEBUG", "Ctrl+F10",
                () => { RunToCursorAtCaret(); return System.Threading.Tasks.Task.CompletedTask; })
        };
        ShowSourcePalette("Command Palette", "Type a command", items);
    }

    private async System.Threading.Tasks.Task ShowCurrentSourceSymbolPaletteAsync()
    {
        ShowSourcePalette("Go to Symbol in Editor", "Loading document symbols…", []);
        var requestVersion = _sourcePaletteRequestVersion;
        var context = await PrepareJavaScriptEditorContextAsync();
        if (context is null)
        {
            if (IsCurrentSourcePaletteRequest(requestVersion))
                SetEmptySourcePalette("No JavaScript or TypeScript document is selected");
            return;
        }

        try
        {
            var root = await _javaScriptLanguageService.GetDocumentSymbolsAsync(
                context.FileName, context.CancellationToken);
            var items = root is null
                ? []
                : CreateSourceSymbolPaletteItems(context.FileName, root.Children).ToArray();
            if (IsCurrentSourcePaletteRequest(requestVersion))
                ShowSourcePalette("Go to Symbol in Editor", "Search symbols in the current file", items, true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (IsCurrentSourcePaletteRequest(requestVersion))
                SetEmptySourcePalette($"Symbols unavailable: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task ShowWorkspaceSourceSymbolPaletteAsync()
    {
        ShowSourcePalette("Go to Symbol in Workspace", "Loading project symbols…", []);
        var requestVersion = _sourcePaletteRequestVersion;
        var context = await PrepareJavaScriptEditorContextAsync();
        if (context is null)
        {
            if (IsCurrentSourcePaletteRequest(requestVersion))
                SetEmptySourcePalette("Open a JavaScript or TypeScript document to load its project");
            return;
        }

        try
        {
            var items = new List<SourcesPaletteItem>();
            foreach (var entry in _javaScriptProject.Values
                         .OrderBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase))
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (!IsJavaScriptExtension(GetLanguageExtension(entry.FileName))) continue;
                var root = await _javaScriptLanguageService.GetDocumentSymbolsAsync(
                    entry.FileName, context.CancellationToken);
                if (root is not null)
                {
                    items.AddRange(CreateSourceSymbolPaletteItems(entry.FileName, root.Children));
                }
            }
            if (IsCurrentSourcePaletteRequest(requestVersion))
                ShowSourcePalette("Go to Symbol in Workspace", "Search project symbols", items, true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (IsCurrentSourcePaletteRequest(requestVersion))
                SetEmptySourcePalette($"Workspace symbols unavailable: {ex.Message}");
        }
    }

    private IEnumerable<SourcesPaletteItem> CreateSourceSymbolPaletteItems(
        string fileName,
        IEnumerable<JavaScriptNavigationSymbol> symbols)
    {
        foreach (var symbol in symbols)
        {
            var navigation = CreateSymbolItem(fileName, symbol);
            yield return new SourcesPaletteItem(
                symbol.Text,
                $"{Path.GetFileName(fileName)} · {symbol.Kind}",
                "SYMBOL",
                "",
                () =>
                {
                    NavigateToJavaScriptItem(navigation);
                    return System.Threading.Tasks.Task.CompletedTask;
                });
            foreach (var child in CreateSourceSymbolPaletteItems(fileName, symbol.Children))
            {
                yield return child;
            }
        }
    }

    private void ShowSourcePalette(
        string title,
        string placeholder,
        IReadOnlyList<SourcesPaletteItem> items,
        bool preserveRequestVersion = false)
    {
        if (!preserveRequestVersion) _sourcePaletteRequestVersion++;
        _sourcePaletteItems = items;
        txtSourcePaletteTitle.Text = title;
        txtSourcePaletteQuery.PlaceholderText = placeholder;
        if (!preserveRequestVersion) txtSourcePaletteQuery.Text = "";
        pnlSourcePalette.IsVisible = true;
        FilterSourcePalette();
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            txtSourcePaletteQuery.Focus();
            txtSourcePaletteQuery.SelectAll();
        });
    }

    private bool IsCurrentSourcePaletteRequest(int requestVersion) =>
        pnlSourcePalette.IsVisible && requestVersion == _sourcePaletteRequestVersion;

    private void SetEmptySourcePalette(string message)
    {
        _sourcePaletteItems = Array.Empty<SourcesPaletteItem>();
        lstSourcePalette.ItemsSource = _sourcePaletteItems;
        lstSourcePalette.SelectedItem = null;
        txtSourcePaletteCount.Text = "0 results";
        txtSourcePaletteQuery.PlaceholderText = message;
    }

    private void FilterSourcePalette()
    {
        var query = txtSourcePaletteQuery.Text?.Trim() ?? "";
        var filtered = _sourcePaletteItems
            .Select((item, index) => (item, index, score: GetSourcePaletteMatchScore(item, query)))
            .Where(value => value.score >= 0)
            .OrderBy(value => value.score)
            .ThenBy(value => value.index)
            .Select(value => value.item)
            .ToArray();
        lstSourcePalette.ItemsSource = filtered;
        lstSourcePalette.SelectedIndex = filtered.Length == 0 ? -1 : 0;
        txtSourcePaletteCount.Text = filtered.Length == 1 ? "1 result" : $"{filtered.Length} results";
    }

    private static int GetSourcePaletteMatchScore(SourcesPaletteItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return 0;
        if (item.Label.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 0;
        if (item.Label.Contains(query, StringComparison.OrdinalIgnoreCase)) return 1;
        if (item.Detail.Contains(query, StringComparison.OrdinalIgnoreCase)) return 2;
        var search = item.SearchText;
        var position = 0;
        foreach (var character in query)
        {
            position = search.IndexOf(character.ToString(), position, StringComparison.OrdinalIgnoreCase);
            if (position < 0) return -1;
            position++;
        }
        return 3;
    }

    private void SourcePaletteQuery_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseSourcePalette();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            _ = ExecuteSelectedSourcePaletteItemAsync();
            e.Handled = true;
        }
        else if (e.Key is Key.Down or Key.Up)
        {
            var count = lstSourcePalette.ItemCount;
            if (count > 0)
            {
                var delta = e.Key == Key.Down ? 1 : -1;
                lstSourcePalette.SelectedIndex = Math.Clamp(lstSourcePalette.SelectedIndex + delta, 0, count - 1);
                if (lstSourcePalette.SelectedItem is { } selected) lstSourcePalette.ScrollIntoView(selected);
            }
            e.Handled = true;
        }
    }

    private async System.Threading.Tasks.Task ExecuteSelectedSourcePaletteItemAsync()
    {
        if (lstSourcePalette.SelectedItem is not SourcesPaletteItem item) return;
        CloseSourcePalette();
        await item.InvokeAsync();
    }

    private void CloseSourcePalette()
    {
        _sourcePaletteRequestVersion++;
        pnlSourcePalette.IsVisible = false;
        _sourcePaletteItems = Array.Empty<SourcesPaletteItem>();
        lstSourcePalette.ItemsSource = null;
        txtSourcePaletteQuery.Text = "";
        txtSourceContent.Focus();
    }

    private static IEnumerable<WorkspaceFileNode> EnumerateWorkspaceFileNodes(
        IEnumerable<WorkspaceFileNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsDirectory)
            {
                yield return node;
            }
            else
            {
                foreach (var child in EnumerateWorkspaceFileNodes(node.Children)) yield return child;
            }
        }
    }

    private static string GetPaletteFileKind(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        return string.IsNullOrWhiteSpace(extension) ? "FILE" : extension.Length <= 6 ? extension : "FILE";
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
        if (TryHandleSourcePaletteShortcut(e))
        {
            return;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            if (e.Key == Key.F12)
            {
                _ = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
                    ? ShowJavaScriptReferencesAsync()
                    : GoToJavaScriptDefinitionAsync();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F2)
            {
                _ = PrepareJavaScriptRenameAsync();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                _ = ShowCurrentSourceSymbolPaletteAsync();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
                e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                _ = FormatJavaScriptDocumentAsync();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F9)
            {
                ToggleBreakpointAtCaret();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F5 && vm.Sources.ResumeCommand.CanExecute(null))
            {
                vm.Sources.ResumeCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F6 && vm.Sources.PauseCommand.CanExecute(null))
            {
                vm.Sources.PauseCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F10)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    RunToCursorAtCaret();
                }
                else if (vm.Sources.StepOverCommand.CanExecute(null))
                {
                    vm.Sources.StepOverCommand.Execute(null);
                }
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F11)
            {
                var command = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
                    ? vm.Sources.StepOutCommand
                    : vm.Sources.StepIntoCommand;
                if (command.CanExecute(null)) command.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                SaveCurrentFile();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.E && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                var expression = txtSourceContent.SelectedText;
                if (string.IsNullOrWhiteSpace(expression) && txtSourceContent.Document is not null)
                {
                    var boundaries = GetWordBoundary(txtSourceContent.Text, txtSourceContent.CaretOffset);
                    if (boundaries.end > boundaries.start)
                    {
                        expression = txtSourceContent.Text[boundaries.start..boundaries.end];
                    }
                }
                if (!string.IsNullOrWhiteSpace(expression))
                {
                    vm.Sources.DebuggerEvaluationExpression = expression.Trim();
                    if (vm.Sources.EvaluateOnCallFrameCommand.CanExecute(null))
                    {
                        vm.Sources.EvaluateOnCallFrameCommand.Execute(null);
                    }
                }
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _ = ShowCompletionAsync(explicitInvocation: true);
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
            _ = ShowCompletionAsync(explicitInvocation: false);
        }

        if (trigger is '(' or ',')
        {
            _ = ShowJavaScriptSignatureHelpAsync();
        }
    }

    private async System.Threading.Tasks.Task ShowCompletionAsync(bool explicitInvocation)
    {
        var editor = txtSourceContent;
        if (editor == null || editor.Document == null) return;

        var vm = DataContext as MainWindowViewModel;
        if (vm == null) return;

        var fileName = GetLanguageFileName(vm.Sources);
        if (string.IsNullOrEmpty(fileName)) return;

        string ext = GetLanguageExtension(fileName);
        if (ext != ".xaml" && ext != ".axaml" && ext != ".cs" && !IsJavaScriptExtension(ext)) return;

        string text = editor.Text ?? "";
        int caretOffset = editor.CaretOffset;
        if (!explicitInvocation && IsJavaScriptExtension(ext) &&
            (caretOffset == 0 || text[caretOffset - 1] is not ('.' or '<')))
        {
            return;
        }

        var loc = editor.Document.GetLocation(caretOffset);
        int line = loc.Line;
        int col = loc.Column;

        List<LspCompletionData> suggestions = new();

        if (ext == ".xaml" || ext == ".axaml")
        {
            _xamlLsp.OpenDocument(fileName, text);
            var comps = _xamlLsp.GetCompletions(fileName, line, col);
            suggestions.AddRange(comps.Select(c => new LspCompletionData(c.Label, "XAML")));
        }
        else if (ext == ".cs")
        {
            _csharpLsp.OpenDocument(fileName, text);
            var comps = _csharpLsp.GetCompletions(fileName, line, col);
            suggestions.AddRange(comps.Select(c => new LspCompletionData(c.Label, "C#")));
        }
        else
        {
            _languageCompletionCancellation?.Cancel();
            _languageCompletionCancellation?.Dispose();
            _languageCompletionCancellation = new System.Threading.CancellationTokenSource();
            var cancellationToken = _languageCompletionCancellation.Token;
            try
            {
                await _javaScriptLanguageService.OpenDocumentAsync(
                    fileName,
                    text,
                    GetProjectRoot(vm.Sources, fileName),
                    cancellationToken);
                var completions = await _javaScriptLanguageService.GetCompletionsAsync(
                    fileName,
                    line,
                    col,
                    cancellationToken);
                if (cancellationToken.IsCancellationRequested ||
                    editor.CaretOffset != caretOffset || editor.Text != text)
                {
                    return;
                }
                suggestions.AddRange(completions.Select(completion => new LspCompletionData(
                    string.IsNullOrWhiteSpace(completion.InsertText) ? completion.Name : completion.InsertText,
                    string.IsNullOrWhiteSpace(completion.Source)
                        ? $"{completion.Kind} · TypeScript {_javaScriptLanguageService.TypeScriptVersion}"
                        : $"{completion.Kind} · {completion.Source}")));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SourcesView] JavaScript completion failed: {ex.Message}");
                return;
            }
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
            completionWindow.CompletionList.CompletionData.Add(suggestion);
        }

        completionWindow.Closed += (s, e) => _completionWindow = null;
        _completionWindow = completionWindow;
        completionWindow.Show();
    }

    private async System.Threading.Tasks.Task<JavaScriptEditorContext?> PrepareJavaScriptEditorContextAsync()
    {
        if (DataContext is not MainWindowViewModel vm || txtSourceContent.Document is null) return null;
        var fileName = GetLanguageFileName(vm.Sources);
        if (string.IsNullOrWhiteSpace(fileName) || !IsJavaScriptExtension(GetLanguageExtension(fileName)))
        {
            SetLanguageStatus("JavaScript/TypeScript source required");
            return null;
        }

        _languageNavigationCancellation?.Cancel();
        _languageNavigationCancellation?.Dispose();
        _languageNavigationCancellation = new System.Threading.CancellationTokenSource();
        var cancellationToken = _languageNavigationCancellation.Token;

        try
        {
            var documents = await vm.Sources.LoadJavaScriptProjectDocumentsAsync(cancellationToken);
            _javaScriptProject.Clear();
            foreach (var document in documents)
            {
                var normalized = _javaScriptLanguageService.GetNormalizedFileName(document.FileName);
                _javaScriptProject[normalized] = new JavaScriptProjectEntry(
                    document.FileName,
                    document.Text,
                    vm.Sources.FindFileByPath(document.FileName) is not null);
            }

            var currentText = txtSourceContent.Text ?? "";
            var currentNormalized = _javaScriptLanguageService.GetNormalizedFileName(fileName);
            _javaScriptProject[currentNormalized] = new JavaScriptProjectEntry(
                fileName,
                currentText,
                vm.Sources.FindFileByPath(fileName) is not null);
            await _javaScriptLanguageService.OpenProjectAsync(
                _javaScriptProject.Values.Select(entry =>
                    new JavaScriptProjectDocument(entry.FileName, entry.Text)),
                GetProjectRoot(vm.Sources, fileName),
                cancellationToken);

            var location = txtSourceContent.Document.GetLocation(txtSourceContent.CaretOffset);
            return new JavaScriptEditorContext(vm.Sources, fileName, currentNormalized,
                location.Line, location.Column, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            SetLanguageStatus($"Language service unavailable: {ex.Message}");
            return null;
        }
    }

    private async System.Threading.Tasks.Task GoToJavaScriptDefinitionAsync()
    {
        var context = await PrepareJavaScriptEditorContextAsync();
        if (context is null) return;
        try
        {
            var definitions = await _javaScriptLanguageService.GetDefinitionsAsync(
                context.FileName, context.Line, context.Column, context.CancellationToken);
            var items = definitions.Select(span => CreateOutlineItem("Definition", span)).ToArray();
            treeOutline.ItemsSource = items;
            SetLanguageStatus(items.Length == 0 ? "No definition found" : $"{items.Length} definition(s)");
            if (items.FirstOrDefault() is { } first) NavigateToJavaScriptItem(first);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetLanguageStatus($"Definition failed: {ex.Message}"); }
    }

    private async System.Threading.Tasks.Task ShowJavaScriptReferencesAsync()
    {
        var context = await PrepareJavaScriptEditorContextAsync();
        if (context is null) return;
        try
        {
            var references = await _javaScriptLanguageService.GetReferencesAsync(
                context.FileName, context.Line, context.Column, context.CancellationToken);
            var items = references.Select(span => CreateOutlineItem(
                span.IsDefinition ? "Definition" : span.IsWriteAccess ? "Write" : "Reference", span)).ToArray();
            treeOutline.ItemsSource = items;
            SetLanguageStatus(items.Length == 0 ? "No references found" : $"{items.Length} reference(s)");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetLanguageStatus($"References failed: {ex.Message}"); }
    }

    private async System.Threading.Tasks.Task ShowJavaScriptSymbolsAsync()
    {
        var context = await PrepareJavaScriptEditorContextAsync();
        if (context is null) return;
        try
        {
            var root = await _javaScriptLanguageService.GetDocumentSymbolsAsync(
                context.FileName, context.CancellationToken);
            var items = root?.Children.Select(symbol => CreateSymbolItem(context.FileName, symbol)).ToArray()
                ?? Array.Empty<JavaScriptOutlineItem>();
            treeOutline.ItemsSource = items;
            SetLanguageStatus(items.Length == 0 ? "No document symbols" : $"{items.Length} top-level symbol(s)");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetLanguageStatus($"Symbols failed: {ex.Message}"); }
    }

    private async System.Threading.Tasks.Task ShowJavaScriptSignatureHelpAsync()
    {
        if (DataContext is not MainWindowViewModel vm || txtSourceContent.Document is null) return;
        var fileName = GetLanguageFileName(vm.Sources);
        if (string.IsNullOrWhiteSpace(fileName) || !IsJavaScriptExtension(GetLanguageExtension(fileName))) return;
        _languageSignatureCancellation?.Cancel();
        _languageSignatureCancellation?.Dispose();
        _languageSignatureCancellation = new System.Threading.CancellationTokenSource();
        var cancellationToken = _languageSignatureCancellation.Token;
        try
        {
            var location = txtSourceContent.Document.GetLocation(txtSourceContent.CaretOffset);
            await _javaScriptLanguageService.OpenDocumentAsync(
                fileName, txtSourceContent.Text ?? "", GetProjectRoot(vm.Sources, fileName), cancellationToken);
            var signature = await _javaScriptLanguageService.GetSignatureHelpAsync(
                fileName, location.Line, location.Column, cancellationToken);
            if (signature is null || signature.Items.Count == 0) return;
            var itemIndex = Math.Clamp(signature.SelectedItemIndex, 0, signature.Items.Count - 1);
            var item = signature.Items[itemIndex];
            SetLanguageStatus(item.Prefix + string.Join(item.Separator, item.Parameters.Select((parameter, index) =>
                index == signature.ArgumentIndex ? $"[{parameter.Display}]" : parameter.Display)) + item.Suffix);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetLanguageStatus($"Signature help failed: {ex.Message}"); }
    }

    private async System.Threading.Tasks.Task FormatJavaScriptDocumentAsync()
    {
        var context = await PrepareJavaScriptEditorContextAsync();
        if (context is null) return;
        try
        {
            var changes = await _javaScriptLanguageService.GetFormattingEditsAsync(
                context.FileName, cancellationToken: context.CancellationToken);
            if (changes.Count == 0)
            {
                SetLanguageStatus("Document is already formatted");
                return;
            }
            var caret = txtSourceContent.CaretOffset;
            txtSourceContent.Text = JavaScriptLanguageService.ApplyTextChanges(txtSourceContent.Text ?? "", changes);
            txtSourceContent.CaretOffset = Math.Min(caret, txtSourceContent.Text.Length);
            SetLanguageStatus($"Applied {changes.Count} formatting edit(s)");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetLanguageStatus($"Format failed: {ex.Message}"); }
    }

    private async System.Threading.Tasks.Task PrepareJavaScriptRenameAsync()
    {
        var context = await PrepareJavaScriptEditorContextAsync();
        if (context is null) return;
        try
        {
            var result = await _javaScriptLanguageService.GetRenameLocationsAsync(
                context.FileName, context.Line, context.Column, context.CancellationToken);
            if (!result.CanRename || result.Locations.Count == 0)
            {
                SetLanguageStatus(result.Error ?? "The selected symbol cannot be renamed");
                return;
            }
            _pendingJavaScriptRename = result;
            txtRenameLabel.Text = $"Rename {result.DisplayName ?? "symbol"} · {result.Locations.Count} occurrence(s)";
            txtRenameSource.Text = result.DisplayName ?? "";
            pnlSourceRename.IsVisible = true;
            txtRenameSource.Focus();
            txtRenameSource.SelectAll();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetLanguageStatus($"Rename failed: {ex.Message}"); }
    }

    private async System.Threading.Tasks.Task ApplyPreparedJavaScriptRenameAsync()
    {
        if (_pendingJavaScriptRename is not { CanRename: true } rename ||
            string.IsNullOrWhiteSpace(txtRenameSource.Text)) return;
        var newName = txtRenameSource.Text.Trim();
        if (!IsJavaScriptIdentifier(newName))
        {
            SetLanguageStatus($"Rename cancelled: '{newName}' is not a valid identifier");
            return;
        }
        var grouped = rename.Locations.GroupBy(location =>
            _javaScriptLanguageService.GetNormalizedFileName(location.FileName));
        var updated = new Dictionary<string, (JavaScriptProjectEntry Entry, string Text)>(StringComparer.Ordinal);
        try
        {
            foreach (var group in grouped)
            {
                if (!_javaScriptProject.TryGetValue(group.Key, out var entry))
                {
                    SetLanguageStatus($"Rename cancelled: {group.Key} is outside the loaded project");
                    return;
                }
                var changes = group.Select(location =>
                    new JavaScriptTextChange(location.TextSpan.Start, location.TextSpan.Length, newName));
                updated[group.Key] = (entry, JavaScriptLanguageService.ApplyTextChanges(entry.Text, changes));
            }

            var currentFile = DataContext is MainWindowViewModel currentVm
                ? GetLanguageFileName(currentVm.Sources)
                : null;
            var currentNormalized = string.IsNullOrWhiteSpace(currentFile)
                ? ""
                : _javaScriptLanguageService.GetNormalizedFileName(currentFile);
            var unsupportedRuntime = updated.FirstOrDefault(change =>
                !change.Value.Entry.IsWorkspace && change.Key != currentNormalized);
            if (!string.IsNullOrEmpty(unsupportedRuntime.Key))
            {
                SetLanguageStatus($"Rename cancelled: {unsupportedRuntime.Value.Entry.FileName} is a separate runtime script");
                return;
            }

            var saved = new List<(JavaScriptProjectEntry Entry, string PreviousText)>();
            if (DataContext is MainWindowViewModel vm)
            {
                foreach (var change in updated.Values.Where(value => value.Entry.IsWorkspace))
                {
                    try
                    {
                        if (!await vm.Sources.SaveJavaScriptProjectDocumentAsync(change.Entry.FileName, change.Text))
                        {
                            throw new InvalidOperationException($"Unable to save {change.Entry.FileName}");
                        }
                        saved.Add((change.Entry, change.Entry.Text));
                    }
                    catch (Exception ex)
                    {
                        foreach (var rollback in saved.AsEnumerable().Reverse())
                        {
                            try
                            {
                                await vm.Sources.SaveJavaScriptProjectDocumentAsync(
                                    rollback.Entry.FileName, rollback.PreviousText);
                            }
                            catch { }
                        }
                        SetLanguageStatus($"Rename rolled back: {ex.Message}");
                        return;
                    }
                }
            }

            if (updated.TryGetValue(currentNormalized, out var currentChange))
            {
                txtSourceContent.Text = currentChange.Text;
                if (DataContext is MainWindowViewModel selectedVm)
                {
                    selectedVm.Sources.SelectedFileContent = currentChange.Text;
                }
            }
            foreach (var (normalized, change) in updated)
            {
                _javaScriptProject[normalized] = change.Entry with { Text = change.Text };
                await _javaScriptLanguageService.OpenDocumentAsync(change.Entry.FileName, change.Text);
            }

            var runtimeEdit = updated.TryGetValue(currentNormalized, out var selectedChange) &&
                              !selectedChange.Entry.IsWorkspace;
            SetLanguageStatus(runtimeEdit
                ? $"Renamed {rename.Locations.Count} occurrence(s) · Ctrl+S applies the V8 live edit"
                : $"Renamed {rename.Locations.Count} occurrence(s) in {updated.Count} file(s)");
            CloseJavaScriptRename(clearStatus: false);
        }
        catch (Exception ex)
        {
            SetLanguageStatus($"Rename failed: {ex.Message}");
        }
    }

    private static bool IsJavaScriptIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || !(char.IsLetter(value[0]) || value[0] is '_' or '$')) return false;
        return value.Skip(1).All(character => char.IsLetterOrDigit(character) || character is '_' or '$' ||
            char.GetUnicodeCategory(character) is System.Globalization.UnicodeCategory.NonSpacingMark or
                System.Globalization.UnicodeCategory.SpacingCombiningMark or
                System.Globalization.UnicodeCategory.ConnectorPunctuation);
    }

    private JavaScriptOutlineItem CreateOutlineItem(string role, JavaScriptDocumentSpan span)
    {
        var normalized = _javaScriptLanguageService.GetNormalizedFileName(span.FileName);
        var fileName = _javaScriptProject.TryGetValue(normalized, out var entry)
            ? entry.FileName
            : span.FileName;
        var (line, column) = _javaScriptLanguageService.GetLineColumn(span.FileName, span.TextSpan.Start);
        return new JavaScriptOutlineItem($"{role} · {Path.GetFileName(fileName)}:{line}:{column}",
            span.FileName, span.TextSpan.Start, []);
    }

    private JavaScriptOutlineItem CreateSymbolItem(string fileName, JavaScriptNavigationSymbol symbol)
    {
        var span = symbol.NameSpan ?? symbol.Spans.FirstOrDefault() ?? new JavaScriptTextSpan(0, 0);
        return new JavaScriptOutlineItem($"{symbol.Text} · {symbol.Kind}", fileName, span.Start,
            symbol.Children.Select(child => CreateSymbolItem(fileName, child)).ToArray());
    }

    private void NavigateToJavaScriptItem(JavaScriptOutlineItem item)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var normalized = _javaScriptLanguageService.GetNormalizedFileName(item.FileName);
        var current = GetLanguageFileName(vm.Sources);
        var currentNormalized = string.IsNullOrWhiteSpace(current)
            ? ""
            : _javaScriptLanguageService.GetNormalizedFileName(current);
        var (line, _) = _javaScriptLanguageService.GetLineColumn(item.FileName, item.Offset);
        if (normalized == currentNormalized)
        {
            txtSourceContent.CaretOffset = Math.Clamp(item.Offset, 0, txtSourceContent.Text.Length);
            txtSourceContent.TextArea.Caret.BringCaretToView();
            txtSourceContent.Focus();
            return;
        }

        if (_javaScriptProject.TryGetValue(normalized, out var entry))
        {
            vm.Sources.PendingScrollLine = line;
            if (vm.Sources.FindFileByPath(entry.FileName) is { } workspaceFile)
            {
                vm.Sources.SelectedFile = workspaceFile;
                return;
            }
            var runtimeScript = vm.Sources.RuntimeScripts.FirstOrDefault(script =>
                string.Equals(script.Url, entry.FileName, StringComparison.Ordinal));
            if (runtimeScript is not null) vm.Sources.SelectedRuntimeScript = runtimeScript;
        }
    }

    private void CloseJavaScriptRename(bool clearStatus = true)
    {
        _pendingJavaScriptRename = null;
        pnlSourceRename.IsVisible = false;
        if (clearStatus) SetLanguageStatus("");
        txtSourceContent.Focus();
    }

    private void SetLanguageStatus(string status)
    {
        txtLanguageStatus.Text = status;
        ToolTip.SetTip(txtLanguageStatus, string.IsNullOrWhiteSpace(status)
            ? "Ctrl+P files · Ctrl+Shift+P commands · Ctrl+T workspace symbols · Ctrl+Shift+O document symbols"
            : status);
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

    private async void TxtSourceContent_PointerMoved(object? sender, PointerEventArgs e)
    {
        var editor = txtSourceContent;
        if (editor == null || editor.Document == null) return;
        _debuggerHoverCancellation?.Cancel();
        _languageHoverCancellation?.Cancel();

        var pos = e.GetPosition(editor.TextArea.TextView);
        var position = editor.TextArea.TextView.GetPosition(pos + editor.TextArea.TextView.ScrollOffset);
        if (position.HasValue)
        {
            int offset = editor.Document.GetOffset(position.Value.Location);
            var loc = editor.Document.GetLocation(offset);
            
            var vm = DataContext as MainWindowViewModel;
            var fileName = vm is null ? null : GetLanguageFileName(vm.Sources);
            if (vm != null && !string.IsNullOrEmpty(fileName))
            {
                string ext = GetLanguageExtension(fileName);

                if (vm.Sources.IsDebuggerPaused && vm.Sources.SelectedCallFrame?.CanInspect == true &&
                    ext is ".js" or ".jsx" or ".mjs" or ".cjs" or ".ts" or ".tsx")
                {
                    var boundaries = GetWordBoundary(editor.Text, offset);
                    var expression = boundaries.end > boundaries.start
                        ? editor.Text[boundaries.start..boundaries.end]
                        : "";
                    if (!string.IsNullOrWhiteSpace(expression))
                    {
                        var hoverKey = $"{vm.Sources.SelectedCallFrame.CallFrameId}:{expression}";
                        if (_lastDebuggerHoverKey == hoverKey && ToolTip.GetIsOpen(editor)) return;
                        _debuggerHoverCancellation?.Cancel();
                        _debuggerHoverCancellation?.Dispose();
                        _debuggerHoverCancellation = new System.Threading.CancellationTokenSource();
                        var cancellationToken = _debuggerHoverCancellation.Token;
                        try
                        {
                            await System.Threading.Tasks.Task.Delay(250, cancellationToken);
                            var value = await vm.Sources.EvaluateHoverAsync(expression);
                            if (!cancellationToken.IsCancellationRequested && !string.IsNullOrWhiteSpace(value))
                            {
                                _lastDebuggerHoverKey = hoverKey;
                                ToolTip.SetTip(editor, $"{expression} = {value}");
                                ToolTip.SetIsOpen(editor, true);
                                return;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }
                
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
                else if (IsJavaScriptExtension(ext))
                {
                    _languageHoverCancellation?.Dispose();
                    _languageHoverCancellation = new System.Threading.CancellationTokenSource();
                    var cancellationToken = _languageHoverCancellation.Token;
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(250, cancellationToken);
                        var text = editor.Text;
                        await _javaScriptLanguageService.OpenDocumentAsync(
                            fileName,
                            text,
                            GetProjectRoot(vm.Sources, fileName),
                            cancellationToken);
                        var hover = await _javaScriptLanguageService.GetQuickInfoAsync(
                            fileName,
                            loc.Line,
                            loc.Column,
                            cancellationToken);
                        if (!cancellationToken.IsCancellationRequested && editor.Text == text && hover is not null)
                        {
                            contents = string.IsNullOrWhiteSpace(hover.Documentation)
                                ? hover.DisplayText
                                : $"{hover.DisplayText}\n\n{hover.Documentation}";
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SourcesView] JavaScript hover failed: {ex.Message}");
                    }
                }

                if (!string.IsNullOrEmpty(contents))
                {
                    ToolTip.SetTip(editor, contents);
                    ToolTip.SetIsOpen(editor, true);
                    return;
                }
            }
        }
        _lastDebuggerHoverKey = "";
        _debuggerHoverCancellation?.Cancel();
        ToolTip.SetIsOpen(editor, false);
    }

    private void UpdateDiagnostics()
    {
        _languageDiagnosticsCancellation?.Cancel();
        _languageDiagnosticsCancellation?.Dispose();
        _languageDiagnosticsCancellation = null;

        var editor = txtSourceContent;
        if (editor == null || editor.Document == null) return;

        var vm = DataContext as MainWindowViewModel;
        var fileName = vm is null ? null : GetLanguageFileName(vm.Sources);
        if (vm == null || string.IsNullOrEmpty(fileName)) return;

        string ext = GetLanguageExtension(fileName);

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
        else if (IsJavaScriptExtension(ext))
        {
            _diagnosticColorizer.Diagnostics = diags;
            editor.TextArea.TextView.Redraw();
            _languageDiagnosticsCancellation = new System.Threading.CancellationTokenSource();
            _ = UpdateJavaScriptDiagnosticsAsync(
                vm.Sources,
                fileName,
                editor.Text,
                _languageDiagnosticsCancellation.Token);
            return;
        }

        _diagnosticColorizer.Diagnostics = diags;
        editor.TextArea.TextView.Redraw();
    }

    private async System.Threading.Tasks.Task UpdateJavaScriptDiagnosticsAsync(
        SourcesViewModel sources,
        string fileName,
        string text,
        System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(300, cancellationToken);
            await _javaScriptLanguageService.OpenDocumentAsync(
                fileName,
                text,
                GetProjectRoot(sources, fileName),
                cancellationToken);
            var diagnostics = await _javaScriptLanguageService.GetDiagnosticsAsync(fileName, cancellationToken);
            var ranges = diagnostics
                .Where(diagnostic => diagnostic.Length > 0)
                .Select(diagnostic =>
                {
                    var start = _javaScriptLanguageService.GetLineColumn(fileName, diagnostic.Start);
                    var end = _javaScriptLanguageService.GetLineColumn(
                        fileName,
                        diagnostic.Start + Math.Max(1, diagnostic.Length));
                    return new LspDiagnosticColorizer.DiagnosticRange(
                        start.Line,
                        start.Column,
                        end.Line,
                        end.Column);
                })
                .ToList();

            if (cancellationToken.IsCancellationRequested) return;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested || txtSourceContent.Text != text ||
                    DataContext is not MainWindowViewModel current ||
                    GetLanguageFileName(current.Sources) != fileName)
                {
                    return;
                }
                _diagnosticColorizer.Diagnostics = ranges;
                txtSourceContent.TextArea.TextView.Redraw();
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SourcesView] JavaScript diagnostics failed: {ex.Message}");
        }
    }

    private static bool IsJavaScriptExtension(string extension) =>
        extension is ".js" or ".jsx" or ".mjs" or ".cjs" or ".ts" or ".tsx";

    private static string GetLanguageExtension(string fileName)
    {
        if (Uri.TryCreate(fileName, UriKind.Absolute, out var uri)) fileName = uri.AbsolutePath;
        return Path.GetExtension(fileName).ToLowerInvariant();
    }

    private static string? GetLanguageFileName(SourcesViewModel sources)
    {
        if (!string.IsNullOrWhiteSpace(sources.SelectedFilePath)) return sources.SelectedFilePath;
        if (!string.IsNullOrWhiteSpace(sources.SelectedRuntimeScript?.Url)) return sources.SelectedRuntimeScript.Url;
        return string.IsNullOrWhiteSpace(sources.SelectedFileName) ? null : sources.SelectedFileName;
    }

    private static string? GetProjectRoot(SourcesViewModel sources, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(sources.SelectedFilePath))
        {
            return Path.GetDirectoryName(sources.SelectedFilePath);
        }
        if (Uri.TryCreate(fileName, UriKind.Absolute, out var uri))
        {
            var slash = uri.AbsolutePath.LastIndexOf('/');
            return slash <= 0 ? $"{uri.Scheme}://{uri.Host}/" : $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath[..slash]}";
        }
        return Path.GetDirectoryName(fileName);
    }

    private sealed record JavaScriptProjectEntry(string FileName, string Text, bool IsWorkspace);

    private sealed record JavaScriptEditorContext(
        SourcesViewModel Sources,
        string FileName,
        string NormalizedFileName,
        int Line,
        int Column,
        System.Threading.CancellationToken CancellationToken);
}

public sealed record JavaScriptOutlineItem(
    string DisplayName,
    string FileName,
    int Offset,
    IReadOnlyList<JavaScriptOutlineItem> Children)
{
    public override string ToString() => DisplayName;
}

public sealed class SourcesPaletteItem
{
    private readonly Func<System.Threading.Tasks.Task> _invoke;

    public SourcesPaletteItem(
        string label,
        string detail,
        string kind,
        string shortcut,
        Func<System.Threading.Tasks.Task> invoke)
    {
        Label = label;
        Detail = detail;
        Kind = kind;
        Shortcut = shortcut;
        _invoke = invoke;
    }

    public string Label { get; }
    public string Detail { get; }
    public string Kind { get; }
    public string Shortcut { get; }
    public string SearchText => $"{Label} {Detail} {Kind}";

    internal System.Threading.Tasks.Task InvokeAsync() => _invoke();

    public override string ToString() => Label;
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

public sealed class DebuggerLineColorizer : DocumentColorizingTransformer
{
    private readonly Func<int?> _activeLine;

    public DebuggerLineColorizer(Func<int?> activeLine) => _activeLine = activeLine;

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_activeLine() != line.LineNumber || line.Length == 0) return;
        ChangeLinePart(line.Offset, line.EndOffset, visualLine =>
        {
            visualLine.BackgroundBrush = new SolidColorBrush(Color.FromArgb(75, 255, 202, 40));
        });
    }
}
