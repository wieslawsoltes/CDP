using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
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
        ZIndex = 1;
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
    private readonly Dictionary<BoxNode, Rect> _previousBoxBounds = new();
    private readonly Dictionary<SplitContainerNode, Rect> _previousSplitterBounds = new();

    public Dictionary<BoxNode, Rect> PreviousBoxBounds => _previousBoxBounds;
    public Dictionary<SplitContainerNode, Rect> PreviousSplitterBounds => _previousSplitterBounds;

    public FlatSplitter? ActiveSplitter { get; private set; }
    private Point _splitterDragStartPoint;
    private double _splitterDragStartRatio;

    public FlatSplitPanel(SuperSplit parentSplit)
    {
        _parentSplit = parentSplit;
        ClipToBounds = true;
    }

    public Dictionary<SplitNode, Rect> NodeBounds => _nodeBounds;
    public Dictionary<SplitContainerNode, Rect> ContainerBounds => _containerBounds;

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

        if (_parentSplit.ZoomedNode != null)
        {
            if (!boxes.ContainsKey(_parentSplit.ZoomedNode))
            {
                _parentSplit.ClearStaleZoom();
            }
        }

        if (_parentSplit.ZoomedNode != null)
        {
            var zoomedBox = _parentSplit.ZoomedNode;
            var normalBoxes = new Dictionary<BoxNode, Rect>(boxes);
            var normalSplitters = new Dictionary<SplitContainerNode, Rect>(splitters);

            foreach (var key in normalBoxes.Keys)
            {
                if (key == zoomedBox)
                {
                    boxes[key] = new Rect(0, 0, availableSize.Width, availableSize.Height);
                }
                else
                {
                    var edge = GetNearestEdge(normalBoxes[key], availableSize);
                    boxes[key] = GetOutOfBoundsRect(normalBoxes[key], edge, availableSize);
                }
            }

            foreach (var key in normalSplitters.Keys)
            {
                var edge = GetNearestEdge(normalSplitters[key], availableSize);
                splitters[key] = GetOutOfBoundsRect(normalSplitters[key], edge, availableSize);
            }
        }

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
            else if (child is FlatCornerGrabHandle cornerControl)
            {
                if (_parentSplit.ZoomedNode != null)
                {
                    cornerControl.Measure(new Size(0, 0));
                    continue;
                }

                Rect rectP = default;
                Rect rectC = default;
                bool hasP = splitters.TryGetValue(cornerControl.ParentContainer, out rectP);
                bool hasC = splitters.TryGetValue(cornerControl.ChildContainer, out rectC);

                if (hasP && hasC)
                {
                    double centerX = cornerControl.ParentContainer.Orientation == Orientation.Horizontal
                        ? rectP.X + rectP.Width / 2
                        : rectC.X + rectC.Width / 2;

                    double centerY = cornerControl.ParentContainer.Orientation == Orientation.Vertical
                        ? rectP.Y + rectP.Height / 2
                        : rectC.Y + rectC.Height / 2;

                    var handleSize = 16.0;
                    var rect = new Rect(centerX - handleSize / 2, centerY - handleSize / 2, handleSize, handleSize);
                    cornerControl.Measure(rect.Size);
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

        if (_parentSplit.ZoomedNode != null)
        {
            if (!boxes.ContainsKey(_parentSplit.ZoomedNode))
            {
                _parentSplit.ClearStaleZoom();
            }
        }

        if (_parentSplit.ZoomedNode != null)
        {
            var zoomedBox = _parentSplit.ZoomedNode;
            var normalBoxes = new Dictionary<BoxNode, Rect>(boxes);
            var normalSplitters = new Dictionary<SplitContainerNode, Rect>(splitters);

            foreach (var key in normalBoxes.Keys)
            {
                if (key == zoomedBox)
                {
                    boxes[key] = new Rect(0, 0, finalSize.Width, finalSize.Height);
                }
                else
                {
                    var edge = GetNearestEdge(normalBoxes[key], finalSize);
                    boxes[key] = GetOutOfBoundsRect(normalBoxes[key], edge, finalSize);
                }
            }

            foreach (var key in normalSplitters.Keys)
            {
                var edge = GetNearestEdge(normalSplitters[key], finalSize);
                splitters[key] = GetOutOfBoundsRect(normalSplitters[key], edge, finalSize);
            }
        }

        foreach (var kv in boxes) _nodeBounds[kv.Key] = kv.Value;
        foreach (var kv in splitters) _nodeBounds[kv.Key] = kv.Value;
        foreach (var kv in containers) _containerBounds[kv.Key] = kv.Value;

        foreach (var child in Children)
        {
            if (child is SuperSplitBox boxControl && boxControl.DataContext is BoxNode boxNode)
            {
                if (boxes.TryGetValue(boxNode, out var rect))
                {
                    if (_parentSplit.IsZoomTransitionPending)
                    {
                        bool boundsChanged = true;
                        if (_previousBoxBounds.TryGetValue(boxNode, out var prev))
                        {
                            boundsChanged = (prev.X != rect.X || prev.Y != rect.Y ||
                                             prev.Width != rect.Width || prev.Height != rect.Height);
                        }
                        if (boundsChanged)
                        {
                            _parentSplit.ConfigureTransitionAnimations(boxControl, true);
                        }
                    }
                    boxControl.Arrange(rect);
                }
            }
            else if (child is FlatSplitter splitterControl)
            {
                if (splitters.TryGetValue(splitterControl.ContainerNode, out var rect))
                {
                    if (_parentSplit.IsZoomTransitionPending)
                    {
                        bool boundsChanged = true;
                        if (_previousSplitterBounds.TryGetValue(splitterControl.ContainerNode, out var prev))
                        {
                            boundsChanged = (prev.X != rect.X || prev.Y != rect.Y ||
                                             prev.Width != rect.Width || prev.Height != rect.Height);
                        }
                        if (boundsChanged)
                        {
                            _parentSplit.ConfigureTransitionAnimations(splitterControl, true);
                        }
                    }
                    splitterControl.Arrange(rect);
                }
            }
            else if (child is FlatCornerGrabHandle cornerControl)
            {
                if (_parentSplit.ZoomedNode != null)
                {
                    cornerControl.Arrange(new Rect(0, 0, 0, 0));
                    continue;
                }

                Rect rectP = default;
                Rect rectC = default;
                bool hasP = splitters.TryGetValue(cornerControl.ParentContainer, out rectP);
                bool hasC = splitters.TryGetValue(cornerControl.ChildContainer, out rectC);

                if (hasP && hasC)
                {
                    double centerX = cornerControl.ParentContainer.Orientation == Orientation.Horizontal
                        ? rectP.X + rectP.Width / 2
                        : rectC.X + rectC.Width / 2;

                    double centerY = cornerControl.ParentContainer.Orientation == Orientation.Vertical
                        ? rectP.Y + rectP.Height / 2
                        : rectC.Y + rectC.Height / 2;

                    var handleSize = 16.0;
                    var rect = new Rect(centerX - handleSize / 2, centerY - handleSize / 2, handleSize, handleSize);
                    cornerControl.Arrange(rect);
                }
            }
        }

        _previousBoxBounds.Clear();
        _previousSplitterBounds.Clear();
        foreach (var kv in boxes) _previousBoxBounds[kv.Key] = kv.Value;
        foreach (var kv in splitters) _previousSplitterBounds[kv.Key] = kv.Value;

        if (_parentSplit.IsZoomTransitionPending)
        {
            _parentSplit.IsZoomTransitionPending = false;
        }

        _parentSplit.UpdateFocusOverlay();

        return finalSize;
    }

    private enum WindowEdge { Left, Right, Top, Bottom }

    private WindowEdge GetNearestEdge(Rect normalRect, Size panelSize)
    {
        bool touchesLeft = normalRect.Left < 1.0;
        bool touchesRight = panelSize.Width - normalRect.Right < 1.0;
        bool touchesTop = normalRect.Top < 1.0;
        bool touchesBottom = panelSize.Height - normalRect.Bottom < 1.0;

        if (touchesLeft || touchesRight || touchesTop || touchesBottom)
        {
            if (touchesLeft) return WindowEdge.Left;
            if (touchesRight) return WindowEdge.Right;
            if (touchesTop) return WindowEdge.Top;
            return WindowEdge.Bottom;
        }

        double distLeft = normalRect.Left;
        double distRight = panelSize.Width - normalRect.Right;
        double distTop = normalRect.Top;
        double distBottom = panelSize.Height - normalRect.Bottom;

        double minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

        if (minDist == distLeft) return WindowEdge.Left;
        if (minDist == distRight) return WindowEdge.Right;
        if (minDist == distTop) return WindowEdge.Top;
        return WindowEdge.Bottom;
    }

    private Rect GetOutOfBoundsRect(Rect normalRect, WindowEdge edge, Size panelSize)
    {
        double padding = 100.0;
        switch (edge)
        {
            case WindowEdge.Left:
                return new Rect(-normalRect.Width - padding, normalRect.Y, normalRect.Width, normalRect.Height);
            case WindowEdge.Right:
                return new Rect(panelSize.Width + padding, normalRect.Y, normalRect.Width, normalRect.Height);
            case WindowEdge.Top:
                return new Rect(normalRect.X, -normalRect.Height - padding, normalRect.Width, normalRect.Height);
            case WindowEdge.Bottom:
            default:
                return new Rect(normalRect.X, panelSize.Height + padding, normalRect.Width, normalRect.Height);
        }
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
        Bottom,
        Center
    }

    public static readonly StyledProperty<SplitNode?> RootProperty =
        AvaloniaProperty.Register<SuperSplit, SplitNode?>(nameof(Root), null);

    public static readonly StyledProperty<BoxNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<SuperSplit, BoxNode?>(nameof(SelectedNode), null, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IDataTemplate?> FocusTemplateProperty =
        AvaloniaProperty.Register<SuperSplit, IDataTemplate?>(nameof(FocusTemplate), null);

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

    public IDataTemplate? FocusTemplate
    {
        get => GetValue(FocusTemplateProperty);
        set => SetValue(FocusTemplateProperty, value);
    }

    public event EventHandler<BoxMenuEventArgs>? BoxMenuClicked;
    public event EventHandler? LayoutRebuilt;

    public Func<string, SuperSplitBox?, Control>? ViewResolver { get; set; }

    private bool _isRebuilding;
    private bool _isDragPending;
    private BoxNode? _pendingDragNode;
    private PointerPressedEventArgs? _pointerPressedEventArgs;
    private Point _dragStartPoint;
    private BoxNode? _currentHoverNode;
    private RelativeDropLocation _currentDropLocation = RelativeDropLocation.None;
    private Avalonia.Rendering.Composition.Animations.ImplicitAnimationCollection? _focusOverlayImplicitAnimations;
    private BoxNode? _lastFocusOverlayNode;
    private Avalonia.Threading.DispatcherTimer? _hideHighlightTimer;
    private BoxNode? _zoomedNode;
    public BoxNode? ZoomedNode => _zoomedNode;
    public void ClearStaleZoom()
    {
        _zoomedNode = null;
    }
    private bool _isZoomTransitionPending;
    public bool IsZoomTransitionPending
    {
        get => _isZoomTransitionPending;
        set => _isZoomTransitionPending = value;
    }

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
        BorderBrush = Brush.Parse("#3c4043"),
        BorderThickness = new Thickness(1.0),
        CornerRadius = new CornerRadius(10)
    };
    private readonly Border _focusOverlay = new()
    {
        IsVisible = false,
        IsHitTestVisible = false,
        Background = Brushes.Transparent,
        BorderBrush = Brush.Parse("#1a73e8"),
        BorderThickness = new Thickness(2.0),
        CornerRadius = new CornerRadius(10)
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
        _overlayCanvas.Children.Add(_focusOverlay);

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        _dropHighlightOverlay.AttachedToVisualTree += (s, e) =>
        {
            var visual = ElementComposition.GetElementVisual(_dropHighlightOverlay);
            if (visual != null)
            {
                var compositor = visual.Compositor;
                var implicitAnimations = compositor.CreateImplicitAnimationCollection();
                
                var offsetAnim = compositor.CreateVector3KeyFrameAnimation();
                offsetAnim.Duration = TimeSpan.FromMilliseconds(120);
                offsetAnim.Target = "Offset";
                offsetAnim.InsertExpressionKeyFrame(1.0f, "this.FinalValue", new SplineEasing(0.25, 0.1, 0.25, 1.0));
                implicitAnimations["Offset"] = offsetAnim;

                var sizeAnim = compositor.CreateVector2KeyFrameAnimation();
                sizeAnim.Duration = TimeSpan.FromMilliseconds(120);
                sizeAnim.Target = "Size";
                sizeAnim.InsertExpressionKeyFrame(1.0f, "this.FinalValue", new SplineEasing(0.25, 0.1, 0.25, 1.0));
                implicitAnimations["Size"] = sizeAnim;

                visual.ImplicitAnimations = implicitAnimations;
            }
        };

        _focusOverlay.AttachedToVisualTree += (s, e) =>
        {
            var visual = ElementComposition.GetElementVisual(_focusOverlay);
            if (visual != null)
            {
                var compositor = visual.Compositor;
                var implicitAnimations = compositor.CreateImplicitAnimationCollection();
                
                var offsetAnim = compositor.CreateVector3KeyFrameAnimation();
                offsetAnim.Duration = TimeSpan.FromMilliseconds(200);
                offsetAnim.Target = "Offset";
                offsetAnim.InsertExpressionKeyFrame(1.0f, "this.FinalValue", new SplineEasing(0.25, 0.1, 0.25, 1.0));
                implicitAnimations["Offset"] = offsetAnim;

                var sizeAnim = compositor.CreateVector2KeyFrameAnimation();
                sizeAnim.Duration = TimeSpan.FromMilliseconds(200);
                sizeAnim.Target = "Size";
                sizeAnim.InsertExpressionKeyFrame(1.0f, "this.FinalValue", new SplineEasing(0.25, 0.1, 0.25, 1.0));
                implicitAnimations["Size"] = sizeAnim;

                _focusOverlayImplicitAnimations = implicitAnimations;
                visual.ImplicitAnimations = implicitAnimations;
            }
        };
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
        UpdateFocusOverlay();
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
        if (node is BoxNode box)
        {
            box.Tabs.CollectionChanged += OnTabsCollectionChanged;
        }
        else if (node is SplitContainerNode container)
        {
            SubscribeToTree(container.Child1);
            SubscribeToTree(container.Child2);
        }
    }

    private void UnsubscribeFromTree(SplitNode? node)
    {
        if (node == null) return;
        node.PropertyChanged -= OnNodePropertyChanged;
        if (node is BoxNode box)
        {
            box.Tabs.CollectionChanged -= OnTabsCollectionChanged;
        }
        else if (node is SplitContainerNode container)
        {
            UnsubscribeFromTree(container.Child1);
            UnsubscribeFromTree(container.Child2);
        }
    }

    private void OnTabsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_isRebuilding) return;

        if (sender is System.Collections.ObjectModel.ObservableCollection<BoxTabNode> tabs && tabs.Count == 0)
        {
            var emptyBoxNode = FindBoxNodeByTabs(Root, tabs);
            if (emptyBoxNode != null)
            {
                PruneEmptyNode(emptyBoxNode);
            }
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild, Avalonia.Threading.DispatcherPriority.Normal);
        }
    }

    private BoxNode? FindBoxNodeByTabs(SplitNode? node, System.Collections.ObjectModel.ObservableCollection<BoxTabNode> tabs)
    {
        if (node == null) return null;
        if (node is BoxNode box && box.Tabs == tabs) return box;
        if (node is SplitContainerNode container)
        {
            var b1 = FindBoxNodeByTabs(container.Child1, tabs);
            if (b1 != null) return b1;
            return FindBoxNodeByTabs(container.Child2, tabs);
        }
        return null;
    }

    private void PruneEmptyNode(BoxNode emptyNode)
    {
        if (emptyNode == Root)
        {
            Root = null;
            Rebuild();
            return;
        }

        if (emptyNode.Parent is SplitContainerNode parent)
        {
            var sibling = parent.Child1 == emptyNode ? parent.Child2 : parent.Child1;
            var grandparent = parent.Parent;
            sibling.Parent = grandparent;

            if (parent == Root)
            {
                Root = sibling;
            }
            else if (grandparent is SplitContainerNode gp)
            {
                if (gp.Child1 == parent) gp.Child1 = sibling;
                else gp.Child2 = sibling;
            }

            Rebuild();
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
                _flatPanel = new FlatSplitPanel(this)
                {
                    Background = Brush.Parse("#202124")
                };
            }

            SyncChildren(_flatPanel);

            if (_wrapperGrid == null)
            {
                _wrapperGrid = new Grid
                {
                    Background = Brush.Parse("#202124")
                };
                _wrapperGrid.Children.Add(_flatPanel);
                _wrapperGrid.Children.Add(_overlayCanvas);
                Content = _wrapperGrid;
            }
        }
        finally
        {
            _isRebuilding = false;
            LayoutRebuilt?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsInteractiveResizing()
    {
        if (_flatPanel == null) return false;
        if (_flatPanel.ActiveSplitter != null) return true;

        foreach (var child in _flatPanel.Children)
        {
            if (child is FlatCornerGrabHandle handle && handle.IsPressed)
            {
                return true;
            }
        }
        return false;
    }

    public void UpdateFocusOverlay()
    {
        if (SelectedNode == null || _flatPanel == null || _zoomedNode != null)
        {
            _focusOverlay.IsVisible = false;
            return;
        }

        if (_flatPanel.NodeBounds.TryGetValue(SelectedNode, out var rect))
        {
            if (FocusTemplate != null)
            {
                if (_lastFocusOverlayNode != SelectedNode || _focusOverlay.Child == null)
                {
                    var builtControl = FocusTemplate.Build(SelectedNode) as Control;
                    if (builtControl != null)
                    {
                        builtControl.DataContext = SelectedNode;
                    }
                    _focusOverlay.Child = builtControl;
                    _focusOverlay.BorderThickness = new Thickness(0);
                    _lastFocusOverlayNode = SelectedNode;
                }
            }
            else
            {
                _focusOverlay.Child = null;
                _focusOverlay.BorderThickness = new Thickness(2.0);
                _lastFocusOverlayNode = null;
            }

            var visual = ElementComposition.GetElementVisual(_focusOverlay);
            if (visual != null)
            {
                if (IsInteractiveResizing())
                {
                    visual.ImplicitAnimations = null;
                }
                else
                {
                    visual.ImplicitAnimations = _focusOverlayImplicitAnimations;
                }
            }

            if (!_focusOverlay.IsVisible)
            {
                if (visual != null)
                {
                    var anims = visual.ImplicitAnimations;
                    visual.ImplicitAnimations = null;
                    Canvas.SetLeft(_focusOverlay, rect.X);
                    Canvas.SetTop(_focusOverlay, rect.Y);
                    _focusOverlay.Width = rect.Width;
                    _focusOverlay.Height = rect.Height;
                    _focusOverlay.IsVisible = true;
                    _overlayCanvas.UpdateLayout();
                    visual.ImplicitAnimations = anims;
                }
                else
                {
                    Canvas.SetLeft(_focusOverlay, rect.X);
                    Canvas.SetTop(_focusOverlay, rect.Y);
                    _focusOverlay.Width = rect.Width;
                    _focusOverlay.Height = rect.Height;
                    _focusOverlay.IsVisible = true;
                }
            }
            else
            {
                Canvas.SetLeft(_focusOverlay, rect.X);
                Canvas.SetTop(_focusOverlay, rect.Y);
                _focusOverlay.Width = rect.Width;
                _focusOverlay.Height = rect.Height;
            }
        }
        else
        {
            _focusOverlay.IsVisible = false;
        }
    }

    private void CollectCornerHandles(SplitNode? node, List<FlatCornerGrabHandle> list)
    {
        if (node == null || node is BoxNode) return;

        if (node is SplitContainerNode container)
        {
            if (container.Orientation == Orientation.Horizontal)
            {
                // Check child 1
                if (container.Child1 is SplitContainerNode child1 && child1.Orientation == Orientation.Vertical)
                {
                    list.Add(new FlatCornerGrabHandle(container, child1));
                }
                // Check child 2
                if (container.Child2 is SplitContainerNode child2 && child2.Orientation == Orientation.Vertical)
                {
                    list.Add(new FlatCornerGrabHandle(container, child2));
                }

                CollectCornerHandles(container.Child1, list);
                CollectCornerHandles(container.Child2, list);
            }
            else
            {
                // Check child 1
                if (container.Child1 is SplitContainerNode child1 && child1.Orientation == Orientation.Horizontal)
                {
                    list.Add(new FlatCornerGrabHandle(container, child1));
                }
                // Check child 2
                if (container.Child2 is SplitContainerNode child2 && child2.Orientation == Orientation.Horizontal)
                {
                    list.Add(new FlatCornerGrabHandle(container, child2));
                }

                CollectCornerHandles(container.Child1, list);
                CollectCornerHandles(container.Child2, list);
            }
        }
    }

    private void SyncChildren(FlatSplitPanel panel)
    {
        var boxesInTree = new List<BoxNode>();
        var containersInTree = new List<SplitContainerNode>();
        CollectNodes(Root, boxesInTree, containersInTree);

        var existingBoxes = new Dictionary<BoxNode, SuperSplitBox>();
        foreach (var b in panel.Children.OfType<SuperSplitBox>())
        {
            if (b.DataContext is BoxNode boxNode)
            {
                existingBoxes[boxNode] = b;
            }
        }

        var existingSplitters = new Dictionary<SplitContainerNode, FlatSplitter>();
        foreach (var s in panel.Children.OfType<FlatSplitter>())
        {
            if (s.ContainerNode != null)
            {
                existingSplitters[s.ContainerNode] = s;
            }
        }

        // Compute new layout bounds to compare against previous bounds for selective animation
        var newBoxes = new Dictionary<BoxNode, Rect>();
        var newSplitters = new Dictionary<SplitContainerNode, Rect>();
        var newContainers = new Dictionary<SplitContainerNode, Rect>();
        if (panel.Bounds.Width > 0 && panel.Bounds.Height > 0)
        {
            ComputePixelBounds(Root, new Rect(panel.Bounds.Size), 8.0, newBoxes, newSplitters, newContainers);
        }

        // Temporarily clear InnerContent of all existing box controls to prevent cross-assignment conflicts during layout swaps
        foreach (var boxControl in existingBoxes.Values)
        {
            boxControl.InnerContent = null;
        }

        // Force layout update to immediately detach visual children before removing the boxes from visual tree
        panel.UpdateLayout();

        var newChildren = new AvaloniaList<Control>();

        foreach (var box in boxesInTree)
        {
            if (existingBoxes.TryGetValue(box, out var boxControl))
            {
                boxControl.DataContext = box;
                newChildren.Add(boxControl);

                bool isMoving = false;
                if (panel.PreviousBoxBounds.TryGetValue(box, out var prevRect) && newBoxes.TryGetValue(box, out var newRect))
                {
                    isMoving = (prevRect.X != newRect.X || prevRect.Y != newRect.Y ||
                                prevRect.Width != newRect.Width || prevRect.Height != newRect.Height);
                }
                ConfigureTransitionAnimations(boxControl, isMoving);
            }
            else
            {
                var newBoxControl = new SuperSplitBox
                {
                    DataContext = box,
                    IsEntranceAnimationRequested = true
                };

                newBoxControl.Bind(SuperSplitBox.HeaderTitleProperty, new Binding(nameof(BoxNode.Title)));
                newBoxControl.Bind(SuperSplitBox.IconKeyProperty, new Binding(nameof(BoxNode.IconKey)));
                newBoxControl.Bind(SuperSplitBox.IsSelectedProperty, new Binding(nameof(BoxNode.IsSelected)));
                newBoxControl.Bind(SuperSplitBox.BackgroundTintProperty, new Binding(nameof(BoxNode.BackgroundTint)));
                newBoxControl.Bind(SuperSplitBox.SelectedViewNameProperty, new Binding(nameof(BoxNode.SelectedViewName)));

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

                bool isMoving = false;
                if (panel.PreviousSplitterBounds.TryGetValue(container, out var prevRect) && newSplitters.TryGetValue(container, out var newRect))
                {
                    isMoving = (prevRect.X != newRect.X || prevRect.Y != newRect.Y ||
                                prevRect.Width != newRect.Width || prevRect.Height != newRect.Height);
                }
                ConfigureTransitionAnimations(splitterControl, isMoving);
            }
            else
            {
                var newSplitter = new FlatSplitter(container);
                newChildren.Add(newSplitter);
            }
        }

        var cornerHandles = new List<FlatCornerGrabHandle>();
        CollectCornerHandles(Root, cornerHandles);
        foreach (var handle in cornerHandles)
        {
            newChildren.Add(handle);
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

    public void ToggleZoomNode(BoxNode node)
    {
        Console.WriteLine($"[SuperSplit] ToggleZoomNode called! current zoomed={(_zoomedNode != null ? _zoomedNode.GetHashCode() : 0)}, requesting node={(node != null ? node.GetHashCode() : 0)}");
        if (_zoomedNode == node)
        {
            _zoomedNode = null;
        }
        else
        {
            _zoomedNode = node;
        }

        if (_flatPanel != null)
        {
            foreach (var child in _flatPanel.Children)
            {
                if (child is SuperSplitBox boxControl && boxControl.DataContext is BoxNode boxNode)
                {
                    boxControl.UpdateZoomButton(boxNode == _zoomedNode);
                }
            }
        }

        _isZoomTransitionPending = true;
        _flatPanel?.InvalidateMeasure();
        _flatPanel?.InvalidateArrange();
    }

    public void ConfigureTransitionAnimations(Control control, bool enabled)
    {
        var visual = ElementComposition.GetElementVisual(control);
        if (visual != null)
        {
            if (enabled)
            {
                var compositor = visual.Compositor;
                var implicitAnimations = compositor.CreateImplicitAnimationCollection();
                
                var offsetAnim = compositor.CreateVector3KeyFrameAnimation();
                offsetAnim.Duration = TimeSpan.FromMilliseconds(250);
                offsetAnim.Target = "Offset";
                offsetAnim.InsertExpressionKeyFrame(1.0f, "this.FinalValue", new SplineEasing(0.25, 0.1, 0.25, 1.0));
                implicitAnimations["Offset"] = offsetAnim;

                var sizeAnim = compositor.CreateVector2KeyFrameAnimation();
                sizeAnim.Duration = TimeSpan.FromMilliseconds(250);
                sizeAnim.Target = "Size";
                sizeAnim.InsertExpressionKeyFrame(1.0f, "this.FinalValue", new SplineEasing(0.25, 0.1, 0.25, 1.0));
                implicitAnimations["Size"] = sizeAnim;

                visual.ImplicitAnimations = implicitAnimations;

                var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                timer.Tick += (s, ev) =>
                {
                    timer.Stop();
                    var v = ElementComposition.GetElementVisual(control);
                    if (v != null) v.ImplicitAnimations = null;
                };
                timer.Start();
            }
            else
            {
                visual.ImplicitAnimations = null;
            }
        }
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

    private void CollectBoxes(SplitNode? node, List<BoxNode> boxes)
    {
        if (node == null) return;
        if (node is BoxNode box) boxes.Add(box);
        else if (node is SplitContainerNode container)
        {
            CollectBoxes(container.Child1, boxes);
            CollectBoxes(container.Child2, boxes);
        }
    }

    private void TriggerEntranceAnimation(SuperSplitBox boxControl, BoxNode boxNode)
    {
        var parent = boxNode.Parent as SplitContainerNode;
        if (parent == null) return;

        var siblingNode = parent.Child1 == boxNode ? parent.Child2 : parent.Child1;
        var panel = boxControl.Parent as FlatSplitPanel;
        if (panel == null) return;

        panel.UpdateLayout();

        if (!panel.NodeBounds.TryGetValue(boxNode, out var finalNewRect))
        {
            return;
        }

        Rect finalSibRect = default;
        bool foundSib = false;
        if (siblingNode is BoxNode siblingBox)
        {
            foundSib = panel.NodeBounds.TryGetValue(siblingBox, out finalSibRect);
        }
        else if (siblingNode is SplitContainerNode siblingContainer)
        {
            foundSib = panel.ContainerBounds.TryGetValue(siblingContainer, out finalSibRect);
        }

        if (!foundSib) return;

        var siblingBoxes = new List<BoxNode>();
        CollectBoxes(siblingNode, siblingBoxes);

        var siblingControls = new List<SuperSplitBox>();
        foreach (var child in panel.Children)
        {
            if (child is SuperSplitBox b && b.DataContext is BoxNode box && siblingBoxes.Contains(box))
            {
                siblingControls.Add(b);
                b.ZIndex = 10;
            }
        }
        boxControl.ZIndex = 5;

        var visual = ElementComposition.GetElementVisual(boxControl);
        if (visual != null)
        {
            // Set starting values directly without animation
            visual.ImplicitAnimations = null;
            visual.Offset = new Vector3((float)finalSibRect.X, (float)finalSibRect.Y, 0.0f);
            visual.Opacity = 0.0f;

            // Configure transition animations for Offset
            var compositor = visual.Compositor;
            var implicitAnimations = compositor.CreateImplicitAnimationCollection();
            
            var offsetAnim = compositor.CreateVector3KeyFrameAnimation();
            offsetAnim.Duration = TimeSpan.FromMilliseconds(250);
            offsetAnim.Target = "Offset";
            offsetAnim.InsertExpressionKeyFrame(1.0f, "this.FinalValue", new SplineEasing(0.25, 0.1, 0.25, 1.0));
            implicitAnimations["Offset"] = offsetAnim;

            visual.ImplicitAnimations = implicitAnimations;

            // Trigger the transition by setting Offset to final target
            visual.Offset = new Vector3((float)finalNewRect.X, (float)finalNewRect.Y, 0.0f);

            // Animate Opacity fade-in explicitly
            var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.Target = "Opacity";
            opacityAnimation.InsertKeyFrame(0.0f, 0.0f);
            opacityAnimation.InsertKeyFrame(1.0f, 1.0f);
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(250);
            visual.StartAnimation("Opacity", opacityAnimation);

            var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                foreach (var c in siblingControls)
                {
                    c.ZIndex = 0;
                }
                boxControl.ZIndex = 0;
                var v = ElementComposition.GetElementVisual(boxControl);
                if (v != null) v.ImplicitAnimations = null;
            };
            timer.Start();
        }
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
            _isDragPending = true;
            _pendingDragNode = node;
            _dragStartPoint = e.GetPosition(this);
            _pointerPressedEventArgs = e;
            e.Pointer.Capture(this);
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
        var pos = e.GetPosition(this);

        if (_isDragPending && _pendingDragNode != null)
        {
            var diff = pos - _dragStartPoint;
            var dist = Math.Sqrt(diff.X * diff.X + diff.Y * diff.Y);
            if (dist > 6)
            {
                InitiateNativeDrag(e);
            }
        }
    }

    private async void InitiateNativeDrag(PointerEventArgs e)
    {
        _isDragPending = false;
        var node = _pendingDragNode;
        if (node == null) return;

        var activeTab = node.ActiveTab;
        BoxNode draggedBox;
        BoxTabNode? draggedTab = null;

        if (activeTab != null && node.Tabs.Count > 1)
        {
            draggedTab = activeTab;
            node.Tabs.Remove(activeTab);
            if (node.Tabs.Count > 0)
            {
                node.ActiveTab = node.Tabs[0];
            }

            draggedBox = new BoxNode { BackgroundTint = "#292a2d" };
            draggedBox.Tabs.Add(activeTab);
            draggedBox.ActiveTab = activeTab;
        }
        else
        {
            draggedTab = activeTab;
            draggedBox = node;
        }

        SuperSplitDragManager.IsDragging = true;
        SuperSplitDragManager.SourceNode = node;
        SuperSplitDragManager.SourceTab = draggedTab;
        SuperSplitDragManager.SourceSplit = this;
        SuperSplitDragManager.DraggedNode = draggedBox;

        SetupDragPreview(draggedBox);
        UpdateDragPreviewPosition(e.GetPosition(this));

        var dataObject = new DataTransfer();
        dataObject.Add(DataTransferItem.CreateText("SuperSplitDraggedNode"));

        bool success = false;
        try
        {
            if (_pointerPressedEventArgs != null)
            {
                _pointerPressedEventArgs.Source = this;
                var result = await DragDrop.DoDragDropAsync(_pointerPressedEventArgs, dataObject, DragDropEffects.Move);
                success = (result == DragDropEffects.Move);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Native drag initiation failed: {ex.Message}");
        }

        if (!success && !SuperSplitDragManager.IsOverDropTarget && SuperSplitDragManager.FloatNodeCallback != null)
        {
            if (draggedBox != null)
            {
                if (draggedBox == node)
                {
                    PruneEmptyNode(node);
                }

                SuperSplitDragManager.FloatNodeCallback(this, draggedBox);
                success = true;
            }
        }

        CleanUpDrag(success);
    }

    private void CleanUpDrag(bool success)
    {
        try
        {
            if (_pointerPressedEventArgs != null)
            {
                _pointerPressedEventArgs.Pointer?.Capture(null);
            }
        }
        catch {}
        _pointerPressedEventArgs = null;

        if (SuperSplitDragManager.IsDragging)
        {
            var sourceTab = SuperSplitDragManager.SourceTab;
            var sourceNode = SuperSplitDragManager.SourceNode;
            var draggedNode = SuperSplitDragManager.DraggedNode;

            if (!success && sourceTab != null && sourceNode != null && draggedNode != sourceNode)
            {
                if (!sourceNode.Tabs.Contains(sourceTab))
                {
                    sourceNode.Tabs.Add(sourceTab);
                    sourceNode.ActiveTab = sourceTab;
                }
            }

            SuperSplitDragManager.Reset();
        }

        _isDragPending = false;
        _pendingDragNode = null;
        _dragPreview.IsVisible = false;
        HideDropHighlight();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!SuperSplitDragManager.IsDragging || SuperSplitDragManager.DraggedNode == null)
        {
            e.DragEffects = DragDropEffects.None;
            SuperSplitDragManager.IsOverDropTarget = false;
            return;
        }

        var pos = e.GetPosition(this);
        _dragPreview.IsVisible = true;
        UpdateDragPreviewPosition(pos);

        var hitBox = FindBoxAtPosition(pos);
        if (hitBox != null && hitBox.DataContext is BoxNode targetNode && targetNode != SuperSplitDragManager.DraggedNode)
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

                if (normX >= 0.25 && normX <= 0.75 && normY >= 0.25 && normY <= 0.75)
                {
                    _currentDropLocation = RelativeDropLocation.Center;
                }
                else if (normX < normY)
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
                e.DragEffects = DragDropEffects.Move;
                SuperSplitDragManager.IsOverDropTarget = true;
            }
            else
            {
                SuperSplitDragManager.IsOverDropTarget = false;
            }
        }
        else
        {
            _currentHoverNode = null;
            _currentDropLocation = RelativeDropLocation.None;
            HideDropHighlight();
            e.DragEffects = DragDropEffects.None;
            SuperSplitDragManager.IsOverDropTarget = false;
        }

        e.Handled = true;
    }

    private void OnDragLeave(object? sender, RoutedEventArgs e)
    {
        HideDropHighlight();
        _dragPreview.IsVisible = false;
        SuperSplitDragManager.IsOverDropTarget = false;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        SuperSplitDragManager.IsOverDropTarget = false;
        bool success = false;
        if (SuperSplitDragManager.IsDragging && SuperSplitDragManager.DraggedNode != null &&
            _currentHoverNode != null && _currentDropLocation != RelativeDropLocation.None)
        {
            var sourceNode = SuperSplitDragManager.DraggedNode;
            var targetNode = _currentHoverNode;
            var sourceSplit = SuperSplitDragManager.SourceSplit;

            MoveNodeCrossWindow(sourceNode, targetNode, _currentDropLocation, sourceSplit);

            e.DragEffects = DragDropEffects.Move;
            success = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }

        CleanUpDrag(success);
        e.Handled = true;
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
            case RelativeDropLocation.Center:
                break;
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
                HideDropHighlight();
                return;
        }

        var visual = ElementComposition.GetElementVisual(_dropHighlightOverlay);

        if (_hideHighlightTimer != null)
        {
            _hideHighlightTimer.Stop();
            _hideHighlightTimer = null;

            if (visual != null)
            {
                var compositor = visual.Compositor;
                var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
                opacityAnimation.Target = "Opacity";
                opacityAnimation.InsertKeyFrame(0.0f, (float)visual.Opacity);
                opacityAnimation.InsertKeyFrame(1.0f, 1.0f);
                opacityAnimation.Duration = TimeSpan.FromMilliseconds(120);
                visual.StartAnimation("Opacity", opacityAnimation);
            }
            else
            {
                _dropHighlightOverlay.Opacity = 1.0;
            }
        }

        if (!_dropHighlightOverlay.IsVisible)
        {
            if (visual != null)
            {
                var implicitAnims = visual.ImplicitAnimations;
                visual.ImplicitAnimations = null;

                Canvas.SetLeft(_dropHighlightOverlay, overlayX);
                Canvas.SetTop(_dropHighlightOverlay, overlayY);
                _dropHighlightOverlay.Width = overlayW;
                _dropHighlightOverlay.Height = overlayH;
                _dropHighlightOverlay.Opacity = 0.0;
                _dropHighlightOverlay.IsVisible = true;

                _overlayCanvas.UpdateLayout();

                visual.ImplicitAnimations = implicitAnims;

                var compositor = visual.Compositor;
                var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
                opacityAnimation.Target = "Opacity";
                opacityAnimation.InsertKeyFrame(0.0f, 0.0f);
                opacityAnimation.InsertKeyFrame(1.0f, 1.0f);
                opacityAnimation.Duration = TimeSpan.FromMilliseconds(120);
                visual.StartAnimation("Opacity", opacityAnimation);
            }
            else
            {
                Canvas.SetLeft(_dropHighlightOverlay, overlayX);
                Canvas.SetTop(_dropHighlightOverlay, overlayY);
                _dropHighlightOverlay.Width = overlayW;
                _dropHighlightOverlay.Height = overlayH;
                _dropHighlightOverlay.IsVisible = true;
                _dropHighlightOverlay.Opacity = 1.0;
            }
        }
        else
        {
            Canvas.SetLeft(_dropHighlightOverlay, overlayX);
            Canvas.SetTop(_dropHighlightOverlay, overlayY);
            _dropHighlightOverlay.Width = overlayW;
            _dropHighlightOverlay.Height = overlayH;
        }
    }

    private void HideDropHighlight()
    {
        if (_hideHighlightTimer != null) return;
        if (!_dropHighlightOverlay.IsVisible) return;

        var visual = ElementComposition.GetElementVisual(_dropHighlightOverlay);
        if (visual != null)
        {
            var compositor = visual.Compositor;
            var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.Target = "Opacity";
            opacityAnimation.InsertKeyFrame(0.0f, (float)visual.Opacity);
            opacityAnimation.InsertKeyFrame(1.0f, 0.0f);
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(120);
            
            _hideHighlightTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _hideHighlightTimer.Tick += (s, ev) =>
            {
                _hideHighlightTimer.Stop();
                _hideHighlightTimer = null;
                _dropHighlightOverlay.IsVisible = false;
            };
            _hideHighlightTimer.Start();

            visual.StartAnimation("Opacity", opacityAnimation);
        }
        else
        {
            _dropHighlightOverlay.IsVisible = false;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragPending)
        {
            _isDragPending = false;
            _pendingDragNode = null;
            e.Pointer.Capture(null);
        }
    }

    private void MoveNode(BoxNode source, BoxNode target, RelativeDropLocation loc)
    {
        MoveNodeCrossWindow(source, target, loc, this);
    }

    private void MoveNodeCrossWindow(BoxNode source, BoxNode target, RelativeDropLocation loc, SuperSplit? sourceSplit)
    {
        sourceSplit ??= this;

        if (source == target) return;

        if (loc == RelativeDropLocation.Center)
        {
            var sourceTabs = new System.Collections.Generic.List<BoxTabNode>(source.Tabs);
            foreach (var tab in sourceTabs)
            {
                source.Tabs.Remove(tab);
                target.Tabs.Add(tab);
            }
            if (sourceTabs.Count > 0)
            {
                target.ActiveTab = sourceTabs[sourceTabs.Count - 1];
            }
            SelectedNode = target;

            if (source.Tabs.Count == 0)
            {
                sourceSplit.PruneEmptyNode(source);
            }
            sourceSplit.Rebuild();
            Rebuild();
            return;
        }

        if (source.Parent is SplitContainerNode sourceParent)
        {
            var sibling = sourceParent.Child1 == source ? sourceParent.Child2 : sourceParent.Child1;
            var grandparent = sourceParent.Parent;

            sibling.Parent = grandparent;

            if (sourceParent == sourceSplit.Root)
            {
                sourceSplit.Root = sibling;
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
        else if (source == sourceSplit.Root)
        {
            sourceSplit.Root = null;
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

        sourceSplit.Rebuild();
        Rebuild();
    }
}
