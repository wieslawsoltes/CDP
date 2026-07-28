using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Session;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Session;
using CDP.Rdp.Tests.Fixtures;
using Xunit;

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

        byte[] framePdu = BuildFastPathBitmapPdu(10, 20, 100, 50, 32, new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

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
        Assert.Equal(100, rect.Width);
        Assert.Equal(50, rect.Height);
        Assert.Equal(32, rect.Bpp);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, rect.Data.ToArray());
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

        byte[] readBuffer = new byte[14];
        int read = await streams.ServerStream.ReadAsync(readBuffer, 0, 14);

        Assert.Equal(14, read);

        RdpPacketReader reader = new RdpPacketReader(readBuffer);
        bool readSuccess = RdpInputEvent.TryRead(ref reader, out RdpInputEvent parsedInput);

        Assert.True(readSuccess);
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

        RdpPacketReader reader = new RdpPacketReader(readBuffer.AsSpan(0, read));
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

    private static void WriteUInt16LE(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)(value >> 8));
    }
}
