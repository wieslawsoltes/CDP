namespace CDP.Rdp.Channels;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// DVC Close PDU (Cmd = 0x04) (MS-RDPEDYC Section 2.2.4).
/// </summary>
public readonly struct DvcClosePdu
{
    public uint ChannelId { get; }
    public byte Priority { get; }

    public DvcClosePdu(uint channelId, byte priority = 0)
    {
        ChannelId = channelId;
        Priority = priority;
    }

    public static bool TryRead(ref RdpPacketReader reader, out DvcClosePdu pdu)
    {
        if (!DvcHeader.TryRead(ref reader, out var header) || header.Command != DvcCommandCode.Close)
        {
            pdu = default;
            return false;
        }

        if (!DvcValueCodec.TryReadValue(ref reader, header.Sp, out uint channelId))
        {
            pdu = default;
            return false;
        }

        pdu = new DvcClosePdu(channelId, header.Priority);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        byte sp = DvcValueCodec.GetRequiredSp(ChannelId);
        var header = new DvcHeader(DvcCommandCode.Close, sp, Priority);
        header.Write(ref writer);
        DvcValueCodec.WriteValue(ref writer, sp, ChannelId);
    }
}
