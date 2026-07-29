using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Frames;

using System;
using System.Collections.Generic;
using System.IO;
using CDP.Rdp.Frames;
using CDP.Rdp.Protocol;
using Xunit;

[Xunit.Collection("RdpTests")]
public class FastPathFrameReaderTests
{
    [AvaloniaFact]
    public void TryParseFrame_SingleUncompressedRectangle_Success()
    {
        byte[] payload = BuildFastPathBitmapPdu(
            new[]
            {
                new TestRectSpec
                {
                    DestLeft = 10,
                    DestTop = 20,
                    DestRight = 109,
                    DestBottom = 69,
                    Width = 100,
                    Height = 50,
                    Bpp = 32,
                    Compressed = false,
                    Data = new byte[] { 0x11, 0x22, 0x33, 0x44 }
                }
            });

        bool success = RdpFastPathFrameReader.TryParseFrame(
            payload,
            frameId: 42,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.True(success);
        Assert.NotNull(args);
        Assert.Equal(42u, args!.FrameId);
        Assert.Single(args.BitmapUpdates);

        RdpBitmapUpdate update = args.BitmapUpdates[0];
        Assert.Equal(10, update.Left);
        Assert.Equal(20, update.Top);
        Assert.Equal(100, update.Width);
        Assert.Equal(50, update.Height);
        Assert.Equal(32, update.Bpp);
        Assert.False(update.Compressed);
        Assert.Equal(20_000, update.Data.Length);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, update.Data.Span[..4].ToArray());
    }

    [AvaloniaFact]
    public void TryParseFrame_MultipleRectangles_Success()
    {
        byte[] payload = BuildFastPathBitmapPdu(
            new[]
            {
                new TestRectSpec
                {
                    DestLeft = 0,
                    DestTop = 0,
                    DestRight = 19,
                    DestBottom = 19,
                    Width = 20,
                    Height = 20,
                    Bpp = 16,
                    Compressed = false,
                    Data = new byte[] { 0x01, 0x02 }
                },
                new TestRectSpec
                {
                    DestLeft = 100,
                    DestTop = 200,
                    DestRight = 149,
                    DestBottom = 299,
                    Width = 50,
                    Height = 100,
                    Bpp = 24,
                    Compressed = true,
                    Data = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }
                }
            });

        bool success = RdpFastPathFrameReader.TryParseFrame(
            payload,
            frameId: 100,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.True(success);
        Assert.NotNull(args);
        Assert.Equal(2, args!.BitmapUpdates.Count);

        RdpBitmapUpdate rect1 = args.BitmapUpdates[0];
        Assert.Equal(0, rect1.Left);
        Assert.Equal(0, rect1.Top);
        Assert.Equal(20, rect1.Width);
        Assert.Equal(20, rect1.Height);
        Assert.Equal(16, rect1.Bpp);
        Assert.False(rect1.Compressed);
        Assert.Equal(800, rect1.Data.Length);
        Assert.Equal(new byte[] { 0x01, 0x02 }, rect1.Data.Span[..2].ToArray());

        RdpBitmapUpdate rect2 = args.BitmapUpdates[1];
        Assert.Equal(100, rect2.Left);
        Assert.Equal(200, rect2.Top);
        Assert.Equal(50, rect2.Width);
        Assert.Equal(100, rect2.Height);
        Assert.Equal(24, rect2.Bpp);
        Assert.True(rect2.Compressed);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, rect2.Data.ToArray());
    }

    [AvaloniaFact]
    public void TryParseFrame_FallbackWidthAndHeight_WhenDestRightLessThanDestLeft()
    {
        byte[] payload = BuildFastPathBitmapPdu(
            new[]
            {
                new TestRectSpec
                {
                    DestLeft = 50,
                    DestTop = 50,
                    DestRight = 40, // destRight < destLeft
                    DestBottom = 30, // destBottom < destTop
                    Width = 120,
                    Height = 80,
                    Bpp = 32,
                    Compressed = false,
                    Data = new byte[] { 0x99 }
                }
            });

        bool success = RdpFastPathFrameReader.TryParseFrame(
            payload,
            frameId: 1,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.True(success);
        Assert.NotNull(args);
        Assert.Single(args!.BitmapUpdates);

        RdpBitmapUpdate update = args.BitmapUpdates[0];
        Assert.Equal(50, update.Left);
        Assert.Equal(50, update.Top);
        Assert.Equal(120, update.Width);
        Assert.Equal(80, update.Height);
    }

    [AvaloniaFact]
    public void TryParseFrame_InvalidFastPathLength_ReturnsFalse()
    {
        byte[] truncatedHeader = new byte[] { 0x00 }; // 1 byte only

        bool success = RdpFastPathFrameReader.TryParseFrame(
            truncatedHeader,
            frameId: 1,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.False(success);
        Assert.Null(args);
    }

    [AvaloniaFact]
    public void TryParseFrame_TruncatedBitmapData_ReturnsFalse()
    {
        byte[] validPdu = BuildFastPathBitmapPdu(
            new[]
            {
                new TestRectSpec
                {
                    DestLeft = 0,
                    DestTop = 0,
                    DestRight = 9,
                    DestBottom = 9,
                    Width = 10,
                    Height = 10,
                    Bpp = 16,
                    Compressed = false,
                    Data = new byte[] { 0x01, 0x02, 0x03, 0x04 }
                }
            });

        // Truncate last 2 bytes of bitmap pixel data
        byte[] truncatedPdu = validPdu[..^2];

        bool success = RdpFastPathFrameReader.TryParseFrame(
            truncatedPdu,
            frameId: 1,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.False(success);
        Assert.Null(args);
    }

    [AvaloniaFact]
    public void TryParseFrame_NonBitmapUpdateCode_ReturnsFalse()
    {
        // Build PDU with updateCode = Orders (0x0)
        byte[] pdu = new byte[]
        {
            0x00, // FastPath Header (action = 0x0)
            0x05, // length = 5
            0x00, // UpdateHeader (updateCode = Orders = 0x0)
            0x01, 0x00 // updateSize = 1
        };

        bool success = RdpFastPathFrameReader.TryParseFrame(
            pdu,
            frameId: 1,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.False(success);
        Assert.Null(args);
    }

    [AvaloniaFact]
    public void TryParseFrame_UpdateTypeNotBitmap_ReturnsFalse()
    {
        using MemoryStream ms = new MemoryStream();

        // 1. FastPath Header (2 bytes length encoding)
        ms.WriteByte(0x00);
        ms.WriteByte(0x80);
        ms.WriteByte(0x0C); // Total length placeholder

        // 2. FastPath Update Header (3 bytes: updateCode=1 (Bitmap), updateSize=7)
        ms.WriteByte(0x01);
        ms.WriteByte(0x07);
        ms.WriteByte(0x00);

        // 3. TS_BITMAP_UPDATE_DATA header with updateType = 0x0002 (Invalid)
        ms.WriteByte(0x02); ms.WriteByte(0x00); // updateType = 2
        ms.WriteByte(0x01); ms.WriteByte(0x00); // numberRectangles = 1

        byte[] bytes = ms.ToArray();
        // Fix length in FastPath header
        ushort totalLen = (ushort)bytes.Length;
        bytes[1] = (byte)(0x80 | (totalLen >> 8));
        bytes[2] = (byte)(totalLen & 0xFF);

        bool success = RdpFastPathFrameReader.TryParseFrame(
            bytes,
            frameId: 1,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.False(success);
        Assert.Null(args);
    }

    private struct TestRectSpec
    {
        public ushort DestLeft;
        public ushort DestTop;
        public ushort DestRight;
        public ushort DestBottom;
        public ushort Width;
        public ushort Height;
        public ushort Bpp;
        public bool Compressed;
        public byte[] Data;
    }

    private static byte[] BuildFastPathBitmapPdu(TestRectSpec[] rects)
    {
        using MemoryStream ms = new MemoryStream();

        // Placeholder for FastPath server header (3 bytes for 2-byte length encoding)
        ms.Write(new byte[3]);

        // FastPath Update Header (updateCode = Bitmap = 0x1, updateSize placeholder)
        long updateHeaderPos = ms.Position;
        ms.WriteByte(0x01); // updateCode = 0x1, fragmentation = 0x0, compression = 0x0
        ms.WriteByte(0x00); ms.WriteByte(0x00); // updateSize placeholder (LE)

        long updateDataStartPos = ms.Position;

        // TS_BITMAP_UPDATE_DATA header
        ms.WriteByte(0x01); ms.WriteByte(0x00); // updateType = 0x0001 (UPDATETYPE_BITMAP)
        ushort numRects = (ushort)rects.Length;
        ms.WriteByte((byte)(numRects & 0xFF));
        ms.WriteByte((byte)(numRects >> 8));

        foreach (var r in rects)
        {
            WriteUInt16LE(ms, r.DestLeft);
            WriteUInt16LE(ms, r.DestTop);
            WriteUInt16LE(ms, r.DestRight);
            WriteUInt16LE(ms, r.DestBottom);
            WriteUInt16LE(ms, r.Width);
            WriteUInt16LE(ms, r.Height);
            WriteUInt16LE(ms, r.Bpp);
            ushort flags = (ushort)(r.Compressed ? 0x0401 : 0x0000);
            WriteUInt16LE(ms, flags);
            byte[] bitmapData = r.Compressed ? r.Data : BuildUncompressedBitmapData(r);
            ushort bitmapLength = checked((ushort)bitmapData.Length);
            WriteUInt16LE(ms, bitmapLength);
            ms.Write(bitmapData, 0, bitmapData.Length);
        }

        long updateDataEndPos = ms.Position;
        ushort updateSize = (ushort)(updateDataEndPos - updateDataStartPos);

        // Patch updateSize in FastPath update header
        ms.Position = updateHeaderPos + 1;
        WriteUInt16LE(ms, updateSize);

        // Patch FastPath server header (2-byte length variant)
        ushort packetLength = (ushort)ms.Length;
        ms.Position = 0;
        ms.WriteByte(0x00); // FastPath header action=0
        ms.WriteByte((byte)(0x80 | (packetLength >> 8)));
        ms.WriteByte((byte)(packetLength & 0xFF));

        return ms.ToArray();
    }

    private static byte[] BuildUncompressedBitmapData(TestRectSpec rect)
    {
        int bytesPerPixel = rect.Bpp switch
        {
            15 or 16 => 2,
            24 => 3,
            32 => 4,
            _ => throw new InvalidDataException($"Unsupported test bitmap depth {rect.Bpp}.")
        };
        int stride = (rect.Width * bytesPerPixel + 3) & ~3;
        byte[] result = new byte[checked(stride * rect.Height)];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = rect.Data[i % rect.Data.Length];
        }
        return result;
    }

    private static void WriteUInt16LE(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)(value >> 8));
    }
}
