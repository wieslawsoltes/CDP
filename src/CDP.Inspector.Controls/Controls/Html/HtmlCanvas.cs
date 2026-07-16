using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using CDP.Html.Parser;
using CDP.Css.Parser;
using CDP.Html.Renderer;

namespace CdpInspectorApp.Controls;

public class HtmlCanvas : Control
{
    public static readonly StyledProperty<string> HtmlTextProperty =
        AvaloniaProperty.Register<HtmlCanvas, string>(nameof(HtmlText), string.Empty);

    public static readonly StyledProperty<string> CssTextProperty =
        AvaloniaProperty.Register<HtmlCanvas, string>(nameof(CssText), string.Empty);

    public string HtmlText
    {
        get => GetValue(HtmlTextProperty);
        set => SetValue(HtmlTextProperty, value);
    }

    public string CssText
    {
        get => GetValue(CssTextProperty);
        set => SetValue(CssTextProperty, value);
    }

    private SKBitmap? _cachedBitmap;
    private WriteableBitmap? _cachedWriteableBitmap;
    private string? _lastHtmlText;
    private string? _lastCssText;
    private HtmlDocument? _cachedDoc;
    private CssStyleSheet? _cachedStylesheet;

    static HtmlCanvas()
    {
        AffectsRender<HtmlCanvas>(HtmlTextProperty, CssTextProperty);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _cachedBitmap?.Dispose();
        _cachedBitmap = null;
        _cachedWriteableBitmap?.Dispose();
        _cachedWriteableBitmap = null;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // 1. Thread-safe cached parsing of inputs
        string html = HtmlText ?? string.Empty;
        string css = CssText ?? string.Empty;

        if (html != _lastHtmlText || _cachedDoc == null)
        {
            _cachedDoc = HtmlParser.Parse(html);
            _lastHtmlText = html;
        }

        if (css != _lastCssText || _cachedStylesheet == null)
        {
            _cachedStylesheet = CssParser.Parse(css);
            _lastCssText = css;
        }

        // 2. Resolve render scaling factors
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        float scale = (float)scaling;
        int pixelWidth = (int)Math.Max(1, Math.Round(bounds.Width * scaling));
        int pixelHeight = (int)Math.Max(1, Math.Round(bounds.Height * scaling));

        // 3. Allocate/resize memory buffers
        if (_cachedBitmap == null || _cachedBitmap.Width != pixelWidth || _cachedBitmap.Height != pixelHeight)
        {
            _cachedBitmap?.Dispose();
            _cachedBitmap = new SKBitmap(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
        }

        if (_cachedWriteableBitmap == null || _cachedWriteableBitmap.PixelSize.Width != pixelWidth || _cachedWriteableBitmap.PixelSize.Height != pixelHeight)
        {
            _cachedWriteableBitmap?.Dispose();
            _cachedWriteableBitmap = new WriteableBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(96 * scaling, 96 * scaling),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
        }

        // 4. Render to Skia Canvas
        using (var canvas = new SKCanvas(_cachedBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Save();
            canvas.Scale(scale);

            var renderBounds = new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height);
            HtmlRenderer.Render(canvas, _cachedDoc, _cachedStylesheet, renderBounds);

            canvas.Restore();
        }

        // 5. Blit pixels to WriteableBitmap (Trim-safe high-speed transfer)
        using (var locked = _cachedWriteableBitmap.Lock())
        {
            var srcPtr = _cachedBitmap.GetPixels();
            var dstPtr = locked.Address;
            var srcRowBytes = _cachedBitmap.RowBytes;
            var dstRowBytes = locked.RowBytes;
            var rowSize = Math.Min(srcRowBytes, dstRowBytes);

            int copyHeight = Math.Min(pixelHeight, _cachedWriteableBitmap.PixelSize.Height);
            unsafe
            {
                for (int y = 0; y < copyHeight; y++)
                {
                    Buffer.MemoryCopy(
                        (void*)(srcPtr + y * srcRowBytes),
                        (void*)(dstPtr + y * dstRowBytes),
                        rowSize,
                        rowSize);
                }
            }
        }

        // 6. Draw the image to Avalonia's DrawingContext
        double logicalWidth = (double)pixelWidth / scaling;
        double logicalHeight = (double)pixelHeight / scaling;
        context.DrawImage(_cachedWriteableBitmap, new Rect(0, 0, logicalWidth, logicalHeight));
    }
}
