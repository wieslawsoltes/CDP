namespace CDP.Rdp.Channels;

using System;
using System.Text;
using CDP.Rdp.Protocol;

/// <summary>
/// DVC Create Request PDU (Cmd = 0x01) (MS-RDPEDYC Section 2.2.2.1).
/// </summary>
public readonly struct DvcCreateRequestPdu
{
    public uint ChannelId { get; }
    public string ChannelName { get; }
    public byte Priority { get; }

    public DvcCreateRequestPdu(uint channelId, string channelName, byte priority = 0)
    {
        ChannelId = channelId;
        ChannelName = channelName ?? string.Empty;
        Priority = priority;
    }

    public static bool TryRead(ref RdpPacketReader reader, out DvcCreateRequestPdu pdu)
    {
        if (!DvcHeader.TryRead(ref reader, out var header) || header.Command != DvcCommandCode.Create)
        {
            pdu = default;
            return false;
        }

        if (!DvcValueCodec.TryReadValue(ref reader, header.Sp, out uint channelId))
        {
            pdu = default;
            return false;
        }

        // Read null-terminated string
        int nameStart = reader.Position;
        ReadOnlySpan<byte> remaining = reader.ReadSpan(reader.UnreadLength);
        int nullIndex = remaining.IndexOf((byte)0x00);
        if (nullIndex < 0)
        {
            pdu = default;
            return false;
        }

        string channelName = Encoding.ASCII.GetString(remaining.Slice(0, nullIndex));
        if (string.IsNullOrEmpty(channelName))
        {
            pdu = default;
            return false;
        }

        // Reset reader position to after null terminator
        int bytesConsumed = nullIndex + 1;
        // Adjust reader position: rewind and advance correctly
        reader = new RdpPacketReader(remaining.Slice(bytesConsumed));

        pdu = new DvcCreateRequestPdu(channelId, channelName, header.Priority);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        byte sp = DvcValueCodec.GetRequiredSp(ChannelId);
        var header = new DvcHeader(DvcCommandCode.Create, sp, Priority);
        header.Write(ref writer);
        DvcValueCodec.WriteValue(ref writer, sp, ChannelId);

        byte[] nameBytes = Encoding.ASCII.GetBytes(ChannelName);
        writer.WriteSpan(nameBytes);
        writer.WriteByte(0x00); // null terminator
    }
}
