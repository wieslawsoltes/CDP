using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Input;

using System;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using Xunit;

[Xunit.Collection("RdpTests")]
public class RdpInputTruncationAndBoundaryTests
{
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SlowPathHeader_TruncatedBuffer_ReturnsFalse(int length)
    {
        byte[] buffer = new byte[length];
        var reader = new RdpPacketReader(buffer);

        bool success = RdpInputPduReader.TryReadSlowPathHeader(ref reader, out ushort numEvents);

        Assert.False(success);
        Assert.Equal(0, numEvents);
        Assert.Equal(0, reader.Position); // Position remains unchanged on failure
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(11)]
    public void SlowPathInputEvent_TruncatedBuffer_ReturnsFalse(int length)
    {
        byte[] buffer = new byte[length];
        var reader = new RdpPacketReader(buffer);

        bool success = RdpInputEvent.TryRead(ref reader, out var ev);

        Assert.False(success);
        Assert.Equal(0, reader.Position);
    }

    [AvaloniaFact]
    public void SlowPathInputEvent_UnknownMessageType_ReturnsFalse()
    {
        byte[] buffer = new byte[RdpInputEvent.EventLength];
        var writer = new RdpPacketWriter(buffer);
        writer.WriteUInt32LE(100); // EventTime
        writer.WriteUInt16LE(0x9999); // Unknown MessageType
        writer.WriteUInt16LE(0); // Pad
        writer.WriteUInt32LE(0); // Payload

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputEvent.TryRead(ref reader, out _);

        Assert.False(success);
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    public void FastPathHeader_TruncatedBuffer_ReturnsFalse(int length)
    {
        byte[] buffer = new byte[length];
        var reader = new RdpPacketReader(buffer);

        bool success = RdpInputPduReader.TryReadFastPathHeader(ref reader, out _, out _, out _, out _);

        Assert.False(success);
    }

    [AvaloniaFact]
    public void FastPathHeader_ExtendedLength_MissingSecondByte_ReturnsFalse()
    {
        // Byte 0: Header (numEvents=1, action=0), Byte 1: 0x80 (indicates 2-byte length, but 2nd byte missing)
        byte[] buffer = new byte[2] { 0x04, 0x80 };
        var reader = new RdpPacketReader(buffer);

        bool success = RdpInputPduReader.TryReadFastPathHeader(ref reader, out _, out _, out _, out ushort pduLen);

        Assert.False(success);
        Assert.Equal(0, pduLen);
    }

    [AvaloniaFact]
    public void FastPathEvent_ScanCode_TruncatedPayload_ReturnsFalse()
    {
        // Header byte indicates FastPath ScanCode (code = 0x00), but keycode byte is missing
        byte[] buffer = new byte[1] { 0x00 };
        var reader = new RdpPacketReader(buffer);

        bool success = RdpFastPathInputEvent.TryRead(ref reader, out _);

        Assert.False(success);
    }

    [AvaloniaTheory]
    [InlineData(1)] // Header byte only (0 bytes payload remaining)
    [InlineData(2)] // 1 byte payload remaining (needs 6)
    [InlineData(3)] // 2 bytes payload remaining
    [InlineData(4)] // 3 bytes payload remaining
    [InlineData(5)] // 4 bytes payload remaining
    [InlineData(6)] // 5 bytes payload remaining
    public void FastPathEvent_Mouse_TruncatedPayload_ReturnsFalse(int totalBufferLength)
    {
        // Header byte indicates FastPath Mouse (code = 0x01)
        byte[] buffer = new byte[totalBufferLength];
        buffer[0] = 0x20; // Code = Mouse (0x01) in the high three bits

        var reader = new RdpPacketReader(buffer);
        bool success = RdpFastPathInputEvent.TryRead(ref reader, out _);

        Assert.False(success);
    }

    [AvaloniaFact]
    public void FastPathEvent_Sync_HeaderContainsToggleFlags()
    {
        byte[] buffer = new byte[1] { 0x63 };
        var reader = new RdpPacketReader(buffer);

        bool success = RdpFastPathInputEvent.TryRead(ref reader, out _);

        Assert.True(success);
    }

    [AvaloniaFact]
    public void FastPathEvent_UnknownEventCode_ReturnsFalse()
    {
        // Header byte with code = 0x07 (unknown/unassigned fastpath code).
        byte[] buffer = new byte[4] { 0xE0, 0x00, 0x00, 0x00 };
        var reader = new RdpPacketReader(buffer);

        bool success = RdpFastPathInputEvent.TryRead(ref reader, out _);

        Assert.False(success);
    }
}
