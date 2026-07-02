using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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
    private Point _titleDragStartPoint;
    private PixelPoint _windowStartPos;
    private SuperSplit? _currentActiveDragTargetSplit;
    private PointerPressedEventArgs? _pointerPressedEventArgs;

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

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

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
                    _pointerPressedEventArgs = e;
                    e.Pointer.Capture(titleBarGrid);
                }
                e.Handled = true;
            }
        };

        titleBarGrid.PointerReleased += (s, e) =>
        {
            if (_isTitleDragPending)
            {
                e.Pointer.Capture(null);
                _isTitleDragPending = false;
                e.Handled = true;
            }
        };

        titleBarGrid.PointerCaptureLost += (s, e) =>
        {
            _isTitleDragPending = false;
        };

        titleBarGrid.PointerMoved += async (s, e) =>
        {
            if (_isTitleDragPending)
            {
                var diff = e.GetPosition(this) - _titleDragStartPoint;
                var dist = Math.Sqrt(diff.X * diff.X + diff.Y * diff.Y);
                if (dist > 6)
                {
                    e.Pointer.Capture(null);
                    _isTitleDragPending = false;
                    await InitiateWindowDragAsync(e);
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

    private async System.Threading.Tasks.Task InitiateWindowDragAsync(PointerEventArgs e)
    {
        if (_superSplit.Root is not BoxNode draggedBox) return;

        SuperSplitDragManager.IsDragging = true;
        SuperSplitDragManager.SourceNode = draggedBox;
        SuperSplitDragManager.SourceTab = null;
        SuperSplitDragManager.SourceSplit = _superSplit;
        SuperSplitDragManager.DraggedNode = draggedBox;

        var dataObject = new DataTransfer();
        dataObject.Add(DataTransferItem.CreateText("SuperSplitDraggedNode"));

        double oldOpacity = Opacity;
        Opacity = 0.5;

        _currentActiveDragTargetSplit = null;
        SuperSplitDragManager.IsOverDropTarget = false;

        bool success = false;
        try
        {
            if (_pointerPressedEventArgs != null)
            {
                var result = await DragDrop.DoDragDropAsync(_pointerPressedEventArgs, dataObject, DragDropEffects.Move);
                success = (result == DragDropEffects.Move);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Window drag initiation failed: {ex.Message}");
        }

        Opacity = oldOpacity;

        if (_currentActiveDragTargetSplit != null)
        {
            _currentActiveDragTargetSplit.HideDropHighlight();
            _currentActiveDragTargetSplit = null;
        }

        if (success)
        {
            // Handled by OnLayoutRebuilt which closes this window
        }
        else
        {
            SuperSplitDragManager.Reset();
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (SuperSplitDragManager.IsDragging && SuperSplitDragManager.DraggedNode == _superSplit.Root)
        {
            var currentPos = e.GetPosition(this);
            var screenPos = this.PointToScreen(currentPos);

            // Move the window physically so it follows the mouse pointer
            Position = new PixelPoint(screenPos.X - (int)_titleDragStartPoint.X, screenPos.Y - (int)_titleDragStartPoint.Y);

            // Hit test other windows to find if we are over another SuperSplit's box
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
                    e.DragEffects = DragDropEffects.Move;
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
                e.DragEffects = DragDropEffects.None;
            }

            e.Handled = true;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (SuperSplitDragManager.IsDragging && SuperSplitDragManager.DraggedNode == _superSplit.Root)
        {
            if (SuperSplitDragManager.IsOverDropTarget && _currentActiveDragTargetSplit != null)
            {
                var screenPos = this.PointToScreen(e.GetPosition(this));
                var targetWindow = TopLevel.GetTopLevel(_currentActiveDragTargetSplit);
                if (targetWindow != null)
                {
                    var wClientPos = targetWindow.PointToClient(screenPos);
                    var topLeft = _currentActiveDragTargetSplit.TranslatePoint(new Point(0, 0), targetWindow);
                    if (topLeft.HasValue)
                    {
                        var ssRelativePos = wClientPos - topLeft.Value;
                        var box = _currentActiveDragTargetSplit.FindBoxAtPosition(ssRelativePos);

                        if (box != null && box.DataContext is BoxNode targetNode)
                        {
                            var boxBounds = box.Bounds;
                            var boxTopLeft = box.TranslatePoint(new Point(0, 0), _currentActiveDragTargetSplit);
                            if (boxTopLeft.HasValue)
                            {
                                double x = ssRelativePos.X - boxTopLeft.Value.X;
                                double y = ssRelativePos.Y - boxTopLeft.Value.Y;
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

                                var sourceNode = SuperSplitDragManager.DraggedNode;
                                _currentActiveDragTargetSplit.MoveNodeCrossWindow(sourceNode, targetNode, loc, _superSplit);

                                e.DragEffects = DragDropEffects.Move;
                                e.Handled = true;
                                return;
                            }
                        }
                    }
                }
            }

            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
        }
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
