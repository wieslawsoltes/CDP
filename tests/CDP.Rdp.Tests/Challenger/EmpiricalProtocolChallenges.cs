namespace CDP.Rdp.Tests.Challenger;

using System;
using System.IO;
using System.Threading.Tasks;
using CDP.Rdp.Exceptions;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Tests.Fixtures;

public class EmpiricalProtocolChallenges
{
    #region RdpPacketReader Edge Cases

    [Fact]
    public void RdpPacketReader_ZeroByteSpan_ThrowsOnReadOperations()
    {
        RdpPacketReader reader = new RdpPacketReader(ReadOnlySpan<byte>.Empty);

        Assert.Equal(0, reader.Length);
        Assert.Equal(0, reader.Position);
        Assert.Equal(0, reader.UnreadLength);

        bool t1 = false; try { reader.ReadByte(); } catch (InvalidOperationException) { t1 = true; } Assert.True(t1);
        bool t2 = false; try { reader.ReadUInt16BE(); } catch (InvalidOperationException) { t2 = true; } Assert.True(t2);
        bool t3 = false; try { reader.ReadUInt16LE(); } catch (InvalidOperationException) { t3 = true; } Assert.True(t3);
        bool t4 = false; try { reader.ReadUInt32BE(); } catch (InvalidOperationException) { t4 = true; } Assert.True(t4);
        bool t5 = false; try { reader.ReadUInt32LE(); } catch (InvalidOperationException) { t5 = true; } Assert.True(t5);
        bool t6 = false; try { reader.ReadSpan(1); } catch (InvalidOperationException) { t6 = true; } Assert.True(t6);
    }

    [Fact]
    public void RdpPacketReader_ReadSpanZero_OnEmptyBuffer_Succeeds()
    {
        RdpPacketReader reader = new RdpPacketReader(ReadOnlySpan<byte>.Empty);
        ReadOnlySpan<byte> emptySlice = reader.ReadSpan(0);

        Assert.Equal(0, emptySlice.Length);
        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public void RdpPacketReader_AdvanceZero_Succeeds()
    {
        byte[] data = new byte[] { 0x01, 0x02 };
        RdpPacketReader reader = new RdpPacketReader(data);

        reader.Advance(0);
        Assert.Equal(0, reader.Position);
        Assert.Equal(2, reader.UnreadLength);
    }

    [Fact]
    public void RdpPacketReader_AdvancePastEnd_ThrowsInvalidOperationException()
    {
        byte[] data = new byte[] { 0x01, 0x02 };
        RdpPacketReader reader = new RdpPacketReader(data);

        bool threw = false;
        try
        {
            reader.Advance(3);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert.True(threw);
    }

    [Fact]
    public void RdpPacketReader_NegativeAdvance_RewindsOffset()
    {
        byte[] data = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        RdpPacketReader reader = new RdpPacketReader(data);

        reader.Advance(3);
        Assert.Equal(3, reader.Position);

        // Note: UnreadLength < count check (1 < -1) evaluates to false when passing negative integer.
        // Therefore Advance(-1) rewinds position.
        reader.Advance(-1);
        Assert.Equal(2, reader.Position);
        Assert.Equal(0x30, reader.ReadByte());
    }

    [Fact]
    public void RdpPacketReader_BoundaryRead_ReadsExactCapacityThenThrows()
    {
        byte[] data = new byte[] { 0x01, 0x02, 0x03 };
        RdpPacketReader reader = new RdpPacketReader(data);

        ReadOnlySpan<byte> slice = reader.ReadSpan(3);
        Assert.Equal(3, slice.Length);
        Assert.Equal(0, reader.UnreadLength);

        bool threw = false;
        try
        {
            reader.ReadByte();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert.True(threw);
    }

    #endregion

    #region RdpPacketWriter Edge Cases

    [Fact]
    public void RdpPacketWriter_ZeroByteSpan_ThrowsOnWriteOperations()
    {
        RdpPacketWriter writer = new RdpPacketWriter(Span<byte>.Empty);

        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(0, writer.RemainingCapacity);

        bool threwByte = false; try { writer.WriteByte(0xFF); } catch (InvalidOperationException) { threwByte = true; } Assert.True(threwByte);
        bool threwU16BE = false; try { writer.WriteUInt16BE(0x1234); } catch (InvalidOperationException) { threwU16BE = true; } Assert.True(threwU16BE);
        bool threwU16LE = false; try { writer.WriteUInt16LE(0x1234); } catch (InvalidOperationException) { threwU16LE = true; } Assert.True(threwU16LE);
        bool threwU32BE = false; try { writer.WriteUInt32BE(0x12345678); } catch (InvalidOperationException) { threwU32BE = true; } Assert.True(threwU32BE);
        bool threwU32LE = false; try { writer.WriteUInt32LE(0x12345678); } catch (InvalidOperationException) { threwU32LE = true; } Assert.True(threwU32LE);
        bool threwSpan = false; try { writer.WriteSpan(new byte[1]); } catch (InvalidOperationException) { threwSpan = true; } Assert.True(threwSpan);
    }

    [Fact]
    public void RdpPacketWriter_WriteZeroLengthSpan_OnEmptyBuffer_Succeeds()
    {
        RdpPacketWriter writer = new RdpPacketWriter(Span<byte>.Empty);
        writer.WriteSpan(ReadOnlySpan<byte>.Empty);

        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void RdpPacketWriter_CapacityOverflow_ThrowsInvalidOperationException()
    {
        byte[] buffer = new byte[3];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        writer.WriteByte(0x01);
        writer.WriteUInt16BE(0x0203);
        Assert.Equal(3, writer.WrittenCount);
        Assert.Equal(0, writer.RemainingCapacity);

        bool threw = false;
        try { writer.WriteByte(0x04); } catch (InvalidOperationException) { threw = true; }
        Assert.True(threw);
    }

    #endregion

    #region TpktHeader Parser Edge Cases

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x03 })]
    [InlineData(new byte[] { 0x03, 0x00 })]
    [InlineData(new byte[] { 0x03, 0x00, 0x00 })]
    public void TpktHeader_TryRead_ShortBuffers_ReturnsFalse(byte[] buffer)
    {
        RdpPacketReader reader = new RdpPacketReader(buffer);
        bool success = TpktHeader.TryRead(ref reader, out TpktHeader header);

        Assert.False(success);
        Assert.Equal(default, header);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x01)]
    [InlineData(0x02)]
    [InlineData(0x04)]
    [InlineData(0xFF)]
    public void TpktHeader_TryRead_InvalidVersions_ReturnsFalse(byte invalidVersion)
    {
        byte[] buffer = new byte[] { invalidVersion, 0x00, 0x00, 0x0A };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = TpktHeader.TryRead(ref reader, out TpktHeader header);
        Assert.False(success);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TpktHeader_TryRead_LengthUnderflow_ReturnsFalse(ushort invalidLength)
    {
        byte[] buffer = new byte[4];
        buffer[0] = 0x03;
        buffer[1] = 0x00;
        buffer[2] = (byte)(invalidLength >> 8);
        buffer[3] = (byte)(invalidLength & 0xFF);

        RdpPacketReader reader = new RdpPacketReader(buffer);
        bool success = TpktHeader.TryRead(ref reader, out TpktHeader header);

        Assert.False(success);
    }

    [Fact]
    public void TpktHeader_TryRead_MinimumValidLength_ReturnsTrue()
    {
        byte[] buffer = new byte[] { 0x03, 0x00, 0x00, 0x04 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = TpktHeader.TryRead(ref reader, out TpktHeader header);

        Assert.True(success);
        Assert.Equal(0x03, header.Version);
        Assert.Equal(0x00, header.Reserved);
        Assert.Equal(4, header.PacketLength);
    }

    [Fact]
    public void TpktHeader_TryRead_MaximumPacketLength_ReturnsTrue()
    {
        byte[] buffer = new byte[] { 0x03, 0x00, 0xFF, 0xFF };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = TpktHeader.TryRead(ref reader, out TpktHeader header);

        Assert.True(success);
        Assert.Equal(65535, header.PacketLength);
    }

    [Fact]
    public void TpktHeader_TryRead_NonZeroReservedByte_PreservesReservedByte()
    {
        byte[] buffer = new byte[] { 0x03, 0xAA, 0x00, 0x10 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = TpktHeader.TryRead(ref reader, out TpktHeader header);

        Assert.True(success);
        Assert.Equal(0xAA, header.Reserved);
        Assert.Equal(16, header.PacketLength);
    }

    #endregion

    #region X224Header Parser Edge Cases

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x06, 0xE0, 0x00, 0x00, 0x00, 0x00 })] // 6 bytes (less than 7)
    public void X224Header_TryRead_BufferLessThan7Bytes_ReturnsFalse(byte[] buffer)
    {
        RdpPacketReader reader = new RdpPacketReader(buffer);
        bool success = X224Header.TryRead(ref reader, out X224Header header);

        Assert.False(success);
        Assert.Equal(default, header);
    }

    [Fact]
    public void X224Header_TryRead_UnknownTpduCode_ParsesWithoutException()
    {
        byte[] buffer = new byte[] { 0x06, 0x70, 0x00, 0x00, 0x11, 0x22, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = X224Header.TryRead(ref reader, out X224Header header);

        Assert.True(success);
        Assert.Equal(6, header.LengthIndicator);
        Assert.Equal((X224TpduCode)0x70, header.Code);
        Assert.Equal(0x0000, header.DstReference);
        Assert.Equal(0x1122, header.SrcReference);
    }

    [Fact]
    public void X224Header_TryRead_PreservesClassAndOption()
    {
        byte[] buffer = new byte[] { 0x0E, 0xE0, 0x00, 0x00, 0x00, 0x00, 0x0F };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = X224Header.TryRead(ref reader, out X224Header header);

        Assert.True(success);
        Assert.Equal(0x0F, header.ClassAndOption);
    }

    #endregion

    #region RdpNegotiationPdu Edge Cases

    [Fact]
    public void NegotiationRequest_TryRead_BufferLessThan8Bytes_ReturnsFalse()
    {
        byte[] buffer = new byte[] { 0x01, 0x00, 0x08, 0x00, 0x03, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = RdpNegotiationRequest.TryRead(ref reader, out RdpNegotiationRequest req);
        Assert.False(success);
    }

    [Fact]
    public void NegotiationRequest_TryRead_WrongType_ReturnsFalse()
    {
        byte[] buffer = new byte[] { 0x02, 0x00, 0x08, 0x00, 0x03, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = RdpNegotiationRequest.TryRead(ref reader, out RdpNegotiationRequest req);
        Assert.False(success);
    }

    [Fact]
    public void NegotiationRequest_TryRead_InvalidLengthField_ReturnsFalse()
    {
        byte[] buffer = new byte[] { 0x01, 0x00, 0x09, 0x00, 0x03, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = RdpNegotiationRequest.TryRead(ref reader, out RdpNegotiationRequest req);
        Assert.False(success);
    }

    [Fact]
    public void NegotiationRequest_TryRead_UnknownSecurityFlags_ParsesValue()
    {
        uint unknownFlags = 0x80000010u;
        byte[] buffer = new byte[] { 0x01, 0x05, 0x08, 0x00, 0x10, 0x00, 0x00, 0x80 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = RdpNegotiationRequest.TryRead(ref reader, out RdpNegotiationRequest req);

        Assert.True(success);
        Assert.Equal(0x05, req.Flags);
        Assert.Equal((RdpSecurityProtocol)unknownFlags, req.RequestedProtocols);
    }

    [Fact]
    public void NegotiationResponse_TryRead_UnknownSelectedProtocol_ParsesValue()
    {
        uint unknownProtocol = 0x00000099u;
        byte[] buffer = new byte[] { 0x02, 0xFF, 0x08, 0x00, 0x99, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = RdpNegotiationResponse.TryRead(ref reader, out RdpNegotiationResponse rsp);

        Assert.True(success);
        Assert.Equal(0xFF, rsp.Flags);
        Assert.Equal((RdpSecurityProtocol)unknownProtocol, rsp.SelectedProtocol);
    }

    [Fact]
    public void NegotiationFailure_TryRead_UnknownFailureCode_ParsesValue()
    {
        uint unknownFailureCode = 0x00000077u;
        byte[] buffer = new byte[] { 0x03, 0x00, 0x08, 0x00, 0x77, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(buffer);

        bool success = RdpNegotiationFailure.TryRead(ref reader, out RdpNegotiationFailure fail);

        Assert.True(success);
        Assert.Equal((RdpNegotiationFailureCode)unknownFailureCode, fail.FailureCode);
    }

    #endregion

    #region RdpNegotiator Machine Challenges

    [Fact]
    public async Task NegotiateAsync_NullStream_ThrowsArgumentNullException()
    {
        RdpNegotiator negotiator = new RdpNegotiator();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            negotiator.NegotiateAsync(null!, "localhost"));
    }

    [Fact]
    public async Task NegotiateAsync_ZeroByteServerResponse_ThrowsIOException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        pair.ServerStream.Close(); // Immediate EOF

        RdpNegotiator negotiator = new RdpNegotiator();
        await Assert.ThrowsAsync<IOException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", cancellationToken: ct));

        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
    }

    [Fact]
    public async Task NegotiateAsync_ServerTpktLengthEqualsFour_ThrowsRdpNegotiationException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] buf = new byte[128];
                await ReadExactAsync(pair.ServerStream, buf, 4, ct);
                byte[] response = new byte[] { 0x03, 0x00, 0x00, 0x04 };
                await pair.ServerStream.WriteAsync(response, ct);
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        RdpNegotiationException ex = await Assert.ThrowsAsync<RdpNegotiationException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", cancellationToken: ct));

        Assert.Contains("Invalid X.224 header", ex.Message);
        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
        try { await serverTask; } catch { }
    }

    [Fact]
    public async Task NegotiateAsync_ServerReturnsDataTpduInsteadOfConfirm_ThrowsRdpNegotiationException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] buf = new byte[128];
                await ReadExactAsync(pair.ServerStream, buf, 4, ct);
                byte[] response = new byte[] { 0x03, 0x00, 0x00, 0x0B, 0x06, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00 };
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
    public async Task NegotiateAsync_LegacyServer_NoNegotiationResponsePdu_NegotiatesPlainRdp()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] buf = new byte[128];
                await ReadExactAsync(pair.ServerStream, buf, 4, ct);
                byte[] legacyConfirm = new byte[] { 0x03, 0x00, 0x00, 0x0B, 0x06, 0xD0, 0x00, 0x00, 0x12, 0x34, 0x00 };
                await pair.ServerStream.WriteAsync(legacyConfirm, ct);
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        IRdpSecurityTransport transport = await negotiator.NegotiateAsync(
            pair.ClientStream,
            "localhost",
            performSecurityHandshake: false,
            cancellationToken: ct);

        Assert.NotNull(transport);
        Assert.Equal(RdpSecurityProtocol.Rdp, transport.Protocol);
        Assert.Equal(RdpSecurityProtocol.Rdp, negotiator.SelectedProtocol);
        Assert.Equal(RdpNegotiationState.Connected, negotiator.State);
        try { await serverTask; } catch { }
    }

    [Fact]
    public async Task NegotiateAsync_ServerReturnsUnknownSelectedProtocol_FallsBackToPlainTransport()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();

        Task serverTask = Task.Run(async () =>
        {
            try
            {
                byte[] buf = new byte[128];
                await ReadExactAsync(pair.ServerStream, buf, 4, ct);
                byte[] rsp = new byte[] {
                    0x03, 0x00, 0x00, 0x13, // TPKT
                    0x0E, 0xD0, 0x00, 0x00, 0x12, 0x34, 0x00, // X.224 CC
                    0x02, 0x00, 0x08, 0x00, 0x99, 0x00, 0x00, 0x00 // RDP_NEG_RSP
                };
                await pair.ServerStream.WriteAsync(rsp, ct);
            }
            catch { }
        }, ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        IRdpSecurityTransport transport = await negotiator.NegotiateAsync(
            pair.ClientStream,
            "localhost",
            performSecurityHandshake: false,
            cancellationToken: ct);

        Assert.NotNull(transport);
        Assert.IsType<PlainRdpSecurityTransport>(transport);
        Assert.Equal((RdpSecurityProtocol)0x99, negotiator.SelectedProtocol);
        Assert.Equal(RdpNegotiationState.Connected, negotiator.State);
        try { await serverTask; } catch { }
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int count, System.Threading.CancellationToken ct)
    {
        int read = 0;
        while (read < count)
        {
            int r = await stream.ReadAsync(buffer.AsMemory(read, count - read), ct);
            if (r == 0) break;
            read += r;
        }
        return read;
    }

    #endregion
}
