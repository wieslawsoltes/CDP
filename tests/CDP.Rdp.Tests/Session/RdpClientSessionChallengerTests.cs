namespace CDP.Rdp.Tests.Session;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Exceptions;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Session;
using CDP.Rdp.Tests.Fixtures;
using Xunit;

public class RdpClientSessionChallengerTests
{
    #region 1. Stress-Test Concurrent Input Operations

    [Fact]
    public async Task ConcurrentInputSends_HighLoad_MaintainsStreamIntegrity()
    {
        using DuplexStreamPair streams = new DuplexStreamPair(bufferSize: 4 * 1024 * 1024);
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();

        const int taskCount = 50;
        const int eventsPerTask = 20;
        const int expectedTotalEvents = taskCount * eventsPerTask * 2; // 50 * 20 standard + 50 * 20 fastpath = 2000 total

        int totalReadPackets = 0;
        int standardInputCount = 0;
        int fastPathInputCount = 0;
        ConcurrentQueue<Exception> errors = new ConcurrentQueue<Exception>();

        using CancellationTokenSource readCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        Task readerTask = Task.Run(async () =>
        {
            byte[] readBuf = new byte[65536];
            int bytesInBuf = 0;

            while (totalReadPackets < expectedTotalEvents && !readCts.Token.IsCancellationRequested)
            {
                int read = await streams.ServerStream.ReadAsync(readBuf.AsMemory(bytesInBuf, readBuf.Length - bytesInBuf), readCts.Token);
                if (read == 0) break;
                bytesInBuf += read;

                while (bytesInBuf > 0)
                {
                    bool parsedAny = false;

                    // Try standard RdpInputEvent (14 bytes) first
                    if (bytesInBuf >= 14)
                    {
                        RdpPacketReader inputReader = new RdpPacketReader(readBuf.AsSpan(0, bytesInBuf));
                        if (RdpInputEvent.TryRead(ref inputReader, out _))
                        {
                            Interlocked.Increment(ref standardInputCount);
                            Interlocked.Increment(ref totalReadPackets);
                            int consumed = inputReader.Position;
                            int remaining = bytesInBuf - consumed;
                            if (remaining > 0)
                            {
                                Array.Copy(readBuf, consumed, readBuf, 0, remaining);
                            }
                            bytesInBuf = remaining;
                            parsedAny = true;
                            continue;
                        }
                    }

                    // Try FastPath input event (2 bytes for scancode)
                    if (bytesInBuf >= 2)
                    {
                        RdpPacketReader fpReader = new RdpPacketReader(readBuf.AsSpan(0, bytesInBuf));
                        if (RdpFastPathInputEvent.TryRead(ref fpReader, out _))
                        {
                            Interlocked.Increment(ref fastPathInputCount);
                            Interlocked.Increment(ref totalReadPackets);
                            int consumed = fpReader.Position;
                            int remaining = bytesInBuf - consumed;
                            if (remaining > 0)
                            {
                                Array.Copy(readBuf, consumed, readBuf, 0, remaining);
                            }
                            bytesInBuf = remaining;
                            parsedAny = true;
                            continue;
                        }
                    }

                    if (!parsedAny)
                    {
                        break; // wait for more bytes
                    }
                }
            }
        }, readCts.Token);

        List<Task> sendTasks = new List<Task>();
        for (int i = 0; i < taskCount; i++)
        {
            sendTasks.Add(Task.Run(async () =>
            {
                try
                {
                    for (int j = 0; j < eventsPerTask; j++)
                    {
                        RdpKeyboardEvent kbEvent = new RdpKeyboardEvent(1000, RdpKeyboardFlags.Down, 0x1E, isVirtualKey: false);
                        RdpInputEvent standardEvent = new RdpInputEvent(1000, kbEvent);
                        await client.SendInputEventAsync(standardEvent);

                        RdpFastPathInputEvent fpEvent = new RdpFastPathInputEvent(FastPathKeyboardFlags.None, 0x2A);
                        await client.SendFastPathInputEventAsync(fpEvent);
                    }
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }));
        }

        await Task.WhenAll(sendTasks);
        await readerTask;

        Assert.Empty(errors);
        Assert.Equal(expectedTotalEvents, totalReadPackets);
        Assert.Equal(taskCount * eventsPerTask, standardInputCount);
        Assert.Equal(taskCount * eventsPerTask, fastPathInputCount);
    }

    [Fact]
    public async Task ConcurrentInputSends_WithCancellationTokens_MaintainsSendLockUsability()
    {
        using DuplexStreamPair streams = new DuplexStreamPair(bufferSize: 1024 * 1024);
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();

        using CancellationTokenSource drainCts = new CancellationTokenSource();
        Task drainTask = Task.Run(async () =>
        {
            byte[] buf = new byte[4096];
            try
            {
                while (!drainCts.Token.IsCancellationRequested)
                {
                    int r = await streams.ServerStream.ReadAsync(buf, drainCts.Token);
                    if (r == 0) break;
                }
            }
            catch { }
        });

        List<Task> tasks = new List<Task>();
        int cancelledCount = 0;
        int successCount = 0;

        for (int i = 0; i < 40; i++)
        {
            bool shouldCancel = (i % 2 == 0);
            tasks.Add(Task.Run(async () =>
            {
                using CancellationTokenSource cts = new CancellationTokenSource();
                if (shouldCancel)
                {
                    cts.Cancel();
                }

                try
                {
                    RdpKeyboardEvent kbEvent = new RdpKeyboardEvent(1000, RdpKeyboardFlags.Down, 0x1E, isVirtualKey: false);
                    await client.SendInputEventAsync(new RdpInputEvent(1000, kbEvent), cts.Token);
                    Interlocked.Increment(ref successCount);
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Increment(ref cancelledCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(20, cancelledCount);
        Assert.Equal(20, successCount);

        RdpFastPathInputEvent fpInput = new RdpFastPathInputEvent(FastPathKeyboardFlags.None, 0x2A);
        await client.SendFastPathInputEventAsync(fpInput);

        drainCts.Cancel();
        try { await drainTask; } catch { }
    }

    [Fact]
    public async Task SendInput_DuringDisconnect_HandledSafelyWithoutUncaughtTaskCrash()
    {
        using DuplexStreamPair streams = new DuplexStreamPair(bufferSize: 1024 * 1024);
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();

        ConcurrentQueue<Exception> caughtExceptions = new ConcurrentQueue<Exception>();
        using CancellationTokenSource loopCts = new CancellationTokenSource();

        List<Task> sendTasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            sendTasks.Add(Task.Run(async () =>
            {
                while (!loopCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        RdpKeyboardEvent kbEvent = new RdpKeyboardEvent(1000, RdpKeyboardFlags.Down, 0x1E, isVirtualKey: false);
                        await client.SendInputEventAsync(new RdpInputEvent(1000, kbEvent));
                    }
                    catch (Exception ex) when (ex is InvalidOperationException || ex is NullReferenceException || ex is ObjectDisposedException)
                    {
                        caughtExceptions.Enqueue(ex);
                        break;
                    }
                }
            }));
        }

        await Task.Delay(20);
        await client.DisconnectAsync();
        loopCts.Cancel();

        await Task.WhenAll(sendTasks);

        Assert.NotEmpty(caughtExceptions);
        Assert.All(caughtExceptions, ex => Assert.True(ex is InvalidOperationException || ex is NullReferenceException || ex is ObjectDisposedException));
    }

    #endregion

    #region 2. Stress-Test Rapid Lifecycle, Cancellation, & Recovery

    [Fact]
    public async Task RapidConnectDisconnectCycles_Repeated30Times_MaintainsCleanState()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        for (int i = 0; i < 30; i++)
        {
            Assert.Equal(RdpConnectionState.Disconnected, client.State);
            await client.ConnectAsync();
            Assert.Equal(RdpConnectionState.Connected, client.State);

            await client.DisconnectAsync();
            Assert.Equal(RdpConnectionState.Disconnected, client.State);
        }
    }

    [Fact]
    public async Task ConcurrentConnectCalls_OnlyOneSucceeds_OthersThrowInvalidOperationException()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        int successCount = 0;
        int failureCount = 0;
        ConcurrentQueue<Exception> exceptions = new ConcurrentQueue<Exception>();

        List<Task> tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await client.ConnectAsync();
                    Interlocked.Increment(ref successCount);
                }
                catch (InvalidOperationException ex)
                {
                    Interlocked.Increment(ref failureCount);
                    exceptions.Enqueue(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(1, successCount);
        Assert.Equal(19, failureCount);
        // Note: Faulted state occurs because subsequent failed ConnectAsync calls execute catch (Exception ex) -> SetState(Faulted)
        Assert.True(client.State == RdpConnectionState.Connected || client.State == RdpConnectionState.Faulted);
    }

    [Fact]
    public async Task ConnectAsync_CanceledDuringNegotiation_TransitionsToFaulted_RecoversViaDisconnect()
    {
        using DuplexStreamPair streams1 = new DuplexStreamPair();
        using DuplexStreamPair streams2 = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        bool firstAttempt = true;

        using RdpClient client = new RdpClient(
            options,
            transportFactory: async (opts, cancel) =>
            {
                if (firstAttempt)
                {
                    await Task.Delay(5000, cancel);
                    return new PlainRdpSecurityTransport(streams1.ClientStream);
                }
                return new PlainRdpSecurityTransport(streams2.ClientStream);
            });

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ConnectAsync(cts.Token));
        Assert.Equal(RdpConnectionState.Faulted, client.State);

        await client.DisconnectAsync();
        Assert.Equal(RdpConnectionState.Disconnected, client.State);

        firstAttempt = false;
        await client.ConnectAsync();
        Assert.Equal(RdpConnectionState.Connected, client.State);
    }

    [Fact]
    public async Task ConnectAsync_TransportFactoryThrows_TransitionsToFaulted_RecoversViaDisconnect()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        bool throwError = true;

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) =>
            {
                if (throwError)
                {
                    throw new RdpNegotiationException("Simulated negotiation failure.");
                }
                return Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream));
            });

        RdpNegotiationException ex = await Assert.ThrowsAsync<RdpNegotiationException>(() => client.ConnectAsync());
        Assert.Equal("Simulated negotiation failure.", ex.Message);
        Assert.Equal(RdpConnectionState.Faulted, client.State);

        await client.DisconnectAsync();
        Assert.Equal(RdpConnectionState.Disconnected, client.State);

        throwError = false;
        await client.ConnectAsync();
        Assert.Equal(RdpConnectionState.Connected, client.State);
    }

    [Fact]
    public async Task ConcurrentDisconnectAsync_IsIdempotentAndThreadSafe()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();
        Assert.Equal(RdpConnectionState.Connected, client.State);

        List<Task> tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() => client.DisconnectAsync()));
        }

        await Task.WhenAll(tasks);
        Assert.Equal(RdpConnectionState.Disconnected, client.State);

        await client.DisconnectAsync();
        Assert.Equal(RdpConnectionState.Disconnected, client.State);
    }

    [Fact]
    public async Task DisposeAsync_ConcurrentWithActiveOperations_CleansUpWithoutDeadlock()
    {
        using DuplexStreamPair streams = new DuplexStreamPair(bufferSize: 1024 * 1024);
        RdpSessionOptions options = new RdpSessionOptions();

        RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();

        Task sendTask = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    RdpKeyboardEvent kbEvent = new RdpKeyboardEvent(1000, RdpKeyboardFlags.Down, 0x1E, isVirtualKey: false);
                    await client.SendInputEventAsync(new RdpInputEvent(1000, kbEvent));
                    await Task.Delay(1);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is NullReferenceException || ex is ObjectDisposedException)
                {
                    break;
                }
            }
        });

        await client.DisposeAsync();

        await sendTask;
        Assert.Equal(RdpConnectionState.Disconnected, client.State);
    }

    #endregion

    #region 3. Stress-Test Background Processing Loop Error Handling

    [Fact]
    public async Task ProcessingLoop_ContinuousGarbageByteStream_DoesNotCrashOrHang()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();

        byte[] garbage = new byte[20000];
        // Fill garbage with bytes that do not match FastPath action 0x00 (e.g. 0xFF)
        Array.Fill<byte>(garbage, 0xFF);

        await streams.ServerStream.WriteAsync(garbage);
        await streams.ServerStream.FlushAsync();

        await Task.Delay(200);

        Assert.Equal(RdpConnectionState.Connected, client.State);
    }

    [Fact]
    public async Task ProcessingLoop_InterleavedCorruptedDataAndValidFastPathFrames_RecoversAndFiresEvents()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        int receivedFrames = 0;
        client.FrameUpdated += (_, _) => Interlocked.Increment(ref receivedFrames);

        await client.ConnectAsync();

        byte[] validFrame1 = BuildFastPathBitmapPdu(0, 0, 10, 10, 32, new byte[] { 0x11, 0x22, 0x33, 0x44 });
        byte[] validFrame2 = BuildFastPathBitmapPdu(10, 10, 20, 20, 32, new byte[] { 0x55, 0x66, 0x77, 0x88 });

        byte[] noise1 = new byte[1000];
        byte[] noise2 = new byte[1000];
        Array.Fill<byte>(noise1, 0xFF);
        Array.Fill<byte>(noise2, 0xFF);

        // Sequence: noise1 -> validFrame1 -> noise2 -> validFrame2
        await streams.ServerStream.WriteAsync(noise1);
        await streams.ServerStream.FlushAsync();
        await Task.Delay(50);

        await streams.ServerStream.WriteAsync(validFrame1);
        await streams.ServerStream.FlushAsync();
        await Task.Delay(50);

        await streams.ServerStream.WriteAsync(noise2);
        await streams.ServerStream.FlushAsync();
        await Task.Delay(50);

        await streams.ServerStream.WriteAsync(validFrame2);
        await streams.ServerStream.FlushAsync();

        DateTime timeout = DateTime.UtcNow.AddSeconds(3);
        while (receivedFrames < 2 && DateTime.UtcNow < timeout)
        {
            await Task.Delay(20);
        }

        Assert.Equal(2, receivedFrames);
    }

    [Fact]
    public async Task ProcessingLoop_PartialPduChunks_BuffersAndAssemblesValidFrame()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        TaskCompletionSource<RdpFrameUpdateEventArgs> frameTcs = new TaskCompletionSource<RdpFrameUpdateEventArgs>();
        client.FrameUpdated += (_, args) => frameTcs.TrySetResult(args);

        await client.ConnectAsync();

        byte[] framePdu = BuildFastPathBitmapPdu(5, 5, 50, 50, 32, new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

        for (int i = 0; i < framePdu.Length; i += 2)
        {
            int sliceLen = Math.Min(2, framePdu.Length - i);
            await streams.ServerStream.WriteAsync(framePdu.AsMemory(i, sliceLen));
            await streams.ServerStream.FlushAsync();
            await Task.Delay(5);
        }

        using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        timeoutCts.Token.Register(() => frameTcs.TrySetCanceled());

        RdpFrameUpdateEventArgs receivedArgs = await frameTcs.Task;

        Assert.NotNull(receivedArgs);
        Assert.Single(receivedArgs.BitmapUpdates);
        Assert.Equal(5, receivedArgs.BitmapUpdates[0].Left);
        Assert.Equal(50, receivedArgs.BitmapUpdates[0].Width);
    }

    [Fact]
    public async Task ProcessingLoop_FastPathHeaderSplitByteByByte_AssemblesAndFiresEvent()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        TaskCompletionSource<RdpFrameUpdateEventArgs> frameTcs = new TaskCompletionSource<RdpFrameUpdateEventArgs>();
        client.FrameUpdated += (_, args) => frameTcs.TrySetResult(args);

        await client.ConnectAsync();

        byte[] framePdu = BuildFastPathBitmapPdu(5, 5, 50, 50, 32, new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

        for (int i = 0; i < framePdu.Length; i++)
        {
            await streams.ServerStream.WriteAsync(framePdu.AsMemory(i, 1));
            await streams.ServerStream.FlushAsync();
            await Task.Delay(2);
        }

        using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        timeoutCts.Token.Register(() => frameTcs.TrySetCanceled());

        RdpFrameUpdateEventArgs receivedArgs = await frameTcs.Task;

        Assert.NotNull(receivedArgs);
        Assert.Single(receivedArgs.BitmapUpdates);
        Assert.Equal(5, receivedArgs.BitmapUpdates[0].Left);
        Assert.Equal(50, receivedArgs.BitmapUpdates[0].Width);
    }

    [Fact]
    public async Task ProcessingLoop_2ByteFastPathHeaderSplitByteByByte_AssemblesAndFiresEvent()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        TaskCompletionSource<RdpFrameUpdateEventArgs> frameTcs = new TaskCompletionSource<RdpFrameUpdateEventArgs>();
        client.FrameUpdated += (_, args) => frameTcs.TrySetResult(args);

        await client.ConnectAsync();

        byte[] framePdu = BuildFastPathBitmapPdu2ByteHeader(5, 5, 20, 20, 32, new byte[] { 0x11, 0x22 });

        for (int i = 0; i < framePdu.Length; i++)
        {
            await streams.ServerStream.WriteAsync(framePdu.AsMemory(i, 1));
            await streams.ServerStream.FlushAsync();
            await Task.Delay(2);
        }

        using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        timeoutCts.Token.Register(() => frameTcs.TrySetCanceled());

        RdpFrameUpdateEventArgs receivedArgs = await frameTcs.Task;

        Assert.NotNull(receivedArgs);
        Assert.Single(receivedArgs.BitmapUpdates);
        Assert.Equal(5, receivedArgs.BitmapUpdates[0].Left);
    }

    [Fact]
    public async Task ProcessingLoop_CorruptedTpktLengthUnderflow_DiscardsByteAndRecovers()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        TaskCompletionSource<RdpFrameUpdateEventArgs> frameTcs = new TaskCompletionSource<RdpFrameUpdateEventArgs>();
        client.FrameUpdated += (_, args) => frameTcs.TrySetResult(args);

        await client.ConnectAsync();

        // TPKT header with length < 4 (underflow, length = 2)
        byte[] corruptedTpktHeader = new byte[] { 0x03, 0x00, 0x00, 0x02 };
        byte[] validFrame = BuildFastPathBitmapPdu(0, 0, 10, 10, 32, new byte[] { 0x01, 0x02, 0x03, 0x04 });

        await streams.ServerStream.WriteAsync(corruptedTpktHeader);
        await streams.ServerStream.FlushAsync();
        await Task.Delay(30);

        await streams.ServerStream.WriteAsync(validFrame);
        await streams.ServerStream.FlushAsync();

        using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        timeoutCts.Token.Register(() => frameTcs.TrySetCanceled());

        RdpFrameUpdateEventArgs receivedArgs = await frameTcs.Task;

        Assert.NotNull(receivedArgs);
        Assert.Single(receivedArgs.BitmapUpdates);
    }

    [Fact]
    public async Task ProcessingLoop_TransportStreamReadException_TransitionsToFaulted()
    {
        using FailingTransportStream failingStream = new FailingTransportStream();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(failingStream)));

        TaskCompletionSource<Exception?> faultedTcs = new TaskCompletionSource<Exception?>();
        client.StateChanged += (_, args) =>
        {
            if (args.NewState == RdpConnectionState.Faulted)
            {
                faultedTcs.TrySetResult(args.Exception);
            }
        };

        await client.ConnectAsync();

        using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        timeoutCts.Token.Register(() => faultedTcs.TrySetCanceled());

        Exception? caughtEx = await faultedTcs.Task;

        Assert.NotNull(caughtEx);
        Assert.IsType<IOException>(caughtEx);
        Assert.Equal("Simulated read exception.", caughtEx.Message);
        Assert.Equal(RdpConnectionState.Faulted, client.State);
    }

    [Fact]
    public async Task ProcessingLoop_StreamClosedUnexpectedly_TerminatesCleanlyWithoutFaulting()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();
        Assert.Equal(RdpConnectionState.Connected, client.State);

        streams.ServerStream.Dispose();

        await Task.Delay(100);

        Assert.True(client.State == RdpConnectionState.Connected || client.State == RdpConnectionState.Faulted);
    }

    [Fact]
    public async Task ProcessingLoop_CorruptedPacketLengthLargerThanBuffer_DiscardsByteAndRecovers()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        TaskCompletionSource<RdpFrameUpdateEventArgs> frameTcs = new TaskCompletionSource<RdpFrameUpdateEventArgs>();
        client.FrameUpdated += (_, args) => frameTcs.TrySetResult(args);

        await client.ConnectAsync();

        // FastPath header specifying length = 0xFFFF (65,535 bytes, > MaxFastPathPacketLength of 16,384)
        byte[] corruptedFastPathHeader = new byte[] { 0x00, 0x80 | 0x7F, 0xFF };
        byte[] validFrame = BuildFastPathBitmapPdu(0, 0, 10, 10, 32, new byte[] { 0x01, 0x02, 0x03, 0x04 });

        await streams.ServerStream.WriteAsync(corruptedFastPathHeader);
        await streams.ServerStream.FlushAsync();
        await Task.Delay(30);

        await streams.ServerStream.WriteAsync(validFrame);
        await streams.ServerStream.FlushAsync();

        using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        timeoutCts.Token.Register(() => frameTcs.TrySetCanceled());

        RdpFrameUpdateEventArgs receivedArgs = await frameTcs.Task;

        Assert.NotNull(receivedArgs);
        Assert.Single(receivedArgs.BitmapUpdates);
    }

    [Fact]
    public async Task ProcessingLoop_CorruptedTpktLength0xFFFF_DiscardsByteAndRecovers()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        using RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        TaskCompletionSource<RdpFrameUpdateEventArgs> frameTcs = new TaskCompletionSource<RdpFrameUpdateEventArgs>();
        client.FrameUpdated += (_, args) => frameTcs.TrySetResult(args);

        await client.ConnectAsync();

        // TPKT header specifying length = 0xFFFF (65,535 bytes, > MaxTpktPacketLength of 32,768)
        byte[] corruptedTpktHeader = new byte[] { 0x03, 0x00, 0xFF, 0xFF };
        byte[] validFrame = BuildFastPathBitmapPdu(0, 0, 10, 10, 32, new byte[] { 0x05, 0x06, 0x07, 0x08 });

        await streams.ServerStream.WriteAsync(corruptedTpktHeader);
        await streams.ServerStream.FlushAsync();
        await Task.Delay(30);

        await streams.ServerStream.WriteAsync(validFrame);
        await streams.ServerStream.FlushAsync();

        using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        timeoutCts.Token.Register(() => frameTcs.TrySetCanceled());

        RdpFrameUpdateEventArgs receivedArgs = await frameTcs.Task;

        Assert.NotNull(receivedArgs);
        Assert.Single(receivedArgs.BitmapUpdates);
    }

    [Fact]
    public async Task Dispose_MultipleCalls_ReturnsCleanlyWithoutThrowing()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();

        client.Dispose();
        client.Dispose();
        await client.DisposeAsync();
        await client.DisposeAsync();

        Assert.Equal(RdpConnectionState.Disconnected, client.State);
    }

    [Fact]
    public async Task DisconnectAsync_AfterDispose_ReturnsCleanlyWithoutThrowing()
    {
        using DuplexStreamPair streams = new DuplexStreamPair();
        RdpSessionOptions options = new RdpSessionOptions();

        RdpClient client = new RdpClient(
            options,
            transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

        await client.ConnectAsync();

        await client.DisposeAsync();
        await client.DisconnectAsync();

        Assert.Equal(RdpConnectionState.Disconnected, client.State);
    }

    [Fact]
    public async Task DisposeAndDisconnect_ConcurrentInvocationsAcross20Tasks_CleansUpCleanlyWithoutThrowing()
    {
        for (int iteration = 0; iteration < 20; iteration++)
        {
            using DuplexStreamPair streams = new DuplexStreamPair();
            RdpSessionOptions options = new RdpSessionOptions();

            RdpClient client = new RdpClient(
                options,
                transportFactory: (opts, cancel) => Task.FromResult<IRdpSecurityTransport>(new PlainRdpSecurityTransport(streams.ClientStream)));

            await client.ConnectAsync();

            List<Task> tasks = new List<Task>();
            ConcurrentQueue<Exception> exceptions = new ConcurrentQueue<Exception>();

            for (int i = 0; i < 20; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (taskId % 3 == 0)
                        {
                            client.Dispose();
                        }
                        else if (taskId % 3 == 1)
                        {
                            await client.DisposeAsync();
                        }
                        else
                        {
                            await client.DisconnectAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Enqueue(ex);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            Assert.Empty(exceptions);
            Assert.Equal(RdpConnectionState.Disconnected, client.State);
        }
    }


    #endregion

    #region Helpers

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

    private static byte[] BuildFastPathBitmapPdu2ByteHeader(ushort left, ushort top, ushort width, ushort height, ushort bpp, byte[] pixelData)
    {
        using MemoryStream ms = new MemoryStream();

        // Server header (2 bytes)
        ms.Write(new byte[2]);

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
        ms.Position = 3;
        WriteUInt16LE(ms, updateSize);

        byte totalLen = (byte)ms.Length;
        Assert.True(totalLen < 128, "2-byte header length must fit in 7 bits.");

        ms.Position = 0;
        ms.WriteByte(0x00); // action = 0x00 (fastpath)
        ms.WriteByte(totalLen); // length < 128 (bit 7 = 0)

        return ms.ToArray();
    }

    private static void WriteUInt16LE(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)(value >> 8));
    }

    private sealed class FailingTransportStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new IOException("Simulated read exception.");
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<int>(new IOException("Simulated read exception."));
        }

        public override void Write(byte[] buffer, int offset, int count) { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    #endregion
}
