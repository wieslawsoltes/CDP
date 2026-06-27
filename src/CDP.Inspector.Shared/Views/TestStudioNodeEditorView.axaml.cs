#nullable enable

using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CDP.Editor.Nodes.Views;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Views;

public partial class TestStudioNodeEditorView : UserControl
{
    public TestStudioNodeEditorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var nodeEditor = this.FindControl<NodeEditorView>("NodeEditor");
        if (nodeEditor != null)
        {
            DragDrop.SetAllowDrop(nodeEditor, true);
            nodeEditor.AddHandler(DragDrop.DragOverEvent, OnNodeEditorDragOver);
            nodeEditor.AddHandler(DragDrop.DropEvent, OnNodeEditorDrop);

            nodeEditor.ContextMenu = ToolboxMenuHelper.CreateContextMenu(cmd =>
            {
                if (nodeEditor.DataContext is TestStudioNodeEditorViewModel vm)
                {
                    var pos = nodeEditor.LastRightClickCanvasPosition;
                    nodeEditor.Focus();
                    vm.CreateNode(cmd, cmd, "", "", pos.X, pos.Y);
                    vm.SyncToTestStudioAction?.Invoke();
                }
            });
        }
    }

    private void OnNodeEditorDragOver(object? sender, DragEventArgs e)
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

    private void OnNodeEditorDrop(object? sender, DragEventArgs e)
    {
        var nodeEditor = this.FindControl<NodeEditorView>("NodeEditor");
        if (nodeEditor != null && nodeEditor.DataContext is TestStudioNodeEditorViewModel vm)
        {
            var command = e.DataTransfer.TryGetValue(ToolboxMenuHelper.CdpCommandFormat);
            if (!string.IsNullOrEmpty(command))
            {
                var canvas = nodeEditor.EditorCanvas;
                if (canvas != null)
                {
                    var dropPos = e.GetPosition(canvas);
                    vm.CreateNode(command, command, "", "", dropPos.X, dropPos.Y);
                    vm.SyncToTestStudioAction?.Invoke();
                    e.Handled = true;
                }
            }
        }
    }
}
