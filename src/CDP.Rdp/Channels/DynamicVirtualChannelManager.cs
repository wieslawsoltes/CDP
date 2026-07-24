namespace CDP.Rdp.Channels;

using System;
using System.Collections.Generic;
using System.Text;
using CDP.Rdp.Protocol;

/// <summary>
/// Delegate callback for receiving reassembled DVC messages.
/// </summary>
public delegate void DynamicChannelDataCallback(uint channelId, ReadOnlySpan<byte> payload);

/// <summary>
/// Dynamic Virtual Channel Manager implementing DRDYNVC protocol capabilities negotiation, channel creation, closing, and data framing (MS-RDPEDYC).
/// </summary>
public sealed class DynamicVirtualChannelManager
{
    private readonly Dictionary<string, DynamicChannelDataCallback> _registeredHandlersByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, DynamicChannelDataCallback> _activeCallbacksByChannelId = new();
    private readonly Dictionary<uint, string> _activeChannelNamesById = new();
    private readonly Dictionary<uint, DvcReassemblyBuffer> _reassemblyBuffers = new();

    public ushort NegotiatedVersion { get; private set; } = 1;

    public const uint MaxMessageSize = 16 * 1024 * 1024;

    private static bool IsValidChannelName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (char c in name)
        {
            if (c < 0x20 || c > 0x7E) return false;
        }
        return true;
    }

    private sealed class DvcReassemblyBuffer
    {
        public uint TotalLength { get; set; }
        public byte[] Buffer = Array.Empty<byte>();
        public int CurrentPosition { get; set; }
        public bool IsInvalid { get; set; }

        public bool Reset(uint totalLength)
        {
            if (totalLength > MaxMessageSize || totalLength == 0)
            {
                TotalLength = 0;
                CurrentPosition = 0;
                IsInvalid = true;
                return false;
            }
            TotalLength = totalLength;
            CurrentPosition = 0;
            IsInvalid = false;
            if (Buffer.Length < totalLength)
            {
                Buffer = new byte[totalLength];
            }
            return true;
        }

        public bool Append(ReadOnlySpan<byte> data)
        {
            if (IsInvalid || CurrentPosition + data.Length > MaxMessageSize || (TotalLength > 0 && CurrentPosition + data.Length > TotalLength))
            {
                IsInvalid = true;
                return false;
            }
            if (CurrentPosition + data.Length > Buffer.Length)
            {
                int newCap = Math.Max(Buffer.Length * 2, CurrentPosition + data.Length);
                Array.Resize(ref Buffer, newCap);
            }
            data.CopyTo(Buffer.AsSpan(CurrentPosition));
            CurrentPosition += data.Length;
            return true;
        }
    }

    /// <summary>
    /// Registers a handler for a named DVC channel (e.g., "AUDIO_INPUT", "RAIL").
    /// </summary>
    public void RegisterHandler(string channelName, DynamicChannelDataCallback callback)
    {
        ArgumentNullException.ThrowIfNull(channelName);
        ArgumentNullException.ThrowIfNull(callback);
        _registeredHandlersByName[channelName] = callback;
    }

    /// <summary>
    /// Processes an incoming DRDYNVC packet payload.
    /// </summary>
    public bool ProcessIncomingPacket(ReadOnlySpan<byte> packet, Action<ReadOnlySpan<byte>>? replyCallback = null)
    {
        var reader = new RdpPacketReader(packet);
        if (reader.UnreadLength < 1) return false;

        byte headerByte = reader.ReadByte();
        var cmd = (DvcCommandCode)(headerByte & 0x0F);
        byte sp = (byte)((headerByte >> 4) & 0x03);
        byte pri = (byte)((headerByte >> 6) & 0x03);

        // Reset reader to include header byte for full PDU parsing
        reader = new RdpPacketReader(packet);

        switch (cmd)
        {
            case DvcCommandCode.Capabilities:
                if (DvcCapabilitiesPdu.TryRead(ref reader, out var caps))
                {
                    NegotiatedVersion = caps.Version;
                    return true;
                }
                return false;

            case DvcCommandCode.Create:
                var checkReqReader = new RdpPacketReader(packet);
                if (DvcCreateRequestPdu.TryRead(ref checkReqReader, out var req) && IsValidChannelName(req.ChannelName))
                {
                    int status = 0; // STATUS_SUCCESS
                    if (_registeredHandlersByName.TryGetValue(req.ChannelName, out var handler))
                    {
                        _activeCallbacksByChannelId[req.ChannelId] = handler;
                        _activeChannelNamesById[req.ChannelId] = req.ChannelName;
                    }
                    else
                    {
                        status = unchecked((int)0xC0000001); // STATUS_UNSUCCESSFUL
                    }

                    if (replyCallback != null)
                    {
                        byte[] rspBuf = new byte[16];
                        var writer = new RdpPacketWriter(rspBuf);
                        var responsePdu = new DvcCreateResponsePdu(req.ChannelId, status, req.Priority);
                        responsePdu.Write(ref writer);
                        replyCallback(rspBuf.AsSpan(0, writer.WrittenCount));
                    }
                    return true;
                }

                var checkRspReader = new RdpPacketReader(packet);
                _ = DvcHeader.TryRead(ref checkRspReader, out _);
                _ = DvcValueCodec.TryReadValue(ref checkRspReader, sp, out _);

                if (checkRspReader.UnreadLength == 4)
                {
                    var rspReader = new RdpPacketReader(packet);
                    if (DvcCreateResponsePdu.TryRead(ref rspReader, out var rsp))
                    {
                        return true;
                    }
                }
                return false;

            case DvcCommandCode.Close:
                if (DvcClosePdu.TryRead(ref reader, out var closePdu))
                {
                    _activeCallbacksByChannelId.Remove(closePdu.ChannelId);
                    _activeChannelNamesById.Remove(closePdu.ChannelId);
                    _reassemblyBuffers.Remove(closePdu.ChannelId);
                    return true;
                }
                return false;

            case DvcCommandCode.DataFirst:
                if (DvcDataFirstHeader.TryRead(ref reader, out var firstHeader))
                {
                    if (!_reassemblyBuffers.TryGetValue(firstHeader.ChannelId, out var buf))
                    {
                        buf = new DvcReassemblyBuffer();
                        _reassemblyBuffers[firstHeader.ChannelId] = buf;
                    }
                    if (!buf.Reset(firstHeader.TotalLength))
                    {
                        return false;
                    }
                    ReadOnlySpan<byte> chunkPayload = reader.ReadSpan(reader.UnreadLength);
                    if (!buf.Append(chunkPayload))
                    {
                        buf.IsInvalid = true;
                        buf.TotalLength = 0;
                        return false;
                    }

                    if (buf.CurrentPosition >= buf.TotalLength)
                    {
                        if (_activeCallbacksByChannelId.TryGetValue(firstHeader.ChannelId, out var handler))
                        {
                            handler(firstHeader.ChannelId, buf.Buffer.AsSpan(0, buf.CurrentPosition));
                        }
                        buf.CurrentPosition = 0;
                        buf.TotalLength = 0;
                    }
                    return true;
                }
                return false;

            case DvcCommandCode.Data:
                if (DvcDataHeader.TryRead(ref reader, out var dataHeader))
                {
                    ReadOnlySpan<byte> chunkPayload = reader.ReadSpan(reader.UnreadLength);

                    if (_reassemblyBuffers.TryGetValue(dataHeader.ChannelId, out var buf))
                    {
                        if (buf.IsInvalid || buf.TotalLength == 0)
                        {
                            return false;
                        }

                        if (!buf.Append(chunkPayload))
                        {
                            buf.IsInvalid = true;
                            buf.TotalLength = 0;
                            return false;
                        }

                        if (buf.CurrentPosition >= buf.TotalLength)
                        {
                            if (_activeCallbacksByChannelId.TryGetValue(dataHeader.ChannelId, out var handler))
                            {
                                handler(dataHeader.ChannelId, buf.Buffer.AsSpan(0, buf.CurrentPosition));
                            }
                            buf.CurrentPosition = 0;
                            buf.TotalLength = 0;
                        }
                        return true;
                    }
                    else
                    {
                        // Single chunk Data PDU
                        if (_activeCallbacksByChannelId.TryGetValue(dataHeader.ChannelId, out var handler))
                        {
                            handler(dataHeader.ChannelId, chunkPayload);
                            return true;
                        }
                        return false;
                    }
                }
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Chunks and sends a DVC data message for a specific channel ID.
    /// </summary>
    public static void SendDvcData(
        uint channelId,
        ReadOnlySpan<byte> payload,
        int maxChunkSize,
        Action<ReadOnlySpan<byte>> sendPacket)
    {
        ArgumentNullException.ThrowIfNull(sendPacket);

        byte sp = DvcValueCodec.GetRequiredSp(channelId);
        int headerOverhead = 1 + (sp == 0 ? 1 : sp == 1 ? 2 : 4);

        if (payload.Length <= maxChunkSize - headerOverhead)
        {
            // Single chunk DVC Data PDU
            byte[] buf = new byte[headerOverhead + payload.Length];
            var writer = new RdpPacketWriter(buf);
            var header = new DvcDataHeader(channelId);
            header.Write(ref writer);
            writer.WriteSpan(payload);
            sendPacket(buf.AsSpan(0, writer.WrittenCount));
            return;
        }

        // Multi-chunk DVC message (DataFirst + Data)
        byte lenSp = DvcValueCodec.GetRequiredSp((uint)payload.Length);
        int firstHeaderOverhead = headerOverhead + (lenSp == 0 ? 1 : lenSp == 1 ? 2 : 4);
        int firstChunkMaxPayload = maxChunkSize - firstHeaderOverhead;

        byte[] firstBuf = new byte[maxChunkSize];
        var firstWriter = new RdpPacketWriter(firstBuf);
        var firstHeader = new DvcDataFirstHeader(channelId, (uint)payload.Length);
        firstHeader.Write(ref firstWriter);

        int firstPayloadSize = Math.Min(payload.Length, firstChunkMaxPayload);
        firstWriter.WriteSpan(payload.Slice(0, firstPayloadSize));
        sendPacket(firstBuf.AsSpan(0, firstWriter.WrittenCount));

        int offset = firstPayloadSize;
        int dataChunkMaxPayload = maxChunkSize - headerOverhead;
        byte[] dataBuf = new byte[maxChunkSize];

        while (offset < payload.Length)
        {
            int remaining = payload.Length - offset;
            int currentPayloadSize = Math.Min(remaining, dataChunkMaxPayload);

            var dataWriter = new RdpPacketWriter(dataBuf);
            var dataHeader = new DvcDataHeader(channelId);
            dataHeader.Write(ref dataWriter);
            dataWriter.WriteSpan(payload.Slice(offset, currentPayloadSize));

            sendPacket(dataBuf.AsSpan(0, dataWriter.WrittenCount));
            offset += currentPayloadSize;
        }
    }
}
