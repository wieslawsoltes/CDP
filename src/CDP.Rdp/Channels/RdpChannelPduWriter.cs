namespace CDP.Rdp.Channels;

using System;
using CDP.Rdp.Protocol;

/// <summary>
/// Static helper writer for Virtual Channel PDUs (MS-RDPBCGR Section 2.2.6).
/// </summary>
public static class RdpChannelPduWriter
{
    public static void WriteHeader(ref RdpPacketWriter writer, ChannelPduHeader header)
    {
        header.Write(ref writer);
    }

    public static void WriteJoinRequest(ref RdpPacketWriter writer, McsChannelJoinRequest request)
    {
        request.Write(ref writer);
    }

    public static void WriteJoinConfirm(ref RdpPacketWriter writer, McsChannelJoinConfirm confirm)
    {
        confirm.Write(ref writer);
    }
}
