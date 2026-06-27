#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace CDP.Editor.Nodes.ViewModels;

public class NodeEditorViewModel : NodeEditorViewModelBase
{
    private ObservableCollection<NodeViewModel> _nodes = new();
    private ObservableCollection<ConnectionViewModel> _connections = new();
    private double _zoom = 1.0;
    private double _panX;
    private double _panY;
    private bool _isDraggingConnection;

    public ObservableCollection<NodeViewModel> Nodes
    {
        get => _nodes;
        set => RaiseAndSetIfChanged(ref _nodes, value);
    }

    public ObservableCollection<ConnectionViewModel> Connections
    {
        get => _connections;
        set => RaiseAndSetIfChanged(ref _connections, value);
    }

    public double Zoom
    {
        get => _zoom;
        set => RaiseAndSetIfChanged(ref _zoom, value);
    }

    public double PanX
    {
        get => _panX;
        set => RaiseAndSetIfChanged(ref _panX, value);
    }

    public double PanY
    {
        get => _panY;
        set => RaiseAndSetIfChanged(ref _panY, value);
    }

    public bool IsDraggingConnection
    {
        get => _isDraggingConnection;
        set => RaiseAndSetIfChanged(ref _isDraggingConnection, value);
    }

    public Action? CollectionChangedAction { get; set; }
    public Action<NodeViewModel>? NodeSelectedAction { get; set; }
    public Func<NodeViewModel>? CreateNodeHandler { get; set; }
    public Action? AutoLayoutHandler { get; set; }
    public Action<NodeViewModel>? BringNodeIntoViewAction { get; set; }
    public Action<string, double, double>? DropNodeHandler { get; set; }

    public void BringNodeIntoView(NodeViewModel node)
    {
        BringNodeIntoViewAction?.Invoke(node);
    }

    public ICommand AddNodeCommand { get; }
    public ICommand CreateConnectionCommand { get; }
    public ICommand SelectNodeCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand DeleteNodeCommand { get; }
    public ICommand AutoLayoutCommand { get; }

    public NodeEditorViewModel()
    {
        AddNodeCommand = new RelayCommand(ExecuteAddNode);
        CreateConnectionCommand = new RelayCommand<object>(ExecuteCreateConnection);
        SelectNodeCommand = new RelayCommand<NodeViewModel>(ExecuteSelectNode);
        DeleteSelectedCommand = new RelayCommand(ExecuteDeleteSelected);
        DeleteNodeCommand = new RelayCommand<NodeViewModel>(node => { if (node != null) DeleteNode(node); });
        AutoLayoutCommand = new RelayCommand(ExecuteAutoLayout);

        _nodes.CollectionChanged += OnNodesCollectionChanged;
        _connections.CollectionChanged += OnConnectionsCollectionChanged;
    }

    private void OnNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (NodeViewModel node in e.OldItems)
            {
                node.PropertyChanged -= OnNodePropertyChanged;
            }
        }
        if (e.NewItems != null)
        {
            foreach (NodeViewModel node in e.NewItems)
            {
                node.PropertyChanged -= OnNodePropertyChanged;
                node.PropertyChanged += OnNodePropertyChanged;
            }
        }
        OnCollectionChanged();
    }

    private void OnConnectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnCollectionChanged();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is NodeViewModel node)
        {
            OnNodePropertyChanged(node, e.PropertyName);
        }
    }

    protected virtual void OnNodePropertyChanged(NodeViewModel node, string? propertyName)
    {
        CollectionChangedAction?.Invoke();
    }

    protected virtual void OnCollectionChanged()
    {
        CollectionChangedAction?.Invoke();
    }

    public NodeViewModel CreateNode(string name, double x, double y)
    {
        NodeViewModel node;
        if (CreateNodeHandler != null)
        {
            node = CreateNodeHandler();
            node.Name = name;
            node.X = x;
            node.Y = y;
        }
        else
        {
            node = new NodeViewModel
            {
                Name = name,
                X = x,
                Y = y
            };
        }
        Nodes.Add(node);
        return node;
    }

    public void ConnectNodes(NodeViewModel fromNode, NodeViewModel toNode)
    {
        if (fromNode == toNode) return;

        // Check if connection already exists
        if (Connections.Any(c => c.FromNode == fromNode && c.ToNode == toNode)) return;

        var connection = new ConnectionViewModel(fromNode, toNode);
        Connections.Add(connection);
    }

    public void DisconnectNodes(NodeViewModel fromNode, NodeViewModel toNode)
    {
        var conn = Connections.FirstOrDefault(c => c.FromNode == fromNode && c.ToNode == toNode);
        if (conn != null)
        {
            Connections.Remove(conn);
        }
    }

    public void SelectNode(NodeViewModel node, bool clearOthers = true)
    {
        if (clearOthers)
        {
            foreach (var n in Nodes)
            {
                if (n != node)
                {
                    n.IsSelected = false;
                }
            }
        }
        node.IsSelected = true;
        NodeSelectedAction?.Invoke(node);
    }

    public void DragNode(NodeViewModel node, double deltaX, double deltaY)
    {
        node.X += deltaX;
        node.Y += deltaY;
    }

    public void DeleteNode(NodeViewModel node)
    {
        Nodes.Remove(node);
        var toRemove = Connections.Where(c => c.FromNode == node || c.ToNode == node).ToList();
        foreach (var conn in toRemove)
        {
            Connections.Remove(conn);
        }
    }

    private void ExecuteAddNode()
    {
        if (CreateNodeHandler != null)
        {
            var node = CreateNodeHandler();
            node.X = 20 + Nodes.Count * 200;
            node.Y = 20;
            Nodes.Add(node);
        }
        else
        {
            var node = new NodeViewModel
            {
                Name = $"Node {Nodes.Count + 1}",
                X = 20 + Nodes.Count * 200,
                Y = 20
            };
            Nodes.Add(node);
        }
    }

    private void ExecuteCreateConnection(object? parameter)
    {
        if (parameter is Tuple<NodeViewModel, NodeViewModel> tuple)
        {
            ConnectNodes(tuple.Item1, tuple.Item2);
        }
        else if (parameter is object[] array && array.Length >= 2 &&
                 array[0] is NodeViewModel from &&
                 array[1] is NodeViewModel to)
        {
            ConnectNodes(from, to);
        }
    }

    private void ExecuteSelectNode(NodeViewModel? node)
    {
        if (node != null)
        {
            SelectNode(node, true);
        }
    }

    private void ExecuteDeleteSelected()
    {
        var selectedNodes = Nodes.Where(n => n.IsSelected).ToList();
        foreach (var node in selectedNodes)
        {
            DeleteNode(node);
        }
    }

    private void ExecuteAutoLayout()
    {
        if (AutoLayoutHandler != null)
        {
            AutoLayoutHandler();
        }
        else
        {
            // Default simple horizontal layout
            for (int i = 0; i < Nodes.Count; i++)
            {
                Nodes[i].X = 200.0 * i + 10.0;
                Nodes[i].Y = 20.0;
            }
        }
    }
}
