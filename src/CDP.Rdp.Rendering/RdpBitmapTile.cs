namespace CDP.Rdp.Rendering;

using System;
using System.Buffers;
using SkiaSharp;
using CDP.Rdp.Frames;

/// <summary>
/// Decoded bitmap tile containing SkiaSharp-compatible (BGRA8888) pixel data and destination bounds.
/// Supports zero-allocation buffer rental via ArrayPool byte management.
/// </summary>
public sealed class RdpBitmapTile : IDisposable
{
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
        int pixelCount = width * height;
        int bgraByteCount = pixelCount * 4;

        byte[] bgraBuffer = targetPool.Rent(bgraByteCount);

        ReadOnlySpan<byte> rawSource = update.Data.Span;
        byte[]? decompressedPoolBuffer = null;

        try
        {
            ReadOnlySpan<byte> uncompressedPixels;
            if (update.Compressed)
            {
                int bytesPerInputPixel = update.Bpp switch
                {
                    15 or 16 => 2,
                    24 => 3,
                    32 => 4,
                    _ => 4
                };
                int decompressedSize = pixelCount * bytesPerInputPixel;
                decompressedPoolBuffer = targetPool.Rent(decompressedSize);
                DecompressRle(rawSource, decompressedPoolBuffer.AsSpan(0, decompressedSize), width, height, update.Bpp);
                uncompressedPixels = decompressedPoolBuffer.AsSpan(0, decompressedSize);
            }
            else
            {
                uncompressedPixels = rawSource;
            }

            ConvertPixelsToBgra32(uncompressedPixels, bgraBuffer.AsSpan(0, bgraByteCount), width, height, update.Bpp);

            if (decompressedPoolBuffer != null)
            {
                targetPool.Return(decompressedPoolBuffer);
                decompressedPoolBuffer = null;
            }

            SKRectI bounds = SKRectI.Create(update.Left, update.Top, update.Width, update.Height);
            return new RdpBitmapTile(bounds, update.Bpp, bgraBuffer, bgraByteCount, bgraBuffer, targetPool);
        }
        catch
        {
            if (decompressedPoolBuffer != null)
            {
                targetPool.Return(decompressedPoolBuffer);
            }

            targetPool.Return(bgraBuffer);
            throw;
        }
    }

    /// <summary>
    /// Decompresses RLE-encoded RDP bitmap data into an uncompressed pixel span.
    /// </summary>
    public static int DecompressRle(ReadOnlySpan<byte> src, Span<byte> dst, int width, int height, ushort bpp)
    {
        int bytesPerPixel = bpp switch
        {
            15 or 16 => 2,
            24 => 3,
            32 => 4,
            _ => 4
        };
        int expectedLength = width * height * bytesPerPixel;
        if (src.IsEmpty)
        {
            dst[..Math.Min(dst.Length, expectedLength)].Clear();
            return expectedLength;
        }

        int srcIdx = 0;
        int dstIdx = 0;

        while (srcIdx < src.Length && dstIdx < expectedLength)
        {
            byte control = src[srcIdx++];
            bool isRun = (control & 0x80) != 0;
            int count = control & 0x7F;

            if (count == 0 && isRun)
            {
                if (srcIdx < src.Length)
                {
                    count = src[srcIdx++] + 128;
                }
            }
            else if (count == 0)
            {
                if (srcIdx < src.Length)
                {
                    count = src[srcIdx++];
                }
            }

            if (count == 0) count = 1;

            if (isRun)
            {
                if (srcIdx + bytesPerPixel <= src.Length)
                {
                    ReadOnlySpan<byte> pixel = src.Slice(srcIdx, bytesPerPixel);
                    srcIdx += bytesPerPixel;

                    for (int r = 0; r < count && dstIdx + bytesPerPixel <= dst.Length; r++)
                    {
                        pixel.CopyTo(dst.Slice(dstIdx, bytesPerPixel));
                        dstIdx += bytesPerPixel;
                    }
                }
                else
                {
                    break;
                }
            }
            else
            {
                int bytesToCopy = Math.Min(count * bytesPerPixel, src.Length - srcIdx);
                bytesToCopy = Math.Min(bytesToCopy, dst.Length - dstIdx);
                if (bytesToCopy > 0)
                {
                    src.Slice(srcIdx, bytesToCopy).CopyTo(dst.Slice(dstIdx, bytesToCopy));
                    srcIdx += bytesToCopy;
                    dstIdx += bytesToCopy;
                }
            }
        }

        if (dstIdx < expectedLength && dstIdx < dst.Length)
        {
            dst[dstIdx..Math.Min(dst.Length, expectedLength)].Clear();
        }

        return expectedLength;
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
