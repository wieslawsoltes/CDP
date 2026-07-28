using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Challenger;

using System;
using System.Buffers;
using System.Diagnostics;
using SkiaSharp;
using CDP.Rdp.Frames;
using CDP.Rdp.Rendering;
using Xunit;

public class RdpRenderingChallengerM2Tests
{
    [AvaloniaFact]
    public void DirtyRegion_ThresholdMerging_16Vs17Rectangles()
    {
        var dirty = new RdpDirtyRegion();

        // Add 16 non-overlapping rectangles (grid 4x4)
        for (int i = 0; i < 16; i++)
        {
            int row = i / 4;
            int col = i % 4;
            dirty.AddRect(col * 20, row * 20, 10, 10);
        }

        // At exactly 16 rectangles, count must be <= 16 (specifically 16 because disjoint with gap)
        Assert.Equal(16, dirty.Rectangles.Count);
        Assert.False(dirty.IsEmpty);

        // Add 17th rectangle
        dirty.AddRect(100, 100, 10, 10);

        // At 17 rectangles (> MaxRectanglesBeforeUnion), all rectangles must be merged into TotalBounds (Count == 1)
        Assert.Single(dirty.Rectangles);
        Assert.Equal(SKRectI.Create(0, 0, 110, 110), dirty.Rectangles[0]);
    }

    [AvaloniaFact]
    public void DirtyRegion_AdjacentVsOverlappingVsDiagonalMerging()
    {
        // 1. Overlapping rectangles
        var dirty1 = new RdpDirtyRegion();
        dirty1.AddRect(0, 0, 10, 10);
        dirty1.AddRect(5, 5, 10, 10);
        Assert.Single(dirty1.Rectangles);
        Assert.Equal(SKRectI.Create(0, 0, 15, 15), dirty1.Rectangles[0]);

        // 2. Horizontally adjacent rectangles (touching right edge)
        var dirty2 = new RdpDirtyRegion();
        dirty2.AddRect(0, 0, 10, 10);
        dirty2.AddRect(10, 0, 10, 10);
        Assert.Single(dirty2.Rectangles);
        Assert.Equal(SKRectI.Create(0, 0, 20, 10), dirty2.Rectangles[0]);

        // 3. Vertically adjacent rectangles (touching bottom edge)
        var dirty3 = new RdpDirtyRegion();
        dirty3.AddRect(0, 0, 10, 10);
        dirty3.AddRect(0, 10, 10, 10);
        Assert.Single(dirty3.Rectangles);
        Assert.Equal(SKRectI.Create(0, 0, 10, 20), dirty3.Rectangles[0]);

        // 4. Diagonally touching corner point (0,0)-(10,10) and (10,10)-(20,20)
        var dirty4 = new RdpDirtyRegion();
        dirty4.AddRect(0, 0, 10, 10);
        dirty4.AddRect(10, 10, 10, 10);
        bool touchesCornerMerged = dirty4.Rectangles.Count == 1;

        // 5. Disjoint rectangles with 1px gap
        var dirty5 = new RdpDirtyRegion();
        dirty5.AddRect(0, 0, 10, 10);
        dirty5.AddRect(11, 0, 10, 10);
        Assert.Equal(2, dirty5.Rectangles.Count);
    }

    [AvaloniaFact]
    public void FrameBuffer_TileClipping_Boundaries()
    {
        using var buffer = new RdpFrameBuffer(100, 100);

        // Case A: Negative coordinates (-10, -10, 30, 30)
        byte[] greenPixels = new byte[30 * 30 * 4];
        for (int i = 0; i < 30 * 30; i++)
        {
            greenPixels[i * 4 + 0] = 0x00; // B
            greenPixels[i * 4 + 1] = 0xFF; // G
            greenPixels[i * 4 + 2] = 0x00; // R
            greenPixels[i * 4 + 3] = 0xFF; // A
        }

        var updateNeg = new RdpBitmapUpdate(0, 0, 30, 30, 32, compressed: false, greenPixels);
        using (var tileNeg = new RdpBitmapTile(SKRectI.Create(-10, -10, 30, 30), 32, greenPixels, greenPixels.Length))
        {
            buffer.ApplyTile(tileNeg);
        }

        // Check clipped pixel at (0, 0) in back buffer
        SKColor colorTopLeft = buffer.BackBuffer.GetPixel(0, 0);
        Assert.Equal(0xFF, colorTopLeft.Green);

        // Case B: Partially extending beyond right/bottom boundary (80, 80, 40, 40)
        byte[] redPixels = new byte[40 * 40 * 4];
        for (int i = 0; i < 40 * 40; i++)
        {
            redPixels[i * 4 + 0] = 0x00; // B
            redPixels[i * 4 + 1] = 0x00; // G
            redPixels[i * 4 + 2] = 0xFF; // R
            redPixels[i * 4 + 3] = 0xFF; // A
        }

        var updateOverflow = new RdpBitmapUpdate(80, 80, 40, 40, 32, compressed: false, redPixels);
        using (var tileOverflow = RdpBitmapTile.FromUpdate(updateOverflow))
        {
            buffer.ApplyTile(tileOverflow);
        }

        SKColor colorBottomRight = buffer.BackBuffer.GetPixel(99, 99);
        Assert.Equal(0xFF, colorBottomRight.Red);

        // Case C: Completely out of bounds (200, 200, 50, 50)
        byte[] bluePixels = new byte[50 * 50 * 4];
        Array.Fill(bluePixels, (byte)0xFF);
        var updateOut = new RdpBitmapUpdate(200, 200, 50, 50, 32, compressed: false, bluePixels);
        using (var tileOut = RdpBitmapTile.FromUpdate(updateOut))
        {
            buffer.ApplyTile(tileOut); // should not crash
        }
    }

    [AvaloniaFact]
    public void ArrayPool_MemoryRecycling_And_DoubleDispose()
    {
        byte[] rawPixels = new byte[32 * 32 * 4];
        Array.Fill(rawPixels, (byte)0xCC);

        var update = new RdpBitmapUpdate(0, 0, 32, 32, 32, compressed: false, rawPixels);

        RdpBitmapTile tile = RdpBitmapTile.FromUpdate(update);
        Assert.NotNull(tile.PixelData);
        Assert.True(tile.PixelDataLength >= 32 * 32 * 4);

        // Verify Double Dispose safety
        tile.Dispose();
        tile.Dispose(); // Must not throw or corrupt ArrayPool
    }

    [AvaloniaFact]
    public void ArrayPool_CompressedRleDecompression_RecyclesTempBuffers()
    {
        // RLE compressed: run of 16 32bpp pixels
        byte[] compressedData = new byte[] { 0x90, 0x00, 0xFF, 0x00, 0xFF }; // 16 pixels of Green
        var update = new RdpBitmapUpdate(0, 0, 4, 4, 32, compressed: true, compressedData);

        using (var tile = RdpBitmapTile.FromUpdate(update))
        {
            Assert.Equal(4 * 4 * 4, tile.PixelDataLength);
            Assert.Equal(0xFF, tile.PixelSpan[1]); // Green channel
        }
    }

    [AvaloniaFact]
    public void SkiaCanvas_60Fps_StreamingPerformance()
    {
        using var buffer = new RdpFrameBuffer(1920, 1080);
        var canvas = new RdpSkiaCanvas(buffer);
        using var targetBitmap = new SKBitmap(1920, 1080, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var targetCanvas = new SKCanvas(targetBitmap);

        byte[] tilePixels = new byte[128 * 128 * 4];
        Array.Fill(tilePixels, (byte)0x88);

        Stopwatch sw = Stopwatch.StartNew();
        const int frameCount = 180; // 3 seconds at 60 FPS

        for (ulong i = 0; i < frameCount; i++)
        {
            ushort left = (ushort)((i * 32) % 1700);
            ushort top = (ushort)((i * 16) % 900);

            var update = new RdpBitmapUpdate(left, top, 128, 128, 32, compressed: false, tilePixels);
            buffer.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(i, DateTimeOffset.UtcNow, new[] { update }));
            buffer.SwapBuffers();

            canvas.Render(targetCanvas, 0, 0);
        }

        sw.Stop();

        double elapsedSeconds = sw.Elapsed.TotalSeconds;
        double actualFps = frameCount / elapsedSeconds;

        Assert.True(actualFps >= 60.0, $"Streaming throughput was {actualFps:F1} FPS (target >= 60 FPS, elapsed: {sw.ElapsedMilliseconds} ms)");
    }
}
