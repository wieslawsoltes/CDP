using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Challenger;

using System;
using CDP.Rdp.Channels;
using CDP.Rdp.Protocol;
using Xunit;

public class ChallengerM126EmpiricalTests
{
    [AvaloniaTheory]
    [InlineData(16 * 1024 * 1024 + 1)]     // 16MB + 1 byte
    [InlineData(17 * 1024 * 1024)]         // 17MB
    [InlineData(50 * 1024 * 1024)]         // 50MB
    [InlineData(uint.MaxValue)]            // 4GB
    public void StaticManager_OversizedFirstHeaderFollowedByContinuationChunks_AllocatesZeroBeyond16MB_ReturnsFalse(uint oversizedLength)
    {
        var manager = new StaticVirtualChannelManager();
        int callbackCount = 0;
        ushort channelId = 1005;

        manager.RegisterChannel("test_static_oversized", channelId, (_, _) =>
        {
            callbackCount++;
        });

        // 1. Oversized First chunk (isFirst = true, isLast = false)
        byte[] firstPduBuf = new byte[64];
        var writer1 = new RdpPacketWriter(firstPduBuf);
        var firstHeader = new ChannelPduHeader(oversizedLength, ChannelPduFlags.First);
        firstHeader.Write(ref writer1);
        writer1.WriteSpan(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        ReadOnlySpan<byte> firstPacket = firstPduBuf.AsSpan(0, writer1.WrittenCount);

        long allocBeforeFirst = GC.GetAllocatedBytesForCurrentThread();
        bool firstResult = manager.ProcessIncomingPacket(channelId, firstPacket);
        long allocAfterFirst = GC.GetAllocatedBytesForCurrentThread();

        Assert.False(firstResult);
        Assert.True(allocAfterFirst - allocBeforeFirst < 100_000,
            $"Expected no buffer allocation for oversized First header, but allocated {allocAfterFirst - allocBeforeFirst} bytes");

        // 2. Subsequent continuation chunk 1 (!isFirst, !isLast)
        byte[] cont1PduBuf = new byte[64];
        var writer2 = new RdpPacketWriter(cont1PduBuf);
        var cont1Header = new ChannelPduHeader(oversizedLength, ChannelPduFlags.None);
        cont1Header.Write(ref writer2);
        writer2.WriteSpan(new byte[] { 0x05, 0x06, 0x07, 0x08 });

        ReadOnlySpan<byte> cont1Packet = cont1PduBuf.AsSpan(0, writer2.WrittenCount);

        long allocBeforeCont1 = GC.GetAllocatedBytesForCurrentThread();
        bool cont1Result = manager.ProcessIncomingPacket(channelId, cont1Packet);
        long allocAfterCont1 = GC.GetAllocatedBytesForCurrentThread();

        Assert.False(cont1Result);
        Assert.True(allocAfterCont1 - allocBeforeCont1 < 10_000,
            $"Expected zero array expansion for continuation chunk, but allocated {allocAfterCont1 - allocBeforeCont1} bytes");

        // 3. Subsequent continuation chunk 2 (!isFirst, isLast)
        byte[] cont2PduBuf = new byte[64];
        var writer3 = new RdpPacketWriter(cont2PduBuf);
        var cont2Header = new ChannelPduHeader(oversizedLength, ChannelPduFlags.Last);
        cont2Header.Write(ref writer3);
        writer3.WriteSpan(new byte[] { 0x09, 0x0A, 0x0B, 0x0C });

        ReadOnlySpan<byte> cont2Packet = cont2PduBuf.AsSpan(0, writer3.WrittenCount);

        bool cont2Result = manager.ProcessIncomingPacket(channelId, cont2Packet);

        Assert.False(cont2Result);
        Assert.Equal(0, callbackCount);
    }

    [AvaloniaTheory]
    [InlineData(16 * 1024 * 1024 + 1)]     // 16MB + 1 byte
    [InlineData(20 * 1024 * 1024)]         // 20MB
    [InlineData(100 * 1024 * 1024)]        // 100MB
    [InlineData(uint.MaxValue)]            // 4GB
    public void DynamicManager_OversizedDataFirstHeaderFollowedByContinuationChunks_AllocatesZeroBeyond16MB_ReturnsFalse(uint oversizedTotalLength)
    {
        var manager = new DynamicVirtualChannelManager();
        int callbackCount = 0;
        uint channelId = 42;

        manager.RegisterHandler("DVC_OVERSIZED_EMPIRICAL", (_, _) =>
        {
            callbackCount++;
        });

        // Initialize active channel via CreateRequest
        var createReq = new DvcCreateRequestPdu(channelId, "DVC_OVERSIZED_EMPIRICAL");
        byte[] reqBuf = new byte[64];
        var reqWriter = new RdpPacketWriter(reqBuf);
        createReq.Write(ref reqWriter);
        bool reqProcessed = manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqWriter.WrittenCount));
        Assert.True(reqProcessed);

        // 1. Oversized DataFirst chunk (totalLength > 16MB)
        byte[] firstBuf = new byte[64];
        var writer1 = new RdpPacketWriter(firstBuf);
        var firstHeader = new DvcDataFirstHeader(channelId, oversizedTotalLength);
        firstHeader.Write(ref writer1);
        writer1.WriteSpan(new byte[] { 0x11, 0x22, 0x33, 0x44 });

        ReadOnlySpan<byte> firstPacket = firstBuf.AsSpan(0, writer1.WrittenCount);

        long allocBeforeFirst = GC.GetAllocatedBytesForCurrentThread();
        bool firstResult = manager.ProcessIncomingPacket(firstPacket);
        long allocAfterFirst = GC.GetAllocatedBytesForCurrentThread();

        Assert.False(firstResult);
        Assert.True(allocAfterFirst - allocBeforeFirst < 100_000,
            $"Expected no buffer allocation for oversized DataFirst header, but allocated {allocAfterFirst - allocBeforeFirst} bytes");

        // 2. Subsequent Data continuation chunk 1
        byte[] cont1Buf = new byte[64];
        var writer2 = new RdpPacketWriter(cont1Buf);
        var cont1Header = new DvcDataHeader(channelId);
        cont1Header.Write(ref writer2);
        writer2.WriteSpan(new byte[] { 0x55, 0x66, 0x77, 0x88 });

        ReadOnlySpan<byte> cont1Packet = cont1Buf.AsSpan(0, writer2.WrittenCount);

        long allocBeforeCont1 = GC.GetAllocatedBytesForCurrentThread();
        bool cont1Result = manager.ProcessIncomingPacket(cont1Packet);
        long allocAfterCont1 = GC.GetAllocatedBytesForCurrentThread();

        Assert.False(cont1Result);
        Assert.True(allocAfterCont1 - allocBeforeCont1 < 10_000,
            $"Expected zero array expansion for continuation Data chunk, but allocated {allocAfterCont1 - allocBeforeCont1} bytes");

        // 3. Subsequent Data continuation chunk 2
        byte[] cont2Buf = new byte[64];
        var writer3 = new RdpPacketWriter(cont2Buf);
        var cont2Header = new DvcDataHeader(channelId);
        cont2Header.Write(ref writer3);
        writer3.WriteSpan(new byte[] { 0x99, 0xAA, 0xBB, 0xCC });

        ReadOnlySpan<byte> cont2Packet = cont2Buf.AsSpan(0, writer3.WrittenCount);

        bool cont2Result = manager.ProcessIncomingPacket(cont2Packet);

        Assert.False(cont2Result);
        Assert.Equal(0, callbackCount);
    }

    [AvaloniaFact]
    public void StaticManager_OutOfOrderContinuationChunk_UninitializedChannel_ReturnsFalseWithoutCallback()
    {
        var manager = new StaticVirtualChannelManager();
        int callbackCount = 0;
        ushort channelId = 2002;

        manager.RegisterChannel("uninit_channel", channelId, (_, _) =>
        {
            callbackCount++;
        });

        // Send continuation chunk (isFirst = false, isLast = false) without prior First PDU
        byte[] contBuf = new byte[32];
        var writer1 = new RdpPacketWriter(contBuf);
        var contHeader = new ChannelPduHeader(length: 500, ChannelPduFlags.None);
        contHeader.Write(ref writer1);
        writer1.WriteSpan(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        bool contResult = manager.ProcessIncomingPacket(channelId, contBuf.AsSpan(0, writer1.WrittenCount));
        Assert.False(contResult);
        Assert.Equal(0, callbackCount);

        // Send continuation chunk with Last flag (isFirst = false, isLast = true) without prior First PDU
        byte[] lastBuf = new byte[32];
        var writer2 = new RdpPacketWriter(lastBuf);
        var lastHeader = new ChannelPduHeader(length: 500, ChannelPduFlags.Last);
        lastHeader.Write(ref writer2);
        writer2.WriteSpan(new byte[] { 0x05, 0x06, 0x07, 0x08 });

        bool lastResult = manager.ProcessIncomingPacket(channelId, lastBuf.AsSpan(0, writer2.WrittenCount));
        Assert.False(lastResult);
        Assert.Equal(0, callbackCount);
    }

    [AvaloniaFact]
    public void StaticManager_OutOfOrderContinuationChunk_UnknownChannel_ReturnsFalse()
    {
        var manager = new StaticVirtualChannelManager();
        ushort unknownChannelId = 9999;

        // Send continuation chunk to unregistered channel
        byte[] contBuf = new byte[32];
        var writer = new RdpPacketWriter(contBuf);
        var contHeader = new ChannelPduHeader(length: 100, ChannelPduFlags.None);
        contHeader.Write(ref writer);
        writer.WriteSpan(new byte[] { 0xAA, 0xBB });

        bool result = manager.ProcessIncomingPacket(unknownChannelId, contBuf.AsSpan(0, writer.WrittenCount));
        Assert.False(result);
    }

    [AvaloniaFact]
    public void DynamicManager_ContinuationDataChunk_UninitializedChannel_ReturnsFalseWithoutCallback()
    {
        var manager = new DynamicVirtualChannelManager();
        int callbackCount = 0;
        uint uninitializedChannelId = 8888;

        manager.RegisterHandler("UNINIT_DVC", (_, _) =>
        {
            callbackCount++;
        });

        // Send Data PDU to channel that was never created via DvcCreateRequestPdu
        byte[] dataBuf = new byte[32];
        var writer = new RdpPacketWriter(dataBuf);
        var dataHeader = new DvcDataHeader(uninitializedChannelId);
        dataHeader.Write(ref writer);
        writer.WriteSpan(new byte[] { 0x11, 0x22, 0x33, 0x44 });

        bool result = manager.ProcessIncomingPacket(dataBuf.AsSpan(0, writer.WrittenCount));

        Assert.False(result);
        Assert.Equal(0, callbackCount);
    }

    [AvaloniaFact]
    public void DynamicManager_ContinuationDataChunk_ClosedChannel_ReturnsFalseWithoutCallback()
    {
        var manager = new DynamicVirtualChannelManager();
        int callbackCount = 0;
        uint channelId = 55;

        manager.RegisterHandler("DVC_CLOSED_EMPIRICAL", (_, _) =>
        {
            callbackCount++;
        });

        // 1. Create channel
        var createReq = new DvcCreateRequestPdu(channelId, "DVC_CLOSED_EMPIRICAL");
        byte[] reqBuf = new byte[64];
        var reqWriter = new RdpPacketWriter(reqBuf);
        createReq.Write(ref reqWriter);
        Assert.True(manager.ProcessIncomingPacket(reqBuf.AsSpan(0, reqWriter.WrittenCount)));

        // 2. Close channel
        var closePdu = new DvcClosePdu(channelId);
        byte[] closeBuf = new byte[32];
        var closeWriter = new RdpPacketWriter(closeBuf);
        closePdu.Write(ref closeWriter);
        Assert.True(manager.ProcessIncomingPacket(closeBuf.AsSpan(0, closeWriter.WrittenCount)));

        // 3. Send Data PDU to closed channel
        byte[] dataBuf = new byte[32];
        var dataWriter = new RdpPacketWriter(dataBuf);
        var dataHeader = new DvcDataHeader(channelId);
        dataHeader.Write(ref dataWriter);
        dataWriter.WriteSpan(new byte[] { 0x55, 0x66 });

        bool result = manager.ProcessIncomingPacket(dataBuf.AsSpan(0, dataWriter.WrittenCount));

        Assert.False(result);
        Assert.Equal(0, callbackCount);
    }
}
