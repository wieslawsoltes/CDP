using System;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace Chrome.DevTools.Protocol;

public class TiledScreencastProducer : IDisposable
{
    private const int TileSize = 64;

    private uint[]? _tileHashes;
    private int _lastCols;
    private int _lastRows;
    private int _lastWidth;
    private int _lastHeight;

    public void Reset()
    {
        _tileHashes = null;
        _lastCols = 0;
        _lastRows = 0;
        _lastWidth = 0;
        _lastHeight = 0;
    }

    public JsonArray? ProcessFrame(SKBitmap skBitmap, string format, int? quality, out int cols, out int rows)
    {
        return ProcessFrame(skBitmap, format, quality, null, out cols, out rows);
    }

    public JsonArray? ProcessFrame(SKBitmap skBitmap, string format, int? quality, SKRect? dirtyRect, out int cols, out int rows)
    {
        int pixelWidth = skBitmap.Width;
        int pixelHeight = skBitmap.Height;

        int tileWidth = TileSize;
        int tileHeight = TileSize;
        cols = (pixelWidth + tileWidth - 1) / tileWidth;
        rows = (pixelHeight + tileHeight - 1) / tileHeight;

        bool isFirstFrame = (_tileHashes == null || cols != _lastCols || rows != _lastRows || pixelWidth != _lastWidth || pixelHeight != _lastHeight);
        if (isFirstFrame)
        {
            _tileHashes = new uint[cols * rows];
            _lastCols = cols;
            _lastRows = rows;
            _lastWidth = pixelWidth;
            _lastHeight = pixelHeight;
        }

        var changedTiles = new JsonArray();
        var newHashes = new uint[cols * rows];
        IntPtr pixelsPtr = skBitmap.GetPixels();
        int rowBytes = skBitmap.RowBytes;
        int bytesPerPixel = skBitmap.BytesPerPixel;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int tx = c * tileWidth;
                int ty = r * tileHeight;
                int tw = Math.Min(tileWidth, pixelWidth - tx);
                int th = Math.Min(tileHeight, pixelHeight - ty);

                bool intersects = true;
                if (dirtyRect.HasValue && !isFirstFrame)
                {
                    intersects = tx < dirtyRect.Value.Right &&
                                 tx + tw > dirtyRect.Value.Left &&
                                 ty < dirtyRect.Value.Bottom &&
                                 ty + th > dirtyRect.Value.Top;
                }

                if (!intersects)
                {
                    newHashes[r * cols + c] = _tileHashes![r * cols + c];
                    continue;
                }

                uint hash = ComputeTileHash(pixelsPtr, rowBytes, bytesPerPixel, tx, ty, tw, th);
                newHashes[r * cols + c] = hash;

                if (isFirstFrame || _tileHashes![r * cols + c] != hash)
                {
                    var info = new SKImageInfo(tw, th, skBitmap.ColorType, skBitmap.AlphaType);
                    IntPtr tilePixels = pixelsPtr + ty * rowBytes + tx * bytesPerPixel;

                    var encodedFormat = SKEncodedImageFormat.Jpeg;
                    if (string.Equals(format, "png", StringComparison.OrdinalIgnoreCase))
                    {
                        encodedFormat = SKEncodedImageFormat.Png;
                    }
                    else if (string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase))
                    {
                        encodedFormat = SKEncodedImageFormat.Webp;
                    }

                    int q = quality ?? 80;
                    if (q < 0) q = 0;
                    if (q > 100) q = 100;

                    using var image = SKImage.FromPixels(info, tilePixels, rowBytes);
                    using var encodedData = image.Encode(encodedFormat, q);
                    if (encodedData != null)
                    {
                        var tileBase64 = Convert.ToBase64String(encodedData.ToArray());
                        changedTiles.Add(new JsonObject
                        {
                            ["x"] = c,
                            ["y"] = r,
                            ["data"] = tileBase64
                        });
                    }
                }
            }
        }

        _tileHashes = newHashes;
        return changedTiles;
    }

    private static unsafe uint ComputeTileHash(IntPtr pixels, int rowBytes, int bytesPerPixel, int tx, int ty, int tw, int th)
    {
        uint hash = 2166136261;
        byte* basePtr = (byte*)pixels;
        if (bytesPerPixel == 4)
        {
            for (int y = 0; y < th; y++)
            {
                uint* rowPtr = (uint*)(basePtr + (ty + y) * rowBytes + tx * 4);
                for (int i = 0; i < tw; i++)
                {
                    hash = (hash ^ rowPtr[i]) * 16777619;
                }
            }
        }
        else
        {
            for (int y = 0; y < th; y++)
            {
                byte* rowPtr = basePtr + (ty + y) * rowBytes + tx * bytesPerPixel;
                int length = tw * bytesPerPixel;
                for (int i = 0; i < length; i++)
                {
                    hash = (hash ^ rowPtr[i]) * 16777619;
                }
            }
        }
        return hash;
    }

    public void Dispose()
    {
        _tileHashes = null;
    }
}
