namespace CDP.Rdp.Tests.Fixtures;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Protocol;

public enum ServerResponseBehavior
{
    AcceptRequestedProtocol,
    ForceProtocol,
    RejectWithFailure,
    SendMalformedTpktVersion,
    SendTruncatedHeader,
    CloseConnectionImmediately
}

/// <summary>
/// In-memory RDP server endpoint simulator for unit & integration testing.
/// </summary>
public sealed class SimulatedRdpServer
{
    private readonly Stream _serverStream;
    public ServerResponseBehavior Behavior { get; set; } = ServerResponseBehavior.AcceptRequestedProtocol;
    public RdpSecurityProtocol ForcedProtocol { get; set; } = RdpSecurityProtocol.Ssl;
    public RdpNegotiationFailureCode FailureCode { get; set; } = RdpNegotiationFailureCode.HybridRequiredByServer;
    public byte ResponseFlags { get; set; } = 0x00;

    public RdpNegotiationRequest? ReceivedRequest { get; private set; }
    public X224Header? ReceivedX224Header { get; private set; }
    public TpktHeader? ReceivedTpktHeader { get; private set; }

    public SimulatedRdpServer(Stream serverStream)
    {
        _serverStream = serverStream ?? throw new ArgumentNullException(nameof(serverStream));
    }

    /// <summary>
    /// Reads client Connection Request PDU and transmits configured server response.
    /// </summary>
    public async Task ProcessConnectionRequestAsync(CancellationToken cancellationToken = default)
    {
        byte[] buffer = new byte[1024];
        int bytesRead = 0;

        // Read TPKT header (4 bytes)
        while (bytesRead < 4)
        {
            int r = await _serverStream.ReadAsync(buffer.AsMemory(bytesRead, 4 - bytesRead), cancellationToken).ConfigureAwait(false);
            if (r == 0) throw new InvalidOperationException("Client closed stream prematurely.");
            bytesRead += r;
        }

        RdpPacketReader reader = new RdpPacketReader(buffer.AsSpan(0, 4));
        if (!TpktHeader.TryRead(ref reader, out TpktHeader tpkt))
            throw new InvalidDataException("Invalid TPKT header received by server.");
        ReceivedTpktHeader = tpkt;

        // Read rest of packet
        while (bytesRead < tpkt.PacketLength)
        {
            int r = await _serverStream.ReadAsync(buffer.AsMemory(bytesRead, tpkt.PacketLength - bytesRead), cancellationToken).ConfigureAwait(false);
            if (r == 0) throw new InvalidOperationException("Client stream EOF during packet payload.");
            bytesRead += r;
        }

        int targetLength = tpkt.PacketLength - 4;
        RdpPacketReader payloadReader = new RdpPacketReader(buffer.AsSpan(4, targetLength));

        if (!X224Header.TryRead(ref payloadReader, out X224Header x224))
            throw new InvalidDataException("Invalid X.224 header received by server.");
        ReceivedX224Header = x224;

        if (RdpNegotiationRequest.TryRead(ref payloadReader, out RdpNegotiationRequest req))
        {
            ReceivedRequest = req;
        }

        // Transmit Server Response according to test behavior
        await SendResponseAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SendResponseAsync(CancellationToken cancellationToken)
    {
        switch (Behavior)
        {
            case ServerResponseBehavior.CloseConnectionImmediately:
                _serverStream.Close();
                break;

            case ServerResponseBehavior.SendMalformedTpktVersion:
                byte[] malformed = new byte[] { 0x99, 0x00, 0x00, 0x0B, 0x06, 0xD0, 0x00, 0x00, 0x00, 0x00, 0x00 };
                await _serverStream.WriteAsync(malformed, cancellationToken).ConfigureAwait(false);
                break;

            case ServerResponseBehavior.SendTruncatedHeader:
                byte[] truncated = new byte[] { 0x03, 0x00 };
                await _serverStream.WriteAsync(truncated, cancellationToken).ConfigureAwait(false);
                _serverStream.Close();
                break;

            case ServerResponseBehavior.RejectWithFailure:
                byte[] failurePdu = RdpTestPackets.CreateX224CcFailureBytes(FailureCode);
                await _serverStream.WriteAsync(failurePdu, cancellationToken).ConfigureAwait(false);
                break;

            case ServerResponseBehavior.AcceptRequestedProtocol:
            case ServerResponseBehavior.ForceProtocol:
                RdpSecurityProtocol selected = (Behavior == ServerResponseBehavior.ForceProtocol)
                    ? ForcedProtocol
                    : (ReceivedRequest?.RequestedProtocols ?? RdpSecurityProtocol.Ssl);

                byte[] acceptPdu = RdpTestPackets.CreateX224CcTpduBytes(selected, ResponseFlags);
                await _serverStream.WriteAsync(acceptPdu, cancellationToken).ConfigureAwait(false);
                break;
        }
    }
}
