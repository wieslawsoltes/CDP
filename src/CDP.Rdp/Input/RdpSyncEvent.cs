namespace CDP.Rdp.Input;

/// <summary>
/// Lock status synchronization event payload.
/// </summary>
public readonly struct RdpSyncEvent
{
    public uint EventTime { get; }
    public RdpSyncToggleFlags ToggleFlags { get; }

    public RdpSyncEvent(uint eventTime, RdpSyncToggleFlags toggleFlags)
    {
        EventTime = eventTime;
        ToggleFlags = toggleFlags;
    }
}
