namespace CDP.Rdp.Protocol;

using System;

/// <summary>
/// RDP Security Protocol Flags (MS-RDPBCGR 2.2.1.1.1).
/// </summary>
[Flags]
public enum RdpSecurityProtocol : uint
{
    Rdp = 0x00000000,
    Ssl = 0x00000001,
    Hybrid = 0x00000002,
    RdsTls = 0x00000004,
    HybridEx = 0x00000008
}

/// <summary>
/// RDP Negotiation Message Types.
/// </summary>
public enum RdpNegotiationType : byte
{
    Request = 0x01,
    Response = 0x02,
    Failure = 0x03
}

/// <summary>
/// RDP Negotiation Failure Codes.
/// </summary>
public enum RdpNegotiationFailureCode : uint
{
    SslRequiredByServer = 0x00000001,
    SslNotAllowedByServer = 0x00000002,
    SslCertNotOnServer = 0x00000003,
    InconsistentFlags = 0x00000004,
    HybridRequiredByServer = 0x00000005,
    SslWithUserAuthRequiredByServer = 0x00000006
}

/// <summary>
/// RDP Negotiation Request (RDP_NEG_REQ).
/// </summary>
public readonly struct RdpNegotiationRequest
{
    public const int PduLength = 8;
    public RdpNegotiationType Type => RdpNegotiationType.Request;
    public byte Flags { get; }
    public ushort Length => PduLength;
    public RdpSecurityProtocol RequestedProtocols { get; }

    public RdpNegotiationRequest(RdpSecurityProtocol requestedProtocols, byte flags = 0x00)
    {
        Flags = flags;
        RequestedProtocols = requestedProtocols;
    }

    public static bool TryRead(ref RdpPacketReader reader, out RdpNegotiationRequest request)
    {
        if (reader.UnreadLength < PduLength)
        {
            request = default;
            return false;
        }

        RdpPacketReader tempReader = reader;
        byte type = tempReader.ReadByte();
        byte flags = tempReader.ReadByte();
        ushort length = tempReader.ReadUInt16LE();
        uint protocols = tempReader.ReadUInt32LE();

        if (type != (byte)RdpNegotiationType.Request || length != PduLength)
        {
            request = default;
            return false;
        }

        reader = tempReader;
        request = new RdpNegotiationRequest((RdpSecurityProtocol)protocols, flags);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        writer.WriteByte((byte)Type);
        writer.WriteByte(Flags);
        writer.WriteUInt16LE(Length);
        writer.WriteUInt32LE((uint)RequestedProtocols);
    }
}

/// <summary>
/// RDP Negotiation Response (RDP_NEG_RSP).
/// </summary>
public readonly struct RdpNegotiationResponse
{
    public const int PduLength = 8;
    public RdpNegotiationType Type => RdpNegotiationType.Response;
    public byte Flags { get; }
    public ushort Length => PduLength;
    public RdpSecurityProtocol SelectedProtocol { get; }

    public RdpNegotiationResponse(RdpSecurityProtocol selectedProtocol, byte flags = 0x00)
    {
        Flags = flags;
        SelectedProtocol = selectedProtocol;
    }

    public static bool TryRead(ref RdpPacketReader reader, out RdpNegotiationResponse response)
    {
        if (reader.UnreadLength < PduLength)
        {
            response = default;
            return false;
        }

        RdpPacketReader tempReader = reader;
        byte type = tempReader.ReadByte();
        byte flags = tempReader.ReadByte();
        ushort length = tempReader.ReadUInt16LE();
        uint selected = tempReader.ReadUInt32LE();

        if (type != (byte)RdpNegotiationType.Response || length != PduLength)
        {
            response = default;
            return false;
        }

        reader = tempReader;
        response = new RdpNegotiationResponse((RdpSecurityProtocol)selected, flags);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        writer.WriteByte((byte)Type);
        writer.WriteByte(Flags);
        writer.WriteUInt16LE(Length);
        writer.WriteUInt32LE((uint)SelectedProtocol);
    }
}

/// <summary>
/// RDP Negotiation Failure (RDP_NEG_FAILURE).
/// </summary>
public readonly struct RdpNegotiationFailure
{
    public const int PduLength = 8;
    public RdpNegotiationType Type => RdpNegotiationType.Failure;
    public byte Flags { get; }
    public ushort Length => PduLength;
    public RdpNegotiationFailureCode FailureCode { get; }

    public RdpNegotiationFailure(RdpNegotiationFailureCode failureCode, byte flags = 0x00)
    {
        Flags = flags;
        FailureCode = failureCode;
    }

    public static bool TryRead(ref RdpPacketReader reader, out RdpNegotiationFailure failure)
    {
        if (reader.UnreadLength < PduLength)
        {
            failure = default;
            return false;
        }

        RdpPacketReader tempReader = reader;
        byte type = tempReader.ReadByte();
        byte flags = tempReader.ReadByte();
        ushort length = tempReader.ReadUInt16LE();
        uint code = tempReader.ReadUInt32LE();

        if (type != (byte)RdpNegotiationType.Failure || length != PduLength)
        {
            failure = default;
            return false;
        }

        reader = tempReader;
        failure = new RdpNegotiationFailure((RdpNegotiationFailureCode)code, flags);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        writer.WriteByte((byte)Type);
        writer.WriteByte(Flags);
        writer.WriteUInt16LE(Length);
        writer.WriteUInt32LE((uint)FailureCode);
    }
}
