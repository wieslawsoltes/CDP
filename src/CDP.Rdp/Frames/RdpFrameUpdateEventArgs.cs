namespace CDP.Rdp.Frames;

using System;
using System.Collections.Generic;

/// <summary>
/// Event arguments delivered when a complete frame update is received and parsed.
/// </summary>
public sealed class RdpFrameUpdateEventArgs : EventArgs
{
    public ulong FrameId { get; }
    public DateTimeOffset Timestamp { get; }
    public IReadOnlyList<RdpBitmapUpdate> BitmapUpdates { get; }

    public RdpFrameUpdateEventArgs(
        ulong frameId,
        DateTimeOffset timestamp,
        IReadOnlyList<RdpBitmapUpdate> bitmapUpdates)
    {
        FrameId = frameId;
        Timestamp = timestamp;
        BitmapUpdates = bitmapUpdates ?? Array.Empty<RdpBitmapUpdate>();
    }
}
