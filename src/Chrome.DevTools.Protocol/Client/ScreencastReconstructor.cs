using System;
using System.IO;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace Chrome.DevTools.Protocol;

public class ScreencastReconstructor : IDisposable
{
    private SKBitmap? _backingBitmap;
    private SKCanvas? _canvas;
    private readonly object _lock = new();

    public SKBitmap? BackingBitmap
    {
        get
        {
            lock (_lock)
            {
                return _backingBitmap;
            }
        }
    }

    public bool CopyTo(IntPtr destAddress, int destRowBytes, int destWidth, int destHeight)
    {
        lock (_lock)
        {
            if (_backingBitmap == null || _backingBitmap.Width != destWidth || _backingBitmap.Height != destHeight)
            {
                return false;
            }

            unsafe
            {
                byte* src = (byte*)_backingBitmap.GetPixels();
                byte* dest = (byte*)destAddress;
                int srcRowBytes = _backingBitmap.RowBytes;
                int bytesToCopy = Math.Min(srcRowBytes, destRowBytes);
                int height = Math.Min(_backingBitmap.Height, destHeight);

                for (int y = 0; y < height; y++)
                {
                    System.Buffer.MemoryCopy(
                        src + y * srcRowBytes,
                        dest + y * destRowBytes,
                        destRowBytes,
                        bytesToCopy);
                }
            }
            return true;
        }
    }

    public void Update(int pixelWidth, int pixelHeight, int tileWidth, int tileHeight, JsonArray tiles)
    {
        lock (_lock)
        {
            if (_backingBitmap == null || _backingBitmap.Width != pixelWidth || _backingBitmap.Height != pixelHeight)
            {
                _canvas?.Dispose();
                _backingBitmap?.Dispose();

                _backingBitmap = new SKBitmap(pixelWidth, pixelHeight);
                _canvas = new SKCanvas(_backingBitmap);
                _canvas.Clear(SKColors.Transparent);
            }

            foreach (var tileNode in tiles)
            {
                if (tileNode is not JsonObject tileObj) continue;

                int col = tileObj["x"]?.GetValue<int>() ?? 0;
                int row = tileObj["y"]?.GetValue<int>() ?? 0;
                string tileBase64 = tileObj["data"]?.GetValue<string>() ?? "";

                if (string.IsNullOrEmpty(tileBase64)) continue;

                try
                {
                    byte[] tileBytes = Convert.FromBase64String(tileBase64);
                    using var ms = new MemoryStream(tileBytes);
                    using var tileBitmap = SKBitmap.Decode(ms);
                    if (tileBitmap != null)
                    {
                        int tx = col * tileWidth;
                        int ty = row * tileHeight;
                        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
                        _canvas.DrawBitmap(tileBitmap, tx, ty, paint);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error decoding tile ({col}, {row}): {ex.Message}");
                }
            }
        }
    }

    public byte[] EncodeToJpeg(int quality = 80)
    {
        lock (_lock)
        {
            if (_backingBitmap == null) return Array.Empty<byte>();
            using var image = SKImage.FromBitmap(_backingBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            return data?.ToArray() ?? Array.Empty<byte>();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _canvas?.Dispose();
            _backingBitmap?.Dispose();
            _canvas = null;
            _backingBitmap = null;
        }
    }
}
