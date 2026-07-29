namespace CDP.Rdp.Protocol;

using System;

/// <summary>
/// X.224 TPDU Codes (ITU-T X.224).
/// </summary>
public enum X224TpduCode : byte
{
    ConnectionRequest = 0xE0,
    ConnectionConfirm = 0xD0,
    DisconnectRequest = 0x80,
    Data = 0xF0
}

/// <summary>
/// X.224 Header structure.
/// </summary>
public readonly struct X224Header
{
    public const int BaseHeaderLength = 7;

    public byte LengthIndicator { get; }
    public X224TpduCode Code { get; }
    public ushort DstReference { get; }
    public ushort SrcReference { get; }
    public byte ClassAndOption { get; }

    public X224Header(byte lengthIndicator, X224TpduCode code, ushort dstReference, ushort srcReference, byte classAndOption = 0x00)
    {
        LengthIndicator = lengthIndicator;
        Code = code;
        DstReference = dstReference;
        SrcReference = srcReference;
        ClassAndOption = classAndOption;
    }

    public static bool TryRead(ref RdpPacketReader reader, out X224Header header)
    {
        if (reader.UnreadLength < BaseHeaderLength)
        {
            header = default;
            return false;
        }

        byte li = reader.ReadByte();
        byte codeByte = reader.ReadByte();
        X224TpduCode code = (X224TpduCode)(codeByte & 0xF0);
        ushort dstRef = reader.ReadUInt16BE();
        ushort srcRef = reader.ReadUInt16BE();
        byte classOpt = reader.ReadByte();

        header = new X224Header(li, code, dstRef, srcRef, classOpt);
        return true;
    }

    public void Write(ref RdpPacketWriter writer)
    {
        writer.WriteByte(LengthIndicator);
        writer.WriteByte((byte)Code);
        writer.WriteUInt16BE(DstReference);
        writer.WriteUInt16BE(SrcReference);
        writer.WriteByte(ClassAndOption);
    }
}
