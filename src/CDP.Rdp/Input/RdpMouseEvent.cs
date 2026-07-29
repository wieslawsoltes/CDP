namespace CDP.Rdp.Input;

/// <summary>
/// Mouse input event data payload.
/// </summary>
public readonly struct RdpMouseEvent
{
    public uint EventTime { get; }
    public RdpPointerFlags PointerFlags { get; }
    public ushort XPos { get; }
    public ushort YPos { get; }

    public RdpMouseEvent(uint eventTime, RdpPointerFlags pointerFlags, ushort xPos, ushort yPos)
    {
        EventTime = eventTime;
        PointerFlags = pointerFlags;
        XPos = xPos;
        YPos = yPos;
    }
}
