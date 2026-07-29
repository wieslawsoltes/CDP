using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Channels;

using System;
using System.Collections.Generic;
using CDP.Rdp.Channels;
using CDP.Rdp.Protocol;
using Xunit;

[Xunit.Collection("RdpTests")]
public class Challenger3EmpiricalVerificationTests
{
    // =========================================================================
    // Requirement 1: Empirical tests for DvcDataFirstHeader with multi-byte total
    // lengths (100, 500, 70,000 bytes) using packet buffer sizes < 256 bytes.
    // =========================================================================

    [AvaloniaTheory]
    [InlineData(100u, 15)]
    [InlineData(100u, 30)]
    [InlineData(100u, 50)]
    [InlineData(100u, 100)]
    [InlineData(100u, 200)]
    [InlineData(500u, 15)]
    [InlineData(500u, 30)]
    [InlineData(500u, 50)]
    [InlineData(500u, 100)]
    [InlineData(500u, 200)]
    [InlineData(70000u, 15)]
    [InlineData(70000u, 30)]
    [InlineData(70000u, 50)]
    [InlineData(70000u, 100)]
    [InlineData(70000u, 200)]
    public void DvcDataFirstHeader_MultiByteTotalLengths_SmallBufferSizes_ReadCorrectly(uint totalLength, int packetBufferSize)
    {
        uint channelId = 42;
        var header = new DvcDataFirstHeader(channelId, totalLength);

        // Allocate small packet buffer (< 256 bytes)
        byte[] buffer = new byte[packetBufferSize];
        var writer = new RdpPacketWriter(buffer);
        header.Write(ref writer);

        int written = writer.WrittenCount;
        Assert.True(written <= packetBufferSize, "Written header size must fit inside packet buffer.");

        // Read back from packet reader constructed over small buffer span
        var reader = new RdpPacketReader(buffer.AsSpan(0, written));
        bool success = DvcDataFirstHeader.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(channelId, parsed.ChannelId);
        Assert.Equal(totalLength, parsed.TotalLength);
    }

    [AvaloniaTheory]
    [InlineData(100, 50)]
    [InlineData(100, 128)]
    [InlineData(500, 50)]
    [InlineData(500, 100)]
    [InlineData(500, 200)]
    [InlineData(70000, 64)]
    [InlineData(70000, 128)]
    [InlineData(70000, 200)]
    public void DynamicManager_SendDvcData_MultiByteLengths_SmallChunkSizes_ReassemblesSuccessfully(int payloadLength, int maxChunkSize)
    {
        var manager = new DynamicVirtualChannelManager();
        byte[]? receivedPayload = null;
        uint channelId = 7;
        string channelName = "EMPIRICAL_TEST_DVC";

        manager.RegisterHandler(channelName, (id, payload) =>
        {
            Assert.Equal(channelId, id);
            receivedPayload = payload.ToArray();
        });

        // Register channel via CreateRequest
        var req = new DvcCreateRequestPdu(channelId, channelName);
        byte[] reqBuf = new byte[32];
        var reqWriter = new RdpPacketWriter(reqBuf);
        req.Write(ref reqWriter);
        Assert.True(manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqWriter.WrittenCount)));

        // Generate synthetic payload of specified length
        byte[] originalPayload = new byte[payloadLength];
        for (int i = 0; i < payloadLength; i++)
        {
            originalPayload[i] = (byte)((i * 31 + 17) & 0xFF);
        }

        // Chunk and send via SendDvcData with maxChunkSize < 256
        var packets = new List<byte[]>();
        DynamicVirtualChannelManager.SendDvcData(channelId, originalPayload, maxChunkSize, packet =>
        {
            packets.Add(packet.ToArray());
        });

        // Verify each packet is smaller than or equal to maxChunkSize
        foreach (var pkt in packets)
        {
            Assert.True(pkt.Length <= maxChunkSize, $"Packet length {pkt.Length} exceeds maxChunkSize {maxChunkSize}");
        }

        // Process all packets through DynamicVirtualChannelManager
        bool allProcessed = true;
        foreach (var pkt in packets)
        {
            allProcessed &= manager.ProcessIncomingPacket(pkt);
        }

        Assert.True(allProcessed, "All DVC packets must be processed without parsing errors.");
        Assert.NotNull(receivedPayload);
        Assert.Equal(originalPayload.Length, receivedPayload.Length);
        Assert.Equal(originalPayload, receivedPayload);
    }

    // =========================================================================
    // Requirement 2: Truncated CreateResponse PDUs (< 4 bytes status) return false cleanly.
    // =========================================================================

    [AvaloniaTheory]
    [InlineData(0)] // 0 bytes status after channelId
    [InlineData(1)] // 1 byte status after channelId
    [InlineData(2)] // 2 bytes status after channelId
    [InlineData(3)] // 3 bytes status after channelId
    public void DvcCreateResponsePdu_TruncatedStatus_TryRead_ReturnsFalseCleanly(int statusBytesPresent)
    {
        uint channelId = 15;
        byte sp = DvcValueCodec.GetRequiredSp(channelId);
        var header = new DvcHeader(DvcCommandCode.Create, sp);

        byte[] buffer = new byte[1 + 1 + statusBytesPresent];
        var writer = new RdpPacketWriter(buffer);
        header.Write(ref writer);
        DvcValueCodec.WriteValue(ref writer, sp, channelId);

        for (int i = 0; i < statusBytesPresent; i++)
        {
            writer.WriteByte(0xFF);
        }

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcCreateResponsePdu.TryRead(ref reader, out var pdu);

        Assert.False(success);
        Assert.Equal(0u, pdu.ChannelId);
        Assert.Equal(0, pdu.CreationStatus);
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DynamicManager_TruncatedCreateResponse_ProcessIncomingPacket_ReturnsFalseCleanly(int statusBytesPresent)
    {
        var manager = new DynamicVirtualChannelManager();

        uint channelId = 25;
        byte sp = DvcValueCodec.GetRequiredSp(channelId);
        var header = new DvcHeader(DvcCommandCode.Create, sp);

        byte[] buffer = new byte[1 + 1 + statusBytesPresent];
        var writer = new RdpPacketWriter(buffer);
        header.Write(ref writer);
        DvcValueCodec.WriteValue(ref writer, sp, channelId);

        for (int i = 0; i < statusBytesPresent; i++)
        {
            writer.WriteByte((byte)(i + 1));
        }

        bool result = manager.ProcessIncomingPacket(buffer.AsSpan(0, writer.WrittenCount));

        Assert.False(result);
    }
}
