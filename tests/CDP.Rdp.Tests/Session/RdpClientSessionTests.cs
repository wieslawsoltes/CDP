using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Session;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Channels;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Session;
using CDP.Rdp.Tests.Fixtures;
using Xunit;

[Xunit.Collection("RdpTests")]
public class RdpClientSessionTests
{
    [AvaloniaFact]
    public async Task ConnectAsync_TransitionsStateCorrectly()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        List<RdpConnectionState> stateChanges = new List<RdpConnectionState>();

        RdpSessionOptions options = new RdpSessionOptions
        {
            Host = "127.0.0.1",
            Port = 3389
        };

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        client.StateChanged += (_, e) => stateChanges.Add(e.NewState);

        Assert.Equal(RdpConnectionState.Disconnected, client.State);

        await client.ConnectAsync();

        Assert.Equal(RdpConnectionState.Connected, client.State);
        Assert.Equal(
            new[]
            {
                RdpConnectionState.Connecting,
                RdpConnectionState.Negotiating,
                RdpConnectionState.Authenticating,
                RdpConnectionState.Connected
            },
            stateChanges);
    }

    [AvaloniaFact]
    public async Task DisconnectAsync_TransitionsToDisconnected()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();
        Assert.Equal(RdpConnectionState.Connected, client.State);

        await client.DisconnectAsync();

        Assert.Equal(RdpConnectionState.Disconnected, client.State);
    }

    [AvaloniaFact]
    public async Task ProcessingLoop_FiresFrameUpdatedEvent()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        TaskCompletionSource<RdpFrameUpdateEventArgs> frameTcs = new TaskCompletionSource<RdpFrameUpdateEventArgs>();
        client.FrameUpdated += (_, args) => frameTcs.TrySetResult(args);

        await client.ConnectAsync();

        byte[] framePdu = BuildFastPathBitmapPdu(10, 20, 1, 1, 32, new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

        await streams.ServerStream.WriteAsync(framePdu);
        await streams.ServerStream.FlushAsync();

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        RdpFrameUpdateEventArgs receivedArgs = await frameTcs.Task.WaitAsync(cts.Token);

        Assert.NotNull(receivedArgs);
        Assert.True(receivedArgs.FrameId > 0);
        Assert.Single(receivedArgs.BitmapUpdates);

        RdpBitmapUpdate rect = receivedArgs.BitmapUpdates[0];
        Assert.Equal(10, rect.Left);
        Assert.Equal(20, rect.Top);
        Assert.Equal(1, rect.Width);
        Assert.Equal(1, rect.Height);
        Assert.Equal(32, rect.Bpp);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, rect.Data.ToArray());
    }

    [AvaloniaFact]
    public async Task ProcessingLoop_TpktSlowPathBitmap_FiresFrameUpdatedEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair streams = new DuplexStreamPair();
        using RdpClient client = new RdpClient(
            new RdpSessionOptions(),
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(
                new PlainRdpSecurityTransport(streams.ClientStream)));
        var frameTcs = new TaskCompletionSource<RdpFrameUpdateEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.FrameUpdated += (_, args) => frameTcs.TrySetResult(args);
        await client.ConnectAsync(ct);

        byte[] packet = BuildSlowPathBitmapPdu(
            1003,
            7,
            9,
            1,
            1,
            32,
            [0x11, 0x22, 0x33, 0x44]);
        await streams.ServerStream.WriteAsync(packet, ct);
        await streams.ServerStream.FlushAsync(ct);

        RdpFrameUpdateEventArgs received = await frameTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        RdpBitmapUpdate update = Assert.Single(received.BitmapUpdates);
        Assert.Equal(7, update.Left);
        Assert.Equal(9, update.Top);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, update.Data.ToArray());
    }

    [AvaloniaFact]
    public async Task ProcessingLoop_TpktStaticChannel_DispatchesReassembledPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair streams = new DuplexStreamPair();
        using RdpClient client = new RdpClient(
            new RdpSessionOptions(),
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(
                new PlainRdpSecurityTransport(streams.ClientStream)));
        await client.ConnectAsync(ct);

        var payloadTcs = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.NotNull(client.StaticVirtualChannels);
        client.StaticVirtualChannels.RegisterChannel(
            "test",
            1004,
            (_, payload) => payloadTcs.TrySetResult(payload.ToArray()));

        byte[] payload = [0xCA, 0xFE, 0xBA, 0xBE];
        byte[] channelData = new byte[ChannelPduHeader.HeaderLength + payload.Length];
        var writer = new RdpPacketWriter(channelData);
        new ChannelPduHeader(
            (uint)payload.Length,
            ChannelPduFlags.First | ChannelPduFlags.Last).Write(ref writer);
        writer.WriteSpan(payload);

        byte[] packet = BuildMcsSendDataIndication(1004, channelData);
        await streams.ServerStream.WriteAsync(packet, ct);
        await streams.ServerStream.FlushAsync(ct);

        Assert.Equal(payload, await payloadTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct));
    }

    [AvaloniaFact]
    public async Task ProcessingLoop_DrdynvcCapabilities_DispatchesAndSendsStaticChannelResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair streams = new DuplexStreamPair();
        using RdpClient client = new RdpClient(
            new RdpSessionOptions(),
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(
                new PlainRdpSecurityTransport(streams.ClientStream)));
        await client.ConnectAsync(ct);
        client.RegisterStaticChannels([1004, 1005, 1006, 1007]);

        Assert.NotNull(client.StaticVirtualChannels);
        Assert.True(client.StaticVirtualChannels.TryGetChannelId("drdynvc", out ushort drdynvcChannelId));
        Assert.Equal(1007, drdynvcChannelId);

        byte[] capabilities = new byte[16];
        var capabilitiesWriter = new RdpPacketWriter(capabilities);
        new DvcCapabilitiesPdu(version: 3).Write(ref capabilitiesWriter);
        byte[] channelData = new byte[ChannelPduHeader.HeaderLength + capabilitiesWriter.WrittenCount];
        var channelWriter = new RdpPacketWriter(channelData);
        new ChannelPduHeader(
            (uint)capabilitiesWriter.WrittenCount,
            ChannelPduFlags.First | ChannelPduFlags.Last).Write(ref channelWriter);
        channelWriter.WriteSpan(capabilities.AsSpan(0, capabilitiesWriter.WrittenCount));

        await streams.ServerStream.WriteAsync(
            BuildMcsSendDataIndication(drdynvcChannelId, channelData),
            ct);
        await streams.ServerStream.FlushAsync(ct);

        byte[] responseHeader = new byte[4];
        await streams.ServerStream.ReadExactlyAsync(responseHeader, ct);
        int responseLength = BinaryPrimitives.ReadUInt16BigEndian(responseHeader.AsSpan(2));
        byte[] response = new byte[responseLength];
        responseHeader.CopyTo(response, 0);
        await streams.ServerStream.ReadExactlyAsync(response.AsMemory(4), ct);

        Assert.Equal(0x64, response[7]); // MCS SendDataRequest
        Assert.Equal(drdynvcChannelId, BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(10, 2)));

        var responseReader = new RdpPacketReader(response.AsSpan(14));
        Assert.True(ChannelPduHeader.TryRead(ref responseReader, out ChannelPduHeader channelHeader));
        Assert.Equal(
            ChannelPduFlags.First | ChannelPduFlags.Last,
            channelHeader.Flags & (ChannelPduFlags.First | ChannelPduFlags.Last));
        Assert.True(DvcCapabilitiesPdu.TryRead(ref responseReader, out DvcCapabilitiesPdu responseCapabilities));
        Assert.Equal(3, responseCapabilities.Version);
        Assert.Equal(0, responseReader.UnreadLength);
        Assert.Equal(4u, channelHeader.Length);
        Assert.Equal(3, client.DynamicVirtualChannels!.NegotiatedVersion);
    }

    [AvaloniaFact]
    public async Task SendInputEventAsync_WritesToStream()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();

        RdpKeyboardEvent kbEvent = new RdpKeyboardEvent(1000, RdpKeyboardFlags.Down, 0x1E, isVirtualKey: false);
        RdpInputEvent inputEvent = new RdpInputEvent(1000, kbEvent);

        await client.SendInputEventAsync(inputEvent);

        byte[] readBuffer = new byte[16];
        int read = await streams.ServerStream.ReadAsync(readBuffer);

        Assert.Equal(4, read);
        Assert.Equal(4, readBuffer[1]);

        RdpPacketReader reader = new RdpPacketReader(readBuffer.AsSpan(2, 2));
        bool readSuccess = RdpFastPathInputEvent.TryRead(ref reader, out RdpFastPathInputEvent parsedInput);

        Assert.True(readSuccess);
        Assert.Equal(FastPathInputEventCode.ScanCode, parsedInput.Code);
        Assert.Equal(0x1E, parsedInput.KeyCode);
    }

    [AvaloniaFact]
    public async Task SendInputEventAsync_WhenFastPathDisabled_WritesSlowPathInputPdu()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions { EnableFastPath = false };

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();

        var inputEvent = new RdpInputEvent(
            1000,
            new RdpKeyboardEvent(1000, RdpKeyboardFlags.Down, 0x1E, isVirtualKey: false));
        await client.SendInputEventAsync(inputEvent);

        byte[] packet = new byte[64];
        int read = await streams.ServerStream.ReadAsync(packet);

        Assert.Equal(read, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(2, 2)));
        Assert.Equal(0x64, packet[7]); // MCS SendDataRequest
        Assert.Equal(28, packet[28]); // PDUTYPE2_INPUT

        var reader = new RdpPacketReader(packet.AsSpan(36, RdpInputEvent.EventLength));
        Assert.True(RdpInputEvent.TryRead(ref reader, out RdpInputEvent parsedInput));
        Assert.Equal(1000u, parsedInput.EventTime);
        Assert.Equal(RdpInputMessageType.ScanCode, parsedInput.MessageType);
        Assert.Equal(0x1Eu, parsedInput.KeyboardEvent.KeyCode);
    }

    [AvaloniaFact]
    public async Task SendFastPathInputEventAsync_WritesToStream()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();

        RdpFastPathInputEvent fpInput = new RdpFastPathInputEvent(FastPathKeyboardFlags.None, 0x2A);

        await client.SendFastPathInputEventAsync(fpInput);

        byte[] readBuffer = new byte[10];
        int read = await streams.ServerStream.ReadAsync(readBuffer, 0, 10);

        Assert.True(read >= 2);

        Assert.Equal(read, readBuffer[1]);
        RdpPacketReader reader = new RdpPacketReader(readBuffer.AsSpan(2, read - 2));
        bool readSuccess = RdpFastPathInputEvent.TryRead(ref reader, out RdpFastPathInputEvent parsedFp);

        Assert.True(readSuccess);
        Assert.Equal(FastPathInputEventCode.ScanCode, parsedFp.Code);
        Assert.Equal(0x2A, parsedFp.KeyCode);
    }

    [AvaloniaFact]
    public async Task SendInputEvent_WhenDisconnected_ThrowsInvalidOperationException()
    {
        RdpSessionOptions options = new RdpSessionOptions();
        using RdpClient client = new RdpClient(options);

        RdpKeyboardEvent kbEvent = new RdpKeyboardEvent(1000, RdpKeyboardFlags.Down, 0x1E, isVirtualKey: false);
        RdpInputEvent inputEvent = new RdpInputEvent(1000, kbEvent);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendInputEventAsync(inputEvent));
    }

    [AvaloniaFact]
    public async Task ConnectionFailure_TransitionsToFaulted()
    {
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => throw new InvalidOperationException("Failed to connect transport"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ConnectAsync());

        Assert.Equal(RdpConnectionState.Faulted, client.State);
    }

    private static byte[] BuildFastPathBitmapPdu(ushort left, ushort top, ushort width, ushort height, ushort bpp, byte[] pixelData)
    {
        using MemoryStream ms = new MemoryStream();

        // Server header (3 bytes)
        ms.Write(new byte[3]);

        // FastPath update header (3 bytes)
        ms.WriteByte(0x01); // updateCode = Bitmap
        ms.WriteByte(0x00); ms.WriteByte(0x00); // updateSize placeholder

        long dataStart = ms.Position;

        // TS_BITMAP_UPDATE_DATA header
        ms.WriteByte(0x01); ms.WriteByte(0x00); // updateType = 1
        ms.WriteByte(0x01); ms.WriteByte(0x00); // numberRectangles = 1

        // TS_BITMAP_DATA
        ushort destRight = (ushort)(left + width - 1);
        ushort destBottom = (ushort)(top + height - 1);

        WriteUInt16LE(ms, left);
        WriteUInt16LE(ms, top);
        WriteUInt16LE(ms, destRight);
        WriteUInt16LE(ms, destBottom);
        WriteUInt16LE(ms, width);
        WriteUInt16LE(ms, height);
        WriteUInt16LE(ms, bpp);
        WriteUInt16LE(ms, 0x0000); // flags = uncompressed
        WriteUInt16LE(ms, (ushort)pixelData.Length);
        ms.Write(pixelData, 0, pixelData.Length);

        ushort updateSize = (ushort)(ms.Position - dataStart);
        ms.Position = 4;
        WriteUInt16LE(ms, updateSize);

        ushort totalLen = (ushort)ms.Length;
        ms.Position = 0;
        ms.WriteByte(0x00);
        ms.WriteByte((byte)(0x80 | (totalLen >> 8)));
        ms.WriteByte((byte)(totalLen & 0xFF));

        return ms.ToArray();
    }

    private static byte[] BuildSlowPathBitmapPdu(
        ushort channelId,
        ushort left,
        ushort top,
        ushort width,
        ushort height,
        ushort bpp,
        byte[] pixelData)
    {
        using var bitmap = new MemoryStream();
        WriteUInt16LE(bitmap, 1); // UPDATETYPE_BITMAP
        WriteUInt16LE(bitmap, 1); // numberRectangles
        WriteUInt16LE(bitmap, left);
        WriteUInt16LE(bitmap, top);
        WriteUInt16LE(bitmap, checked((ushort)(left + width - 1)));
        WriteUInt16LE(bitmap, checked((ushort)(top + height - 1)));
        WriteUInt16LE(bitmap, width);
        WriteUInt16LE(bitmap, height);
        WriteUInt16LE(bitmap, bpp);
        WriteUInt16LE(bitmap, 0);
        WriteUInt16LE(bitmap, checked((ushort)pixelData.Length));
        bitmap.Write(pixelData);

        byte[] graphics = bitmap.ToArray();
        byte[] shareData = new byte[18 + graphics.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(shareData, checked((ushort)shareData.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(shareData.AsSpan(2), 0x0017);
        BinaryPrimitives.WriteUInt16LittleEndian(shareData.AsSpan(4), 1002);
        BinaryPrimitives.WriteUInt32LittleEndian(shareData.AsSpan(6), 0x000103EA);
        shareData[11] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(
            shareData.AsSpan(12),
            checked((ushort)(graphics.Length + 4)));
        shareData[14] = 2; // PDUTYPE2_UPDATE
        graphics.CopyTo(shareData, 18);
        return BuildMcsSendDataIndication(channelId, shareData);
    }

    private static byte[] BuildMcsSendDataIndication(ushort channelId, byte[] userData)
    {
        using var domain = new MemoryStream();
        domain.WriteByte(0x68);
        domain.WriteByte(0x00);
        domain.WriteByte(0x01);
        domain.WriteByte((byte)(channelId >> 8));
        domain.WriteByte((byte)channelId);
        domain.WriteByte(0x70);
        if (userData.Length < 0x80)
        {
            domain.WriteByte((byte)userData.Length);
        }
        else
        {
            domain.WriteByte((byte)(0x80 | (userData.Length >> 8)));
            domain.WriteByte((byte)userData.Length);
        }
        domain.Write(userData);

        byte[] domainData = domain.ToArray();
        byte[] packet = new byte[7 + domainData.Length];
        packet[0] = 3;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), checked((ushort)packet.Length));
        packet[4] = 2;
        packet[5] = 0xF0;
        packet[6] = 0x80;
        domainData.CopyTo(packet, 7);
        return packet;
    }

    private static void WriteUInt16LE(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)(value >> 8));
    }
}
