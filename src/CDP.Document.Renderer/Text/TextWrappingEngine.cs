using System;
using System.Collections.Generic;
using SkiaSharp;

namespace CDP.Document.Renderer.Text;

/// <summary>
/// Result of wrapping a single line of text within a given width.
/// </summary>
public class WrappedLine
{
    public int StartIndex { get; set; }
    public int Length { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}

/// <summary>
/// Simple word-wrapping engine that breaks text into lines that fit within a specified width.
/// </summary>
public static class TextWrappingEngine
{
    /// <summary>
    /// Wraps text into lines constrained to the given width using the provided paint for measurement.
    /// </summary>
    public static List<WrappedLine> WrapText(string text, float maxWidth, SKPaint paint)
    {
        var result = new List<WrappedLine>();
        if (string.IsNullOrEmpty(text))
        {
            result.Add(new WrappedLine { StartIndex = 0, Length = 0, Width = 0, Height = paint.TextSize * 1.2f });
            return result;
        }

        float lineHeight = paint.TextSize * 1.2f;
        int start = 0;

        while (start < text.Length)
        {
            // Handle explicit line breaks
            int newlineIdx = text.IndexOf('\n', start);
            string lineCandidate;
            int lineEnd;

            if (newlineIdx >= 0 && newlineIdx < text.Length)
            {
                lineCandidate = text.Substring(start, newlineIdx - start);
                lineEnd = newlineIdx + 1; // skip the newline
            }
            else
            {
                lineCandidate = text.Substring(start);
                lineEnd = text.Length;
            }

            float candidateWidth = paint.MeasureText(lineCandidate);
            if (candidateWidth <= maxWidth || lineCandidate.Length <= 1)
            {
                result.Add(new WrappedLine
                {
                    StartIndex = start,
                    Length = lineCandidate.Length,
                    Width = candidateWidth,
                    Height = lineHeight
                });
                start = lineEnd;
            }
            else
            {
                // Word wrap: find the last space that fits
                int lastSpace = -1;
                float measuredWidth = 0;

                for (int i = 0; i < lineCandidate.Length; i++)
                {
                    float charW = paint.MeasureText(lineCandidate.Substring(0, i + 1));
                    if (charW > maxWidth && i > 0)
                    {
                        break;
                    }
                    measuredWidth = charW;
                    if (lineCandidate[i] == ' ')
                    {
                        lastSpace = i;
                    }
                }

                int breakAt;
                if (lastSpace > 0)
                {
                    breakAt = lastSpace + 1; // break after the space
                }
                else
                {
                    // No space found, break at the character that exceeds width
                    breakAt = 1;
                    for (int i = 1; i < lineCandidate.Length; i++)
                    {
                        float w = paint.MeasureText(lineCandidate.Substring(0, i + 1));
                        if (w > maxWidth)
                        {
                            breakAt = i;
                            break;
                        }
                        breakAt = i + 1;
                    }
                }

                string wrappedLine = lineCandidate.Substring(0, breakAt);
                result.Add(new WrappedLine
                {
                    StartIndex = start,
                    Length = wrappedLine.Length,
                    Width = paint.MeasureText(wrappedLine),
                    Height = lineHeight
                });
                start += breakAt;
            }
        }

        if (result.Count == 0)
        {
            result.Add(new WrappedLine { StartIndex = 0, Length = 0, Width = 0, Height = lineHeight });
        }

        return result;
    }
}
