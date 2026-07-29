using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Rendering;

using System;
using System.Diagnostics;
using SkiaSharp;
using CDP.Rdp.Frames;
using CDP.Rdp.Rendering;
using Xunit;

[Xunit.Collection("RdpTests")]
public class RdpRenderingPerformanceTests
{
    [AvaloniaFact]
    public void FramePipeline_LowAllocation_UnderHighUpdateLoad()
    {
        using var buffer = new RdpFrameBuffer(1920, 1080);

        // Pre-create 64x64 tile pixel payload (24 bpp)
        byte[] rawPixels = new byte[64 * 64 * 3];
        Array.Fill(rawPixels, (byte)0xAB);

        // Warm up JIT, pool initialization, and XUnit runner overhead
        for (ulong i = 0; i < 50; i++)
        {
            var update = new RdpBitmapUpdate(0, 0, 64, 64, 24, compressed: false, rawPixels);
            var eventArgs = new RdpFrameUpdateEventArgs(i, DateTimeOffset.UtcNow, new[] { update });
            buffer.ApplyFrameUpdate(eventArgs);
            buffer.SwapBuffers();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long initialMemory = GC.GetAllocatedBytesForCurrentThread();

        const int frameCount = 500;
        for (ulong i = 0; i < frameCount; i++)
        {
            ushort left = (ushort)((i * 64) % 1800);
            ushort top = (ushort)((i * 32) % 1000);

            var update = new RdpBitmapUpdate(left, top, 64, 64, 24, compressed: false, rawPixels);
            var eventArgs = new RdpFrameUpdateEventArgs(i, DateTimeOffset.UtcNow, new[] { update });

            buffer.ApplyFrameUpdate(eventArgs);
            buffer.SwapBuffers();
        }

        long finalMemory = GC.GetAllocatedBytesForCurrentThread();
        long totalAllocated = finalMemory - initialMemory;
        double averageAllocBytesPerFrame = (double)totalAllocated / frameCount;

        // Allocation per frame should be low (< 10 KB)
        Assert.True(averageAllocBytesPerFrame < 10240, $"Average allocation per frame too high: {averageAllocBytesPerFrame:F2} bytes/frame");
    }

    [AvaloniaFact]
    public void RenderingPipeline_60Fps_UnderHighUpdateLoad()
    {
        using var buffer = new RdpFrameBuffer(1280, 720);
        var canvas = new RdpSkiaCanvas(buffer);
        using var targetBitmap = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var targetCanvas = new SKCanvas(targetBitmap);

        byte[] tilePixels = new byte[64 * 64 * 4]; // 32 bpp
        Array.Fill(tilePixels, (byte)0x7F);

        Stopwatch sw = Stopwatch.StartNew();
        const int targetFrames = 60;

        for (ulong i = 0; i < targetFrames; i++)
        {
            ushort left = (ushort)((i * 16) % 1200);
            ushort top = (ushort)((i * 8) % 650);

            var update = new RdpBitmapUpdate(left, top, 64, 64, 32, compressed: false, tilePixels);
            buffer.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(i, DateTimeOffset.UtcNow, new[] { update }));
            buffer.SwapBuffers();

            canvas.Render(targetCanvas, 0, 0);
        }

        sw.Stop();

        Assert.Equal(targetFrames, canvas.RenderedFrameCount);
        // 60 frames should process easily under 2 seconds on modern CPUs
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Rendering 60 frames took {sw.ElapsedMilliseconds} ms, expected < 2000 ms");
    }
}
