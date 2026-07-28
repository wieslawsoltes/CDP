using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Protocol;

using System;
using CDP.Rdp.Protocol;

public class RdpNegotiationPduTests
{
    [AvaloniaFact]
    public void NegotiationRequest_TryRead_ValidPayload_ParsesProtocols()
    {
        byte[] data = new byte[] { 0x01, 0x00, 0x08, 0x00, 0x03, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(data);

        bool success = RdpNegotiationRequest.TryRead(ref reader, out RdpNegotiationRequest req);

        Assert.True(success);
        Assert.Equal(RdpNegotiationType.Request, req.Type);
        Assert.Equal(0x00, req.Flags);
        Assert.Equal(8, req.Length);
        Assert.Equal(RdpSecurityProtocol.Ssl | RdpSecurityProtocol.Hybrid, req.RequestedProtocols);
    }

    [AvaloniaFact]
    public void NegotiationRequest_Write_ValidStruct_SerializesToSpan()
    {
        byte[] buffer = new byte[8];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        RdpNegotiationRequest req = new RdpNegotiationRequest(RdpSecurityProtocol.Ssl | RdpSecurityProtocol.Hybrid);
        req.Write(ref writer);

        Assert.Equal(8, writer.WrittenCount);
        Assert.Equal(0x01, buffer[0]);
        Assert.Equal(0x00, buffer[1]);
        Assert.Equal(0x08, buffer[2]);
        Assert.Equal(0x00, buffer[3]);
        Assert.Equal(0x03, buffer[4]);
        Assert.Equal(0x00, buffer[5]);
        Assert.Equal(0x00, buffer[6]);
        Assert.Equal(0x00, buffer[7]);
    }

    [AvaloniaFact]
    public void NegotiationResponse_TryRead_ValidPayload_ParsesSelectedProtocol()
    {
        byte[] data = new byte[] { 0x02, 0x01, 0x08, 0x00, 0x01, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(data);

        bool success = RdpNegotiationResponse.TryRead(ref reader, out RdpNegotiationResponse rsp);

        Assert.True(success);
        Assert.Equal(RdpNegotiationType.Response, rsp.Type);
        Assert.Equal(0x01, rsp.Flags);
        Assert.Equal(8, rsp.Length);
        Assert.Equal(RdpSecurityProtocol.Ssl, rsp.SelectedProtocol);
    }

    [AvaloniaFact]
    public void NegotiationResponse_Write_ValidStruct_SerializesToSpan()
    {
        byte[] buffer = new byte[8];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        RdpNegotiationResponse rsp = new RdpNegotiationResponse(RdpSecurityProtocol.Hybrid, 0x02);
        rsp.Write(ref writer);

        Assert.Equal(8, writer.WrittenCount);
        Assert.Equal(0x02, buffer[0]);
        Assert.Equal(0x02, buffer[1]);
        Assert.Equal(0x08, buffer[2]);
        Assert.Equal(0x00, buffer[3]);
        Assert.Equal(0x02, buffer[4]);
    }

    [AvaloniaFact]
    public void NegotiationFailure_TryRead_ValidPayload_ParsesFailureCode()
    {
        byte[] data = new byte[] { 0x03, 0x00, 0x08, 0x00, 0x05, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(data);

        bool success = RdpNegotiationFailure.TryRead(ref reader, out RdpNegotiationFailure fail);

        Assert.True(success);
        Assert.Equal(RdpNegotiationType.Failure, fail.Type);
        Assert.Equal(RdpNegotiationFailureCode.HybridRequiredByServer, fail.FailureCode);
    }

    [AvaloniaFact]
    public void NegotiationFailure_Write_ValidStruct_SerializesToSpan()
    {
        byte[] buffer = new byte[8];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        RdpNegotiationFailure fail = new RdpNegotiationFailure(RdpNegotiationFailureCode.SslRequiredByServer);
        fail.Write(ref writer);

        Assert.Equal(8, writer.WrittenCount);
        Assert.Equal(0x03, buffer[0]);
        Assert.Equal(0x00, buffer[1]);
        Assert.Equal(0x08, buffer[2]);
        Assert.Equal(0x00, buffer[3]);
        Assert.Equal(0x01, buffer[4]);
    }

    [AvaloniaFact]
    public void TryRead_InvalidTypeOrLength_DoesNotMutateReaderPosition()
    {
        // Bad type 0xFF, length 8
        byte[] invalidData = new byte[] { 0xFF, 0x00, 0x08, 0x00, 0x01, 0x00, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(invalidData);
        int initialUnread = reader.UnreadLength;

        bool resultReq = RdpNegotiationRequest.TryRead(ref reader, out _);
        Assert.False(resultReq);
        Assert.Equal(initialUnread, reader.UnreadLength);

        bool resultRsp = RdpNegotiationResponse.TryRead(ref reader, out _);
        Assert.False(resultRsp);
        Assert.Equal(initialUnread, reader.UnreadLength);

        bool resultFail = RdpNegotiationFailure.TryRead(ref reader, out _);
        Assert.False(resultFail);
        Assert.Equal(initialUnread, reader.UnreadLength);
    }
}

