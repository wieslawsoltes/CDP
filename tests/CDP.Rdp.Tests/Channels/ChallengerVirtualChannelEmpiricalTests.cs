namespace CDP.Rdp.Tests.Channels;

using System;
using System.Collections.Generic;
using CDP.Rdp.Channels;
using CDP.Rdp.Protocol;
using Xunit;

public class ChallengerVirtualChannelEmpiricalTests
{
    // =========================================================================
    // Area 1: Multi-chunk SVC reassembly with boundary payload sizes
    // =========================================================================

    [Theory]
    [InlineData(0, 50)]      // Empty payload
    [InlineData(41, 50)]     // 1 byte below chunk capacity (maxPayloadPerChunk = 50 - 8 = 42)
    [InlineData(42, 50)]     // Exact chunk capacity (1 chunk)
    [InlineData(43, 50)]     // 1 byte above chunk capacity (2 chunks: 42 + 1)
    [InlineData(84, 50)]     // Exact 2x chunk capacity (2 chunks: 42 + 42)
    [InlineData(85, 50)]     // 2x chunk capacity + 1 (3 chunks: 42 + 42 + 1)
    public void StaticManager_BoundaryPayloadSizes_ChunkAndReassemble(int payloadSize, int maxChunkSize)
    {
        var manager = new StaticVirtualChannelManager();
        byte[]? receivedData = null;
        ushort targetChannel = 1005;

        manager.RegisterChannel("test_channel", targetChannel, (_, payload) =>
        {
            receivedData = payload.ToArray();
        });

        byte[] originalPayload = new byte[payloadSize];
        for (int i = 0; i < payloadSize; i++) originalPayload[i] = (byte)(i & 0xFF);

        var chunks = new List<byte[]>();
        StaticVirtualChannelManager.ChunkMessage(originalPayload, maxChunkSize, chunk =>
        {
            chunks.Add(chunk.ToArray());
        });

        int maxPayloadPerChunk = maxChunkSize - ChannelPduHeader.HeaderLength;
        int expectedChunkCount = payloadSize == 0 ? 1 : (int)Math.Ceiling((double)payloadSize / maxPayloadPerChunk);
        Assert.Equal(expectedChunkCount, chunks.Count);

        bool allProcessed = true;
        foreach (var chunk in chunks)
        {
            allProcessed &= manager.ProcessIncomingPacket(targetChannel, chunk);
        }

        Assert.True(allProcessed);
        Assert.NotNull(receivedData);
        Assert.Equal(originalPayload, receivedData);
    }

    [Fact]
    public void StaticManager_MinimumValidChunkSize_ReassemblesCorrectly()
    {
        var manager = new StaticVirtualChannelManager();
        byte[]? receivedData = null;
        ushort channelId = 1008;

        manager.RegisterChannel("min_chunk_chan", channelId, (_, payload) =>
        {
            receivedData = payload.ToArray();
        });

        // maxChunkSize = HeaderLength + 1 = 9 -> 1 byte payload per chunk
        byte[] payload = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE };
        var chunks = new List<byte[]>();

        StaticVirtualChannelManager.ChunkMessage(payload, maxChunkSize: 9, chunk =>
        {
            chunks.Add(chunk.ToArray());
        });

        Assert.Equal(5, chunks.Count);

        foreach (var chunk in chunks)
        {
            Assert.Equal(9, chunk.Length);
            manager.ProcessIncomingPacket(channelId, chunk);
        }

        Assert.NotNull(receivedData);
        Assert.Equal(payload, receivedData);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(7)]
    [InlineData(0)]
    [InlineData(-1)]
    public void StaticManager_InvalidMaxChunkSize_ThrowsArgumentOutOfRangeException(int invalidMaxChunkSize)
    {
        byte[] payload = new byte[10];
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            StaticVirtualChannelManager.ChunkMessage(payload, invalidMaxChunkSize, _ => { });
        });
    }

    [Fact]
    public void StaticManager_InterleavedMultiChannelChunks_ReassemblesSeparately()
    {
        var manager = new StaticVirtualChannelManager();
        byte[]? ch1Received = null;
        byte[]? ch2Received = null;

        ushort ch1Id = 1001;
        ushort ch2Id = 1002;

        manager.RegisterChannel("chan1", ch1Id, (_, payload) => ch1Received = payload.ToArray());
        manager.RegisterChannel("chan2", ch2Id, (_, payload) => ch2Received = payload.ToArray());

        byte[] ch1Payload = new byte[100];
        byte[] ch2Payload = new byte[100];
        for (int i = 0; i < 100; i++)
        {
            ch1Payload[i] = (byte)(0x10 + i);
            ch2Payload[i] = (byte)(0x80 + i);
        }

        var ch1Chunks = new List<byte[]>();
        var ch2Chunks = new List<byte[]>();

        StaticVirtualChannelManager.ChunkMessage(ch1Payload, maxChunkSize: 35, c => ch1Chunks.Add(c.ToArray()));
        StaticVirtualChannelManager.ChunkMessage(ch2Payload, maxChunkSize: 35, c => ch2Chunks.Add(c.ToArray()));

        // Interleave chunk delivery: Ch1[0], Ch2[0], Ch1[1], Ch2[1], ...
        int maxLen = Math.Max(ch1Chunks.Count, ch2Chunks.Count);
        for (int i = 0; i < maxLen; i++)
        {
            if (i < ch1Chunks.Count) manager.ProcessIncomingPacket(ch1Id, ch1Chunks[i]);
            if (i < ch2Chunks.Count) manager.ProcessIncomingPacket(ch2Id, ch2Chunks[i]);
        }

        Assert.NotNull(ch1Received);
        Assert.NotNull(ch2Received);
        Assert.Equal(ch1Payload, ch1Received);
        Assert.Equal(ch2Payload, ch2Received);
    }

    // =========================================================================
    // Area 2: DVC variable-length field (Sp = 0, 1, 2) edge cases
    // =========================================================================

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(255u, 0)]
    [InlineData(256u, 1)]
    [InlineData(65535u, 1)]
    [InlineData(65536u, 2)]
    [InlineData(uint.MaxValue, 2)]
    public void DvcValueCodec_GetRequiredSp_Boundaries(uint value, byte expectedSp)
    {
        Assert.Equal(expectedSp, DvcValueCodec.GetRequiredSp(value));
    }

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(255u, 0)]
    [InlineData(256u, 1)]
    [InlineData(65535u, 1)]
    [InlineData(65536u, 2)]
    [InlineData(uint.MaxValue, 2)]
    public void DvcValueCodec_ValueCodec_RoundTrip(uint value, byte sp)
    {
        byte[] buffer = new byte[8];
        var writer = new RdpPacketWriter(buffer);
        DvcValueCodec.WriteValue(ref writer, sp, value);

        int written = writer.WrittenCount;
        int expectedBytes = sp == 0 ? 1 : (sp == 1 ? 2 : 4);
        Assert.Equal(expectedBytes, written);

        var reader = new RdpPacketReader(buffer.AsSpan(0, written));
        bool success = DvcValueCodec.TryReadValue(ref reader, sp, out uint parsedValue);

        Assert.True(success);
        Assert.Equal(value, parsedValue);
        Assert.Equal(written, reader.Position);
    }

    [Theory]
    [InlineData(0, 0)] // Sp=0 expects 1 byte, 0 provided
    [InlineData(1, 1)] // Sp=1 expects 2 bytes, 1 provided
    [InlineData(2, 3)] // Sp=2 expects 4 bytes, 3 provided
    [InlineData(3, 4)] // Sp=3 is invalid
    public void DvcValueCodec_InsufficientBytes_ReturnsFalse(byte sp, int availableBytes)
    {
        byte[] buffer = new byte[availableBytes];
        var reader = new RdpPacketReader(buffer);

        bool success = DvcValueCodec.TryReadValue(ref reader, sp, out uint val);

        Assert.False(success);
        Assert.Equal(0u, val);
    }

    [Theory]
    [InlineData(5u, 0)]          // 1-byte ChannelId
    [InlineData(0x1A0u, 1)]      // 2-byte ChannelId (416)
    [InlineData(0x10000u, 2)]    // 4-byte ChannelId (65536)
    public void DvcPdu_ChannelIdBoundaries_RoundTrip(uint channelId, byte expectedSp)
    {
        Assert.Equal(expectedSp, DvcValueCodec.GetRequiredSp(channelId));

        // Test DvcCreateRequestPdu
        var req = new DvcCreateRequestPdu(channelId, "RAIL");
        byte[] reqBuf = new byte[32];
        var reqWriter = new RdpPacketWriter(reqBuf);
        req.Write(ref reqWriter);

        var reqReader = new RdpPacketReader(reqBuf.AsSpan(0, reqWriter.WrittenCount));
        Assert.True(DvcCreateRequestPdu.TryRead(ref reqReader, out var parsedReq));
        Assert.Equal(channelId, parsedReq.ChannelId);

        // Test DvcCreateResponsePdu
        var rsp = new DvcCreateResponsePdu(channelId, 0);
        byte[] rspBuf = new byte[16];
        var rspWriter = new RdpPacketWriter(rspBuf);
        rsp.Write(ref rspWriter);

        var rspReader = new RdpPacketReader(rspBuf.AsSpan(0, rspWriter.WrittenCount));
        Assert.True(DvcCreateResponsePdu.TryRead(ref rspReader, out var parsedRsp));
        Assert.Equal(channelId, parsedRsp.ChannelId);

        // Test DvcClosePdu
        var closePdu = new DvcClosePdu(channelId);
        byte[] closeBuf = new byte[16];
        var closeWriter = new RdpPacketWriter(closeBuf);
        closePdu.Write(ref closeWriter);

        var closeReader = new RdpPacketReader(closeBuf.AsSpan(0, closeWriter.WrittenCount));
        Assert.True(DvcClosePdu.TryRead(ref closeReader, out var parsedClose));
        Assert.Equal(channelId, parsedClose.ChannelId);

        // Test DvcDataHeader
        var dataHeader = new DvcDataHeader(channelId);
        byte[] dataBuf = new byte[16];
        var dataWriter = new RdpPacketWriter(dataBuf);
        dataHeader.Write(ref dataWriter);

        var dataReader = new RdpPacketReader(dataBuf.AsSpan(0, dataWriter.WrittenCount));
        Assert.True(DvcDataHeader.TryRead(ref dataReader, out var parsedData));
        Assert.Equal(channelId, parsedData.ChannelId);
    }

    // =========================================================================
    // Area 3: DVC DataFirst + Data chunking and reassembly across multiple channels
    // =========================================================================

    [Fact]
    public void DynamicManager_MultiChannelInterleavedDvcData_ReassemblesCorrectly()
    {
        var manager = new DynamicVirtualChannelManager();
        byte[]? ch1Received = null;
        byte[]? ch2Received = null;

        manager.RegisterHandler("AUDIO_INPUT", (chanId, payload) => ch1Received = payload.ToArray());
        manager.RegisterHandler("RAIL", (chanId, payload) => ch2Received = payload.ToArray());

        // Open channel 1 (id 1) and channel 2 (id 2)
        var req1 = new DvcCreateRequestPdu(1, "AUDIO_INPUT");
        var req2 = new DvcCreateRequestPdu(2, "RAIL");
        byte[] b1 = new byte[32]; byte[] b2 = new byte[32];
        var w1 = new RdpPacketWriter(b1); req1.Write(ref w1);
        var w2 = new RdpPacketWriter(b2); req2.Write(ref w2);

        manager.ProcessIncomingPacket(b1.AsSpan(0, w1.WrittenCount));
        manager.ProcessIncomingPacket(b2.AsSpan(0, w2.WrittenCount));

        // Create payloads (small enough to stay under 256 bytes per payload so TotalLength <= 255)
        byte[] ch1Payload = new byte[120];
        byte[] ch2Payload = new byte[120];
        for (int i = 0; i < 120; i++)
        {
            ch1Payload[i] = (byte)(0x30 + i);
            ch2Payload[i] = (byte)(0x70 + i);
        }

        var ch1Packets = new List<byte[]>();
        var ch2Packets = new List<byte[]>();

        // Small maxChunkSize (40) to force DataFirst + multiple Data chunks
        DynamicVirtualChannelManager.SendDvcData(1, ch1Payload, maxChunkSize: 40, p => ch1Packets.Add(p.ToArray()));
        DynamicVirtualChannelManager.SendDvcData(2, ch2Payload, maxChunkSize: 40, p => ch2Packets.Add(p.ToArray()));

        Assert.True(ch1Packets.Count > 1);
        Assert.True(ch2Packets.Count > 1);

        // Interleave packet processing
        int count = Math.Max(ch1Packets.Count, ch2Packets.Count);
        for (int i = 0; i < count; i++)
        {
            if (i < ch1Packets.Count) manager.ProcessIncomingPacket(ch1Packets[i]);
            if (i < ch2Packets.Count) manager.ProcessIncomingPacket(ch2Packets[i]);
        }

        Assert.NotNull(ch1Received);
        Assert.NotNull(ch2Received);
        Assert.Equal(ch1Payload, ch1Received);
        Assert.Equal(ch2Payload, ch2Received);
    }

    [Fact]
    public void DvcDataFirstHeader_RoundTrip_LargeTotalLength_EmpiricalChallenge()
    {
        // Direct test of DvcDataFirstHeader serialization & deserialization when TotalLength > 255 (requires lenSp=1)
        var firstHeader = new DvcDataFirstHeader(channelId: 10, totalLength: 500);

        byte[] buf = new byte[100];
        var writer = new RdpPacketWriter(buf);
        firstHeader.Write(ref writer);

        var reader = new RdpPacketReader(buf.AsSpan(0, writer.WrittenCount));
        bool success = DvcDataFirstHeader.TryRead(ref reader, out var parsedHeader);

        Assert.True(success);
        Assert.Equal(10u, parsedHeader.ChannelId);
        // BUG DISCOVERY: TryRead reads lenSp based on reader.UnreadLength (98), returning lenSp=0 (1 byte length),
        // causing TotalLength 500 to be misread as 244 (0xF4).
        Assert.Equal(500u, parsedHeader.TotalLength);
    }

    [Fact]
    public void DynamicManager_DvcDataFirst_LargePayloadTotalLength_EmpiricalChallenge()
    {
        // Challenge: Test SendDvcData with a payload size > 255 bytes (requiring 2-byte lenSp in DataFirst Header)
        // processed through DynamicVirtualChannelManager with chunk size = 100 bytes.
        var manager = new DynamicVirtualChannelManager();
        byte[]? receivedPayload = null;

        manager.RegisterHandler("LARGE_DVC", (_, payload) => receivedPayload = payload.ToArray());

        var req = new DvcCreateRequestPdu(10, "LARGE_DVC");
        byte[] reqBuf = new byte[32];
        var reqW = new RdpPacketWriter(reqBuf);
        req.Write(ref reqW);
        manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqW.WrittenCount));

        // 500 bytes payload -> TotalLength = 500 (> 255 bytes)
        byte[] payload = new byte[500];
        for (int i = 0; i < 500; i++) payload[i] = (byte)(i & 0xFF);

        var packets = new List<byte[]>();
        DynamicVirtualChannelManager.SendDvcData(10, payload, maxChunkSize: 100, p => packets.Add(p.ToArray()));

        bool allProcessed = true;
        foreach (var pkt in packets)
        {
            allProcessed &= manager.ProcessIncomingPacket(pkt);
        }

        Assert.True(allProcessed, "All incoming DVC packets should be processed without parser error.");
        Assert.NotNull(receivedPayload);
        Assert.Equal(payload, receivedPayload);
    }

    // =========================================================================
    // Area 4: Error handling for invalid/corrupted channel headers or unregistered channels
    // =========================================================================

    [Fact]
    public void StaticManager_TruncatedHeader_ReturnsFalse()
    {
        var manager = new StaticVirtualChannelManager();
        byte[] truncatedPacket = new byte[7]; // Less than 8 bytes HeaderLength

        bool result = manager.ProcessIncomingPacket(1005, truncatedPacket);

        Assert.False(result);
    }

    [Fact]
    public void StaticManager_UnregisteredChannel_HandledGracefully()
    {
        var manager = new StaticVirtualChannelManager();
        // Send single-chunk message for unregistered channel 9999
        byte[] payload = new byte[] { 1, 2, 3, 4 };
        byte[]? chunk = null;
        StaticVirtualChannelManager.ChunkMessage(payload, 50, c => chunk = c.ToArray());

        Assert.NotNull(chunk);
        bool result = manager.ProcessIncomingPacket(9999, chunk);

        // ProcessIncomingPacket returns true because packet header is valid, but no callback is invoked
        Assert.True(result);
    }

    [Fact]
    public void DynamicManager_EmptyPacket_ReturnsFalse()
    {
        var manager = new DynamicVirtualChannelManager();
        bool result = manager.ProcessIncomingPacket(Array.Empty<byte>());
        Assert.False(result);
    }

    [Theory]
    [InlineData((byte)0x00)] // Cmd = 0 (invalid)
    [InlineData((byte)0x06)] // Cmd = 6 (unrecognized)
    [InlineData((byte)0x0F)] // Cmd = 15 (unrecognized)
    public void DynamicManager_InvalidCommandCode_ReturnsFalse(byte invalidHeaderByte)
    {
        var manager = new DynamicVirtualChannelManager();
        byte[] packet = new byte[] { invalidHeaderByte, 0x01, 0x02 };

        bool result = manager.ProcessIncomingPacket(packet);

        Assert.False(result);
    }

    [Fact]
    public void DynamicManager_TruncatedCreateRequest_MissingNullTerminator_ReturnsFalse()
    {
        var manager = new DynamicVirtualChannelManager();

        // Build a CreateRequest header + channel ID, but string without null terminator
        byte[] invalidCreateReq = new byte[]
        {
            0x01, // Cmd = Create (0x01), Sp = 0, Pri = 0
            0x05, // ChannelId = 5
            (byte)'A', (byte)'U', (byte)'D', (byte)'I', (byte)'O' // No null terminator 0x00!
        };

        bool result = manager.ProcessIncomingPacket(invalidCreateReq);

        Assert.False(result);
    }

    [Fact]
    public void DynamicManager_TruncatedCreateResponse_MissingStatus_ReturnsFalse()
    {
        var manager = new DynamicVirtualChannelManager();

        // Build CreateResponse header + channel ID, but missing 4-byte creation status (only 2 bytes status)
        byte[] truncatedRsp = new byte[]
        {
            0x01, // Cmd = Create
            0x05, // ChannelId = 5
            0x00, 0x00 // Only 2 bytes status instead of 4
        };

        bool result = manager.ProcessIncomingPacket(truncatedRsp);

        // BUG DISCOVERY: ProcessIncomingPacket misidentifies truncated CreateResponse as CreateRequest and returns true!
        Assert.False(result);
    }

    [Fact]
    public void DynamicManager_UnregisteredChannelCreateRequest_RepliesWithUnsuccessfulStatus()
    {
        var manager = new DynamicVirtualChannelManager();
        byte[]? replyPacket = null;

        var createReq = new DvcCreateRequestPdu(channelId: 15, channelName: "UNREGISTERED_CHANNEL");
        byte[] reqBuf = new byte[32];
        var reqW = new RdpPacketWriter(reqBuf);
        createReq.Write(ref reqW);

        bool processed = manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqW.WrittenCount), reply =>
        {
            replyPacket = reply.ToArray();
        });

        Assert.True(processed);
        Assert.NotNull(replyPacket);

        var reader = new RdpPacketReader(replyPacket);
        Assert.True(DvcCreateResponsePdu.TryRead(ref reader, out var rsp));
        Assert.Equal(15u, rsp.ChannelId);
        Assert.False(rsp.IsSuccess);
        Assert.Equal(unchecked((int)0xC0000001), rsp.CreationStatus);
    }

    [Fact]
    public void DynamicManager_ClosePdu_RemovesChannelAndReassemblyState()
    {
        var manager = new DynamicVirtualChannelManager();
        byte[]? receivedPayload = null;

        manager.RegisterHandler("TEMP_DVC", (_, payload) => receivedPayload = payload.ToArray());

        // 1. Create channel 20
        var req = new DvcCreateRequestPdu(20, "TEMP_DVC");
        byte[] reqBuf = new byte[32];
        var reqW = new RdpPacketWriter(reqBuf);
        req.Write(ref reqW);
        manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqW.WrittenCount));

        // 2. Close channel 20
        var closePdu = new DvcClosePdu(20);
        byte[] closeBuf = new byte[16];
        var closeW = new RdpPacketWriter(closeBuf);
        closePdu.Write(ref closeW);
        bool closeResult = manager.ProcessIncomingPacket(closeBuf.AsSpan(0, closeW.WrittenCount));
        Assert.True(closeResult);

        // 3. Send data packet to closed channel 20
        var dataHeader = new DvcDataHeader(20);
        byte[] dataBuf = new byte[16];
        var dataW = new RdpPacketWriter(dataBuf);
        dataHeader.Write(ref dataW);
        dataW.WriteSpan(new byte[] { 0x01, 0x02, 0x03 });

        manager.ProcessIncomingPacket(dataBuf.AsSpan(0, dataW.WrittenCount));

        // Callback should NOT have been invoked after channel closed
        Assert.Null(receivedPayload);
    }
}
