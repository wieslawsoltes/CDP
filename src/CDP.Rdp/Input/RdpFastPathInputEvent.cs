namespace CDP.Rdp.Input;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// FastPath Input Event parser and writer (MS-RDPBCGR Section 2.2.8.1.2.2).
/// </summary>
public readonly struct RdpFastPathInputEvent
{
    public FastPathInputEventCode Code { get; }
    public FastPathKeyboardFlags KeyboardFlags { get; }
    public byte KeyCode { get; }
    public ushort UnicodeCode { get; }
    public uint QoeTimestamp { get; }
    public RdpPointerFlags PointerFlags { get; }
    public ushort XPos { get; }
    public ushort YPos { get; }
    public byte ToggleFlags { get; }

    public RdpFastPathInputEvent(FastPathKeyboardFlags keyboardFlags, byte keyCode)
    {
        Code = FastPathInputEventCode.ScanCode;
        KeyboardFlags = keyboardFlags;
        KeyCode = keyCode;
        UnicodeCode = 0;
        QoeTimestamp = 0;
        PointerFlags = default;
        XPos = 0;
        YPos = 0;
        ToggleFlags = 0;
    }

    public RdpFastPathInputEvent(FastPathInputEventCode code, RdpPointerFlags pointerFlags, ushort xPos, ushort yPos)
    {
        Code = code;
        KeyboardFlags = default;
        KeyCode = 0;
        UnicodeCode = 0;
        QoeTimestamp = 0;
        PointerFlags = pointerFlags;
        XPos = xPos;
        YPos = yPos;
        ToggleFlags = 0;
    }

    public RdpFastPathInputEvent(byte toggleFlags)
    {
        Code = FastPathInputEventCode.Sync;
        KeyboardFlags = default;
        KeyCode = 0;
        UnicodeCode = 0;
        QoeTimestamp = 0;
        PointerFlags = default;
        XPos = 0;
        YPos = 0;
        ToggleFlags = toggleFlags;
    }

    public RdpFastPathInputEvent(FastPathInputEventCode code)
    {
        Code = code;
        KeyboardFlags = default;
        KeyCode = 0;
        UnicodeCode = 0;
        QoeTimestamp = 0;
        PointerFlags = default;
        XPos = 0;
        YPos = 0;
        ToggleFlags = 0;
    }

    public RdpFastPathInputEvent(FastPathKeyboardFlags keyboardFlags, ushort unicodeCode)
    {
        Code = FastPathInputEventCode.Unicode;
        KeyboardFlags = keyboardFlags;
        KeyCode = 0;
        UnicodeCode = unicodeCode;
        QoeTimestamp = 0;
        PointerFlags = default;
        XPos = 0;
        YPos = 0;
        ToggleFlags = 0;
    }

    public RdpFastPathInputEvent(uint qoeTimestamp)
    {
        Code = FastPathInputEventCode.QoeTimestamp;
        KeyboardFlags = default;
        KeyCode = 0;
        UnicodeCode = 0;
        QoeTimestamp = qoeTimestamp;
        PointerFlags = default;
        XPos = 0;
        YPos = 0;
        ToggleFlags = 0;
    }

    public RdpFastPathInputEvent(
        FastPathInputEventCode code,
        FastPathKeyboardFlags keyboardFlags,
        byte keyCode,
        RdpPointerFlags pointerFlags,
        ushort xPos,
        ushort yPos,
        byte toggleFlags)
    {
        Code = code;
        KeyboardFlags = keyboardFlags;
        KeyCode = keyCode;
        UnicodeCode = 0;
        QoeTimestamp = 0;
        PointerFlags = pointerFlags;
        XPos = xPos;
        YPos = yPos;
        ToggleFlags = toggleFlags;
    }

    public static bool TryRead(ref RdpPacketReader reader, out RdpFastPathInputEvent fpEvent)
    {
        if (reader.UnreadLength < 1)
        {
            fpEvent = default;
            return false;
        }

        byte header = reader.ReadByte();
        FastPathInputEventCode code = (FastPathInputEventCode)((header >> 5) & 0x07);
        byte flags = (byte)(header & 0x1F);

        switch (code)
        {
            case FastPathInputEventCode.ScanCode:
                if (reader.UnreadLength < 1) { fpEvent = default; return false; }
                byte keyCode = reader.ReadByte();
                fpEvent = new RdpFastPathInputEvent(FastPathInputEventCode.ScanCode, (FastPathKeyboardFlags)flags, keyCode, default, 0, 0, 0);
                return true;

            case FastPathInputEventCode.Mouse:
            case FastPathInputEventCode.MouseX:
                if (reader.UnreadLength < 6) { fpEvent = default; return false; }
                ushort ptrFlags = reader.ReadUInt16LE();
                ushort x = reader.ReadUInt16LE();
                ushort y = reader.ReadUInt16LE();
                fpEvent = new RdpFastPathInputEvent(code, (RdpPointerFlags)ptrFlags, x, y);
                return true;

            case FastPathInputEventCode.ReleaseAll:
                fpEvent = new RdpFastPathInputEvent(FastPathInputEventCode.ReleaseAll);
                return true;

            case FastPathInputEventCode.Sync:
                fpEvent = new RdpFastPathInputEvent(flags);
                return true;

            case FastPathInputEventCode.Unicode:
                if (reader.UnreadLength < 2) { fpEvent = default; return false; }
                fpEvent = new RdpFastPathInputEvent(
                    (FastPathKeyboardFlags)flags,
                    reader.ReadUInt16LE());
                return true;

            case FastPathInputEventCode.QoeTimestamp:
                if (reader.UnreadLength < 4) { fpEvent = default; return false; }
                fpEvent = new RdpFastPathInputEvent(reader.ReadUInt32LE());
                return true;

            default:
                fpEvent = default;
                return false;
        }
    }

    public void Write(ref RdpPacketWriter writer)
    {
        byte header = (byte)(((byte)Code & 0x07) << 5);

        switch (Code)
        {
            case FastPathInputEventCode.ScanCode:
                header |= (byte)((byte)KeyboardFlags & 0x1F);
                writer.WriteByte(header);
                writer.WriteByte(KeyCode);
                break;

            case FastPathInputEventCode.Mouse:
            case FastPathInputEventCode.MouseX:
                writer.WriteByte(header);
                writer.WriteUInt16LE((ushort)PointerFlags);
                writer.WriteUInt16LE(XPos);
                writer.WriteUInt16LE(YPos);
                break;

            case FastPathInputEventCode.ReleaseAll:
                writer.WriteByte(header);
                break;

            case FastPathInputEventCode.Sync:
                header |= (byte)(ToggleFlags & 0x1F);
                writer.WriteByte(header);
                break;

            case FastPathInputEventCode.Unicode:
                header |= (byte)((byte)KeyboardFlags & 0x1F);
                writer.WriteByte(header);
                writer.WriteUInt16LE(UnicodeCode);
                break;

            case FastPathInputEventCode.QoeTimestamp:
                writer.WriteByte(header);
                writer.WriteUInt32LE(QoeTimestamp);
                break;

            default:
                writer.WriteByte(header);
                break;
        }
    }
}
