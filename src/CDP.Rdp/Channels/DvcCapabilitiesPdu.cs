namespace CDP.Rdp.Channels;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// DVC Capabilities Request/Response PDU (Cmd = 0x05) (MS-RDPEDYC Section 2.2.1.1 & 2.2.1.2).
/// </summary>
public readonly struct DvcCapabilitiesPdu
{
    public ushort Version { get; }
    public ushort Priority0Charge { get; }
    public ushort Priority1Charge { get; }
    public ushort Priority2Charge { get; }
    public ushort Priority3Charge { get; }

    public DvcCapabilitiesPdu(ushort version)
    {
        Version = version;
        Priority0Charge = 0;
        Priority1Charge = 0;
        Priority2Charge = 0;
        Priority3Charge = 0;
    }

    public DvcCapabilitiesPdu(ushort version, ushort pri0, ushort pri1, ushort pri2, ushort pri3)
    {
        Version = version;
        Priority0Charge = pri0;
        Priority1Charge = pri1;
        Priority2Charge = pri2;
        Priority3Charge = pri3;
    }

    public static bool TryRead(ref RdpPacketReader reader, out DvcCapabilitiesPdu pdu)
    {
        if (!DvcHeader.TryRead(ref reader, out var header) || header.Command != DvcCommandCode.Capabilities)
        {
            pdu = default;
            return false;
        }

        if (reader.UnreadLength < 3) // 1 byte pad + 2 bytes version
        {
            pdu = default;
            return false;
        }

        reader.Advance(1); // pad byte
        ushort version = reader.ReadUInt16LE();

        if (version >= 2 && reader.UnreadLength >= 8)
        {
            ushort pri0 = reader.ReadUInt16LE();
            ushort pri1 = reader.ReadUInt16LE();
            ushort pri2 = reader.ReadUInt16LE();
            ushort pri3 = reader.ReadUInt16LE();
            pdu = new DvcCapabilitiesPdu(version, pri0, pri1, pri2, pri3);
            return true;
        }

        pdu = new DvcCapabilitiesPdu(version);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        var header = new DvcHeader(DvcCommandCode.Capabilities, sp: 0, priority: 0);
        header.Write(ref writer);
        writer.WriteByte(0x00); // pad
        writer.WriteUInt16LE(Version);

        if (Version >= 2)
        {
            writer.WriteUInt16LE(Priority0Charge);
            writer.WriteUInt16LE(Priority1Charge);
            writer.WriteUInt16LE(Priority2Charge);
            writer.WriteUInt16LE(Priority3Charge);
        }
    }
}
