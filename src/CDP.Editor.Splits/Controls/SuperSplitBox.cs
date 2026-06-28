using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using System.Numerics;
using Avalonia.Rendering.Composition;
using Avalonia.Interactivity;
using Avalonia.Animation;

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
    private readonly PathIcon _iconPath;
    private readonly TextBlock _titleTextBlock;

    public SuperSplitBox()
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *"),
            ClipToBounds = true
        };

        // Header Panel
        var headerPanel = new Border
        {
            Height = 32,
            Background = Brush.Parse("#292a2d"),
            BorderBrush = Brush.Parse("#3c4043"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        headerPanel.PointerPressed += (sender, args) =>
        {
            BoxSelected?.Invoke(this, EventArgs.Empty);
            var pointerProperties = args.GetCurrentPoint(headerPanel).Properties;
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

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *")
        };

        // Header Icon
        _iconPath = new PathIcon
        {
            Width = 14,
            Height = 14,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(_iconPath, 0);
        headerGrid.Children.Add(_iconPath);

        // Header Text
        _titleTextBlock = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Foreground = Brush.Parse("#e8eaed"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(_titleTextBlock, 1);
        headerGrid.Children.Add(_titleTextBlock);

        headerPanel.Child = headerGrid;
        Grid.SetRow(headerPanel, 0);
        grid.Children.Add(headerPanel);

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
            ClipToBounds = true,
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
        UpdateIcon();
        UpdateTitle();

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
        
        var visual = ElementComposition.GetElementVisual(this);
        if (visual != null)
        {
            var compositor = visual.Compositor;
            var implicitAnimations = compositor.CreateImplicitAnimationCollection();
            
            var offsetAnim = compositor.CreateVector3KeyFrameAnimation();
            offsetAnim.Duration = TimeSpan.FromMilliseconds(250);
            offsetAnim.Target = "Offset";
            offsetAnim.InsertExpressionKeyFrame(1.0f, "this.FinalValue");
            implicitAnimations["Offset"] = offsetAnim;

            var sizeAnim = compositor.CreateVector2KeyFrameAnimation();
            sizeAnim.Duration = TimeSpan.FromMilliseconds(250);
            sizeAnim.Target = "Size";
            sizeAnim.InsertExpressionKeyFrame(1.0f, "this.FinalValue");
            implicitAnimations["Size"] = sizeAnim;

            visual.ImplicitAnimations = implicitAnimations;
        }

        if (IsEntranceAnimationRequested)
        {
            IsEntranceAnimationRequested = false;
            AnimateEntranceComposition();
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
        else if (change.Property == IconKeyProperty)
        {
            UpdateIcon();
        }
        else if (change.Property == HeaderTitleProperty)
        {
            UpdateTitle();
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

    private void UpdateIcon()
    {
        if (_iconPath != null)
        {
            var key = IconKey;
            if (Application.Current != null && Application.Current.TryFindResource(key, out var resource) && resource is Geometry geom)
            {
                _iconPath.Data = geom;
            }
            else if (Application.Current != null && Application.Current.TryFindResource("DocumentIcon", out var fallback) && fallback is Geometry fallbackGeom)
            {
                _iconPath.Data = fallbackGeom;
            }
            else
            {
                _iconPath.Data = null;
            }
        }
    }

    private void UpdateTitle()
    {
        if (_titleTextBlock != null)
        {
            _titleTextBlock.Text = HeaderTitle;
        }
    }
}
