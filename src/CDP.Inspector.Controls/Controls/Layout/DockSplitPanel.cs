using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CdpInspectorApp.Controls;

[TemplatePart("PART_HeaderButton", typeof(Button))]
[TemplatePart("PART_PinButton", typeof(Button))]
[TemplatePart("PART_OverlayGrip", typeof(Control))]
[PseudoClasses(":expanded", ":collapsed", ":pinned", ":unpinned")]
public class DockSplitPanel : TemplatedControl
{
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<DockSplitPanel, object?>(nameof(Header));

    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<DockSplitPanel, IDataTemplate?>(nameof(HeaderTemplate));

    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<DockSplitPanel, object?>(nameof(Content));

    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<DockSplitPanel, IDataTemplate?>(nameof(ContentTemplate));

    public static readonly StyledProperty<object?> MainContentProperty =
        AvaloniaProperty.Register<DockSplitPanel, object?>(nameof(MainContent));

    public static readonly StyledProperty<IDataTemplate?> MainContentTemplateProperty =
        AvaloniaProperty.Register<DockSplitPanel, IDataTemplate?>(nameof(MainContentTemplate));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<DockSplitPanel, bool>(nameof(IsExpanded), false);

    public static readonly StyledProperty<bool> IsPinnedProperty =
        AvaloniaProperty.Register<DockSplitPanel, bool>(nameof(IsPinned), true);

    public static readonly StyledProperty<double> PanelHeightProperty =
        AvaloniaProperty.Register<DockSplitPanel, double>(nameof(PanelHeight), 220.0);

    public static readonly StyledProperty<double> MinPanelHeightProperty =
        AvaloniaProperty.Register<DockSplitPanel, double>(nameof(MinPanelHeight), 80.0);

    public static readonly StyledProperty<double> MaxPanelHeightProperty =
        AvaloniaProperty.Register<DockSplitPanel, double>(nameof(MaxPanelHeight), 600.0);

    private bool _isDraggingGrip;
    private Point _gripStartPoint;
    private double _gripStartHeight;

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public IDataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    public object? MainContent
    {
        get => GetValue(MainContentProperty);
        set => SetValue(MainContentProperty, value);
    }

    public IDataTemplate? MainContentTemplate
    {
        get => GetValue(MainContentTemplateProperty);
        set => SetValue(MainContentTemplateProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public bool IsPinned
    {
        get => GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    public double PanelHeight
    {
        get => GetValue(PanelHeightProperty);
        set => SetValue(PanelHeightProperty, value);
    }

    public double MinPanelHeight
    {
        get => GetValue(MinPanelHeightProperty);
        set => SetValue(MinPanelHeightProperty, value);
    }

    public double MaxPanelHeight
    {
        get => GetValue(MaxPanelHeightProperty);
        set => SetValue(MaxPanelHeightProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(DockSplitPanel);

    public DockSplitPanel()
    {
        UpdatePseudoClasses(IsExpanded, IsPinned);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsExpandedProperty || change.Property == IsPinnedProperty)
        {
            UpdatePseudoClasses(IsExpanded, IsPinned);
        }
    }

    private void UpdatePseudoClasses(bool expanded, bool pinned)
    {
        PseudoClasses.Set(":expanded", expanded);
        PseudoClasses.Set(":collapsed", !expanded);
        PseudoClasses.Set(":pinned", pinned);
        PseudoClasses.Set(":unpinned", !pinned);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var headerBar = e.NameScope.Find<Control>("PART_HeaderBar");
        if (headerBar != null)
        {
            headerBar.PointerPressed += OnHeaderBarPointerPressed;
        }

        var btnToggleExpand = e.NameScope.Find<Button>("PART_HeaderButton");
        if (btnToggleExpand != null)
        {
            btnToggleExpand.Click += OnHeaderToggleClick;
        }

        var btnPin = e.NameScope.Find<Button>("PART_PinButton");
        if (btnPin != null)
        {
            btnPin.Click += OnPinToggleClick;
        }

        var grip = e.NameScope.Find<Control>("PART_OverlayGrip");
        if (grip != null)
        {
            grip.PointerPressed += OnGripPointerPressed;
            grip.PointerMoved += OnGripPointerMoved;
            grip.PointerReleased += OnGripPointerReleased;
            grip.PointerCaptureLost += OnGripPointerCaptureLost;
        }

        var contentHost = e.NameScope.Find<Control>("PART_ContentHost");
        if (contentHost != null)
        {
            contentHost.SizeChanged += (s, args) =>
            {
                if (IsPinned && IsExpanded && args.NewSize.Height > 0 && !_isDraggingGrip)
                {
                    var clamped = Math.Clamp(args.NewSize.Height, MinPanelHeight, MaxPanelHeight);
                    if (Math.Abs(PanelHeight - clamped) > 0.5)
                    {
                        SetCurrentValue(PanelHeightProperty, clamped);
                    }
                }
            };
        }
    }

    private void OnHeaderBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            IsExpanded = !IsExpanded;
            e.Handled = true;
        }
    }

    private void OnHeaderToggleClick(object? sender, RoutedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private void OnPinToggleClick(object? sender, RoutedEventArgs e)
    {
        IsPinned = !IsPinned;
    }

    private void OnGripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            _isDraggingGrip = true;
            _gripStartPoint = e.GetPosition(this);
            _gripStartHeight = PanelHeight;
            e.Pointer.Capture(control);
            e.Handled = true;
        }
    }

    private void OnGripPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingGrip)
        {
            var currentPos = e.GetPosition(this);
            var deltaY = _gripStartPoint.Y - currentPos.Y;
            var newHeight = Math.Clamp(_gripStartHeight + deltaY, MinPanelHeight, MaxPanelHeight);
            PanelHeight = newHeight;
            e.Handled = true;
        }
    }

    private void OnGripPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDraggingGrip && sender is Control control)
        {
            _isDraggingGrip = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnGripPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isDraggingGrip = false;
    }
}
