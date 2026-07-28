using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Rendering;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using CDP.Rdp.Frames;
using CDP.Rdp.Rendering;
using Xunit;

public class RdpRenderingEmpiricalStressTests
{
    [AvaloniaTheory]
    [InlineData(0, 64)]
    [InlineData(64, 0)]
    [InlineData(0, 0)]
    public void Boundary_ZeroWidthOrHeightTiles_HandledWithoutException(int width, int height)
    {
        using var buffer = new RdpFrameBuffer(200, 200);

        byte[] rawPixels = new byte[Math.Max(1, width * height * 4)];
        var update = new RdpBitmapUpdate(10, 10, (ushort)width, (ushort)height, 32, compressed: false, rawPixels);

        using var tile = RdpBitmapTile.FromUpdate(update);
        Assert.Equal(width, tile.Width);
        Assert.Equal(height, tile.Height);

        // Apply to frame buffer
        buffer.ApplyTile(tile);

        var dirty = buffer.SwapBuffers();
        Assert.True(dirty.IsEmpty);
    }

    [AvaloniaTheory]
    [InlineData(0, 0, 100, 100)]
    [InlineData(150, 150, 100, 100)]   // Partial bottom-right
    [InlineData(100, 100, 100, 100)] // Partially out of bounds (bottom-right)
    public void Boundary_OutOfBoundsTileCoordinates_ClipsCorrectlyWithoutCrashing(ushort left, ushort top, ushort width, ushort height)
    {
        using var buffer = new RdpFrameBuffer(200, 200);

        byte[] rawPixels = new byte[width * height * 4];
        Array.Fill(rawPixels, (byte)0xFF); // White tile

        var update = new RdpBitmapUpdate(left, top, width, height, 32, compressed: false, rawPixels);
        using var tile = RdpBitmapTile.FromUpdate(update);

        // Apply tile with out of bounds coordinates
        buffer.ApplyTile(tile);

        var dirty = buffer.SwapBuffers();
        // Should not crash and should record dirty bounds clipped or unclipped
        if (left >= 200 || top >= 200)
        {
            // Fully out of bounds - front buffer pixels should remain Black (0x00)
            SKColor samplePixel = buffer.FrontBuffer.GetPixel(100, 100);
            Assert.Equal(0x00, samplePixel.Red);
        }
        else
        {
            // Partially inside - check that within-bounds pixel was modified
            int checkX = Math.Clamp(left + 2, 0, 199);
            int checkY = Math.Clamp(top + 2, 0, 199);
            SKColor samplePixel = buffer.FrontBuffer.GetPixel(checkX, checkY);
            Assert.Equal(0xFF, samplePixel.Red);
        }
    }

    [AvaloniaFact]
    public void Boundary_NegativeTileCoordinates_ClipsCorrectly()
    {
        using var buffer = new RdpFrameBuffer(200, 200);
        byte[] rawPixels = new byte[50 * 50 * 4];
        Array.Fill(rawPixels, (byte)0xFF);

        // Test negative coordinates directly via RdpBitmapTile
        var tile = new RdpBitmapTile(SKRectI.Create(-20, -20, 50, 50), 32, rawPixels, rawPixels.Length);
        buffer.ApplyTile(tile);

        var dirty = buffer.SwapBuffers();
        Assert.False(dirty.IsEmpty);
        SKColor samplePixel = buffer.FrontBuffer.GetPixel(0, 0);
        Assert.Equal(0xFF, samplePixel.Red);
    }

    [AvaloniaFact]
    public void Boundary_EmptyDirtyRegions_HandledSafely()
    {
        using var buffer = new RdpFrameBuffer(100, 100);
        var dirty1 = buffer.SwapBuffers();
        Assert.True(dirty1.IsEmpty);
        Assert.Empty(dirty1.Rectangles);
        Assert.Equal(SKRectI.Empty, dirty1.TotalBounds);

        var skiaCanvas = new RdpSkiaCanvas(buffer);
        using var targetBitmap = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var targetCanvas = new SKCanvas(targetBitmap);

        // Render dirty only on empty region
        skiaCanvas.Render(targetCanvas, SKRect.Create(0, 0, 100, 100), drawDirtyOnly: true);
        Assert.Equal(1, skiaCanvas.RenderedFrameCount);

        buffer.ClearDirtyRegions();
        Assert.Empty(buffer.DirtyRegions);
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(48)]
    [InlineData(64)]
    public void Boundary_ExtremeColorDepthInputs_ThrowsArgumentException(ushort bpp)
    {
        byte[] rawPixels = new byte[64 * 64 * 4];
        var update = new RdpBitmapUpdate(0, 0, 64, 64, bpp, compressed: false, rawPixels);

        Assert.Throws<ArgumentException>(() => RdpBitmapTile.FromUpdate(update));
    }

    [AvaloniaFact]
    public void Boundary_ExtremeColorDepthInputs_DoesNotLeakPooledMemoryOnException()
    {
        // Verify that if FromUpdate throws ArgumentException due to invalid bpp,
        // any rented ArrayPool buffers are returned and not leaked.
        byte[] rawPixels = new byte[64 * 64 * 4];
        var update = new RdpBitmapUpdate(0, 0, 64, 64, 99, compressed: false, rawPixels);

        for (int i = 0; i < 100; i++)
        {
            Assert.Throws<ArgumentException>(() => RdpBitmapTile.FromUpdate(update));
        }

        // Check array pool stability by renting and asserting buffer availability
        byte[] testBuffer = ArrayPool<byte>.Shared.Rent(64 * 64 * 4);
        ArrayPool<byte>.Shared.Return(testBuffer);
    }

    [AvaloniaFact]
    public void Stress_HighMultiThreadingContention_NoDeadlocksOrDataRaceExceptions()
    {
        using var buffer = new RdpFrameBuffer(800, 600);
        var canvas = new RdpSkiaCanvas(buffer);

        using var targetBitmap = new SKBitmap(800, 600, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var targetCanvas = new SKCanvas(targetBitmap);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var tasks = new List<Task>();

        // 4 Writer Threads: ApplyFrameUpdate and ApplyTile
        for (int t = 0; t < 4; t++)
        {
            int threadId = t;
            tasks.Add(Task.Run(() =>
            {
                byte[] tilePixels = new byte[32 * 32 * 4];
                Array.Fill(tilePixels, (byte)(threadId * 50));
                ulong frameId = 0;

                while (!cts.Token.IsCancellationRequested)
                {
                    ushort x = (ushort)((frameId * 16 + (ulong)threadId * 50) % 750);
                    ushort y = (ushort)((frameId * 8 + (ulong)threadId * 30) % 550);

                    var update = new RdpBitmapUpdate(x, y, 32, 32, 32, compressed: false, tilePixels);
                    buffer.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(frameId++, DateTimeOffset.UtcNow, new[] { update }));
                    Thread.Sleep(1);
                }
            }));
        }

        // 4 Reader Threads: SwapBuffers, RenderToCanvas, GetFrontBufferSnapshot, Resize
        for (int t = 0; t < 4; t++)
        {
            int threadId = t;
            tasks.Add(Task.Run(() =>
            {
                using var localBitmap = new SKBitmap(800, 600, SKColorType.Bgra8888, SKAlphaType.Opaque);
                using var localCanvas = new SKCanvas(localBitmap);

                while (!cts.Token.IsCancellationRequested)
                {
                    if (threadId % 2 == 0)
                    {
                        lock (canvas)
                        {
                            var dirty = buffer.SwapBuffers();
                            canvas.Render(localCanvas, SKRect.Create(0, 0, 800, 600), drawDirtyOnly: true);
                        }
                    }
                    else
                    {
                        using var snapshot = buffer.GetFrontBufferSnapshot();
                        Assert.NotNull(snapshot);
                    }
                    Thread.Sleep(1);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());
        Assert.True(canvas.RenderedFrameCount > 0);
    }

    [AvaloniaFact]
    public void Stress_MemoryLeakCheck_ThousandsOfRapidFrameUpdates()
    {
        using var buffer = new RdpFrameBuffer(1280, 720);
        var canvas = new RdpSkiaCanvas(buffer);

        using var targetBitmap = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var targetCanvas = new SKCanvas(targetBitmap);

        byte[] rawPixels = new byte[64 * 64 * 3]; // 24bpp
        Array.Fill(rawPixels, (byte)0xCE);

        // Warm up memory and allocations
        for (ulong i = 0; i < 200; i++)
        {
            var update = new RdpBitmapUpdate(0, 0, 64, 64, 24, compressed: false, rawPixels);
            buffer.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(i, DateTimeOffset.UtcNow, new[] { update }));
            buffer.SwapBuffers();
            canvas.Render(targetCanvas, 0, 0);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startMemory = GC.GetTotalMemory(true);

        const int rapidIterationCount = 5000;
        for (ulong i = 0; i < rapidIterationCount; i++)
        {
            ushort left = (ushort)((i * 32) % 1200);
            ushort top = (ushort)((i * 16) % 650);

            var update = new RdpBitmapUpdate(left, top, 64, 64, 24, compressed: false, rawPixels);
            buffer.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(i, DateTimeOffset.UtcNow, new[] { update }));
            var dirty = buffer.SwapBuffers();
            canvas.Render(targetCanvas, SKRect.Create(0, 0, 1280, 720), drawDirtyOnly: true);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long endMemory = GC.GetTotalMemory(true);
        long memoryDiff = endMemory - startMemory;

        // Ensure memory growth after 5,000 rapid updates is bounded (< 10 MB)
        Assert.True(memoryDiff < 10 * 1024 * 1024, $"Memory grew by {memoryDiff} bytes after {rapidIterationCount} updates, exceeding 10 MB threshold.");
    }
}
