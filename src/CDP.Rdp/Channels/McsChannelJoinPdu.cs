namespace CDP.Rdp.Channels;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// MCS Channel Join Request (CJrq 0x38) and Channel Join Confirm (CJcf 0x3C)
/// aligned-PER PDU serializer (MS-RDPBCGR Section 2.2.1.8).
/// </summary>
public readonly struct McsChannelJoinRequest
{
    public const byte ChoiceTag = 0x38;

    public ushort InitiatorId { get; }
    public ushort ChannelId { get; }

    public McsChannelJoinRequest(ushort initiatorId, ushort channelId)
    {
        InitiatorId = initiatorId;
        ChannelId = channelId;
    }

    public static bool TryRead(ref RdpPacketReader reader, out McsChannelJoinRequest pdu)
    {
        if (reader.UnreadLength < 5)
        {
            pdu = default;
            return false;
        }

        byte tag = reader.ReadByte();
        if (tag != ChoiceTag)
        {
            pdu = default;
            return false;
        }

        ushort initiator = checked((ushort)(reader.ReadUInt16BE() + McsPerHelper.UserIdBase));
        ushort channel = reader.ReadUInt16BE();
        pdu = new McsChannelJoinRequest(initiator, channel);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        writer.WriteByte(ChoiceTag);
        McsPerHelper.WriteUserId(ref writer, InitiatorId);
        writer.WriteUInt16BE(ChannelId);
    }
}

/// <summary>
/// MCS Channel Join Confirm (CJcf 0x3C) aligned-PER PDU (MS-RDPBCGR Section 2.2.1.8).
/// </summary>
public readonly struct McsChannelJoinConfirm
{
    public const byte ChoiceTag = 0x3C;

    public byte Result { get; }
    public ushort InitiatorId { get; }
    public ushort RequestedChannelId { get; }
    public ushort ChannelId { get; }

    public McsChannelJoinConfirm(byte result, ushort initiatorId, ushort requestedChannelId, ushort channelId)
    {
        Result = result;
        InitiatorId = initiatorId;
        RequestedChannelId = requestedChannelId;
        ChannelId = channelId;
    }

    public static bool TryRead(ref RdpPacketReader reader, out McsChannelJoinConfirm pdu)
    {
        if (reader.UnreadLength < 8)
        {
            pdu = default;
            return false;
        }

        byte tag = reader.ReadByte();
        if (tag != ChoiceTag)
        {
            pdu = default;
            return false;
        }

        byte result = reader.ReadByte();
        ushort initiator = checked((ushort)(reader.ReadUInt16BE() + McsPerHelper.UserIdBase));
        ushort requested = reader.ReadUInt16BE();
        ushort channel = reader.ReadUInt16BE();
        pdu = new McsChannelJoinConfirm(result, initiator, requested, channel);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        writer.WriteByte(ChoiceTag);
        writer.WriteByte(Result);
        McsPerHelper.WriteUserId(ref writer, InitiatorId);
        writer.WriteUInt16BE(RequestedChannelId);
        writer.WriteUInt16BE(ChannelId);
    }
}

/// <summary>
/// Internal BER encoding helper for MCS PDUs.
/// </summary>
internal static class McsPerHelper
{
    internal const ushort UserIdBase = 1001;

    public static void WriteUserId(ref RdpPacketWriter writer, ushort userId)
    {
        if (userId < UserIdBase)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        writer.WriteUInt16BE((ushort)(userId - UserIdBase));
    }
}
