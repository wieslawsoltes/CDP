namespace CDP.Rdp.Rendering;

using System;
using System.Buffers;
using System.IO;
using SkiaSharp;
using CDP.Rdp.Frames;

/// <summary>
/// Decoded bitmap tile containing SkiaSharp-compatible (BGRA8888) pixel data and destination bounds.
/// Supports zero-allocation buffer rental via ArrayPool byte management.
/// </summary>
public sealed class RdpBitmapTile : IDisposable
{
    private const int MaxBitmapDimension = 16_384;
    private const int MaxDecodedBitmapBytes = 256 * 1024 * 1024;
    private readonly ArrayPool<byte>? _pool;
    private byte[]? _pooledBuffer;
    private bool _disposed;

    public SKRectI Bounds { get; }
    public int Width => Bounds.Width;
    public int Height => Bounds.Height;
    public int Left => Bounds.Left;
    public int Top => Bounds.Top;
    public ushort Bpp { get; }

    /// <summary>
    /// Gets the pixel data in 32-bit BGRA (Bgra8888) format.
    /// </summary>
    public byte[] PixelData { get; }

    /// <summary>
    /// Gets the valid length of BGRA pixel data in PixelData array.
    /// </summary>
    public int PixelDataLength { get; }

    public ReadOnlySpan<byte> PixelSpan => PixelData.AsSpan(0, PixelDataLength);

    public RdpBitmapTile(SKRectI bounds, ushort bpp, byte[] pixelData, int pixelDataLength, byte[]? pooledBuffer = null, ArrayPool<byte>? pool = null)
    {
        Bounds = bounds;
        Bpp = bpp;
        PixelData = pixelData;
        PixelDataLength = pixelDataLength;
        _pooledBuffer = pooledBuffer;
        _pool = pool;
    }

    /// <summary>
    /// Decodes an RdpBitmapUpdate into a 32-bit BGRA RdpBitmapTile using pooled memory.
    /// </summary>
    public static RdpBitmapTile FromUpdate(in RdpBitmapUpdate update, ArrayPool<byte>? pool = null)
    {
        var targetPool = pool ?? ArrayPool<byte>.Shared;
        int width = update.Width;
        int height = update.Height;
        if (width <= 0 || height <= 0 || width > MaxBitmapDimension || height > MaxBitmapDimension)
        {
            throw new InvalidDataException($"Invalid RDP bitmap dimensions: {width}x{height}.");
        }

        int bytesPerInputPixel = GetBytesPerPixel(update.Bpp);
        int pixelCount;
        int bgraByteCount;
        int tightSourceByteCount;
        try
        {
            pixelCount = checked(width * height);
            bgraByteCount = checked(pixelCount * 4);
            tightSourceByteCount = checked(pixelCount * bytesPerInputPixel);
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException("RDP bitmap dimensions overflow the decode buffer size.", ex);
        }

        if (bgraByteCount > MaxDecodedBitmapBytes || tightSourceByteCount > MaxDecodedBitmapBytes)
        {
            throw new InvalidDataException("RDP bitmap exceeds the maximum decoded size.");
        }

        byte[] bgraBuffer = targetPool.Rent(bgraByteCount);
        bool success = false;

        try
        {
            ReadOnlySpan<byte> rawSource = update.Data.Span;
            byte[]? normalizedPoolBuffer = null;

            try
            {
                normalizedPoolBuffer = targetPool.Rent(tightSourceByteCount);
                Span<byte> normalizedPixels = normalizedPoolBuffer.AsSpan(0, tightSourceByteCount);
                normalizedPixels.Clear();

                if (update.Compressed)
                {
                    byte[] encodedOrderBuffer = targetPool.Rent(tightSourceByteCount);
                    try
                    {
                        Span<byte> encodedOrderPixels = encodedOrderBuffer.AsSpan(0, tightSourceByteCount);
                        encodedOrderPixels.Clear();
                        DecompressRle(rawSource, encodedOrderPixels, width, height, update.Bpp);
                        CopyRowsTopDown(encodedOrderPixels, normalizedPixels, width * bytesPerInputPixel, height);
                    }
                    finally
                    {
                        targetPool.Return(encodedOrderBuffer, clearArray: true);
                    }
                }
                else
                {
                    int sourceStride = checked((width * bytesPerInputPixel + 3) & ~3);
                    int requiredSourceBytes = checked(sourceStride * height);
                    if (rawSource.Length < requiredSourceBytes)
                    {
                        throw new InvalidDataException(
                            $"Uncompressed RDP bitmap is truncated. Expected {requiredSourceBytes} bytes, received {rawSource.Length}.");
                    }

                    CopyPaddedRowsTopDown(rawSource, normalizedPixels, sourceStride, width * bytesPerInputPixel, height);
                }

                Span<byte> bgraPixels = bgraBuffer.AsSpan(0, bgraByteCount);
                bgraPixels.Clear();
                ConvertPixelsToBgra32(normalizedPixels, bgraPixels, width, height, update.Bpp);
            }
            finally
            {
                if (normalizedPoolBuffer != null)
                {
                    targetPool.Return(normalizedPoolBuffer, clearArray: true);
                }
            }

            SKRectI bounds = SKRectI.Create(update.Left, update.Top, update.Width, update.Height);
            var tile = new RdpBitmapTile(bounds, update.Bpp, bgraBuffer, bgraByteCount, bgraBuffer, targetPool);
            success = true;
            return tile;
        }
        finally
        {
            if (!success)
            {
                targetPool.Return(bgraBuffer);
            }
        }
    }

    /// <summary>
    /// Decompresses RLE-encoded RDP bitmap data into an uncompressed pixel span.
    /// </summary>
    public static int DecompressRle(ReadOnlySpan<byte> src, Span<byte> dst, int width, int height, ushort bpp)
    {
        int bytesPerPixel = GetBytesPerPixel(bpp);
        int pixelCount = checked(width * height);
        int expectedLength = checked(pixelCount * bytesPerPixel);
        if (width <= 0 || height <= 0 || dst.Length < expectedLength)
        {
            throw new ArgumentException("Destination buffer is too small for the decoded RDP bitmap.", nameof(dst));
        }

        dst[..expectedLength].Clear();
        var decoder = new RleDecoder(src, dst, width, pixelCount, bytesPerPixel, bpp);
        decoder.Decode();
        return expectedLength;
    }

    private ref struct RleDecoder
    {
        private readonly ReadOnlySpan<byte> _source;
        private readonly Span<byte> _destination;
        private readonly int _width;
        private readonly int _pixelCount;
        private readonly int _bytesPerPixel;
        private readonly ushort _bpp;
        private int _sourceOffset;
        private int _destinationPixel;
        private uint _foreground;
        private bool _insertForeground;

        public RleDecoder(
            ReadOnlySpan<byte> source,
            Span<byte> destination,
            int width,
            int pixelCount,
            int bytesPerPixel,
            ushort bpp)
        {
            _source = source;
            _destination = destination;
            _width = width;
            _pixelCount = pixelCount;
            _bytesPerPixel = bytesPerPixel;
            _bpp = bpp;
            _sourceOffset = 0;
            _destinationPixel = 0;
            _foreground = GetWhitePixel(bpp);
            _insertForeground = false;
        }

        private uint ReadSourcePixel()
        {
            if (_sourceOffset + _bytesPerPixel > _source.Length)
            {
                throw new InvalidDataException("RDP bitmap compression stream is truncated.");
            }

            uint value = 0;
            for (int i = 0; i < _bytesPerPixel; i++)
            {
                value |= (uint)_source[_sourceOffset++] << (8 * i);
            }
            return value;
        }

        private uint ReadPreviousPixel()
        {
            return _destinationPixel >= _width
                ? ReadDestinationPixel(_destination, _destinationPixel - _width, _bytesPerPixel)
                : 0;
        }

        private void WritePixel(uint value)
        {
            if (_destinationPixel >= _pixelCount)
            {
                throw new InvalidDataException("RDP bitmap compression stream expands past the declared dimensions.");
            }

            WriteDestinationPixel(_destination, _destinationPixel++, _bytesPerPixel, value);
        }

        public void Decode()
        {
            while (_sourceOffset < _source.Length && _destinationPixel < _pixelCount)
            {
                byte orderHeader = _source[_sourceOffset++];
                int code = ExtractCompressionCode(orderHeader);
                int runLength = ReadRunLength(code, orderHeader, _source, ref _sourceOffset);

                if (code is 0x00 or 0xF0)
                {
                    if (_insertForeground && runLength > 0)
                    {
                        WritePixel(ReadPreviousPixel() ^ _foreground);
                        runLength--;
                    }

                    while (runLength-- > 0)
                    {
                        WritePixel(ReadPreviousPixel());
                    }

                    _insertForeground = true;
                    continue;
                }

                _insertForeground = false;

                if (code is 0x01 or 0xF1 or 0x0C or 0xF6)
                {
                    if (code is 0x0C or 0xF6)
                    {
                        _foreground = ReadSourcePixel();
                    }

                    while (runLength-- > 0)
                    {
                        WritePixel(ReadPreviousPixel() ^ _foreground);
                    }
                    continue;
                }

                if (code is 0x0E or 0xF8)
                {
                    uint pixelA = ReadSourcePixel();
                    uint pixelB = ReadSourcePixel();
                    while (runLength-- > 0)
                    {
                        WritePixel(pixelA);
                        WritePixel(pixelB);
                    }
                    continue;
                }

                if (code is 0x03 or 0xF3)
                {
                    uint color = ReadSourcePixel();
                    while (runLength-- > 0)
                    {
                        WritePixel(color);
                    }
                    continue;
                }

                if (code is 0x02 or 0xF2 or 0x0D or 0xF7)
                {
                    if (code is 0x0D or 0xF7)
                    {
                        _foreground = ReadSourcePixel();
                    }

                    while (runLength > 0)
                    {
                        if (_sourceOffset >= _source.Length)
                        {
                            throw new InvalidDataException("RDP foreground/background image bitmask is truncated.");
                        }

                        byte bitmask = _source[_sourceOffset++];
                        int bits = Math.Min(runLength, 8);
                        for (int bit = 0; bit < bits; bit++)
                        {
                            uint previous = ReadPreviousPixel();
                            WritePixel((bitmask & (1 << bit)) != 0 ? previous ^ _foreground : previous);
                        }
                        runLength -= bits;
                    }
                    continue;
                }

                if (code is 0x04 or 0xF4)
                {
                    while (runLength-- > 0)
                    {
                        WritePixel(ReadSourcePixel());
                    }
                    continue;
                }

                if (code is 0xF9 or 0xFA)
                {
                    byte bitmask = code == 0xF9 ? (byte)0x03 : (byte)0x05;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        uint previous = ReadPreviousPixel();
                        WritePixel((bitmask & (1 << bit)) != 0 ? previous ^ _foreground : previous);
                    }
                    continue;
                }

                if (code == 0xFD)
                {
                    WritePixel(GetWhitePixel(_bpp));
                    continue;
                }

                if (code == 0xFE)
                {
                    WritePixel(0);
                    continue;
                }

                throw new InvalidDataException($"Unsupported RDP bitmap compression order 0x{code:X2}.");
            }

            if (_destinationPixel != _pixelCount)
            {
                throw new InvalidDataException(
                    $"RDP bitmap compression stream produced {_destinationPixel} of {_pixelCount} declared pixels.");
            }
        }
    }

    private static int GetBytesPerPixel(ushort bpp)
    {
        return bpp switch
        {
            15 or 16 => 2,
            24 => 3,
            32 => 4,
            _ => throw new InvalidDataException($"Unsupported RDP bitmap color depth: {bpp}.")
        };
    }

    private static int ExtractCompressionCode(byte header)
    {
        if (header >= 0xF0)
        {
            return header;
        }

        return header >= 0xC0 ? header >> 4 : header >> 5;
    }

    private static int ReadRunLength(int code, byte header, ReadOnlySpan<byte> source, ref int offset)
    {
        if (code is 0xF9 or 0xFA or 0xFD or 0xFE)
        {
            return code is 0xF9 or 0xFA ? 8 : 1;
        }

        if (code >= 0xF0)
        {
            if (offset + 2 > source.Length)
            {
                throw new InvalidDataException("RDP MEGA_MEGA compression order is truncated.");
            }

            return source[offset++] | (source[offset++] << 8);
        }

        bool lite = code >= 0x0C;
        int encoded = header & (lite ? 0x0F : 0x1F);
        bool foregroundBackground = code is 0x02 or 0x0D;
        if (encoded != 0)
        {
            return foregroundBackground ? checked(encoded * 8) : encoded;
        }

        if (offset >= source.Length)
        {
            throw new InvalidDataException("RDP compression run length is truncated.");
        }

        int extension = source[offset++];
        if (foregroundBackground)
        {
            return extension + 1;
        }

        return extension + (lite ? 16 : 32);
    }

    private static uint ReadDestinationPixel(Span<byte> buffer, int pixelIndex, int bytesPerPixel)
    {
        int offset = checked(pixelIndex * bytesPerPixel);
        uint value = 0;
        for (int i = 0; i < bytesPerPixel; i++)
        {
            value |= (uint)buffer[offset + i] << (8 * i);
        }
        return value;
    }

    private static void WriteDestinationPixel(Span<byte> buffer, int pixelIndex, int bytesPerPixel, uint value)
    {
        int offset = checked(pixelIndex * bytesPerPixel);
        for (int i = 0; i < bytesPerPixel; i++)
        {
            buffer[offset + i] = (byte)(value >> (8 * i));
        }
    }

    private static uint GetWhitePixel(ushort bpp)
    {
        return bpp switch
        {
            15 => 0x7FFF,
            16 => 0xFFFF,
            24 => 0xFFFFFF,
            32 => 0xFFFFFFFF,
            _ => 0
        };
    }

    private static void CopyRowsTopDown(
        ReadOnlySpan<byte> bottomUpSource,
        Span<byte> topDownDestination,
        int rowBytes,
        int height)
    {
        for (int destinationRow = 0; destinationRow < height; destinationRow++)
        {
            int sourceRow = height - destinationRow - 1;
            bottomUpSource.Slice(sourceRow * rowBytes, rowBytes)
                .CopyTo(topDownDestination.Slice(destinationRow * rowBytes, rowBytes));
        }
    }

    private static void CopyPaddedRowsTopDown(
        ReadOnlySpan<byte> bottomUpSource,
        Span<byte> topDownDestination,
        int sourceStride,
        int destinationRowBytes,
        int height)
    {
        for (int destinationRow = 0; destinationRow < height; destinationRow++)
        {
            int sourceRow = height - destinationRow - 1;
            bottomUpSource.Slice(sourceRow * sourceStride, destinationRowBytes)
                .CopyTo(topDownDestination.Slice(destinationRow * destinationRowBytes, destinationRowBytes));
        }
    }

    /// <summary>
    /// Converts 15, 16, 24, or 32 bpp raw pixels to 32-bit BGRA (Bgra8888) format.
    /// </summary>
    public static void ConvertPixelsToBgra32(
        ReadOnlySpan<byte> src,
        Span<byte> dst,
        int width,
        int height,
        ushort bpp)
    {
        int pixelCount = width * height;
        switch (bpp)
        {
            case 32:
                for (int i = 0; i < pixelCount; i++)
                {
                    int srcIdx = i * 4;
                    int dstIdx = i * 4;
                    if (srcIdx + 3 < src.Length && dstIdx + 3 < dst.Length)
                    {
                        dst[dstIdx + 0] = src[srcIdx + 0]; // B
                        dst[dstIdx + 1] = src[srcIdx + 1]; // G
                        dst[dstIdx + 2] = src[srcIdx + 2]; // R
                        dst[dstIdx + 3] = 0xFF;             // A
                    }
                }
                break;

            case 24:
                for (int i = 0; i < pixelCount; i++)
                {
                    int srcIdx = i * 3;
                    int dstIdx = i * 4;
                    if (srcIdx + 2 < src.Length && dstIdx + 3 < dst.Length)
                    {
                        dst[dstIdx + 0] = src[srcIdx + 0]; // B
                        dst[dstIdx + 1] = src[srcIdx + 1]; // G
                        dst[dstIdx + 2] = src[srcIdx + 2]; // R
                        dst[dstIdx + 3] = 0xFF;             // A
                    }
                }
                break;

            case 16:
                for (int i = 0; i < pixelCount; i++)
                {
                    int srcIdx = i * 2;
                    int dstIdx = i * 4;
                    if (srcIdx + 1 < src.Length && dstIdx + 3 < dst.Length)
                    {
                        ushort pixel = (ushort)(src[srcIdx] | (src[srcIdx + 1] << 8));
                        byte r5 = (byte)((pixel >> 11) & 0x1F);
                        byte g6 = (byte)((pixel >> 5) & 0x3F);
                        byte b5 = (byte)(pixel & 0x1F);

                        byte r = (byte)((r5 * 255 + 15) / 31);
                        byte g = (byte)((g6 * 255 + 31) / 63);
                        byte b = (byte)((b5 * 255 + 15) / 31);

                        dst[dstIdx + 0] = b;
                        dst[dstIdx + 1] = g;
                        dst[dstIdx + 2] = r;
                        dst[dstIdx + 3] = 0xFF;
                    }
                }
                break;

            case 15:
                for (int i = 0; i < pixelCount; i++)
                {
                    int srcIdx = i * 2;
                    int dstIdx = i * 4;
                    if (srcIdx + 1 < src.Length && dstIdx + 3 < dst.Length)
                    {
                        ushort pixel = (ushort)(src[srcIdx] | (src[srcIdx + 1] << 8));
                        byte r5 = (byte)((pixel >> 10) & 0x1F);
                        byte g5 = (byte)((pixel >> 5) & 0x1F);
                        byte b5 = (byte)(pixel & 0x1F);

                        byte r = (byte)((r5 * 255 + 15) / 31);
                        byte g = (byte)((g5 * 255 + 15) / 31);
                        byte b = (byte)((b5 * 255 + 15) / 31);

                        dst[dstIdx + 0] = b;
                        dst[dstIdx + 1] = g;
                        dst[dstIdx + 2] = r;
                        dst[dstIdx + 3] = 0xFF;
                    }
                }
                break;

            default:
                throw new ArgumentException($"Unsupported color depth: {bpp} bpp", nameof(bpp));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_pooledBuffer != null)
        {
            (_pool ?? ArrayPool<byte>.Shared).Return(_pooledBuffer);
            _pooledBuffer = null;
        }
    }
}
