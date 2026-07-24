namespace CDP.Rdp.Tests.Fixtures;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// Pre-constructed packet byte factory for unit test verification.
/// </summary>
public static class RdpTestPackets
{
    /// <summary>
    /// Generates a valid TPKT + X.224 CR TPDU with RdpNegotiationRequest.
    /// </summary>
    public static byte[] CreateX224CrTpduBytes(RdpSecurityProtocol requestedProtocols, byte flags = 0x00)
    {
        byte[] buffer = new byte[19];
        Span<byte> span = buffer;
        RdpPacketWriter writer = new RdpPacketWriter(span);

        TpktHeader tpkt = new TpktHeader(19);
        tpkt.Write(ref writer);

        X224Header x224 = new X224Header(14, X224TpduCode.ConnectionRequest, 0x0000, 0x1234, 0x00);
        x224.Write(ref writer);

        RdpNegotiationRequest req = new RdpNegotiationRequest(requestedProtocols, flags);
        req.Write(ref writer);

        return buffer;
    }

    /// <summary>
    /// Generates a valid TPKT + X.224 CC TPDU with RdpNegotiationResponse.
    /// </summary>
    public static byte[] CreateX224CcTpduBytes(RdpSecurityProtocol selectedProtocol, byte flags = 0x00)
    {
        byte[] buffer = new byte[19];
        Span<byte> span = buffer;
        RdpPacketWriter writer = new RdpPacketWriter(span);

        TpktHeader tpkt = new TpktHeader(19);
        tpkt.Write(ref writer);

        X224Header x224 = new X224Header(14, X224TpduCode.ConnectionConfirm, 0x1234, 0x5678, 0x00);
        x224.Write(ref writer);

        RdpNegotiationResponse rsp = new RdpNegotiationResponse(selectedProtocol, flags);
        rsp.Write(ref writer);

        return buffer;
    }

    /// <summary>
    /// Generates a valid TPKT + X.224 CC TPDU with RdpNegotiationFailure.
    /// </summary>
    public static byte[] CreateX224CcFailureBytes(RdpNegotiationFailureCode failureCode)
    {
        byte[] buffer = new byte[19];
        Span<byte> span = buffer;
        RdpPacketWriter writer = new RdpPacketWriter(span);

        TpktHeader tpkt = new TpktHeader(19);
        tpkt.Write(ref writer);

        X224Header x224 = new X224Header(14, X224TpduCode.ConnectionConfirm, 0x1234, 0x5678, 0x00);
        x224.Write(ref writer);

        RdpNegotiationFailure fail = new RdpNegotiationFailure(failureCode);
        fail.Write(ref writer);

        return buffer;
    }
}
