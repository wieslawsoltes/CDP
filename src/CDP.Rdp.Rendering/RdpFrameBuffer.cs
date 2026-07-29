namespace CDP.Rdp.Rendering;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Rdp.Frames;

/// <summary>
/// Thread-safe double-buffered RDP offscreen frame buffer engine with SkiaSharp compositing,
/// high-speed unsafe pixel memory blitting, and dirty region tracking.
/// </summary>
public sealed class RdpFrameBuffer : IDisposable
{
    private readonly object _lock = new();

    public object SyncRoot => _lock;

    private SKBitmap _backBuffer;
    private SKBitmap _frontBuffer;
    private readonly RdpDirtyRegion _backDirtyRegion = new();
    private readonly RdpDirtyRegion _frontDirtyRegion = new();
    private bool _disposed;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public ulong CurrentFrameId { get; private set; }
    public DateTimeOffset LastFrameTimestamp { get; private set; }

    public SKBitmap BackBuffer => _backBuffer;
    public SKBitmap FrontBuffer => _frontBuffer;
    public IReadOnlyList<SKRectI> DirtyRegions => _frontDirtyRegion.Rectangles;

    public RdpFrameBuffer(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException("Dimensions must be positive.");

        Width = width;
        Height = height;

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        _backBuffer = new SKBitmap(info);
        _frontBuffer = new SKBitmap(info);

        _backBuffer.Erase(SKColors.Black);
        _frontBuffer.Erase(SKColors.Black);
    }

    /// <summary>
    /// Applies a frame update to the back buffer and records dirty regions.
    /// </summary>
    public void ApplyFrameUpdate(RdpFrameUpdateEventArgs args)
    {
        if (args == null || args.BitmapUpdates == null || args.BitmapUpdates.Count == 0)
            return;

        lock (_lock)
        {
            CurrentFrameId = args.FrameId;
            LastFrameTimestamp = args.Timestamp;

            var updates = args.BitmapUpdates;
            for (int i = 0; i < updates.Count; i++)
            {
                var update = updates[i];
                using var tile = RdpBitmapTile.FromUpdate(update);
                BlitTileToBitmapLocked(_backBuffer, tile);
                _backDirtyRegion.AddRect(tile.Bounds);
            }
        }
    }

    /// <summary>
    /// Alias method for ApplyFrameUpdate.
    /// </summary>
    public void ApplyUpdate(RdpFrameUpdateEventArgs args) => ApplyFrameUpdate(args);

    /// <summary>
    /// Applies a single decoded RdpBitmapTile to the back buffer.
    /// </summary>
    public void ApplyTile(RdpBitmapTile tile)
    {
        if (tile == null) return;

        lock (_lock)
        {
            BlitTileToBitmapLocked(_backBuffer, tile);
            _backDirtyRegion.AddRect(tile.Bounds);
        }
    }

    private unsafe void BlitTileToBitmapLocked(SKBitmap targetBitmap, RdpBitmapTile tile)
    {
        int clipLeft = Math.Max(0, tile.Left);
        int clipTop = Math.Max(0, tile.Top);
        int clipRight = Math.Min(Width, tile.Left + tile.Width);
        int clipBottom = Math.Min(Height, tile.Top + tile.Height);

        int copyWidth = clipRight - clipLeft;
        int copyHeight = clipBottom - clipTop;

        if (copyWidth <= 0 || copyHeight <= 0)
            return;

        byte* targetPtr = (byte*)targetBitmap.GetPixels();
        int targetRowBytes = targetBitmap.RowBytes;

        fixed (byte* srcPtr = tile.PixelData)
        {
            int srcRowBytes = tile.Width * 4;

            for (int y = 0; y < copyHeight; y++)
            {
                int srcY = (clipTop - tile.Top) + y;
                int dstY = clipTop + y;

                byte* srcRow = srcPtr + (srcY * srcRowBytes) + ((clipLeft - tile.Left) * 4);
                byte* dstRow = targetPtr + (dstY * targetRowBytes) + (clipLeft * 4);

                long bytesToCopy = (long)copyWidth * 4;
                Buffer.MemoryCopy(srcRow, dstRow, bytesToCopy, bytesToCopy);
            }
        }
    }

    /// <summary>
    /// Swaps back and front buffers atomically, transferring dirty region pixel content to the front buffer.
    /// Returns the accumulated dirty region copied to front buffer.
    /// </summary>
    public RdpDirtyRegion SwapBuffers()
    {
        lock (_lock)
        {
            if (!_backDirtyRegion.IsEmpty)
            {
                CopyDirtyRegionsToFrontLocked();

                _frontDirtyRegion.Clear();
                var backRects = _backDirtyRegion.Rectangles;
                for (int i = 0; i < backRects.Count; i++)
                {
                    _frontDirtyRegion.AddRect(backRects[i]);
                }

                _backDirtyRegion.Clear();
            }

            return _frontDirtyRegion.Clone();
        }
    }

    private unsafe void CopyDirtyRegionsToFrontLocked()
    {
        byte* srcPixels = (byte*)_backBuffer.GetPixels();
        byte* dstPixels = (byte*)_frontBuffer.GetPixels();
        int rowBytes = _backBuffer.RowBytes;

        var backRects = _backDirtyRegion.Rectangles;
        for (int i = 0; i < backRects.Count; i++)
        {
            var rect = backRects[i];
            int clipLeft = Math.Max(0, rect.Left);
            int clipTop = Math.Max(0, rect.Top);
            int clipRight = Math.Min(Width, rect.Right);
            int clipBottom = Math.Min(Height, rect.Bottom);

            int copyWidth = clipRight - clipLeft;
            int copyHeight = clipBottom - clipTop;

            if (copyWidth <= 0 || copyHeight <= 0)
                continue;

            for (int y = 0; y < copyHeight; y++)
            {
                int lineY = clipTop + y;
                byte* srcRow = srcPixels + (lineY * rowBytes) + (clipLeft * 4);
                byte* dstRow = dstPixels + (lineY * rowBytes) + (clipLeft * 4);
                long bytesToCopy = (long)copyWidth * 4;
                Buffer.MemoryCopy(srcRow, dstRow, bytesToCopy, bytesToCopy);
            }
        }
    }

    public void ClearDirtyRegions()
    {
        lock (_lock)
        {
            _backDirtyRegion.Clear();
            _frontDirtyRegion.Clear();
        }
    }

    /// <summary>
    /// Renders the current front buffer onto a target Skia canvas at specified offset.
    /// </summary>
    public void RenderToCanvas(SKCanvas canvas, float destX = 0, float destY = 0, SKPaint? paint = null)
    {
        if (canvas == null) throw new ArgumentNullException(nameof(canvas));

        lock (_lock)
        {
            canvas.DrawBitmap(_frontBuffer, destX, destY, paint);
        }
    }

    /// <summary>
    /// Renders a source rectangle of the front buffer onto a target Skia canvas destination rectangle.
    /// </summary>
    public void RenderToCanvas(SKCanvas canvas, SKRect srcRect, SKRect destRect, SKPaint? paint = null)
    {
        if (canvas == null) throw new ArgumentNullException(nameof(canvas));

        lock (_lock)
        {
            canvas.DrawBitmap(_frontBuffer, srcRect, destRect, paint);
        }
    }

    /// <summary>
    /// Creates a deep copy of the current front buffer SKBitmap.
    /// Caller is responsible for disposing the returned SKBitmap.
    /// </summary>
    public SKBitmap GetFrontBufferSnapshot()
    {
        lock (_lock)
        {
            return _frontBuffer.Copy();
        }
    }

    /// <summary>
    /// Resizes frame buffer dimensions, clearing existing buffer contents.
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth <= 0 || newHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(newWidth), "Dimensions must be positive.");

        lock (_lock)
        {
            if (Width == newWidth && Height == newHeight)
                return;

            Width = newWidth;
            Height = newHeight;

            _backBuffer?.Dispose();
            _frontBuffer?.Dispose();

            var info = new SKImageInfo(newWidth, newHeight, SKColorType.Bgra8888, SKAlphaType.Opaque);
            _backBuffer = new SKBitmap(info);
            _frontBuffer = new SKBitmap(info);

            _backBuffer.Erase(SKColors.Black);
            _frontBuffer.Erase(SKColors.Black);

            _backDirtyRegion.Clear();
            _frontDirtyRegion.Clear();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            _backBuffer?.Dispose();
            _frontBuffer?.Dispose();
        }
    }
}
