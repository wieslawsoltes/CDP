using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Rendering;

using System;
using SkiaSharp;
using CDP.Rdp.Frames;
using CDP.Rdp.Rendering;
using Xunit;

public class RdpSkiaCanvasTests
{
    [AvaloniaFact]
    public void Canvas_Initialization_SetsFrameBuffer()
    {
        using var buffer = new RdpFrameBuffer(200, 150);
        var canvas = new RdpSkiaCanvas(buffer);

        Assert.Same(buffer, canvas.FrameBuffer);
        Assert.Equal(0, canvas.RenderedFrameCount);
    }

    [AvaloniaFact]
    public void Render_DirectOffset_DrawsToTargetCanvas()
    {
        using var buffer = new RdpFrameBuffer(50, 50);

        // Populate front buffer with red pixel via tile + swap
        byte[] pixels = new byte[] { 0x00, 0x00, 0xFF, 0xFF }; // Red in BGRA
        var update = new RdpBitmapUpdate(0, 0, 1, 1, 32, compressed: false, pixels);
        buffer.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, new[] { update }));
        buffer.SwapBuffers();

        var skiaCanvas = new RdpSkiaCanvas(buffer);

        using var targetBitmap = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Opaque);
        targetBitmap.Erase(SKColors.Black);
        using var targetCanvas = new SKCanvas(targetBitmap);

        skiaCanvas.Render(targetCanvas, 10, 10);

        Assert.Equal(1, skiaCanvas.RenderedFrameCount);

        SKColor targetPixel = targetBitmap.GetPixel(10, 10);
        Assert.Equal(0xFF, targetPixel.Red);
    }

    [AvaloniaFact]
    public void Render_ScaledTargetBounds_ScalesImage()
    {
        using var buffer = new RdpFrameBuffer(50, 50);

        // Fill top-left quarter with Green (B=0, G=255, R=0)
        byte[] pixels = new byte[25 * 25 * 4];
        for (int i = 0; i < 25 * 25; i++)
        {
            pixels[i * 4 + 0] = 0x00; // B
            pixels[i * 4 + 1] = 0xFF; // G
            pixels[i * 4 + 2] = 0x00; // R
            pixels[i * 4 + 3] = 0xFF; // A
        }
        var update = new RdpBitmapUpdate(0, 0, 25, 25, 32, compressed: false, pixels);
        buffer.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, new[] { update }));
        buffer.SwapBuffers();

        var skiaCanvas = new RdpSkiaCanvas(buffer);

        using var targetBitmap = new SKBitmap(200, 200, SKColorType.Bgra8888, SKAlphaType.Opaque);
        targetBitmap.Erase(SKColors.Black);
        using var targetCanvas = new SKCanvas(targetBitmap);

        skiaCanvas.Render(targetCanvas, SKRect.Create(0, 0, 200, 200));

        SKColor scaledPixel = targetBitmap.GetPixel(50, 50);
        Assert.Equal(0xFF, scaledPixel.Green);
    }

    [AvaloniaFact]
    public void Render_DirtyOnly_RepaintsOnlyDirtyRectangles()
    {
        using var buffer = new RdpFrameBuffer(100, 100);

        byte[] pixels = new byte[] { 0xFF, 0xFF, 0x00, 0xFF }; // Yellow (B=0xFF, G=0xFF, R=0x00)
        var update = new RdpBitmapUpdate(10, 10, 1, 1, 32, compressed: false, pixels);
        buffer.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, new[] { update }));

        var skiaCanvas = new RdpSkiaCanvas(buffer);

        using var targetBitmap = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Opaque);
        targetBitmap.Erase(SKColors.Black);
        using var targetCanvas = new SKCanvas(targetBitmap);

        skiaCanvas.Render(targetCanvas, SKRect.Create(0, 0, 100, 100), drawDirtyOnly: true);

        SKColor dirtyPixel = targetBitmap.GetPixel(10, 10);
        Assert.Equal(0xFF, dirtyPixel.Blue);
        Assert.Equal(0xFF, dirtyPixel.Green);
    }

    [AvaloniaFact]
    public void FpsTracking_IncrementsFrameCount()
    {
        using var buffer = new RdpFrameBuffer(10, 10);
        var skiaCanvas = new RdpSkiaCanvas(buffer);

        using var targetBitmap = new SKBitmap(10, 10, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var targetCanvas = new SKCanvas(targetBitmap);

        for (int i = 0; i < 10; i++)
        {
            skiaCanvas.Render(targetCanvas, 0, 0);
        }

        Assert.Equal(10, skiaCanvas.RenderedFrameCount);
        Assert.True(skiaCanvas.CurrentFps >= 0.0f);
    }
}
