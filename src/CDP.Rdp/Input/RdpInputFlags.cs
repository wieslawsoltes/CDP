namespace CDP.Rdp.Input;

using System;

/// <summary>
/// Keyboard flags for SlowPath input events (MS-RDPBCGR Section 2.2.8.1.1.3.1.1).
/// </summary>
[Flags]
public enum RdpKeyboardFlags : ushort
{
    None = 0x0000,

    /// <summary>
    /// Extended scancode 1 (prefixed with 0xE0).
    /// </summary>
    Extended = 0x0100,

    /// <summary>
    /// Extended scancode 2 (prefixed with 0xE1).
    /// </summary>
    Extended1 = 0x0200,

    /// <summary>
    /// Key press action (indicated when Release flag is cleared).
    /// </summary>
    Down = 0x0000,

    /// <summary>
    /// Key release action (key up).
    /// </summary>
    Release = 0x8000
}

/// <summary>
/// Pointer flags for mouse movement, click, and wheel actions (MS-RDPBCGR Section 2.2.8.1.1.3.1.1).
/// </summary>
[Flags]
public enum RdpPointerFlags : ushort
{
    None = 0x0000,

    /// <summary>
    /// Wheel rotation direction is negative (downward/away).
    /// </summary>
    WheelNegative = 0x0100,

    /// <summary>
    /// Vertical mouse wheel event.
    /// </summary>
    Wheel = 0x0200,

    /// <summary>
    /// Pointer movement event.
    /// </summary>
    Move = 0x0800,

    /// <summary>
    /// Left mouse button.
    /// </summary>
    Button1 = 0x1000,

    /// <summary>
    /// Right mouse button.
    /// </summary>
    Button2 = 0x2000,

    /// <summary>
    /// Middle mouse button.
    /// </summary>
    Button3 = 0x4000,

    /// <summary>
    /// Mouse button press (DOWN). When cleared, button is released (UP).
    /// </summary>
    Down = 0x8000
}

/// <summary>
/// Lock state synchronization toggle flags (MS-RDPBCGR Section 2.2.8.1.1.3.1.1).
/// </summary>
[Flags]
public enum RdpSyncToggleFlags : uint
{
    None = 0x00000000,

    /// <summary>
    /// Scroll Lock state active.
    /// </summary>
    ScrollLock = 0x00000001,

    /// <summary>
    /// Num Lock state active.
    /// </summary>
    NumLock = 0x00000002,

    /// <summary>
    /// Caps Lock state active.
    /// </summary>
    CapsLock = 0x00000004,

    /// <summary>
    /// Kana Lock state active.
    /// </summary>
    KanaLock = 0x00000008
}

/// <summary>
/// SlowPath input event message types (MS-RDPBCGR Section 2.2.8.1.1.3.1.1).
/// </summary>
public enum RdpInputMessageType : ushort
{
    /// <summary>
    /// Lock status synchronization event.
    /// </summary>
    Sync = 0x0000,

    /// <summary>
    /// Keyboard scan code event.
    /// </summary>
    ScanCode = 0x0004,

    /// <summary>
    /// Unicode key event.
    /// </summary>
    Unicode = 0x0005,

    /// <summary>
    /// Standard mouse event.
    /// </summary>
    Mouse = 0x8001,

    /// <summary>
    /// Extended mouse event.
    /// </summary>
    MouseX = 0x8002,

    /// <summary>
    /// Virtual-Key code event.
    /// </summary>
    VkCode = 0x0008
}

/// <summary>
/// FastPath input event codes (MS-RDPBCGR Section 2.2.8.1.2.2).
/// </summary>
public enum FastPathInputEventCode : byte
{
    /// <summary>
    /// Keyboard scancode event.
    /// </summary>
    ScanCode = 0x00,

    /// <summary>
    /// Standard mouse event.
    /// </summary>
    Mouse = 0x01,

    /// <summary>
    /// Extended mouse event.
    /// </summary>
    MouseX = 0x02,

    /// <summary>
    /// Synchronize lock state event.
    /// </summary>
    Sync = 0x03,

    /// <summary>
    /// Unicode keyboard event.
    /// </summary>
    Unicode = 0x04,

    /// <summary>
    /// Release all pressed keys event.
    /// </summary>
    ReleaseAll = 0x05,

    /// <summary>
    /// Quality of Experience timestamp event.
    /// </summary>
    QoeTimestamp = 0x06
}

/// <summary>
/// FastPath keyboard flags (MS-RDPBCGR Section 2.2.8.1.2.2).
/// </summary>
[Flags]
public enum FastPathKeyboardFlags : byte
{
    None = 0x00,

    /// <summary>
    /// Key release action.
    /// </summary>
    Release = 0x01,

    /// <summary>
    /// Extended scancode 1 (0xE0).
    /// </summary>
    Extended = 0x02,

    /// <summary>
    /// Extended scancode 2 (0xE1).
    /// </summary>
    Extended1 = 0x04
}
