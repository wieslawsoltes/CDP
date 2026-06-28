#nullable enable

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Controls.Primitives;
using CDP.Editor.Nodes.ViewModels;

namespace CDP.Editor.Nodes.Views;

public partial class NodeEditorView : UserControl
{
    private Border? _canvasContainer;
    private Canvas? _nodeCanvas;
    private Avalonia.Controls.Shapes.Path? _previewConnectionPath;
    private PathFigure? _previewFigure;
    private BezierSegment? _previewSegment;
    private Button? _btnZoomIn;
    private Button? _btnZoomOut;
    private Button? _btnZoomToFit;

    // Dragging state
    private bool _isDraggingNode;
    private NodeViewModel? _draggedNode;
    private Point _lastPointerPosition;

    private bool _isPanning;
    private double _initialPanX;
    private double _initialPanY;
    private Point _initialPanPointerPosition;

    // Connection dragging state
    private bool _isDraggingConnection;
    private NodeViewModel? _connectionSourceNode;
    private bool _isReverseDrag;
    private NodeViewModel? _highlightedSnapNode;

    // Selection box state
    private Rectangle? _selectionBox;
    private bool _isSelecting;
    private Point _selectionStartCanvas;

    // Resizing state
    private bool _isResizingNode;
    private NodeViewModel? _resizedNode;
    private Point _resizeStartPointerPosition;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    public static readonly StyledProperty<object?> HeaderContentProperty =
        AvaloniaProperty.Register<NodeEditorView, object?>(nameof(HeaderContent));

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public Point LastRightClickCanvasPosition { get; private set; }

    public Canvas? EditorCanvas => _nodeCanvas;

    public NodeEditorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _canvasContainer = this.FindControl<Border>("CanvasContainer");
        _nodeCanvas = this.FindControl<Canvas>("NodeCanvas");
        _previewConnectionPath = this.FindControl<Avalonia.Controls.Shapes.Path>("PreviewConnectionPath");
        if (_previewConnectionPath != null && _previewConnectionPath.Data is PathGeometry pg && pg.Figures != null)
        {
            _previewFigure = pg.Figures.FirstOrDefault();
            if (_previewFigure != null && _previewFigure.Segments != null)
            {
                _previewSegment = _previewFigure.Segments.FirstOrDefault() as BezierSegment;
            }
        }
        _btnZoomIn = this.FindControl<Button>("btnZoomIn");
        _btnZoomOut = this.FindControl<Button>("btnZoomOut");
        _btnZoomToFit = this.FindControl<Button>("btnZoomToFit");
        _selectionBox = this.FindControl<Rectangle>("SelectionBox");

        if (_canvasContainer != null)
        {
            _canvasContainer.PointerPressed += OnPointerPressed;
            _canvasContainer.PointerMoved += OnPointerMoved;
            _canvasContainer.PointerReleased += OnPointerReleased;
            _canvasContainer.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
        }

        if (_btnZoomIn != null)
        {
            _btnZoomIn.Click += (s, e) => AdjustZoom(1.2);
        }
        if (_btnZoomOut != null)
        {
            _btnZoomOut.Click += (s, e) => AdjustZoom(0.8);
        }
        if (_btnZoomToFit != null)
        {
            _btnZoomToFit.Click += (s, e) => ZoomToFit();
        }

        var btnAutoLayoutOptions = this.FindControl<Button>("btnAutoLayoutOptions");
        var autoLayoutPopup = this.FindControl<Popup>("AutoLayoutPopup");
        var btnRunLayout = this.FindControl<Button>("btnRunLayout");

        if (btnAutoLayoutOptions != null && autoLayoutPopup != null)
        {
            btnAutoLayoutOptions.Click += (s, e) =>
            {
                autoLayoutPopup.IsOpen = !autoLayoutPopup.IsOpen;
            };
        }

        if (btnRunLayout != null && autoLayoutPopup != null)
        {
            btnRunLayout.Click += (s, e) =>
            {
                autoLayoutPopup.IsOpen = false;
            };
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is NodeEditorViewModel vm)
        {
            vm.BringNodeIntoViewAction = BringNodeIntoView;
            vm.LayoutAppliedAction = ZoomToFit;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var topLevel = TopLevel.GetTopLevel(this);
        var focused = topLevel?.FocusManager?.GetFocusedElement();
        if (focused is Control ctrl && IsInputControl(ctrl))
        {
            return;
        }

        if (DataContext is not NodeEditorViewModel vm)
            return;

        var isCtrlOrCmd = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (isCtrlOrCmd)
        {
            switch (e.Key)
            {
                case Key.C:
                    vm.CopySelectedNodes();
                    e.Handled = true;
                    break;
                case Key.V:
                    if (!vm.IsReadOnly) vm.PasteNodes();
                    e.Handled = true;
                    break;
                case Key.A:
                    foreach (var n in vm.Nodes)
                    {
                        n.IsSelected = true;
                    }
                    e.Handled = true;
                    break;
                case Key.G:
                    if (!vm.IsReadOnly) vm.GroupSelectedNodes();
                    e.Handled = true;
                    break;
                case Key.D0:
                case Key.NumPad0:
                    ZoomToFit();
                    e.Handled = true;
                    break;
                case Key.OemPlus:
                case Key.Add:
                    AdjustZoom(1.2);
                    e.Handled = true;
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    AdjustZoom(0.8);
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.Delete:
                case Key.Back:
                    if (!vm.IsReadOnly) vm.DeleteSelectedCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }

    public void BringNodeIntoView(NodeViewModel node)
    {
        if (node == null || _canvasContainer == null) return;

        double viewportWidth = _canvasContainer.Bounds.Width;
        double viewportHeight = _canvasContainer.Bounds.Height;

        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        if (DataContext is NodeEditorViewModel vm)
        {
            double zoom = vm.Zoom;
            double nodeCenterX = node.X + node.Width / 2;
            double nodeCenterY = node.Y + node.Height / 2;

            double targetPanX = (viewportWidth / 2) - (nodeCenterX * zoom);
            double targetPanY = (viewportHeight / 2) - (nodeCenterY * zoom);

            vm.PanX = targetPanX;
            vm.PanY = targetPanY;
        }
    }

    private void AdjustZoom(double factor)
    {
        if (DataContext is NodeEditorViewModel vm && _canvasContainer != null)
        {
            double oldZoom = vm.Zoom;
            double newZoom = Math.Clamp(oldZoom * factor, 0.05, 10.0);
            
            if (Math.Abs(newZoom - oldZoom) > 0.0001)
            {
                // Zoom relative to the center of the viewport
                double viewportWidth = _canvasContainer.Bounds.Width;
                double viewportHeight = _canvasContainer.Bounds.Height;
                Point center = new Point(viewportWidth / 2.0, viewportHeight / 2.0);

                double newPanX = center.X - (center.X - vm.PanX) * (newZoom / oldZoom);
                double newPanY = center.Y - (center.Y - vm.PanY) * (newZoom / oldZoom);

                vm.Zoom = newZoom;
                vm.PanX = newPanX;
                vm.PanY = newPanY;
            }
        }
    }

    private void ZoomToFit()
    {
        if (DataContext is NodeEditorViewModel vm && _canvasContainer != null)
        {
            if (vm.Nodes.Count == 0)
            {
                vm.Zoom = 1.0;
                vm.PanX = 0;
                vm.PanY = 0;
                return;
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (var node in vm.Nodes)
            {
                minX = Math.Min(minX, node.X);
                minY = Math.Min(minY, node.Y);
                maxX = Math.Max(maxX, node.X + node.Width);
                maxY = Math.Max(maxY, node.Y + node.Height);
            }

            double nodesWidth = maxX - minX;
            double nodesHeight = maxY - minY;

            if (nodesWidth <= 0 || nodesHeight <= 0) return;

            double viewportWidth = _canvasContainer.Bounds.Width;
            double viewportHeight = _canvasContainer.Bounds.Height;

            if (viewportWidth <= 0 || viewportHeight <= 0) return;

            double padding = 40.0;
            double targetWidth = nodesWidth + padding * 2.0;
            double targetHeight = nodesHeight + padding * 2.0;

            double zoomX = viewportWidth / targetWidth;
            double zoomY = viewportHeight / targetHeight;
            double targetZoom = Math.Min(zoomX, zoomY);

            targetZoom = Math.Clamp(targetZoom, 0.05, 10.0);

            double nodesCenterX = minX + nodesWidth / 2.0;
            double nodesCenterY = minY + nodesHeight / 2.0;

            double targetPanX = (viewportWidth / 2.0) - (nodesCenterX * targetZoom);
            double targetPanY = (viewportHeight / 2.0) - (nodesCenterY * targetZoom);

            vm.Zoom = targetZoom;
            vm.PanX = targetPanX;
            vm.PanY = targetPanY;
        }
    }

    private bool IsInputControl(Control? control)
    {
        while (control != null)
        {
            if (control is TextBox || 
                control is Button || 
                control is ComboBox || 
                control.GetType().Name == "AutoCompleteBox" ||
                control.Name == "InputPin" ||
                control.Name == "OutputPin" ||
                control.Classes.Contains("input-pin") ||
                control.Classes.Contains("output-pin"))
            {
                return true;
            }
            control = control.Parent as Control;
        }
        return false;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not NodeEditorViewModel vm || _nodeCanvas == null || _canvasContainer == null)
            return;

        var properties = e.GetCurrentPoint(_canvasContainer).Properties;
        if (properties.IsRightButtonPressed)
        {
            LastRightClickCanvasPosition = e.GetPosition(_nodeCanvas);
        }

        // Check for double click
        if (e.ClickCount == 2)
        {
            NodeViewModel? clickedNode = null;
            var visual = e.Source as Visual;
            while (visual != null && visual != _nodeCanvas)
            {
                if (visual.DataContext is NodeViewModel node)
                {
                    clickedNode = node;
                    break;
                }
                visual = visual.GetVisualParent();
            }

            if (clickedNode != null)
            {
                vm.NodeDoubleClickedAction?.Invoke(clickedNode);
                e.Handled = true;
                return;
            }
        }

        var sourceElement = e.Source as Control;

        // 1. Connection dragging
        bool isOutputPin = sourceElement != null && (sourceElement.Name == "OutputPin" || sourceElement.Classes.Contains("output-pin"));
        bool isInputPin = sourceElement != null && (sourceElement.Name == "InputPin" || sourceElement.Classes.Contains("input-pin"));
        
        if (isOutputPin || isInputPin)
        {
            if (vm.IsReadOnly)
            {
                e.Handled = true;
                return;
            }
            if (sourceElement?.DataContext is NodeViewModel nodeVM)
            {
                _isReverseDrag = isInputPin;
                _highlightedSnapNode = null;

                if (isInputPin)
                {
                    var existingConn = vm.Connections.FirstOrDefault(c => c.ToNode == nodeVM);
                    if (existingConn != null)
                    {
                        // Unplug existing connection and drag from its source output pin instead!
                        _connectionSourceNode = existingConn.FromNode;
                        _isReverseDrag = false;
                        vm.Connections.Remove(existingConn);
                    }
                    else
                    {
                        _connectionSourceNode = nodeVM;
                    }
                }
                else
                {
                    _connectionSourceNode = nodeVM;
                }

                if (_connectionSourceNode != null)
                {
                    _isDraggingConnection = true;
                    vm.IsDraggingConnection = true;

                    e.Pointer.Capture(_canvasContainer);

                    if (_previewConnectionPath != null && _previewFigure != null && _previewSegment != null)
                    {
                        Point startPoint;
                        if (_isReverseDrag)
                        {
                            startPoint = new Point(_connectionSourceNode.X, _connectionSourceNode.Y + _connectionSourceNode.Height / 2.0);
                        }
                        else
                        {
                            startPoint = new Point(_connectionSourceNode.X + _connectionSourceNode.Width, _connectionSourceNode.Y + _connectionSourceNode.Height / 2.0);
                        }

                        _previewFigure.StartPoint = startPoint;
                        _previewSegment.Point1 = new Point(startPoint.X + (_isReverseDrag ? -80.0 : 80.0), startPoint.Y);
                        _previewSegment.Point2 = new Point(startPoint.X + (_isReverseDrag ? -80.0 : 80.0), startPoint.Y);
                        _previewSegment.Point3 = startPoint;
                        _previewConnectionPath.IsVisible = true;
                    }

                    e.Handled = true;
                    return;
                }
            }
        }

        // Check if pressed on a node card
        NodeViewModel? pressedNode = null;
        var visualElement = e.Source as Visual;
        while (visualElement != null && visualElement != _nodeCanvas)
        {
            if (visualElement.DataContext is NodeViewModel node)
            {
                pressedNode = node;
                break;
            }
            visualElement = visualElement.GetVisualParent();
        }

        // Check if pressed on a resize grip
        bool isResizeGrip = false;
        var checkElement = sourceElement as Visual;
        while (checkElement != null && checkElement != _nodeCanvas)
        {
            if (checkElement is Control ctrl && ctrl.Name == "ResizeGrip")
            {
                isResizeGrip = true;
                break;
            }
            checkElement = checkElement.GetVisualParent();
        }

        if (isResizeGrip && pressedNode != null)
        {
            if (vm.IsReadOnly)
            {
                e.Handled = true;
                return;
            }
            _isResizingNode = true;
            _resizedNode = pressedNode;
            _resizeStartPointerPosition = e.GetPosition(_canvasContainer);
            _resizeStartWidth = pressedNode.Width;
            _resizeStartHeight = pressedNode.Height;
            e.Pointer.Capture(_canvasContainer);
            e.Handled = true;
            return;
        }

        // 2. Node Selection & Dragging
        if (pressedNode != null)
        {
            // Let text input, buttons, and other controls handle their own events instead of dragging/panning
            if (IsInputControl(sourceElement))
            {
                return;
            }

            this.Focus();

            bool clearOthers = !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                pressedNode.IsSelected = !pressedNode.IsSelected;
                if (pressedNode.IsSelected)
                {
                    vm.NodeSelectedAction?.Invoke(pressedNode);
                }
            }
            else
            {
                vm.SelectNode(pressedNode, clearOthers);
            }

            if (!vm.IsReadOnly)
            {
                _isDraggingNode = true;
                _draggedNode = pressedNode;
                _lastPointerPosition = e.GetPosition(_canvasContainer);

                e.Pointer.Capture(_canvasContainer);
            }
            e.Handled = true;
            return;
        }

        // Focus UserControl when clicking empty background space
        this.Focus();

        // 3. Canvas Panning (Middle click or Alt + Left click on empty space)
        bool isPanDrag = properties.IsMiddleButtonPressed || (properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt));
        if (isPanDrag)
        {
            _isPanning = true;
            _initialPanX = vm.PanX;
            _initialPanY = vm.PanY;
            _initialPanPointerPosition = e.GetPosition(_canvasContainer);

            e.Pointer.Capture(_canvasContainer);
            e.Handled = true;
        }
        else if (properties.IsLeftButtonPressed)
        {
            // 4. Rubber-band Selection Box
            _isSelecting = true;
            _selectionStartCanvas = e.GetPosition(_nodeCanvas);

            if (_selectionBox != null)
            {
                _selectionBox.IsVisible = true;
                _selectionBox.Width = 0;
                _selectionBox.Height = 0;
                Canvas.SetLeft(_selectionBox, _selectionStartCanvas.X);
                Canvas.SetTop(_selectionBox, _selectionStartCanvas.Y);
            }

            // Deselect all on clicking empty canvas if not holding modifier keys
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                foreach (var n in vm.Nodes)
                {
                    n.IsSelected = false;
                }
            }

            e.Pointer.Capture(_canvasContainer);
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not NodeEditorViewModel vm || _nodeCanvas == null || _canvasContainer == null)
            return;

        // 1. Connection dragging preview
        if (_isDraggingConnection && _connectionSourceNode != null && _previewSegment != null && _previewFigure != null)
        {
            var currentPosCanvas = e.GetPosition(_nodeCanvas);
            NodeViewModel? targetNode = null;

            foreach (var node in vm.Nodes)
            {
                if (node == _connectionSourceNode) continue;
                if (node is GroupNodeViewModel) continue;

                double pinX, pinY;
                if (_isReverseDrag)
                {
                    pinX = node.X + node.Width;
                    pinY = node.Y + node.Height / 2.0;
                }
                else
                {
                    pinX = node.X;
                    pinY = node.Y + node.Height / 2.0;
                }

                double dx = currentPosCanvas.X - pinX;
                double dy = currentPosCanvas.Y - pinY;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < 25.0)
                {
                    targetNode = node;
                    break;
                }
            }

            if (targetNode != _highlightedSnapNode)
            {
                if (_highlightedSnapNode != null)
                {
                    _highlightedSnapNode.IsConnectionTarget = false;
                }
                _highlightedSnapNode = targetNode;
                if (_highlightedSnapNode != null)
                {
                    _highlightedSnapNode.IsConnectionTarget = true;
                }
            }

            Point endpoint;
            if (_highlightedSnapNode != null)
            {
                if (_isReverseDrag)
                {
                    endpoint = new Point(_highlightedSnapNode.X + _highlightedSnapNode.Width, _highlightedSnapNode.Y + _highlightedSnapNode.Height / 2.0);
                }
                else
                {
                    endpoint = new Point(_highlightedSnapNode.X, _highlightedSnapNode.Y + _highlightedSnapNode.Height / 2.0);
                }
            }
            else
            {
                endpoint = currentPosCanvas;
            }

            _previewSegment.Point3 = endpoint;
            var startPoint = _previewFigure.StartPoint;

            if (_isReverseDrag)
            {
                _previewSegment.Point1 = new Point(startPoint.X - 80.0, startPoint.Y);
                _previewSegment.Point2 = new Point(endpoint.X + 80.0, endpoint.Y);
            }
            else
            {
                _previewSegment.Point1 = new Point(startPoint.X + 80.0, startPoint.Y);
                _previewSegment.Point2 = new Point(endpoint.X - 80.0, endpoint.Y);
            }

            e.Handled = true;
            return;
        }

        // 1.5. Resizing node
        if (_isResizingNode && _resizedNode != null)
        {
            var currentPos = e.GetPosition(_canvasContainer);
            double deltaX = (currentPos.X - _resizeStartPointerPosition.X) / vm.Zoom;
            double deltaY = (currentPos.Y - _resizeStartPointerPosition.Y) / vm.Zoom;

            _resizedNode.Width = Math.Max(100, _resizeStartWidth + deltaX);
            _resizedNode.Height = Math.Max(50, _resizeStartHeight + deltaY);

            e.Handled = true;
            return;
        }

        // 2. Node dragging
        if (_isDraggingNode && _draggedNode != null)
        {
            var currentPos = e.GetPosition(_canvasContainer);
            double deltaX = (currentPos.X - _lastPointerPosition.X) / vm.Zoom;
            double deltaY = (currentPos.Y - _lastPointerPosition.Y) / vm.Zoom;

            vm.DragNode(_draggedNode, deltaX, deltaY);
            _lastPointerPosition = currentPos;

            e.Handled = true;
            return;
        }

        // 3. Canvas panning
        if (_isPanning)
        {
            var currentPos = e.GetPosition(_canvasContainer);
            double deltaX = currentPos.X - _initialPanPointerPosition.X;
            double deltaY = currentPos.Y - _initialPanPointerPosition.Y;

            vm.PanX = _initialPanX + deltaX;
            vm.PanY = _initialPanY + deltaY;

            e.Handled = true;
            return;
        }

        // 4. Selection box dragging (rubber-band selection)
        if (_isSelecting && _selectionBox != null && _nodeCanvas != null)
        {
            var currentPosCanvas = e.GetPosition(_nodeCanvas);
            double x = Math.Min(_selectionStartCanvas.X, currentPosCanvas.X);
            double y = Math.Min(_selectionStartCanvas.Y, currentPosCanvas.Y);
            double width = Math.Abs(_selectionStartCanvas.X - currentPosCanvas.X);
            double height = Math.Abs(_selectionStartCanvas.Y - currentPosCanvas.Y);

            Canvas.SetLeft(_selectionBox, x);
            Canvas.SetTop(_selectionBox, y);
            _selectionBox.Width = width;
            _selectionBox.Height = height;

            var selectionRect = new Rect(x, y, width, height);
            bool isModifier = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control);

            foreach (var node in vm.Nodes)
            {
                var nodeRect = new Rect(node.X, node.Y, node.Width, node.Height);
                if (selectionRect.Intersects(nodeRect))
                {
                    node.IsSelected = true;
                }
                else if (!isModifier)
                {
                    node.IsSelected = false;
                }
            }

            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);

        if (DataContext is not NodeEditorViewModel vm || _nodeCanvas == null)
            return;

        // 0.5. Finish resizing
        if (_isResizingNode)
        {
            _isResizingNode = false;
            _resizedNode = null;
            e.Handled = true;
            return;
        }

        // 1. Finish connection dragging
        if (_isDraggingConnection && _connectionSourceNode != null)
        {
            if (_previewConnectionPath != null)
            {
                _previewConnectionPath.IsVisible = false;
            }

            if (_highlightedSnapNode != null)
            {
                _highlightedSnapNode.IsConnectionTarget = false;

                if (_isReverseDrag)
                {
                    vm.ConnectNodes(_highlightedSnapNode, _connectionSourceNode);
                }
                else
                {
                    vm.ConnectNodes(_connectionSourceNode, _highlightedSnapNode);
                }
            }

            _isDraggingConnection = false;
            vm.IsDraggingConnection = false;
            _connectionSourceNode = null;
            _highlightedSnapNode = null;
            _isReverseDrag = false;
            e.Handled = true;
            return;
        }

        // 2. Finish node dragging
        if (_isDraggingNode)
        {
            _isDraggingNode = false;
            _draggedNode = null;
            e.Handled = true;
            return;
        }

        // 3. Finish canvas panning
        if (_isPanning)
        {
            _isPanning = false;
            e.Handled = true;
            return;
        }

        // 4. Finish selection box dragging
        if (_isSelecting)
        {
            _isSelecting = false;
            if (_selectionBox != null)
            {
                double left = Canvas.GetLeft(_selectionBox);
                double top = Canvas.GetTop(_selectionBox);
                double width = _selectionBox.Width;
                double height = _selectionBox.Height;
                double right = left + width;
                double bottom = top + height;
                var rect = new Rect(new Point(left, top), new Point(right, bottom));

                bool isAdding = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift);

                if (width < 3.0 && height < 3.0)
                {
                    // Simple click on empty space
                    if (!isAdding)
                    {
                        foreach (var n in vm.Nodes)
                        {
                            n.IsSelected = false;
                        }
                    }
                }
                else
                {
                    // Rubber-band selection
                    if (!isAdding)
                    {
                        foreach (var n in vm.Nodes)
                        {
                            n.IsSelected = false;
                        }
                    }
                    foreach (var n in vm.Nodes)
                    {
                        var nodeRect = new Rect(n.X, n.Y, n.Width, n.Height);
                        if (rect.Intersects(nodeRect))
                        {
                            n.IsSelected = true;
                        }
                    }
                }

                _selectionBox.IsVisible = false;
            }
            e.Handled = true;
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not NodeEditorViewModel vm || _nodeCanvas == null || _canvasContainer == null)
            return;

        double zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
        double oldZoom = vm.Zoom;
        double newZoom = Math.Clamp(oldZoom * zoomFactor, 0.05, 10.0);

        if (Math.Abs(newZoom - oldZoom) > 0.0001)
        {
            // Zoom relative to mouse pointer position
            var mousePos = e.GetPosition(_canvasContainer);
            double newPanX = mousePos.X - (mousePos.X - vm.PanX) * (newZoom / oldZoom);
            double newPanY = mousePos.Y - (mousePos.Y - vm.PanY) * (newZoom / oldZoom);

            vm.Zoom = newZoom;
            vm.PanX = newPanX;
            vm.PanY = newPanY;
        }

        e.Handled = true;
    }
}
