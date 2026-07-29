namespace CDP.Rdp.Channels;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// Static helper reader for Virtual Channel PDUs (MS-RDPBCGR Section 2.2.6).
/// </summary>
public static class RdpChannelPduReader
{
    public static bool TryReadHeader(ref RdpPacketReader reader, out ChannelPduHeader header)
    {
        return ChannelPduHeader.TryRead(ref reader, out header);
    }

    public static bool TryReadJoinRequest(ref RdpPacketReader reader, out McsChannelJoinRequest request)
    {
        return McsChannelJoinRequest.TryRead(ref reader, out request);
    }

    public static bool TryReadJoinConfirm(ref RdpPacketReader reader, out McsChannelJoinConfirm confirm)
    {
        return McsChannelJoinConfirm.TryRead(ref reader, out confirm);
    }
}
