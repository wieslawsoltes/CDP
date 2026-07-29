namespace CDP.Rdp.Channels;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// Codec for DVC variable length Sp fields (1, 2, or 4 bytes UIntLE) (MS-RDPEDYC Section 2.2.1).
/// </summary>
public static class DvcValueCodec
{
    public static bool TryReadValue(ref RdpPacketReader reader, byte sp, out uint value)
    {
        switch (sp)
        {
            case 0:
                if (reader.UnreadLength < 1) { value = 0; return false; }
                value = reader.ReadByte();
                return true;
            case 1:
                if (reader.UnreadLength < 2) { value = 0; return false; }
                value = reader.ReadUInt16LE();
                return true;
            case 2:
                if (reader.UnreadLength < 4) { value = 0; return false; }
                value = reader.ReadUInt32LE();
                return true;
            default:
                value = 0;
                return false;
        }
    }

    public static void WriteValue(ref RdpPacketWriter writer, byte sp, uint value)
    {
        switch (sp)
        {
            case 0: writer.WriteByte((byte)value); break;
            case 1: writer.WriteUInt16LE((ushort)value); break;
            case 2: writer.WriteUInt32LE(value); break;
        }
    }

    public static byte GetRequiredSp(uint value)
    {
        if (value <= byte.MaxValue) return 0;
        if (value <= ushort.MaxValue) return 1;
        return 2;
    }
}
