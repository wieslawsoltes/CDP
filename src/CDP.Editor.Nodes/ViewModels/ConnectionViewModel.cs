#nullable enable

using System.ComponentModel;
using Avalonia;

namespace CDP.Editor.Nodes.ViewModels;

public class ConnectionViewModel : NodeEditorViewModelBase
{
    private NodeViewModel? _fromNode;
    private NodeViewModel? _toNode;
    private PinViewModel? _fromPin;
    private PinViewModel? _toPin;

    public PinViewModel? FromPin
    {
        get => _fromPin;
        set
        {
            if (RaiseAndSetIfChanged(ref _fromPin, value))
            {
                if (_fromPin != null)
                {
                    FromNode = _fromPin.Owner;
                }
                SubscribeFrom();
                UpdatePoints();
            }
        }
    }

    public PinViewModel? ToPin
    {
        get => _toPin;
        set
        {
            if (RaiseAndSetIfChanged(ref _toPin, value))
            {
                if (_toPin != null)
                {
                    ToNode = _toPin.Owner;
                }
                SubscribeTo();
                UpdatePoints();
            }
        }
    }

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

    public Point StartPoint
    {
        get
        {
            if (FromPin != null && FromPin.Owner != null)
            {
                return new Point(FromPin.Owner.X + FromPin.Owner.Width, FromPin.Owner.Y + FromPin.Top + 5.0);
            }
            if (FromNode != null)
            {
                return new Point(FromNode.X + FromNode.Width, FromNode.Y + FromNode.Height / 2.0);
            }
            return default;
        }
    }

    public Point EndPoint
    {
        get
        {
            if (ToPin != null && ToPin.Owner != null)
            {
                return new Point(ToPin.Owner.X, ToPin.Owner.Y + ToPin.Top + 5.0);
            }
            if (ToNode != null)
            {
                return new Point(ToNode.X, ToNode.Y + ToNode.Height / 2.0);
            }
            return default;
        }
    }

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

    public ConnectionViewModel(PinViewModel fromPin, PinViewModel toPin)
    {
        FromPin = fromPin;
        ToPin = toPin;
    }

    private NodeViewModel? _subscribedFromNode;
    private NodeViewModel? _subscribedToNode;
    private PinViewModel? _subscribedFromPin;
    private PinViewModel? _subscribedToPin;

    private void SubscribeFrom()
    {
        if (_subscribedFromPin != null)
        {
            _subscribedFromPin.PropertyChanged -= Pin_PropertyChanged;
        }
        if (_subscribedFromNode != null)
        {
            _subscribedFromNode.PropertyChanged -= Node_PropertyChanged;
        }

        _subscribedFromPin = _fromPin;
        _subscribedFromNode = _fromPin?.Owner;

        if (_subscribedFromPin != null)
        {
            _subscribedFromPin.PropertyChanged += Pin_PropertyChanged;
        }
        if (_subscribedFromNode != null)
        {
            _subscribedFromNode.PropertyChanged += Node_PropertyChanged;
        }
    }

    private void SubscribeTo()
    {
        if (_subscribedToPin != null)
        {
            _subscribedToPin.PropertyChanged -= Pin_PropertyChanged;
        }
        if (_subscribedToNode != null)
        {
            _subscribedToNode.PropertyChanged -= Node_PropertyChanged;
        }

        _subscribedToPin = _toPin;
        _subscribedToNode = _toPin?.Owner;

        if (_subscribedToPin != null)
        {
            _subscribedToPin.PropertyChanged += Pin_PropertyChanged;
        }
        if (_subscribedToNode != null)
        {
            _subscribedToNode.PropertyChanged += Node_PropertyChanged;
        }
    }

    private void Pin_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PinViewModel.Top))
        {
            UpdatePoints();
        }
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
