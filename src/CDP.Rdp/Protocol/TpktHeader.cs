namespace CDP.Rdp.Protocol;

using System;

/// <summary>
/// TPKT Packet Header (RFC 1006).
/// </summary>
public readonly struct TpktHeader
{
    public const byte ExpectedVersion = 0x03;
    public const int HeaderLength = 4;

    public byte Version { get; }
    public byte Reserved { get; }
    public ushort PacketLength { get; }

    public TpktHeader(ushort packetLength)
    {
        Version = ExpectedVersion;
        Reserved = 0x00;
        PacketLength = packetLength;
    }

    public TpktHeader(byte version, byte reserved, ushort packetLength)
    {
        Version = version;
        Reserved = reserved;
        PacketLength = packetLength;
    }

    public static bool TryRead(ref RdpPacketReader reader, out TpktHeader header)
    {
        if (reader.UnreadLength < HeaderLength)
        {
            header = default;
            return false;
        }

        byte version = reader.ReadByte();
        byte reserved = reader.ReadByte();
        ushort length = reader.ReadUInt16BE();

        if (version != ExpectedVersion || length < HeaderLength)
        {
            header = default;
            return false;
        }

        header = new TpktHeader(version, reserved, length);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        writer.WriteByte(Version);
        writer.WriteByte(Reserved);
        writer.WriteUInt16BE(PacketLength);
    }
}
