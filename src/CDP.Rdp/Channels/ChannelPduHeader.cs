namespace CDP.Rdp.Channels;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// Static Virtual Channel PDU Header (CHANNEL_PDU_HEADER 8 bytes) (MS-RDPBCGR Section 2.2.6.1).
/// </summary>
public readonly struct ChannelPduHeader
{
    public const int HeaderLength = 8;

    public uint Length { get; }
    public ChannelPduFlags Flags { get; }

    public ChannelPduHeader(uint length, ChannelPduFlags flags)
    {
        Length = length;
        Flags = flags;
    }

    public static bool TryRead(ref RdpPacketReader reader, out ChannelPduHeader header)
    {
        if (reader.UnreadLength < HeaderLength)
        {
            header = default;
            return false;
        }

        uint length = reader.ReadUInt32LE();
        uint flags = reader.ReadUInt32LE();

        header = new ChannelPduHeader(length, (ChannelPduFlags)flags);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        writer.WriteUInt32LE(Length);
        writer.WriteUInt32LE((uint)Flags);
    }
}
