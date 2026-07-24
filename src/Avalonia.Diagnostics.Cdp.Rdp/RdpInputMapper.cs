namespace Avalonia.Diagnostics.Cdp.Rdp;

using System;
using System.Collections.Generic;
using Avalonia.Input;
using CDP.Rdp.Input;

/// <summary>
/// Maps Avalonia Key and PhysicalKey inputs to RDP scancodes and flags.
/// </summary>
public static class RdpInputMapper
{
    private static readonly Dictionary<Key, ushort> KeyToScanCodeMap = new()
    {
        { Key.Escape, 0x01 },
        { Key.D1, 0x02 }, { Key.D2, 0x03 }, { Key.D3, 0x04 }, { Key.D4, 0x05 },
        { Key.D5, 0x06 }, { Key.D6, 0x07 }, { Key.D7, 0x08 }, { Key.D8, 0x09 },
        { Key.D9, 0x0A }, { Key.D0, 0x0B },
        { Key.OemMinus, 0x0C }, { Key.OemPlus, 0x0D },
        { Key.Back, 0x0E }, { Key.Tab, 0x0F },
        { Key.Q, 0x10 }, { Key.W, 0x11 }, { Key.E, 0x12 }, { Key.R, 0x13 },
        { Key.T, 0x14 }, { Key.Y, 0x15 }, { Key.U, 0x16 }, { Key.I, 0x17 },
        { Key.O, 0x18 }, { Key.P, 0x19 },
        { Key.OemOpenBrackets, 0x1A }, { Key.OemCloseBrackets, 0x1B },
        { Key.Enter, 0x1C }, { Key.LeftCtrl, 0x1D },
        { Key.A, 0x1E }, { Key.S, 0x1F }, { Key.D, 0x20 }, { Key.F, 0x21 },
        { Key.G, 0x22 }, { Key.H, 0x23 }, { Key.J, 0x24 }, { Key.K, 0x25 },
        { Key.L, 0x26 }, { Key.OemSemicolon, 0x27 }, { Key.OemQuotes, 0x28 },
        { Key.OemTilde, 0x29 }, { Key.LeftShift, 0x2A }, { Key.OemPipe, 0x2B },
        { Key.Z, 0x2C }, { Key.X, 0x2D }, { Key.C, 0x2E }, { Key.V, 0x2F },
        { Key.B, 0x30 }, { Key.N, 0x31 }, { Key.M, 0x32 },
        { Key.OemComma, 0x33 }, { Key.OemPeriod, 0x34 }, { Key.OemQuestion, 0x35 },
        { Key.RightShift, 0x36 }, { Key.Multiply, 0x37 },
        { Key.LeftAlt, 0x38 }, { Key.Space, 0x39 }, { Key.CapsLock, 0x3A },
        { Key.F1, 0x3B }, { Key.F2, 0x3C }, { Key.F3, 0x3D }, { Key.F4, 0x3E },
        { Key.F5, 0x3F }, { Key.F6, 0x40 }, { Key.F7, 0x41 }, { Key.F8, 0x42 },
        { Key.F9, 0x43 }, { Key.F10, 0x44 },
        { Key.NumLock, 0x45 }, { Key.Scroll, 0x46 },
        { Key.F11, 0x57 }, { Key.F12, 0x58 }
    };

    private static readonly HashSet<Key> ExtendedKeys = new()
    {
        Key.Up, Key.Down, Key.Left, Key.Right,
        Key.Insert, Key.Delete, Key.Home, Key.End, Key.PageUp, Key.PageDown,
        Key.RightAlt, Key.RightCtrl, Key.LWin, Key.RWin, Key.Apps
    };

    private static readonly Dictionary<Key, ushort> ExtendedKeyScanCodeMap = new()
    {
        { Key.Up, 0x48 }, { Key.Down, 0x50 }, { Key.Left, 0x4B }, { Key.Right, 0x4D },
        { Key.Insert, 0x52 }, { Key.Delete, 0x53 }, { Key.Home, 0x47 }, { Key.End, 0x4F },
        { Key.PageUp, 0x49 }, { Key.PageDown, 0x51 },
        { Key.RightAlt, 0x38 }, { Key.RightCtrl, 0x1D },
        { Key.LWin, 0x5B }, { Key.RWin, 0x5C }, { Key.Apps, 0x5D }
    };

    public static bool TryMapKey(Key key, bool isDown, out RdpKeyboardEvent kbEvent)
    {
        return TryMapKey(key, PhysicalKey.None, isDown, out kbEvent);
    }

    public static bool TryMapKey(Key key, PhysicalKey physicalKey, bool isDown, out RdpKeyboardEvent kbEvent)
    {
        RdpKeyboardFlags flags = isDown ? RdpKeyboardFlags.Down : RdpKeyboardFlags.Release;

        if (ExtendedKeys.Contains(key) && ExtendedKeyScanCodeMap.TryGetValue(key, out ushort extCode))
        {
            flags |= RdpKeyboardFlags.Extended;
            kbEvent = new RdpKeyboardEvent((uint)Environment.TickCount, flags, extCode, isVirtualKey: false);
            return true;
        }

        if (KeyToScanCodeMap.TryGetValue(key, out ushort scanCode))
        {
            kbEvent = new RdpKeyboardEvent((uint)Environment.TickCount, flags, scanCode, isVirtualKey: false);
            return true;
        }

        kbEvent = default;
        return false;
    }
}
