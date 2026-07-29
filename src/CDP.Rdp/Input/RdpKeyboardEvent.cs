namespace CDP.Rdp.Input;

/// <summary>
/// Keyboard input event data payload (ScanCode or Virtual-Key).
/// </summary>
public readonly struct RdpKeyboardEvent
{
    public uint EventTime { get; }
    public RdpKeyboardFlags Flags { get; }
    public uint KeyCode { get; }
    public bool IsVirtualKey { get; }

    public RdpKeyboardEvent(uint eventTime, RdpKeyboardFlags flags, uint keyCode, bool isVirtualKey = false)
    {
        EventTime = eventTime;
        Flags = flags;
        KeyCode = keyCode;
        IsVirtualKey = isVirtualKey;
    }
}
