using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Frames;

using System;
using System.Collections.Generic;
using System.IO;
using CDP.Rdp.Frames;
using CDP.Rdp.Protocol;
using Xunit;

/// <summary>
/// Empirical challenge test suite for FastPath and Bitmap Update PDU parsing in <see cref="RdpFastPathFrameReader"/>.
/// </summary>
[Xunit.Collection("RdpTests")]
public class FastPathFrameReaderChallengerTests
{
    #region Category A: FastPath Server Header Parsing

    [AvaloniaFact]
    public void TryReadFastPathHeader_OneByteLength_Standard_ReturnsTrue()
    {
        // Action = 0, EncFlags = 0, CompFlags = 0, Length = 64 (1-byte length variant)
        byte[] raw = new byte[] { 0x00, 0x40 };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadFastPathHeader(ref reader, out FastPathServerHeader header);

        Assert.True(success);
        Assert.Equal(0, header.Action);
        Assert.Equal(0, header.EncryptionFlags);
        Assert.Equal(0, header.CompressionFlags);
        Assert.Equal(64, header.PacketLength);
        Assert.Equal(2, header.HeaderLength);
        Assert.Equal(0, reader.UnreadLength);
    }

    [AvaloniaFact]
    public void TryReadFastPathHeader_OneByteLength_WithFlags_ReturnsTrue()
    {
        // 0xE5: compFlags = 0xC0 (FASTPATH_OUTPUT_COMPRESSED), encFlags = 0x01 (FASTPATH_OUTPUT_ENCRYPTED), action = 0x01
        // Length = 127 (0x7F, max 1-byte length)
        byte[] raw = new byte[] { 0xE5, 0x7F };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadFastPathHeader(ref reader, out FastPathServerHeader header);

        Assert.True(success);
        Assert.Equal(1, header.Action);
        Assert.Equal(1, header.EncryptionFlags);
        Assert.Equal(0xC0, header.CompressionFlags);
        Assert.Equal(127, header.PacketLength);
        Assert.Equal(2, header.HeaderLength);
    }

    [AvaloniaFact]
    public void TryReadFastPathHeader_TwoByteLength_Standard_ReturnsTrue()
    {
        // 0x00: Action=0, Enc=0, Comp=0
        // 0x82, 0x58: 2-byte length encoding ((0x82 & 0x7F) << 8) | 0x58 = (2 << 8) | 0x58 = 600
        byte[] raw = new byte[] { 0x00, 0x82, 0x58 };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadFastPathHeader(ref reader, out FastPathServerHeader header);

        Assert.True(success);
        Assert.Equal(0, header.Action);
        Assert.Equal(0, header.EncryptionFlags);
        Assert.Equal(0, header.CompressionFlags);
        Assert.Equal(600, header.PacketLength);
        Assert.Equal(3, header.HeaderLength);
        Assert.Equal(0, reader.UnreadLength);
    }

    [AvaloniaFact]
    public void TryReadFastPathHeader_TwoByteLength_MaxUshort_ReturnsTrue()
    {
        // (0x7F << 8) | 0xFF = 32767
        byte[] raw = new byte[] { 0x00, 0xFF, 0xFF };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadFastPathHeader(ref reader, out FastPathServerHeader header);

        Assert.True(success);
        Assert.Equal(32767, header.PacketLength);
        Assert.Equal(3, header.HeaderLength);
    }

    [AvaloniaFact]
    public void TryReadFastPathHeader_Truncated_0Bytes_ReturnsFalse()
    {
        ReadOnlySpan<byte> raw = ReadOnlySpan<byte>.Empty;
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadFastPathHeader(ref reader, out FastPathServerHeader header);

        Assert.False(success);
        Assert.Equal(default, header);
    }

    [AvaloniaFact]
    public void TryReadFastPathHeader_Truncated_1Byte_ReturnsFalse()
    {
        byte[] raw = new byte[] { 0x00 };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadFastPathHeader(ref reader, out FastPathServerHeader header);

        Assert.False(success);
        Assert.Equal(default, header);
    }

    [AvaloniaFact]
    public void TryReadFastPathHeader_Truncated_2ByteLengthMissingSecondByte_ReturnsFalse()
    {
        // 0x81 has MSB set indicating 2-byte length, but 3rd byte is missing
        byte[] raw = new byte[] { 0x00, 0x81 };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadFastPathHeader(ref reader, out FastPathServerHeader header);

        Assert.False(success);
        Assert.Equal(default, header);
    }

    #endregion

    #region Category B: FastPath Update Header Parsing

    [AvaloniaFact]
    public void TryReadUpdateHeader_Uncompressed_Standard_ReturnsTrue()
    {
        // 0x01: updateCode = 0x1 (Bitmap), frag = 0 (Single), isComp = false
        // UpdateSize = 100 (0x0064 LE)
        byte[] raw = new byte[] { 0x01, 0x64, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadUpdateHeader(ref reader, out FastPathUpdateHeader header);

        Assert.True(success);
        Assert.Equal(FastPathUpdateCode.Bitmap, header.UpdateCode);
        Assert.Equal(FastPathFragmentation.Single, header.Fragmentation);
        Assert.False(header.IsCompressed);
        Assert.Equal(0, header.CompressionFlags);
        Assert.Equal(100, header.UpdateSize);
        Assert.Equal(3, header.HeaderLength);
    }

    [AvaloniaFact]
    public void TryReadUpdateHeader_Compressed_Standard_ReturnsTrue()
    {
        // 0x81: bit 6-7 is 0x2 (isComp = true), bit 4-5 is 0x0 (Single), bit 0-3 is 0x1 (Bitmap)
        // compFlags = 0x02
        // UpdateSize = 200 (0x00C8 LE)
        byte[] raw = new byte[] { 0x81, 0x02, 0xC8, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadUpdateHeader(ref reader, out FastPathUpdateHeader header);

        Assert.True(success);
        Assert.Equal(FastPathUpdateCode.Bitmap, header.UpdateCode);
        Assert.Equal(FastPathFragmentation.Single, header.Fragmentation);
        Assert.True(header.IsCompressed);
        Assert.Equal(0x02, header.CompressionFlags);
        Assert.Equal(200, header.UpdateSize);
        Assert.Equal(4, header.HeaderLength);
    }

    [AvaloniaTheory]
    [InlineData(0x00, FastPathFragmentation.Single)]
    [InlineData(0x10, FastPathFragmentation.Last)]
    [InlineData(0x20, FastPathFragmentation.First)]
    [InlineData(0x30, FastPathFragmentation.Next)]
    public void TryReadUpdateHeader_FragmentationFlags_CorrectMapping(byte fragByte, FastPathFragmentation expectedFrag)
    {
        byte firstByte = (byte)(0x01 | fragByte); // Bitmap code + frag
        byte[] raw = new byte[] { firstByte, 0x10, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadUpdateHeader(ref reader, out FastPathUpdateHeader header);

        Assert.True(success);
        Assert.Equal(expectedFrag, header.Fragmentation);
    }

    [AvaloniaTheory]
    [InlineData(0x07)] // Unknown / unassigned update code
    [InlineData(0x0C)]
    [InlineData(0x0D)]
    [InlineData(0x0E)]
    [InlineData(0x0F)]
    public void TryReadUpdateHeader_UnknownUpdateCodes_ParsedAsEnum(byte codeNibble)
    {
        byte firstByte = (byte)(codeNibble & 0x0F);
        byte[] raw = new byte[] { firstByte, 0x20, 0x00 };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadUpdateHeader(ref reader, out FastPathUpdateHeader header);

        Assert.True(success);
        Assert.Equal((FastPathUpdateCode)codeNibble, header.UpdateCode);
        Assert.Equal(32, header.UpdateSize);
    }

    [AvaloniaFact]
    public void TryReadUpdateHeader_Truncated_0Bytes_ReturnsFalse()
    {
        ReadOnlySpan<byte> raw = ReadOnlySpan<byte>.Empty;
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadUpdateHeader(ref reader, out FastPathUpdateHeader header);

        Assert.False(success);
        Assert.Equal(default, header);
    }

    [AvaloniaFact]
    public void TryReadUpdateHeader_Truncated_CompressedMissingCompFlags_ReturnsFalse()
    {
        // 0x81 indicates isCompressed = true, but no second byte provided
        byte[] raw = new byte[] { 0x81 };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadUpdateHeader(ref reader, out FastPathUpdateHeader header);

        Assert.False(success);
        Assert.Equal(default, header);
    }

    [AvaloniaFact]
    public void TryReadUpdateHeader_Truncated_MissingUpdateSize_ReturnsFalse()
    {
        // 0x01: isCompressed = false, but only 1 size byte (0x10) provided instead of 2
        byte[] raw = new byte[] { 0x01, 0x10 };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadUpdateHeader(ref reader, out FastPathUpdateHeader header);

        Assert.False(success);
        Assert.Equal(default, header);
    }

    [AvaloniaFact]
    public void TryReadUpdateHeader_Truncated_CompressedMissingUpdateSize_ReturnsFalse()
    {
        // 0x81: isCompressed = true, byte 2: compFlags = 0x02, byte 3: 0x10 (missing 2nd size byte)
        byte[] raw = new byte[] { 0x81, 0x02, 0x10 };
        RdpPacketReader reader = new RdpPacketReader(raw);

        bool success = RdpFastPathFrameReader.TryReadUpdateHeader(ref reader, out FastPathUpdateHeader header);

        Assert.False(success);
        Assert.Equal(default, header);
    }

    #endregion

    #region Category C: Bitmap Update Data Parsing

    [AvaloniaFact]
    public void TryReadBitmapUpdateData_MultiRectangle_Success()
    {
        TestRectSpec[] rects = new[]
        {
            new TestRectSpec
            {
                DestLeft = 0, DestTop = 0, DestRight = 0, DestBottom = 0,
                Width = 1, Height = 1, Bpp = 32, Compressed = false,
                Data = new byte[] { 0x01, 0x02, 0x03, 0x04 }
            },
            new TestRectSpec
            {
                DestLeft = 50, DestTop = 50, DestRight = 69, DestBottom = 79,
                Width = 20, Height = 30, Bpp = 16, Compressed = true,
                Data = new byte[] { 0xAA, 0xBB }
            },
            new TestRectSpec
            {
                DestLeft = 100, DestTop = 100, DestRight = 100, DestBottom = 100,
                Width = 1, Height = 1, Bpp = 24, Compressed = false,
                Data = new byte[] { 0x11, 0x22, 0x33, 0x00 }
            }
        };

        byte[] bitmapPayload = BuildBitmapUpdatePayload(0x0001, rects);
        ReadOnlyMemory<byte> mem = bitmapPayload;
        RdpPacketReader reader = new RdpPacketReader(mem.Span);

        bool success = RdpFastPathFrameReader.TryReadBitmapUpdateData(ref reader, mem, out List<RdpBitmapUpdate> updates);

        Assert.True(success);
        Assert.Equal(3, updates.Count);

        // Rect 0
        Assert.Equal(0, updates[0].Left);
        Assert.Equal(0, updates[0].Top);
        Assert.Equal(1, updates[0].Width);
        Assert.Equal(1, updates[0].Height);
        Assert.Equal(32, updates[0].Bpp);
        Assert.False(updates[0].Compressed);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, updates[0].Data.ToArray());

        // Rect 1
        Assert.Equal(50, updates[1].Left);
        Assert.Equal(50, updates[1].Top);
        Assert.Equal(20, updates[1].Width);
        Assert.Equal(30, updates[1].Height);
        Assert.Equal(16, updates[1].Bpp);
        Assert.True(updates[1].Compressed);
        Assert.Equal(new byte[] { 0xAA, 0xBB }, updates[1].Data.ToArray());

        // Rect 2
        Assert.Equal(100, updates[2].Left);
        Assert.Equal(100, updates[2].Top);
        Assert.Equal(1, updates[2].Width);
        Assert.Equal(1, updates[2].Height);
        Assert.Equal(24, updates[2].Bpp);
        Assert.False(updates[2].Compressed);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x00 }, updates[2].Data.ToArray());
    }

    [AvaloniaFact]
    public void TryReadBitmapUpdateData_ZeroWidthAndHeight_IsRejected()
    {
        // Rect A: destRight < destLeft, destBottom < destTop, Width = 0, Height = 0 -> calcWidth = 0, calcHeight = 0
        // Rect B: destLeft = 10, destTop = 20, destRight = 10, destBottom = 20 -> calcWidth = 1, calcHeight = 1
        // Rect C: destRight < destLeft, destBottom >= destTop -> calcWidth = width fallback (15), calcHeight = 11
        TestRectSpec[] rects = new[]
        {
            new TestRectSpec
            {
                DestLeft = 10, DestTop = 20, DestRight = 9, DestBottom = 19,
                Width = 0, Height = 0, Bpp = 32, Compressed = false,
                Data = new byte[] { 0x01 }
            },
            new TestRectSpec
            {
                DestLeft = 10, DestTop = 20, DestRight = 10, DestBottom = 20,
                Width = 100, Height = 100, Bpp = 32, Compressed = false,
                Data = new byte[] { 0x02 }
            },
            new TestRectSpec
            {
                DestLeft = 10, DestTop = 20, DestRight = 9, DestBottom = 30,
                Width = 15, Height = 99, Bpp = 32, Compressed = false,
                Data = new byte[] { 0x03 }
            }
        };

        byte[] payload = BuildBitmapUpdatePayload(0x0001, rects);
        ReadOnlyMemory<byte> mem = payload;
        RdpPacketReader reader = new RdpPacketReader(mem.Span);

        bool success = RdpFastPathFrameReader.TryReadBitmapUpdateData(ref reader, mem, out List<RdpBitmapUpdate> updates);

        Assert.False(success);
        Assert.Empty(updates);
    }

    [AvaloniaTheory]
    [InlineData(0x0000, false)] // No compression flag
    [InlineData(0x0001, true)]  // BITMAP_COMPRESSION
    [InlineData(0x0002, false)] // Other flag
    [InlineData(0x0003, true)]  // BITMAP_COMPRESSION | 0x0002
    [InlineData(0x0400, false)] // RDP6 compression flag without bit 0
    public void TryReadBitmapUpdateData_BitmapCompressionFlagParsing_EvaluatesBit0(ushort flags, bool expectedCompressed)
    {
        TestRectSpec[] rects = new[]
        {
            new TestRectSpec
            {
                DestLeft = 0, DestTop = 0, DestRight = 0, DestBottom = 0,
                Width = 1, Height = 1, Bpp = 16, CustomFlags = flags,
                Data = new byte[] { 0x55, 0xAA, 0x00, 0x00 }
            }
        };

        byte[] payload = BuildBitmapUpdatePayload(0x0001, rects);
        ReadOnlyMemory<byte> mem = payload;
        RdpPacketReader reader = new RdpPacketReader(mem.Span);

        bool success = RdpFastPathFrameReader.TryReadBitmapUpdateData(ref reader, mem, out List<RdpBitmapUpdate> updates);

        Assert.True(success);
        Assert.Single(updates);
        Assert.Equal(expectedCompressed, updates[0].Compressed);
    }

    [AvaloniaFact]
    public void TryReadBitmapUpdateData_BufferBoundarySlicing_MatchesSlicedMemoryOffset()
    {
        TestRectSpec[] rects = new[]
        {
            new TestRectSpec
            {
                DestLeft = 0, DestTop = 0, DestRight = 0, DestBottom = 0,
                Width = 1, Height = 1, Bpp = 32, Compressed = false,
                Data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }
            }
        };

        byte[] innerPayload = BuildBitmapUpdatePayload(0x0001, rects);
        // Prefix with 16 bytes of dummy offset
        byte[] fullBuffer = new byte[16 + innerPayload.Length];
        Array.Copy(innerPayload, 0, fullBuffer, 16, innerPayload.Length);

        // Slice memory starting at offset 16
        ReadOnlyMemory<byte> slicedMemory = fullBuffer.AsMemory(16);
        RdpPacketReader reader = new RdpPacketReader(slicedMemory.Span);

        bool success = RdpFastPathFrameReader.TryReadBitmapUpdateData(ref reader, slicedMemory, out List<RdpBitmapUpdate> updates);

        Assert.True(success);
        Assert.Single(updates);
        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, updates[0].Data.ToArray());
    }

    [AvaloniaFact]
    public void TryReadBitmapUpdateData_ZeroRectangles_ReturnsTrueWithEmptyList()
    {
        byte[] payload = BuildBitmapUpdatePayload(0x0001, Array.Empty<TestRectSpec>());
        ReadOnlyMemory<byte> mem = payload;
        RdpPacketReader reader = new RdpPacketReader(mem.Span);

        bool success = RdpFastPathFrameReader.TryReadBitmapUpdateData(ref reader, mem, out List<RdpBitmapUpdate> updates);

        Assert.True(success);
        Assert.Empty(updates);
    }

    [AvaloniaFact]
    public void TryReadBitmapUpdateData_Truncated_LessThan4BytesHeader_ReturnsFalse()
    {
        byte[] raw = new byte[] { 0x01, 0x00, 0x01 }; // only 3 bytes
        ReadOnlyMemory<byte> mem = raw;
        RdpPacketReader reader = new RdpPacketReader(mem.Span);

        bool success = RdpFastPathFrameReader.TryReadBitmapUpdateData(ref reader, mem, out List<RdpBitmapUpdate> updates);

        Assert.False(success);
        Assert.Empty(updates);
    }

    [AvaloniaFact]
    public void TryReadBitmapUpdateData_InvalidUpdateType_ReturnsFalse()
    {
        // updateType = 0x0002 instead of 0x0001
        byte[] raw = new byte[] { 0x02, 0x00, 0x01, 0x00 };
        ReadOnlyMemory<byte> mem = raw;
        RdpPacketReader reader = new RdpPacketReader(mem.Span);

        bool success = RdpFastPathFrameReader.TryReadBitmapUpdateData(ref reader, mem, out List<RdpBitmapUpdate> updates);

        Assert.False(success);
        Assert.Empty(updates);
    }

    [AvaloniaFact]
    public void TryReadBitmapUpdateData_Truncated_MidRectangleHeader_ReturnsFalse()
    {
        TestRectSpec[] rects = new[]
        {
            new TestRectSpec
            {
                DestLeft = 0, DestTop = 0, DestRight = 0, DestBottom = 0,
                Width = 1, Height = 1, Bpp = 32, Compressed = false,
                Data = new byte[] { 0x01, 0x02, 0x00, 0x00 }
            },
            new TestRectSpec
            {
                DestLeft = 10, DestTop = 10, DestRight = 10, DestBottom = 10,
                Width = 1, Height = 1, Bpp = 32, Compressed = false,
                Data = new byte[] { 0x03, 0x04, 0x00, 0x00 }
            }
        };

        byte[] payload = BuildBitmapUpdatePayload(0x0001, rects);
        // Truncate in the middle of rect 2's 18-byte header
        byte[] truncated = payload[..^10];

        ReadOnlyMemory<byte> mem = truncated;
        RdpPacketReader reader = new RdpPacketReader(mem.Span);

        bool success = RdpFastPathFrameReader.TryReadBitmapUpdateData(ref reader, mem, out List<RdpBitmapUpdate> updates);

        Assert.False(success);
    }

    [AvaloniaFact]
    public void TryReadBitmapUpdateData_Truncated_RectanglePixelDataIncomplete_ReturnsFalse()
    {
        TestRectSpec[] rects = new[]
        {
            new TestRectSpec
            {
                DestLeft = 0, DestTop = 0, DestRight = 1, DestBottom = 0,
                Width = 2, Height = 1, Bpp = 32, Compressed = false,
                Data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }
            }
        };

        byte[] payload = BuildBitmapUpdatePayload(0x0001, rects);
        // Truncate 4 bytes of pixel data
        byte[] truncated = payload[..^4];

        ReadOnlyMemory<byte> mem = truncated;
        RdpPacketReader reader = new RdpPacketReader(mem.Span);

        bool success = RdpFastPathFrameReader.TryReadBitmapUpdateData(ref reader, mem, out List<RdpBitmapUpdate> updates);

        Assert.False(success);
    }

    #endregion

    #region Category D: Frame Parsing End-to-End

    [AvaloniaFact]
    public void TryParseFrame_MultipleUpdateHeadersInSinglePdu_SkipsNonBitmapAndParsesBitmap()
    {
        // PDU contains:
        // 1. FastPath server header (1-byte length variant)
        // 2. Palette update header (updateCode = 0x2, updateSize = 4, payload = 4 dummy bytes)
        // 3. Bitmap update header (updateCode = 0x1, updateSize = TS_BITMAP_DATA size)
        using MemoryStream ms = new MemoryStream();

        // 1. FastPath Header placeholder (2 bytes)
        ms.Write(new byte[2]);

        // 2. Update Header 1: Palette (0x02), size = 4
        ms.WriteByte(0x02);
        WriteUInt16LE(ms, 4);
        ms.Write(new byte[] { 0x10, 0x20, 0x30, 0x40 }); // dummy palette payload

        // 3. Update Header 2: Bitmap (0x01)
        long bitmapHeaderPos = ms.Position;
        ms.WriteByte(0x01);
        ms.WriteByte(0x00); ms.WriteByte(0x00); // size placeholder

        long bitmapDataStart = ms.Position;
        byte[] bitmapPayload = BuildBitmapUpdatePayload(0x0001, new[]
        {
            new TestRectSpec
            {
                DestLeft = 0, DestTop = 0, DestRight = 0, DestBottom = 0,
                Width = 1, Height = 1, Bpp = 32, Compressed = false,
                Data = new byte[] { 0x77, 0x88, 0x00, 0x00 }
            }
        });
        ms.Write(bitmapPayload);
        long bitmapDataEnd = ms.Position;

        // Patch Bitmap updateSize
        ushort bitmapUpdateSize = (ushort)(bitmapDataEnd - bitmapDataStart);
        ms.Position = bitmapHeaderPos + 1;
        WriteUInt16LE(ms, bitmapUpdateSize);

        // Patch FastPath server header
        ushort totalLen = (ushort)ms.Length;
        ms.Position = 0;
        ms.WriteByte(0x00); // Action=0
        ms.WriteByte((byte)totalLen);

        byte[] frameBytes = ms.ToArray();

        bool success = RdpFastPathFrameReader.TryParseFrame(
            frameBytes,
            frameId: 101,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.True(success);
        Assert.NotNull(args);
        Assert.Equal(101u, args!.FrameId);
        Assert.Single(args.BitmapUpdates);
        Assert.Equal(new byte[] { 0x77, 0x88, 0x00, 0x00 }, args.BitmapUpdates[0].Data.ToArray());
    }

    [AvaloniaFact]
    public void TryParseFrame_MultipleBitmapUpdateHeadersInSinglePdu_AggregatesAllUpdates()
    {
        // PDU contains 2 separate Bitmap update headers
        using MemoryStream ms = new MemoryStream();

        ms.Write(new byte[3]); // FastPath header placeholder (3 bytes)

        // Bitmap Update Header 1
        long h1 = ms.Position;
        ms.WriteByte(0x01);
        ms.WriteByte(0x00); ms.WriteByte(0x00);
        long s1 = ms.Position;
        byte[] b1 = BuildBitmapUpdatePayload(0x0001, new[]
        {
            new TestRectSpec
            {
                DestLeft = 0, DestTop = 0, DestRight = 0, DestBottom = 0,
                Width = 1, Height = 1, Bpp = 16, Compressed = false,
                Data = new byte[] { 0x11, 0x00, 0x00, 0x00 }
            }
        });
        ms.Write(b1);
        long e1 = ms.Position;
        ms.Position = h1 + 1;
        WriteUInt16LE(ms, (ushort)(e1 - s1));
        ms.Position = e1;

        // Bitmap Update Header 2
        long h2 = ms.Position;
        ms.WriteByte(0x01);
        ms.WriteByte(0x00); ms.WriteByte(0x00);
        long s2 = ms.Position;
        byte[] b2 = BuildBitmapUpdatePayload(0x0001, new[]
        {
            new TestRectSpec
            {
                DestLeft = 10, DestTop = 10, DestRight = 10, DestBottom = 10,
                Width = 1, Height = 1, Bpp = 16, Compressed = false,
                Data = new byte[] { 0x22, 0x00, 0x00, 0x00 }
            },
            new TestRectSpec
            {
                DestLeft = 20, DestTop = 20, DestRight = 20, DestBottom = 20,
                Width = 1, Height = 1, Bpp = 16, Compressed = false,
                Data = new byte[] { 0x33, 0x00, 0x00, 0x00 }
            }
        });
        ms.Write(b2);
        long e2 = ms.Position;
        ms.Position = h2 + 1;
        WriteUInt16LE(ms, (ushort)(e2 - s2));

        // Patch FastPath server header (2-byte length variant)
        ushort totalLen = (ushort)ms.Length;
        ms.Position = 0;
        ms.WriteByte(0x00);
        ms.WriteByte((byte)(0x80 | (totalLen >> 8)));
        ms.WriteByte((byte)(totalLen & 0xFF));

        byte[] frameBytes = ms.ToArray();

        bool success = RdpFastPathFrameReader.TryParseFrame(
            frameBytes,
            frameId: 202,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.True(success);
        Assert.NotNull(args);
        Assert.Equal(3, args!.BitmapUpdates.Count);
        Assert.Equal(new byte[] { 0x11, 0x00, 0x00, 0x00 }, args.BitmapUpdates[0].Data.ToArray());
        Assert.Equal(new byte[] { 0x22, 0x00, 0x00, 0x00 }, args.BitmapUpdates[1].Data.ToArray());
        Assert.Equal(new byte[] { 0x33, 0x00, 0x00, 0x00 }, args.BitmapUpdates[2].Data.ToArray());
    }

    [AvaloniaFact]
    public void TryParseFrame_UnknownUpdateCode_SkippedSuccessfully()
    {
        using MemoryStream ms = new MemoryStream();

        ms.Write(new byte[2]); // FastPath header (1 byte length)

        // Unknown update header: code = 0x7, updateSize = 6
        ms.WriteByte(0x07);
        WriteUInt16LE(ms, 6);
        ms.Write(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }); // payload

        // Valid Bitmap update header
        long hPos = ms.Position;
        ms.WriteByte(0x01);
        ms.WriteByte(0x00); ms.WriteByte(0x00);
        long sPos = ms.Position;
        byte[] bitmapPayload = BuildBitmapUpdatePayload(0x0001, new[]
        {
            new TestRectSpec
            {
                DestLeft = 0, DestTop = 0, DestRight = 0, DestBottom = 0,
                Width = 1, Height = 1, Bpp = 32, Compressed = false,
                Data = new byte[] { 0x99, 0x00, 0x00, 0x00 }
            }
        });
        ms.Write(bitmapPayload);
        long ePos = ms.Position;
        ms.Position = hPos + 1;
        WriteUInt16LE(ms, (ushort)(ePos - sPos));

        // Patch FastPath server header
        ushort totalLen = (ushort)ms.Length;
        ms.Position = 0;
        ms.WriteByte(0x00);
        ms.WriteByte((byte)totalLen);

        byte[] frameBytes = ms.ToArray();

        bool success = RdpFastPathFrameReader.TryParseFrame(
            frameBytes,
            frameId: 303,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.True(success);
        Assert.NotNull(args);
        Assert.Single(args!.BitmapUpdates);
        Assert.Equal(new byte[] { 0x99, 0x00, 0x00, 0x00 }, args.BitmapUpdates[0].Data.ToArray());
    }

    [AvaloniaFact]
    public void TryParseFrame_UnknownUpdateCode_TruncatedPayload_ReturnsFalse()
    {
        using MemoryStream ms = new MemoryStream();

        ms.Write(new byte[2]); // FastPath header

        // Unknown update header: code = 0x7, updateSize = 100, but no payload
        ms.WriteByte(0x07);
        WriteUInt16LE(ms, 100);

        ushort totalLen = (ushort)ms.Length;
        ms.Position = 0;
        ms.WriteByte(0x00);
        ms.WriteByte((byte)totalLen);

        byte[] frameBytes = ms.ToArray();

        bool success = RdpFastPathFrameReader.TryParseFrame(
            frameBytes,
            frameId: 404,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.False(success);
        Assert.Null(args);
    }

    [AvaloniaFact]
    public void TryParseFrame_EncryptedAndCompressedServerHeaderFlags_RejectsUnprocessedPayload()
    {
        using MemoryStream ms = new MemoryStream();

        ms.Write(new byte[2]); // FastPath header

        // Bitmap update header
        long hPos = ms.Position;
        ms.WriteByte(0x01);
        ms.WriteByte(0x00); ms.WriteByte(0x00);
        long sPos = ms.Position;
        byte[] bitmapPayload = BuildBitmapUpdatePayload(0x0001, new[]
        {
            new TestRectSpec
            {
                DestLeft = 0, DestTop = 0, DestRight = 0, DestBottom = 0,
                Width = 1, Height = 1, Bpp = 32, Compressed = false,
                Data = new byte[] { 0xAB, 0x00, 0x00, 0x00 }
            }
        });
        ms.Write(bitmapPayload);
        long ePos = ms.Position;
        ms.Position = hPos + 1;
        WriteUInt16LE(ms, (ushort)(ePos - sPos));

        // Patch FastPath server header with encFlags = 0x01, compFlags = 0x80
        ushort totalLen = (ushort)ms.Length;
        ms.Position = 0;
        // 0x84: compFlags = 0x80, encFlags = 0x01, action = 0x00
        ms.WriteByte(0x84);
        ms.WriteByte((byte)totalLen);

        byte[] frameBytes = ms.ToArray();

        bool success = RdpFastPathFrameReader.TryParseFrame(
            frameBytes,
            frameId: 505,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.False(success);
        Assert.Null(args);
    }

    [AvaloniaFact]
    public void TryParseFrame_ZeroBitmapUpdates_ReturnsFalse()
    {
        // PDU contains only a Synchronize update (updateCode = 0x3, updateSize = 0)
        using MemoryStream ms = new MemoryStream();

        ms.Write(new byte[2]); // FastPath header
        ms.WriteByte(0x03);    // Synchronize
        WriteUInt16LE(ms, 0);

        ushort totalLen = (ushort)ms.Length;
        ms.Position = 0;
        ms.WriteByte(0x00);
        ms.WriteByte((byte)totalLen);

        byte[] frameBytes = ms.ToArray();

        bool success = RdpFastPathFrameReader.TryParseFrame(
            frameBytes,
            frameId: 606,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.False(success);
        Assert.Null(args);
    }

    [AvaloniaFact]
    public void TryParseFrame_CorruptedTrailingBytesAfterValidBitmap_ReturnsTrueWithParsedBitmap()
    {
        using MemoryStream ms = new MemoryStream();

        ms.Write(new byte[2]); // FastPath header

        // Valid Bitmap update header
        long hPos = ms.Position;
        ms.WriteByte(0x01);
        ms.WriteByte(0x00); ms.WriteByte(0x00);
        long sPos = ms.Position;
        byte[] bitmapPayload = BuildBitmapUpdatePayload(0x0001, new[]
        {
            new TestRectSpec
            {
                DestLeft = 0, DestTop = 0, DestRight = 0, DestBottom = 0,
                Width = 1, Height = 1, Bpp = 32, Compressed = false,
                Data = new byte[] { 0xEF, 0x00, 0x00, 0x00 }
            }
        });
        ms.Write(bitmapPayload);
        long ePos = ms.Position;
        ms.Position = hPos + 1;
        WriteUInt16LE(ms, (ushort)(ePos - sPos));

        // Add 1 corrupted trailing byte at end of frame
        ms.Position = ePos;
        ms.WriteByte(0xFF);

        // Patch FastPath server header
        ushort totalLen = (ushort)ms.Length;
        ms.Position = 0;
        ms.WriteByte(0x00);
        ms.WriteByte((byte)totalLen);

        byte[] frameBytes = ms.ToArray();

        bool success = RdpFastPathFrameReader.TryParseFrame(
            frameBytes,
            frameId: 707,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.True(success);
        Assert.NotNull(args);
        Assert.Single(args!.BitmapUpdates);
        Assert.Equal(new byte[] { 0xEF, 0x00, 0x00, 0x00 }, args.BitmapUpdates[0].Data.ToArray());
    }

    [AvaloniaFact]
    public void TryParseFrame_EmptyPayload_ReturnsFalse()
    {
        bool success = RdpFastPathFrameReader.TryParseFrame(
            ReadOnlyMemory<byte>.Empty,
            frameId: 808,
            timestamp: DateTimeOffset.UtcNow,
            out RdpFrameUpdateEventArgs? args);

        Assert.False(success);
        Assert.Null(args);
    }

    #endregion

    #region Helper Structs & Methods

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
        public ushort? CustomFlags;
        public byte[] Data;
    }

    private static byte[] BuildBitmapUpdatePayload(ushort updateType, TestRectSpec[] rects)
    {
        using MemoryStream ms = new MemoryStream();

        WriteUInt16LE(ms, updateType);
        ushort numRects = (ushort)rects.Length;
        WriteUInt16LE(ms, numRects);

        foreach (var r in rects)
        {
            WriteUInt16LE(ms, r.DestLeft);
            WriteUInt16LE(ms, r.DestTop);
            WriteUInt16LE(ms, r.DestRight);
            WriteUInt16LE(ms, r.DestBottom);
            WriteUInt16LE(ms, r.Width);
            WriteUInt16LE(ms, r.Height);
            WriteUInt16LE(ms, r.Bpp);

            ushort flags = r.CustomFlags ?? (ushort)(r.Compressed ? 0x0001 : 0x0000);
            WriteUInt16LE(ms, flags);

            bool includeCompressionHeader = (flags & 0x0001) != 0 && (flags & 0x0400) == 0;
            ushort bitmapLength = checked((ushort)(r.Data.Length + (includeCompressionHeader ? 8 : 0)));
            WriteUInt16LE(ms, bitmapLength);
            if (includeCompressionHeader)
            {
                WriteUInt16LE(ms, 0);
                WriteUInt16LE(ms, checked((ushort)r.Data.Length));
                WriteUInt16LE(ms, checked((ushort)(r.Width * (r.Bpp is 15 or 16 ? 2 : r.Bpp / 8))));
                WriteUInt16LE(ms, 0);
            }
            ms.Write(r.Data, 0, r.Data.Length);
        }

        return ms.ToArray();
    }

    private static void WriteUInt16LE(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)(value >> 8));
    }

    #endregion
}
