using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace CdpInspectorApp.Controls;

public class PulsingStatusBadge : Control
{
    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<PulsingStatusBadge, string>(nameof(StatusText), "DISCONNECTED");

    public static readonly StyledProperty<Color> StatusColorProperty =
        AvaloniaProperty.Register<PulsingStatusBadge, Color>(nameof(StatusColor), Colors.Red);

    public static readonly StyledProperty<bool> IsPulsingProperty =
        AvaloniaProperty.Register<PulsingStatusBadge, bool>(nameof(IsPulsing), false);

    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public Color StatusColor
    {
        get => GetValue(StatusColorProperty);
        set => SetValue(StatusColorProperty, value);
    }

    public bool IsPulsing
    {
        get => GetValue(IsPulsingProperty);
        set => SetValue(IsPulsingProperty, value);
    }

    private double _pulseOpacity = 0.5;
    private double _pulseRadiusFactor = 1.0;
    private DispatcherTimer? _animationTimer;

    static PulsingStatusBadge()
    {
        AffectsRender<PulsingStatusBadge>(
            StatusTextProperty,
            StatusColorProperty,
            IsPulsingProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateAnimationState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopAnimation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPulsingProperty)
        {
            UpdateAnimationState();
        }
    }

    private void UpdateAnimationState()
    {
        if (IsPulsing)
        {
            StartAnimation();
        }
        else
        {
            StopAnimation();
        }
    }

    private void StartAnimation()
    {
        if (_animationTimer != null) return;

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        if (_animationTimer == null) return;

        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer = null;
        _pulseOpacity = 0.5;
        _pulseRadiusFactor = 1.0;
        InvalidateVisual();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        double ms = now.Millisecond + now.Second * 1000;
        double factor = (ms % 1500) / 1500.0;

        _pulseOpacity = 1.0 - factor;
        _pulseRadiusFactor = 1.0 + (factor * 0.8);

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // 1. Draw Capsule Pill Background
        var bgBrush = new SolidColorBrush(Color.FromArgb((byte)(StatusColor.A * 0.12), StatusColor.R, StatusColor.G, StatusColor.B));
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(StatusColor.A * 0.25), StatusColor.R, StatusColor.G, StatusColor.B)), 1.0);
        context.DrawRectangle(bgBrush, borderPen, new Rect(0, 0, w, h), h / 2.0, h / 2.0);

        // 2. Draw Static Status Dot
        double dotRadius = 4.0;
        var dotCenter = new Point(h / 2.0 + 2.0, h / 2.0);
        var dotBrush = new SolidColorBrush(StatusColor);
        context.DrawEllipse(dotBrush, null, dotCenter, dotRadius, dotRadius);

        // 3. Draw Pulsing Ring
        if (IsPulsing)
        {
            var pulseBrush = new SolidColorBrush(Color.FromArgb((byte)(_pulseOpacity * 255), StatusColor.R, StatusColor.G, StatusColor.B));
            context.DrawEllipse(pulseBrush, null, dotCenter, dotRadius * _pulseRadiusFactor, dotRadius * _pulseRadiusFactor);
        }

        // 4. Draw Status Text
        var textBrush = new SolidColorBrush(Color.Parse("#e8eaed"));
        var formattedText = new FormattedText(
            StatusText.ToUpperInvariant(),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
            10.0,
            textBrush
        );

        context.DrawText(formattedText, new Point(dotCenter.X + 10.0, h / 2.0 - formattedText.Height / 2.0));
    }
}
