namespace CDP.Rdp.Tests.Protocol;

using System;
using CDP.Rdp.Protocol;

public class RdpPacketReaderTests
{
    [Fact]
    public void ReadByte_ValidBytes_DecodesCorrectly()
    {
        byte[] data = new byte[] { 0x01, 0x02, 0xFF };
        RdpPacketReader reader = new RdpPacketReader(data);

        Assert.Equal(3, reader.Length);
        Assert.Equal(0, reader.Position);
        Assert.Equal(3, reader.UnreadLength);

        Assert.Equal(0x01, reader.ReadByte());
        Assert.Equal(1, reader.Position);
        Assert.Equal(2, reader.UnreadLength);

        Assert.Equal(0x02, reader.ReadByte());
        Assert.Equal(0xFF, reader.ReadByte());
        Assert.Equal(0, reader.UnreadLength);
    }

    [Fact]
    public void ReadByte_PastEnd_ThrowsInvalidOperationException()
    {
        byte[] data = new byte[] { 0x42 };
        RdpPacketReader reader = new RdpPacketReader(data);
        reader.ReadByte();

        bool threw = false;
        try
        {
            reader.ReadByte();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert.True(threw);
    }

    [Fact]
    public void ReadUInt16BE_ValidBytes_DecodesBigEndian()
    {
        byte[] data = new byte[] { 0x12, 0x34 };
        RdpPacketReader reader = new RdpPacketReader(data);

        ushort value = reader.ReadUInt16BE();
        Assert.Equal(0x1234, value);
        Assert.Equal(2, reader.Position);
    }

    [Fact]
    public void ReadUInt16LE_ValidBytes_DecodesLittleEndian()
    {
        byte[] data = new byte[] { 0x34, 0x12 };
        RdpPacketReader reader = new RdpPacketReader(data);

        ushort value = reader.ReadUInt16LE();
        Assert.Equal(0x1234, value);
        Assert.Equal(2, reader.Position);
    }

    [Fact]
    public void ReadUInt32BE_ValidBytes_DecodesBigEndian()
    {
        byte[] data = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        RdpPacketReader reader = new RdpPacketReader(data);

        uint value = reader.ReadUInt32BE();
        Assert.Equal(0x12345678u, value);
    }

    [Fact]
    public void ReadUInt32LE_ValidBytes_DecodesLittleEndian()
    {
        byte[] data = new byte[] { 0x78, 0x56, 0x34, 0x12 };
        RdpPacketReader reader = new RdpPacketReader(data);

        uint value = reader.ReadUInt32LE();
        Assert.Equal(0x12345678u, value);
    }

    [Fact]
    public void ReadSpan_ValidLength_ReturnsSliceAndAdvances()
    {
        byte[] data = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
        RdpPacketReader reader = new RdpPacketReader(data);

        ReadOnlySpan<byte> slice = reader.ReadSpan(3);
        Assert.Equal(3, slice.Length);
        Assert.Equal(0x10, slice[0]);
        Assert.Equal(0x20, slice[1]);
        Assert.Equal(0x30, slice[2]);
        Assert.Equal(3, reader.Position);
    }

    [Fact]
    public void Advance_ValidCount_AdvancesPosition()
    {
        byte[] data = new byte[10];
        RdpPacketReader reader = new RdpPacketReader(data);

        reader.Advance(4);
        Assert.Equal(4, reader.Position);
        Assert.Equal(6, reader.UnreadLength);
    }
}
