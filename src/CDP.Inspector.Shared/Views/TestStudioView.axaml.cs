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
using CDP.Editor.Splits.Controls;

namespace CdpInspectorApp.Views;

[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataGrid is not trim-safe")]
public partial class TestStudioView : UserControl
{
    public static readonly DataFormat<string> WorkspaceItemPathFormat = DataFormat.CreateInProcessFormat<string>("workspace-item-path");
    private bool _isUpdatingText = false;
    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;

    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();
    private TextEditor? _yamlEditor;
    private DataGrid? _stepsGrid;

    public TestStudioView()
    {
        InitializeComponent();

        // 1. Cache controls from HiddenPanel
        var hiddenPanel = this.FindControl<Grid>("HiddenPanel");
        if (hiddenPanel != null)
        {
            var children = hiddenPanel.Children.ToList();
            foreach (var child in children)
            {
                if (child is Control ctrl && !string.IsNullOrEmpty(ctrl.Name))
                {
                    hiddenPanel.Children.Remove(ctrl);
                    _viewsCache[ctrl.Name] = ctrl;
                }
            }
        }

        // 2. Setup SuperSplit ViewResolver
        var splitControl = this.FindControl<SuperSplit>("SplitControl");
        if (splitControl != null)
        {
            splitControl.ViewResolver = (viewName, box) =>
            {
                string cacheKey = viewName;
                if (viewName == "StepsList") cacheKey = "pnlStepsList";
                else if (viewName == "NodeEditor") cacheKey = "pnlNodeEditor";
                else if (viewName == "YamlConfiguration") cacheKey = "pnlYamlConfiguration";
                else if (viewName == "ExecutionLog") cacheKey = "pnlExecutionLog";
                else if (viewName == "ProjectSidebar") cacheKey = "pnlProjectSidebar";

                if (_viewsCache.TryGetValue(cacheKey, out var view))
                {
                    return view;
                }
                return null;
            };
        }

        // 3. Cache references to the text editor and steps list
        _yamlEditor = this.FindControl<TextEditor>("txtYamlCode");
        _stepsGrid = this.FindControl<DataGrid>("lstSteps");
        
        var editor = _yamlEditor;
        if (editor != null)
        {
            if (!OperatingSystem.IsBrowser())
            {
                try
                {
                    // Initialize TextMate with Dark+ theme
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

            // Synchronize editor edits back to ViewModel
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

            // Attach auto-completion handlers
            editor.TextArea.TextEntered += TextArea_TextEntered;
            editor.TextArea.KeyDown += TextArea_KeyDown;

            // Update editor text when it gets focused to ensure sync
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

        // Synchronize ViewModel changes to editor
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

                    vm.Recorder.TestStudio.FilePickerHandler = async () =>
                    {
                        var topLevel = TopLevel.GetTopLevel(this);
                        if (topLevel != null)
                        {
                            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                            {
                                Title = "Select Executable Application",
                                AllowMultiple = false
                            });
                            if (files != null && files.Count > 0)
                            {
                                return files[0].Path.LocalPath;
                            }
                        }
                        return null;
                    };

                    vm.Recorder.TestStudio.ConfirmCloseDirtyEditorCallback = async (filePath) =>
                    {
                        var topLevel = TopLevel.GetTopLevel(this);
                        if (topLevel is Window parentWindow)
                        {
                            var discardBtn = new Button { Content = "Discard" };
                            var cancelBtn = new Button { Content = "Cancel" };
                            var dialog = new Window
                            {
                                Title = "Save Changes?",
                                Width = 400,
                                Height = 150,
                                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                Content = new StackPanel
                                {
                                    Spacing = 20,
                                    Margin = new Thickness(20),
                                    Children =
                                    {
                                        new TextBlock 
                                        { 
                                            Text = filePath == null 
                                                ? "You have unsaved changes in multiple editors. Do you want to close them and discard unsaved changes?"
                                                : $"You have unsaved changes in '{System.IO.Path.GetFileName(filePath)}'. Do you want to close it and discard unsaved changes?",
                                            TextWrapping = Avalonia.Media.TextWrapping.Wrap 
                                        },
                                        new StackPanel
                                        {
                                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                                            Spacing = 10,
                                            Children =
                                            {
                                                discardBtn,
                                                cancelBtn
                                            }
                                        }
                                    }
                                }
                            };
                            discardBtn.Click += (s, e) => dialog.Close(true);
                            cancelBtn.Click += (s, e) => dialog.Close(false);
                            return await dialog.ShowDialog<bool>(parentWindow);
                        }
                        return true;
                    };

                    // Insert Gutter status margin if not already added
                    if (_yamlEditor != null)
                    {
                        bool alreadyAdded = false;
                        foreach (var margin in _yamlEditor.TextArea.LeftMargins)
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
                            for (int i = 0; i < _yamlEditor.TextArea.LeftMargins.Count; i++)
                            {
                                var margin = _yamlEditor.TextArea.LeftMargins[i];
                                if (margin.GetType().Name.Contains("LineNumberMargin"))
                                {
                                    insertIndex = i + 1;
                                    break;
                                }
                            }
                            _yamlEditor.TextArea.LeftMargins.Insert(insertIndex, gutter);
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

            // Setup Workspace Drag & Drop support
            DragDrop.SetAllowDrop(treeWorkspace, true);
            
            Point dragStartPoint = new Point();
            WorkspaceItemModel? dragSourceItem = null;
            PointerPressedEventArgs? pressedArgs = null;

            treeWorkspace.PointerPressed += (s, e) =>
            {
                var properties = e.GetCurrentPoint(treeWorkspace).Properties;
                if (properties.IsLeftButtonPressed)
                {
                    dragStartPoint = e.GetPosition(treeWorkspace);
                    pressedArgs = e;
                    var visual = e.Source as Visual;
                    while (visual != null)
                    {
                        if (visual is DataGridRow row && row.DataContext is HierarchicalNode<WorkspaceItemModel> node)
                        {
                            dragSourceItem = node.Item;
                            break;
                        }
                        visual = visual.GetVisualParent();
                    }
                }
            };

            treeWorkspace.PointerMoved += async (s, e) =>
            {
                var properties = e.GetCurrentPoint(treeWorkspace).Properties;
                if (properties.IsLeftButtonPressed && dragSourceItem != null && pressedArgs != null)
                {
                    var pos = e.GetPosition(treeWorkspace);
                    if (Math.Abs(pos.X - dragStartPoint.X) > 5 || Math.Abs(pos.Y - dragStartPoint.Y) > 5)
                    {
                        var dragData = new DataTransfer();
                        var item = new DataTransferItem();
                        item.Set(WorkspaceItemPathFormat, dragSourceItem.Path);
                        dragData.Add(item);

                        var currentPressedArgs = pressedArgs;
                        dragSourceItem = null; // Reset to prevent multiple drag starts
                        pressedArgs = null;

                        await DragDrop.DoDragDropAsync(currentPressedArgs, dragData, DragDropEffects.Move);
                    }
                }
            };

            treeWorkspace.AddHandler(DragDrop.DragOverEvent, (s, e) =>
            {
                if (e.DataTransfer.Formats.Any(f => f == WorkspaceItemPathFormat))
                {
                    e.DragEffects = DragDropEffects.Move;
                }
                else
                {
                    e.DragEffects = DragDropEffects.None;
                }
                e.Handled = true;
            });

            treeWorkspace.AddHandler(DragDrop.DropEvent, (s, e) =>
            {
                if (e.DataTransfer.Formats.Any(f => f == WorkspaceItemPathFormat) && DataContext is MainWindowViewModel vm)
                {
                    var sourcePath = e.DataTransfer.TryGetValue(WorkspaceItemPathFormat) as string;
                    if (!string.IsNullOrEmpty(sourcePath))
                    {
                        var visual = e.Source as Visual;
                        WorkspaceItemModel? targetItem = null;
                        while (visual != null)
                        {
                            if (visual is DataGridRow row && row.DataContext is HierarchicalNode<WorkspaceItemModel> node)
                            {
                                targetItem = node.Item;
                                break;
                            }
                            visual = visual.GetVisualParent();
                        }

                        string targetPath = vm.Recorder.TestStudio.WorkspaceRootPath ?? "";
                        if (targetItem != null)
                        {
                            targetPath = targetItem.IsFolder ? targetItem.Path : System.IO.Path.GetDirectoryName(targetItem.Path) ?? targetPath;
                        }

                        vm.Recorder.TestStudio.MoveWorkspaceItem(sourcePath, targetPath);
                    }
                }
                e.Handled = true;
            });
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

        if (_yamlEditor != null)
        {
            DragDrop.SetAllowDrop(_yamlEditor, true);
            _yamlEditor.AddHandler(DragDrop.DragOverEvent, OnYamlDragOver);
            _yamlEditor.AddHandler(DragDrop.DropEvent, OnYamlDrop);

            _yamlEditor.PointerPressed += (s, e) =>
            {
                var prop = e.GetCurrentPoint(_yamlEditor).Properties;
                if (prop.IsRightButtonPressed)
                {
                    var pos = e.GetPosition(_yamlEditor.TextArea);
                    var caretPos = _yamlEditor.GetPositionFromPoint(pos);
                    if (caretPos.HasValue)
                    {
                        _yamlEditor.CaretOffset = _yamlEditor.Document.GetOffset(caretPos.Value.Line, caretPos.Value.Column);
                    }
                }
            };

            _yamlEditor.ContextMenu = ToolboxMenuHelper.CreateContextMenu(cmd =>
            {
                string yamlTemplate = ToolboxMenuHelper.GetYamlTemplateForCommand(cmd);
                _yamlEditor.Document.Insert(_yamlEditor.CaretOffset, yamlTemplate);
            });
        }

        if (_stepsGrid != null)
        {
            DragDrop.SetAllowDrop(_stepsGrid, true);
            _stepsGrid.AddHandler(DragDrop.DragOverEvent, OnStepsDragOver);
            _stepsGrid.AddHandler(DragDrop.DropEvent, OnStepsDrop);

            _stepsGrid.ContextMenu = ToolboxMenuHelper.CreateContextMenu(cmd =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    int insertIndex = vm.Recorder.TestStudio.Steps.Count;
                    if (_stepsGrid.SelectedItem is HierarchicalNode<TestStudioStepModel> node && node.Item is TestStudioStepModel targetStep)
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
                    if (_yamlEditor != null && !_yamlEditor.IsFocused && !_yamlEditor.TextArea.IsFocused)
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
                        if (_yamlEditor != null && step.StartLine > 0 && step.StartLine <= _yamlEditor.Document.LineCount)
                        {
                            _yamlEditor.ScrollToLine(step.StartLine);
                        }

                        var node = testStudio.NodeEditor.Nodes.OfType<TestStudioNodeViewModel>()
                                             .FirstOrDefault(n => n.Step == step);
                        if (node != null)
                        {
                            testStudio.NodeEditor.BringNodeIntoView(node);
                        }

                        if (_stepsGrid != null)
                        {
                            _stepsGrid.ScrollIntoView(step, null);
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
            if (_yamlEditor != null && _yamlEditor.Text != text)
            {
                _yamlEditor.Text = text ?? "";
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
        if (_yamlEditor == null) return;

        var vm = DataContext as MainWindowViewModel;
        if (vm == null) return;

        string text = _yamlEditor.Text ?? "";
        int caretOffset = _yamlEditor.CaretOffset;

        var suggestions = YamlIntelliSenseProvider.GetSuggestions(text, caretOffset, vm);
        if (suggestions == null || suggestions.Count == 0)
        {
            CloseCompletionWindow();
            return;
        }

        CloseCompletionWindow();

        var completionWindow = new CompletionWindow(_yamlEditor.TextArea)
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
        if (_yamlEditor != null)
        {
            var text = e.DataTransfer.TryGetText();
            if (!string.IsNullOrEmpty(text))
            {
                var visualPos = e.GetPosition(_yamlEditor.TextArea);
                var position = _yamlEditor.GetPositionFromPoint(visualPos);
                if (position.HasValue)
                {
                    int offset = _yamlEditor.Document.GetOffset(position.Value.Line, position.Value.Column);
                    _yamlEditor.Document.Insert(offset, text);
                    _yamlEditor.CaretOffset = offset + text.Length;
                    _yamlEditor.Focus();
                }
                else
                {
                    _yamlEditor.Document.Insert(_yamlEditor.CaretOffset, text);
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
        if (_stepsGrid != null && DataContext is MainWindowViewModel vm)
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
