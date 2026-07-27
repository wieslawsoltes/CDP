namespace CDP.Rdp.Tests.Challenger;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Exceptions;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Tests.Fixtures;
using Xunit;

public class Challenger4EmpiricalTests
{
    private class ThrowOnWriteStream : Stream
    {
        private readonly bool _throwOnWrite;
        private readonly bool _throwOnFlush;

        public ThrowOnWriteStream(bool throwOnWrite = true, bool throwOnFlush = false)
        {
            _throwOnWrite = throwOnWrite;
            _throwOnFlush = throwOnFlush;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            if (_throwOnFlush) throw new IOException("Simulated write flush error.");
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_throwOnFlush) return Task.FromException(new IOException("Simulated write flush error."));
            return Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_throwOnWrite) throw new IOException("Simulated write error.");
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_throwOnWrite) return Task.FromException(new IOException("Simulated write error."));
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_throwOnWrite) return ValueTask.FromException(new IOException("Simulated write error."));
            return ValueTask.CompletedTask;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private static async Task ReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.Slice(totalRead), ct).ConfigureAwait(false);
            if (read == 0) break;
            totalRead += read;
        }
    }

    #region 1. RdpNegotiator Exception Safety Tests

    [Fact]
    public async Task NegotiateAsync_StreamWriteFails_StateTransitionsToFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        using Stream stream = new ThrowOnWriteStream(throwOnWrite: true, throwOnFlush: false);
        RdpNegotiator negotiator = new RdpNegotiator();

        await Assert.ThrowsAsync<IOException>(() =>
            negotiator.NegotiateAsync(stream, "localhost", cancellationToken: ct));

        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
    }

    [Fact]
    public async Task NegotiateAsync_StreamFlushFails_StateTransitionsToFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        using Stream stream = new ThrowOnWriteStream(throwOnWrite: false, throwOnFlush: true);
        RdpNegotiator negotiator = new RdpNegotiator();

        await Assert.ThrowsAsync<IOException>(() =>
            negotiator.NegotiateAsync(stream, "localhost", cancellationToken: ct));

        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task NegotiateAsync_StreamReadDropMidTpktHeader_StateTransitionsToFailed(int bytesReturnedBeforeEof)
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] req = new byte[1024];
                await ReadExactAsync(pair.ServerStream, req.AsMemory(0, 4), ct);
                if (bytesReturnedBeforeEof > 0)
                {
                    byte[] partialTpkt = new byte[] { 0x03, 0x00, 0x00, 0x13 };
                    await pair.ServerStream.WriteAsync(partialTpkt.AsMemory(0, bytesReturnedBeforeEof), ct);
                }
                pair.ServerStream.Close();
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        await Assert.ThrowsAsync<IOException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", performSecurityHandshake: false, cancellationToken: ct));

        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
        try { await serverTask; } catch { }
    }

    [Fact]
    public async Task NegotiateAsync_StreamReadDropMidPayload_StateTransitionsToFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] req = new byte[1024];
                await ReadExactAsync(pair.ServerStream, req.AsMemory(0, 4), ct);
                byte[] partialResponse = new byte[] { 0x03, 0x00, 0x00, 0x13, 0x0E, 0xD0, 0x00, 0x00, 0x12 };
                await pair.ServerStream.WriteAsync(partialResponse, ct);
                pair.ServerStream.Close();
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        await Assert.ThrowsAsync<IOException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", performSecurityHandshake: false, cancellationToken: ct));

        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
        try { await serverTask; } catch { }
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x01)]
    [InlineData(0x02)]
    [InlineData(0x04)]
    [InlineData(0xFF)]
    public async Task NegotiateAsync_InvalidTpktVersion_StateTransitionsToFailed(byte invalidVersion)
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] req = new byte[1024];
                await ReadExactAsync(pair.ServerStream, req.AsMemory(0, 4), ct);
                byte[] response = new byte[] { invalidVersion, 0x00, 0x00, 0x0B, 0x06, 0xD0, 0x00, 0x00, 0x12, 0x34, 0x00 };
                await pair.ServerStream.WriteAsync(response, ct);
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        RdpNegotiationException ex = await Assert.ThrowsAsync<RdpNegotiationException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", cancellationToken: ct));

        Assert.Contains("Invalid TPKT header", ex.Message);
        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
        try { await serverTask; } catch { }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task NegotiateAsync_InvalidTpktPacketLength_StateTransitionsToFailed(ushort invalidLength)
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] req = new byte[1024];
                await ReadExactAsync(pair.ServerStream, req.AsMemory(0, 4), ct);
                byte[] response = new byte[] { 0x03, 0x00, (byte)(invalidLength >> 8), (byte)(invalidLength & 0xFF) };
                await pair.ServerStream.WriteAsync(response, ct);
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        RdpNegotiationException ex = await Assert.ThrowsAsync<RdpNegotiationException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", cancellationToken: ct));

        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
        try { await serverTask; } catch { }
    }

    [Theory]
    [InlineData(0x80)] // DisconnectRequest
    [InlineData(0x00)] // Invalid code
    [InlineData(0x10)] // Invalid code
    public async Task NegotiateAsync_InvalidX224TpduCode_StateTransitionsToFailed(byte invalidTpduCode)
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] req = new byte[1024];
                await ReadExactAsync(pair.ServerStream, req.AsMemory(0, 4), ct);
                byte[] response = new byte[] { 0x03, 0x00, 0x00, 0x0B, 0x06, invalidTpduCode, 0x00, 0x00, 0x12, 0x34, 0x00 };
                await pair.ServerStream.WriteAsync(response, ct);
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        RdpNegotiationException ex = await Assert.ThrowsAsync<RdpNegotiationException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", cancellationToken: ct));

        Assert.Contains("Unexpected X.224 TPDU code received", ex.Message);
        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
        try { await serverTask; } catch { }
    }

    [Fact]
    public async Task NegotiateAsync_NegotiationFailurePdu_StateTransitionsToFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] req = new byte[1024];
                await ReadExactAsync(pair.ServerStream, req.AsMemory(0, 4), ct);
                byte[] failureRsp = new byte[] {
                    0x03, 0x00, 0x00, 0x13,
                    0x0E, 0xD0, 0x00, 0x00, 0x12, 0x34, 0x00,
                    0x03, 0x00, 0x08, 0x00, 0x05, 0x00, 0x00, 0x00
                };
                await pair.ServerStream.WriteAsync(failureRsp, ct);
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        RdpNegotiationException ex = await Assert.ThrowsAsync<RdpNegotiationException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", cancellationToken: ct));

        Assert.Equal(RdpNegotiationFailureCode.HybridRequiredByServer, ex.FailureCode);
        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
        try { await serverTask; } catch { }
    }

    [Fact]
    public async Task NegotiateAsync_SecurityHandshakeFails_StateTransitionsToFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] req = new byte[1024];
                await ReadExactAsync(pair.ServerStream, req.AsMemory(0, 4), ct);
                byte[] sslConfirm = new byte[] {
                    0x03, 0x00, 0x00, 0x13,
                    0x0E, 0xD0, 0x00, 0x00, 0x12, 0x34, 0x00,
                    0x02, 0x00, 0x08, 0x00, 0x01, 0x00, 0x00, 0x00
                };
                await pair.ServerStream.WriteAsync(sslConfirm, ct);
                pair.ServerStream.Close();
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", RdpSecurityProtocol.Ssl, performSecurityHandshake: true, cancellationToken: ct));

        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
        try { await serverTask; } catch { }
    }

    [Fact]
    public async Task NegotiateAsync_CancellationTokenCancelledMidNegotiation_StateTransitionsToFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] buf = new byte[1024];
                await ReadExactAsync(pair.ServerStream, buf.AsMemory(0, 4), cts.Token);
                cts.Cancel();
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", cancellationToken: cts.Token));

        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
        try { await serverTask; } catch { }
    }

    #endregion

    #region 2. Non-Mutating PDU Readers & Mutation Side-Effect Tests

    [Fact]
    public void TpktHeader_TryRead_InvalidVersion_CheckMutationSideEffect()
    {
        byte[] buffer = new byte[] { 0x02, 0x00, 0x00, 0x10 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        int initialPosition = reader.Position;
        bool success = TpktHeader.TryRead(ref reader, out _);

        Assert.False(success);
        // Note: Demonstrates whether reader position was mutated on failure.
        // Non-mutating contract requires initialPosition == reader.Position.
        bool positionPreserved = (initialPosition == reader.Position);
        Assert.False(positionPreserved, "TpktHeader.TryRead currently mutates reader.Position on invalid version failure.");
    }

    [Fact]
    public void TpktHeader_TryRead_InvalidLengthUnderflow_CheckMutationSideEffect()
    {
        byte[] buffer = new byte[] { 0x03, 0x00, 0x00, 0x02 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        int initialPosition = reader.Position;
        bool success = TpktHeader.TryRead(ref reader, out _);

        Assert.False(success);
        bool positionPreserved = (initialPosition == reader.Position);
        Assert.False(positionPreserved, "TpktHeader.TryRead currently mutates reader.Position on invalid length failure.");
    }

    [Fact]
    public void TpktHeader_TryRead_ShortBuffer_DoesNotMutateReaderPosition()
    {
        byte[] buffer = new byte[] { 0x03, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        int initialPosition = reader.Position;
        bool success = TpktHeader.TryRead(ref reader, out _);

        Assert.False(success);
        Assert.Equal(initialPosition, reader.Position);
    }

    [Fact]
    public void X224Header_TryRead_ShortBuffer_DoesNotMutateReaderPosition()
    {
        byte[] buffer = new byte[] { 0x06, 0xE0, 0x00, 0x00, 0x00, 0x00 }; // 6 bytes (needs 7)
        RdpPacketReader reader = new RdpPacketReader(buffer);

        int initialPosition = reader.Position;
        bool success = X224Header.TryRead(ref reader, out _);

        Assert.False(success);
        Assert.Equal(initialPosition, reader.Position);
    }

    [Fact]
    public void RdpNegotiationRequest_TryRead_InvalidType_DoesNotMutateReaderPosition()
    {
        byte[] buffer = new byte[] { 0x02, 0x00, 0x08, 0x00, 0x01, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        int initialPosition = reader.Position;
        bool success = RdpNegotiationRequest.TryRead(ref reader, out _);

        Assert.False(success);
        Assert.Equal(initialPosition, reader.Position);
    }

    [Fact]
    public void RdpNegotiationRequest_TryRead_InvalidLength_DoesNotMutateReaderPosition()
    {
        byte[] buffer = new byte[] { 0x01, 0x00, 0x09, 0x00, 0x01, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        int initialPosition = reader.Position;
        bool success = RdpNegotiationRequest.TryRead(ref reader, out _);

        Assert.False(success);
        Assert.Equal(initialPosition, reader.Position);
    }

    [Fact]
    public void RdpNegotiationResponse_TryRead_InvalidType_DoesNotMutateReaderPosition()
    {
        byte[] buffer = new byte[] { 0x01, 0x00, 0x08, 0x00, 0x01, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        int initialPosition = reader.Position;
        bool success = RdpNegotiationResponse.TryRead(ref reader, out _);

        Assert.False(success);
        Assert.Equal(initialPosition, reader.Position);
    }

    [Fact]
    public void RdpNegotiationResponse_TryRead_InvalidLength_DoesNotMutateReaderPosition()
    {
        byte[] buffer = new byte[] { 0x02, 0x00, 0x07, 0x00, 0x01, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        int initialPosition = reader.Position;
        bool success = RdpNegotiationResponse.TryRead(ref reader, out _);

        Assert.False(success);
        Assert.Equal(initialPosition, reader.Position);
    }

    [Fact]
    public void RdpNegotiationFailure_TryRead_InvalidType_DoesNotMutateReaderPosition()
    {
        byte[] buffer = new byte[] { 0x01, 0x00, 0x08, 0x00, 0x05, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        int initialPosition = reader.Position;
        bool success = RdpNegotiationFailure.TryRead(ref reader, out _);

        Assert.False(success);
        Assert.Equal(initialPosition, reader.Position);
    }

    [Fact]
    public void RdpNegotiationFailure_TryRead_InvalidLength_DoesNotMutateReaderPosition()
    {
        byte[] buffer = new byte[] { 0x03, 0x00, 0x0A, 0x00, 0x05, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        int initialPosition = reader.Position;
        bool success = RdpNegotiationFailure.TryRead(ref reader, out _);

        Assert.False(success);
        Assert.Equal(initialPosition, reader.Position);
    }

    #endregion
}
