#nullable enable

using System.ComponentModel;
using Avalonia;

namespace CDP.Editor.Nodes.ViewModels;

public class ConnectionViewModel : NodeEditorViewModelBase
{
    private NodeViewModel? _fromNode;
    private NodeViewModel? _toNode;

    public NodeViewModel? FromNode
    {
        get => _fromNode;
        set
        {
            if (_fromNode != null)
            {
                _fromNode.PropertyChanged -= Node_PropertyChanged;
            }
            if (RaiseAndSetIfChanged(ref _fromNode, value))
            {
                if (_fromNode != null)
                {
                    _fromNode.PropertyChanged += Node_PropertyChanged;
                }
                UpdatePoints();
            }
        }
    }

    public NodeViewModel? ToNode
    {
        get => _toNode;
        set
        {
            if (_toNode != null)
            {
                _toNode.PropertyChanged -= Node_PropertyChanged;
            }
            if (RaiseAndSetIfChanged(ref _toNode, value))
            {
                if (_toNode != null)
                {
                    _toNode.PropertyChanged += Node_PropertyChanged;
                }
                UpdatePoints();
            }
        }
    }

    public Point StartPoint => FromNode != null
        ? new Point(FromNode.X + FromNode.Width, FromNode.Y + FromNode.Height / 2.0)
        : default;

    public Point EndPoint => ToNode != null
        ? new Point(ToNode.X, ToNode.Y + ToNode.Height / 2.0)
        : default;

    public Point ControlPoint1 => new Point(StartPoint.X + 80.0, StartPoint.Y);

    public Point ControlPoint2 => new Point(EndPoint.X - 80.0, EndPoint.Y);

    public ConnectionViewModel()
    {
    }

    public ConnectionViewModel(NodeViewModel fromNode, NodeViewModel toNode)
    {
        FromNode = fromNode;
        ToNode = toNode;
    }

    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeViewModel.X) ||
            e.PropertyName == nameof(NodeViewModel.Y) ||
            e.PropertyName == nameof(NodeViewModel.Width) ||
            e.PropertyName == nameof(NodeViewModel.Height))
        {
            UpdatePoints();
        }
    }

    private void UpdatePoints()
    {
        OnPropertyChanged(nameof(StartPoint));
        OnPropertyChanged(nameof(EndPoint));
        OnPropertyChanged(nameof(ControlPoint1));
        OnPropertyChanged(nameof(ControlPoint2));
    }
}
