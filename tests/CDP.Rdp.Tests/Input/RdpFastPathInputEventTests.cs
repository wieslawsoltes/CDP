using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Input;

using System;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using Xunit;

public class RdpFastPathInputEventTests
{
    [AvaloniaFact]
    public void FastPathScancode_KeyPress_RoundTripSerialization()
    {
        var fpEvent = new RdpFastPathInputEvent(FastPathKeyboardFlags.None, 0x1E); // Key 'A'

        byte[] buffer = new byte[2];
        var writer = new RdpPacketWriter(buffer);
        fpEvent.Write(ref writer);

        Assert.Equal(2, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpFastPathInputEvent.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(FastPathInputEventCode.ScanCode, parsed.Code);
        Assert.Equal(0x1E, parsed.KeyCode);
        Assert.Equal(FastPathKeyboardFlags.None, parsed.KeyboardFlags);
    }

    [AvaloniaFact]
    public void FastPathScancode_KeyReleaseExtended_RoundTripSerialization()
    {
        var fpEvent = new RdpFastPathInputEvent(FastPathKeyboardFlags.Release | FastPathKeyboardFlags.Extended, 0x48);

        byte[] buffer = new byte[2];
        var writer = new RdpPacketWriter(buffer);
        fpEvent.Write(ref writer);

        Assert.Equal(2, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpFastPathInputEvent.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(FastPathInputEventCode.ScanCode, parsed.Code);
        Assert.Equal(0x48, parsed.KeyCode);
        Assert.True(parsed.KeyboardFlags.HasFlag(FastPathKeyboardFlags.Release));
        Assert.True(parsed.KeyboardFlags.HasFlag(FastPathKeyboardFlags.Extended));
    }

    [AvaloniaFact]
    public void FastPathMouse_MoveAndClick_RoundTripSerialization()
    {
        var fpEvent = new RdpFastPathInputEvent(FastPathInputEventCode.Mouse, RdpPointerFlags.Move | RdpPointerFlags.Button1 | RdpPointerFlags.Down, 1024, 768);

        byte[] buffer = new byte[7];
        var writer = new RdpPacketWriter(buffer);
        fpEvent.Write(ref writer);

        Assert.Equal(7, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpFastPathInputEvent.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(FastPathInputEventCode.Mouse, parsed.Code);
        Assert.Equal(1024, parsed.XPos);
        Assert.Equal(768, parsed.YPos);
        Assert.True(parsed.PointerFlags.HasFlag(RdpPointerFlags.Button1));
    }

    [AvaloniaFact]
    public void FastPathReleaseAll_RoundTripSerialization()
    {
        var fpEvent = new RdpFastPathInputEvent(FastPathInputEventCode.ReleaseAll);

        byte[] buffer = new byte[1];
        var writer = new RdpPacketWriter(buffer);
        fpEvent.Write(ref writer);

        Assert.Equal(1, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpFastPathInputEvent.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(FastPathInputEventCode.ReleaseAll, parsed.Code);
    }

    [AvaloniaFact]
    public void FastPathSync_RoundTripSerialization()
    {
        var fpEvent = new RdpFastPathInputEvent((byte)RdpSyncToggleFlags.CapsLock);

        byte[] buffer = new byte[2];
        var writer = new RdpPacketWriter(buffer);
        fpEvent.Write(ref writer);

        Assert.Equal(2, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpFastPathInputEvent.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(FastPathInputEventCode.Sync, parsed.Code);
        Assert.Equal((byte)RdpSyncToggleFlags.CapsLock, parsed.ToggleFlags);
    }

    [AvaloniaFact]
    public void FastPathHeader_ShortLength_RoundTrip()
    {
        byte[] buffer = new byte[2];
        var writer = new RdpPacketWriter(buffer);
        RdpInputPduWriter.WriteFastPathHeader(ref writer, numEvents: 3, pduLength: 45);

        Assert.Equal(2, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputPduReader.TryReadFastPathHeader(ref reader, out byte action, out byte numEvents, out byte sec, out ushort len);

        Assert.True(success);
        Assert.Equal(0, action);
        Assert.Equal(3, numEvents);
        Assert.Equal(0, sec);
        Assert.Equal(45, len);
    }

    [AvaloniaFact]
    public void FastPathHeader_ExtendedLength_RoundTrip()
    {
        byte[] buffer = new byte[3];
        var writer = new RdpPacketWriter(buffer);
        RdpInputPduWriter.WriteFastPathHeader(ref writer, numEvents: 7, pduLength: 350);

        Assert.Equal(3, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputPduReader.TryReadFastPathHeader(ref reader, out byte action, out byte numEvents, out byte sec, out ushort len);

        Assert.True(success);
        Assert.Equal(7, numEvents);
        Assert.Equal(350, len);
    }
}
