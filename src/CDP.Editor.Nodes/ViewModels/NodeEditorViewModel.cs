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
    public Action<NodeViewModel>? NodeDoubleClickedAction { get; set; }

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
        if (node is GroupNodeViewModel groupNode)
        {
            groupNode.X += deltaX;
            groupNode.Y += deltaY;

            foreach (var childId in groupNode.ChildNodeIds)
            {
                var child = Nodes.FirstOrDefault(n => n.Id == childId);
                if (child != null && child != groupNode)
                {
                    child.X += deltaX;
                    child.Y += deltaY;
                }
            }
        }
        else if (node.IsSelected)
        {
            foreach (var n in Nodes.Where(n => n.IsSelected))
            {
                n.X += deltaX;
                n.Y += deltaY;
            }
        }
        else
        {
            node.X += deltaX;
            node.Y += deltaY;
        }
    }

    public void DeleteNode(NodeViewModel node)
    {
        Nodes.Remove(node);
        var toRemove = Connections.Where(c => c.FromNode == node || c.ToNode == node).ToList();
        foreach (var conn in toRemove)
        {
            Connections.Remove(conn);
        }

        foreach (var gNode in Nodes.OfType<GroupNodeViewModel>())
        {
            gNode.ChildNodeIds.Remove(node.Id);
        }
    }

    private static List<CopiedNodeData> s_copiedNodes = new();
    private static List<Tuple<string, string>> s_copiedConnections = new();

    private class CopiedNodeData
    {
        public string Name { get; set; } = "";
        public string Action { get; set; } = "";
        public string Selector { get; set; } = "";
        public string Value { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public string Id { get; set; } = "";
        public bool IsGroup { get; set; }
        public string? ScenarioPath { get; set; }
        public List<string> ChildNodeIds { get; } = new();
    }

    public void CopySelectedNodes()
    {
        s_copiedNodes.Clear();
        s_copiedConnections.Clear();

        var selected = Nodes.Where(n => n.IsSelected).ToList();
        if (selected.Count == 0) return;

        foreach (var n in selected)
        {
            var data = new CopiedNodeData
            {
                Name = n.Name,
                X = n.X,
                Y = n.Y,
                Id = n.Id,
                ScenarioPath = n.ScenarioPath
            };

            var nodeType = n.GetType();
            if (n is GroupNodeViewModel gNode)
            {
                data.IsGroup = true;
                data.ChildNodeIds.AddRange(gNode.ChildNodeIds);
            }
            else
            {
                var actionProp = nodeType.GetProperty("Action");
                var selectorProp = nodeType.GetProperty("Selector");
                var valueProp = nodeType.GetProperty("Value");

                if (actionProp != null) data.Action = actionProp.GetValue(n)?.ToString() ?? "";
                if (selectorProp != null) data.Selector = selectorProp.GetValue(n)?.ToString() ?? "";
                if (valueProp != null) data.Value = valueProp.GetValue(n)?.ToString() ?? "";
            }

            s_copiedNodes.Add(data);
        }

        var selectedIds = new HashSet<string>(selected.Select(n => n.Id));
        foreach (var c in Connections)
        {
            if (c.FromNode != null && c.ToNode != null &&
                selectedIds.Contains(c.FromNode.Id) && selectedIds.Contains(c.ToNode.Id))
            {
                s_copiedConnections.Add(Tuple.Create(c.FromNode.Id, c.ToNode.Id));
            }
        }
    }

    public void PasteNodes()
    {
        if (s_copiedNodes.Count == 0) return;

        foreach (var n in Nodes)
        {
            n.IsSelected = false;
        }

        var idMap = new Dictionary<string, NodeViewModel>();
        var pastedGroups = new List<GroupNodeViewModel>();
        var pastedNodes = new List<NodeViewModel>();

        foreach (var data in s_copiedNodes)
        {
            NodeViewModel newNode;
            if (data.IsGroup)
            {
                var gNode = new GroupNodeViewModel
                {
                    Name = data.Name,
                    X = data.X + 40,
                    Y = data.Y + 40,
                    IsSelected = true,
                    ScenarioPath = data.ScenarioPath
                };
                newNode = gNode;
                pastedGroups.Add(gNode);
            }
            else
            {
                if (CreateNodeHandler != null)
                {
                    newNode = CreateNodeHandler();
                    newNode.Name = data.Name;
                    newNode.X = data.X + 40;
                    newNode.Y = data.Y + 40;
                    newNode.ScenarioPath = data.ScenarioPath;

                    var nodeType = newNode.GetType();
                    var actionProp = nodeType.GetProperty("Action");
                    var selectorProp = nodeType.GetProperty("Selector");
                    var valueProp = nodeType.GetProperty("Value");

                    if (actionProp != null) actionProp.SetValue(newNode, data.Action);
                    if (selectorProp != null) selectorProp.SetValue(newNode, data.Selector);
                    if (valueProp != null) valueProp.SetValue(newNode, data.Value);
                }
                else
                {
                    newNode = new NodeViewModel
                    {
                        Name = data.Name,
                        X = data.X + 40,
                        Y = data.Y + 40,
                        ScenarioPath = data.ScenarioPath
                    };
                }
                newNode.IsSelected = true;
                pastedNodes.Add(newNode);
            }

            idMap[data.Id] = newNode;
        }

        foreach (var data in s_copiedNodes.Where(d => d.IsGroup))
        {
            var pastedGroup = (GroupNodeViewModel)idMap[data.Id];
            foreach (var childId in data.ChildNodeIds)
            {
                if (idMap.TryGetValue(childId, out var pastedChild))
                {
                    pastedGroup.ChildNodeIds.Add(pastedChild.Id);
                }
            }
        }

        foreach (var gNode in pastedGroups)
        {
            Nodes.Insert(0, gNode);
        }
        foreach (var tNode in pastedNodes)
        {
            Nodes.Add(tNode);
        }

        foreach (var conn in s_copiedConnections)
        {
            if (idMap.TryGetValue(conn.Item1, out var fromNode) &&
                idMap.TryGetValue(conn.Item2, out var toNode))
            {
                ConnectNodes(fromNode, toNode);
            }
        }

        OnCollectionChanged();
    }

    public void GroupSelectedNodes()
    {
        var selected = Nodes.Where(n => n.IsSelected && n is not GroupNodeViewModel).ToList();
        if (selected.Count == 0) return;

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        foreach (var n in selected)
        {
            minX = Math.Min(minX, n.X);
            minY = Math.Min(minY, n.Y);
            maxX = Math.Max(maxX, n.X + n.Width);
            maxY = Math.Max(maxY, n.Y + n.Height);
        }

        var groupNode = new GroupNodeViewModel
        {
            Name = "New Group",
            X = minX - 20,
            Y = minY - 40,
            Width = (maxX - minX) + 40,
            Height = (maxY - minY) + 60,
            IsSelected = true
        };

        foreach (var n in selected)
        {
            groupNode.ChildNodeIds.Add(n.Id);
            n.IsSelected = false;
        }

        Nodes.Insert(0, groupNode);
        OnCollectionChanged();
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
