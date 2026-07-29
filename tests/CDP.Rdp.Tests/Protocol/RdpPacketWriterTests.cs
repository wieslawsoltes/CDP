using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Protocol;

using System;
using CDP.Rdp.Protocol;

[Xunit.Collection("RdpTests")]
public class RdpPacketWriterTests
{
    [AvaloniaFact]
    public void WriteByte_ValidSpan_EncodesCorrectly()
    {
        byte[] buffer = new byte[4];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        writer.WriteByte(0xAB);
        writer.WriteByte(0xCD);

        Assert.Equal(2, writer.WrittenCount);
        Assert.Equal(2, writer.RemainingCapacity);
        Assert.Equal(0xAB, buffer[0]);
        Assert.Equal(0xCD, buffer[1]);
    }

    [AvaloniaFact]
    public void WriteUInt16BE_ValidSpan_WritesBigEndian()
    {
        byte[] buffer = new byte[2];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        writer.WriteUInt16BE(0x1234);

        Assert.Equal(2, writer.WrittenCount);
        Assert.Equal(0x12, buffer[0]);
        Assert.Equal(0x34, buffer[1]);
    }

    [AvaloniaFact]
    public void WriteUInt16LE_ValidSpan_WritesLittleEndian()
    {
        byte[] buffer = new byte[2];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        writer.WriteUInt16LE(0x1234);

        Assert.Equal(2, writer.WrittenCount);
        Assert.Equal(0x34, buffer[0]);
        Assert.Equal(0x12, buffer[1]);
    }

    [AvaloniaFact]
    public void WriteUInt32BE_ValidSpan_WritesBigEndian()
    {
        byte[] buffer = new byte[4];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        writer.WriteUInt32BE(0x12345678u);

        Assert.Equal(4, writer.WrittenCount);
        Assert.Equal(0x12, buffer[0]);
        Assert.Equal(0x34, buffer[1]);
        Assert.Equal(0x56, buffer[2]);
        Assert.Equal(0x78, buffer[3]);
    }

    [AvaloniaFact]
    public void WriteUInt32LE_ValidSpan_WritesLittleEndian()
    {
        byte[] buffer = new byte[4];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        writer.WriteUInt32LE(0x12345678u);

        Assert.Equal(4, writer.WrittenCount);
        Assert.Equal(0x78, buffer[0]);
        Assert.Equal(0x56, buffer[1]);
        Assert.Equal(0x34, buffer[2]);
        Assert.Equal(0x12, buffer[3]);
    }

    [AvaloniaFact]
    public void WriteSpan_ValidSource_CopiesSpanContent()
    {
        byte[] buffer = new byte[5];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        byte[] source = new byte[] { 0x01, 0x02, 0x03 };
        writer.WriteSpan(source);

        Assert.Equal(3, writer.WrittenCount);
        Assert.Equal(0x01, buffer[0]);
        Assert.Equal(0x02, buffer[1]);
        Assert.Equal(0x03, buffer[2]);
    }

    [AvaloniaFact]
    public void WriteByte_CapacityExceeded_ThrowsInvalidOperationException()
    {
        byte[] buffer = new byte[1];
        RdpPacketWriter writer = new RdpPacketWriter(buffer);

        writer.WriteByte(0x01);
        bool threw = false;
        try
        {
            writer.WriteByte(0x02);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert.True(threw);
    }
}
