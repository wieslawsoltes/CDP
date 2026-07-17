using System;
using SkiaSharp;
using CDP.Markdown.Renderer.Layout;
using CDP.Markdown.Renderer.Rendering;

namespace CDP.Rendering.Comparison.Tests
{
    public class SkiaTextMeasurer : ITextMeasurer
    {
        private readonly RenderResources _resources;

        public SkiaTextMeasurer(RenderResources resources)
        {
            _resources = resources;
        }

        public float MeasureText(string text, TextStyle style)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var font = TextLayoutEngine.ResolveFont(style, _resources);
            using var paint = new SKPaint { Typeface = font.Typeface, TextSize = font.Size };
            return paint.MeasureText(text);
        }

        public float[] GetCharacterWidths(string text, TextStyle style)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<float>();
            var font = TextLayoutEngine.ResolveFont(style, _resources);
            using var paint = new SKPaint { Typeface = font.Typeface, TextSize = font.Size };
            var widths = new float[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                widths[i] = paint.MeasureText(text[i].ToString());
            }
            return widths;
        }

        public float GetLineHeight(TextStyle style)
        {
            var font = TextLayoutEngine.ResolveFont(style, _resources);
            return font.Spacing;
        }
    }
}
