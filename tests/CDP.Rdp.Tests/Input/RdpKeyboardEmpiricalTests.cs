using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Input;

using System;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using Xunit;

public class RdpKeyboardEmpiricalTests
{
    [AvaloniaTheory]
    [InlineData(0x1C, RdpKeyboardFlags.None)] // Enter Down
    [InlineData(0x1C, RdpKeyboardFlags.Release)] // Enter Up
    [InlineData(0x1D, RdpKeyboardFlags.Extended)] // Right Ctrl Down (0xE0)
    [InlineData(0x1D, RdpKeyboardFlags.Extended | RdpKeyboardFlags.Release)] // Right Ctrl Up (0xE0)
    [InlineData(0x38, RdpKeyboardFlags.Extended)] // Right Alt Down (0xE0)
    [InlineData(0x38, RdpKeyboardFlags.Extended | RdpKeyboardFlags.Release)] // Right Alt Up (0xE0)
    [InlineData(0x45, RdpKeyboardFlags.Extended1)] // Pause/Break Down (0xE1)
    [InlineData(0x45, RdpKeyboardFlags.Extended1 | RdpKeyboardFlags.Release)] // Pause/Break Up (0xE1)
    public void SlowPathScancode_ExtendedFlagsAndRelease_RoundTripPreserved(ushort keyCode, RdpKeyboardFlags flags)
    {
        uint time = 998877;
        var kbEvent = new RdpKeyboardEvent(time, flags, keyCode, isVirtualKey: false);
        var inputEvent = new RdpInputEvent(time, kbEvent);

        byte[] buffer = new byte[RdpInputEvent.EventLength];
        var writer = new RdpPacketWriter(buffer);
        inputEvent.Write(ref writer);

        Assert.Equal(RdpInputEvent.EventLength, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool readSuccess = RdpInputEvent.TryRead(ref reader, out var parsedEvent);

        Assert.True(readSuccess);
        Assert.Equal(RdpInputEvent.EventLength, reader.Position);
        Assert.Equal(time, parsedEvent.EventTime);
        Assert.Equal(RdpInputMessageType.ScanCode, parsedEvent.MessageType);
        Assert.False(parsedEvent.KeyboardEvent.IsVirtualKey);
        Assert.Equal(keyCode, parsedEvent.KeyboardEvent.KeyCode);
        Assert.Equal(flags, parsedEvent.KeyboardEvent.Flags);
    }

    [AvaloniaTheory]
    [InlineData(0x0D, RdpKeyboardFlags.None)] // VK_RETURN
    [InlineData(0x10, RdpKeyboardFlags.Release)] // VK_SHIFT release
    [InlineData(0x11, RdpKeyboardFlags.Extended)] // VK_CONTROL extended
    [InlineData(0x41, RdpKeyboardFlags.None)] // VK_A
    [InlineData(0x70, RdpKeyboardFlags.None)] // VK_F1
    [InlineData(0xB0, RdpKeyboardFlags.Extended | RdpKeyboardFlags.Release)] // VK_MEDIA_NEXT_TRACK
    [InlineData(0xFFFF, RdpKeyboardFlags.Extended1 | RdpKeyboardFlags.Release)] // Max ushort VkCode
    public void SlowPathVkCode_VirtualKeyModesAndFlags_RoundTripPreserved(ushort vkCode, RdpKeyboardFlags flags)
    {
        uint time = 554433;
        var kbEvent = new RdpKeyboardEvent(time, flags, vkCode, isVirtualKey: true);
        var inputEvent = new RdpInputEvent(time, kbEvent);

        byte[] buffer = new byte[RdpInputEvent.EventLength];
        var writer = new RdpPacketWriter(buffer);
        inputEvent.Write(ref writer);

        Assert.Equal(RdpInputEvent.EventLength, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool readSuccess = RdpInputEvent.TryRead(ref reader, out var parsedEvent);

        Assert.True(readSuccess);
        Assert.Equal(RdpInputMessageType.VkCode, parsedEvent.MessageType);
        Assert.True(parsedEvent.KeyboardEvent.IsVirtualKey);
        Assert.Equal(vkCode, parsedEvent.KeyboardEvent.KeyCode);
        Assert.Equal(flags, parsedEvent.KeyboardEvent.Flags);
    }

    [AvaloniaTheory]
    [InlineData(0x1E, FastPathKeyboardFlags.None)] // 'A' key down
    [InlineData(0x1E, FastPathKeyboardFlags.Release)] // 'A' key up
    [InlineData(0x1D, FastPathKeyboardFlags.Extended)] // Right Ctrl down (0xE0)
    [InlineData(0x1D, FastPathKeyboardFlags.Extended | FastPathKeyboardFlags.Release)] // Right Ctrl up (0xE0)
    [InlineData(0x45, FastPathKeyboardFlags.Extended1)] // Pause down (0xE1)
    [InlineData(0x45, FastPathKeyboardFlags.Extended1 | FastPathKeyboardFlags.Release)] // Pause up (0xE1)
    public void FastPathScancode_ExtendedFlagsAndRelease_HeaderAndPayloadRoundTrip(byte keyCode, FastPathKeyboardFlags flags)
    {
        var original = new RdpFastPathInputEvent(flags, keyCode);

        byte[] buffer = new byte[2];
        var writer = new RdpPacketWriter(buffer);
        original.Write(ref writer);

        Assert.Equal(2, writer.WrittenCount);

        // Inspect header byte bit layout
        byte headerByte = buffer[0];
        byte expectedEventCode = (byte)FastPathInputEventCode.ScanCode; // 0x00
        Assert.Equal(expectedEventCode, headerByte & 0x1F); // bits 0-4 must be event code

        byte expectedFlagBits = (byte)(((byte)flags & 0x07) << 5); // bits 5-7
        Assert.Equal(expectedFlagBits, headerByte & 0xE0);

        var reader = new RdpPacketReader(buffer);
        bool success = RdpFastPathInputEvent.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(2, reader.Position);
        Assert.Equal(FastPathInputEventCode.ScanCode, parsed.Code);
        Assert.Equal(keyCode, parsed.KeyCode);
        Assert.Equal(flags, parsed.KeyboardFlags);
    }
}
