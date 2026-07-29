namespace CDP.Rdp.Input;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// SlowPath 12-byte Input Event container and reader/writer (MS-RDPBCGR Section 2.2.8.1.1.3.1.1).
/// </summary>
public readonly struct RdpInputEvent
{
    public const int EventLength = 12;

    public uint EventTime { get; }
    public RdpInputMessageType MessageType { get; }
    public RdpKeyboardEvent KeyboardEvent { get; }
    public RdpMouseEvent MouseEvent { get; }
    public RdpSyncEvent SyncEvent { get; }

    public RdpInputEvent(uint eventTime, RdpKeyboardEvent keyboardEvent)
    {
        EventTime = eventTime;
        MessageType = keyboardEvent.IsVirtualKey ? RdpInputMessageType.VkCode : RdpInputMessageType.ScanCode;
        KeyboardEvent = keyboardEvent;
        MouseEvent = default;
        SyncEvent = default;
    }

    public RdpInputEvent(uint eventTime, RdpMouseEvent mouseEvent, bool isExtendedMouse = false)
    {
        EventTime = eventTime;
        MessageType = isExtendedMouse ? RdpInputMessageType.MouseX : RdpInputMessageType.Mouse;
        KeyboardEvent = default;
        MouseEvent = mouseEvent;
        SyncEvent = default;
    }

    public RdpInputEvent(uint eventTime, RdpSyncEvent syncEvent)
    {
        EventTime = eventTime;
        MessageType = RdpInputMessageType.Sync;
        KeyboardEvent = default;
        MouseEvent = default;
        SyncEvent = syncEvent;
    }

    public RdpInputEvent(
        uint eventTime,
        RdpInputMessageType messageType,
        RdpKeyboardEvent keyboardEvent,
        RdpMouseEvent mouseEvent,
        RdpSyncEvent syncEvent)
    {
        EventTime = eventTime;
        MessageType = messageType;
        KeyboardEvent = keyboardEvent;
        MouseEvent = mouseEvent;
        SyncEvent = syncEvent;
    }

    public static bool TryRead(ref RdpPacketReader reader, out RdpInputEvent inputEvent)
    {
        if (reader.UnreadLength < EventLength)
        {
            inputEvent = default;
            return false;
        }

        RdpPacketReader localReader = reader;
        uint time = localReader.ReadUInt32LE();
        ushort typeRaw = localReader.ReadUInt16LE();

        RdpInputMessageType type = (RdpInputMessageType)typeRaw;

        switch (type)
        {
            case RdpInputMessageType.ScanCode:
            case RdpInputMessageType.VkCode:
            case RdpInputMessageType.Unicode:
                ushort kbFlags = localReader.ReadUInt16LE();
                ushort code = localReader.ReadUInt16LE();
                localReader.Advance(2); // pad2Octets
                var kbEvent = new RdpKeyboardEvent(time, (RdpKeyboardFlags)kbFlags, code, type == RdpInputMessageType.VkCode);
                inputEvent = new RdpInputEvent(time, type, kbEvent, default, default);
                reader = localReader;
                return true;

            case RdpInputMessageType.Mouse:
            case RdpInputMessageType.MouseX:
                ushort ptrFlags = localReader.ReadUInt16LE();
                ushort x = localReader.ReadUInt16LE();
                ushort y = localReader.ReadUInt16LE();
                var mouseEvent = new RdpMouseEvent(time, (RdpPointerFlags)ptrFlags, x, y);
                inputEvent = new RdpInputEvent(time, mouseEvent, type == RdpInputMessageType.MouseX);
                reader = localReader;
                return true;

            case RdpInputMessageType.Sync:
                localReader.Advance(2); // pad2Octets
                uint toggle = localReader.ReadUInt32LE();
                var syncEvent = new RdpSyncEvent(time, (RdpSyncToggleFlags)toggle);
                inputEvent = new RdpInputEvent(time, syncEvent);
                reader = localReader;
                return true;

            default:
                inputEvent = default;
                return false;
        }
    }

    public void Write(ref RdpPacketWriter writer)
    {
        writer.WriteUInt32LE(EventTime);
        writer.WriteUInt16LE((ushort)MessageType);

        switch (MessageType)
        {
            case RdpInputMessageType.ScanCode:
            case RdpInputMessageType.VkCode:
                writer.WriteUInt16LE((ushort)KeyboardEvent.Flags);
                writer.WriteUInt16LE((ushort)KeyboardEvent.KeyCode);
                writer.WriteUInt16LE(0x0000); // pad2Octets
                break;

            case RdpInputMessageType.Unicode:
                writer.WriteUInt16LE((ushort)KeyboardEvent.Flags);
                writer.WriteUInt16LE(checked((ushort)KeyboardEvent.KeyCode));
                writer.WriteUInt16LE(0x0000); // pad2Octets
                break;

            case RdpInputMessageType.Mouse:
            case RdpInputMessageType.MouseX:
                writer.WriteUInt16LE((ushort)MouseEvent.PointerFlags);
                writer.WriteUInt16LE(MouseEvent.XPos);
                writer.WriteUInt16LE(MouseEvent.YPos);
                break;

            case RdpInputMessageType.Sync:
                writer.WriteUInt16LE(0x0000); // pad2Octets
                writer.WriteUInt32LE((uint)SyncEvent.ToggleFlags);
                break;

            default:
                writer.WriteUInt16LE(0x0000);
                writer.WriteUInt16LE(0x0000);
                writer.WriteUInt16LE(0x0000);
                break;
        }
    }
}
