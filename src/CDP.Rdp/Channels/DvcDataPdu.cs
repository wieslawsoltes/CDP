namespace CDP.Rdp.Channels;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// DVC Data First PDU (Cmd = 0x02) header and reader/writer (MS-RDPEDYC Section 2.2.3.1).
/// </summary>
public readonly struct DvcDataFirstHeader
{
    public uint ChannelId { get; }
    public uint TotalLength { get; }
    public byte Priority { get; }

    public DvcDataFirstHeader(uint channelId, uint totalLength, byte priority = 0)
    {
        ChannelId = channelId;
        TotalLength = totalLength;
        Priority = priority;
    }

    public static bool TryRead(ref RdpPacketReader reader, out DvcDataFirstHeader header)
    {
        if (!DvcHeader.TryRead(ref reader, out var dvcHeader) || dvcHeader.Command != DvcCommandCode.DataFirst)
        {
            header = default;
            return false;
        }

        if (!DvcValueCodec.TryReadValue(ref reader, dvcHeader.Sp, out uint channelId))
        {
            header = default;
            return false;
        }

        // MS-RDPEDYC Section 2.2.3.1: Len field (bits 6-7 of header byte, extracted into dvcHeader.Priority) encodes lenSp
        byte lenSp = dvcHeader.Priority;
        if (!DvcValueCodec.TryReadValue(ref reader, lenSp, out uint totalLength))
        {
            header = default;
            return false;
        }

        header = new DvcDataFirstHeader(channelId, totalLength, dvcHeader.Priority);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        byte sp = DvcValueCodec.GetRequiredSp(ChannelId);
        byte lenSp = DvcValueCodec.GetRequiredSp(TotalLength);
        var header = new DvcHeader(DvcCommandCode.DataFirst, sp, lenSp);
        header.Write(ref writer);
        DvcValueCodec.WriteValue(ref writer, sp, ChannelId);
        DvcValueCodec.WriteValue(ref writer, lenSp, TotalLength);
    }
}

/// <summary>
/// DVC Data PDU (Cmd = 0x03) header and reader/writer (MS-RDPEDYC Section 2.2.3.2).
/// </summary>
public readonly struct DvcDataHeader
{
    public uint ChannelId { get; }
    public byte Priority { get; }

    public DvcDataHeader(uint channelId, byte priority = 0)
    {
        ChannelId = channelId;
        Priority = priority;
    }

    public static bool TryRead(ref RdpPacketReader reader, out DvcDataHeader header)
    {
        if (!DvcHeader.TryRead(ref reader, out var dvcHeader) || dvcHeader.Command != DvcCommandCode.Data)
        {
            header = default;
            return false;
        }

        if (!DvcValueCodec.TryReadValue(ref reader, dvcHeader.Sp, out uint channelId))
        {
            header = default;
            return false;
        }

        header = new DvcDataHeader(channelId, dvcHeader.Priority);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        byte sp = DvcValueCodec.GetRequiredSp(ChannelId);
        var header = new DvcHeader(DvcCommandCode.Data, sp, Priority);
        header.Write(ref writer);
        DvcValueCodec.WriteValue(ref writer, sp, ChannelId);
    }
}
