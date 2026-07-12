using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CdpInspectorApp.ViewModels;
using CDP.Editor.Splits.Controls;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.Views;

public partial class ScratchView : UserControl
{
    private readonly System.Collections.Generic.Dictionary<string, Control> _viewsCache = new();

    public static readonly DataFormat<string> NodeTypeFormat = DataFormat.CreateInProcessFormat<string>("NodeType");
    public static readonly DataFormat<TimeMachineFrame> TimeMachineFrameFormat = DataFormat.CreateInProcessFormat<TimeMachineFrame>("TimeMachineFrame");

    private void DetachControl(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }
        else if (control.Parent is SuperSplitBox splitBox)
        {
            splitBox.InnerContent = null;
            splitBox.UpdateLayout();
        }
    }

    public ScratchView()
    {
        InitializeComponent();

        var toolboxPanel = ScratchToolboxPanel;
        var canvasPanel = ScratchCanvasPanel;
        var detailsPanel = ScratchDetailsPanel;

        HiddenPanel.Children.Clear();

        _viewsCache["ScratchToolbox"] = toolboxPanel;
        _viewsCache["ScratchCanvas"] = canvasPanel;
        _viewsCache["ScratchDetails"] = detailsPanel;

        SplitControl.ViewResolver = (viewName, targetBox) =>
        {
            if (_viewsCache.TryGetValue(viewName, out var cached))
            {
                if (targetBox == null || cached.Parent != targetBox)
                {
                    DetachControl(cached);
                }
                return cached;
            }
            return new Control();
        };

        var nodeEditor = this.FindControl<CDP.Editor.Nodes.Views.NodeEditorView>("ScratchNodeEditor");
        if (nodeEditor != null)
        {
            DragDrop.SetAllowDrop(nodeEditor, true);
            nodeEditor.AddHandler(DragDrop.DragOverEvent, OnNodeEditorDragOver);
            nodeEditor.AddHandler(DragDrop.DropEvent, OnNodeEditorDrop);
        }

        SetupToolboxDragHandlers(ScratchToolboxPanel);
    }

    private void SetupToolboxDragHandlers(ILogical parent)
    {
        if (parent is Button btn)
        {
            var commandParameter = btn.CommandParameter as string;
            if (!string.IsNullOrEmpty(commandParameter))
            {
                btn.AddHandler(PointerPressedEvent, async (sender, e) =>
                {
                    if (e.GetCurrentPoint(btn).Properties.IsLeftButtonPressed)
                    {
                        var dataObject = new DataTransfer();
                        var item = new DataTransferItem();
                        item.Set(NodeTypeFormat, commandParameter);
                        item.Set(DataFormat.Text, commandParameter);
                        dataObject.Add(item);
                        await DragDrop.DoDragDropAsync(e, dataObject, DragDropEffects.Copy);
                    }
                }, RoutingStrategies.Tunnel);
            }
        }

        foreach (var child in parent.LogicalChildren)
        {
            SetupToolboxDragHandlers(child);
        }
    }

    public async void ToolboxButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Button btn)
        {
            var commandParameter = btn.CommandParameter as string;
            if (!string.IsNullOrEmpty(commandParameter))
            {
                var dataObject = new DataTransfer();
                var item = new DataTransferItem();
                item.Set(NodeTypeFormat, commandParameter);
                item.Set(DataFormat.Text, commandParameter);
                dataObject.Add(item);
                await DragDrop.DoDragDropAsync(e, dataObject, DragDropEffects.Copy);
            }
        }
    }

    private void OnNodeEditorDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Formats.Contains(NodeTypeFormat) || e.DataTransfer.Formats.Contains(TimeMachineFrameFormat))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnNodeEditorDrop(object? sender, DragEventArgs e)
    {
        var nodeEditor = this.FindControl<CDP.Editor.Nodes.Views.NodeEditorView>("ScratchNodeEditor");
        if (nodeEditor != null && DataContext is MainWindowViewModel vm)
        {
            var canvas = nodeEditor.EditorCanvas;
            if (canvas != null)
            {
                var dropPos = e.GetPosition(canvas);

                if (e.DataTransfer.Formats.Contains(NodeTypeFormat))
                {
                    var nodeType = e.DataTransfer.TryGetValue(NodeTypeFormat);
                    if (!string.IsNullOrEmpty(nodeType))
                    {
                        var parameters = new ScratchViewModel.AddNodeParameters(nodeType, dropPos.X, dropPos.Y);
                        vm.Scratch.AddScratchNodeCommand.Execute(parameters);
                        e.Handled = true;
                    }
                }
                else if (e.DataTransfer.Formats.Contains(TimeMachineFrameFormat))
                {
                    var frame = e.DataTransfer.TryGetValue(TimeMachineFrameFormat);
                    if (frame != null)
                    {
                        string? nodeType = null;
                        if (frame.Domain.Equals("DOM", StringComparison.OrdinalIgnoreCase))
                            nodeType = "DOM";
                        else if (frame.Domain.Equals("Accessibility", StringComparison.OrdinalIgnoreCase))
                            nodeType = "Accessibility";
                        else if (frame.Domain.Equals("Network", StringComparison.OrdinalIgnoreCase))
                            nodeType = "Network";
                        else if (frame.Domain.Equals("Console", StringComparison.OrdinalIgnoreCase) || 
                                 frame.Domain.Equals("Runtime", StringComparison.OrdinalIgnoreCase))
                            nodeType = "Console";
                        else if (frame.Domain.Equals("Page", StringComparison.OrdinalIgnoreCase))
                            nodeType = "Page";
                        else if (frame.Domain.Equals("Application", StringComparison.OrdinalIgnoreCase))
                            nodeType = "Application";

                        if (!string.IsNullOrEmpty(nodeType))
                        {
                            var parameters = new ScratchViewModel.AddNodeParameters(nodeType, dropPos.X, dropPos.Y);
                            vm.Scratch.AddScratchNodeCommand.Execute(parameters);

                            var newNode = vm.Scratch.Nodes.LastOrDefault();
                            if (newNode is IImportExportNode importNode)
                            {
                                var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                                var payload = frame.Payload ?? frame.Params;
                                var rawJson = payload?.ToJsonString(jsonOptions) ?? "{}";
                                importNode.RawJsonData = rawJson;
                            }
                            e.Handled = true;
                        }
                    }
                }
            }
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.Scratch.FileSavePickerHandler = async () =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Save Scratch Project",
                        DefaultExtension = "scratch",
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("Scratch Files (*.scratch, *.json)")
                            {
                                Patterns = new[] { "*.scratch", "*.json" }
                            }
                        }
                    });
                    return file?.Path.LocalPath;
                }
                return null;
            };

            vm.Scratch.FileLoadPickerHandler = async () =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Load Scratch Project",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("Scratch Files (*.scratch, *.json)")
                            {
                                Patterns = new[] { "*.scratch", "*.json" }
                            }
                        }
                    });
                    if (files != null && files.Count > 0)
                    {
                        return files[0].Path.LocalPath;
                    }
                }
                return null;
            };

            vm.Scratch.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ScratchViewModel.SelectedNode))
                {
                    var selected = vm.Scratch.SelectedNode;
                    if (selected != null)
                    {
                        async Task<string?> ImportJsonAsync()
                        {
                            var topLevel = TopLevel.GetTopLevel(this);
                            if (topLevel != null)
                            {
                                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                                {
                                    Title = "Import Payload JSON",
                                    AllowMultiple = false,
                                    FileTypeFilter = new[]
                                    {
                                        new FilePickerFileType("JSON Files (*.json)")
                                        {
                                            Patterns = new[] { "*.json" }
                                        }
                                    }
                                });
                                if (files != null && files.Count > 0)
                                {
                                    return await File.ReadAllTextAsync(files[0].Path.LocalPath);
                                }
                            }
                            return null;
                        }

                        async Task ExportJsonAsync(string rawJson)
                        {
                            var topLevel = TopLevel.GetTopLevel(this);
                            if (topLevel != null)
                            {
                                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                                {
                                    Title = "Export Payload JSON",
                                    DefaultExtension = "json",
                                    FileTypeChoices = new[]
                                    {
                                        new FilePickerFileType("JSON Files (*.json)")
                                        {
                                            Patterns = new[] { "*.json" }
                                        }
                                    }
                                });
                                if (file != null)
                                {
                                    await File.WriteAllTextAsync(file.Path.LocalPath, rawJson);
                                }
                            }
                        }

                        if (selected is IImportExportNode importExportNode)
                        {
                            importExportNode.PayloadImportHandler = ImportJsonAsync;
                            importExportNode.PayloadExportHandler = () => ExportJsonAsync(importExportNode.RawJsonData);
                        }
                    }
                }
            };
        }
    }
}
