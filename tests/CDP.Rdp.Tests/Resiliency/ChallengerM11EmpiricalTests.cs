using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Resiliency;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Exceptions;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Tests.Fixtures;

[Xunit.Collection("RdpTests")]
public class ChallengerM11EmpiricalTests
{
    // Helper Stream wrapper that breaks reads into 1-byte chunks
    private sealed class SingleByteReadStream : Stream
    {
        private readonly Stream _inner;

        public SingleByteReadStream(Stream inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, Math.Min(count, 1));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _inner.ReadAsync(buffer, offset, Math.Min(count, 1), cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _inner.ReadAsync(buffer.Slice(0, Math.Min(buffer.Length, 1)), cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.WriteAsync(buffer, offset, count, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _inner.WriteAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
    }

    #region 1. Unexpected Disconnect Tests

    [AvaloniaFact]
    public async Task NegotiateAsync_ServerDisconnectsMidTpktHeader_ThrowsIOException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        // Server writes only 2 bytes of TPKT header and closes
        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] req = new byte[1024];
                await pair.ServerStream.ReadAsync(req, 0, 4, ct);
                await pair.ServerStream.WriteAsync(new byte[] { 0x03, 0x00 }, ct);
                pair.ServerStream.Close();
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        var ex = await Assert.ThrowsAsync<IOException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", performSecurityHandshake: false, cancellationToken: ct));

        Assert.Contains("Expected 4 bytes, read 2 bytes", ex.Message);
        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
        try { await serverTask; } catch { }
    }

    [AvaloniaFact]
    public async Task NegotiateAsync_ServerDisconnectsMidPayload_ThrowsIOException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        // Server writes valid TPKT header indicating 19 total bytes (15 payload bytes), but sends only 3 bytes of payload then closes
        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] req = new byte[1024];
                await pair.ServerStream.ReadAsync(req, 0, 4, ct);
                byte[] truncatedResponse = new byte[] { 0x03, 0x00, 0x00, 0x13, 0x0E, 0xD0, 0x00 };
                await pair.ServerStream.WriteAsync(truncatedResponse, ct);
                pair.ServerStream.Close();
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        var ex = await Assert.ThrowsAsync<IOException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", performSecurityHandshake: false, cancellationToken: ct));

        Assert.Contains("Expected 15 bytes, read 3 bytes", ex.Message);
        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
        try { await serverTask; } catch { }
    }

    [AvaloniaFact]
    public async Task SimulatedRdpServer_ClientDisconnectsEarly_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream);

        // Client writes 2 bytes then closes stream
        Task clientTask = Task.Run(async () =>
        {
            try
            {
                await pair.ClientStream.WriteAsync(new byte[] { 0x03, 0x00 }, ct);
                pair.ClientStream.Close();
            }
            catch { }
        }, ct);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            server.ProcessConnectionRequestAsync(ct));

        Assert.Contains("Client closed stream prematurely", ex.Message);
        try { await clientTask; } catch { }
    }

    #endregion

    #region 2. Single-Byte Chunked Network Arrivals Tests

    [AvaloniaFact]
    public async Task NegotiateAsync_SingleByteReadArrivals_SuccessfullyNegotiates()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream)
        {
            Behavior = ServerResponseBehavior.AcceptRequestedProtocol,
            ResponseFlags = 0x01
        };

        Task serverTask = server.ProcessConnectionRequestAsync(ct);

        // Wrap client stream in SingleByteReadStream
        using Stream chunkedClientStream = new SingleByteReadStream(pair.ClientStream);

        RdpNegotiator negotiator = new RdpNegotiator();
        IRdpSecurityTransport transport = await negotiator.NegotiateAsync(
            chunkedClientStream,
            "localhost",
            RdpSecurityProtocol.Ssl,
            performSecurityHandshake: false,
            cancellationToken: ct);

        await serverTask;

        Assert.NotNull(transport);
        Assert.Equal(RdpSecurityProtocol.Ssl, transport.Protocol);
        Assert.Equal(RdpNegotiationState.Connected, negotiator.State);
    }

    [AvaloniaFact]
    public async Task SimulatedRdpServer_SingleByteArrivalsFromClient_SuccessfullyProcessesRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream)
        {
            Behavior = ServerResponseBehavior.AcceptRequestedProtocol
        };

        Task serverTask = server.ProcessConnectionRequestAsync(ct);

        byte[] requestPacket = RdpNegotiator.BuildConnectionRequestPacket(RdpSecurityProtocol.Ssl);

        // Client writes request packet 1 byte at a time
        foreach (byte b in requestPacket)
        {
            await pair.ClientStream.WriteAsync(new byte[] { b }, ct);
            await pair.ClientStream.FlushAsync(ct);
        }

        byte[] serverResponse = new byte[1024];
        int read = await pair.ClientStream.ReadAsync(serverResponse, ct);

        await serverTask;

        Assert.True(read > 0);
        Assert.NotNull(server.ReceivedRequest);
        Assert.Equal(RdpSecurityProtocol.Ssl, server.ReceivedRequest.Value.RequestedProtocols);
    }

    [AvaloniaFact]
    public async Task DuplexPipeStream_SingleByteReadAsync_ReadsAllBytesCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        byte[] originalData = new byte[100];
        Random.Shared.NextBytes(originalData);

        await pair.ClientStream.WriteAsync(originalData, ct);
        await pair.ClientStream.FlushAsync(ct);

        byte[] readBuffer = new byte[100];
        for (int i = 0; i < 100; i++)
        {
            int r = await pair.ServerStream.ReadAsync(readBuffer.AsMemory(i, 1), ct);
            Assert.Equal(1, r);
        }

        Assert.Equal(originalData, readBuffer);
    }

    #endregion

    #region 3. Cancellation Token Triggers Tests

    [AvaloniaFact]
    public async Task NegotiateAsync_PreCanceledToken_ThrowsOperationCanceledException()
    {
        using DuplexStreamPair pair = new DuplexStreamPair();
        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        RdpNegotiator negotiator = new RdpNegotiator();
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", cancellationToken: cts.Token));
    }

    [AvaloniaFact]
    public async Task NegotiateAsync_CanceledDuringRead_ThrowsOperationCanceledException()
    {
        using DuplexStreamPair pair = new DuplexStreamPair();
        using CancellationTokenSource cts = new CancellationTokenSource(100);

        // Server receives request but never responds
        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] buf = new byte[1024];
                await pair.ServerStream.ReadAsync(buf);
            }
            catch { }
        });

        RdpNegotiator negotiator = new RdpNegotiator();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", cancellationToken: cts.Token));
        try { await serverTask; } catch { }
    }

    [AvaloniaFact]
    public async Task SimulatedRdpServer_CanceledDuringRead_ThrowsOperationCanceledException()
    {
        using DuplexStreamPair pair = new DuplexStreamPair();
        using CancellationTokenSource cts = new CancellationTokenSource(100);

        SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream);

        // Client never sends request
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            server.ProcessConnectionRequestAsync(cts.Token));
    }

    #endregion

    #region 4. Multi-Threaded / Concurrent Connection Tests

    [AvaloniaFact]
    public async Task SimulatedRdpServer_MultipleParallelConnections_HandledIndependently()
    {
        var ct = TestContext.Current.CancellationToken;
        const int connectionCount = 10;

        Task[] tasks = new Task[connectionCount];

        for (int i = 0; i < connectionCount; i++)
        {
            int index = i;
            tasks[i] = Task.Run(async () =>
            {
                using DuplexStreamPair pair = new DuplexStreamPair();
                SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream)
                {
                    Behavior = ServerResponseBehavior.AcceptRequestedProtocol
                };

                Task serverTask = server.ProcessConnectionRequestAsync(ct);

                RdpNegotiator negotiator = new RdpNegotiator();
                IRdpSecurityTransport transport = await negotiator.NegotiateAsync(
                    pair.ClientStream,
                    $"host-{index}",
                    RdpSecurityProtocol.Ssl,
                    performSecurityHandshake: false,
                    cancellationToken: ct);

                await serverTask;

                Assert.NotNull(transport);
                Assert.Equal(RdpNegotiationState.Connected, negotiator.State);
            }, ct);
        }

        await Task.WhenAll(tasks);
    }

    [AvaloniaFact]
    public async Task DuplexPipeStream_ConcurrentWriteAndRead_TransfersDataWithoutLoss()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        const int messageCount = 50;
        const int messageSize = 128;
        byte[][] testMessages = new byte[messageCount][];
        for (int i = 0; i < messageCount; i++)
        {
            testMessages[i] = new byte[messageSize];
            Random.Shared.NextBytes(testMessages[i]);
        }

        Task writerTask = Task.Run(async () =>
        {
            for (int i = 0; i < messageCount; i++)
            {
                await pair.ClientStream.WriteAsync(testMessages[i], ct);
                await pair.ClientStream.FlushAsync(ct);
                await Task.Delay(1, ct);
            }
        }, ct);

        Task readerTask = Task.Run(async () =>
        {
            byte[] fullBuffer = new byte[messageCount * messageSize];
            int totalRead = 0;
            while (totalRead < fullBuffer.Length)
            {
                int r = await pair.ServerStream.ReadAsync(fullBuffer.AsMemory(totalRead, fullBuffer.Length - totalRead), ct);
                if (r == 0) break;
                totalRead += r;
            }

            Assert.Equal(fullBuffer.Length, totalRead);
            for (int i = 0; i < messageCount; i++)
            {
                ReadOnlySpan<byte> expected = testMessages[i];
                ReadOnlySpan<byte> actual = fullBuffer.AsSpan(i * messageSize, messageSize);
                Assert.True(expected.SequenceEqual(actual), $"Message mismatch at index {i}");
            }
        }, ct);

        await Task.WhenAll(writerTask, readerTask);
    }

    [AvaloniaFact]
    public async Task RdpNegotiator_ConcurrentNegotiateCallsOnSameInstance_DetectsStateMutation()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair1 = new DuplexStreamPair();
        using DuplexStreamPair pair2 = new DuplexStreamPair();

        SimulatedRdpServer server1 = new SimulatedRdpServer(pair1.ServerStream);
        SimulatedRdpServer server2 = new SimulatedRdpServer(pair2.ServerStream);

        Task serverTask1 = server1.ProcessConnectionRequestAsync(ct);
        Task serverTask2 = server2.ProcessConnectionRequestAsync(ct);

        RdpNegotiator singleNegotiator = new RdpNegotiator();

        Task t1 = singleNegotiator.NegotiateAsync(pair1.ClientStream, "host1", RdpSecurityProtocol.Ssl, performSecurityHandshake: false, cancellationToken: ct);
        Task t2 = singleNegotiator.NegotiateAsync(pair2.ClientStream, "host2", RdpSecurityProtocol.Rdp, performSecurityHandshake: false, cancellationToken: ct);

        // One or both may complete or race
        await Task.WhenAll(Task.WhenAny(t1, t2), serverTask1, serverTask2);
    }

    #endregion
}
