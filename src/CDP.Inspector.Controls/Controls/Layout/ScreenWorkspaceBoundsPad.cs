using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace CdpInspectorApp.Controls;

public class ScreenWorkspaceBoundsPad : Control
{
    public static readonly StyledProperty<int> WindowXProperty =
        AvaloniaProperty.Register<ScreenWorkspaceBoundsPad, int>(nameof(WindowX), 0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> WindowYProperty =
        AvaloniaProperty.Register<ScreenWorkspaceBoundsPad, int>(nameof(WindowY), 0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> WindowWidthProperty =
        AvaloniaProperty.Register<ScreenWorkspaceBoundsPad, int>(nameof(WindowWidth), 800, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> WindowHeightProperty =
        AvaloniaProperty.Register<ScreenWorkspaceBoundsPad, int>(nameof(WindowHeight), 600, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> ScreenWidthProperty =
        AvaloniaProperty.Register<ScreenWorkspaceBoundsPad, int>(nameof(ScreenWidth), 1920);

    public static readonly StyledProperty<int> ScreenHeightProperty =
        AvaloniaProperty.Register<ScreenWorkspaceBoundsPad, int>(nameof(ScreenHeight), 1080);

    public int WindowX
    {
        get => GetValue(WindowXProperty);
        set => SetValue(WindowXProperty, value);
    }

    public int WindowY
    {
        get => GetValue(WindowYProperty);
        set => SetValue(WindowYProperty, value);
    }

    public int WindowWidth
    {
        get => GetValue(WindowWidthProperty);
        set => SetValue(WindowWidthProperty, value);
    }

    public int WindowHeight
    {
        get => GetValue(WindowHeightProperty);
        set => SetValue(WindowHeightProperty, value);
    }

    public int ScreenWidth
    {
        get => GetValue(ScreenWidthProperty);
        set => SetValue(ScreenWidthProperty, value);
    }

    public int ScreenHeight
    {
        get => GetValue(ScreenHeightProperty);
        set => SetValue(ScreenHeightProperty, value);
    }

    private bool _isDragging;
    private bool _isResizing;
    private Point _lastPointerPosition;
    private int _startWindowX;
    private int _startWindowY;
    private int _startWindowWidth;
    private int _startWindowHeight;

    private Rect _lastScreenRect;
    private Rect _lastWindowRect;
    private Rect _lastResizeHandleRect;

    static ScreenWorkspaceBoundsPad()
    {
        AffectsRender<ScreenWorkspaceBoundsPad>(
            WindowXProperty,
            WindowYProperty,
            WindowWidthProperty,
            WindowHeightProperty,
            ScreenWidthProperty,
            ScreenHeightProperty);
    }

    public ScreenWorkspaceBoundsPad()
    {
        ClipToBounds = true;
    }

    private bool CalculateLayout(out Rect screenRect, out Rect windowRect, out Rect resizeHandleRect)
    {
        double w = Bounds.Width;
        double h = Bounds.Height;

        if (w <= 0 || h <= 0 || ScreenWidth <= 0 || ScreenHeight <= 0)
        {
            screenRect = new Rect();
            windowRect = new Rect();
            resizeHandleRect = new Rect();
            return false;
        }

        double screenAspect = (double)ScreenWidth / ScreenHeight;
        double controlAspect = w / h;

        double padW, padH;
        if (screenAspect > controlAspect)
        {
            padW = w - 16;
            padH = padW / screenAspect;
        }
        else
        {
            padH = h - 16;
            padW = padH * screenAspect;
        }

        double offsetX = (w - padW) / 2.0;
        double offsetY = (h - padH) / 2.0;
        screenRect = new Rect(offsetX, offsetY, padW, padH);

        double scaleX = padW / ScreenWidth;
        double scaleY = padH / ScreenHeight;

        double winX = offsetX + (WindowX * scaleX);
        double winY = offsetY + (WindowY * scaleY);
        double winW = WindowWidth * scaleX;
        double winH = WindowHeight * scaleY;

        windowRect = new Rect(winX, winY, winW, winH);

        double handleSize = 8.0;
        resizeHandleRect = new Rect(windowRect.Right - handleSize / 2.0, windowRect.Bottom - handleSize / 2.0, handleSize, handleSize);
        return true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed) return;

        if (!CalculateLayout(out _lastScreenRect, out _lastWindowRect, out _lastResizeHandleRect)) return;

        var pos = e.GetPosition(this);
        if (_lastResizeHandleRect.Contains(pos))
        {
            _isResizing = true;
            _isDragging = false;
            _lastPointerPosition = pos;
            _startWindowWidth = WindowWidth;
            _startWindowHeight = WindowHeight;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else if (_lastWindowRect.Contains(pos))
        {
            _isDragging = true;
            _isResizing = false;
            _lastPointerPosition = pos;
            _startWindowX = WindowX;
            _startWindowY = WindowY;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var pos = e.GetPosition(this);

        if (_isDragging || _isResizing)
        {
            if (!CalculateLayout(out _lastScreenRect, out _lastWindowRect, out _lastResizeHandleRect)) return;

            double scaleX = _lastScreenRect.Width / ScreenWidth;
            double scaleY = _lastScreenRect.Height / ScreenHeight;

            double deltaX = pos.X - _lastPointerPosition.X;
            double deltaY = pos.Y - _lastPointerPosition.Y;

            if (_isDragging)
            {
                int screenDeltaX = (int)Math.Round(deltaX / scaleX);
                int screenDeltaY = (int)Math.Round(deltaY / scaleY);

                WindowX = Math.Clamp(_startWindowX + screenDeltaX, 0, ScreenWidth - WindowWidth);
                WindowY = Math.Clamp(_startWindowY + screenDeltaY, 0, ScreenHeight - WindowHeight);
            }
            else if (_isResizing)
            {
                int screenDeltaW = (int)Math.Round(deltaX / scaleX);
                int screenDeltaH = (int)Math.Round(deltaY / scaleY);

                WindowWidth = Math.Max(100, Math.Min(_startWindowWidth + screenDeltaW, ScreenWidth - WindowX));
                WindowHeight = Math.Max(100, Math.Min(_startWindowHeight + screenDeltaH, ScreenHeight - WindowY));
            }

            e.Handled = true;
        }
        else
        {
            if (CalculateLayout(out _lastScreenRect, out _lastWindowRect, out _lastResizeHandleRect))
            {
                if (_lastResizeHandleRect.Contains(pos))
                {
                    Cursor = new Cursor(StandardCursorType.TopLeftCorner);
                }
                else if (_lastWindowRect.Contains(pos))
                {
                    Cursor = new Cursor(StandardCursorType.Hand);
                }
                else
                {
                    Cursor = Cursor.Default;
                }
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging || _isResizing)
        {
            _isDragging = false;
            _isResizing = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!CalculateLayout(out var screenRect, out var windowRect, out var resizeHandleRect)) return;

        // 1. Draw Screen Monitor Backdrop
        var bgBrush = new SolidColorBrush(Color.Parse("#202124"));
        var borderPen = new Pen(new SolidColorBrush(Color.Parse("#3c4043")), 1.5);
        context.DrawRectangle(bgBrush, borderPen, screenRect, 4, 4);

        // Draw grid lines inside screen backdrop
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x15, 0xff, 0xff, 0xff)), 1.0);
        double gridStep = screenRect.Width / 10.0;
        for (int i = 1; i < 10; i++)
        {
            double x = screenRect.Left + i * gridStep;
            context.DrawLine(gridPen, new Point(x, screenRect.Top), new Point(x, screenRect.Bottom));
        }
        gridStep = screenRect.Height / 10.0;
        for (int i = 1; i < 10; i++)
        {
            double y = screenRect.Top + i * gridStep;
            context.DrawLine(gridPen, new Point(screenRect.Left, y), new Point(screenRect.Right, y));
        }

        // 2. Draw Simulated Target Window
        var winBg = new SolidColorBrush(Color.FromArgb(0x26, 0x1a, 0x73, 0xe8)); // 15% opacity primary
        var winBorder = new Pen(new SolidColorBrush(Color.Parse("#1a73e8")), 1.5);
        context.DrawRectangle(winBg, winBorder, windowRect, 2, 2);

        // 3. Draw Resize Handle
        var handleBrush = new SolidColorBrush(Color.Parse("#1a73e8"));
        context.DrawRectangle(handleBrush, null, resizeHandleRect, 1, 1);

        // 4. Render Dimensions Text Overlay
        string dimText = $"{WindowWidth} × {WindowHeight}";
        var formattedText = new FormattedText(
            dimText,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            10.0,
            new SolidColorBrush(Color.Parse("#e8eaed"))
        );

        var textPoint = new Point(
            windowRect.Center.X - formattedText.Width / 2.0,
            windowRect.Center.Y - formattedText.Height / 2.0
        );

        if (windowRect.Width > formattedText.Width + 10 && windowRect.Height > formattedText.Height + 10)
        {
            context.DrawText(formattedText, textPoint);
        }
    }
}
