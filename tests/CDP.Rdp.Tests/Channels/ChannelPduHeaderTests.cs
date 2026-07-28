using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Channels;

using System;
using CDP.Rdp.Channels;
using CDP.Rdp.Protocol;
using Xunit;

public class ChannelPduHeaderTests
{
    [AvaloniaFact]
    public void ChannelPduHeader_SingleChunkFlags_RoundTrip()
    {
        uint payloadLength = 1024;
        var header = new ChannelPduHeader(payloadLength, ChannelPduFlags.First | ChannelPduFlags.Last);

        byte[] buffer = new byte[8];
        var writer = new RdpPacketWriter(buffer);
        header.Write(ref writer);

        Assert.Equal(8, writer.WrittenCount);

        var reader = new RdpPacketReader(buffer);
        bool success = ChannelPduHeader.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(8, reader.Position);
        Assert.Equal(1024u, parsed.Length);
        Assert.True(parsed.Flags.HasFlag(ChannelPduFlags.First));
        Assert.True(parsed.Flags.HasFlag(ChannelPduFlags.Last));
    }

    [AvaloniaFact]
    public void ChannelPduHeader_InsufficientBytes_ReturnsFalse()
    {
        byte[] buffer = new byte[7];
        var reader = new RdpPacketReader(buffer);

        bool success = ChannelPduHeader.TryRead(ref reader, out _);

        Assert.False(success);
    }

    [AvaloniaFact]
    public void McsChannelJoinRequest_BER_RoundTrip()
    {
        var req = new McsChannelJoinRequest(initiatorId: 1004, channelId: 1005);

        byte[] buffer = new byte[16];
        var writer = new RdpPacketWriter(buffer);
        req.Write(ref writer);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = McsChannelJoinRequest.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(1004, parsed.InitiatorId);
        Assert.Equal(1005, parsed.ChannelId);
    }

    [AvaloniaFact]
    public void McsChannelJoinConfirm_BER_RoundTrip()
    {
        var cfm = new McsChannelJoinConfirm(result: 0, initiatorId: 1004, requestedChannelId: 1005, channelId: 1005);

        byte[] buffer = new byte[32];
        var writer = new RdpPacketWriter(buffer);
        cfm.Write(ref writer);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = McsChannelJoinConfirm.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(0, parsed.Result);
        Assert.Equal(1004, parsed.InitiatorId);
        Assert.Equal(1005, parsed.RequestedChannelId);
        Assert.Equal(1005, parsed.ChannelId);
    }
}
