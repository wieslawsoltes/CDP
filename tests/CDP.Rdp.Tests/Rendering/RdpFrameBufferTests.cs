namespace CDP.Rdp.Tests.Rendering;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Rdp.Frames;
using CDP.Rdp.Rendering;
using Xunit;

public class RdpFrameBufferTests
{
    [Fact]
    public void Constructor_ValidDimensions_InitializesBuffers()
    {
        using var buffer = new RdpFrameBuffer(800, 600);

        Assert.Equal(800, buffer.Width);
        Assert.Equal(600, buffer.Height);
        Assert.NotNull(buffer.BackBuffer);
        Assert.NotNull(buffer.FrontBuffer);
        Assert.Empty(buffer.DirtyRegions);
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(800, 0)]
    [InlineData(-100, 600)]
    public void Constructor_InvalidDimensions_ThrowsException(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RdpFrameBuffer(width, height));
    }

    [Fact]
    public void ApplyTile_32Bpp_UpdatesBackBufferPixels()
    {
        using var buffer = new RdpFrameBuffer(100, 100);

        // 32 bpp BGRA pixel: Red (B=0, G=0, R=255, A=255)
        byte[] rawPixels = new byte[4 * 4]; // 2x2 tile
        for (int i = 0; i < 4; i++)
        {
            rawPixels[i * 4 + 0] = 0x00; // B
            rawPixels[i * 4 + 1] = 0x00; // G
            rawPixels[i * 4 + 2] = 0xFF; // R
            rawPixels[i * 4 + 3] = 0xFF; // A
        }

        var update = new RdpBitmapUpdate(10, 10, 2, 2, 32, compressed: false, rawPixels);
        using var tile = RdpBitmapTile.FromUpdate(update);

        buffer.ApplyTile(tile);

        SKColor backPixel = buffer.BackBuffer.GetPixel(10, 10);
        Assert.Equal(0xFF, backPixel.Red);
        Assert.Equal(0x00, backPixel.Green);
        Assert.Equal(0x00, backPixel.Blue);

        // Front buffer should not be updated until swap
        SKColor frontPixel = buffer.FrontBuffer.GetPixel(10, 10);
        Assert.Equal(SKColors.Black, frontPixel);
    }

    [Fact]
    public void ApplyTile_24Bpp_ConvertsPixelsCorrectly()
    {
        using var buffer = new RdpFrameBuffer(100, 100);

        // 24 bpp BGR pixel: Green (B=0, G=255, R=0)
        byte[] rawPixels = new byte[2 * 2 * 3];
        for (int i = 0; i < 4; i++)
        {
            rawPixels[i * 3 + 0] = 0x00; // B
            rawPixels[i * 3 + 1] = 0xFF; // G
            rawPixels[i * 3 + 2] = 0x00; // R
        }

        var update = new RdpBitmapUpdate(0, 0, 2, 2, 24, compressed: false, rawPixels);
        using var tile = RdpBitmapTile.FromUpdate(update);

        buffer.ApplyTile(tile);

        SKColor pixel = buffer.BackBuffer.GetPixel(0, 0);
        Assert.Equal(0x00, pixel.Red);
        Assert.Equal(0xFF, pixel.Green);
        Assert.Equal(0x00, pixel.Blue);
    }

    [Fact]
    public void ApplyTile_16BppRGB565_ConvertsPixelsCorrectly()
    {
        using var buffer = new RdpFrameBuffer(100, 100);

        // RGB565 Pure Blue: B=31 (0x1F), G=0, R=0 -> 0x001F (little endian: 0x1F, 0x00)
        byte[] rawPixels = new byte[] { 0x1F, 0x00 };

        var update = new RdpBitmapUpdate(5, 5, 1, 1, 16, compressed: false, rawPixels);
        using var tile = RdpBitmapTile.FromUpdate(update);

        buffer.ApplyTile(tile);

        SKColor pixel = buffer.BackBuffer.GetPixel(5, 5);
        Assert.True(pixel.Blue > 240);
        Assert.Equal(0x00, pixel.Red);
        Assert.Equal(0x00, pixel.Green);
    }

    [Fact]
    public void ApplyTile_15BppRGB555_ConvertsPixelsCorrectly()
    {
        using var buffer = new RdpFrameBuffer(100, 100);

        // RGB555 Pure Red: R=31 (0x1F) at bits 10-14 -> 0x7C00 (little endian: 0x00, 0x7C)
        byte[] rawPixels = new byte[] { 0x00, 0x7C };

        var update = new RdpBitmapUpdate(0, 0, 1, 1, 15, compressed: false, rawPixels);
        using var tile = RdpBitmapTile.FromUpdate(update);

        buffer.ApplyTile(tile);

        SKColor pixel = buffer.BackBuffer.GetPixel(0, 0);
        Assert.True(pixel.Red > 240);
        Assert.Equal(0x00, pixel.Green);
        Assert.Equal(0x00, pixel.Blue);
    }

    [Fact]
    public void ApplyTile_RleCompressed_DecompressesAndBlits()
    {
        using var buffer = new RdpFrameBuffer(100, 100);

        // RLE compressed: run of 4 24bpp pixels (B=255, G=0, R=0)
        // Control byte: 0x80 | 4 = 0x84, followed by 3 bytes (B=0xFF, G=0x00, R=0x00)
        byte[] compressedData = new byte[] { 0x84, 0xFF, 0x00, 0x00 };

        var update = new RdpBitmapUpdate(20, 20, 2, 2, 24, compressed: true, compressedData);
        using var tile = RdpBitmapTile.FromUpdate(update);

        buffer.ApplyTile(tile);

        SKColor pixel = buffer.BackBuffer.GetPixel(20, 20);
        Assert.Equal(0xFF, pixel.Blue);
    }

    [Fact]
    public void ApplyTile_OutOfBounds_ClipsCorrectly()
    {
        using var buffer = new RdpFrameBuffer(50, 50);

        // Tile extending beyond right and bottom edges (40, 40, size 20x20 -> max 60,60)
        byte[] pixels = new byte[20 * 20 * 4];
        Array.Fill(pixels, (byte)0xFF);

        var update = new RdpBitmapUpdate(40, 40, 20, 20, 32, compressed: false, pixels);
        using var tile = RdpBitmapTile.FromUpdate(update);

        buffer.ApplyTile(tile);

        Assert.Equal(0xFF, buffer.BackBuffer.GetPixel(49, 49).Red);
    }

    [Fact]
    public void SwapBuffers_TransfersDirtyRegionsAndPixelsToFrontBuffer()
    {
        using var buffer = new RdpFrameBuffer(100, 100);

        byte[] pixels = new byte[] { 0xFF, 0x00, 0x00, 0xFF }; // Blue
        var update = new RdpBitmapUpdate(10, 10, 1, 1, 32, compressed: false, pixels);

        var eventArgs = new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, new[] { update });
        buffer.ApplyFrameUpdate(eventArgs);

        var dirtyRegion = buffer.SwapBuffers();

        Assert.False(dirtyRegion.IsEmpty);
        Assert.Single(dirtyRegion.Rectangles);
        Assert.Equal(10, dirtyRegion.Rectangles[0].Left);
        Assert.Equal(10, dirtyRegion.Rectangles[0].Top);

        SKColor frontPixel = buffer.FrontBuffer.GetPixel(10, 10);
        Assert.Equal(0xFF, frontPixel.Blue);
        Assert.Equal(1u, buffer.CurrentFrameId);
    }

    [Fact]
    public void DirtyRegion_MergesOverlappingAndAdjacentRectangles()
    {
        var dirty = new RdpDirtyRegion();

        dirty.AddRect(0, 0, 10, 10);
        dirty.AddRect(5, 5, 10, 10); // Overlapping

        Assert.Single(dirty.Rectangles);
        Assert.Equal(SKRectI.Create(0, 0, 15, 15), dirty.Rectangles[0]);

        dirty.AddRect(15, 0, 10, 15); // Adjacent right edge
        Assert.Single(dirty.Rectangles);
        Assert.Equal(SKRectI.Create(0, 0, 25, 15), dirty.Rectangles[0]);
    }

    [Fact]
    public void DirtyRegion_ExceedingThreshold_UnionsAllRectangles()
    {
        var dirty = new RdpDirtyRegion();

        for (int i = 0; i < 20; i++)
        {
            dirty.AddRect(i * 10, i * 10, 5, 5);
        }

        Assert.True(dirty.Rectangles.Count <= RdpDirtyRegion.MaxRectanglesBeforeUnion);
        Assert.Equal(SKRectI.Create(0, 0, 195, 195), dirty.TotalBounds);
    }

    [Fact]
    public void Resize_UpdatesDimensionsAndResetsBuffers()
    {
        using var buffer = new RdpFrameBuffer(100, 100);
        buffer.Resize(200, 300);

        Assert.Equal(200, buffer.Width);
        Assert.Equal(300, buffer.Height);
        Assert.Equal(200, buffer.BackBuffer.Width);
        Assert.Equal(300, buffer.BackBuffer.Height);
        Assert.Empty(buffer.DirtyRegions);
    }

    [Fact]
    public void GetFrontBufferSnapshot_ReturnsDeepCopy()
    {
        using var buffer = new RdpFrameBuffer(50, 50);
        using var snapshot = buffer.GetFrontBufferSnapshot();

        Assert.NotNull(snapshot);
        Assert.Equal(50, snapshot.Width);
        Assert.Equal(50, snapshot.Height);
    }

    [Fact]
    public void RenderToCanvas_WithPaintAndOffset_RendersCorrectly()
    {
        using var buffer = new RdpFrameBuffer(50, 50);

        byte[] pixels = new byte[] { 0x00, 0x00, 0xFF, 0xFF }; // Red (BGRA)
        var update = new RdpBitmapUpdate(0, 0, 1, 1, 32, compressed: false, pixels);
        buffer.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, new[] { update }));
        buffer.SwapBuffers();

        using var targetBitmap = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Opaque);
        targetBitmap.Erase(SKColors.Black);
        using var targetCanvas = new SKCanvas(targetBitmap);
        using var paint = new SKPaint();

        buffer.RenderToCanvas(targetCanvas, 10, 10, paint);

        SKColor targetPixel = targetBitmap.GetPixel(10, 10);
        Assert.Equal(0xFF, targetPixel.Red);
    }

    [Fact]
    public void RenderToCanvas_WithSrcAndDestRects_RendersCorrectly()
    {
        using var buffer = new RdpFrameBuffer(50, 50);

        byte[] pixels = new byte[] { 0x00, 0xFF, 0x00, 0xFF }; // Green (BGRA)
        var update = new RdpBitmapUpdate(0, 0, 1, 1, 32, compressed: false, pixels);
        buffer.ApplyFrameUpdate(new RdpFrameUpdateEventArgs(1, DateTimeOffset.UtcNow, new[] { update }));
        buffer.SwapBuffers();

        using var targetBitmap = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Opaque);
        targetBitmap.Erase(SKColors.Black);
        using var targetCanvas = new SKCanvas(targetBitmap);

        SKRect srcRect = SKRect.Create(0, 0, 1, 1);
        SKRect destRect = SKRect.Create(20, 20, 10, 10);
        buffer.RenderToCanvas(targetCanvas, srcRect, destRect);

        SKColor targetPixel = targetBitmap.GetPixel(20, 20);
        Assert.Equal(0xFF, targetPixel.Green);
    }
}
