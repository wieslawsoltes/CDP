using System;
using CDP.Editor.Nodes.ViewModels;

namespace CdpGalleryApp.ViewModels;

public class NodeEditorPageViewModel : ViewModelBase
{
    private NodeEditorViewModel _nodeEditorVm;

    public NodeEditorViewModel NodeEditorVm
    {
        get => _nodeEditorVm;
        set => RaiseAndSetIfChanged(ref _nodeEditorVm, value);
    }

    public NodeEditorPageViewModel()
    {
        _nodeEditorVm = new NodeEditorViewModel();

        // 1. Add Source Node
        var node1 = new NodeViewModel
        {
            Name = "Source Node",
            X = 80,
            Y = 120,
            Width = 160,
            Height = 100
        };
        node1.Inputs.Add(new PinViewModel { Name = "Data In", Kind = PinKind.Input, Owner = node1, Index = 0 });
        node1.Outputs.Add(new PinViewModel { Name = "Data Out", Kind = PinKind.Output, Owner = node1, Index = 0 });
        _nodeEditorVm.Nodes.Add(node1);

        // 2. Add Process Node
        var node2 = new NodeViewModel
        {
            Name = "Process Node",
            X = 320,
            Y = 180,
            Width = 160,
            Height = 100
        };
        node2.Inputs.Add(new PinViewModel { Name = "Input A", Kind = PinKind.Input, Owner = node2, Index = 0 });
        node2.Outputs.Add(new PinViewModel { Name = "Result Out", Kind = PinKind.Output, Owner = node2, Index = 0 });
        _nodeEditorVm.Nodes.Add(node2);

        // 3. Connect them via a Bezier connection
        var connection = new ConnectionViewModel
        {
            FromPin = node1.Outputs[0],
            ToPin = node2.Inputs[0]
        };
        _nodeEditorVm.Connections.Add(connection);
    }
}
