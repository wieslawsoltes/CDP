using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using CdpGalleryApp.ViewModels;

namespace CdpGalleryApp.Views;

public partial class NodeEditorPage : UserControl
{
    public static readonly DataFormat<string> NodeTypeFormat = DataFormat.CreateInProcessFormat<string>("NodeType");

    public NodeEditorPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var toolboxPanel = this.FindControl<StackPanel>("ToolboxPanel");
        if (toolboxPanel != null)
        {
            SetupToolboxDragHandlers(toolboxPanel);
        }

        var nodeEditor = this.FindControl<CDP.Editor.Nodes.Views.NodeEditorView>("LogicNodeEditor");
        if (nodeEditor != null)
        {
            DragDrop.SetAllowDrop(nodeEditor, true);
            nodeEditor.AddHandler(DragDrop.DragOverEvent, OnNodeEditorDragOver);
            nodeEditor.AddHandler(DragDrop.DropEvent, OnNodeEditorDrop);
        }
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

    private void OnNodeEditorDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Formats.Contains(NodeTypeFormat))
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
        var nodeEditor = this.FindControl<CDP.Editor.Nodes.Views.NodeEditorView>("LogicNodeEditor");
        if (nodeEditor != null && DataContext is NodeEditorPageViewModel vm)
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
                        var parameters = new NodeEditorPageViewModel.AddNodeParameters(nodeType, dropPos.X, dropPos.Y);
                        vm.AddLogicNodeCommand.Execute(parameters);
                        e.Handled = true;
                    }
                }
            }
        }
    }
}
