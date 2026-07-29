namespace Avalonia.Diagnostics.Cdp.Rdp;

using System;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Chrome.DevTools.Protocol;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Rendering;
using CDP.Rdp.Session;
using SkiaSharp;

/// <summary>
/// Custom Avalonia control rendering RDP frame updates via SkiaSharp double buffering
/// and mapping pointer/keyboard user input to RDP session events.
/// </summary>
public class RdpControl : Control
{
    public static readonly StyledProperty<string> HostProperty =
        AvaloniaProperty.Register<RdpControl, string>(nameof(Host), "127.0.0.1");

    public static readonly StyledProperty<int> PortProperty =
        AvaloniaProperty.Register<RdpControl, int>(nameof(Port), 3389);

    public static readonly StyledProperty<string> UsernameProperty =
        AvaloniaProperty.Register<RdpControl, string>(nameof(Username), string.Empty);

    public static readonly StyledProperty<string> PasswordProperty =
        AvaloniaProperty.Register<RdpControl, string>(nameof(Password), string.Empty);

    public static readonly StyledProperty<string> DomainProperty =
        AvaloniaProperty.Register<RdpControl, string>(nameof(Domain), string.Empty);

    public static readonly StyledProperty<bool> IsConnectedProperty =
        AvaloniaProperty.Register<RdpControl, bool>(nameof(IsConnected), false);

    public static readonly StyledProperty<ICommand?> ConnectCommandProperty =
        AvaloniaProperty.Register<RdpControl, ICommand?>(nameof(ConnectCommand));

    public static readonly StyledProperty<ICommand?> DisconnectCommandProperty =
        AvaloniaProperty.Register<RdpControl, ICommand?>(nameof(DisconnectCommand));

    public static readonly StyledProperty<IRdpSession?> SessionProperty =
        AvaloniaProperty.Register<RdpControl, IRdpSession?>(nameof(Session));

    public static readonly StyledProperty<double> ScaleFactorProperty =
        AvaloniaProperty.Register<RdpControl, double>(nameof(ScaleFactor), 1.0);

    public double ScaleFactor
    {
        get => GetValue(ScaleFactorProperty);
        set => SetValue(ScaleFactorProperty, value);
    }

    public string Host
    {
        get => GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public int Port
    {
        get => GetValue(PortProperty);
        set => SetValue(PortProperty, value);
    }

    public string Username
    {
        get => GetValue(UsernameProperty);
        set => SetValue(UsernameProperty, value);
    }

    public string Password
    {
        get => GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public string Domain
    {
        get => GetValue(DomainProperty);
        set => SetValue(DomainProperty, value);
    }

    public bool IsConnected
    {
        get => GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    public ICommand? ConnectCommand
    {
        get => GetValue(ConnectCommandProperty);
        set => SetValue(ConnectCommandProperty, value);
    }

    public ICommand? DisconnectCommand
    {
        get => GetValue(DisconnectCommandProperty);
        set => SetValue(DisconnectCommandProperty, value);
    }

    public IRdpSession? Session
    {
        get => GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    private RdpFrameBuffer? _frameBuffer;
    private RdpSkiaCanvas? _skiaCanvas;
    private SKBitmap? _cachedSkiaBitmap;
    private WriteableBitmap? _writeableBitmap;
    private IRdpSession? _subscribedSession;
    private readonly object _frameGate = new();

    public RdpFrameBuffer? FrameBuffer => _frameBuffer;
    public RdpSkiaCanvas? SkiaCanvas => _skiaCanvas;

    static RdpControl()
    {
        FocusableProperty.OverrideDefaultValue<RdpControl>(true);
        AffectsRender<RdpControl>(SessionProperty);
    }

    public RdpControl()
    {
        InitFrameBuffer(1280, 720);
    }

    public void InitFrameBuffer(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Frame-buffer dimensions must be positive.");
        }

        lock (_frameGate)
        {
            InitFrameBufferCore(width, height);
        }
    }

    private void InitFrameBufferCore(int width, int height)
    {
        _frameBuffer?.Dispose();
        _frameBuffer = new RdpFrameBuffer(width, height);
        _skiaCanvas = new RdpSkiaCanvas(_frameBuffer);
        _cachedSkiaBitmap?.Dispose();
        _cachedSkiaBitmap = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SessionProperty)
        {
            UnsubscribeFromSession();
            if (change.NewValue is IRdpSession newSession)
            {
                InitFrameBuffer(newSession.DesktopWidth, newSession.DesktopHeight);
                SubscribeToSession(newSession);
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_frameBuffer == null)
        {
            int width = Session?.DesktopWidth ?? 1280;
            int height = Session?.DesktopHeight ?? 720;
            InitFrameBuffer(width, height);
        }

        if (Session is { } session)
        {
            SubscribeToSession(session);
        }
    }

    private void SubscribeToSession(IRdpSession session)
    {
        if (ReferenceEquals(_subscribedSession, session))
        {
            return;
        }

        UnsubscribeFromSession();
        lock (_frameGate)
        {
            session.FrameUpdated += OnFrameUpdated;
            session.StateChanged += OnSessionStateChanged;
            _subscribedSession = session;
        }

        if (session.State == RdpConnectionState.Connected)
        {
            InitializeActivatedDesktop(session);
        }
    }

    private void UnsubscribeFromSession()
    {
        lock (_frameGate)
        {
            if (_subscribedSession != null)
            {
                _subscribedSession.FrameUpdated -= OnFrameUpdated;
                _subscribedSession.StateChanged -= OnSessionStateChanged;
                _subscribedSession = null;
            }
        }
    }

    private void OnSessionStateChanged(object? sender, RdpConnectionStateChangedEventArgs e)
    {
        if (e.NewState == RdpConnectionState.Connected && sender is IRdpSession session)
        {
            InitializeActivatedDesktop(session);
        }
    }

    private void InitializeActivatedDesktop(IRdpSession session)
    {
        lock (_frameGate)
        {
            if (!ReferenceEquals(session, _subscribedSession))
            {
                return;
            }

            InitFrameBufferCore(session.DesktopWidth, session.DesktopHeight);
        }

        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    private void OnFrameUpdated(object? sender, RdpFrameUpdateEventArgs e)
    {
        lock (_frameGate)
        {
            if (!ReferenceEquals(sender, _subscribedSession) || _frameBuffer == null)
            {
                return;
            }

            _frameBuffer.ApplyFrameUpdate(e);
        }
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);

        foreach (var session in Chrome.DevTools.Protocol.CdpServer.Sessions)
        {
            session.RequestScreencastFrame();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        UnsubscribeFromSession();

        lock (_frameGate)
        {
            _cachedSkiaBitmap?.Dispose();
            _cachedSkiaBitmap = null;
            _writeableBitmap?.Dispose();
            _writeableBitmap = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        lock (_frameGate)
        {
            RenderCore(context);
        }
    }

    private void RenderCore(DrawingContext context)
    {
        if (_frameBuffer == null || _skiaCanvas == null) return;

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        int pixelWidth = (int)Math.Max(1, Math.Round(width * scaling));
        int pixelHeight = (int)Math.Max(1, Math.Round(height * scaling));

        bool isNewBitmap = false;
        if (_cachedSkiaBitmap == null || _cachedSkiaBitmap.Width != pixelWidth || _cachedSkiaBitmap.Height != pixelHeight)
        {
            _cachedSkiaBitmap?.Dispose();
            _cachedSkiaBitmap = new SKBitmap(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Opaque));
            isNewBitmap = true;
        }

        if (_writeableBitmap == null || _writeableBitmap.PixelSize.Width != pixelWidth || _writeableBitmap.PixelSize.Height != pixelHeight)
        {
            _writeableBitmap?.Dispose();
            _writeableBitmap = new WriteableBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(96 * scaling, 96 * scaling),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
        }

        using (var canvas = new SKCanvas(_cachedSkiaBitmap))
        {
            double requestedScale = ScaleFactor;
            float contentScale = requestedScale > 0 && !double.IsNaN(requestedScale) && !double.IsInfinity(requestedScale)
                ? (float)requestedScale
                : 1f;
            var targetBounds = new SKRect(0, 0, pixelWidth * contentScale, pixelHeight * contentScale);
            if (isNewBitmap)
            {
                canvas.Clear(SKColors.Black);
                _skiaCanvas.Render(canvas, targetBounds, drawDirtyOnly: false);
            }
            else
            {
                _skiaCanvas.Render(canvas, targetBounds, drawDirtyOnly: true);
            }
        }

        using (var locked = _writeableBitmap.Lock())
        {
            IntPtr srcPtr = _cachedSkiaBitmap.GetPixels();
            IntPtr dstPtr = locked.Address;
            int srcRowBytes = _cachedSkiaBitmap.RowBytes;
            int dstRowBytes = locked.RowBytes;
            int rowSize = Math.Min(srcRowBytes, dstRowBytes);

            unsafe
            {
                for (int y = 0; y < pixelHeight; y++)
                {
                    Buffer.MemoryCopy(
                        (void*)(srcPtr + y * srcRowBytes),
                        (void*)(dstPtr + y * dstRowBytes),
                        rowSize,
                        rowSize);
                }
            }
        }

        context.DrawImage(_writeableBitmap, new Rect(0, 0, width, height));
    }

    #region Input Event Handlers

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        e.Pointer.Capture(this);

        SendPointerEvent(e, isDown: true);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        SendPointerEvent(e, isDown: false, isMove: true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);

        SendPointerEvent(e, isDown: false);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (Session == null || _frameBuffer == null)
            return;

        Point pos = e.GetPosition(this);
        TranslateCoordinates(pos, out ushort xPos, out ushort yPos);

        RdpPointerFlags flags = RdpPointerFlags.Wheel;
        int magnitude = Math.Clamp((int)Math.Round(Math.Abs(e.Delta.Y) * 120.0), 1, 0xFF);
        flags |= (RdpPointerFlags)magnitude;
        if (e.Delta.Y < 0)
        {
            flags |= RdpPointerFlags.WheelNegative;
        }

        var mouseEvent = new RdpMouseEvent((uint)Environment.TickCount, flags, xPos, yPos);
        _ = Session.SendInputEventAsync(new RdpInputEvent((uint)Environment.TickCount, mouseEvent));
    }

    private void SendPointerEvent(PointerEventArgs e, bool isDown, bool isMove = false)
    {
        if (Session == null || _frameBuffer == null)
            return;

        Point pos = e.GetPosition(this);
        TranslateCoordinates(pos, out ushort xPos, out ushort yPos);

        RdpPointerFlags flags = CreatePointerFlags(
            isDown,
            isMove,
            e.GetCurrentPoint(this).Properties.PointerUpdateKind);

        var mouseEvent = new RdpMouseEvent((uint)Environment.TickCount, flags, xPos, yPos);
        _ = Session.SendInputEventAsync(new RdpInputEvent((uint)Environment.TickCount, mouseEvent));
    }

    internal static RdpPointerFlags CreatePointerFlags(
        bool isDown,
        bool isMove,
        PointerUpdateKind updateKind)
    {
        if (isMove)
        {
            // Button1/2/3 without Down encode releases in RDP. A drag move is
            // therefore a coordinate-only move; the remote button remains down
            // until the corresponding explicit release event arrives.
            return RdpPointerFlags.Move;
        }

        // PointerPointProperties describes the entire current button chord.
        // RDP button flags instead describe one transition, so encode only the
        // button named by PointerUpdateKind for both presses and releases.
        RdpPointerFlags flags = updateKind switch
        {
            PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased =>
                RdpPointerFlags.Button1,
            PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased =>
                RdpPointerFlags.Button2,
            PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased =>
                RdpPointerFlags.Button3,
            _ => RdpPointerFlags.None
        };

        if (isDown)
        {
            flags |= RdpPointerFlags.Down;
        }

        return flags;
    }

    public void TranslateCoordinates(Point controlPoint, out ushort xPos, out ushort yPos)
    {
        double width = Bounds.Width > 0 ? Bounds.Width : (Width > 0 && !double.IsNaN(Width) ? Width : 1);
        double height = Bounds.Height > 0 ? Bounds.Height : (Height > 0 && !double.IsNaN(Height) ? Height : 1);

        double scale = ScaleFactor > 0 && !double.IsNaN(ScaleFactor) && !double.IsInfinity(ScaleFactor) ? ScaleFactor : 1.0;

        int fbWidth = _frameBuffer?.Width ?? 1280;
        int fbHeight = _frameBuffer?.Height ?? 720;

        int mappedX;
        if (double.IsPositiveInfinity(controlPoint.X))
        {
            mappedX = fbWidth - 1;
        }
        else if (double.IsNegativeInfinity(controlPoint.X) || double.IsNaN(controlPoint.X))
        {
            mappedX = 0;
        }
        else
        {
            mappedX = (int)Math.Clamp(Math.Floor((controlPoint.X / (width * scale)) * fbWidth), 0, fbWidth - 1);
        }

        int mappedY;
        if (double.IsPositiveInfinity(controlPoint.Y))
        {
            mappedY = fbHeight - 1;
        }
        else if (double.IsNegativeInfinity(controlPoint.Y) || double.IsNaN(controlPoint.Y))
        {
            mappedY = 0;
        }
        else
        {
            mappedY = (int)Math.Clamp(Math.Floor((controlPoint.Y / (height * scale)) * fbHeight), 0, fbHeight - 1);
        }

        xPos = (ushort)mappedX;
        yPos = (ushort)mappedY;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsPrintableTextKey(e.Key) && !HasCommandModifier(e.KeyModifiers))
        {
            return;
        }
        SendKeyEvent(e.Key, e.PhysicalKey, isDown: true);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (IsPrintableTextKey(e.Key) && !HasCommandModifier(e.KeyModifiers))
        {
            return;
        }
        SendKeyEvent(e.Key, e.PhysicalKey, isDown: false);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        if (Session == null || string.IsNullOrEmpty(e.Text))
            return;

        Span<char> utf16 = stackalloc char[2];
        foreach (var rune in e.Text.EnumerateRunes())
        {
            int codeUnitCount = rune.EncodeToUtf16(utf16);
            for (int i = 0; i < codeUnitCount; i++)
            {
                SendUnicodeCodeUnit(utf16[i]);
            }
        }
    }

    private void SendUnicodeCodeUnit(char codeUnit)
    {
        if (Session == null)
        {
            return;
        }

        uint timestamp = (uint)Environment.TickCount;
        var press = new RdpKeyboardEvent(timestamp, RdpKeyboardFlags.Down, codeUnit, isVirtualKey: false);
        _ = Session.SendInputEventAsync(new RdpInputEvent(timestamp, RdpInputMessageType.Unicode, press, default, default));

        var release = new RdpKeyboardEvent(timestamp, RdpKeyboardFlags.Release, codeUnit, isVirtualKey: false);
        _ = Session.SendInputEventAsync(new RdpInputEvent(timestamp, RdpInputMessageType.Unicode, release, default, default));
    }

    private static bool HasCommandModifier(KeyModifiers modifiers)
    {
        const KeyModifiers commandModifiers = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta;
        return (modifiers & commandModifiers) != 0;
    }

    private static bool IsPrintableTextKey(Key key)
    {
        return key is >= Key.A and <= Key.Z
            or >= Key.D0 and <= Key.D9
            or Key.Space
            or Key.OemMinus
            or Key.OemPlus
            or Key.OemOpenBrackets
            or Key.OemCloseBrackets
            or Key.OemSemicolon
            or Key.OemQuotes
            or Key.OemTilde
            or Key.OemPipe
            or Key.OemComma
            or Key.OemPeriod
            or Key.OemQuestion;
    }

    private void SendKeyEvent(Key key, PhysicalKey physicalKey, bool isDown)
    {
        if (Session == null)
            return;

        if (RdpInputMapper.TryMapKey(key, physicalKey, isDown, out RdpKeyboardEvent kbEvent))
        {
            _ = Session.SendInputEventAsync(new RdpInputEvent((uint)Environment.TickCount, kbEvent));
        }
    }

    #endregion
}
