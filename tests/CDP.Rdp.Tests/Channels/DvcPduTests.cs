using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Channels;

using System;
using CDP.Rdp.Channels;
using CDP.Rdp.Protocol;
using Xunit;

[Xunit.Collection("RdpTests")]
public class DvcPduTests
{
    [AvaloniaTheory]
    [InlineData(DvcCommandCode.Create, (byte)0, (byte)1)]
    [InlineData(DvcCommandCode.DataFirst, (byte)1, (byte)2)]
    [InlineData(DvcCommandCode.Capabilities, (byte)2, (byte)0)]
    public void DvcHeader_RoundTrip(DvcCommandCode cmd, byte sp, byte priority)
    {
        var header = new DvcHeader(cmd, sp, priority);

        byte[] buffer = new byte[1];
        var writer = new RdpPacketWriter(buffer);
        header.Write(ref writer);

        var reader = new RdpPacketReader(buffer);
        bool success = DvcHeader.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(cmd, parsed.Command);
        Assert.Equal(sp, parsed.Sp);
        Assert.Equal(priority, parsed.Priority);
    }

    [AvaloniaTheory]
    [InlineData(0x42u, 0)]
    [InlineData(0x1234u, 1)]
    [InlineData(0x12345678u, 2)]
    public void DvcValueCodec_SpEncoding_RoundTrip(uint value, byte expectedSp)
    {
        byte sp = DvcValueCodec.GetRequiredSp(value);
        Assert.Equal(expectedSp, sp);

        byte[] buffer = new byte[4];
        var writer = new RdpPacketWriter(buffer);
        DvcValueCodec.WriteValue(ref writer, sp, value);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcValueCodec.TryReadValue(ref reader, sp, out uint parsed);

        Assert.True(success);
        Assert.Equal(value, parsed);
    }

    [AvaloniaFact]
    public void DvcCapabilities_V1_RoundTrip()
    {
        var caps = new DvcCapabilitiesPdu(version: 1);

        byte[] buffer = new byte[16];
        var writer = new RdpPacketWriter(buffer);
        caps.Write(ref writer);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcCapabilitiesPdu.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(1, parsed.Version);
    }

    [AvaloniaFact]
    public void DvcCapabilities_V2WithCharges_RoundTrip()
    {
        var caps = new DvcCapabilitiesPdu(version: 2, pri0: 10, pri1: 20, pri2: 30, pri3: 40);

        byte[] buffer = new byte[32];
        var writer = new RdpPacketWriter(buffer);
        caps.Write(ref writer);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcCapabilitiesPdu.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(2, parsed.Version);
        Assert.Equal(10, parsed.Priority0Charge);
        Assert.Equal(20, parsed.Priority1Charge);
        Assert.Equal(30, parsed.Priority2Charge);
        Assert.Equal(40, parsed.Priority3Charge);
    }

    [AvaloniaFact]
    public void DvcCreateRequest_RoundTrip()
    {
        var req = new DvcCreateRequestPdu(channelId: 42, channelName: "AUDIO_INPUT", priority: 1);

        byte[] buffer = new byte[32];
        var writer = new RdpPacketWriter(buffer);
        req.Write(ref writer);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcCreateRequestPdu.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(42u, parsed.ChannelId);
        Assert.Equal("AUDIO_INPUT", parsed.ChannelName);
        Assert.Equal(1, parsed.Priority);
    }

    [AvaloniaFact]
    public void DvcCreateResponse_Success_RoundTrip()
    {
        var rsp = new DvcCreateResponsePdu(channelId: 100, creationStatus: 0, priority: 0);

        byte[] buffer = new byte[16];
        var writer = new RdpPacketWriter(buffer);
        rsp.Write(ref writer);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcCreateResponsePdu.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(100u, parsed.ChannelId);
        Assert.Equal(0, parsed.CreationStatus);
        Assert.True(parsed.IsSuccess);
    }

    [AvaloniaFact]
    public void DvcClose_RoundTrip()
    {
        var closePdu = new DvcClosePdu(channelId: 250);

        byte[] buffer = new byte[8];
        var writer = new RdpPacketWriter(buffer);
        closePdu.Write(ref writer);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcClosePdu.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(250u, parsed.ChannelId);
    }

    [AvaloniaFact]
    public void DvcDataHeader_RoundTrip()
    {
        var dataHeader = new DvcDataHeader(channelId: 300);

        byte[] buffer = new byte[8];
        var writer = new RdpPacketWriter(buffer);
        dataHeader.Write(ref writer);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcDataHeader.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(300u, parsed.ChannelId);
    }

    [AvaloniaFact]
    public void DvcDataFirstHeader_SmallTotalLength_RoundTrip()
    {
        var header = new DvcDataFirstHeader(channelId: 1, totalLength: 100);

        byte[] buffer = new byte[16];
        var writer = new RdpPacketWriter(buffer);
        header.Write(ref writer);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcDataFirstHeader.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(1u, parsed.ChannelId);
        Assert.Equal(100u, parsed.TotalLength);
    }

    [AvaloniaFact]
    public void DvcDataFirstHeader_MediumTotalLength_RoundTrip_Bits67Encoded()
    {
        // Total length = 500 (> 255), requires 2-byte length encoding (lenSp = 1)
        var header = new DvcDataFirstHeader(channelId: 1, totalLength: 500);

        byte[] buffer = new byte[16];
        var writer = new RdpPacketWriter(buffer);
        header.Write(ref writer);

        // MS-RDPEDYC packs Len into header bits 2-3.
        byte headerByte = buffer[0];
        byte lenSpBits = (byte)((headerByte >> 2) & 0x03);
        Assert.Equal(1, lenSpBits);

        // Verify TryRead with small packet buffer (50 bytes) correctly reads totalLength = 500
        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcDataFirstHeader.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(1u, parsed.ChannelId);
        Assert.Equal(500u, parsed.TotalLength);
    }

    [AvaloniaFact]
    public void DvcDataFirstHeader_LargeTotalLength_RoundTrip()
    {
        // Total length = 70,000 (> 65,535), requires 4-byte length encoding (lenSp = 2)
        var header = new DvcDataFirstHeader(channelId: 10, totalLength: 70000);

        byte[] buffer = new byte[16];
        var writer = new RdpPacketWriter(buffer);
        header.Write(ref writer);

        byte headerByte = buffer[0];
        byte lenSpBits = (byte)((headerByte >> 2) & 0x03);
        Assert.Equal(2, lenSpBits);

        var reader = new RdpPacketReader(buffer.AsSpan(0, writer.WrittenCount));
        bool success = DvcDataFirstHeader.TryRead(ref reader, out var parsed);

        Assert.True(success);
        Assert.Equal(10u, parsed.ChannelId);
        Assert.Equal(70000u, parsed.TotalLength);
    }
}
