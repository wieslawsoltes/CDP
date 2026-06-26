using System;
using System.IO;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Chrome.DevTools.Protocol;
using SkiaSharp;

namespace Avalonia.Diagnostics.Cdp.Benchmarks;

[MemoryDiagnoser]
public class ScreencastBenchmarks
{
    private SKBitmap? _originalBitmap;
    private SKBitmap? _modifiedBitmap;
    private byte[]? _originalPngBytes;
    private ScreencastReconstructor? _reconstructor;

    private const int Width = 1920;
    private const int Height = 1080;
    private const int TileSize = 64;

    private uint[]? _tileHashes;
    private int _cols;
    private int _rows;

    [GlobalSetup]
    public void Setup()
    {
        _originalBitmap = new SKBitmap(Width, Height);
        using (var canvas = new SKCanvas(_originalBitmap))
        {
            canvas.Clear(SKColors.White);
            using (var paint = new SKPaint { Color = SKColors.Blue, StrokeWidth = 5 })
            {
                canvas.DrawRect(new SKRect(100, 100, 500, 500), paint);
            }
        }

        using (var ms = new MemoryStream())
        using (var img = SKImage.FromBitmap(_originalBitmap))
        using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
        {
            data.SaveTo(ms);
            _originalPngBytes = ms.ToArray();
        }

        _modifiedBitmap = new SKBitmap(Width, Height);
        using (var canvas = new SKCanvas(_modifiedBitmap))
        {
            canvas.Clear(SKColors.White);
            using (var paint = new SKPaint { Color = SKColors.Blue, StrokeWidth = 5 })
            {
                canvas.DrawRect(new SKRect(100, 100, 500, 500), paint);
            }
            using (var paint = new SKPaint { Color = SKColors.Red })
            {
                canvas.DrawCircle(600, 600, 10, paint);
            }
        }

        _cols = (Width + TileSize - 1) / TileSize;
        _rows = (Height + TileSize - 1) / TileSize;
        _tileHashes = new uint[_cols * _rows];

        _reconstructor = new ScreencastReconstructor();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _originalBitmap?.Dispose();
        _modifiedBitmap?.Dispose();
        _reconstructor?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public byte[] StandardFullFrameEncodeDecode()
    {
        using var ms = new MemoryStream(_originalPngBytes!);
        using var decoded = SKBitmap.Decode(ms);

        using var image = SKImage.FromBitmap(decoded);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }

    [Benchmark]
    public (JsonArray, uint[]) TiledScreencastGeneration()
    {
        using var ms = new MemoryStream(_originalPngBytes!);
        using var skBitmap = SKBitmap.Decode(ms);

        var changedTiles = new JsonArray();
        var newHashes = new uint[_cols * _rows];

        IntPtr pixelsPtr = skBitmap.GetPixels();
        int rowBytes = skBitmap.RowBytes;
        int bytesPerPixel = skBitmap.BytesPerPixel;

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                int tx = c * TileSize;
                int ty = r * TileSize;
                int tw = Math.Min(TileSize, Width - tx);
                int th = Math.Min(TileSize, Height - ty);

                uint hash = ComputeTileHash(pixelsPtr, rowBytes, bytesPerPixel, tx, ty, tw, th);
                newHashes[r * _cols + c] = hash;

                if (_tileHashes![r * _cols + c] != hash)
                {
                    using var tileBitmap = new SKBitmap(tw, th);
                    using (var canvas = new SKCanvas(tileBitmap))
                    {
                        canvas.Clear(SKColors.Transparent);
                        var srcRect = new SKRect(tx, ty, tx + tw, ty + th);
                        var destRect = new SKRect(0, 0, tw, th);
                        canvas.DrawBitmap(skBitmap, srcRect, destRect);
                    }

                    using var image = SKImage.FromBitmap(tileBitmap);
                    using var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, 80);
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

        return (changedTiles, newHashes);
    }

    [Benchmark]
    public (JsonArray, uint[]) TiledScreencastGenerationOptimized()
    {
        using var ms = new MemoryStream(_originalPngBytes!);
        using var skBitmap = SKBitmap.Decode(ms);

        var changedTiles = new JsonArray();
        var newHashes = new uint[_cols * _rows];

        IntPtr pixelsPtr = skBitmap.GetPixels();
        int rowBytes = skBitmap.RowBytes;
        int bytesPerPixel = skBitmap.BytesPerPixel;

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                int tx = c * TileSize;
                int ty = r * TileSize;
                int tw = Math.Min(TileSize, Width - tx);
                int th = Math.Min(TileSize, Height - ty);

                uint hash = ComputeTileHashUint(pixelsPtr, rowBytes, bytesPerPixel, tx, ty, tw, th);
                newHashes[r * _cols + c] = hash;

                if (_tileHashes![r * _cols + c] != hash)
                {
                    var info = new SKImageInfo(tw, th, skBitmap.ColorType, skBitmap.AlphaType);
                    IntPtr tilePixels = pixelsPtr + ty * rowBytes + tx * bytesPerPixel;
                    using var image = SKImage.FromPixels(info, tilePixels, rowBytes);

                    using var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, 80);
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

        return (changedTiles, newHashes);
    }

    [Benchmark]
    public unsafe uint FastFNV1aHash()
    {
        IntPtr pixelsPtr = _originalBitmap!.GetPixels();
        int rowBytes = _originalBitmap.RowBytes;
        int bytesPerPixel = _originalBitmap.BytesPerPixel;
        return ComputeTileHash(pixelsPtr, rowBytes, bytesPerPixel, 100, 100, 64, 64);
    }

    [Benchmark]
    public unsafe uint FastFNV1aHashUint()
    {
        IntPtr pixelsPtr = _originalBitmap!.GetPixels();
        int rowBytes = _originalBitmap.RowBytes;
        int bytesPerPixel = _originalBitmap.BytesPerPixel;
        return ComputeTileHashUint(pixelsPtr, rowBytes, bytesPerPixel, 100, 100, 64, 64);
    }

    [Benchmark]
    public uint SafeGetPixelHash()
    {
        uint hash = 2166136261;
        for (int y = 100; y < 164; y++)
        {
            for (int x = 100; x < 164; x++)
            {
                var color = _originalBitmap!.GetPixel(x, y);
                hash = (hash ^ color.Red) * 16777619;
                hash = (hash ^ color.Green) * 16777619;
                hash = (hash ^ color.Blue) * 16777619;
                hash = (hash ^ color.Alpha) * 16777619;
            }
        }
        return hash;
    }

    private static unsafe uint ComputeTileHash(IntPtr pixels, int rowBytes, int bytesPerPixel, int tx, int ty, int tw, int th)
    {
        uint hash = 2166136261;
        byte* basePtr = (byte*)pixels;
        for (int y = 0; y < th; y++)
        {
            byte* rowPtr = basePtr + (ty + y) * rowBytes + tx * bytesPerPixel;
            int length = tw * bytesPerPixel;
            for (int i = 0; i < length; i++)
            {
                hash = (hash ^ rowPtr[i]) * 16777619;
            }
        }
        return hash;
    }

    private static unsafe uint ComputeTileHashUint(IntPtr pixels, int rowBytes, int bytesPerPixel, int tx, int ty, int tw, int th)
    {
        uint hash = 2166136261;
        byte* basePtr = (byte*)pixels;
        for (int y = 0; y < th; y++)
        {
            uint* rowPtr = (uint*)(basePtr + (ty + y) * rowBytes + tx * bytesPerPixel);
            for (int i = 0; i < tw; i++)
            {
                hash = (hash ^ rowPtr[i]) * 16777619;
            }
        }
        return hash;
    }
}
