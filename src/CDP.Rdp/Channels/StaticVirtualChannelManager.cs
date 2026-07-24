namespace CDP.Rdp.Channels;

using System;
using System.Collections.Generic;
using CDP.Rdp.Protocol;

/// <summary>
/// Handler callback delegate for reassembled Static Virtual Channel messages.
/// </summary>
public delegate void StaticChannelDataCallback(ushort channelId, ReadOnlySpan<byte> messagePayload);

/// <summary>
/// Static Virtual Channel Manager handling channel registration, chunking, and multi-chunk reassembly (MS-RDPBCGR Section 2.2.6).
/// </summary>
public sealed class StaticVirtualChannelManager
{
    private readonly Dictionary<string, ushort> _channelIdsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, string> _channelNamesById = new();
    private readonly Dictionary<ushort, StaticChannelDataCallback> _callbacks = new();
    private readonly Dictionary<ushort, ReassemblyBuffer> _reassemblyBuffers = new();

    public const uint MaxMessageSize = 16 * 1024 * 1024;

    private sealed class ReassemblyBuffer
    {
        public uint ExpectedLength { get; set; }
        public byte[] Buffer = Array.Empty<byte>();
        public int CurrentPosition { get; set; }

        public bool Reset(uint expectedLength)
        {
            if (expectedLength > MaxMessageSize || expectedLength == 0)
            {
                ExpectedLength = 0;
                CurrentPosition = 0;
                return false;
            }
            ExpectedLength = expectedLength;
            if (Buffer.Length < expectedLength)
            {
                Buffer = new byte[expectedLength];
            }
            CurrentPosition = 0;
            return true;
        }

        public bool Append(ReadOnlySpan<byte> data)
        {
            if (CurrentPosition + data.Length > MaxMessageSize || (ExpectedLength > 0 && CurrentPosition + data.Length > ExpectedLength))
            {
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
    /// Registers a static virtual channel with name, channel ID, and optional data callback.
    /// </summary>
    public void RegisterChannel(string name, ushort channelId, StaticChannelDataCallback? callback = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        _channelIdsByName[name] = channelId;
        _channelNamesById[channelId] = name;

        if (callback != null)
        {
            _callbacks[channelId] = callback;
        }
    }

    /// <summary>
    /// Try get channel ID by registered channel name.
    /// </summary>
    public bool TryGetChannelId(string name, out ushort channelId)
    {
        return _channelIdsByName.TryGetValue(name, out channelId);
    }

    /// <summary>
    /// Try get channel name by registered channel ID.
    /// </summary>
    public bool TryGetChannelName(ushort channelId, out string? name)
    {
        return _channelNamesById.TryGetValue(channelId, out name);
    }

    /// <summary>
    /// Processes an incoming SVC packet for a given channel ID.
    /// Reassembles multi-chunk messages and invokes registered callbacks upon completion.
    /// </summary>
    public bool ProcessIncomingPacket(ushort channelId, ReadOnlySpan<byte> packetData)
    {
        var reader = new RdpPacketReader(packetData);
        if (!ChannelPduHeader.TryRead(ref reader, out var header))
        {
            return false;
        }

        ReadOnlySpan<byte> chunkPayload = reader.ReadSpan(reader.UnreadLength);
        bool isFirst = (header.Flags & ChannelPduFlags.First) != 0;
        bool isLast = (header.Flags & ChannelPduFlags.Last) != 0;

        if (isFirst && isLast)
        {
            // Single chunk message
            if (_callbacks.TryGetValue(channelId, out var callback))
            {
                callback(channelId, chunkPayload);
            }
            return true;
        }

        if (isFirst)
        {
            if (!_reassemblyBuffers.TryGetValue(channelId, out var buf))
            {
                buf = new ReassemblyBuffer();
                _reassemblyBuffers[channelId] = buf;
            }

            if (!buf.Reset(header.Length) || !buf.Append(chunkPayload))
            {
                _reassemblyBuffers.Remove(channelId);
                return false;
            }
        }
        else
        {
            if (!_reassemblyBuffers.TryGetValue(channelId, out var buf) || buf.ExpectedLength == 0)
            {
                return false;
            }

            if (!buf.Append(chunkPayload))
            {
                _reassemblyBuffers.Remove(channelId);
                return false;
            }
        }

        if (isLast)
        {
            if (_reassemblyBuffers.TryGetValue(channelId, out var buf))
            {
                if (_callbacks.TryGetValue(channelId, out var callback))
                {
                    callback(channelId, buf.Buffer.AsSpan(0, buf.CurrentPosition));
                }
                buf.CurrentPosition = 0;
                buf.ExpectedLength = 0;
            }
        }

        return true;
    }

    /// <summary>
    /// Chunks an outbound virtual channel payload into one or more SVC PDUs.
    /// </summary>
    public static void ChunkMessage(
        ReadOnlySpan<byte> messagePayload,
        int maxChunkSize,
        Action<ReadOnlySpan<byte>> sendChunkCallback)
    {
        ArgumentNullException.ThrowIfNull(sendChunkCallback);
        if (maxChunkSize <= ChannelPduHeader.HeaderLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChunkSize), "Max chunk size must exceed header size.");
        }

        int maxPayloadPerChunk = maxChunkSize - ChannelPduHeader.HeaderLength;
        uint totalLength = (uint)messagePayload.Length;

        if (messagePayload.Length <= maxPayloadPerChunk)
        {
            // Single chunk
            Span<byte> chunkBuffer = stackalloc byte[ChannelPduHeader.HeaderLength + messagePayload.Length];
            var writer = new RdpPacketWriter(chunkBuffer);
            var header = new ChannelPduHeader(totalLength, ChannelPduFlags.First | ChannelPduFlags.Last);
            header.Write(ref writer);
            writer.WriteSpan(messagePayload);

            sendChunkCallback(chunkBuffer);
            return;
        }

        // Multi-chunk
        int offset = 0;
        byte[] chunkBufferHeap = new byte[maxChunkSize];

        while (offset < messagePayload.Length)
        {
            int remaining = messagePayload.Length - offset;
            int currentPayloadSize = Math.Min(remaining, maxPayloadPerChunk);

            ChannelPduFlags flags = ChannelPduFlags.None;
            if (offset == 0) flags |= ChannelPduFlags.First;
            if (offset + currentPayloadSize >= messagePayload.Length) flags |= ChannelPduFlags.Last;

            var writer = new RdpPacketWriter(chunkBufferHeap);
            var header = new ChannelPduHeader(totalLength, flags);
            header.Write(ref writer);
            writer.WriteSpan(messagePayload.Slice(offset, currentPayloadSize));

            sendChunkCallback(chunkBufferHeap.AsSpan(0, writer.WrittenCount));
            offset += currentPayloadSize;
        }
    }
}
