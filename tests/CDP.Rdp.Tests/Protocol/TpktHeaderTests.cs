using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Protocol;

using System;
using CDP.Rdp.Protocol;

[Xunit.Collection("RdpTests")]
public class TpktHeaderTests
{
    [AvaloniaFact]
    public void TryRead_ValidHeader_ReturnsTrueAndParsesCorrectly()
    {
        byte[] data = new byte[] { 0x03, 0x00, 0x00, 0x13 };
        RdpPacketReader reader = new RdpPacketReader(data);

        bool success = TpktHeader.TryRead(ref reader, out TpktHeader header);

        Assert.True(success);
        Assert.Equal(0x03, header.Version);
        Assert.Equal(0x00, header.Reserved);
        Assert.Equal(19, header.PacketLength);
        Assert.Equal(4, reader.Position);
    }

    [AvaloniaFact]
    public void TryRead_InvalidVersion_ReturnsFalse()
    {
        byte[] data = new byte[] { 0x02, 0x00, 0x00, 0x13 };
        RdpPacketReader reader = new RdpPacketReader(data);

        bool success = TpktHeader.TryRead(ref reader, out TpktHeader header);

        Assert.False(success);
    }

    [AvaloniaFact]
    public void TryRead_LengthLessThan4_ReturnsFalse()
    {
        byte[] data = new byte[] { 0x03, 0x00, 0x00, 0x03 };
        RdpPacketReader reader = new RdpPacketReader(data);

        bool success = TpktHeader.TryRead(ref reader, out TpktHeader header);

        Assert.False(success);
    }

    [AvaloniaFact]
    public void TryRead_InsufficientBytes_ReturnsFalse()
    {
        byte[] data = new byte[] { 0x03, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(data);

        bool success = TpktHeader.TryRead(ref reader, out TpktHeader header);

        Assert.False(success);
    }

    [AvaloniaFact]
    public void Write_ValidHeader_SerializesToSpan()
    {
        byte[] buffer = new byte[4];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        TpktHeader header = new TpktHeader(128);
        header.Write(ref writer);

        Assert.Equal(4, writer.WrittenCount);
        Assert.Equal(0x03, buffer[0]);
        Assert.Equal(0x00, buffer[1]);
        Assert.Equal(0x00, buffer[2]);
        Assert.Equal(0x80, buffer[3]);
    }
}
