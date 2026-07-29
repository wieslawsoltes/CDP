using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Controls;

using System;

using Avalonia;
using Avalonia.Diagnostics.Cdp.Rdp;
using CDP.Rdp.Frames;
using CDP.Rdp.Session;
using Xunit;

[Xunit.Collection("RdpTests")]
public class RdpControlTests
{
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
}
