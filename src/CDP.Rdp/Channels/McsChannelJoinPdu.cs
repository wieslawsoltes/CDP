namespace CDP.Rdp.Channels;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// MCS Channel Join Request (CJrq 0x38) and Channel Join Confirm (CJcf 0x3C) BER PDU serializer (MS-RDPBCGR Section 2.2.1.8).
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

        if (!McsBerHelper.TryReadBerInteger(ref reader, out uint initiator) ||
            !McsBerHelper.TryReadBerInteger(ref reader, out uint channel))
        {
            pdu = default;
            return false;
        }

        pdu = new McsChannelJoinRequest((ushort)initiator, (ushort)channel);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        writer.WriteByte(ChoiceTag);
        McsBerHelper.WriteBerInteger(ref writer, InitiatorId);
        McsBerHelper.WriteBerInteger(ref writer, ChannelId);
    }
}

/// <summary>
/// MCS Channel Join Confirm (CJcf 0x3C) BER PDU (MS-RDPBCGR Section 2.2.1.8).
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
        if (reader.UnreadLength < 7)
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

        if (!McsBerHelper.TryReadBerEnum(ref reader, out byte result) ||
            !McsBerHelper.TryReadBerInteger(ref reader, out uint initiator) ||
            !McsBerHelper.TryReadBerInteger(ref reader, out uint requested) ||
            !McsBerHelper.TryReadBerInteger(ref reader, out uint channel))
        {
            pdu = default;
            return false;
        }

        pdu = new McsChannelJoinConfirm(result, (ushort)initiator, (ushort)requested, (ushort)channel);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        writer.WriteByte(ChoiceTag);
        McsBerHelper.WriteBerEnum(ref writer, Result);
        McsBerHelper.WriteBerInteger(ref writer, InitiatorId);
        McsBerHelper.WriteBerInteger(ref writer, RequestedChannelId);
        McsBerHelper.WriteBerInteger(ref writer, ChannelId);
    }
}

/// <summary>
/// Internal BER encoding helper for MCS PDUs.
/// </summary>
internal static class McsBerHelper
{
    public static bool TryReadBerInteger(ref RdpPacketReader reader, out uint value)
    {
        value = 0;
        if (reader.UnreadLength < 2) return false;

        byte tag = reader.ReadByte();
        if (tag != 0x02) return false; // BER Integer tag

        byte len = reader.ReadByte();
        if (reader.UnreadLength < len) return false;

        uint val = 0;
        for (int i = 0; i < len; i++)
        {
            val = (val << 8) | reader.ReadByte();
        }
        value = val;
        return true;
    }

    public static void WriteBerInteger(ref RdpPacketWriter writer, uint value)
    {
        writer.WriteByte(0x02); // BER Integer tag

        if (value <= 0x7F)
        {
            writer.WriteByte(1);
            writer.WriteByte((byte)value);
        }
        else if (value <= 0x7FFF)
        {
            writer.WriteByte(2);
            writer.WriteByte((byte)((value >> 8) & 0xFF));
            writer.WriteByte((byte)(value & 0xFF));
        }
        else if (value <= 0xFFFF)
        {
            // Pad 0x00 for unsigned 16-bit values with high bit set
            writer.WriteByte(3);
            writer.WriteByte(0x00);
            writer.WriteByte((byte)((value >> 8) & 0xFF));
            writer.WriteByte((byte)(value & 0xFF));
        }
        else if (value <= 0x7FFFFFFF)
        {
            writer.WriteByte(4);
            writer.WriteByte((byte)((value >> 24) & 0xFF));
            writer.WriteByte((byte)((value >> 16) & 0xFF));
            writer.WriteByte((byte)((value >> 8) & 0xFF));
            writer.WriteByte((byte)(value & 0xFF));
        }
        else
        {
            writer.WriteByte(5);
            writer.WriteByte(0x00);
            writer.WriteByte((byte)((value >> 24) & 0xFF));
            writer.WriteByte((byte)((value >> 16) & 0xFF));
            writer.WriteByte((byte)((value >> 8) & 0xFF));
            writer.WriteByte((byte)(value & 0xFF));
        }
    }

    public static bool TryReadBerEnum(ref RdpPacketReader reader, out byte enumVal)
    {
        enumVal = 0;
        if (reader.UnreadLength < 3) return false;

        byte tag = reader.ReadByte();
        if (tag != 0x0A) return false; // BER Enumerated tag

        byte len = reader.ReadByte();
        if (len != 1 || reader.UnreadLength < 1) return false;

        enumVal = reader.ReadByte();
        return true;
    }

    public static void WriteBerEnum(ref RdpPacketWriter writer, byte enumVal)
    {
        writer.WriteByte(0x0A); // BER Enumerated tag
        writer.WriteByte(1);
        writer.WriteByte(enumVal);
    }
}
