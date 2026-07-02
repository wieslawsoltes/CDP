using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CDP.Editor.Splits.Controls;
using CDP.Editor.Splits.Models;

namespace CDP.Inspector.Shared.Controls;

public class FloatingSplitWindow : Window
{
    private readonly SuperSplit _superSplit;
    private readonly SuperSplit _mainSplit;
    private Button? _btnMaximize;
    private bool _isTitleDragPending;
    private bool _isTitleDragging;
    private Point _titleDragStartPoint;
    private PixelPoint _windowStartPos;
    private SuperSplit? _currentActiveDragTargetSplit;
    private BoxNode? _currentDropTargetNode;
    private RelativeDropLocation _currentDropLocation;

    public FloatingSplitWindow(SuperSplit mainSplit, BoxNode rootNode)
    {
        _mainSplit = mainSplit;

        Title = "Inspector Panel - Floating";
        Width = 800;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush.Parse("#202124");
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        DataContext = mainSplit.DataContext;

        // Use border-only decorations to hide default title bar but keep resize borders
        WindowDecorations = Avalonia.Controls.WindowDecorations.BorderOnly;

        _superSplit = new SuperSplit
        {
            ViewResolver = mainSplit.ViewResolver,
            Root = rootNode
        };

        _superSplit.LayoutRebuilt += OnLayoutRebuilt;

        // Custom Title Bar Grid layout
        var titleBarGrid = new Grid
        {
            Height = 32,
            Background = Brush.Parse("#1e1e1e"),
            ColumnDefinitions = new ColumnDefinitions("*, Auto"),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Title and Icon container
        var titleContainer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        var appIcon = new PathIcon
        {
            Width = 12,
            Height = 12,
            Foreground = Brush.Parse("#8ab4f8"),
            Margin = new Thickness(0, 0, 8, 0)
        };
        if (Application.Current != null && Application.Current.TryFindResource("WindowMultipleIcon", out var iconRes) && iconRes is Geometry iconGeom)
        {
            appIcon.Data = iconGeom;
        }
        titleContainer.Children.Add(appIcon);

        var titleText = new TextBlock
        {
            Text = "Inspector Panel - Floating",
            FontSize = 11,
            Foreground = Brush.Parse("#e8eaed"),
            VerticalAlignment = VerticalAlignment.Center
        };
        titleContainer.Children.Add(titleText);
        Grid.SetColumn(titleContainer, 0);
        titleBarGrid.Children.Add(titleContainer);

        // Window control buttons panel
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var btnMinimize = new Button
        {
            Width = 45,
            Height = 32,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Content = CreateMinimizeIcon(),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _btnMaximize = new Button
        {
            Width = 45,
            Height = 32,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Content = CreateMaximizeIcon(),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var btnClose = new Button
        {
            Width = 45,
            Height = 32,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Content = CreateCloseIcon(),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Add premium mouse hover effects
        btnMinimize.PointerEntered += (s, e) => { btnMinimize.Background = Brush.Parse("#35363a"); };
        btnMinimize.PointerExited += (s, e) => { btnMinimize.Background = Brushes.Transparent; };

        _btnMaximize.PointerEntered += (s, e) => { _btnMaximize.Background = Brush.Parse("#35363a"); };
        _btnMaximize.PointerExited += (s, e) => { _btnMaximize.Background = Brushes.Transparent; };

        btnClose.PointerEntered += (s, e) =>
        {
            btnClose.Background = Brush.Parse("#e81123");
            if (btnClose.Content is PathIcon icon) icon.Foreground = Brushes.White;
        };
        btnClose.PointerExited += (s, e) =>
        {
            btnClose.Background = Brushes.Transparent;
            if (btnClose.Content is PathIcon icon) icon.Foreground = Brush.Parse("#9aa0a6");
        };

        // Handle button clicks
        btnMinimize.Click += (s, e) => { WindowState = WindowState.Minimized; };
        _btnMaximize.Click += (s, e) => { ToggleMaximize(); };
        btnClose.Click += (s, e) => { Close(); };

        buttonsPanel.Children.Add(btnMinimize);
        buttonsPanel.Children.Add(_btnMaximize);
        buttonsPanel.Children.Add(btnClose);

        Grid.SetColumn(buttonsPanel, 1);
        titleBarGrid.Children.Add(buttonsPanel);

        // Window drag and double click to maximize handling
        titleBarGrid.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(titleBarGrid).Properties.IsLeftButtonPressed)
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximize();
                }
                else
                {
                    _isTitleDragPending = true;
                    _titleDragStartPoint = e.GetPosition(this);
                    _windowStartPos = Position;
                    e.Pointer.Capture(titleBarGrid);
                }
                e.Handled = true;
            }
        };

        titleBarGrid.PointerReleased += (s, e) =>
        {
            if (_isTitleDragging)
            {
                e.Pointer.Capture(null);
                _isTitleDragging = false;
                EndTitleDrag(e);
                e.Handled = true;
            }
            else if (_isTitleDragPending)
            {
                e.Pointer.Capture(null);
                _isTitleDragPending = false;
                e.Handled = true;
            }
        };

        titleBarGrid.PointerCaptureLost += (s, e) =>
        {
            if (_isTitleDragging)
            {
                _isTitleDragging = false;
                CancelTitleDrag();
            }
            _isTitleDragPending = false;
        };

        titleBarGrid.PointerMoved += (s, e) =>
        {
            if (_isTitleDragging)
            {
                UpdateTitleDrag(e);
                e.Handled = true;
            }
            else if (_isTitleDragPending)
            {
                var diff = e.GetPosition(this) - _titleDragStartPoint;
                var dist = Math.Sqrt(diff.X * diff.X + diff.Y * diff.Y);
                if (dist > 6)
                {
                    _isTitleDragPending = false;
                    _isTitleDragging = true;
                    StartTitleDrag(e);
                }
                e.Handled = true;
            }
        };

        // Grid hosting custom title bar + main content
        var mainLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *")
        };
        Grid.SetRow(titleBarGrid, 0);
        mainLayout.Children.Add(titleBarGrid);

        Grid.SetRow(_superSplit, 1);
        mainLayout.Children.Add(_superSplit);

        // Window border frame
        var windowBorder = new Border
        {
            BorderBrush = Brush.Parse("#3c4043"),
            BorderThickness = new Thickness(1),
            Background = Brush.Parse("#202124"),
            Child = mainLayout
        };

        Content = windowBorder;
    }

    private PathIcon CreateMinimizeIcon() => new() { Width = 10, Height = 10, Foreground = Brush.Parse("#9aa0a6"), Data = Geometry.Parse("M 1 4.5 H 9 V 5.5 H 1 Z") };
    private PathIcon CreateMaximizeIcon() => new() { Width = 10, Height = 10, Foreground = Brush.Parse("#9aa0a6"), Data = Geometry.Parse("M 1 1 H 9 V 9 H 1 Z M 2 2 H 8 V 8 H 2 Z") };
    private PathIcon CreateRestoreIcon() => new() { Width = 10, Height = 10, Foreground = Brush.Parse("#9aa0a6"), Data = Geometry.Parse("M 3 1 H 9 V 7 H 3 Z M 4 2 H 8 V 6 H 4 Z M 1 3 H 7 V 9 H 1 Z M 2 4 H 6 V 8 H 2 Z") };
    private PathIcon CreateCloseIcon() => new() { Width = 8, Height = 8, Foreground = Brush.Parse("#9aa0a6"), Data = Geometry.Parse("M 1.5 2 L 2 1.5 L 4 3.5 L 6 1.5 L 6.5 2 L 4.5 4 L 6.5 6 L 6 6.5 L 4 4.5 L 2 6.5 L 1.5 6 L 3.5 4 Z") };

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void UpdateMaximizeButtonIcon()
    {
        if (_btnMaximize != null)
        {
            _btnMaximize.Content = WindowState == WindowState.Maximized ? CreateRestoreIcon() : CreateMaximizeIcon();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            UpdateMaximizeButtonIcon();
        }
    }

    private void OnLayoutRebuilt(object? sender, EventArgs e)
    {
        if (_superSplit.Root == null)
        {
            _superSplit.LayoutRebuilt -= OnLayoutRebuilt;
            Close();
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_superSplit.Root != null)
        {
            var nodeToMove = _superSplit.Root;
            _superSplit.Root = null; // Detach from floating split

            if (_mainSplit.Root == null)
            {
                _mainSplit.Root = nodeToMove;
            }
            else
            {
                var newRoot = new SplitContainerNode(Orientation.Horizontal, _mainSplit.Root, nodeToMove)
                {
                    SplitterRatio = 0.7
                };
                _mainSplit.Root = newRoot;
            }
            _mainSplit.Rebuild();
        }
    }

    private void StartTitleDrag(PointerEventArgs e)
    {
        if (_superSplit.Root is not BoxNode draggedBox) return;

        SuperSplitDragManager.IsDragging = true;
        SuperSplitDragManager.SourceNode = draggedBox;
        SuperSplitDragManager.SourceTab = null;
        SuperSplitDragManager.SourceSplit = _superSplit;
        SuperSplitDragManager.DraggedNode = draggedBox;

        Opacity = 0.5;
        _currentActiveDragTargetSplit = null;
        SuperSplitDragManager.IsOverDropTarget = false;
        _currentDropTargetNode = null;
        _currentDropLocation = RelativeDropLocation.None;
    }

    private void UpdateTitleDrag(PointerEventArgs e)
    {
        var currentPos = e.GetPosition(this);
        var screenPos = this.PointToScreen(currentPos);

        Position = new PixelPoint(screenPos.X - (int)_titleDragStartPoint.X, screenPos.Y - (int)_titleDragStartPoint.Y);

        SuperSplit? targetSplit = null;
        SuperSplitBox? targetBox = null;
        Point targetRelativePos = default;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var w in desktop.Windows)
            {
                if (w == this || !w.IsVisible) continue;

                var wPos = w.Position;
                var wSize = w.ClientSize;
                var wRect = new Rect(wPos.X, wPos.Y, wSize.Width, wSize.Height);

                if (wRect.Contains(new Point(screenPos.X, screenPos.Y)))
                {
                    var ss = FindSuperSplitInVisual(w);
                    if (ss != null)
                    {
                        var wClientPos = w.PointToClient(screenPos);
                        var topLeft = ss.TranslatePoint(new Point(0, 0), w);
                        if (topLeft.HasValue)
                        {
                            var ssRelativePos = wClientPos - topLeft.Value;
                            var box = ss.FindBoxAtPosition(ssRelativePos);
                            if (box != null && box.DataContext is BoxNode targetNode && targetNode != SuperSplitDragManager.DraggedNode)
                            {
                                targetSplit = ss;
                                targetBox = box;
                                targetRelativePos = ssRelativePos;
                                break;
                            }
                        }
                    }
                }
            }
        }

        if (targetSplit != null && targetBox != null)
        {
            if (_currentActiveDragTargetSplit != null && _currentActiveDragTargetSplit != targetSplit)
            {
                _currentActiveDragTargetSplit.HideDropHighlight();
            }

            _currentActiveDragTargetSplit = targetSplit;

            var boxBounds = targetBox.Bounds;
            var boxTopLeft = targetBox.TranslatePoint(new Point(0, 0), targetSplit);
            if (boxTopLeft.HasValue)
            {
                double x = targetRelativePos.X - boxTopLeft.Value.X;
                double y = targetRelativePos.Y - boxTopLeft.Value.Y;
                double w = boxBounds.Width;
                double h = boxBounds.Height;

                double normX = x / w;
                double normY = y / h;

                RelativeDropLocation loc;
                if (normX >= 0.25 && normX <= 0.75 && normY >= 0.25 && normY <= 0.75)
                {
                    loc = RelativeDropLocation.Center;
                }
                else if (normX < normY)
                {
                    if (normX < 1.0 - normY)
                        loc = RelativeDropLocation.Left;
                    else
                        loc = RelativeDropLocation.Bottom;
                }
                else
                {
                    if (normX < 1.0 - normY)
                        loc = RelativeDropLocation.Top;
                    else
                        loc = RelativeDropLocation.Right;
                }

                targetSplit.ShowDropHighlight(boxTopLeft.Value, w, h, loc);
                SuperSplitDragManager.IsOverDropTarget = true;
                _currentDropTargetNode = targetBox.DataContext as BoxNode;
                _currentDropLocation = loc;
            }
        }
        else
        {
            if (_currentActiveDragTargetSplit != null)
            {
                _currentActiveDragTargetSplit.HideDropHighlight();
                _currentActiveDragTargetSplit = null;
            }
            SuperSplitDragManager.IsOverDropTarget = false;
            _currentDropTargetNode = null;
            _currentDropLocation = RelativeDropLocation.None;
        }
    }

    private void EndTitleDrag(PointerEventArgs e)
    {
        Opacity = 1.0;

        if (_currentActiveDragTargetSplit != null)
        {
            _currentActiveDragTargetSplit.HideDropHighlight();
        }

        if (_currentDropTargetNode != null && _currentActiveDragTargetSplit != null)
        {
            var sourceNode = SuperSplitDragManager.DraggedNode;
            if (sourceNode != null)
            {
                var targetSplit = _currentActiveDragTargetSplit;
                var sourceSplit = _superSplit;
                targetSplit.MoveNodeCrossWindow(sourceNode, _currentDropTargetNode, _currentDropLocation, sourceSplit);
            }
        }

        _currentActiveDragTargetSplit = null;
        _currentDropTargetNode = null;
        _currentDropLocation = RelativeDropLocation.None;
        SuperSplitDragManager.Reset();
    }

    private void CancelTitleDrag()
    {
        Opacity = 1.0;

        if (_currentActiveDragTargetSplit != null)
        {
            _currentActiveDragTargetSplit.HideDropHighlight();
            _currentActiveDragTargetSplit = null;
        }

        _currentDropTargetNode = null;
        _currentDropLocation = RelativeDropLocation.None;
        SuperSplitDragManager.Reset();
    }

    private SuperSplit? FindSuperSplitInVisual(Visual visual)
    {
        if (visual is SuperSplit ss) return ss;
        foreach (var child in visual.GetVisualChildren())
        {
            var found = FindSuperSplitInVisual(child);
            if (found != null) return found;
        }
        return null;
    }
}
