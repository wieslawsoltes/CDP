namespace CDP.Rdp.Protocol;

using System;
using System.IO;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Exceptions;
using CDP.Rdp.Security;

/// <summary>
/// Connection negotiation state machine.
/// </summary>
public enum RdpNegotiationState
{
    Disconnected,
    SendingConnectionRequest,
    AwaitingConnectionConfirm,
    SecuringHandshake,
    Connected,
    Failed
}

/// <summary>
/// RDP Connection Negotiator executing TPKT/X.224 negotiation exchange over a stream.
/// </summary>
public sealed class RdpNegotiator
{
    public RdpNegotiationState State { get; private set; } = RdpNegotiationState.Disconnected;
    public RdpSecurityProtocol SelectedProtocol { get; private set; } = RdpSecurityProtocol.Rdp;
    public byte ResponseFlags { get; private set; }

    public async Task<IRdpSecurityTransport> NegotiateAsync(
        Stream stream,
        string targetHost,
        RdpSecurityProtocol requestedProtocols = RdpSecurityProtocol.Ssl | RdpSecurityProtocol.Hybrid,
        string? routingCookie = null,
        string? username = null,
        string? password = null,
        string? domain = null,
        bool performSecurityHandshake = false,
        RemoteCertificateValidationCallback? certValidation = null,
        CancellationToken cancellationToken = default)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        try
        {
            State = RdpNegotiationState.SendingConnectionRequest;

            byte[] requestPacket = BuildConnectionRequestPacket(requestedProtocols, routingCookie);
            await stream.WriteAsync(requestPacket, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            State = RdpNegotiationState.AwaitingConnectionConfirm;

            byte[] tpktBuffer = new byte[4];
            await ReadExactAsync(stream, tpktBuffer, 0, 4, cancellationToken).ConfigureAwait(false);

            RdpPacketReader tpktReader = new RdpPacketReader(tpktBuffer);
            if (!TpktHeader.TryRead(ref tpktReader, out TpktHeader tpkt))
            {
                throw new RdpNegotiationException("Invalid TPKT header received from server.");
            }

            int payloadLength = tpkt.PacketLength - 4;
            if (payloadLength < 0)
            {
                throw new RdpNegotiationException($"Invalid TPKT packet length: {tpkt.PacketLength}");
            }

            byte[] payloadBuffer = new byte[payloadLength];
            if (payloadLength > 0)
            {
                await ReadExactAsync(stream, payloadBuffer, 0, payloadLength, cancellationToken).ConfigureAwait(false);
            }

            RdpPacketReader payloadReader = new RdpPacketReader(payloadBuffer);
            if (!X224Header.TryRead(ref payloadReader, out X224Header x224))
            {
                throw new RdpNegotiationException("Invalid X.224 header received from server.");
            }

            if (x224.Code != X224TpduCode.ConnectionConfirm)
            {
                throw new RdpNegotiationException($"Unexpected X.224 TPDU code received: {x224.Code}");
            }

            if (RdpNegotiationResponse.TryRead(ref payloadReader, out RdpNegotiationResponse rsp))
            {
                SelectedProtocol = rsp.SelectedProtocol;
                ResponseFlags = rsp.Flags;
                ValidateSelectedProtocol(requestedProtocols, SelectedProtocol);
            }
            else
            {
                payloadReader = new RdpPacketReader(payloadBuffer);
                payloadReader.Advance(X224Header.BaseHeaderLength);
                if (RdpNegotiationFailure.TryRead(ref payloadReader, out RdpNegotiationFailure fail))
                {
                    throw new RdpNegotiationException(fail.FailureCode, $"RDP server rejected connection with failure code: {fail.FailureCode}");
                }

                SelectedProtocol = RdpSecurityProtocol.Rdp;
                ResponseFlags = 0x00;
                ValidateSelectedProtocol(requestedProtocols, SelectedProtocol);
            }

            State = RdpNegotiationState.SecuringHandshake;

            IRdpSecurityTransport transport = SelectedProtocol switch
            {
                RdpSecurityProtocol.Rdp => new PlainRdpSecurityTransport(stream),
                RdpSecurityProtocol.Ssl => new TlsSecurityTransport(stream, certValidation),
                RdpSecurityProtocol.Hybrid => new CredSspSecurityTransport(stream, username ?? "", password ?? "", domain, certValidation),
                RdpSecurityProtocol.HybridEx => throw new RdpNegotiationException(
                    "The server selected HYBRID_EX, whose Early User Authorization Result exchange is not supported by this client."),
                RdpSecurityProtocol.RdsTls => throw new RdpNegotiationException(
                    "The server selected RDSTLS, which is not supported by this client."),
                _ => throw new RdpNegotiationException(
                    $"The server selected an unsupported security protocol: {SelectedProtocol}.")
            };

            if (performSecurityHandshake)
            {
                await transport.HandshakeAsync(targetHost, cancellationToken).ConfigureAwait(false);
            }

            State = RdpNegotiationState.Connected;
            return transport;
        }
        catch
        {
            State = RdpNegotiationState.Failed;
            throw;
        }
    }

    private static void ValidateSelectedProtocol(
        RdpSecurityProtocol requestedProtocols,
        RdpSecurityProtocol selectedProtocol)
    {
        if (selectedProtocol is not (
                RdpSecurityProtocol.Rdp or
                RdpSecurityProtocol.Ssl or
                RdpSecurityProtocol.Hybrid or
                RdpSecurityProtocol.RdsTls or
                RdpSecurityProtocol.HybridEx))
        {
            throw new RdpNegotiationException(
                $"The server selected an unsupported security protocol: {selectedProtocol}.");
        }

        bool wasRequested = selectedProtocol == RdpSecurityProtocol.Rdp
            ? requestedProtocols == RdpSecurityProtocol.Rdp
            : (requestedProtocols & selectedProtocol) == selectedProtocol;

        if (!wasRequested)
        {
            throw new RdpNegotiationException(
                $"The server selected security protocol {selectedProtocol}, which the client did not request.");
        }
    }

    public static byte[] BuildConnectionRequestPacket(RdpSecurityProtocol requestedProtocols, string? routingCookie = null)
    {
        byte[]? cookieBytes = null;
        if (!string.IsNullOrEmpty(routingCookie))
        {
            cookieBytes = System.Text.Encoding.ASCII.GetBytes($"Cookie: mstshash={routingCookie}\r\n");
        }

        int cookieLength = cookieBytes?.Length ?? 0;
        int x224Li = 6 + cookieLength + RdpNegotiationRequest.PduLength;
        ushort totalLength = (ushort)(4 + 1 + x224Li);

        byte[] buffer = new byte[totalLength];
        Span<byte> span = buffer;
        RdpPacketWriter writer = new RdpPacketWriter(span);

        TpktHeader tpkt = new TpktHeader(totalLength);
        tpkt.Write(ref writer);

        X224Header x224 = new X224Header((byte)x224Li, X224TpduCode.ConnectionRequest, 0x0000, 0x1234, 0x00);
        x224.Write(ref writer);

        if (cookieBytes != null)
        {
            writer.WriteSpan(cookieBytes);
        }

        RdpNegotiationRequest negReq = new RdpNegotiationRequest(requestedProtocols);
        negReq.Write(ref writer);

        return buffer;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new IOException($"Unexpected end of stream. Expected {count} bytes, read {totalRead} bytes.");
            }
            totalRead += read;
        }
    }
}
