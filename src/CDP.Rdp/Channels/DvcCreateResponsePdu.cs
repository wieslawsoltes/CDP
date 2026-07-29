namespace CDP.Rdp.Channels;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// DVC Create Response PDU (Cmd = 0x01) (MS-RDPEDYC Section 2.2.2.2).
/// </summary>
public readonly struct DvcCreateResponsePdu
{
    public uint ChannelId { get; }
    public int CreationStatus { get; }
    public byte Priority { get; }

    public bool IsSuccess => CreationStatus >= 0;

    public DvcCreateResponsePdu(uint channelId, int creationStatus = 0, byte priority = 0)
    {
        ChannelId = channelId;
        CreationStatus = creationStatus;
        Priority = priority;
    }

    public static bool TryRead(ref RdpPacketReader reader, out DvcCreateResponsePdu pdu)
    {
        if (!DvcHeader.TryRead(ref reader, out var header) || header.Command != DvcCommandCode.Create)
        {
            pdu = default;
            return false;
        }

        if (!DvcValueCodec.TryReadValue(ref reader, header.Sp, out uint channelId) || reader.UnreadLength < 4)
        {
            pdu = default;
            return false;
        }

        int creationStatus = (int)reader.ReadUInt32LE();
        pdu = new DvcCreateResponsePdu(channelId, creationStatus, header.Priority);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        byte sp = DvcValueCodec.GetRequiredSp(ChannelId);
        var header = new DvcHeader(DvcCommandCode.Create, sp, Priority);
        header.Write(ref writer);
        DvcValueCodec.WriteValue(ref writer, sp, ChannelId);
        writer.WriteUInt32LE((uint)CreationStatus);
    }
}
