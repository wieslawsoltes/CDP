namespace CDP.Rdp.Input;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// Helper reader for SlowPath and FastPath Input PDU sequences (MS-RDPBCGR Section 2.2.8.1).
/// </summary>
public static class RdpInputPduReader
{
    /// <summary>
    /// Reads SlowPath TS_INPUT_HEADER (numEvents 2B, pad2Octets 2B).
    /// </summary>
    public static bool TryReadSlowPathHeader(ref RdpPacketReader reader, out ushort numEvents)
    {
        if (reader.UnreadLength < 4)
        {
            numEvents = 0;
            return false;
        }

        numEvents = reader.ReadUInt16LE();
        reader.Advance(2); // pad2Octets
        return true;
    }

    /// <summary>
    /// Reads FastPath TS_FP_INPUT_PDU header (1 byte header + 1 or 2 byte length).
    /// </summary>
    public static bool TryReadFastPathHeader(
        ref RdpPacketReader reader,
        out byte action,
        out byte numEvents,
        out byte securityFlags,
        out ushort pduLength)
    {
        if (reader.UnreadLength < 2)
        {
            action = 0;
            numEvents = 0;
            securityFlags = 0;
            pduLength = 0;
            return false;
        }

        byte header = reader.ReadByte();
        action = (byte)(header & 0x03);
        numEvents = (byte)((header >> 2) & 0x0F);
        securityFlags = (byte)((header >> 6) & 0x03);

        byte lenByte1 = reader.ReadByte();
        if ((lenByte1 & 0x80) != 0)
        {
            if (reader.UnreadLength < 1)
            {
                pduLength = 0;
                return false;
            }
            byte lenByte2 = reader.ReadByte();
            pduLength = (ushort)(((lenByte1 & 0x7F) << 8) | lenByte2);
        }
        else
        {
            pduLength = lenByte1;
        }

        return true;
    }
}
