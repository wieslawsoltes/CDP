namespace CDP.Rdp.Input;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// Helper writer for SlowPath and FastPath Input PDU sequences (MS-RDPBCGR Section 2.2.8.1).
/// </summary>
public static class RdpInputPduWriter
{
    /// <summary>
    /// Writes SlowPath TS_INPUT_HEADER (numEvents 2B, pad2Octets 2B).
    /// </summary>
    public static void WriteSlowPathHeader(ref RdpPacketWriter writer, ushort numEvents)
    {
        writer.WriteUInt16LE(numEvents);
        writer.WriteUInt16LE(0x0000); // pad2Octets
    }

    /// <summary>
    /// Writes FastPath TS_FP_INPUT_PDU header (1 byte header + 1 or 2 byte length).
    /// </summary>
    public static void WriteFastPathHeader(
        ref RdpPacketWriter writer,
        byte numEvents,
        ushort pduLength,
        byte action = 0x00,
        byte securityFlags = 0x00)
    {
        byte header = (byte)(action & 0x03);
        header |= (byte)((numEvents & 0x0F) << 2);
        header |= (byte)((securityFlags & 0x03) << 6);
        writer.WriteByte(header);

        if (pduLength > 0x7F)
        {
            byte lenByte1 = (byte)(0x80 | ((pduLength >> 8) & 0x7F));
            byte lenByte2 = (byte)(pduLength & 0xFF);
            writer.WriteByte(lenByte1);
            writer.WriteByte(lenByte2);
        }
        else
        {
            writer.WriteByte((byte)pduLength);
        }
    }
}
