using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Input;

using System;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using Xunit;

[Xunit.Collection("RdpTests")]
public class RdpMouseEmpiricalTests
{
    [AvaloniaTheory]
    [InlineData(120, false)] // Positive 1 notch (wheel up/forward)
    [InlineData(120, true)]  // Negative 1 notch (wheel down/backward)
    [InlineData(240, false)] // Positive 2 notches
    [InlineData(240, true)]  // Negative 2 notches
    [InlineData(1, false)]   // Min positive delta
    [InlineData(1, true)]    // Min negative delta
    [InlineData(255, false)] // Max byte positive delta
    [InlineData(255, true)]  // Max byte negative delta
    public void SlowPathMouseWheel_NegativeAndPositiveDeltas_RoundTripPreserved(ushort deltaMagnitude, bool isNegative)
    {
        uint time = 112233;
        ushort deltaByte = (ushort)(deltaMagnitude & 0x00FF);
        var pointerFlags = RdpPointerFlags.Wheel | (RdpPointerFlags)deltaByte;
        if (isNegative)
        {
            pointerFlags |= RdpPointerFlags.WheelNegative;
        }

        var mouseEvent = new RdpMouseEvent(time, pointerFlags, xPos: 500, yPos: 400);
        var inputEvent = new RdpInputEvent(time, mouseEvent);

        byte[] buffer = new byte[RdpInputEvent.EventLength];
        var writer = new RdpPacketWriter(buffer);
        inputEvent.Write(ref writer);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputEvent.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(RdpInputMessageType.Mouse, parsed.MessageType);
        Assert.True(parsed.MouseEvent.PointerFlags.HasFlag(RdpPointerFlags.Wheel));
        Assert.Equal(isNegative, parsed.MouseEvent.PointerFlags.HasFlag(RdpPointerFlags.WheelNegative));

        ushort parsedDelta = (ushort)((ushort)parsed.MouseEvent.PointerFlags & 0x00FF);
        Assert.Equal(deltaMagnitude, parsedDelta);
    }

    [AvaloniaTheory]
    [InlineData(120, false)]
    [InlineData(120, true)]
    [InlineData(240, false)]
    [InlineData(240, true)]
    public void FastPathMouseWheel_NegativeAndPositiveDeltas_RoundTripPreserved(ushort deltaMagnitude, bool isNegative)
    {
        ushort deltaByte = (ushort)(deltaMagnitude & 0x00FF);
        var pointerFlags = RdpPointerFlags.Wheel | (RdpPointerFlags)deltaByte;
        if (isNegative)
        {
            pointerFlags |= RdpPointerFlags.WheelNegative;
        }

        var fpEvent = new RdpFastPathInputEvent(FastPathInputEventCode.Mouse, pointerFlags, xPos: 1920, yPos: 1080);

        byte[] buffer = new byte[7];
        var writer = new RdpPacketWriter(buffer);
        fpEvent.Write(ref writer);

        Assert.Equal(7, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpFastPathInputEvent.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(FastPathInputEventCode.Mouse, parsed.Code);
        Assert.True(parsed.PointerFlags.HasFlag(RdpPointerFlags.Wheel));
        Assert.Equal(isNegative, parsed.PointerFlags.HasFlag(RdpPointerFlags.WheelNegative));

        ushort parsedDelta = (ushort)((ushort)parsed.PointerFlags & 0x00FF);
        Assert.Equal(deltaMagnitude, parsedDelta);
    }

    [AvaloniaTheory]
    [InlineData(0, 0)]
    [InlineData(0, 65535)]
    [InlineData(65535, 0)]
    [InlineData(65535, 65535)]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    [InlineData(7680, 4320)]
    public void SlowPathAndFastPathMouse_CoordinateBounds_RoundTripPreserved(ushort x, ushort y)
    {
        uint time = 445566;

        // SlowPath
        var mouse = new RdpMouseEvent(time, RdpPointerFlags.Move, x, y);
        var slowEvent = new RdpInputEvent(time, mouse);
        byte[] slowBuffer = new byte[RdpInputEvent.EventLength];
        var slowWriter = new RdpPacketWriter(slowBuffer);
        slowEvent.Write(ref slowWriter);

        var slowReader = new RdpPacketReader(slowBuffer);
        Assert.True(RdpInputEvent.TryRead(ref slowReader, out var slowParsed));
        Assert.Equal(x, slowParsed.MouseEvent.XPos);
        Assert.Equal(y, slowParsed.MouseEvent.YPos);

        // FastPath
        var fastEvent = new RdpFastPathInputEvent(FastPathInputEventCode.Mouse, RdpPointerFlags.Move, x, y);
        byte[] fastBuffer = new byte[7];
        var fastWriter = new RdpPacketWriter(fastBuffer);
        fastEvent.Write(ref fastWriter);

        var fastReader = new RdpPacketReader(fastBuffer);
        Assert.True(RdpFastPathInputEvent.TryRead(ref fastReader, out var fastParsed));
        Assert.Equal(x, fastParsed.XPos);
        Assert.Equal(y, fastParsed.YPos);
    }

    [AvaloniaTheory]
    [InlineData(RdpPointerFlags.Button1 | RdpPointerFlags.Down)] // Left Down
    [InlineData(RdpPointerFlags.Button1)]                        // Left Up
    [InlineData(RdpPointerFlags.Button2 | RdpPointerFlags.Down)] // Right Down
    [InlineData(RdpPointerFlags.Button2)]                        // Right Up
    [InlineData(RdpPointerFlags.Button3 | RdpPointerFlags.Down)] // Middle Down
    [InlineData(RdpPointerFlags.Button3)]                        // Middle Up
    [InlineData(RdpPointerFlags.Move | RdpPointerFlags.Button1 | RdpPointerFlags.Down)] // Move + Left Down
    public void SlowAndFastPathMouse_ButtonsAndMove_RoundTripPreserved(RdpPointerFlags flags)
    {
        uint time = 778899;
        ushort x = 800;
        ushort y = 600;

        // SlowPath
        var slowEvent = new RdpInputEvent(time, new RdpMouseEvent(time, flags, x, y));
        byte[] slowBuf = new byte[14];
        var slowWr = new RdpPacketWriter(slowBuf);
        slowEvent.Write(ref slowWr);
        var slowRd = new RdpPacketReader(slowBuf);
        Assert.True(RdpInputEvent.TryRead(ref slowRd, out var slowParsed));
        Assert.Equal(flags, slowParsed.MouseEvent.PointerFlags);

        // FastPath
        var fastEvent = new RdpFastPathInputEvent(FastPathInputEventCode.Mouse, flags, x, y);
        byte[] fastBuf = new byte[7];
        var fastWr = new RdpPacketWriter(fastBuf);
        fastEvent.Write(ref fastWr);
        var fastRd = new RdpPacketReader(fastBuf);
        Assert.True(RdpFastPathInputEvent.TryRead(ref fastRd, out var fastParsed));
        Assert.Equal(flags, fastParsed.PointerFlags);
    }
}
