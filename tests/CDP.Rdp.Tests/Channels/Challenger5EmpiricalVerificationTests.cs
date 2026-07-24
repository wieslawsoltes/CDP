namespace CDP.Rdp.Tests.Channels;

using System;
using System.Collections.Generic;
using CDP.Rdp.Channels;
using CDP.Rdp.Protocol;
using Xunit;

public class Challenger5EmpiricalVerificationTests
{
    // =========================================================================
    // Requirement 1: Empirical tests creating dynamic channels with 3-character
    // names ("DVC", "FOO", "CTX", "SND"), verifying success and response PDU generation.
    // =========================================================================

    [Theory]
    [InlineData("DVC", 1u, (byte)0)]
    [InlineData("FOO", 2u, (byte)1)]
    [InlineData("CTX", 3u, (byte)2)]
    [InlineData("SND", 4u, (byte)3)]
    public void DvcCreateRequestPdu_ThreeCharNames_RoundTrip(string channelName, uint channelId, byte priority)
    {
        var reqPdu = new DvcCreateRequestPdu(channelId, channelName, priority);

        byte[] buffer = new byte[32];
        var writer = new RdpPacketWriter(buffer);
        reqPdu.Write(ref writer);

        int written = writer.WrittenCount;
        // Header (1) + ChannelId (1 for ID <= 255) + 3 chars + null terminator (1) = 6 bytes
        Assert.Equal(6, written);

        var reader = new RdpPacketReader(buffer.AsSpan(0, written));
        bool success = DvcCreateRequestPdu.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(channelId, parsed.ChannelId);
        Assert.Equal(channelName, parsed.ChannelName);
        Assert.Equal(priority, parsed.Priority);
    }

    [Theory]
    [InlineData("DVC", 10u)]
    [InlineData("FOO", 20u)]
    [InlineData("CTX", 30u)]
    [InlineData("SND", 40u)]
    public void DynamicManager_ThreeCharChannelNames_RegisterAndCreateSuccess(string channelName, uint channelId)
    {
        var manager = new DynamicVirtualChannelManager();
        byte[]? receivedPayload = null;
        byte[]? responsePduBytes = null;

        // 1. Register handler for 3-character channel name
        manager.RegisterHandler(channelName, (id, payload) =>
        {
            Assert.Equal(channelId, id);
            receivedPayload = payload.ToArray();
        });

        // 2. Build DvcCreateRequestPdu packet
        var reqPdu = new DvcCreateRequestPdu(channelId, channelName, priority: 1);
        byte[] reqBuf = new byte[32];
        var writer = new RdpPacketWriter(reqBuf);
        reqPdu.Write(ref writer);

        // 3. Process incoming CreateRequest PDU and capture reply callback
        bool processed = manager.ProcessIncomingPacket(reqBuf.AsSpan(0, writer.WrittenCount), reply =>
        {
            responsePduBytes = reply.ToArray();
        });

        Assert.True(processed, "ProcessIncomingPacket should return true for valid CreateRequest PDU.");
        Assert.NotNull(responsePduBytes);

        // 4. Verify CreateResponse PDU content
        var rspReader = new RdpPacketReader(responsePduBytes);
        bool rspSuccess = DvcCreateResponsePdu.TryRead(ref rspReader, out var rspPdu);
        Assert.True(rspSuccess, "Response PDU should be a valid DvcCreateResponsePdu.");
        Assert.Equal(channelId, rspPdu.ChannelId);
        Assert.Equal(0, rspPdu.CreationStatus); // STATUS_SUCCESS = 0
        Assert.True(rspPdu.IsSuccess);
        Assert.Equal(1, rspPdu.Priority);

        // 5. Verify channel is functional by sending data
        byte[] testData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        byte[] dataBuf = new byte[32];
        var dataWriter = new RdpPacketWriter(dataBuf);
        var dataHeader = new DvcDataHeader(channelId, priority: 1);
        dataHeader.Write(ref dataWriter);
        dataWriter.WriteSpan(testData);

        bool dataProcessed = manager.ProcessIncomingPacket(dataBuf.AsSpan(0, dataWriter.WrittenCount));
        Assert.True(dataProcessed);
        Assert.NotNull(receivedPayload);
        Assert.Equal(testData, receivedPayload);
    }

    [Fact]
    public void DynamicManager_MultipleThreeCharChannels_OpenConcurrently()
    {
        var manager = new DynamicVirtualChannelManager();
        var receivedMap = new Dictionary<uint, byte[]>();

        string[] names = new[] { "DVC", "FOO", "CTX", "SND" };
        uint[] ids = new uint[] { 101, 102, 103, 104 };

        for (int i = 0; i < names.Length; i++)
        {
            uint id = ids[i];
            string name = names[i];

            manager.RegisterHandler(name, (chanId, payload) =>
            {
                receivedMap[chanId] = payload.ToArray();
            });

            var req = new DvcCreateRequestPdu(id, name);
            byte[] buf = new byte[32];
            var w = new RdpPacketWriter(buf);
            req.Write(ref w);

            byte[]? rspBytes = null;
            bool ok = manager.ProcessIncomingPacket(buf.AsSpan(0, w.WrittenCount), r => rspBytes = r.ToArray());
            Assert.True(ok);
            Assert.NotNull(rspBytes);

            var rReader = new RdpPacketReader(rspBytes);
            Assert.True(DvcCreateResponsePdu.TryRead(ref rReader, out var rsp));
            Assert.True(rsp.IsSuccess);
            Assert.Equal(id, rsp.ChannelId);
        }

        // Send data to each channel
        for (int i = 0; i < names.Length; i++)
        {
            uint id = ids[i];
            byte[] data = new byte[] { (byte)i, 0xAA, 0xBB, (byte)(i * 10) };

            byte[] dataBuf = new byte[32];
            var dw = new RdpPacketWriter(dataBuf);
            var dh = new Rdp.Channels.DvcDataHeader(id);
            dh.Write(ref dw);
            dw.WriteSpan(data);

            manager.ProcessIncomingPacket(dataBuf.AsSpan(0, dw.WrittenCount));

            Assert.True(receivedMap.ContainsKey(id));
            Assert.Equal(data, receivedMap[id]);
        }
    }

    [Theory]
    [InlineData("DVC")]
    [InlineData("FOO")]
    [InlineData("CTX")]
    [InlineData("SND")]
    public void DynamicManager_UnregisteredThreeCharChannel_ReturnsUnsuccessfulResponse(string channelName)
    {
        var manager = new DynamicVirtualChannelManager();
        byte[]? responsePduBytes = null;

        var reqPdu = new DvcCreateRequestPdu(channelId: 99, channelName: channelName);
        byte[] reqBuf = new byte[32];
        var writer = new RdpPacketWriter(reqBuf);
        reqPdu.Write(ref writer);

        bool processed = manager.ProcessIncomingPacket(reqBuf.AsSpan(0, writer.WrittenCount), reply =>
        {
            responsePduBytes = reply.ToArray();
        });

        Assert.True(processed);
        Assert.NotNull(responsePduBytes);

        var rspReader = new RdpPacketReader(responsePduBytes);
        Assert.True(DvcCreateResponsePdu.TryRead(ref rspReader, out var rspPdu));
        Assert.Equal(99u, rspPdu.ChannelId);
        Assert.False(rspPdu.IsSuccess);
        Assert.Equal(unchecked((int)0xC0000001), rspPdu.CreationStatus);
    }

    // =========================================================================
    // Requirement 2: Verify multi-byte total length DVC framing and small packet chunk reassembly.
    // =========================================================================

    [Theory]
    [InlineData(100u, 0)]    // lenSp = 0 (1-byte length)
    [InlineData(255u, 0)]    // lenSp = 0 (1-byte length max)
    [InlineData(256u, 1)]    // lenSp = 1 (2-byte length min)
    [InlineData(500u, 1)]    // lenSp = 1 (2-byte length)
    [InlineData(65535u, 1)]  // lenSp = 1 (2-byte length max)
    [InlineData(65536u, 2)]  // lenSp = 2 (4-byte length min)
    [InlineData(70000u, 2)]  // lenSp = 2 (4-byte length)
    [InlineData(1000000u, 2)]// lenSp = 2 (4-byte length)
    public void DvcDataFirstHeader_MultiByteTotalLength_LenSpEncoding_RoundTrip(uint totalLength, byte expectedLenSp)
    {
        uint channelId = 5;
        byte sp = DvcValueCodec.GetRequiredSp(channelId);
        byte lenSp = DvcValueCodec.GetRequiredSp(totalLength);
        Assert.Equal(expectedLenSp, lenSp);

        var header = new DvcDataFirstHeader(channelId, totalLength);
        byte[] buffer = new byte[32];
        var writer = new RdpPacketWriter(buffer);
        header.Write(ref writer);

        byte firstByte = buffer[0];
        byte lenSpInHeader = (byte)((firstByte >> 6) & 0x03);
        Assert.Equal(expectedLenSp, lenSpInHeader);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcDataFirstHeader.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(channelId, parsed.ChannelId);
        Assert.Equal(totalLength, parsed.TotalLength);
    }

    [Theory]
    [InlineData("DVC", 500, 15)]   // 500 bytes payload, 15 byte max chunk
    [InlineData("FOO", 500, 25)]   // 500 bytes payload, 25 byte max chunk
    [InlineData("CTX", 70000, 32)] // 70,000 bytes payload, 32 byte max chunk
    [InlineData("SND", 70000, 64)] // 70,000 bytes payload, 64 byte max chunk
    [InlineData("DVC", 1000, 12)]  // 1,000 bytes payload, 12 byte max chunk
    public void DynamicManager_ThreeCharChannels_MultiByteLength_SmallChunkReassembly(string channelName, int payloadSize, int maxChunkSize)
    {
        var manager = new DynamicVirtualChannelManager();
        byte[]? receivedPayload = null;
        uint channelId = 50;

        manager.RegisterHandler(channelName, (id, payload) =>
        {
            Assert.Equal(channelId, id);
            receivedPayload = payload.ToArray();
        });

        // Open channel via CreateRequest
        var req = new DvcCreateRequestPdu(channelId, channelName);
        byte[] reqBuf = new byte[32];
        var reqWriter = new RdpPacketWriter(reqBuf);
        req.Write(ref reqWriter);
        Assert.True(manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqWriter.WrittenCount)));

        // Create synthetic payload
        byte[] originalPayload = new byte[payloadSize];
        for (int i = 0; i < payloadSize; i++)
        {
            originalPayload[i] = (byte)((i * 17 + 3) & 0xFF);
        }

        // Chunk using SendDvcData
        var packets = new List<byte[]>();
        DynamicVirtualChannelManager.SendDvcData(channelId, originalPayload, maxChunkSize, packet =>
        {
            packets.Add(packet.ToArray());
        });

        Assert.True(packets.Count > 1, "Payload should be split into multiple chunks.");

        // Process all packets sequentially
        bool allProcessed = true;
        foreach (var pkt in packets)
        {
            Assert.True(pkt.Length <= maxChunkSize, $"Packet size {pkt.Length} exceeds maxChunkSize {maxChunkSize}");
            allProcessed &= manager.ProcessIncomingPacket(pkt);
        }

        Assert.True(allProcessed, "All incoming chunks must be successfully processed.");
        Assert.NotNull(receivedPayload);
        Assert.Equal(originalPayload.Length, receivedPayload.Length);
        Assert.Equal(originalPayload, receivedPayload);
    }

    [Fact]
    public void DynamicManager_InterleavedSmallChunks_AcrossMultipleThreeCharChannels()
    {
        var manager = new DynamicVirtualChannelManager();
        var receivedMap = new Dictionary<uint, byte[]>();

        string[] names = new[] { "DVC", "FOO", "CTX", "SND" };
        uint[] ids = new uint[] { 10, 20, 30, 40 };
        int[] payloadSizes = new int[] { 300, 600, 70000, 1500 };
        int maxChunkSize = 30;

        var allPacketsPerChannel = new Dictionary<uint, List<byte[]>>();

        for (int i = 0; i < names.Length; i++)
        {
            uint id = ids[i];
            string name = names[i];
            int size = payloadSizes[i];

            manager.RegisterHandler(name, (chanId, payload) =>
            {
                receivedMap[chanId] = payload.ToArray();
            });

            var req = new DvcCreateRequestPdu(id, name);
            byte[] reqBuf = new byte[32];
            var w = new RdpPacketWriter(reqBuf);
            req.Write(ref w);
            Assert.True(manager.ProcessIncomingPacket(reqBuf.AsSpan(0, w.WrittenCount)));

            byte[] payload = new byte[size];
            for (int j = 0; j < size; j++) payload[j] = (byte)((id + j) & 0xFF);

            var packets = new List<byte[]>();
            DynamicVirtualChannelManager.SendDvcData(id, payload, maxChunkSize, p => packets.Add(p.ToArray()));
            allPacketsPerChannel[id] = packets;
        }

        // Interleave packets round-robin across all 4 channels
        int maxPackets = 0;
        foreach (var list in allPacketsPerChannel.Values)
        {
            if (list.Count > maxPackets) maxPackets = list.Count;
        }

        for (int step = 0; step < maxPackets; step++)
        {
            foreach (var id in ids)
            {
                var list = allPacketsPerChannel[id];
                if (step < list.Count)
                {
                    bool ok = manager.ProcessIncomingPacket(list[step]);
                    Assert.True(ok, $"Packet at step {step} for channel {id} failed to process.");
                }
            }
        }

        // Assert all 4 channels received complete reassembled payloads correctly
        for (int i = 0; i < names.Length; i++)
        {
            uint id = ids[i];
            int size = payloadSizes[i];
            Assert.True(receivedMap.ContainsKey(id), $"Channel {id} payload not received.");
            Assert.Equal(size, receivedMap[id].Length);

            byte[] expected = new byte[size];
            for (int j = 0; j < size; j++) expected[j] = (byte)((id + j) & 0xFF);
            Assert.Equal(expected, receivedMap[id]);
        }
    }

    // =========================================================================
    // Area 3: Empirical Findings and Boundary Conditions
    // =========================================================================

    [Fact]
    public void DynamicManager_CreateRequest_MissingNullTerminator_FailsGracefully()
    {
        var manager = new DynamicVirtualChannelManager();

        // 3-character channel name "DVC" without null terminator at end of buffer (5 bytes total: header + id + 3 chars)
        byte[] malformedBuf = new byte[]
        {
            0x01, // Cmd = Create, Sp = 0, Pri = 0
            0x01, // ChannelId = 1
            (byte)'D', (byte)'V', (byte)'C' // Missing 0x00
        };

        bool result = manager.ProcessIncomingPacket(malformedBuf);

        Assert.False(result, "CreateRequest PDU without null terminator must fail gracefully.");
    }

    [Fact]
    public void DynamicManager_ThreeCharChannelName_StructuralOverlapWithCreateResponse_EmpiricalDiscovery()
    {
        // Empirical test demonstrating that a 3-character channel name payload (3 bytes string + 1 byte null = 4 bytes)
        // has exact 4-byte structural overlap with DvcCreateResponsePdu (which has a 4-byte CreationStatus field).
        var manager = new DynamicVirtualChannelManager();

        byte[] invalidNameBuf = new byte[]
        {
            0x01, // Cmd = Create (0x01)
            0x02, // ChannelId = 2
            (byte)'D', (byte)'V', 0x05, 0x00 // 3 chars + null (4 bytes total, but contains non-printable 0x05)
        };

        // ProcessIncomingPacket first tries CreateRequest. TryRead succeeds, but IsValidChannelName returns false.
        // It then falls through to the CreateResponse branch. Since UnreadLength == 4 (the 4 bytes of 'D','V',0x05,0x00),
        // DvcCreateResponsePdu.TryRead succeeds by interpreting 'D','V',0x05,0x00 as CreationStatus 0x00055644.
        bool result = manager.ProcessIncomingPacket(invalidNameBuf);

        // We empirically document this fallthrough behavior:
        Assert.True(result, "Empirical Finding: Invalid 3-char name payload (4 bytes) falls through and parses as CreateResponse.");
    }

    [Fact]
    public void DynamicManager_DvcDataFirst_ZeroTotalLength_FailsGracefully()
    {
        var manager = new DynamicVirtualChannelManager();
        manager.RegisterHandler("DVC", (_, _) => { });

        var req = new DvcCreateRequestPdu(1, "DVC");
        byte[] reqBuf = new byte[32];
        var w = new RdpPacketWriter(reqBuf);
        req.Write(ref w);
        manager.ProcessIncomingPacket(reqBuf.AsSpan(0, w.WrittenCount));

        // DataFirst with TotalLength = 0
        var header = new DvcDataFirstHeader(1, totalLength: 0);
        byte[] dataFirstBuf = new byte[32];
        var dw = new RdpPacketWriter(dataFirstBuf);
        header.Write(ref dw);

        bool result = manager.ProcessIncomingPacket(dataFirstBuf.AsSpan(0, dw.WrittenCount));

        Assert.False(result, "DataFirst with TotalLength = 0 must fail gracefully.");
    }

    [Fact]
    public void DynamicManager_DvcDataFirst_ExceedingMaxMessageSize_FailsGracefully()
    {
        var manager = new DynamicVirtualChannelManager();
        manager.RegisterHandler("DVC", (_, _) => { });

        var req = new DvcCreateRequestPdu(1, "DVC");
        byte[] reqBuf = new byte[32];
        var w = new RdpPacketWriter(reqBuf);
        req.Write(ref w);
        manager.ProcessIncomingPacket(reqBuf.AsSpan(0, w.WrittenCount));

        // DataFirst with TotalLength = 17,000,000 (> 16MB MaxMessageSize)
        var header = new DvcDataFirstHeader(1, totalLength: 17 * 1024 * 1024);
        byte[] dataFirstBuf = new byte[32];
        var dw = new RdpPacketWriter(dataFirstBuf);
        header.Write(ref dw);

        bool result = manager.ProcessIncomingPacket(dataFirstBuf.AsSpan(0, dw.WrittenCount));

        Assert.False(result, "DataFirst exceeding 16MB MaxMessageSize must fail gracefully.");
    }

    [Fact]
    public void DynamicManager_DataChunkExceedingTotalLength_FailsGracefully()
    {
        var manager = new DynamicVirtualChannelManager();
        byte[]? received = null;
        manager.RegisterHandler("DVC", (_, payload) => received = payload.ToArray());

        var req = new DvcCreateRequestPdu(1, "DVC");
        byte[] reqBuf = new byte[32];
        var w = new RdpPacketWriter(reqBuf);
        req.Write(ref w);
        manager.ProcessIncomingPacket(reqBuf.AsSpan(0, w.WrittenCount));

        // DataFirst totalLength = 10 bytes
        var firstHeader = new DvcDataFirstHeader(1, totalLength: 10);
        byte[] firstBuf = new byte[32];
        var fw = new RdpPacketWriter(firstBuf);
        firstHeader.Write(ref fw);
        fw.WriteSpan(new byte[] { 1, 2, 3, 4, 5 }); // 5 bytes
        Assert.True(manager.ProcessIncomingPacket(firstBuf.AsSpan(0, fw.WrittenCount)));

        // Data PDU with 10 bytes (5 + 10 = 15 > 10 totalLength)
        var dataHeader = new DvcDataHeader(1);
        byte[] dataBuf = new byte[32];
        var dw = new RdpPacketWriter(dataBuf);
        dataHeader.Write(ref dw);
        dw.WriteSpan(new byte[] { 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }); // 10 bytes

        bool result = manager.ProcessIncomingPacket(dataBuf.AsSpan(0, dw.WrittenCount));

        Assert.False(result, "Data PDU exceeding TotalLength must return false and mark buffer invalid.");
        Assert.Null(received);
    }
}
