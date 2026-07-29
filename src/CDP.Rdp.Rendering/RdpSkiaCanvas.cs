namespace CDP.Rdp.Rendering;

using System;
using System.Diagnostics;
using SkiaSharp;

/// <summary>
/// SkiaSharp rendering surface wrapper managing canvas draws, dirty region repaints, scaling, clipping, and FPS tracking.
/// </summary>
public sealed class RdpSkiaCanvas
{
    private readonly Stopwatch _fpsStopwatch = new();
    private long _frameCountInWindow;
    private long _totalRenderedFrames;
    private float _currentFps;
    private readonly object _fpsLock = new();

    public RdpFrameBuffer FrameBuffer { get; }

    /// <summary>
    /// Gets the current estimated frames per second (FPS).
    /// </summary>
    public float CurrentFps
    {
        get
        {
            lock (_fpsLock)
            {
                return _currentFps;
            }
        }
    }

    /// <summary>
    /// Gets the total number of rendered frames onto target canvas.
    /// </summary>
    public long RenderedFrameCount
    {
        get
        {
            lock (_fpsLock)
            {
                return _totalRenderedFrames;
            }
        }
    }

    public RdpSkiaCanvas(RdpFrameBuffer frameBuffer)
    {
        FrameBuffer = frameBuffer ?? throw new ArgumentNullException(nameof(frameBuffer));
        _fpsStopwatch.Start();
    }

    public RdpSkiaCanvas(int width, int height)
        : this(new RdpFrameBuffer(width, height))
    {
    }

    /// <summary>
    /// Renders the current frame buffer front bitmap onto the target canvas within specified bounds.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect targetBounds)
    {
        Render(canvas, targetBounds, drawDirtyOnly: false);
    }

    /// <summary>
    /// Renders the current frame buffer front bitmap onto the target canvas with scaling and optional dirty region clipping.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect targetBounds, bool drawDirtyOnly)
    {
        if (canvas == null) throw new ArgumentNullException(nameof(canvas));

        UpdateFps();

        lock (FrameBuffer.SyncRoot)
        {
            canvas.Save();
            try
            {
                canvas.ClipRect(targetBounds);

                if (drawDirtyOnly)
                {
                    var dirtyRegion = FrameBuffer.SwapBuffers();
                    if (!dirtyRegion.IsEmpty)
                    {
                        float scaleX = targetBounds.Width / FrameBuffer.Width;
                        float scaleY = targetBounds.Height / FrameBuffer.Height;

                        var dirtyRects = dirtyRegion.Rectangles;
                        for (int i = 0; i < dirtyRects.Count; i++)
                        {
                            var dirtyRect = dirtyRects[i];
                            SKRect srcRect = dirtyRect;
                            SKRect destRect = SKRect.Create(
                                targetBounds.Left + (dirtyRect.Left * scaleX),
                                targetBounds.Top + (dirtyRect.Top * scaleY),
                                dirtyRect.Width * scaleX,
                                dirtyRect.Height * scaleY);

                            FrameBuffer.RenderToCanvas(canvas, srcRect, destRect);
                        }
                    }
                }
                else
                {
                    FrameBuffer.SwapBuffers();
                    SKRect srcBounds = SKRect.Create(0, 0, FrameBuffer.Width, FrameBuffer.Height);
                    FrameBuffer.RenderToCanvas(canvas, srcBounds, targetBounds);
                }
            }
            finally
            {
                canvas.Restore();
            }
        }
    }

    /// <summary>
    /// Renders the frame buffer directly to canvas at offset (destX, destY) without scaling.
    /// </summary>
    public void Render(SKCanvas canvas, float destX = 0, float destY = 0)
    {
        if (canvas == null) throw new ArgumentNullException(nameof(canvas));

        UpdateFps();
        lock (FrameBuffer.SyncRoot)
        {
            FrameBuffer.RenderToCanvas(canvas, destX, destY);
        }
    }

    private void UpdateFps()
    {
        lock (_fpsLock)
        {
            _totalRenderedFrames++;
            _frameCountInWindow++;

            double elapsedSeconds = _fpsStopwatch.Elapsed.TotalSeconds;
            if (elapsedSeconds >= 1.0)
            {
                _currentFps = (float)(_frameCountInWindow / elapsedSeconds);
                _frameCountInWindow = 0;
                _fpsStopwatch.Restart();
            }
        }
    }
}
