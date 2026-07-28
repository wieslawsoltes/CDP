using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Input;

using System;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using Xunit;

public class RdpInputEventTests
{
    [AvaloniaFact]
    public void ScanCodeEvent_RoundTripSerialization_PreservesValues()
    {
        uint eventTime = 123456;
        var kb = new RdpKeyboardEvent(eventTime, RdpKeyboardFlags.Release | RdpKeyboardFlags.Extended, 0x1C, isVirtualKey: false);
        var originalEvent = new RdpInputEvent(eventTime, kb);

        byte[] buffer = new byte[14];
        var writer = new RdpPacketWriter(buffer);
        originalEvent.Write(ref writer);

        Assert.Equal(14, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputEvent.TryRead(ref reader, out var parsedEvent);

        Assert.True(success);
        Assert.Equal(14, reader.Position);
        Assert.Equal(eventTime, parsedEvent.EventTime);
        Assert.Equal(RdpInputMessageType.ScanCode, parsedEvent.MessageType);
        Assert.False(parsedEvent.KeyboardEvent.IsVirtualKey);
        Assert.Equal(0x1Cu, parsedEvent.KeyboardEvent.KeyCode);
        Assert.True(parsedEvent.KeyboardEvent.Flags.HasFlag(RdpKeyboardFlags.Release));
        Assert.True(parsedEvent.KeyboardEvent.Flags.HasFlag(RdpKeyboardFlags.Extended));
    }

    [AvaloniaFact]
    public void VkCodeEvent_RoundTripSerialization_PreservesValues()
    {
        uint eventTime = 654321;
        var kb = new RdpKeyboardEvent(eventTime, RdpKeyboardFlags.Down, 0x41, isVirtualKey: true);
        var originalEvent = new RdpInputEvent(eventTime, kb);

        byte[] buffer = new byte[14];
        var writer = new RdpPacketWriter(buffer);
        originalEvent.Write(ref writer);

        Assert.Equal(14, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputEvent.TryRead(ref reader, out var parsedEvent);

        Assert.True(success);
        Assert.Equal(RdpInputMessageType.VkCode, parsedEvent.MessageType);
        Assert.True(parsedEvent.KeyboardEvent.IsVirtualKey);
        Assert.Equal(0x41u, parsedEvent.KeyboardEvent.KeyCode);
    }

    [AvaloniaFact]
    public void MouseEvent_MoveAndButtonClick_RoundTripSerialization()
    {
        uint eventTime = 100200;
        var mouse = new RdpMouseEvent(eventTime, RdpPointerFlags.Move | RdpPointerFlags.Button1 | RdpPointerFlags.Down, 800, 600);
        var originalEvent = new RdpInputEvent(eventTime, mouse, isExtendedMouse: false);

        byte[] buffer = new byte[14];
        var writer = new RdpPacketWriter(buffer);
        originalEvent.Write(ref writer);

        Assert.Equal(14, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputEvent.TryRead(ref reader, out var parsedEvent);

        Assert.True(success);
        Assert.Equal(RdpInputMessageType.Mouse, parsedEvent.MessageType);
        Assert.Equal(800, parsedEvent.MouseEvent.XPos);
        Assert.Equal(600, parsedEvent.MouseEvent.YPos);
        Assert.True(parsedEvent.MouseEvent.PointerFlags.HasFlag(RdpPointerFlags.Button1));
        Assert.True(parsedEvent.MouseEvent.PointerFlags.HasFlag(RdpPointerFlags.Down));
    }

    [AvaloniaFact]
    public void MouseEvent_WheelNegative_RoundTripSerialization()
    {
        uint eventTime = 200300;
        var flags = RdpPointerFlags.Wheel | RdpPointerFlags.WheelNegative | (RdpPointerFlags)0x0078; // 120 units
        var mouse = new RdpMouseEvent(eventTime, flags, 400, 300);
        var originalEvent = new RdpInputEvent(eventTime, mouse, isExtendedMouse: false);

        byte[] buffer = new byte[14];
        var writer = new RdpPacketWriter(buffer);
        originalEvent.Write(ref writer);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputEvent.TryRead(ref reader, out var parsedEvent);

        Assert.True(success);
        Assert.True(parsedEvent.MouseEvent.PointerFlags.HasFlag(RdpPointerFlags.Wheel));
        Assert.True(parsedEvent.MouseEvent.PointerFlags.HasFlag(RdpPointerFlags.WheelNegative));
    }

    [AvaloniaFact]
    public void SyncEvent_RoundTripSerialization_PreservesToggleFlags()
    {
        uint eventTime = 300400;
        var sync = new RdpSyncEvent(eventTime, RdpSyncToggleFlags.CapsLock | RdpSyncToggleFlags.NumLock);
        var originalEvent = new RdpInputEvent(eventTime, sync);

        byte[] buffer = new byte[14];
        var writer = new RdpPacketWriter(buffer);
        originalEvent.Write(ref writer);

        Assert.Equal(14, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputEvent.TryRead(ref reader, out var parsedEvent);

        Assert.True(success);
        Assert.Equal(RdpInputMessageType.Sync, parsedEvent.MessageType);
        Assert.True(parsedEvent.SyncEvent.ToggleFlags.HasFlag(RdpSyncToggleFlags.CapsLock));
        Assert.True(parsedEvent.SyncEvent.ToggleFlags.HasFlag(RdpSyncToggleFlags.NumLock));
    }

    [AvaloniaFact]
    public void TryRead_InsufficientBytes_ReturnsFalse()
    {
        byte[] buffer = new byte[10];
        var reader = new RdpPacketReader(buffer);

        bool success = RdpInputEvent.TryRead(ref reader, out _);

        Assert.False(success);
    }

    [AvaloniaFact]
    public void SlowPathPduHeader_RoundTripSerialization()
    {
        byte[] buffer = new byte[4];
        var writer = new RdpPacketWriter(buffer);
        RdpInputPduWriter.WriteSlowPathHeader(ref writer, numEvents: 5);

        Assert.Equal(4, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputPduReader.TryReadSlowPathHeader(ref reader, out ushort count);

        Assert.True(success);
        Assert.Equal(5, count);
    }
}
