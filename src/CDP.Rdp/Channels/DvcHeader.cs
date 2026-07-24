namespace CDP.Rdp.Channels;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// Common header byte for Dynamic Virtual Channel PDUs (MS-RDPEDYC Section 2.2.1).
/// </summary>
public readonly struct DvcHeader
{
    public DvcCommandCode Command { get; }
    public byte Sp { get; }       // 0 = 1 byte, 1 = 2 bytes, 2 = 4 bytes
    public byte Priority { get; } // 0..3

    public DvcHeader(DvcCommandCode command, byte sp, byte priority = 0)
    {
        Command = command;
        Sp = sp;
        Priority = priority;
    }

    public static bool TryRead(ref RdpPacketReader reader, out DvcHeader header)
    {
        if (reader.UnreadLength < 1)
        {
            header = default;
            return false;
        }

        byte b = reader.ReadByte();
        DvcCommandCode cmd = (DvcCommandCode)(b & 0x0F);
        byte sp = (byte)((b >> 4) & 0x03);
        byte pri = (byte)((b >> 6) & 0x03);

        header = new DvcHeader(cmd, sp, pri);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        byte b = (byte)((byte)Command & 0x0F);
        b |= (byte)((Sp & 0x03) << 4);
        b |= (byte)((Priority & 0x03) << 6);
        writer.WriteByte(b);
    }
}
