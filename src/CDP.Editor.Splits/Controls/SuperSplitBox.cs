using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Numerics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CDP.Editor.Splits.Models;

namespace CDP.Editor.Splits.Controls;

public class SuperSplitBox : ContentControl
{
    public static readonly StyledProperty<string> HeaderTitleProperty =
        AvaloniaProperty.Register<SuperSplitBox, string>(nameof(HeaderTitle), string.Empty);

    public static readonly StyledProperty<string> IconKeyProperty =
        AvaloniaProperty.Register<SuperSplitBox, string>(nameof(IconKey), string.Empty);

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<SuperSplitBox, bool>(nameof(IsSelected), false);

    public static readonly StyledProperty<string?> BackgroundTintProperty =
        AvaloniaProperty.Register<SuperSplitBox, string?>(nameof(BackgroundTint), null);

    public static readonly StyledProperty<object?> InnerContentProperty =
        AvaloniaProperty.Register<SuperSplitBox, object?>(nameof(InnerContent), null);

    public static readonly StyledProperty<string?> SelectedViewNameProperty =
        AvaloniaProperty.Register<SuperSplitBox, string?>(nameof(SelectedViewName), null);

    public string? SelectedViewName
    {
        get => GetValue(SelectedViewNameProperty);
        set => SetValue(SelectedViewNameProperty, value);
    }

    public string HeaderTitle
    {
        get => GetValue(HeaderTitleProperty);
        set => SetValue(HeaderTitleProperty, value);
    }

    public string IconKey
    {
        get => GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public string? BackgroundTint
    {
        get => GetValue(BackgroundTintProperty);
        set => SetValue(BackgroundTintProperty, value);
    }

    public object? InnerContent
    {
        get => GetValue(InnerContentProperty);
        set => SetValue(InnerContentProperty, value);
    }

    public event EventHandler? MenuClicked;
    public event EventHandler? BoxSelected;
    public event EventHandler<PointerPressedEventArgs>? HeaderPressed;
    public event EventHandler<TabDragEventArgs>? TabDragStarted;

    public bool IsEntranceAnimationRequested { get; set; } = false;

    private readonly Border _mainBorder;
    private readonly Border _headerPanel;
    private BoxNode? _currentBoxNode;
    private readonly StackPanel _tabsPanel;
    private readonly ScrollViewer _tabsScrollViewer;
    private readonly Button _btnScrollLeft;
    private readonly Button _btnScrollRight;
    private readonly Button _btnZoom;
    private bool _isTabDragging;
    private BoxTabNode? _draggingTab;
    private PointerPressedEventArgs? _tabPressedEventArgs;
    private bool _isMovingTab;
    private readonly StackPanel _singleTabHeaderPanel;
    private readonly PathIcon _singleTabIcon;
    private readonly TextBlock _singleTabTitle;

    public SuperSplitBox()
    {
        ClipToBounds = true;

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *"),
            ClipToBounds = true
        };

        // Header Panel
        _headerPanel = new Border
        {
            Height = 32,
            Background = Brush.Parse("#292a2d"),
            BorderBrush = Brush.Parse("#3c4043"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(8, 8, 0, 0)
        };

        _headerPanel.DoubleTapped += (sender, e) =>
        {
            ToggleZoom();
            e.Handled = true;
        };

        _tabsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };

        _tabsScrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _tabsPanel,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };

        _tabsScrollViewer.PointerWheelChanged += (sender, e) =>
        {
            double delta = e.Delta.Y * 40;
            _tabsScrollViewer.Offset = new Point(Math.Max(0, Math.Min(_tabsScrollViewer.Offset.X - delta, _tabsScrollViewer.Extent.Width - _tabsScrollViewer.Viewport.Width)), 0);
            e.Handled = true;
        };

        _btnScrollLeft = new Button
        {
            Width = 20,
            Height = 32,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            Background = Brush.Parse("#202124"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            IsVisible = false
        };
        var leftIcon = new PathIcon
        {
            Width = 10,
            Height = 10,
            Foreground = Brush.Parse("#8ab4f8")
        };
        if (Application.Current != null && Application.Current.TryFindResource("ChevronLeftIcon", out var leftRes) && leftRes is Geometry leftGeom)
        {
            leftIcon.Data = leftGeom;
        }
        _btnScrollLeft.Content = leftIcon;

        _btnScrollRight = new Button
        {
            Width = 20,
            Height = 32,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            Background = Brush.Parse("#202124"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            IsVisible = false
        };
        var rightIcon = new PathIcon
        {
            Width = 10,
            Height = 10,
            Foreground = Brush.Parse("#8ab4f8")
        };
        if (Application.Current != null && Application.Current.TryFindResource("ChevronRightIcon", out var rightRes) && rightRes is Geometry rightGeom)
        {
            rightIcon.Data = rightGeom;
        }
        _btnScrollRight.Content = rightIcon;

        _btnZoom = new Button
        {
            Name = "btnZoom",
            Width = 32,
            Height = 32,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        AutomationProperties.SetAutomationId(_btnZoom, "btnZoom");
        AutomationProperties.SetName(_btnZoom, "Zoom Panel Button");
        ToolTip.SetTip(_btnZoom, "Zoom Panel");

        var zoomIcon = new PathIcon
        {
            Width = 10,
            Height = 10,
            Foreground = Brush.Parse("#8ab4f8")
        };
        if (Application.Current != null && Application.Current.TryFindResource("MaximizeIcon", out var zoomRes) && zoomRes is Geometry zoomGeom)
        {
            zoomIcon.Data = zoomGeom;
        }
        _btnZoom.Content = zoomIcon;

        _btnZoom.Click += (sender, e) =>
        {
            ToggleZoom();
        };

        _btnScrollLeft.Click += (sender, e) =>
        {
            double newOffset = Math.Max(0, _tabsScrollViewer.Offset.X - 100);
            _tabsScrollViewer.Offset = new Point(newOffset, 0);
        };

        _btnScrollRight.Click += (sender, e) =>
        {
            double newOffset = Math.Min(_tabsScrollViewer.Extent.Width - _tabsScrollViewer.Viewport.Width, _tabsScrollViewer.Offset.X + 100);
            _tabsScrollViewer.Offset = new Point(newOffset, 0);
        };

        _tabsScrollViewer.PropertyChanged += (sender, e) =>
        {
            if (e.Property == ScrollViewer.OffsetProperty ||
                e.Property == ScrollViewer.ExtentProperty ||
                e.Property == ScrollViewer.ViewportProperty)
            {
                UpdateScrollButtonsVisibility();
            }
        };

        _singleTabHeaderPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(12, 0),
            IsVisible = false
        };

        _singleTabIcon = new PathIcon
        {
            Width = 12,
            Height = 12,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        _singleTabTitle = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeight.Normal,
            Foreground = Brush.Parse("#e8eaed"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        _singleTabHeaderPanel.Children.Add(_singleTabIcon);
        _singleTabHeaderPanel.Children.Add(_singleTabTitle);

        var headerLayoutGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto, Auto"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };

        Grid.SetColumn(_btnScrollLeft, 0);
        Grid.SetColumn(_tabsScrollViewer, 1);
        Grid.SetColumn(_btnScrollRight, 2);
        Grid.SetColumn(_btnZoom, 3);
        Grid.SetColumn(_singleTabHeaderPanel, 1);

        headerLayoutGrid.Children.Add(_btnScrollLeft);
        headerLayoutGrid.Children.Add(_tabsScrollViewer);
        headerLayoutGrid.Children.Add(_btnScrollRight);
        headerLayoutGrid.Children.Add(_btnZoom);
        headerLayoutGrid.Children.Add(_singleTabHeaderPanel);

        _headerPanel.Child = headerLayoutGrid;

        Grid.SetRow(_headerPanel, 0);
        grid.Children.Add(_headerPanel);

        _headerPanel.PointerPressed += (sender, e) =>
        {
            if (e.GetCurrentPoint(_headerPanel).Properties.IsLeftButtonPressed)
            {
                // Drag the panel if the click source is not one of the control buttons
                if (e.Source != _btnZoom && e.Source != _btnScrollLeft && e.Source != _btnScrollRight)
                {
                    if (DataContext is BoxNode boxNode)
                    {
                        HeaderPressed?.Invoke(this, e);
                    }
                }
            }
        };

        // Content Area
        var contentPresenter = new ContentPresenter
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
        contentPresenter.Bind(ContentPresenter.ContentProperty, this.GetObservable(InnerContentProperty));
        Grid.SetRow(contentPresenter, 1);
        grid.Children.Add(contentPresenter);

        // Selected Border Outline
        _mainBorder = new Border
        {
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(10),
            ClipToBounds = false,
            Margin = new Thickness(2),
            Child = grid,
            Transitions = new Transitions
            {
                new BrushTransition
                {
                    Property = Border.BorderBrushProperty,
                    Duration = TimeSpan.FromMilliseconds(200)
                },
                new ThicknessTransition
                {
                    Property = Border.BorderThicknessProperty,
                    Duration = TimeSpan.FromMilliseconds(200)
                }
            }
        };

        UpdateBorderHighlight();
        UpdateBackground();

        AutomationProperties.SetHelpText(this, "Split Box Pane");
        AutomationProperties.SetName(this, "Split Box Pane");

        // Use tunneling PointerPressed handler to capture clicks anywhere inside the box (even handled by children)
        AddHandler(PointerPressedEvent, (sender, args) =>
        {
            BoxSelected?.Invoke(this, EventArgs.Empty);
        }, RoutingStrategies.Tunnel);

        // Set main border as the control's content
        Content = _mainBorder;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateViewContent();

        if (IsEntranceAnimationRequested)
        {
            IsEntranceAnimationRequested = false;
            AnimateEntranceComposition();
        }

        _currentBoxNode = DataContext as BoxNode;
        if (_currentBoxNode != null)
        {
            // Unsubscribe first to avoid double subscriptions
            _currentBoxNode.Tabs.CollectionChanged -= OnTabsCollectionChanged;
            _currentBoxNode.Tabs.CollectionChanged += OnTabsCollectionChanged;
            _currentBoxNode.PropertyChanged -= OnBoxNodePropertyChanged;
            _currentBoxNode.PropertyChanged += OnBoxNodePropertyChanged;
        }
        RebuildHeaderTabs();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isTabDragging = false;
        _draggingTab = null;
        _tabPressedEventArgs = null;
        if (_currentBoxNode != null)
        {
            _currentBoxNode.Tabs.CollectionChanged -= OnTabsCollectionChanged;
            _currentBoxNode.PropertyChanged -= OnBoxNodePropertyChanged;
            _currentBoxNode = null;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_currentBoxNode != null)
        {
            _currentBoxNode.Tabs.CollectionChanged -= OnTabsCollectionChanged;
            _currentBoxNode.PropertyChanged -= OnBoxNodePropertyChanged;
        }

        _currentBoxNode = DataContext as BoxNode;

        if (_currentBoxNode != null)
        {
            _currentBoxNode.Tabs.CollectionChanged += OnTabsCollectionChanged;
            _currentBoxNode.PropertyChanged += OnBoxNodePropertyChanged;
        }

        RebuildHeaderTabs();
    }

    private void OnTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            int oldIndex = e.OldStartingIndex;
            int newIndex = e.NewStartingIndex;
            if (oldIndex >= 0 && oldIndex < _tabsPanel.Children.Count &&
                newIndex >= 0 && newIndex < _tabsPanel.Children.Count)
            {
                var control = _tabsPanel.Children[oldIndex];
                _tabsPanel.Children.RemoveAt(oldIndex);
                _tabsPanel.Children.Insert(newIndex, control);
                UpdateTabCornerRadii();
                return;
            }
        }
        RebuildHeaderTabs();
    }

    private void UpdateTabCornerRadii()
    {
        int count = _tabsPanel.Children.Count;
        for (int i = 0; i < count; i++)
        {
            if (_tabsPanel.Children[i] is Border tabBorder)
            {
                var cornerRadius = new CornerRadius(0);
                if (i == 0 && i == count - 1)
                {
                    cornerRadius = new CornerRadius(8, 8, 0, 0);
                }
                else if (i == 0)
                {
                    cornerRadius = new CornerRadius(8, 0, 0, 0);
                }
                else if (i == count - 1)
                {
                    cornerRadius = new CornerRadius(0, 8, 0, 0);
                }
                tabBorder.CornerRadius = cornerRadius;
            }
        }
    }

    private void OnBoxNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BoxNode.ActiveTab))
        {
            RebuildHeaderTabs();
        }
    }

    private void RebuildHeaderTabs()
    {
        _tabsPanel.Children.Clear();

        if (DataContext is BoxNode boxNode)
        {
            int count = boxNode.Tabs.Count;
            _headerPanel.IsVisible = count > 0;
            if (count == 0)
            {
                _tabsScrollViewer.IsVisible = false;
                _btnScrollLeft.IsVisible = false;
                _btnScrollRight.IsVisible = false;
                _singleTabHeaderPanel.IsVisible = false;
                return;
            }

            if (count == 1)
            {
                _tabsScrollViewer.IsVisible = false;
                _btnScrollLeft.IsVisible = false;
                _btnScrollRight.IsVisible = false;
                _singleTabHeaderPanel.IsVisible = true;

                var tab = boxNode.Tabs[0];
                _singleTabTitle.Text = tab.Title;

                if (Application.Current != null && Application.Current.TryFindResource(tab.IconKey, out var resource) && resource is Geometry geom)
                {
                    _singleTabIcon.Data = geom;
                }
                else if (Application.Current != null && Application.Current.TryFindResource("DocumentIcon", out var fallback) && fallback is Geometry fallbackGeom)
                {
                    _singleTabIcon.Data = fallbackGeom;
                }
                return;
            }

            _tabsScrollViewer.IsVisible = true;
            _singleTabHeaderPanel.IsVisible = false;

            for (int i = 0; i < count; i++)
            {
                var tab = boxNode.Tabs[i];
                var isActive = tab == boxNode.ActiveTab;

                var cornerRadius = new CornerRadius(0);
                if (i == 0 && i == count - 1)
                {
                    cornerRadius = new CornerRadius(8, 8, 0, 0);
                }
                else if (i == 0)
                {
                    cornerRadius = new CornerRadius(8, 0, 0, 0);
                }
                else if (i == count - 1)
                {
                    cornerRadius = new CornerRadius(0, 8, 0, 0);
                }

                var tabBorder = new Border
                {
                    Background = isActive ? Brush.Parse("#35363a") : Brushes.Transparent,
                    BorderBrush = Brush.Parse("#3c4043"),
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    CornerRadius = cornerRadius,
                    Padding = new Thickness(12, 0),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };

                AutomationProperties.SetName(tabBorder, tab.Title + " Tab");
                AutomationProperties.SetHelpText(tabBorder, tab.Title + " Tab");

                // Add subtle hover effects to inactive tabs
                if (!isActive)
                {
                    tabBorder.PointerEntered += (s, e) =>
                    {
                        tabBorder.Background = Brush.Parse("#323337");
                    };
                    tabBorder.PointerExited += (s, e) =>
                    {
                        tabBorder.Background = Brushes.Transparent;
                    };
                }

                var tabGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto, Auto, Auto"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                };

                // 1. Icon
                var tabIcon = new PathIcon
                {
                    Width = 12,
                    Height = 12,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                if (Application.Current != null && Application.Current.TryFindResource(tab.IconKey, out var resource) && resource is Geometry geom)
                {
                    tabIcon.Data = geom;
                }
                else if (Application.Current != null && Application.Current.TryFindResource("DocumentIcon", out var fallback) && fallback is Geometry fallbackGeom)
                {
                    tabIcon.Data = fallbackGeom;
                }
                Grid.SetColumn(tabIcon, 0);
                tabGrid.Children.Add(tabIcon);

                // 2. Title Text
                var tabTitle = new TextBlock
                {
                    Text = tab.Title,
                    FontSize = 11,
                    FontWeight = isActive ? FontWeight.Bold : FontWeight.Normal,
                    Foreground = isActive ? Brush.Parse("#e8eaed") : Brush.Parse("#9aa0a6"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                Grid.SetColumn(tabTitle, 1);
                tabGrid.Children.Add(tabTitle);

                // 3. Close button (small x)
                var closeButton = new Border
                {
                    Width = 14,
                    Height = 14,
                    CornerRadius = new CornerRadius(7),
                    Background = Brushes.Transparent,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };

                var closeIcon = new PathIcon
                {
                    Width = 6,
                    Height = 6,
                    Foreground = Brush.Parse("#9aa0a6"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                if (Application.Current != null && Application.Current.TryFindResource("DismissIcon", out var crossGeom) && crossGeom is Geometry cross)
                {
                    closeIcon.Data = cross;
                }
                closeButton.Child = closeIcon;

                closeButton.PointerEntered += (s, e) =>
                {
                    closeButton.Background = Brush.Parse("#ff5252");
                    closeIcon.Foreground = Brushes.White;
                };
                closeButton.PointerExited += (s, e) =>
                {
                    closeButton.Background = Brushes.Transparent;
                    closeIcon.Foreground = Brush.Parse("#9aa0a6");
                };

                closeButton.PointerPressed += (s, e) =>
                {
                    boxNode.Tabs.Remove(tab);
                    e.Handled = true;
                };

                Grid.SetColumn(closeButton, 2);
                tabGrid.Children.Add(closeButton);

                tabBorder.Child = tabGrid;

                // Tab selection and drag press handling
                tabBorder.PointerPressed += (sender, args) =>
                {
                    var pointerProperties = args.GetCurrentPoint(tabBorder).Properties;
                    if (pointerProperties.IsRightButtonPressed)
                    {
                        boxNode.ActiveTab = tab;
                        BoxSelected?.Invoke(this, EventArgs.Empty);
                        MenuClicked?.Invoke(this, EventArgs.Empty);
                        args.Handled = true;
                    }
                    else if (pointerProperties.IsLeftButtonPressed)
                    {
                        boxNode.ActiveTab = tab;
                        BoxSelected?.Invoke(this, EventArgs.Empty);
                        _isTabDragging = true;
                        _draggingTab = tab;
                        _tabPressedEventArgs = args;
                        args.Pointer.Capture(tabBorder);
                        args.Handled = true;
                    }
                };

                tabBorder.PointerReleased += (sender, args) =>
                {
                    if (_isTabDragging && _draggingTab == tab)
                    {
                        args.Pointer.Capture(null);
                        _isTabDragging = false;
                        _draggingTab = null;
                        args.Handled = true;
                    }
                };

                tabBorder.PointerCaptureLost += (sender, args) =>
                {
                    if (_isMovingTab) return;

                    if (_isTabDragging && _draggingTab == tab)
                    {
                        _isTabDragging = false;
                        _draggingTab = null;
                    }
                };

                tabBorder.PointerMoved += (sender, args) =>
                {
                    if (_isTabDragging && _draggingTab == tab)
                    {
                        var posInHeader = args.GetPosition(_headerPanel);
                        bool isOutside = posInHeader.Y < -15 || posInHeader.Y > _headerPanel.Bounds.Height + 15 ||
                                         posInHeader.X < -50 || posInHeader.X > _headerPanel.Bounds.Width + 50;

                        if (isOutside)
                        {
                            args.Pointer.Capture(null);
                            _isTabDragging = false;
                            _draggingTab = null;
                            if (_tabPressedEventArgs != null)
                            {
                                TabDragStarted?.Invoke(this, new TabDragEventArgs(tab, _tabPressedEventArgs, args));
                            }
                        }
                        else
                        {
                            int fromIndex = boxNode.Tabs.IndexOf(tab);
                            if (fromIndex >= 0 && fromIndex < _tabsPanel.Children.Count)
                            {
                                Point posInTabs = args.GetPosition(_tabsPanel);
                                double x = posInTabs.X;

                                // Check left neighbor swap
                                if (fromIndex > 0)
                                {
                                    var prevChild = _tabsPanel.Children[fromIndex - 1] as Control;
                                    if (prevChild != null)
                                    {
                                        double prevMidX = prevChild.Bounds.X + prevChild.Bounds.Width / 2.0;
                                        if (x < prevMidX)
                                        {
                                            _isMovingTab = true;
                                            try
                                            {
                                                boxNode.Tabs.Move(fromIndex, fromIndex - 1);
                                                args.Pointer.Capture(tabBorder);
                                            }
                                            finally
                                            {
                                                _isMovingTab = false;
                                            }
                                            args.Handled = true;
                                            return;
                                        }
                                    }
                                }

                                // Check right neighbor swap
                                if (fromIndex < _tabsPanel.Children.Count - 1)
                                {
                                    var nextChild = _tabsPanel.Children[fromIndex + 1] as Control;
                                    if (nextChild != null)
                                    {
                                        double nextMidX = nextChild.Bounds.X + nextChild.Bounds.Width / 2.0;
                                        if (x > nextMidX)
                                        {
                                            _isMovingTab = true;
                                            try
                                            {
                                                boxNode.Tabs.Move(fromIndex, fromIndex + 1);
                                                args.Pointer.Capture(tabBorder);
                                            }
                                            finally
                                            {
                                                _isMovingTab = false;
                                            }
                                            args.Handled = true;
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                        args.Handled = true;
                    }
                };

                _tabsPanel.Children.Add(tabBorder);
            }

            if (boxNode.ActiveTab != null)
            {
                var activeTab = boxNode.ActiveTab;
                Dispatcher.UIThread.Post(() =>
                {
                    int activeIdx = boxNode.Tabs.IndexOf(activeTab);
                    if (activeIdx >= 0 && activeIdx < _tabsPanel.Children.Count)
                    {
                        var targetControl = _tabsPanel.Children[activeIdx] as Control;
                        if (targetControl != null)
                        {
                            var transform = targetControl.TransformToVisual(_tabsPanel);
                            if (transform.HasValue)
                            {
                                var relativePoint = new Point(0, 0).Transform(transform.Value);

                                double viewLeft = _tabsScrollViewer.Offset.X;
                                double viewRight = viewLeft + _tabsScrollViewer.Viewport.Width;

                                double tabLeft = relativePoint.X;
                                double tabRight = tabLeft + targetControl.Bounds.Width;

                                if (tabLeft < viewLeft)
                                {
                                    _tabsScrollViewer.Offset = new Point(Math.Max(0, tabLeft - 20), 0);
                                }
                                else if (tabRight > viewRight)
                                {
                                    double targetOffset = tabRight - _tabsScrollViewer.Viewport.Width + 20;
                                    _tabsScrollViewer.Offset = new Point(Math.Min(_tabsScrollViewer.Extent.Width - _tabsScrollViewer.Viewport.Width, targetOffset), 0);
                                }
                            }
                        }
                    }
                    UpdateScrollButtonsVisibility();
                }, DispatcherPriority.Render);
            }
            else
            {
                UpdateScrollButtonsVisibility();
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateScrollButtonsVisibility();
        var visual = ElementComposition.GetElementVisual(this);
        if (visual != null)
        {
            visual.CenterPoint = new Vector3((float)e.NewSize.Width / 2, (float)e.NewSize.Height / 2, 0);
        }
    }

    private void UpdateScrollButtonsVisibility()
    {
        double offset = _tabsScrollViewer.Offset.X;
        double extent = _tabsScrollViewer.Extent.Width;
        double viewport = _tabsScrollViewer.Viewport.Width;

        if (extent <= viewport)
        {
            _btnScrollLeft.IsVisible = false;
            _btnScrollRight.IsVisible = false;
        }
        else
        {
            _btnScrollLeft.IsVisible = offset > 0.1;
            _btnScrollRight.IsVisible = offset < (extent - viewport - 0.1);
        }
    }

    private void AnimateEntranceComposition()
    {
        var visual = ElementComposition.GetElementVisual(this);
        if (visual == null) return;

        var compositor = visual.Compositor;

        var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Target = "Scale";
        scaleAnimation.InsertKeyFrame(0.0f, new Vector3(0.95f, 0.95f, 1.0f));
        scaleAnimation.InsertKeyFrame(1.0f, new Vector3(1.0f, 1.0f, 1.0f));
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(250);

        var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Target = "Opacity";
        opacityAnimation.InsertKeyFrame(0.0f, 0.0f);
        opacityAnimation.InsertKeyFrame(1.0f, 1.0f);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(250);

        visual.StartAnimation("Scale", scaleAnimation);
        visual.StartAnimation("Opacity", opacityAnimation);
    }

    private void AnimateFocusPulse()
    {
        var visual = ElementComposition.GetElementVisual(this);
        if (visual == null) return;

        var compositor = visual.Compositor;

        var pulseAnimation = compositor.CreateVector3KeyFrameAnimation();
        pulseAnimation.Target = "Scale";
        pulseAnimation.InsertKeyFrame(0.0f, new Vector3(1.0f, 1.0f, 1.0f));
        pulseAnimation.InsertKeyFrame(0.5f, new Vector3(1.01f, 1.01f, 1.0f)); // subtle expansion
        pulseAnimation.InsertKeyFrame(1.0f, new Vector3(1.0f, 1.0f, 1.0f));
        pulseAnimation.Duration = TimeSpan.FromMilliseconds(200);

        visual.StartAnimation("Scale", pulseAnimation);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        BoxSelected?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            UpdateBorderHighlight();
            if (IsSelected)
            {
                AnimateFocusPulse();
            }
        }
        else if (change.Property == BackgroundTintProperty)
        {
            UpdateBackground();
        }
        else if (change.Property == IconKeyProperty || change.Property == HeaderTitleProperty)
        {
            RebuildHeaderTabs();
        }
        else if (change.Property == SelectedViewNameProperty)
        {
            UpdateViewContent();
        }
    }

    private void UpdateViewContent()
    {
        var viewName = SelectedViewName;
        System.Diagnostics.Debug.WriteLine($"[SuperSplitBox] UpdateViewContent called! viewName='{viewName}'");
        if (string.IsNullOrEmpty(viewName))
        {
            InnerContent = null;
            return;
        }

        var superSplit = this.FindAncestorOfType<SuperSplit>();
        System.Diagnostics.Debug.WriteLine($"[SuperSplitBox] FindAncestorOfType<SuperSplit>() found superSplit={(superSplit != null)}, ViewResolver={(superSplit?.ViewResolver != null)}");
        if (superSplit?.ViewResolver != null)
        {
            var view = superSplit.ViewResolver(viewName, this);
            System.Diagnostics.Debug.WriteLine($"[SuperSplitBox] ViewResolver returned view={(view != null ? view.GetType().Name : "null")}, view.Parent={(view?.Parent != null ? view.Parent.GetType().Name : "null")}");
            if (view != null)
            {
                if (view.Parent is SuperSplitBox oldParent && oldParent != this)
                {
                    oldParent.InnerContent = null;
                }
                else if (view.Parent is Panel panel)
                {
                    panel.Children.Remove(view);
                }
                else if (view.Parent is ContentControl contentControl)
                {
                    contentControl.Content = null;
                }

                InnerContent = view;
                view.Bind(StyledElement.DataContextProperty, new Avalonia.Data.Binding("DataContext") { Source = superSplit });
                System.Diagnostics.Debug.WriteLine($"[SuperSplitBox] Set InnerContent to view with DataContext binding!");
            }
        }
    }

    private void ToggleZoom()
    {
        var superSplit = this.FindAncestorOfType<SuperSplit>();
        var boxNode = DataContext as BoxNode;
        Console.WriteLine($"[SuperSplitBox] ToggleZoom clicked! parent found={superSplit != null}, boxNode={(boxNode != null ? boxNode.GetHashCode() : 0)}");
        if (superSplit != null && boxNode != null)
        {
            superSplit.ToggleZoomNode(boxNode);
        }
    }

    public void UpdateZoomButton(bool isZoomed)
    {
        var icon = _btnZoom.Content as PathIcon;
        if (icon != null && Application.Current != null)
        {
            var resKey = isZoomed ? "WindowMultipleIcon" : "MaximizeIcon";
            if (Application.Current.TryFindResource(resKey, out var res) && res is Geometry geom)
            {
                icon.Data = geom;
            }
        }
        ToolTip.SetTip(_btnZoom, isZoomed ? "Unzoom Panel" : "Zoom Panel");
    }

    private void UpdateBorderHighlight()
    {
        if (_mainBorder != null)
        {
            _mainBorder.BorderBrush = Brush.Parse("#3c4043");
            _mainBorder.BorderThickness = new Thickness(1.5);
        }
    }

    private void UpdateBackground()
    {
        if (_mainBorder != null)
        {
            var tint = BackgroundTint;
            if (!string.IsNullOrEmpty(tint) && Color.TryParse(tint, out var color))
            {
                _mainBorder.Background = new SolidColorBrush(color);
            }
            else
            {
                _mainBorder.Background = Brush.Parse("#292a2d");
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new ControlAutomationPeer(this);
    }
}

public class TabDragEventArgs : EventArgs
{
    public BoxTabNode Tab { get; }
    public PointerPressedEventArgs PressedArgs { get; }
    public PointerEventArgs MovedArgs { get; }

    public TabDragEventArgs(BoxTabNode tab, PointerPressedEventArgs pressedArgs, PointerEventArgs movedArgs)
    {
        Tab = tab;
        PressedArgs = pressedArgs;
        MovedArgs = movedArgs;
    }
}
