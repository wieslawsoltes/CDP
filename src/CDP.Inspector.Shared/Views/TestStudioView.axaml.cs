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
using System.Linq;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.LogicalTree;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
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
            if (!OperatingSystem.IsBrowser())
            {
                try
                {
                    // 1. Initialize TextMate with Dark+ theme
                    _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                    _textMateInstallation = editor.InstallTextMate(_registryOptions);
                    var yamlLanguage = _registryOptions.GetLanguageByExtension(".yaml");
                    if (yamlLanguage != null)
                    {
                        _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(yamlLanguage.Id));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TestStudioView] Failed to initialize TextMate: {ex.Message}");
                }
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

            // 2c. Update editor text when it gets focused to ensure sync
            editor.GotFocus += (s, ev) =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    UpdateEditorText(vm.Recorder.TestStudio.YamlCode);
                }
            };
            editor.TextArea.GotFocus += (s, ev) =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    UpdateEditorText(vm.Recorder.TestStudio.YamlCode);
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
                            var provider = new TestStudioGutterDataProvider(vm.Recorder.TestStudio, vm.Recorder);
                            var gutter = new ReplayGutterMargin(provider);
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
                        try
                        {
                            vm.Recorder.TestStudio.LoadFlowFile(item.Path);
                        }
                        catch (System.Exception ex)
                        {
                            vm.Recorder.TestStudio.Log($"Error loading flow file: {ex.Message}");
                        }
                    }
                }
            };
        }

        // --- Toolbox Drag & Drop & Context Menus Initialization ---
        Loaded += (sender, e) =>
        {
            var accordion = this.FindControl<Control>("toolboxAccordion");
            if (accordion != null)
            {
                SetupToolboxDragHandlers(accordion);
            }
        };

        this.AddHandler(PointerMovedEvent, OnToolboxPointerMoved, RoutingStrategies.Tunnel);
        this.AddHandler(PointerReleasedEvent, OnToolboxPointerReleased, RoutingStrategies.Tunnel);

        var yamlEditor = this.FindControl<TextEditor>("txtYamlCode");
        if (yamlEditor != null)
        {
            DragDrop.SetAllowDrop(yamlEditor, true);
            yamlEditor.AddHandler(DragDrop.DragOverEvent, OnYamlDragOver);
            yamlEditor.AddHandler(DragDrop.DropEvent, OnYamlDrop);

            yamlEditor.PointerPressed += (s, e) =>
            {
                var prop = e.GetCurrentPoint(yamlEditor).Properties;
                if (prop.IsRightButtonPressed)
                {
                    var pos = e.GetPosition(yamlEditor.TextArea);
                    var caretPos = yamlEditor.GetPositionFromPoint(pos);
                    if (caretPos.HasValue)
                    {
                        yamlEditor.CaretOffset = yamlEditor.Document.GetOffset(caretPos.Value.Line, caretPos.Value.Column);
                    }
                }
            };

            yamlEditor.ContextMenu = ToolboxMenuHelper.CreateContextMenu(cmd =>
            {
                string yamlTemplate = ToolboxMenuHelper.GetYamlTemplateForCommand(cmd);
                yamlEditor.Document.Insert(yamlEditor.CaretOffset, yamlTemplate);
            });
        }

        var stepsGrid = this.FindControl<DataGrid>("lstSteps");
        if (stepsGrid != null)
        {
            DragDrop.SetAllowDrop(stepsGrid, true);
            stepsGrid.AddHandler(DragDrop.DragOverEvent, OnStepsDragOver);
            stepsGrid.AddHandler(DragDrop.DropEvent, OnStepsDrop);

            stepsGrid.ContextMenu = ToolboxMenuHelper.CreateContextMenu(cmd =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    int insertIndex = vm.Recorder.TestStudio.Steps.Count;
                    if (stepsGrid.SelectedItem is HierarchicalNode<TestStudioStepModel> node && node.Item is TestStudioStepModel targetStep)
                    {
                        insertIndex = vm.Recorder.TestStudio.Steps.IndexOf(targetStep);
                    }
                    vm.Recorder.TestStudio.InsertCommandStep(cmd, "", "", insertIndex);
                }
            });
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
                    var editor = this.FindControl<TextEditor>("txtYamlCode");
                    if (editor != null && !editor.IsFocused && !editor.TextArea.IsFocused)
                    {
                        UpdateEditorText(vm.Recorder.TestStudio.YamlCode);
                    }
                }
            });
        }
        else if (e.PropertyName == nameof(TestStudioViewModel.ExecutingStep))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    var testStudio = vm.Recorder.TestStudio;
                    var step = testStudio.ExecutingStep;
                    if (step != null)
                    {
                        var editor = this.FindControl<TextEditor>("txtYamlCode");
                        if (editor != null && step.StartLine > 0 && step.StartLine <= editor.Document.LineCount)
                        {
                            editor.ScrollToLine(step.StartLine);
                        }

                        var node = testStudio.NodeEditor.Nodes.OfType<TestStudioNodeViewModel>()
                                             .FirstOrDefault(n => n.Step == step);
                        if (node != null)
                        {
                            testStudio.NodeEditor.BringNodeIntoView(node);
                        }

                        var lstSteps = this.FindControl<DataGrid>("lstSteps");
                        if (lstSteps != null)
                        {
                            lstSteps.ScrollIntoView(step, null);
                        }
                    }
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

    // --- Toolbox Drag & Drop Support Handlers ---
    private Point _dragStartPoint;
    private bool _isMouseDown;
    private string? _draggedCommand;
    private PointerPressedEventArgs? _pressedEventArgs;

    private void SetupToolboxDragHandlers(ILogical parent)
    {
        if (parent is Button btn && btn.Content is string text)
        {
            string? commandName = ToolboxMenuHelper.MapContentToCommand(text);
            if (commandName != null)
            {
                btn.AddHandler(PointerPressedEvent, (sender, e) =>
                {
                    if (e.GetCurrentPoint(btn).Properties.IsLeftButtonPressed)
                    {
                        _isMouseDown = true;
                        _dragStartPoint = e.GetPosition(this);
                        _draggedCommand = commandName;
                        _pressedEventArgs = e;
                    }
                }, RoutingStrategies.Tunnel);
            }
        }

        foreach (var child in parent.LogicalChildren)
        {
            SetupToolboxDragHandlers(child);
        }
    }

    private async void OnToolboxPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isMouseDown && _draggedCommand != null)
        {
            var currentPos = e.GetPosition(this);
            var delta = currentPos - _dragStartPoint;
            if (Math.Abs(delta.X) > 3 || Math.Abs(delta.Y) > 3)
            {
                _isMouseDown = false;
                var cmd = _draggedCommand;
                _draggedCommand = null;

                var item = new DataTransferItem();
                item.Set(ToolboxMenuHelper.CdpCommandFormat, cmd);
                
                string yamlTemplate = ToolboxMenuHelper.GetYamlTemplateForCommand(cmd);
                item.Set(DataFormat.Text, yamlTemplate);

                var data = new DataTransfer();
                data.Add(item);

                var effect = await DragDrop.DoDragDropAsync(_pressedEventArgs!, data, DragDropEffects.Copy);
            }
        }
    }

    private void OnToolboxPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isMouseDown = false;
        _draggedCommand = null;
    }

    private void OnYamlDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Formats.Contains(DataFormat.Text) || e.DataTransfer.Formats.Contains(ToolboxMenuHelper.CdpCommandFormat))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnYamlDrop(object? sender, DragEventArgs e)
    {
        var yamlEditor = this.FindControl<TextEditor>("txtYamlCode");
        if (yamlEditor != null)
        {
            var text = e.DataTransfer.TryGetText();
            if (!string.IsNullOrEmpty(text))
            {
                var visualPos = e.GetPosition(yamlEditor.TextArea);
                var position = yamlEditor.GetPositionFromPoint(visualPos);
                if (position.HasValue)
                {
                    int offset = yamlEditor.Document.GetOffset(position.Value.Line, position.Value.Column);
                    yamlEditor.Document.Insert(offset, text);
                    yamlEditor.CaretOffset = offset + text.Length;
                    yamlEditor.Focus();
                }
                else
                {
                    yamlEditor.Document.Insert(yamlEditor.CaretOffset, text);
                }
                e.Handled = true;
            }
        }
    }

    private void OnStepsDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Formats.Contains(ToolboxMenuHelper.CdpCommandFormat))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnStepsDrop(object? sender, DragEventArgs e)
    {
        var stepsGrid = this.FindControl<DataGrid>("lstSteps");
        if (stepsGrid != null && DataContext is MainWindowViewModel vm)
        {
            var command = e.DataTransfer.TryGetValue(ToolboxMenuHelper.CdpCommandFormat);
            if (!string.IsNullOrEmpty(command))
            {
                int insertIndex = vm.Recorder.TestStudio.Steps.Count;
                var hitVisual = e.Source as Control;
                
                var row = hitVisual;
                while (row != null && !(row is DataGridRow))
                {
                    row = row.Parent as Control;
                }

                if (row != null && row.DataContext is HierarchicalNode<TestStudioStepModel> node && node.Item is TestStudioStepModel targetStep)
                {
                    insertIndex = vm.Recorder.TestStudio.Steps.IndexOf(targetStep);
                }

                vm.Recorder.TestStudio.InsertCommandStep(command, "", "", insertIndex);
                e.Handled = true;
            }
        }
    }
}
