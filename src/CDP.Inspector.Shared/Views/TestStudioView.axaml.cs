using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Services;
using CdpInspectorApp.Models;
using XamlPlayground.Editor.Minimap.Inline;
using ProDataGrid;
using Avalonia.Controls.DataGridHierarchical;

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

            // 2b. Attach auto-completion handlers
            editor.TextArea.TextEntered += TextArea_TextEntered;
            editor.TextArea.KeyDown += TextArea_KeyDown;
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

                    vm.Recorder.TestStudio.FolderPickerHandler = async () =>
                    {
                        var topLevel = TopLevel.GetTopLevel(this);
                        if (topLevel != null)
                        {
                            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                            {
                                Title = "Select Workspace Root Directory",
                                AllowMultiple = false
                            });
                            if (folders != null && folders.Count > 0)
                            {
                                return folders[0].Path.LocalPath;
                            }
                        }
                        return null;
                    };

                    // Insert Gutter status margin if not already added
                    var editor = this.FindControl<TextEditor>("txtYamlCode");
                    if (editor != null)
                    {
                        bool alreadyAdded = false;
                        foreach (var margin in editor.TextArea.LeftMargins)
                        {
                            if (margin is ReplayGutterMargin)
                            {
                                alreadyAdded = true;
                                break;
                            }
                        }

                        if (!alreadyAdded)
                        {
                            var gutter = new ReplayGutterMargin(vm.Recorder.TestStudio, vm.Recorder);
                            int insertIndex = 0;
                            for (int i = 0; i < editor.TextArea.LeftMargins.Count; i++)
                            {
                                var margin = editor.TextArea.LeftMargins[i];
                                if (margin.GetType().Name.Contains("LineNumberMargin"))
                                {
                                    insertIndex = i + 1;
                                    break;
                                }
                            }
                            editor.TextArea.LeftMargins.Insert(insertIndex, gutter);
                        }
                    }
                }
            });
        };

        var btnBrowse = this.FindControl<Button>("btnBrowseReportDir");
        if (btnBrowse != null)
        {
            btnBrowse.Click += async (s, e) =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel != null)
                    {
                        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                        {
                            Title = "Select Output Directory",
                            AllowMultiple = false
                        });
                        if (folders != null && folders.Count > 0)
                        {
                            var folder = folders[0];
                            vm.Recorder.TestStudio.OutputDirectory = folder.Path.LocalPath;
                        }
                    }
                }
            };
        }

        var treeWorkspace = this.FindControl<DataGrid>("treeWorkspace");
        if (treeWorkspace != null)
        {
            treeWorkspace.DoubleTapped += (s, e) =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    var selected = treeWorkspace.SelectedItem;
                    var item = selected is HierarchicalNode<WorkspaceItemModel> node ? node.Item : (selected as WorkspaceItemModel);
                    if (item != null && !item.IsFolder)
                    {
                        vm.Recorder.TestStudio.LoadFlowFile(item.Path);
                    }
                }
            };
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.Recorder.TestStudio.SelectedStep != null)
                {
                    vm.Recorder.TestStudio.SelectedStep = null;
                    e.Handled = true;
                }
            }
        }
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

    private CompletionWindow? _completionWindow;

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

        // Auto-trigger completion on typing letters, digits, spaces, hyphens, colons, quotes, hashes, or brackets
        if (char.IsLetterOrDigit(trigger) || trigger == '-' || trigger == ' ' || trigger == ':' || trigger == '"' || trigger == '\'' || trigger == '#' || trigger == '[' || trigger == ']')
        {
            ShowCompletion(explicitInvocation: false);
        }
    }

    private void ShowCompletion(bool explicitInvocation)
    {
        var editor = this.FindControl<TextEditor>("txtYamlCode");
        if (editor == null) return;

        var vm = DataContext as MainWindowViewModel;
        if (vm == null) return;

        string text = editor.Text ?? "";
        int caretOffset = editor.CaretOffset;

        var suggestions = YamlIntelliSenseProvider.GetSuggestions(text, caretOffset, vm);
        if (suggestions == null || suggestions.Count == 0)
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
            completionWindow.CompletionList.CompletionData.Add(new YamlCompletionData(suggestion));
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
        return char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '#' || c == '"' || c == '[' || c == ']';
    }
}
