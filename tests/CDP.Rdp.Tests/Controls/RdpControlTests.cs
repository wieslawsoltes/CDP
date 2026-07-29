using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Controls;

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Diagnostics.Cdp.Rdp;
using Avalonia.Input;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Session;
using SkiaSharp;
using Xunit;

[Xunit.Collection("RdpTests")]
public class RdpControlTests
{
    private sealed class TestSession : IRdpSession
    {
        private RdpConnectionState _state;

        public TestSession(RdpConnectionState state = RdpConnectionState.Connected)
        {
            _state = state;
        }

        public RdpConnectionState State => _state;
        public RdpSessionOptions Options { get; } = new() { Width = 2, Height = 2 };
        public ushort DesktopWidth { get; private set; } = 2;
        public ushort DesktopHeight { get; private set; } = 2;
        public event EventHandler<RdpFrameUpdateEventArgs>? FrameUpdated;
        public event EventHandler<RdpConnectionStateChangedEventArgs>? StateChanged;
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendInputEventAsync(RdpInputEvent inputEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendFastPathInputEventAsync(RdpFastPathInputEvent inputEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RaiseFrame(RdpFrameUpdateEventArgs args) => FrameUpdated?.Invoke(this, args);
        public void RaiseConnected(ushort desktopWidth, ushort desktopHeight)
        {
            RdpConnectionState oldState = _state;
            DesktopWidth = desktopWidth;
            DesktopHeight = desktopHeight;
            _state = RdpConnectionState.Connected;
            StateChanged?.Invoke(
                this,
                new RdpConnectionStateChangedEventArgs(oldState, RdpConnectionState.Connected));
        }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [AvaloniaFact]
    public void RdpControl_Defaults_InitializedCorrectly()
    {
        var control = new RdpControl();

        Assert.NotNull(control.FrameBuffer);
        Assert.NotNull(control.SkiaCanvas);
        Assert.Equal(1280, control.FrameBuffer.Width);
        Assert.Equal(720, control.FrameBuffer.Height);
        Assert.Equal("127.0.0.1", control.Host);
        Assert.Equal(3389, control.Port);
        Assert.Equal(string.Empty, control.Username);
        Assert.Equal(string.Empty, control.Password);
        Assert.Equal(string.Empty, control.Domain);
        Assert.False(control.IsConnected);
        Assert.Null(control.Session);
    }

    [AvaloniaFact]
    public void RdpControl_StyledProperties_CanBeSetAndRetrieved()
    {
        var control = new RdpControl
        {
            Host = "192.168.1.100",
            Port = 3390,
            Username = "administrator",
            Password = "SecretPassword123!",
            Domain = "CORP",
            IsConnected = true
        };

        Assert.Equal("192.168.1.100", control.Host);
        Assert.Equal(3390, control.Port);
        Assert.Equal("administrator", control.Username);
        Assert.Equal("SecretPassword123!", control.Password);
        Assert.Equal("CORP", control.Domain);
        Assert.True(control.IsConnected);
    }

    [AvaloniaTheory]
    [InlineData(0, 0, 100, 100, 1280, 720, (ushort)0, (ushort)0)]
    [InlineData(50, 50, 100, 100, 1280, 720, (ushort)640, (ushort)360)]
    [InlineData(100, 100, 100, 100, 1280, 720, (ushort)1279, (ushort)719)]
    public void TranslateCoordinates_MapsControlPointToRemoteResolution(
        double posX, double posY, double controlWidth, double controlHeight,
        int fbWidth, int fbHeight, ushort expectedX, ushort expectedY)
    {
        var control = new RdpControl();
        control.InitFrameBuffer(fbWidth, fbHeight);
        control.Width = controlWidth;
        control.Height = controlHeight;

        // Force Bounds calculation via mock or width/height
        Point point = new Point(posX, posY);
        control.TranslateCoordinates(point, out ushort mappedX, out ushort mappedY);

        Assert.Equal(expectedX, mappedX);
        Assert.Equal(expectedY, mappedY);
    }

    [AvaloniaFact]
    public void TranslateCoordinates_HandlesNaNAndInfinityBoundsCorrectly()
    {
        var control = new RdpControl();
        control.InitFrameBuffer(1280, 720);

        // Positive Infinity -> Max bounds (1279, 719)
        control.TranslateCoordinates(new Point(double.PositiveInfinity, double.PositiveInfinity), out ushort xPosInf, out ushort yPosInf);
        Assert.Equal((ushort)1279, xPosInf);
        Assert.Equal((ushort)719, yPosInf);

        // Negative Infinity -> (0, 0)
        control.TranslateCoordinates(new Point(double.NegativeInfinity, double.NegativeInfinity), out ushort xNegInf, out ushort yNegInf);
        Assert.Equal((ushort)0, xNegInf);
        Assert.Equal((ushort)0, yNegInf);

        // NaN -> (0, 0)
        control.TranslateCoordinates(new Point(double.NaN, double.NaN), out ushort xNan, out ushort yNan);
        Assert.Equal((ushort)0, xNan);
        Assert.Equal((ushort)0, yNan);
    }

    [AvaloniaFact]
    public void CreatePointerFlags_DragMoveDoesNotEncodeButtonRelease()
    {
        RdpPointerFlags flags = RdpControl.CreatePointerFlags(
            isDown: false,
            isMove: true,
            PointerUpdateKind.Other);

        Assert.Equal(RdpPointerFlags.Move, flags);
        Assert.False(flags.HasFlag(RdpPointerFlags.Button1));
        Assert.False(flags.HasFlag(RdpPointerFlags.Down));
    }

    [AvaloniaFact]
    public void CreatePointerFlags_ReleaseEmitsOnlyTransitionedButton()
    {
        RdpPointerFlags flags = RdpControl.CreatePointerFlags(
            isDown: false,
            isMove: false,
            PointerUpdateKind.RightButtonReleased);

        Assert.Equal(RdpPointerFlags.Button2, flags);
        Assert.False(flags.HasFlag(RdpPointerFlags.Button1));
        Assert.False(flags.HasFlag(RdpPointerFlags.Down));
    }

    [AvaloniaFact]
    public void SessionReplacement_IgnoresInFlightFramesFromPreviousSession()
    {
        var control = new RdpControl();
        using var first = new TestSession();
        using var second = new TestSession();
        control.Session = first;
        control.Session = second;

        var staleUpdate = new RdpBitmapUpdate(
            0,
            0,
            1,
            1,
            32,
            false,
            new byte[] { 0x10, 0x20, 0x30, 0xFF });
        first.RaiseFrame(new RdpFrameUpdateEventArgs(41, DateTimeOffset.UtcNow, [staleUpdate]));

        Assert.NotNull(control.FrameBuffer);
        Assert.Equal(0UL, control.FrameBuffer.CurrentFrameId);

        second.RaiseFrame(new RdpFrameUpdateEventArgs(42, DateTimeOffset.UtcNow, [staleUpdate]));
        Assert.Equal(42UL, control.FrameBuffer.CurrentFrameId);
    }

    [AvaloniaFact]
    public void InitFrameBuffer_ClearsCachedRenderTarget()
    {
        var control = new RdpControl();
        FieldInfo cacheField = typeof(RdpControl).GetField(
            "_cachedSkiaBitmap",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var cachedBitmap = new SKBitmap(2, 2);
        cacheField.SetValue(control, cachedBitmap);

        control.InitFrameBuffer(4, 3);

        Assert.Null(cacheField.GetValue(control));
        Assert.Equal(4, control.FrameBuffer!.Width);
        Assert.Equal(3, control.FrameBuffer.Height);
    }

    [AvaloniaFact]
    public void ConnectedSession_ResizesFramebufferToActivatedDesktop()
    {
        var control = new RdpControl();
        using var session = new TestSession(RdpConnectionState.Activating);
        control.Session = session;

        Assert.Equal(2, control.FrameBuffer!.Width);
        Assert.Equal(2, control.FrameBuffer.Height);

        session.RaiseConnected(4, 3);

        Assert.Equal(4, control.FrameBuffer.Width);
        Assert.Equal(3, control.FrameBuffer.Height);
    }
}
