namespace CDP.Rdp.Frames;

using System;
using System.Collections.Generic;
using CDP.Rdp.Protocol;

public enum FastPathUpdateCode : byte
{
    Orders = 0x0,
    Bitmap = 0x1,
    Palette = 0x2,
    Synchronize = 0x3,
    SurfaceCommands = 0x4,
    PtrNull = 0x5,
    PtrDefault = 0x6,
    PtrPosition = 0x8,
    ColorPointer = 0x9,
    CachedPointer = 0xA,
    NewPointer = 0xB
}

public enum FastPathFragmentation : byte
{
    Single = 0x0,
    Last = 0x1,
    First = 0x2,
    Next = 0x3
}

public readonly struct FastPathServerHeader
{
    public byte Action { get; }
    public byte EncryptionFlags { get; }
    public byte CompressionFlags { get; }
    public ushort PacketLength { get; }
    public int HeaderLength { get; }

    public FastPathServerHeader(byte action, byte encryptionFlags, byte compressionFlags, ushort packetLength, int headerLength)
    {
        Action = action;
        EncryptionFlags = encryptionFlags;
        CompressionFlags = compressionFlags;
        PacketLength = packetLength;
        HeaderLength = headerLength;
    }
}

public readonly struct FastPathUpdateHeader
{
    public FastPathUpdateCode UpdateCode { get; }
    public FastPathFragmentation Fragmentation { get; }
    public bool IsCompressed { get; }
    public byte CompressionFlags { get; }
    public ushort UpdateSize { get; }
    public int HeaderLength { get; }

    public FastPathUpdateHeader(
        FastPathUpdateCode updateCode,
        FastPathFragmentation fragmentation,
        bool isCompressed,
        byte compressionFlags,
        ushort updateSize,
        int headerLength)
    {
        UpdateCode = updateCode;
        Fragmentation = fragmentation;
        IsCompressed = isCompressed;
        CompressionFlags = compressionFlags;
        UpdateSize = updateSize;
        HeaderLength = headerLength;
    }
}

/// <summary>
/// Safe, zero-allocation reader facade for FastPath frame and bitmap update PDUs.
/// </summary>
public static class RdpFastPathFrameReader
{
    public static bool TryReadFastPathHeader(ref RdpPacketReader reader, out FastPathServerHeader header)
    {
        if (reader.UnreadLength < 2)
        {
            header = default;
            return false;
        }

        byte b1 = reader.ReadByte();
        byte action = (byte)(b1 & 0x03);
        byte encFlags = (byte)((b1 >> 2) & 0x03);
        byte compFlags = (byte)(b1 & 0xC0);

        byte b2 = reader.ReadByte();
        ushort length;
        int headerLen;

        if ((b2 & 0x80) != 0)
        {
            if (reader.UnreadLength < 1)
            {
                header = default;
                return false;
            }
            byte b3 = reader.ReadByte();
            length = (ushort)(((b2 & 0x7F) << 8) | b3);
            headerLen = 3;
        }
        else
        {
            length = b2;
            headerLen = 2;
        }

        if (length < headerLen)
        {
            header = default;
            return false;
        }

        header = new FastPathServerHeader(action, encFlags, compFlags, length, headerLen);
        return true;
    }

    public static bool TryReadUpdateHeader(ref RdpPacketReader reader, out FastPathUpdateHeader updateHeader)
    {
        if (reader.UnreadLength < 1)
        {
            updateHeader = default;
            return false;
        }

        byte b = reader.ReadByte();
        FastPathUpdateCode code = (FastPathUpdateCode)(b & 0x0F);
        FastPathFragmentation frag = (FastPathFragmentation)((b >> 4) & 0x03);
        bool isComp = ((b >> 6) & 0x03) == 0x2;

        byte compFlags = 0;
        int headerLen = 1;

        if (isComp)
        {
            if (reader.UnreadLength < 1)
            {
                updateHeader = default;
                return false;
            }
            compFlags = reader.ReadByte();
            headerLen++;
        }

        if (reader.UnreadLength < 2)
        {
            updateHeader = default;
            return false;
        }

        ushort size = reader.ReadUInt16LE();
        headerLen += 2;

        updateHeader = new FastPathUpdateHeader(code, frag, isComp, compFlags, size, headerLen);
        return true;
    }

    public static bool TryReadBitmapUpdateData(
        ref RdpPacketReader reader,
        ReadOnlyMemory<byte> packetBuffer,
        out List<RdpBitmapUpdate> updates)
    {
        updates = new List<RdpBitmapUpdate>();

        if (reader.UnreadLength < 4)
            return false;

        ushort updateType = reader.ReadUInt16LE();
        if (updateType != 0x0001) // UPDATETYPE_BITMAP
            return false;

        ushort numberRectangles = reader.ReadUInt16LE();

        for (int i = 0; i < numberRectangles; i++)
        {
            if (reader.UnreadLength < 18)
                return false;

            ushort destLeft = reader.ReadUInt16LE();
            ushort destTop = reader.ReadUInt16LE();
            ushort destRight = reader.ReadUInt16LE();
            ushort destBottom = reader.ReadUInt16LE();
            ushort width = reader.ReadUInt16LE();
            ushort height = reader.ReadUInt16LE();
            ushort bpp = reader.ReadUInt16LE();
            ushort flags = reader.ReadUInt16LE();
            ushort bitmapLength = reader.ReadUInt16LE();

            if (reader.UnreadLength < bitmapLength)
                return false;

            int currentOffset = reader.Position;
            ReadOnlyMemory<byte> bitmapData = packetBuffer.Slice(currentOffset, bitmapLength);
            reader.Advance(bitmapLength);

            ushort calcWidth = (destRight >= destLeft) ? (ushort)(destRight - destLeft + 1) : width;
            ushort calcHeight = (destBottom >= destTop) ? (ushort)(destBottom - destTop + 1) : height;
            bool isCompressed = (flags & 0x0001) != 0;

            updates.Add(new RdpBitmapUpdate(destLeft, destTop, calcWidth, calcHeight, bpp, isCompressed, bitmapData));
        }

        return true;
    }

    public static bool TryParseFrame(
        ReadOnlyMemory<byte> packetBuffer,
        ulong frameId,
        DateTimeOffset timestamp,
        out RdpFrameUpdateEventArgs? eventArgs)
    {
        RdpPacketReader reader = new RdpPacketReader(packetBuffer.Span);
        if (!TryReadFastPathHeader(ref reader, out _))
        {
            eventArgs = null;
            return false;
        }

        List<RdpBitmapUpdate> allUpdates = new List<RdpBitmapUpdate>();

        while (reader.UnreadLength > 0)
        {
            if (!TryReadUpdateHeader(ref reader, out FastPathUpdateHeader updateHeader))
            {
                break;
            }

            if (updateHeader.UpdateCode == FastPathUpdateCode.Bitmap)
            {
                if (TryReadBitmapUpdateData(ref reader, packetBuffer, out List<RdpBitmapUpdate> updates))
                {
                    allUpdates.AddRange(updates);
                }
                else
                {
                    break;
                }
            }
            else
            {
                if (reader.UnreadLength < updateHeader.UpdateSize)
                    break;
                reader.Advance(updateHeader.UpdateSize);
            }
        }

        if (allUpdates.Count == 0)
        {
            eventArgs = null;
            return false;
        }

        eventArgs = new RdpFrameUpdateEventArgs(frameId, timestamp, allUpdates);
        return true;
    }
}
