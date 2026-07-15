namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public class HeadingLayoutBlock : ILayoutBlock
{
    public HeadingBlock Node { get; }
    MarkdownBlock ILayoutBlock.Node => Node;
    public SKRect Bounds { get; private set; }

    private readonly List<VisualLine> _lines = new();

    public HeadingLayoutBlock(HeadingBlock node)
    {
        Node = node;
    }

    public void Layout(LayoutContext context)
    {
        float startY = context.CurrentY;
        foreach (var line in _lines)
        {
            line.Dispose();
        }
        _lines.Clear();

        var baseStyle = new TextStyle { HeadingLevel = Node.Level, IsBold = true };
        TextLayoutEngine.LayoutInlines(Node.Children, context.MaxWidth, context.Measurer, context.Resources, baseStyle, _lines);

        float blockHeight = 0;
        foreach (var line in _lines)
        {
            blockHeight += line.Bounds.Height;
        }

        // Heading bottom margin space (12px)
        blockHeight += 12.0f;

        Bounds = new SKRect(0, startY, context.MaxWidth, startY + blockHeight);
        context.CurrentY = startY + blockHeight;
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        canvas.Save();
        canvas.Translate(Bounds.Left, Bounds.Top);

        foreach (var line in _lines)
        {
            float absoluteLineTop = Bounds.Top + line.Bounds.Top;
            float absoluteLineBottom = Bounds.Top + line.Bounds.Bottom;

            if (absoluteLineBottom >= context.Viewport.Top && absoluteLineTop <= context.Viewport.Bottom)
            {
                foreach (var run in line.Runs)
                {
                    float baseline = context.Resources.GetHeadingFont(Node.Level).Spacing * 0.75f;
                    if (run.TextBlob != null)
                    {
                        canvas.DrawText(run.TextBlob, run.LocalBounds.Left + line.Bounds.Left, run.LocalBounds.Top + line.Bounds.Top + baseline, run.Paint);
                    }
                    else
                    {
                        canvas.DrawText(run.Text, run.LocalBounds.Left + line.Bounds.Left, run.LocalBounds.Top + line.Bounds.Top + baseline, context.Resources.GetHeadingFont(Node.Level), run.Paint);
                    }

                    if (run.Style.IsUnderline || run.Style.LinkUrl != null)
                    {
                        float lineY = run.LocalBounds.Top + line.Bounds.Top + baseline + 2f;
                        canvas.DrawLine(
                            run.LocalBounds.Left + line.Bounds.Left,
                            lineY,
                            run.LocalBounds.Right + line.Bounds.Left,
                            lineY,
                            run.Paint
                        );
                    }

                    if (run.Style.IsStrikeThrough)
                    {
                        float lineY = run.LocalBounds.Top + line.Bounds.Top + baseline - (context.Resources.GetHeadingFont(Node.Level).Size * 0.3f);
                        canvas.DrawLine(
                            run.LocalBounds.Left + line.Bounds.Left,
                            lineY,
                            run.LocalBounds.Right + line.Bounds.Left,
                            lineY,
                            run.Paint
                        );
                    }
                }
            }
        }

        canvas.Restore();

        // Level 1 and 2 draw a solid horizontal border at the bottom
        if (Node.Level <= 2)
        {
            float borderY = Bounds.Bottom - 4.0f;
            canvas.DrawLine(Bounds.Left, borderY, Bounds.Right, borderY, context.Resources.HeadingBorderPaint);
        }
    }

    public int HitTest(SKPoint docPoint)
    {
        float localY = docPoint.Y - Bounds.Top;
        float localX = docPoint.X - Bounds.Left;

        if (_lines.Count == 0) return Node.Span.Start;

        VisualLine selectedLine = _lines[0];
        if (localY < 0)
        {
            selectedLine = _lines[0];
        }
        else if (localY >= _lines[^1].Bounds.Bottom)
        {
            selectedLine = _lines[^1];
        }
        else
        {
            foreach (var line in _lines)
            {
                if (localY >= line.Bounds.Top && localY < line.Bounds.Bottom)
                {
                    selectedLine = line;
                    break;
                }
            }
        }

        if (selectedLine.Runs.Count == 0) return selectedLine.StartOffset;

        float lineLocalX = localX - selectedLine.Bounds.Left;
        VisualTextRun selectedRun = selectedLine.Runs[0];
        if (lineLocalX < 0)
        {
            selectedRun = selectedLine.Runs[0];
        }
        else if (lineLocalX >= selectedLine.Runs[^1].LocalBounds.Right)
        {
            selectedRun = selectedLine.Runs[^1];
        }
        else
        {
            foreach (var run in selectedLine.Runs)
            {
                if (lineLocalX >= run.LocalBounds.Left && lineLocalX < run.LocalBounds.Right)
                {
                    selectedRun = run;
                    break;
                }
            }
        }

        float runLocalX = lineLocalX - selectedRun.LocalBounds.Left;
        int n = selectedRun.Text.Length;
        if (n == 0) return selectedRun.Span.Start;

        if (runLocalX <= 0)
        {
            return selectedRun.Span.Start;
        }
        if (runLocalX >= selectedRun.LocalBounds.Width)
        {
            return selectedRun.Span.Start + n;
        }

        for (int i = 0; i < n; i++)
        {
            float cStart = selectedRun.CharacterPositions[i];
            float cEnd = selectedRun.CharacterPositions[i + 1];
            if (runLocalX >= cStart && runLocalX < cEnd)
            {
                if (runLocalX - cStart < (cEnd - cStart) / 2f)
                {
                    return selectedRun.Span.Start + i;
                }
                else
                {
                    return selectedRun.Span.Start + i + 1;
                }
            }
        }

        return selectedRun.Span.Start + n;
    }

    public SKRect GetCaretBounds(int offset)
    {
        if (_lines.Count == 0)
        {
            return new SKRect(Bounds.Left, Bounds.Top, Bounds.Left + 2f, Bounds.Top + 20f);
        }

        foreach (var line in _lines)
        {
            foreach (var run in line.Runs)
            {
                if (offset < run.Span.Start)
                {
                    float xInRun = run.CharacterPositions[0];
                    float xDoc = Bounds.Left + line.Bounds.Left + run.LocalBounds.Left + xInRun;
                    float yTop = Bounds.Top + line.Bounds.Top;
                    float yBottom = Bounds.Top + line.Bounds.Bottom;
                    return new SKRect(xDoc, yTop, xDoc + 2f, yBottom);
                }
                else if (offset >= run.Span.Start && offset <= run.Span.Start + run.Span.Length)
                {
                    int localIdx = Math.Min(offset - run.Span.Start, run.CharacterPositions.Length - 1);
                    float xInRun = run.CharacterPositions[localIdx];
                    float xDoc = Bounds.Left + line.Bounds.Left + run.LocalBounds.Left + xInRun;
                    float yTop = Bounds.Top + line.Bounds.Top;
                    float yBottom = Bounds.Top + line.Bounds.Bottom;
                    return new SKRect(xDoc, yTop, xDoc + 2f, yBottom);
                }
            }
        }

        var lastLine = _lines[^1];
        float lastX = Bounds.Left + lastLine.Bounds.Right;
        return new SKRect(lastX, Bounds.Top + lastLine.Bounds.Top, lastX + 2f, Bounds.Top + lastLine.Bounds.Bottom);
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        int blockStart = Node.Span.Start;
        int blockEnd = Node.Span.Start + Node.Span.Length;
        if (Math.Max(start, blockStart) >= Math.Min(end, blockEnd))
        {
            return;
        }

        foreach (var line in _lines)
        {
            int lineStart = line.StartOffset;
            int lineEnd = line.EndOffset;
            
            if (Math.Max(start, lineStart) >= Math.Min(end, lineEnd))
            {
                continue;
            }

            int selStart = Math.Max(start, lineStart);
            int selEnd = Math.Min(end, lineEnd);
            
            float leftDoc = -1;
            float rightDoc = -1;
            float lineYTop = Bounds.Top + line.Bounds.Top;
            float lineYBottom = Bounds.Top + line.Bounds.Bottom;

            foreach (var run in line.Runs)
            {
                int runStart = run.Span.Start;
                int runEnd = run.Span.Start + run.Span.Length;

                if (Math.Max(selStart, runStart) >= Math.Min(selEnd, runEnd))
                {
                    continue;
                }

                int runSelStart = Math.Max(selStart, runStart);
                int runSelEnd = Math.Min(selEnd, runEnd);

                int idxStart = Math.Min(runSelStart - run.Span.Start, run.CharacterPositions.Length - 1);
                int idxEnd = Math.Min(runSelEnd - run.Span.Start, run.CharacterPositions.Length - 1);

                float x1 = run.CharacterPositions[idxStart];
                float x2 = run.CharacterPositions[idxEnd];

                float runLeftDoc = Bounds.Left + line.Bounds.Left + run.LocalBounds.Left + x1;
                float runRightDoc = Bounds.Left + line.Bounds.Left + run.LocalBounds.Left + x2;

                if (leftDoc < 0)
                {
                    leftDoc = runLeftDoc;
                    rightDoc = runRightDoc;
                }
                else
                {
                    rightDoc = runRightDoc;
                }
            }

            if (leftDoc >= 0)
            {
                result.Add(new SKRect(leftDoc, lineYTop, rightDoc, lineYBottom));
                
                if (end > lineEnd && line != _lines[^1])
                {
                    result.Add(new SKRect(rightDoc, lineYTop, rightDoc + 10f, lineYBottom));
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (var line in _lines)
        {
            line.Dispose();
        }
        _lines.Clear();
    }
}
