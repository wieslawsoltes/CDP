using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Numerics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
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

    public bool IsEntranceAnimationRequested { get; set; } = false;

    private readonly Border _mainBorder;
    private readonly Border _headerPanel;
    private BoxNode? _currentBoxNode;

    public SuperSplitBox()
    {
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

        var tabsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
        _headerPanel.Child = tabsPanel;

        Grid.SetRow(_headerPanel, 0);
        grid.Children.Add(_headerPanel);

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
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
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
        RebuildHeaderTabs();
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
        var tabsPanel = _headerPanel.Child as StackPanel;
        if (tabsPanel == null) return;

        tabsPanel.Children.Clear();

        if (DataContext is BoxNode boxNode)
        {
            int count = boxNode.Tabs.Count;
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
                    boxNode.ActiveTab = tab;
                    BoxSelected?.Invoke(this, EventArgs.Empty);

                    var pointerProperties = args.GetCurrentPoint(tabBorder).Properties;
                    if (pointerProperties.IsRightButtonPressed)
                    {
                        MenuClicked?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        HeaderPressed?.Invoke(this, args);
                    }
                    args.Handled = true;
                };

                tabsPanel.Children.Add(tabBorder);
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var visual = ElementComposition.GetElementVisual(this);
        if (visual != null)
        {
            visual.CenterPoint = new Vector3((float)e.NewSize.Width / 2, (float)e.NewSize.Height / 2, 0);
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
        if (string.IsNullOrEmpty(viewName))
        {
            InnerContent = null;
            return;
        }

        var superSplit = this.FindAncestorOfType<SuperSplit>();
        if (superSplit?.ViewResolver != null)
        {
            var view = superSplit.ViewResolver(viewName, this);
            if (view != null)
            {
                if (view.Parent is SuperSplitBox oldParent && oldParent != this)
                {
                    oldParent.InnerContent = null;
                    oldParent.UpdateLayout();
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
            }
        }
    }

    private void UpdateBorderHighlight()
    {
        if (_mainBorder != null)
        {
            _mainBorder.BorderBrush = IsSelected ? Brush.Parse("#1a73e8") : Brush.Parse("#3c4043");
            _mainBorder.BorderThickness = IsSelected ? new Thickness(2.0) : new Thickness(1.5);
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
}
