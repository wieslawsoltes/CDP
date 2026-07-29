namespace CDP.Rdp.Session;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Channels;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;

/// <summary>
/// Performs the post-security RDP connection sequence defined by MS-RDPBCGR:
/// MCS/GCC setup, channel attachment, secure client-info exchange, capability
/// activation, and connection finalization.
/// </summary>
internal sealed class RdpActivationSequence
{
    private const ushort GlobalChannelId = 1003;
    private const int MaximumPacketLength = 65_535;
    private readonly IRdpSecurityTransport _transport;
    private readonly Stream _stream;
    private readonly RdpSessionOptions _options;
    private readonly RdpSecurityProtocol _protocol;

    public RdpActivationSequence(IRdpSecurityTransport transport, RdpSessionOptions options)
    {
        _transport = transport;
        _stream = transport.TransportStream;
        _options = options;
        _protocol = transport.Protocol;
    }

    public async Task<RdpActivationResult> ActivateAsync(CancellationToken cancellationToken)
    {
        await WriteAsync(CreateConnectInitial(), cancellationToken).ConfigureAwait(false);
        byte[] connectResponse = await ReadTpktAsync(cancellationToken).ConfigureAwait(false);
        RdpServerNetworkData network = ParseConnectResponse(connectResponse);

        await WriteDomainPduAsync(new byte[] { 0x04, 0x01, 0x00, 0x01, 0x00 }, cancellationToken).ConfigureAwait(false);
        await WriteDomainPduAsync(new byte[] { 0x28 }, cancellationToken).ConfigureAwait(false);

        byte[] attachConfirm = GetDomainPayload(await ReadTpktAsync(cancellationToken).ConfigureAwait(false)).ToArray();
        if (attachConfirm.Length < 4 || attachConfirm[0] != 0x2E || attachConfirm[1] != 0)
        {
            throw new InvalidDataException("The server returned an invalid MCS Attach User Confirm PDU.");
        }

        ushort userId = checked((ushort)(BinaryPrimitives.ReadUInt16BigEndian(attachConfirm.AsSpan(2, 2)) + 1001));
        var channels = new List<ushort> { userId, network.IoChannelId };
        channels.AddRange(network.StaticChannelIds.Where(channel => !channels.Contains(channel)));

        foreach (ushort channelId in channels)
        {
            byte[] joinRequest = new byte[5];
            var writer = new RdpPacketWriter(joinRequest);
            new McsChannelJoinRequest(userId, channelId).Write(ref writer);
            await WriteDomainPduAsync(joinRequest, cancellationToken).ConfigureAwait(false);

            byte[] joinConfirm = GetDomainPayload(await ReadTpktAsync(cancellationToken).ConfigureAwait(false)).ToArray();
            var reader = new RdpPacketReader(joinConfirm);
            if (!McsChannelJoinConfirm.TryRead(ref reader, out McsChannelJoinConfirm confirm) ||
                confirm.Result != 0 ||
                !confirm.HasChannelId ||
                confirm.RequestedChannelId != channelId)
            {
                throw new InvalidDataException($"The server rejected MCS channel {channelId}.");
            }
        }

        byte[] clientInfo = CreateClientInfo();
        await WriteMcsSendDataAsync(userId, network.IoChannelId, clientInfo, cancellationToken).ConfigureAwait(false);

        RdpDemandActiveInfo demandActive = await WaitForDemandActiveAsync(
            userId,
            network.IoChannelId,
            cancellationToken).ConfigureAwait(false);
        await WriteMcsSendDataAsync(
            userId,
            network.IoChannelId,
            CreateConfirmActive(userId, demandActive.ShareId, demandActive.DesktopWidth, demandActive.DesktopHeight),
            cancellationToken).ConfigureAwait(false);
        await WriteMcsSendDataAsync(userId, network.IoChannelId, CreateSynchronize(userId, demandActive.ShareId), cancellationToken).ConfigureAwait(false);
        await WriteMcsSendDataAsync(userId, network.IoChannelId, CreateControl(userId, demandActive.ShareId, action: 4), cancellationToken).ConfigureAwait(false);
        await WriteMcsSendDataAsync(userId, network.IoChannelId, CreateControl(userId, demandActive.ShareId, action: 1), cancellationToken).ConfigureAwait(false);
        await WriteMcsSendDataAsync(userId, network.IoChannelId, CreateFontList(userId, demandActive.ShareId), cancellationToken).ConfigureAwait(false);
        await WaitForFontMapAsync(cancellationToken).ConfigureAwait(false);

        return new RdpActivationResult(
            userId,
            network.IoChannelId,
            demandActive.ShareId,
            network.StaticChannelIds,
            demandActive.DesktopWidth,
            demandActive.DesktopHeight);
    }

    internal byte[] CreateConnectInitial()
    {
        // Microsoft MS-RDPBCGR 4.1.3 client template. The captured three-channel
        // baseline is extended below with the DRDYNVC transport channel.
        byte[] packet = Convert.FromHexString(
            "030001A002F0807F658201940401010401010101FF30190201220201020201000201010201000201010202FFFF020102" +
            "301902010102010102010102010102010002010102020420020102301C0202FFFF0202FC170202FFFF020101020100" +
            "0201010202FFFF02010204820133000500147C0001812A000800100001C00044756361811C01C0D800040008000005" +
            "000401CA03AA09040000CE0E000045004C0054004F004E0053002D0044004500560032000000000000000000000004" +
            "000000000000000C0000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000001CA010000000000180007000100" +
            "360039003700310032002D003700380033002D0030003300350037003900370034002D003400320037003100340000" +
            "000000000000000000000000000000000000000000000000000004C00C000D0000000000000002C00C001B000000" +
            "0000000003C02C0003000000726470647200000000008080636C6970726472000000A0C0726470736E640000000000C0");

        // The annotated capture includes six alignment bytes after the fixed-size
        // Client Core Data block which are not part of its declared GCC length.
        packet = RemoveRange(packet, 348, 6);
        packet = AddDynamicVirtualChannel(packet);

        int coreOffset = FindSequence(packet, new byte[] { 0x01, 0xC0, 0xD8, 0x00 });
        if (coreOffset < 0)
        {
            throw new InvalidDataException("The built-in GCC client core template is invalid.");
        }

        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(coreOffset + 8, 2), _options.Width);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(coreOffset + 10, 2), _options.Height);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(coreOffset + 142, 2), NormalizeHighColorDepth(_options.ColorDepth));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(coreOffset + 144, 2), GetSupportedColorDepthFlag(_options.ColorDepth));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(coreOffset + 212, 4), (uint)_protocol);

        Span<byte> clientName = packet.AsSpan(coreOffset + 24, 32);
        clientName.Clear();
        Encoding.Unicode.GetBytes(Environment.MachineName.AsSpan(0, Math.Min(Environment.MachineName.Length, 15)), clientName);

        // Do not negotiate static-channel compression until an MS-RDPBCGR bulk
        // compression history is available for those channels.
        int networkOffset = FindSequence(packet, new byte[] { 0x03, 0xC0 });
        if (networkOffset >= 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(networkOffset + 16, 4), 0x80000000);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(networkOffset + 28, 4), 0xC0200000);
        }
        return packet;
    }

    private static ushort NormalizeHighColorDepth(ushort colorDepth)
    {
        return colorDepth switch
        {
            15 => 15,
            16 => 16,
            24 or 32 => 24,
            _ => 24
        };
    }

    private static ushort GetSupportedColorDepthFlag(ushort colorDepth)
    {
        return colorDepth switch
        {
            15 => 0x0001, // RNS_UD_15BPP_SUPPORT
            16 => 0x0002, // RNS_UD_16BPP_SUPPORT
            24 => 0x0004, // RNS_UD_24BPP_SUPPORT
            32 => 0x0008, // RNS_UD_32BPP_SUPPORT
            _ => 0x0004
        };
    }

    internal static RdpServerNetworkData ParseConnectResponse(byte[] packet)
    {
        ReadOnlySpan<byte> payload = GetDomainPayload(packet);
        if (payload.Length < 4 || payload[0] != 0x7F || payload[1] != 0x66)
        {
            throw new InvalidDataException("The server returned an invalid MCS Connect Response PDU.");
        }

        int networkOffset = FindSequence(payload, new byte[] { 0x03, 0x0C });
        if (networkOffset < 0 || networkOffset + 8 > payload.Length)
        {
            throw new InvalidDataException("The MCS Connect Response does not contain Server Network Data.");
        }

        ushort blockLength = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(networkOffset + 2, 2));
        if (blockLength < 8 || networkOffset + blockLength > payload.Length)
        {
            throw new InvalidDataException("The Server Network Data length is invalid.");
        }

        ushort ioChannel = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(networkOffset + 4, 2));
        ushort channelCount = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(networkOffset + 6, 2));
        int required = checked(8 + channelCount * 2);
        if (required > blockLength)
        {
            throw new InvalidDataException("The Server Network Data channel list is truncated.");
        }

        var staticChannels = new ushort[channelCount];
        for (int i = 0; i < channelCount; i++)
        {
            staticChannels[i] = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(networkOffset + 8 + i * 2, 2));
        }

        return new RdpServerNetworkData(ioChannel, staticChannels);
    }

    private byte[] CreateClientInfo()
    {
        using var stream = new MemoryStream(1024);
        using var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true);
        writer.Write((ushort)0x0040); // SEC_INFO_PKT
        writer.Write((ushort)0);
        writer.Write(0u); // CodePage is ignored when INFO_UNICODE is set.

        uint flags = 0x00000001u | // INFO_MOUSE
                     0x00000002u | // INFO_DISABLECTRLALTDEL
                     0x00000008u | // INFO_AUTOLOGON
                     0x00000010u | // INFO_UNICODE
                     0x00000020u | // INFO_MAXIMIZESHELL
                     0x00000040u | // INFO_LOGONNOTIFY
                     0x00000100u | // INFO_ENABLEWINDOWSKEY
                     0x00010000u | // INFO_LOGONERRORS
                     0x00020000u;  // INFO_MOUSE_HAS_WHEEL
        if (string.IsNullOrEmpty(_options.Password))
        {
            flags &= ~0x00000008u;
        }
        writer.Write(flags);

        string domain = _options.Domain ?? string.Empty;
        string username = _options.Username ?? string.Empty;
        string password = _options.Password ?? string.Empty;
        WriteStringByteLength(writer, domain);
        WriteStringByteLength(writer, username);
        WriteStringByteLength(writer, password);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        WriteUnicodeString(writer, domain);
        WriteUnicodeString(writer, username);
        WriteUnicodeString(writer, password);
        WriteUnicodeString(writer, string.Empty);
        WriteUnicodeString(writer, string.Empty);

        writer.Write((ushort)2); // AF_INET
        WriteLengthPrefixedUnicodeString(writer, "127.0.0.1");
        WriteLengthPrefixedUnicodeString(writer, @"C:\Windows\System32\mstscax.dll");
        writer.Write(new byte[172]); // TS_TIME_ZONE_INFORMATION
        writer.Write(0u);            // clientSessionId
        writer.Write(0x00000008u | 0x00000010u | 0x00000080u); // disable wallpaper/full-window drag/animations
        writer.Write((ushort)0);     // cbAutoReconnectLen
        return stream.ToArray();
    }

    private static void WriteStringByteLength(BinaryWriter writer, string value)
    {
        writer.Write(checked((ushort)(value.Length * 2)));
    }

    private static void WriteUnicodeString(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.Unicode.GetBytes(value));
        writer.Write((ushort)0);
    }

    private static void WriteLengthPrefixedUnicodeString(BinaryWriter writer, string value)
    {
        writer.Write(checked((ushort)((value.Length + 1) * 2)));
        WriteUnicodeString(writer, value);
    }

    private async Task<RdpDemandActiveInfo> WaitForDemandActiveAsync(
        ushort userId,
        ushort ioChannelId,
        CancellationToken cancellationToken)
    {
        var licensing = new RdpLicenseSession(_options, GetRemoteCertificate());
        while (true)
        {
            byte[] packet = await ReadTpktAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySpan<byte> userData = GetMcsUserData(packet);
            if (userData.Length < 6)
            {
                continue;
            }

            if (IsDemandActivePdu(userData))
            {
                if (!licensing.IsComplete)
                {
                    throw new InvalidDataException(
                        "The server sent Demand Active before completing the RDP licensing exchange.");
                }

                int demandActiveLength = BinaryPrimitives.ReadUInt16LittleEndian(userData);
                return ParseDemandActive(userData[..demandActiveLength]);
            }

            ushort securityFlags = BinaryPrimitives.ReadUInt16LittleEndian(userData);
            if ((securityFlags & 0x0080) != 0)
            {
                byte[]? response = licensing.ProcessServerPacket(userData[4..]);
                if (response != null)
                {
                    await WriteMcsSendDataAsync(
                        userId,
                        ioChannelId,
                        response,
                        cancellationToken).ConfigureAwait(false);
                }
                continue;
            }
        }
    }

    internal static bool IsDemandActivePdu(ReadOnlySpan<byte> userData)
    {
        if (userData.Length < 6)
        {
            return false;
        }

        int totalLength = BinaryPrimitives.ReadUInt16LittleEndian(userData);
        ushort pduType = BinaryPrimitives.ReadUInt16LittleEndian(userData.Slice(2, 2));
        return totalLength >= 18 &&
               totalLength <= userData.Length &&
               (pduType & 0x000F) == 0x0001;
    }

    internal static RdpDemandActiveInfo ParseDemandActive(ReadOnlySpan<byte> pdu)
    {
        if (pdu.Length < 18)
        {
            throw new InvalidDataException("The Demand Active PDU is truncated.");
        }

        uint shareId = BinaryPrimitives.ReadUInt32LittleEndian(pdu.Slice(6, 4));
        int sourceDescriptorLength = BinaryPrimitives.ReadUInt16LittleEndian(pdu.Slice(10, 2));
        int combinedCapabilitiesLength = BinaryPrimitives.ReadUInt16LittleEndian(pdu.Slice(12, 2));
        int combinedCapabilitiesOffset = checked(14 + sourceDescriptorLength);
        int combinedCapabilitiesEnd = checked(combinedCapabilitiesOffset + combinedCapabilitiesLength);
        if (combinedCapabilitiesLength < 4 ||
            combinedCapabilitiesOffset > pdu.Length - 4 ||
            combinedCapabilitiesEnd > pdu.Length)
        {
            throw new InvalidDataException("The Demand Active combined capability set is truncated.");
        }

        ushort capabilityCount = BinaryPrimitives.ReadUInt16LittleEndian(
            pdu.Slice(combinedCapabilitiesOffset, 2));
        int capabilityOffset = combinedCapabilitiesOffset + 4;
        ushort desktopWidth = 0;
        ushort desktopHeight = 0;
        for (int i = 0; i < capabilityCount; i++)
        {
            if (capabilityOffset > combinedCapabilitiesEnd - 4)
            {
                throw new InvalidDataException("The Demand Active capability list is truncated.");
            }

            ushort capabilityType = BinaryPrimitives.ReadUInt16LittleEndian(
                pdu.Slice(capabilityOffset, 2));
            int capabilityLength = BinaryPrimitives.ReadUInt16LittleEndian(
                pdu.Slice(capabilityOffset + 2, 2));
            if (capabilityLength < 4 || capabilityOffset > combinedCapabilitiesEnd - capabilityLength)
            {
                throw new InvalidDataException("The Demand Active capability has an invalid length.");
            }

            if (capabilityType == 2)
            {
                if (capabilityLength < 28)
                {
                    throw new InvalidDataException("The server Bitmap Capability Set is truncated.");
                }

                desktopWidth = BinaryPrimitives.ReadUInt16LittleEndian(
                    pdu.Slice(capabilityOffset + 12, 2));
                desktopHeight = BinaryPrimitives.ReadUInt16LittleEndian(
                    pdu.Slice(capabilityOffset + 14, 2));
                if (desktopWidth == 0 || desktopHeight == 0)
                {
                    throw new InvalidDataException(
                        "The server Bitmap Capability Set contains an invalid desktop size.");
                }

            }

            capabilityOffset += capabilityLength;
        }

        return desktopWidth != 0 && desktopHeight != 0
            ? new RdpDemandActiveInfo(shareId, desktopWidth, desktopHeight)
            : throw new InvalidDataException("The Demand Active PDU omitted the Bitmap Capability Set.");
    }

    private System.Security.Cryptography.X509Certificates.X509Certificate2? GetRemoteCertificate()
    {
        return _transport.RemoteCertificate;
    }

    internal byte[] CreateConfirmActive(ushort userId, uint shareId)
    {
        return CreateConfirmActive(userId, shareId, _options.Width, _options.Height);
    }

    internal byte[] CreateConfirmActive(
        ushort userId,
        uint shareId,
        ushort desktopWidth,
        ushort desktopHeight)
    {
        byte[] pdu = Convert.FromHexString(
            "EC011300EF03EA030100EA030600D6014D53545343001200000001001800010003000002000000001D040000000000000000" +
            "02001C00180001000100010000050004000001000100000001000000030058000000000000000000000000000000000000" +
            "000000000000010014000000010000002A0001010101010000010101000100000001010101010101010001010100000000" +
            "00A106000000000000000084030000000000E404000013002800030000037800000078000000FB0900800000000000000000" +
            "00000000000000000000000000000A0008000600000007000C00000000000000000005000C00000000000200020008000A" +
            "0001001400150009000800000000000D005800150020000904000004000000000000000C0000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000C000800010000000E0008000100000010003400FE000400FE000400FE000800FE" +
            "000800FE001000FE002000FE004000FE008000FE0000014000000800010001030000000F0008000100000011000C000100" +
            "0000001E6400140008000100000015000C0002000000000A0001160028000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000");

        // Remove padding copied from the annotated dump after four fixed-size
        // capability sets. Their length fields and the enclosing Confirm Active
        // length already describe the canonical structures.
        pdu = RemoveRange(pdu, 509, 8);
        pdu = RemoveRange(pdu, 350, 11);
        pdu = RemoveRange(pdu, 210, 2);
        pdu = RemoveRange(pdu, 166, 4);

        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(4, 2), userId);
        BinaryPrimitives.WriteUInt32LittleEndian(pdu.AsSpan(6, 4), shareId);
        int bitmap = FindSequence(pdu, new byte[] { 0x02, 0x00, 0x1C, 0x00 });
        if (bitmap >= 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                pdu.AsSpan(bitmap + 4, 2),
                NormalizeBitmapColorDepth(_options.ColorDepth));
            BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(bitmap + 12, 2), desktopWidth);
            BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(bitmap + 14, 2), desktopHeight);
        }

        int order = FindSequence(pdu, new byte[] { 0x03, 0x00, 0x58, 0x00 });
        if (order >= 0)
        {
            // This client currently renders bitmap updates only. Advertising an
            // all-zero orderSupport array makes the server fall back to bitmap
            // updates instead of emitting drawing orders that cannot be applied.
            pdu.AsSpan(order + 36, 32).Clear();
        }
        return pdu;
    }

    private static ushort NormalizeBitmapColorDepth(ushort colorDepth)
    {
        return colorDepth is 15 or 16 or 24 or 32 ? colorDepth : (ushort)24;
    }

    private static byte[] CreateSynchronize(ushort userId, uint shareId)
    {
        ushort targetUser = checked((ushort)(shareId & 0xFFFF));
        byte[] body = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(body, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(2), targetUser);
        return CreateShareDataPdu(userId, shareId, 31, body);
    }

    private static byte[] CreateControl(ushort userId, uint shareId, ushort action)
    {
        byte[] body = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(body, action);
        return CreateShareDataPdu(userId, shareId, 20, body);
    }

    private static byte[] CreateFontList(ushort userId, uint shareId)
    {
        byte[] body = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(4), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(6), 50);
        return CreateShareDataPdu(userId, shareId, 39, body);
    }

    private static byte[] CreateShareDataPdu(ushort userId, uint shareId, byte pduType2, byte[] body)
    {
        byte[] pdu = new byte[18 + body.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(pdu, checked((ushort)pdu.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(2), 0x0017);
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(4), userId);
        BinaryPrimitives.WriteUInt32LittleEndian(pdu.AsSpan(6), shareId);
        pdu[11] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(pdu.AsSpan(12), checked((ushort)(body.Length + 4)));
        pdu[14] = pduType2;
        body.CopyTo(pdu, 18);
        return pdu;
    }

    internal static byte[] CreateSlowPathInputPacket(
        ushort userId,
        ushort ioChannelId,
        uint shareId,
        in RdpInputEvent inputEvent)
    {
        byte[] inputBody = new byte[4 + RdpInputEvent.EventLength];
        var inputWriter = new RdpPacketWriter(inputBody);
        RdpInputPduWriter.WriteSlowPathHeader(ref inputWriter, 1);
        inputEvent.Write(ref inputWriter);

        byte[] shareData = CreateShareDataPdu(userId, shareId, 28, inputBody);
        return CreateMcsSendDataPacket(userId, ioChannelId, shareData);
    }

    private async Task WaitForFontMapAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            byte[] packet = await ReadTpktAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySpan<byte> data = GetMcsUserData(packet);
            if (data.Length >= 18 &&
                (BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2, 2)) & 0x000F) == 0x0007 &&
                data[14] == 40)
            {
                return;
            }
        }
    }

    private async Task WriteMcsSendDataAsync(
        ushort initiator,
        ushort channelId,
        byte[] userData,
        CancellationToken cancellationToken)
    {
        await WriteAsync(CreateMcsSendDataPacket(initiator, channelId, userData), cancellationToken).ConfigureAwait(false);
    }

    internal static byte[] CreateMcsSendDataPacket(ushort initiator, ushort channelId, byte[] userData)
    {
        using var stream = new MemoryStream(userData.Length + 16);
        stream.WriteByte(0x64);
        WriteUInt16BigEndian(stream, checked((ushort)(initiator - 1001)));
        WriteUInt16BigEndian(stream, channelId);
        stream.WriteByte(0x70);
        WritePerLength(stream, userData.Length);
        stream.Write(userData);
        return CreateDomainPacket(stream.ToArray());
    }

    private async Task WriteDomainPduAsync(byte[] domainPdu, CancellationToken cancellationToken)
    {
        await WriteAsync(CreateDomainPacket(domainPdu), cancellationToken).ConfigureAwait(false);
    }

    private static byte[] CreateDomainPacket(byte[] domainPdu)
    {
        byte[] packet = new byte[7 + domainPdu.Length];
        packet[0] = 3;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), checked((ushort)packet.Length));
        packet[4] = 2;
        packet[5] = 0xF0;
        packet[6] = 0x80;
        domainPdu.CopyTo(packet, 7);
        return packet;
    }

    private async Task WriteAsync(byte[] packet, CancellationToken cancellationToken)
    {
        await _stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadTpktAsync(CancellationToken cancellationToken)
    {
        byte[] header = new byte[4];
        await ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        if (header[0] != 3 || header[1] != 0)
        {
            throw new InvalidDataException("Expected a TPKT-framed RDP connection-sequence packet.");
        }

        int packetLength = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));
        if (packetLength < 7 || packetLength > MaximumPacketLength)
        {
            throw new InvalidDataException($"Invalid TPKT packet length {packetLength}.");
        }

        byte[] packet = new byte[packetLength];
        header.CopyTo(packet, 0);
        await ReadExactlyAsync(packet.AsMemory(4), cancellationToken).ConfigureAwait(false);
        return packet;
    }

    private async Task ReadExactlyAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < destination.Length)
        {
            int read = await _stream.ReadAsync(destination[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("The RDP server closed the connection during session activation.");
            }
            offset += read;
        }
    }

    private static ReadOnlySpan<byte> GetDomainPayload(byte[] packet)
    {
        if (packet.Length < 7 || packet[4] != 2 || packet[5] != 0xF0 || packet[6] != 0x80)
        {
            throw new InvalidDataException("The TPKT packet does not contain an X.224 Data TPDU.");
        }
        return packet.AsSpan(7);
    }

    private static ReadOnlySpan<byte> GetMcsUserData(byte[] packet)
    {
        ReadOnlySpan<byte> payload = GetDomainPayload(packet);
        if (payload.Length < 7 || payload[0] != 0x68)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        int offset = 6;
        int length = ReadPerLength(payload, ref offset);
        return length >= 0 && offset + length <= payload.Length
            ? payload.Slice(offset, length)
            : ReadOnlySpan<byte>.Empty;
    }

    private static int ReadPerLength(ReadOnlySpan<byte> source, ref int offset)
    {
        if (offset >= source.Length)
        {
            return -1;
        }
        byte first = source[offset++];
        if ((first & 0x80) == 0)
        {
            return first;
        }
        if (offset >= source.Length)
        {
            return -1;
        }
        return ((first & 0x3F) << 8) | source[offset++];
    }

    private static void WritePerLength(Stream stream, int length)
    {
        if (length < 0x80)
        {
            stream.WriteByte((byte)length);
            return;
        }
        if (length > 0x3FFF)
        {
            throw new InvalidDataException("MCS user data exceeds the two-byte PER length range.");
        }
        stream.WriteByte((byte)(0x80 | (length >> 8)));
        stream.WriteByte((byte)length);
    }

    private static void WriteUInt16BigEndian(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static int FindSequence(ReadOnlySpan<byte> source, ReadOnlySpan<byte> sequence)
    {
        return source.IndexOf(sequence);
    }

    private static byte[] RemoveRange(byte[] source, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > source.Length)
        {
            throw new InvalidDataException("The built-in RDP activation template is invalid.");
        }

        byte[] result = new byte[source.Length - count];
        source.AsSpan(0, offset).CopyTo(result);
        source.AsSpan(offset + count).CopyTo(result.AsSpan(offset));
        return result;
    }

    private static byte[] AddDynamicVirtualChannel(byte[] packet)
    {
        int networkOffset = FindSequence(packet, new byte[] { 0x03, 0xC0, 0x2C, 0x00 });
        if (networkOffset < 0)
        {
            throw new InvalidDataException("The built-in GCC client network template is invalid.");
        }

        ushort networkLength = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(networkOffset + 2, 2));
        int insertionOffset = checked(networkOffset + networkLength);
        byte[] channelDefinition =
        [
            (byte)'d', (byte)'r', (byte)'d', (byte)'y',
            (byte)'n', (byte)'v', (byte)'c', 0,
            0, 0, 0, 0x80 // CHANNEL_OPTION_INITIALIZED
        ];

        byte[] result = new byte[packet.Length + channelDefinition.Length];
        packet.AsSpan(0, insertionOffset).CopyTo(result);
        channelDefinition.CopyTo(result, insertionOffset);
        packet.AsSpan(insertionOffset).CopyTo(result.AsSpan(insertionOffset + channelDefinition.Length));

        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(2, 2), checked((ushort)result.Length));
        WriteTwoByteBerLength(result, 9, checked(result.Length - 12));

        int gccUserDataOffset = FindSequence(result, new byte[] { 0x04, 0x82, 0x01, 0x33 });
        if (gccUserDataOffset < 0)
        {
            throw new InvalidDataException("The built-in GCC user-data template is invalid.");
        }
        WriteTwoByteBerLength(result, gccUserDataOffset + 1, 307 + channelDefinition.Length);

        int gccLengthOffset = FindSequence(result, new byte[] { 0x00, 0x05, 0x00, 0x14, 0x7C, 0x00, 0x01 });
        if (gccLengthOffset < 0)
        {
            throw new InvalidDataException("The built-in GCC conference template is invalid.");
        }
        WriteTwoBytePerLength(result, gccLengthOffset + 7, 298 + channelDefinition.Length);

        int coreOffset = FindSequence(result, new byte[] { 0x01, 0xC0, 0xD8, 0x00 });
        if (coreOffset < 2)
        {
            throw new InvalidDataException("The built-in GCC core template is invalid.");
        }
        WriteTwoBytePerLength(result, coreOffset - 2, 284 + channelDefinition.Length);

        BinaryPrimitives.WriteUInt16LittleEndian(
            result.AsSpan(networkOffset + 2, 2),
            checked((ushort)(networkLength + channelDefinition.Length)));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(networkOffset + 4, 4), 4);
        return result;
    }

    private static void WriteTwoByteBerLength(byte[] destination, int offset, int length)
    {
        destination[offset] = 0x82;
        BinaryPrimitives.WriteUInt16BigEndian(destination.AsSpan(offset + 1, 2), checked((ushort)length));
    }

    private static void WriteTwoBytePerLength(byte[] destination, int offset, int length)
    {
        if (length is < 0x80 or > 0x3FFF)
        {
            throw new InvalidDataException("The GCC payload length is outside the two-byte PER range.");
        }

        destination[offset] = (byte)(0x80 | (length >> 8));
        destination[offset + 1] = (byte)length;
    }

    internal readonly record struct RdpServerNetworkData(ushort IoChannelId, ushort[] StaticChannelIds);
}

internal sealed record RdpActivationResult(
    ushort UserId,
    ushort IoChannelId,
    uint ShareId,
    IReadOnlyList<ushort> StaticChannelIds,
    ushort DesktopWidth,
    ushort DesktopHeight);

internal readonly record struct RdpDemandActiveInfo(
    uint ShareId,
    ushort DesktopWidth,
    ushort DesktopHeight);
