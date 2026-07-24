namespace CDP.Rdp.Tests.Input;

using System;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using Xunit;

public class RdpInputTruncationAndBoundaryTests
{
    [Theory]
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(13)]
    public void SlowPathInputEvent_TruncatedBuffer_ReturnsFalse(int length)
    {
        byte[] buffer = new byte[length];
        var reader = new RdpPacketReader(buffer);

        bool success = RdpInputEvent.TryRead(ref reader, out var ev);

        Assert.False(success);
        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public void SlowPathInputEvent_UnknownMessageType_ReturnsFalse()
    {
        byte[] buffer = new byte[14];
        var writer = new RdpPacketWriter(buffer);
        writer.WriteUInt32LE(100); // EventTime
        writer.WriteUInt16LE(0x9999); // Unknown MessageType
        writer.WriteUInt16LE(0); // Pad
        writer.WriteUInt32LE(0); // Payload
        writer.WriteUInt16LE(0); // Payload

        var reader = new RdpPacketReader(buffer);
        bool success = RdpInputEvent.TryRead(ref reader, out _);

        Assert.False(success);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void FastPathHeader_TruncatedBuffer_ReturnsFalse(int length)
    {
        byte[] buffer = new byte[length];
        var reader = new RdpPacketReader(buffer);

        bool success = RdpInputPduReader.TryReadFastPathHeader(ref reader, out _, out _, out _, out _);

        Assert.False(success);
    }

    [Fact]
    public void FastPathHeader_ExtendedLength_MissingSecondByte_ReturnsFalse()
    {
        // Byte 0: Header (numEvents=1, action=0), Byte 1: 0x80 (indicates 2-byte length, but 2nd byte missing)
        byte[] buffer = new byte[2] { 0x04, 0x80 };
        var reader = new RdpPacketReader(buffer);

        bool success = RdpInputPduReader.TryReadFastPathHeader(ref reader, out _, out _, out _, out ushort pduLen);

        Assert.False(success);
        Assert.Equal(0, pduLen);
    }

    [Fact]
    public void FastPathEvent_ScanCode_TruncatedPayload_ReturnsFalse()
    {
        // Header byte indicates FastPath ScanCode (code = 0x00), but keycode byte is missing
        byte[] buffer = new byte[1] { 0x00 };
        var reader = new RdpPacketReader(buffer);

        bool success = RdpFastPathInputEvent.TryRead(ref reader, out _);

        Assert.False(success);
    }

    [Theory]
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
        buffer[0] = 0x01; // Code = Mouse (0x01)

        var reader = new RdpPacketReader(buffer);
        bool success = RdpFastPathInputEvent.TryRead(ref reader, out _);

        Assert.False(success);
    }

    [Fact]
    public void FastPathEvent_Sync_TruncatedPayload_ReturnsFalse()
    {
        // Header byte indicates FastPath Sync (code = 0x03), but toggle flags byte is missing
        byte[] buffer = new byte[1] { 0x03 };
        var reader = new RdpPacketReader(buffer);

        bool success = RdpFastPathInputEvent.TryRead(ref reader, out _);

        Assert.False(success);
    }

    [Fact]
    public void FastPathEvent_UnknownEventCode_ReturnsFalse()
    {
        // Header byte with code = 0x1F (unknown/unassigned fastpath code)
        byte[] buffer = new byte[4] { 0x1F, 0x00, 0x00, 0x00 };
        var reader = new RdpPacketReader(buffer);

        bool success = RdpFastPathInputEvent.TryRead(ref reader, out _);

        Assert.False(success);
    }
}
