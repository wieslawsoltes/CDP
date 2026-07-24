namespace CDP.Rdp.Tests.Protocol;

using System;
using CDP.Rdp.Protocol;

public class X224HeaderTests
{
    [Fact]
    public void TryRead_ConnectionRequest_ParsesCodeAndReferences()
    {
        byte[] data = new byte[] { 0x0E, 0xE0, 0x00, 0x00, 0x12, 0x34, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(data);

        bool success = X224Header.TryRead(ref reader, out X224Header header);

        Assert.True(success);
        Assert.Equal(14, header.LengthIndicator);
        Assert.Equal(X224TpduCode.ConnectionRequest, header.Code);
        Assert.Equal(0x0000, header.DstReference);
        Assert.Equal(0x1234, header.SrcReference);
        Assert.Equal(0x00, header.ClassAndOption);
    }

    [Fact]
    public void TryRead_ConnectionConfirm_ParsesCodeAndReferences()
    {
        byte[] data = new byte[] { 0x0E, 0xD0, 0x12, 0x34, 0x56, 0x78, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(data);

        bool success = X224Header.TryRead(ref reader, out X224Header header);

        Assert.True(success);
        Assert.Equal(14, header.LengthIndicator);
        Assert.Equal(X224TpduCode.ConnectionConfirm, header.Code);
        Assert.Equal(0x1234, header.DstReference);
        Assert.Equal(0x5678, header.SrcReference);
        Assert.Equal(0x00, header.ClassAndOption);
    }

    [Fact]
    public void TryRead_InsufficientBytes_ReturnsFalse()
    {
        byte[] data = new byte[] { 0x0E, 0xE0, 0x00, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(data);

        bool success = X224Header.TryRead(ref reader, out X224Header header);

        Assert.False(success);
    }

    [Fact]
    public void Write_ValidHeader_SerializesToSpan()
    {
        byte[] buffer = new byte[7];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        X224Header header = new X224Header(14, X224TpduCode.ConnectionRequest, 0x0000, 0x1234, 0x00);
        header.Write(ref writer);

        Assert.Equal(7, writer.WrittenCount);
        Assert.Equal(14, buffer[0]);
        Assert.Equal(0xE0, buffer[1]);
        Assert.Equal(0x00, buffer[2]);
        Assert.Equal(0x00, buffer[3]);
        Assert.Equal(0x12, buffer[4]);
        Assert.Equal(0x34, buffer[5]);
        Assert.Equal(0x00, buffer[6]);
    }
}
