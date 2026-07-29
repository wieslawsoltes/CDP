using Avalonia.Headless.XUnit;

namespace CDP.Rdp.Tests.Session;

using System.Buffers.Binary;
using System.IO;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Session;
using Xunit;

[Xunit.Collection("RdpTests")]
public sealed class RdpActivationSequenceTests
{
    [AvaloniaFact]
    public void CreateConnectInitial_WritesRequestedDesktopAndValidTpktLength()
    {
        using var stream = new MemoryStream();
        using var transport = new PlainRdpSecurityTransport(stream);
        var sequence = new RdpActivationSequence(
            transport,
            new RdpSessionOptions { Width = 1440, Height = 900, ColorDepth = 32 });

        byte[] packet = sequence.CreateConnectInitial();

        Assert.Equal(3, packet[0]);
        Assert.Equal(packet.Length, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(2, 2)));
        int coreOffset = packet.AsSpan().IndexOf(new byte[] { 0x01, 0xC0, 0xD8, 0x00 });
        Assert.True(coreOffset >= 0);
        Assert.Equal(1440, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(coreOffset + 8, 2)));
        Assert.Equal(900, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(coreOffset + 10, 2)));
        Assert.Equal(24, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(coreOffset + 142, 2)));
        Assert.Equal(0x0008, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(coreOffset + 144, 2)));

        int networkOffset = packet.AsSpan().IndexOf(new byte[] { 0x03, 0xC0, 0x38, 0x00 });
        Assert.True(networkOffset >= 0);
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(networkOffset + 4, 4)));
        Assert.True(packet.AsSpan(networkOffset + 44, 8).SequenceEqual("drdynvc\0"u8));
    }

    [AvaloniaTheory]
    [InlineData((ushort)15, (ushort)15, (ushort)0x0001)]
    [InlineData((ushort)16, (ushort)16, (ushort)0x0002)]
    [InlineData((ushort)24, (ushort)24, (ushort)0x0004)]
    [InlineData((ushort)32, (ushort)24, (ushort)0x0008)]
    public void CreateConnectInitial_EncodesHighColorDepthAndSupportedDepthFlag(
        ushort requestedDepth,
        ushort expectedHighColorDepth,
        ushort expectedSupportedFlag)
    {
        using var stream = new MemoryStream();
        using var transport = new PlainRdpSecurityTransport(stream);
        var sequence = new RdpActivationSequence(
            transport,
            new RdpSessionOptions { ColorDepth = requestedDepth });

        byte[] packet = sequence.CreateConnectInitial();
        int coreOffset = packet.AsSpan().IndexOf(new byte[] { 0x01, 0xC0, 0xD8, 0x00 });

        Assert.True(coreOffset >= 0);
        Assert.Equal(
            expectedHighColorDepth,
            BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(coreOffset + 142, 2)));
        Assert.Equal(
            expectedSupportedFlag,
            BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(coreOffset + 144, 2)));
    }

    [AvaloniaFact]
    public void CreateConfirmActive_WritesIdentityDesktopAndDeclaredLength()
    {
        using var stream = new MemoryStream();
        using var transport = new PlainRdpSecurityTransport(stream);
        var sequence = new RdpActivationSequence(
            transport,
            new RdpSessionOptions { Width = 1366, Height = 768 });

        byte[] pdu = sequence.CreateConfirmActive(1002, 0x000103EA);

        Assert.Equal(pdu.Length, BinaryPrimitives.ReadUInt16LittleEndian(pdu.AsSpan(0, 2)));
        Assert.Equal(1002, BinaryPrimitives.ReadUInt16LittleEndian(pdu.AsSpan(4, 2)));
        Assert.Equal(0x000103EAu, BinaryPrimitives.ReadUInt32LittleEndian(pdu.AsSpan(6, 4)));
        int bitmapOffset = pdu.AsSpan().IndexOf(new byte[] { 0x02, 0x00, 0x1C, 0x00 });
        Assert.True(bitmapOffset >= 0);
        Assert.Equal(1366, BinaryPrimitives.ReadUInt16LittleEndian(pdu.AsSpan(bitmapOffset + 12, 2)));
        Assert.Equal(768, BinaryPrimitives.ReadUInt16LittleEndian(pdu.AsSpan(bitmapOffset + 14, 2)));

        int orderOffset = pdu.AsSpan().IndexOf(new byte[] { 0x03, 0x00, 0x58, 0x00 });
        Assert.True(orderOffset >= 0);
        Assert.True(pdu.AsSpan(orderOffset + 36, 32).SequenceEqual(new byte[32]));
    }

    [AvaloniaFact]
    public void ParseDemandActive_ReadsServerDesktopDimensions()
    {
        byte[] pdu = new byte[46];
        BinaryPrimitives.WriteUInt16LittleEndian(pdu, checked((ushort)pdu.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(2), 0x0011);
        BinaryPrimitives.WriteUInt32LittleEndian(pdu.AsSpan(6), 0x000103EA);
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(12), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(18), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(20), 28);
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(30), 1600);
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(32), 900);

        RdpDemandActiveInfo result = RdpActivationSequence.ParseDemandActive(pdu);

        Assert.Equal(0x000103EAu, result.ShareId);
        Assert.Equal(1600, result.DesktopWidth);
        Assert.Equal(900, result.DesktopHeight);
    }

    [AvaloniaFact]
    public void IsDemandActivePdu_LengthWithLicenseFlagBitIsStillShareControl()
    {
        byte[] pdu = new byte[0x0080];
        BinaryPrimitives.WriteUInt16LittleEndian(pdu, checked((ushort)pdu.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(2), 0x0011);

        Assert.True(RdpActivationSequence.IsDemandActivePdu(pdu));
    }

    [AvaloniaFact]
    public void IsDemandActivePdu_LicenseSecurityHeaderIsNotShareControl()
    {
        byte[] licensingPacket = new byte[0x0080];
        BinaryPrimitives.WriteUInt16LittleEndian(licensingPacket, 0x0080);
        BinaryPrimitives.WriteUInt16LittleEndian(licensingPacket.AsSpan(2), 0);

        Assert.False(RdpActivationSequence.IsDemandActivePdu(licensingPacket));
    }

    [AvaloniaFact]
    public void CreateConfirmActive_UsesActivatedDesktopDimensions()
    {
        using var stream = new MemoryStream();
        using var transport = new PlainRdpSecurityTransport(stream);
        var sequence = new RdpActivationSequence(
            transport,
            new RdpSessionOptions { Width = 1920, Height = 1080 });

        byte[] pdu = sequence.CreateConfirmActive(1002, 0x000103EA, 1600, 900);
        int bitmapOffset = pdu.AsSpan().IndexOf(new byte[] { 0x02, 0x00, 0x1C, 0x00 });

        Assert.True(bitmapOffset >= 0);
        Assert.Equal(1600, BinaryPrimitives.ReadUInt16LittleEndian(pdu.AsSpan(bitmapOffset + 12, 2)));
        Assert.Equal(900, BinaryPrimitives.ReadUInt16LittleEndian(pdu.AsSpan(bitmapOffset + 14, 2)));
    }

    [AvaloniaFact]
    public void ParseConnectResponse_RejectsMissingServerNetworkData()
    {
        byte[] packet = new byte[]
        {
            3, 0, 0, 11,
            2, 0xF0, 0x80,
            0x7F, 0x66, 0, 0
        };

        Assert.Throws<InvalidDataException>(() => RdpActivationSequence.ParseConnectResponse(packet));
    }
}
