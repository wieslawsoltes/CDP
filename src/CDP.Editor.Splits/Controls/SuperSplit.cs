using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CDP.Editor.Splits.Models;
using Avalonia.VisualTree;

namespace CDP.Editor.Splits.Controls;

public class BoxMenuEventArgs : EventArgs
{
    public BoxNode Node { get; }
    public Control AnchorControl { get; }

    public BoxMenuEventArgs(BoxNode node, Control anchorControl)
    {
        Node = node;
        AnchorControl = anchorControl;
    }
}

public class FlatSplitter : Control
{
    public SplitContainerNode ContainerNode { get; }
    public IBrush Background { get; set; } = Brush.Parse("#3c4043");

    private bool _isHovered;
    private bool _isPressed;

    public FlatSplitter(SplitContainerNode node)
    {
        ContainerNode = node;
        Cursor = new Cursor(node.Orientation == Orientation.Horizontal
            ? StandardCursorType.SizeWestEast
            : StandardCursorType.SizeNorthSouth);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isHovered = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isHovered = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPressed = true;
            e.Pointer.Capture(this);
            var panel = Parent as FlatSplitPanel;
            if (panel != null)
            {
                panel.StartSplitterDrag(this, e);
            }
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var panel = Parent as FlatSplitPanel;
        if (panel != null && panel.ActiveSplitter == this)
        {
            panel.UpdateSplitterDrag(e);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isPressed = false;
        var panel = Parent as FlatSplitPanel;
        if (panel != null && panel.ActiveSplitter == this)
        {
            e.Pointer.Capture(null);
            panel.EndSplitterDrag();
            e.Handled = true;
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Fill background to match window background `#292a2d` so we don't have a transparent gap
        context.FillRectangle(Brush.Parse("#292a2d"), new Rect(0, 0, Bounds.Width, Bounds.Height));

        // Draw the divider line in the center
        IBrush lineBrush = _isPressed 
            ? Brush.Parse("#1a73e8") 
            : (_isHovered ? Brush.Parse("#8ab4f8") : Background);

        double thickness = _isHovered || _isPressed ? 3.0 : 1.5;

        if (ContainerNode.Orientation == Orientation.Horizontal)
        {
            double x = (Bounds.Width - thickness) / 2;
            context.FillRectangle(lineBrush, new Rect(x, 0, thickness, Bounds.Height));
        }
        else
        {
            double y = (Bounds.Height - thickness) / 2;
            context.FillRectangle(lineBrush, new Rect(0, y, Bounds.Width, thickness));
        }
    }
}

public class FlatSplitPanel : Panel
{
    private readonly SuperSplit _parentSplit;
    private readonly Dictionary<SplitNode, Rect> _nodeBounds = new();
    private readonly Dictionary<SplitContainerNode, Rect> _containerBounds = new();

    public FlatSplitter? ActiveSplitter { get; private set; }
    private Point _splitterDragStartPoint;
    private double _splitterDragStartRatio;

    public FlatSplitPanel(SuperSplit parentSplit)
    {
        _parentSplit = parentSplit;
    }

    public Dictionary<SplitNode, Rect> NodeBounds => _nodeBounds;

    public void StartSplitterDrag(FlatSplitter splitter, PointerPressedEventArgs e)
    {
        ActiveSplitter = splitter;
        _splitterDragStartPoint = e.GetPosition(this);
        _splitterDragStartRatio = splitter.ContainerNode.SplitterRatio;
    }

    public void UpdateSplitterDrag(PointerEventArgs e)
    {
        if (ActiveSplitter == null) return;

        var currentPoint = e.GetPosition(this);
        var node = ActiveSplitter.ContainerNode;

        if (_containerBounds.TryGetValue(node, out var containerRect))
        {
            double delta;
            double totalSize;
            if (node.Orientation == Orientation.Horizontal)
            {
                delta = currentPoint.X - _splitterDragStartPoint.X;
                totalSize = containerRect.Width - 8.0;
            }
            else
            {
                delta = currentPoint.Y - _splitterDragStartPoint.Y;
                totalSize = containerRect.Height - 8.0;
            }

            if (totalSize > 0)
            {
                double ratioDelta = delta / totalSize;
                double newRatio = Math.Clamp(_splitterDragStartRatio + ratioDelta, 0.05, 0.95);
                node.SplitterRatio = newRatio;
            }
        }
    }

    public void EndSplitterDrag()
    {
        ActiveSplitter = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _nodeBounds.Clear();
        _containerBounds.Clear();
        var boxes = new Dictionary<BoxNode, Rect>();
        var splitters = new Dictionary<SplitContainerNode, Rect>();
        var containers = new Dictionary<SplitContainerNode, Rect>();

        _parentSplit.ComputePixelBounds(_parentSplit.Root, new Rect(availableSize), 8.0, boxes, splitters, containers);

        foreach (var kv in boxes) _nodeBounds[kv.Key] = kv.Value;
        foreach (var kv in splitters) _nodeBounds[kv.Key] = kv.Value;
        foreach (var kv in containers) _containerBounds[kv.Key] = kv.Value;

        foreach (var child in Children)
        {
            if (child is SuperSplitBox boxControl && boxControl.DataContext is BoxNode boxNode)
            {
                if (boxes.TryGetValue(boxNode, out var rect))
                {
                    boxControl.Measure(rect.Size);
                }
            }
            else if (child is FlatSplitter splitterControl)
            {
                if (splitters.TryGetValue(splitterControl.ContainerNode, out var rect))
                {
                    splitterControl.Measure(rect.Size);
                }
            }
        }

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _nodeBounds.Clear();
        _containerBounds.Clear();
        var boxes = new Dictionary<BoxNode, Rect>();
        var splitters = new Dictionary<SplitContainerNode, Rect>();
        var containers = new Dictionary<SplitContainerNode, Rect>();

        _parentSplit.ComputePixelBounds(_parentSplit.Root, new Rect(finalSize), 8.0, boxes, splitters, containers);

        foreach (var kv in boxes) _nodeBounds[kv.Key] = kv.Value;
        foreach (var kv in splitters) _nodeBounds[kv.Key] = kv.Value;
        foreach (var kv in containers) _containerBounds[kv.Key] = kv.Value;

        foreach (var child in Children)
        {
            if (child is SuperSplitBox boxControl && boxControl.DataContext is BoxNode boxNode)
            {
                if (boxes.TryGetValue(boxNode, out var rect))
                {
                    boxControl.Arrange(rect);
                }
            }
            else if (child is FlatSplitter splitterControl)
            {
                if (splitters.TryGetValue(splitterControl.ContainerNode, out var rect))
                {
                    splitterControl.Arrange(rect);
                }
            }
        }

        return finalSize;
    }
}

public class SuperSplit : ContentControl
{
    private enum RelativeDropLocation
    {
        None,
        Left,
        Right,
        Top,
        Bottom
    }

    public static readonly StyledProperty<SplitNode?> RootProperty =
        AvaloniaProperty.Register<SuperSplit, SplitNode?>(nameof(Root), null);

    public static readonly StyledProperty<BoxNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<SuperSplit, BoxNode?>(nameof(SelectedNode), null, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public SplitNode? Root
    {
        get => GetValue(RootProperty);
        set => SetValue(RootProperty, value);
    }

    public BoxNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public event EventHandler<BoxMenuEventArgs>? BoxMenuClicked;

    private bool _isRebuilding;
    private bool _isDragging;
    private BoxNode? _draggedNode;
    private Point _dragStartPoint;
    private BoxNode? _currentHoverNode;
    private RelativeDropLocation _currentDropLocation = RelativeDropLocation.None;

    private FlatSplitPanel? _flatPanel;
    private Grid? _wrapperGrid;
    private readonly Canvas _overlayCanvas = new() { IsHitTestVisible = false };
    private readonly Border _dropHighlightOverlay = new()
    {
        IsVisible = false,
        IsHitTestVisible = false,
        Background = Brush.Parse("#401a73e8"),
        BorderBrush = Brush.Parse("#1a73e8"),
        BorderThickness = new Thickness(1.5),
        CornerRadius = new CornerRadius(10) // macOS 26 perfection radius
    };
    private readonly Border _dragPreview = new()
    {
        IsVisible = false,
        IsHitTestVisible = false,
        Background = Brush.Parse("#2d2d2d"),
        BorderBrush = Brush.Parse("#1a73e8"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(10, 6)
    };

    static SuperSplit()
    {
        RootProperty.Changed.AddClassHandler<SuperSplit>((x, e) => x.OnRootChanged(e));
        SelectedNodeProperty.Changed.AddClassHandler<SuperSplit>((x, e) => x.OnSelectedNodeChanged(e));
    }

    public SuperSplit()
    {
        ClipToBounds = true;
        _overlayCanvas.Children.Add(_dropHighlightOverlay);
        _overlayCanvas.Children.Add(_dragPreview);
    }

    private void OnRootChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var oldVal = e.OldValue as SplitNode;
        var newVal = e.NewValue as SplitNode;

        if (oldVal != null) UnsubscribeFromTree(oldVal);
        if (newVal != null) SubscribeToTree(newVal);

        Rebuild();
    }

    private void OnSelectedNodeChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var selected = e.NewValue as BoxNode;
        UpdateSelectionInTree(Root, selected);
    }

    private void UpdateSelectionInTree(SplitNode? node, BoxNode? selected)
    {
        if (node == null) return;
        if (node is BoxNode box)
        {
            box.IsSelected = (box == selected);
        }
        else if (node is SplitContainerNode container)
        {
            UpdateSelectionInTree(container.Child1, selected);
            UpdateSelectionInTree(container.Child2, selected);
        }
    }

    private void SubscribeToTree(SplitNode? node)
    {
        if (node == null) return;
        node.PropertyChanged += OnNodePropertyChanged;
        if (node is SplitContainerNode container)
        {
            SubscribeToTree(container.Child1);
            SubscribeToTree(container.Child2);
        }
    }

    private void UnsubscribeFromTree(SplitNode? node)
    {
        if (node == null) return;
        node.PropertyChanged -= OnNodePropertyChanged;
        if (node is SplitContainerNode container)
        {
            UnsubscribeFromTree(container.Child1);
            UnsubscribeFromTree(container.Child2);
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isRebuilding) return;

        if (e.PropertyName == nameof(SplitContainerNode.SplitterRatio))
        {
            _flatPanel?.InvalidateMeasure();
            _flatPanel?.InvalidateArrange();
            return;
        }

        if (e.PropertyName == nameof(SplitContainerNode.Child1) ||
            e.PropertyName == nameof(SplitContainerNode.Child2) ||
            e.PropertyName == nameof(SplitContainerNode.Orientation) ||
            e.PropertyName == nameof(BoxNode.Content) ||
            e.PropertyName == nameof(BoxNode.Title) ||
            e.PropertyName == nameof(BoxNode.IconKey))
        {
            // Coalesce visual tree rebuilds using layout dispatcher post
            Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild, Avalonia.Threading.DispatcherPriority.Normal);
        }
    }

    public void Rebuild()
    {
        _isRebuilding = true;
        try
        {
            // Sync tree subscriptions
            UnsubscribeFromTree(Root);
            SubscribeToTree(Root);

            if (_flatPanel == null)
            {
                _flatPanel = new FlatSplitPanel(this);
            }

            SyncChildren(_flatPanel);

            if (_wrapperGrid == null)
            {
                _wrapperGrid = new Grid();
                _wrapperGrid.Children.Add(_flatPanel);
                _wrapperGrid.Children.Add(_overlayCanvas);
                Content = _wrapperGrid;
            }
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private void SyncChildren(FlatSplitPanel panel)
    {
        var boxesInTree = new List<BoxNode>();
        var containersInTree = new List<SplitContainerNode>();
        CollectNodes(Root, boxesInTree, containersInTree);

        var existingBoxes = panel.Children.OfType<SuperSplitBox>().Where(b => b.DataContext is BoxNode).ToDictionary(b => (BoxNode)b.DataContext!);
        var existingSplitters = panel.Children.OfType<FlatSplitter>().ToDictionary(s => s.ContainerNode);

        var newChildren = new AvaloniaList<Control>();

        foreach (var box in boxesInTree)
        {
            if (existingBoxes.TryGetValue(box, out var boxControl))
            {
                boxControl.HeaderTitle = box.Title;
                boxControl.IconKey = box.IconKey;
                boxControl.IsSelected = box.IsSelected;
                boxControl.BackgroundTint = box.BackgroundTint;
                boxControl.InnerContent = box.Content;
                newChildren.Add(boxControl);
            }
            else
            {
                var newBoxControl = new SuperSplitBox
                {
                    HeaderTitle = box.Title,
                    IconKey = box.IconKey,
                    IsSelected = box.IsSelected,
                    BackgroundTint = box.BackgroundTint,
                    InnerContent = box.Content,
                    DataContext = box
                };

                newBoxControl.BoxSelected += (s, e) => { SelectedNode = box; };
                newBoxControl.HeaderPressed += (s, ev) => { StartDrag(box, ev); };
                newBoxControl.MenuClicked += (s, e) => { BoxMenuClicked?.Invoke(this, new BoxMenuEventArgs(box, newBoxControl)); };

                newChildren.Add(newBoxControl);

                TriggerEntranceAnimation(newBoxControl, box);
            }
        }

        foreach (var container in containersInTree)
        {
            if (existingSplitters.TryGetValue(container, out var splitterControl))
            {
                newChildren.Add(splitterControl);
            }
            else
            {
                var newSplitter = new FlatSplitter(container);
                newChildren.Add(newSplitter);
            }
        }

        // Clear InnerContent of discarded boxes to release their hosted views cleanly
        foreach (var oldBoxControl in existingBoxes.Values)
        {
            if (!newChildren.Contains(oldBoxControl))
            {
                oldBoxControl.InnerContent = null;
            }
        }

        panel.Children.Clear();
        panel.Children.AddRange(newChildren);
    }

    private void CollectNodes(SplitNode? node, List<BoxNode> boxes, List<SplitContainerNode> containers)
    {
        if (node == null) return;
        if (node is BoxNode box)
        {
            boxes.Add(box);
        }
        else if (node is SplitContainerNode container)
        {
            containers.Add(container);
            CollectNodes(container.Child1, boxes, containers);
            CollectNodes(container.Child2, boxes, containers);
        }
    }

    private void TriggerEntranceAnimation(SuperSplitBox boxControl, BoxNode boxNode)
    {
        var parent = boxNode.Parent as SplitContainerNode;
        if (parent == null) return;

        var siblingNode = parent.Child1 == boxNode ? parent.Child2 : parent.Child1;
        var panel = boxControl.Parent as FlatSplitPanel;
        if (panel == null) return;

        SuperSplitBox? siblingControl = null;
        foreach (var child in panel.Children)
        {
            if (child is SuperSplitBox b && b.DataContext == siblingNode)
            {
                siblingControl = b;
                break;
            }
        }

        if (siblingControl == null) return;

        siblingControl.ZIndex = 10;
        boxControl.ZIndex = 5;

        bool isHorizontal = parent.Orientation == Orientation.Horizontal;
        bool isNewFirstChild = parent.Child1 == boxNode;

        var startTime = DateTime.UtcNow;
        var duration = TimeSpan.FromMilliseconds(250);

        var timer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };

        timer.Tick += (s, e) =>
        {
            var elapsed = DateTime.UtcNow - startTime;
            double t = elapsed.TotalMilliseconds / duration.TotalMilliseconds;
            if (t >= 1.0)
            {
                t = 1.0;
                timer.Stop();
                siblingControl.ZIndex = 0;
                boxControl.ZIndex = 0;
                boxControl.RenderTransform = null;
                boxControl.Clip = null;
                return;
            }

            double ease = 1.0 - Math.Pow(1.0 - t, 3.0); // cubic out ease

            var targetBounds = boxControl.Bounds;
            var siblingBounds = siblingControl.Bounds;

            double w = targetBounds.Width;
            double h = targetBounds.Height;

            if (w <= 0 || h <= 0) return;

            if (isHorizontal)
            {
                if (isNewFirstChild)
                {
                    double startOffset = w;
                    double currentOffset = startOffset * (1.0 - ease);
                    boxControl.RenderTransform = new TranslateTransform(currentOffset, 0);
                    boxControl.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, w - currentOffset), h));
                }
                else
                {
                    double startOffset = -siblingBounds.Width;
                    double currentOffset = startOffset * (1.0 - ease);
                    boxControl.RenderTransform = new TranslateTransform(currentOffset, 0);
                    boxControl.Clip = new RectangleGeometry(new Rect(Math.Max(0, -currentOffset), 0, Math.Max(0, w + currentOffset), h));
                }
            }
            else
            {
                if (isNewFirstChild)
                {
                    double startOffset = h;
                    double currentOffset = startOffset * (1.0 - ease);
                    boxControl.RenderTransform = new TranslateTransform(0, currentOffset);
                    boxControl.Clip = new RectangleGeometry(new Rect(0, 0, w, Math.Max(0, h - currentOffset)));
                }
                else
                {
                    double startOffset = -siblingBounds.Height;
                    double currentOffset = startOffset * (1.0 - ease);
                    boxControl.RenderTransform = new TranslateTransform(0, currentOffset);
                    boxControl.Clip = new RectangleGeometry(new Rect(0, Math.Max(0, -currentOffset), w, Math.Max(0, h + currentOffset)));
                }
            }
        };

        timer.Start();
    }

    public void ComputePixelBounds(SplitNode? node, Rect rect, double splitterThickness,
        Dictionary<BoxNode, Rect> boxes, Dictionary<SplitContainerNode, Rect> splitters,
        Dictionary<SplitContainerNode, Rect> containers)
    {
        if (node == null) return;

        if (node is BoxNode box)
        {
            boxes[box] = rect;
        }
        else if (node is SplitContainerNode container)
        {
            containers[container] = rect;

            if (container.Orientation == Orientation.Horizontal)
            {
                double totalW = rect.Width - splitterThickness;
                if (totalW < 0) totalW = 0;
                double w1 = totalW * container.SplitterRatio;
                double w2 = totalW - w1;

                var r1 = new Rect(rect.X, rect.Y, w1, rect.Height);
                var rSplit = new Rect(rect.X + w1, rect.Y, splitterThickness, rect.Height);
                var r2 = new Rect(rect.X + w1 + splitterThickness, rect.Y, w2, rect.Height);

                splitters[container] = rSplit;
                ComputePixelBounds(container.Child1, r1, splitterThickness, boxes, splitters, containers);
                ComputePixelBounds(container.Child2, r2, splitterThickness, boxes, splitters, containers);
            }
            else
            {
                double totalH = rect.Height - splitterThickness;
                if (totalH < 0) totalH = 0;
                double h1 = totalH * container.SplitterRatio;
                double h2 = totalH - h1;

                var r1 = new Rect(rect.X, rect.Y, rect.Width, h1);
                var rSplit = new Rect(rect.X, rect.Y + h1, rect.Width, splitterThickness);
                var r2 = new Rect(rect.X, rect.Y + h1 + splitterThickness, rect.Width, h2);

                splitters[container] = rSplit;
                ComputePixelBounds(container.Child1, r1, splitterThickness, boxes, splitters, containers);
                ComputePixelBounds(container.Child2, r2, splitterThickness, boxes, splitters, containers);
            }
        }
    }

    private void StartDrag(BoxNode node, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _draggedNode = node;
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(this);

            SetupDragPreview(node);
            UpdateDragPreviewPosition(e.GetPosition(this));

            e.Handled = true;
        }
    }

    private void SetupDragPreview(BoxNode node)
    {
        var previewGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto, *") };

        var icon = new PathIcon
        {
            Width = 12,
            Height = 12,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (Application.Current != null && Application.Current.TryFindResource(node.IconKey, out var res) && res is Geometry geom)
        {
            icon.Data = geom;
        }
        Grid.SetColumn(icon, 0);
        previewGrid.Children.Add(icon);

        var text = new TextBlock
        {
            Text = node.Title,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = Brush.Parse("#e8eaed"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(text, 1);
        previewGrid.Children.Add(text);

        _dragPreview.Child = previewGrid;
        _dragPreview.IsVisible = true;
    }

    private void UpdateDragPreviewPosition(Point p)
    {
        Canvas.SetLeft(_dragPreview, p.X + 12);
        Canvas.SetTop(_dragPreview, p.Y + 12);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        HandlePointerMoved(e);
    }

    private void HandlePointerMoved(PointerEventArgs e)
    {
        if (_isDragging && _draggedNode != null)
        {
            var pos = e.GetPosition(this);
            UpdateDragPreviewPosition(pos);

            var hitBox = FindBoxAtPosition(pos);
            if (hitBox != null && hitBox.DataContext is BoxNode targetNode && targetNode != _draggedNode)
            {
                _currentHoverNode = targetNode;

                var bounds = hitBox.Bounds;
                var topLeft = hitBox.TranslatePoint(new Point(0, 0), this);
                if (topLeft.HasValue)
                {
                    double x = pos.X - topLeft.Value.X;
                    double y = pos.Y - topLeft.Value.Y;
                    double w = bounds.Width;
                    double h = bounds.Height;

                    double normX = x / w;
                    double normY = y / h;

                    if (normX < normY)
                    {
                        if (normX < 1.0 - normY)
                            _currentDropLocation = RelativeDropLocation.Left;
                        else
                            _currentDropLocation = RelativeDropLocation.Bottom;
                    }
                    else
                    {
                        if (normX < 1.0 - normY)
                            _currentDropLocation = RelativeDropLocation.Top;
                        else
                            _currentDropLocation = RelativeDropLocation.Right;
                    }

                    ShowDropHighlight(topLeft.Value, w, h, _currentDropLocation);
                }
            }
            else
            {
                _currentHoverNode = null;
                _currentDropLocation = RelativeDropLocation.None;
                _dropHighlightOverlay.IsVisible = false;
            }
        }
    }

    private SuperSplitBox? FindBoxAtPosition(Point p)
    {
        return FindBoxAtPositionRecursive(this, p);
    }

    private SuperSplitBox? FindBoxAtPositionRecursive(Visual visual, Point p)
    {
        if (visual is SuperSplitBox box)
        {
            var topLeft = box.TranslatePoint(new Point(0, 0), this);
            if (topLeft.HasValue)
            {
                var rect = new Rect(topLeft.Value.X, topLeft.Value.Y, box.Bounds.Width, box.Bounds.Height);
                if (rect.Contains(p))
                {
                    return box;
                }
            }
        }

        foreach (var child in visual.GetVisualChildren())
        {
            var found = FindBoxAtPositionRecursive(child, p);
            if (found != null) return found;
        }

        return null;
    }

    private void ShowDropHighlight(Point topLeft, double w, double h, RelativeDropLocation loc)
    {
        double overlayX = topLeft.X;
        double overlayY = topLeft.Y;
        double overlayW = w;
        double overlayH = h;

        switch (loc)
        {
            case RelativeDropLocation.Left:
                overlayW = w / 2;
                break;
            case RelativeDropLocation.Right:
                overlayX = topLeft.X + w / 2;
                overlayW = w / 2;
                break;
            case RelativeDropLocation.Top:
                overlayH = h / 2;
                break;
            case RelativeDropLocation.Bottom:
                overlayY = topLeft.Y + h / 2;
                overlayH = h / 2;
                break;
            default:
                _dropHighlightOverlay.IsVisible = false;
                return;
        }

        Canvas.SetLeft(_dropHighlightOverlay, overlayX);
        Canvas.SetTop(_dropHighlightOverlay, overlayY);
        _dropHighlightOverlay.Width = overlayW;
        _dropHighlightOverlay.Height = overlayH;
        _dropHighlightOverlay.IsVisible = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            _dragPreview.IsVisible = false;
            _dropHighlightOverlay.IsVisible = false;

            if (_draggedNode != null && _currentHoverNode != null && _currentDropLocation != RelativeDropLocation.None)
            {
                MoveNode(_draggedNode, _currentHoverNode, _currentDropLocation);
            }

            _draggedNode = null;
            _currentHoverNode = null;
            _currentDropLocation = RelativeDropLocation.None;
        }
    }

    private void MoveNode(BoxNode source, BoxNode target, RelativeDropLocation loc)
    {
        if (source == target) return;

        if (source.Parent is SplitContainerNode sourceParent)
        {
            var sibling = sourceParent.Child1 == source ? sourceParent.Child2 : sourceParent.Child1;
            var grandparent = sourceParent.Parent;

            sibling.Parent = grandparent;

            if (sourceParent == Root)
            {
                Root = sibling;
            }
            else if (grandparent is SplitContainerNode gp)
            {
                if (gp.Child1 == sourceParent)
                {
                    gp.Child1 = sibling;
                }
                else
                {
                    gp.Child2 = sibling;
                }
            }
        }
        else
        {
            return;
        }

        var orientation = (loc == RelativeDropLocation.Left || loc == RelativeDropLocation.Right)
            ? Orientation.Horizontal
            : Orientation.Vertical;
        bool insertBefore = (loc == RelativeDropLocation.Left || loc == RelativeDropLocation.Top);

        var targetParent = target.Parent as SplitContainerNode;
        SplitContainerNode newContainer;

        if (insertBefore)
        {
            newContainer = new SplitContainerNode(orientation, source, target);
        }
        else
        {
            newContainer = new SplitContainerNode(orientation, target, source);
        }

        if (target == Root)
        {
            Root = newContainer;
        }
        else if (targetParent != null)
        {
            newContainer.Parent = targetParent;
            if (targetParent.Child1 == target)
            {
                targetParent.Child1 = newContainer;
            }
            else
            {
                targetParent.Child2 = newContainer;
            }
        }

        SelectedNode = source;

        Rebuild();
    }
}
