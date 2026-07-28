using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Channels;

using System;
using System.Collections.Generic;
using CDP.Rdp.Channels;
using Xunit;

public class VirtualChannelManagerTests
{
    [AvaloniaFact]
    public void StaticManager_RegisterAndLookup_Success()
    {
        var manager = new StaticVirtualChannelManager();
        manager.RegisterChannel("cliprdr", 1005);
        manager.RegisterChannel("rdpsnd", 1006);

        Assert.True(manager.TryGetChannelId("cliprdr", out ushort id1));
        Assert.Equal(1005, id1);

        Assert.True(manager.TryGetChannelName(1006, out string? name2));
        Assert.Equal("rdpsnd", name2);
    }

    [AvaloniaFact]
    public void StaticManager_SingleChunkMessage_ReassemblesAndTriggersCallback()
    {
        var manager = new StaticVirtualChannelManager();
        byte[] receivedData = null!;
        ushort receivedChannel = 0;

        manager.RegisterChannel("cliprdr", 1005, (chan, payload) =>
        {
            receivedChannel = chan;
            receivedData = payload.ToArray();
        });

        byte[] originalPayload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        byte[] chunkPdu = null!;

        StaticVirtualChannelManager.ChunkMessage(originalPayload, maxChunkSize: 100, chunk =>
        {
            chunkPdu = chunk.ToArray();
        });

        bool processed = manager.ProcessIncomingPacket(1005, chunkPdu);

        Assert.True(processed);
        Assert.Equal(1005, receivedChannel);
        Assert.Equal(originalPayload, receivedData);
    }

    [AvaloniaFact]
    public void StaticManager_MultiChunkMessage_ReassemblesAndTriggersCallback()
    {
        var manager = new StaticVirtualChannelManager();
        byte[] receivedData = null!;

        manager.RegisterChannel("drdynvc", 1007, (_, payload) =>
        {
            receivedData = payload.ToArray();
        });

        // 100 bytes payload chunked with max chunk size 30 (header 8B, payload 22B per chunk) -> 5 chunks
        byte[] largePayload = new byte[100];
        for (int i = 0; i < largePayload.Length; i++) largePayload[i] = (byte)(i & 0xFF);

        var chunks = new List<byte[]>();
        StaticVirtualChannelManager.ChunkMessage(largePayload, maxChunkSize: 30, chunk =>
        {
            chunks.Add(chunk.ToArray());
        });

        Assert.True(chunks.Count > 1);

        foreach (var chunk in chunks)
        {
            manager.ProcessIncomingPacket(1007, chunk);
        }

        Assert.NotNull(receivedData);
        Assert.Equal(largePayload, receivedData);
    }

    [AvaloniaFact]
    public void DynamicManager_RegisterAndHandleCreateRequest()
    {
        var manager = new DynamicVirtualChannelManager();
        byte[] receivedPayload = null!;

        manager.RegisterHandler("AUDIO_INPUT", (chanId, payload) =>
        {
            receivedPayload = payload.ToArray();
        });

        byte[] replyPacket = null!;
        var createReq = new DvcCreateRequestPdu(channelId: 10, channelName: "AUDIO_INPUT");
        byte[] reqBuf = new byte[32];
        var writer = new CDP.Rdp.Protocol.RdpPacketWriter(reqBuf);
        createReq.Write(ref writer);

        bool processed = manager.ProcessIncomingPacket(reqBuf.AsSpan(0, writer.WrittenCount), reply =>
        {
            replyPacket = reply.ToArray();
        });

        Assert.True(processed);
        Assert.NotNull(replyPacket);

        // Verify reply is a successful CreateResponse PDU
        var reader = new CDP.Rdp.Protocol.RdpPacketReader(replyPacket);
        bool readRsp = DvcCreateResponsePdu.TryRead(ref reader, out var rsp);
        Assert.True(readRsp);
        Assert.Equal(10u, rsp.ChannelId);
        Assert.True(rsp.IsSuccess);
    }

    [AvaloniaFact]
    public void DynamicManager_MultiChunkDvcData_ReassemblesAndTriggersCallback()
    {
        var manager = new DynamicVirtualChannelManager();
        byte[] receivedPayload = null!;

        manager.RegisterHandler("TEST_DVC", (chanId, payload) =>
        {
            receivedPayload = payload.ToArray();
        });

        // Simulate channel creation first
        var createReq = new DvcCreateRequestPdu(channelId: 5, channelName: "TEST_DVC");
        byte[] reqBuf = new byte[32];
        var reqWriter = new CDP.Rdp.Protocol.RdpPacketWriter(reqBuf);
        createReq.Write(ref reqWriter);
        manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqWriter.WrittenCount));

        // Create large payload (200 bytes) and chunk it via SendDvcData with maxChunkSize 50
        byte[] originalPayload = new byte[200];
        for (int i = 0; i < originalPayload.Length; i++) originalPayload[i] = (byte)(i + 1);

        var dvcPackets = new List<byte[]>();
        DynamicVirtualChannelManager.SendDvcData(5, originalPayload, maxChunkSize: 50, packet =>
        {
            dvcPackets.Add(packet.ToArray());
        });

        Assert.True(dvcPackets.Count > 1);

        foreach (var pkt in dvcPackets)
        {
            manager.ProcessIncomingPacket(pkt);
        }

        Assert.NotNull(receivedPayload);
        Assert.Equal(originalPayload, receivedPayload);
    }

    [AvaloniaTheory]
    [InlineData(16 * 1024 * 1024 + 1)] // 16MB + 1 byte
    [InlineData(20 * 1024 * 1024)]     // 20MB
    [InlineData(100 * 1024 * 1024)]    // 100MB
    [InlineData(uint.MaxValue)]        // 4GB
    public void StaticManager_MaxMessageSizeExceeded_ReturnsFalseWithoutAllocatingLargeArray(uint invalidLength)
    {
        var manager = new StaticVirtualChannelManager();
        manager.RegisterChannel("test_channel", 1005, (_, _) => { });

        byte[] packetBuf = new byte[32];
        var writer = new CDP.Rdp.Protocol.RdpPacketWriter(packetBuf);
        var header = new ChannelPduHeader(invalidLength, ChannelPduFlags.First);
        header.Write(ref writer);
        writer.WriteSpan(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        ReadOnlySpan<byte> packetData = packetBuf.AsSpan(0, writer.WrittenCount);

        long beforeAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

        bool result = manager.ProcessIncomingPacket(1005, packetData);

        long afterAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        long allocatedDelta = afterAllocatedBytes - beforeAllocatedBytes;

        Assert.False(result);
        Assert.True(allocatedDelta < 100_000, $"Expected minimal allocation, but allocated {allocatedDelta} bytes");
    }

    [AvaloniaTheory]
    [InlineData(16 * 1024 * 1024 + 1)] // 16MB + 1 byte
    [InlineData(30 * 1024 * 1024)]     // 30MB
    [InlineData(1_000_000_000)]        // 1GB
    [InlineData(uint.MaxValue)]        // 4GB
    public void DynamicManager_MaxMessageSizeExceeded_ReturnsFalseWithoutAllocatingLargeArray(uint invalidTotalLength)
    {
        var manager = new DynamicVirtualChannelManager();
        manager.RegisterHandler("TEST_DVC", (_, _) => { });

        var createReq = new DvcCreateRequestPdu(channelId: 5, channelName: "TEST_DVC");
        byte[] reqBuf = new byte[32];
        var reqWriter = new CDP.Rdp.Protocol.RdpPacketWriter(reqBuf);
        createReq.Write(ref reqWriter);
        manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqWriter.WrittenCount));

        byte[] packetBuf = new byte[64];
        var writer = new CDP.Rdp.Protocol.RdpPacketWriter(packetBuf);
        var firstHeader = new DvcDataFirstHeader(channelId: 5, totalLength: invalidTotalLength);
        firstHeader.Write(ref writer);
        writer.WriteSpan(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

        ReadOnlySpan<byte> packetData = packetBuf.AsSpan(0, writer.WrittenCount);

        long beforeAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

        bool result = manager.ProcessIncomingPacket(packetData);

        long afterAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        long allocatedDelta = afterAllocatedBytes - beforeAllocatedBytes;

        Assert.False(result);
        Assert.True(allocatedDelta < 100_000, $"Expected minimal allocation, but allocated {allocatedDelta} bytes");
    }

    [AvaloniaFact]
    public void StaticManager_MultiChannelInterleaved_ReassemblesAllChannelsCorrectly()
    {
        var manager = new StaticVirtualChannelManager();
        int channelCount = 5;
        int payloadSize = 5000;
        int maxChunkSize = 120;

        var receivedMap = new Dictionary<ushort, byte[]>();
        var originalPayloads = new Dictionary<ushort, byte[]>();
        var chunkQueues = new Dictionary<ushort, Queue<byte[]>>();

        for (ushort chId = 1001; chId < 1001 + channelCount; chId++)
        {
            ushort currentId = chId;
            manager.RegisterChannel($"chan_{currentId}", currentId, (id, payload) =>
            {
                receivedMap[id] = payload.ToArray();
            });

            byte[] payload = new byte[payloadSize];
            for (int b = 0; b < payloadSize; b++)
            {
                payload[b] = (byte)((currentId + b) & 0xFF);
            }
            originalPayloads[currentId] = payload;

            var queue = new Queue<byte[]>();
            StaticVirtualChannelManager.ChunkMessage(payload, maxChunkSize, chunk =>
            {
                queue.Enqueue(chunk.ToArray());
            });
            chunkQueues[currentId] = queue;
        }

        bool itemsRemaining = true;
        while (itemsRemaining)
        {
            itemsRemaining = false;
            for (ushort chId = 1001; chId < 1001 + channelCount; chId++)
            {
                if (chunkQueues[chId].Count > 0)
                {
                    byte[] chunk = chunkQueues[chId].Dequeue();
                    bool ok = manager.ProcessIncomingPacket(chId, chunk);
                    Assert.True(ok);
                    itemsRemaining = true;
                }
            }
        }

        Assert.Equal(channelCount, receivedMap.Count);
        for (ushort chId = 1001; chId < 1001 + channelCount; chId++)
        {
            Assert.True(receivedMap.ContainsKey(chId));
            Assert.Equal(originalPayloads[chId], receivedMap[chId]);
        }
    }

    [AvaloniaFact]
    public void DynamicManager_MultiChannelInterleaved_ReassemblesAllChannelsCorrectly()
    {
        var manager = new DynamicVirtualChannelManager();
        int channelCount = 5;
        int payloadSize = 4000;
        int maxChunkSize = 90;

        var receivedMap = new Dictionary<uint, byte[]>();
        var originalPayloads = new Dictionary<uint, byte[]>();
        var packetQueues = new Dictionary<uint, Queue<byte[]>>();

        for (uint chId = 1; chId <= (uint)channelCount; chId++)
        {
            uint currentId = chId;
            string channelName = $"DVC_CHAN_{currentId}";
            manager.RegisterHandler(channelName, (id, payload) =>
            {
                receivedMap[id] = payload.ToArray();
            });

            var createReq = new DvcCreateRequestPdu(currentId, channelName);
            byte[] reqBuf = new byte[64];
            var reqWriter = new CDP.Rdp.Protocol.RdpPacketWriter(reqBuf);
            createReq.Write(ref reqWriter);
            bool reqProcessed = manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqWriter.WrittenCount));
            Assert.True(reqProcessed);

            byte[] payload = new byte[payloadSize];
            for (int b = 0; b < payloadSize; b++)
            {
                payload[b] = (byte)((currentId * 7 + b) & 0xFF);
            }
            originalPayloads[currentId] = payload;

            var queue = new Queue<byte[]>();
            DynamicVirtualChannelManager.SendDvcData(currentId, payload, maxChunkSize, packet =>
            {
                queue.Enqueue(packet.ToArray());
            });
            packetQueues[currentId] = queue;
        }

        bool itemsRemaining = true;
        while (itemsRemaining)
        {
            itemsRemaining = false;
            for (uint chId = 1; chId <= (uint)channelCount; chId++)
            {
                if (packetQueues[chId].Count > 0)
                {
                    byte[] packet = packetQueues[chId].Dequeue();
                    bool ok = manager.ProcessIncomingPacket(packet);
                    Assert.True(ok);
                    itemsRemaining = true;
                }
            }
        }

        Assert.Equal(channelCount, receivedMap.Count);
        for (uint chId = 1; chId <= (uint)channelCount; chId++)
        {
            Assert.True(receivedMap.ContainsKey(chId));
            Assert.Equal(originalPayloads[chId], receivedMap[chId]);
        }
    }

    [AvaloniaFact]
    public async System.Threading.Tasks.Task StaticManager_ConcurrentChannelsMultiThreaded_ReassemblesWithoutDataCorruption()
    {
        var manager = new StaticVirtualChannelManager();
        int channelCount = 10;
        int messagesPerChannel = 20;
        int payloadSize = 2000;
        int maxChunkSize = 100;

        var lockObj = new object();
        var receivedCount = new Dictionary<ushort, int>();

        for (ushort chId = 2001; chId < 2001 + channelCount; chId++)
        {
            ushort currentId = chId;
            receivedCount[currentId] = 0;
            manager.RegisterChannel($"chan_{currentId}", currentId, (id, payload) =>
            {
                lock (lockObj)
                {
                    receivedCount[id]++;
                }
            });
        }

        var tasks = new System.Threading.Tasks.Task[channelCount];
        for (int i = 0; i < channelCount; i++)
        {
            ushort chId = (ushort)(2001 + i);
            tasks[i] = System.Threading.Tasks.Task.Run(() =>
            {
                for (int m = 0; m < messagesPerChannel; m++)
                {
                    byte[] payload = new byte[payloadSize];
                    Array.Fill(payload, (byte)(m & 0xFF));

                    var chunks = new List<byte[]>();
                    StaticVirtualChannelManager.ChunkMessage(payload, maxChunkSize, chunk =>
                    {
                        chunks.Add(chunk.ToArray());
                    });

                    foreach (var chunk in chunks)
                    {
                        lock (lockObj)
                        {
                            manager.ProcessIncomingPacket(chId, chunk);
                        }
                    }
                }
            });
        }

        await System.Threading.Tasks.Task.WhenAll(tasks);

        for (ushort chId = 2001; chId < 2001 + channelCount; chId++)
        {
            Assert.Equal(messagesPerChannel, receivedCount[chId]);
        }
    }

    [AvaloniaFact]
    public void StaticManager_ExactMaxMessageSizeBoundary_BehaviorVerified()
    {
        var manager = new StaticVirtualChannelManager();
        manager.RegisterChannel("boundary_test", 3001, (_, _) => { });

        byte[] packetBuf = new byte[32];
        var writer = new CDP.Rdp.Protocol.RdpPacketWriter(packetBuf);

        // 16MB exact: should return true from Reset
        var validHeader = new ChannelPduHeader(StaticVirtualChannelManager.MaxMessageSize, ChannelPduFlags.First);
        validHeader.Write(ref writer);
        writer.WriteSpan(new byte[] { 0x01, 0x02 });

        bool validResult = manager.ProcessIncomingPacket(3001, packetBuf.AsSpan(0, writer.WrittenCount));
        Assert.True(validResult);

        // 16MB + 1 byte: should return false from Reset
        writer = new CDP.Rdp.Protocol.RdpPacketWriter(packetBuf);
        var invalidHeader = new ChannelPduHeader(StaticVirtualChannelManager.MaxMessageSize + 1, ChannelPduFlags.First);
        invalidHeader.Write(ref writer);
        writer.WriteSpan(new byte[] { 0x01, 0x02 });

        bool invalidResult = manager.ProcessIncomingPacket(3001, packetBuf.AsSpan(0, writer.WrittenCount));
        Assert.False(invalidResult);
    }

    [AvaloniaFact]
    public void DynamicManager_ExactMaxMessageSizeBoundary_BehaviorVerified()
    {
        var manager = new DynamicVirtualChannelManager();
        manager.RegisterHandler("DVC_BOUND", (_, _) => { });

        var createReq = new DvcCreateRequestPdu(channelId: 12, channelName: "DVC_BOUND");
        byte[] reqBuf = new byte[32];
        var reqWriter = new CDP.Rdp.Protocol.RdpPacketWriter(reqBuf);
        createReq.Write(ref reqWriter);
        manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqWriter.WrittenCount));

        byte[] packetBuf = new byte[64];

        // 16MB exact: should return true from Reset
        var validWriter = new CDP.Rdp.Protocol.RdpPacketWriter(packetBuf);
        var validFirst = new DvcDataFirstHeader(channelId: 12, totalLength: DynamicVirtualChannelManager.MaxMessageSize);
        validFirst.Write(ref validWriter);
        validWriter.WriteSpan(new byte[] { 0x01, 0x02 });

        bool validResult = manager.ProcessIncomingPacket(packetBuf.AsSpan(0, validWriter.WrittenCount));
        Assert.True(validResult);

        // 16MB + 1 byte: should return false from Reset
        var invalidWriter = new CDP.Rdp.Protocol.RdpPacketWriter(packetBuf);
        var invalidFirst = new DvcDataFirstHeader(channelId: 12, totalLength: DynamicVirtualChannelManager.MaxMessageSize + 1);
        invalidFirst.Write(ref invalidWriter);
        invalidWriter.WriteSpan(new byte[] { 0x01, 0x02 });

        bool invalidResult = manager.ProcessIncomingPacket(packetBuf.AsSpan(0, invalidWriter.WrittenCount));
        Assert.False(invalidResult);
    }

    [AvaloniaTheory]
    [InlineData("DVC")]
    [InlineData("FOO")]
    [InlineData("CTX")]
    [InlineData("SND")]
    public void DynamicManager_ThreeCharacterChannelName_CreateRequestHandledSuccessfully(string channelName)
    {
        var manager = new DynamicVirtualChannelManager();
        uint receivedChanId = 0;
        byte[] receivedPayload = null!;

        manager.RegisterHandler(channelName, (chanId, payload) =>
        {
            receivedChanId = chanId;
            receivedPayload = payload.ToArray();
        });

        byte[] replyPacket = null!;
        var createReq = new DvcCreateRequestPdu(channelId: 42, channelName: channelName);
        byte[] reqBuf = new byte[32];
        var writer = new CDP.Rdp.Protocol.RdpPacketWriter(reqBuf);
        createReq.Write(ref writer);

        bool processed = manager.ProcessIncomingPacket(reqBuf.AsSpan(0, writer.WrittenCount), reply =>
        {
            replyPacket = reply.ToArray();
        });

        Assert.True(processed);
        Assert.NotNull(replyPacket);

        // Verify reply is a successful CreateResponse PDU for channel 42
        var reader = new CDP.Rdp.Protocol.RdpPacketReader(replyPacket);
        bool readRsp = DvcCreateResponsePdu.TryRead(ref reader, out var rsp);
        Assert.True(readRsp);
        Assert.Equal(42u, rsp.ChannelId);
        Assert.True(rsp.IsSuccess);

        // Verify channel is active by sending data to it
        byte[] testData = new byte[] { 0x10, 0x20, 0x30 };
        byte[] dataPktBuf = new byte[32];
        var dataWriter = new CDP.Rdp.Protocol.RdpPacketWriter(dataPktBuf);
        var dataHeader = new DvcDataHeader(channelId: 42);
        dataHeader.Write(ref dataWriter);
        dataWriter.WriteSpan(testData);

        bool dataProcessed = manager.ProcessIncomingPacket(dataPktBuf.AsSpan(0, dataWriter.WrittenCount));
        Assert.True(dataProcessed);
        Assert.Equal(42u, receivedChanId);
        Assert.Equal(testData, receivedPayload);
    }

    [AvaloniaFact]
    public void StaticManager_OversizedFirstHeader_RejectsHeaderAndSubsequentContinuationChunksWithoutArrayExpansion()
    {
        var manager = new StaticVirtualChannelManager();
        bool callbackInvoked = false;
        manager.RegisterChannel("oversized_svc", 2001, (_, _) => { callbackInvoked = true; });

        // 1. Send first PDU with oversized length header (17MB)
        uint oversizedLength = StaticVirtualChannelManager.MaxMessageSize + 1_000_000;
        byte[] firstPduBuf = new byte[32];
        var writer1 = new CDP.Rdp.Protocol.RdpPacketWriter(firstPduBuf);
        var firstHeader = new ChannelPduHeader(oversizedLength, ChannelPduFlags.First);
        firstHeader.Write(ref writer1);
        writer1.WriteSpan(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        bool firstResult = manager.ProcessIncomingPacket(2001, firstPduBuf.AsSpan(0, writer1.WrittenCount));
        Assert.False(firstResult);

        // 2. Send subsequent continuation chunks (isFirst = false, isLast = false)
        byte[] contPduBuf = new byte[32];
        var writer2 = new CDP.Rdp.Protocol.RdpPacketWriter(contPduBuf);
        var contHeader = new ChannelPduHeader(oversizedLength, ChannelPduFlags.None);
        contHeader.Write(ref writer2);
        writer2.WriteSpan(new byte[] { 0x05, 0x06, 0x07, 0x08 });

        bool contResult = manager.ProcessIncomingPacket(2001, contPduBuf.AsSpan(0, writer2.WrittenCount));
        Assert.False(contResult);

        // 3. Send final continuation chunk (isFirst = false, isLast = true)
        byte[] finalPduBuf = new byte[32];
        var writer3 = new CDP.Rdp.Protocol.RdpPacketWriter(finalPduBuf);
        var finalHeader = new ChannelPduHeader(oversizedLength, ChannelPduFlags.Last);
        finalHeader.Write(ref writer3);
        writer3.WriteSpan(new byte[] { 0x09, 0x0A, 0x0B, 0x0C });

        bool finalResult = manager.ProcessIncomingPacket(2001, finalPduBuf.AsSpan(0, writer3.WrittenCount));
        Assert.False(finalResult);
        Assert.False(callbackInvoked);
    }

    [AvaloniaFact]
    public void DynamicManager_OversizedDataFirstHeader_RejectsFirstAndSubsequentDataChunksWithoutCallbackOrAllocation()
    {
        var manager = new DynamicVirtualChannelManager();
        bool callbackInvoked = false;
        manager.RegisterHandler("OVERSIZED_DVC", (_, _) => { callbackInvoked = true; });

        // Create channel ID 7
        var createReq = new DvcCreateRequestPdu(channelId: 7, channelName: "OVERSIZED_DVC");
        byte[] reqBuf = new byte[32];
        var reqWriter = new CDP.Rdp.Protocol.RdpPacketWriter(reqBuf);
        createReq.Write(ref reqWriter);
        manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqWriter.WrittenCount));

        // Send DataFirst PDU with totalLength > MaxMessageSize (20MB)
        uint oversizedTotalLength = DynamicVirtualChannelManager.MaxMessageSize + 4_000_000;
        byte[] firstBuf = new byte[64];
        var writer1 = new CDP.Rdp.Protocol.RdpPacketWriter(firstBuf);
        var firstHeader = new DvcDataFirstHeader(channelId: 7, totalLength: oversizedTotalLength);
        firstHeader.Write(ref writer1);
        writer1.WriteSpan(new byte[] { 0x11, 0x22, 0x33, 0x44 });

        bool firstResult = manager.ProcessIncomingPacket(firstBuf.AsSpan(0, writer1.WrittenCount));
        Assert.False(firstResult);

        // Send subsequent Data continuation chunk (Cmd = Data)
        byte[] dataBuf = new byte[64];
        var writer2 = new CDP.Rdp.Protocol.RdpPacketWriter(dataBuf);
        var dataHeader = new DvcDataHeader(channelId: 7);
        dataHeader.Write(ref writer2);
        writer2.WriteSpan(new byte[] { 0x55, 0x66, 0x77, 0x88 });

        bool dataResult = manager.ProcessIncomingPacket(dataBuf.AsSpan(0, writer2.WrittenCount));
        Assert.False(dataResult);
        Assert.False(callbackInvoked);
    }

    [AvaloniaFact]
    public void StaticManager_OutOfOrderContinuationChunk_DroppedWithoutCallback()
    {
        var manager = new StaticVirtualChannelManager();
        bool callbackInvoked = false;
        manager.RegisterChannel("test_channel", 1005, (_, _) => { callbackInvoked = true; });

        // Send continuation chunk without any prior First chunk
        byte[] packetBuf = new byte[32];
        var writer = new CDP.Rdp.Protocol.RdpPacketWriter(packetBuf);
        var header = new ChannelPduHeader(length: 100, ChannelPduFlags.Last); // continuation chunk with Last flag
        header.Write(ref writer);
        writer.WriteSpan(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        bool result = manager.ProcessIncomingPacket(1005, packetBuf.AsSpan(0, writer.WrittenCount));

        Assert.False(result);
        Assert.False(callbackInvoked);
    }
}



