namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public class CodeLayoutBlock : ILayoutBlock
{
    public CodeBlock Node { get; }
    MarkdownBlock ILayoutBlock.Node => Node;
    public SKRect Bounds { get; private set; }

    private readonly List<VisualLine> _lines = new();
    
    private const float Padding = 8.0f;
    private const float MarginBottom = 10.0f;

    public CodeLayoutBlock(CodeBlock node)
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

        int codeStartOffset = Node.Span.Start;
        if (context.MarkdownText != null && !string.IsNullOrEmpty(Node.Code))
        {
            int index = context.MarkdownText.IndexOf(Node.Code, Node.Span.Start);
            if (index >= 0)
            {
                codeStartOffset = index;
            }
        }

        string[] rawLines = Node.Code.Split('\n');
        
        var monospaceStyle = new TextStyle { IsMonospace = true };
        float lineHeight = context.Measurer.GetLineHeight(monospaceStyle);
        if (lineHeight == 0) lineHeight = 20f;

        float currentY = Padding;
        int currentCodeOffset = codeStartOffset;

        for (int l = 0; l < rawLines.Length; l++)
        {
            string lineText = rawLines[l];
            // If the last line is empty (due to trailing \n), we still want to layout an empty line.
            if (l == rawLines.Length - 1 && string.IsNullOrEmpty(lineText))
            {
                break;
            }

            string cleanLineText = lineText.EndsWith('\r') ? lineText.Substring(0, lineText.Length - 1) : lineText;
            var tokens = TokenizeCodeLine(cleanLineText);
            
            var line = new VisualLine();
            float currentX = Padding;
            int runOffset = currentCodeOffset;

            foreach (var token in tokens)
            {
                SKPaint paint = context.Resources != null ? ResolveCodePaint(token.Type, context.Resources) : RenderResources.DefaultPaint;
                SKFont? font = context.Resources?.CodeFont;

                SKTextBlob? textBlob = null;
                if (token.Text.Length > 0 && font != null)
                {
                    textBlob = SKTextBlob.Create(token.Text, font);
                }

                var charWidths = context.Measurer.GetCharacterWidths(token.Text, monospaceStyle);
                var charPositions = new float[token.Text.Length + 1];
                charPositions[0] = 0;
                for (int i = 0; i < token.Text.Length; i++)
                {
                    charPositions[i + 1] = charPositions[i] + charWidths[i];
                }

                float runWidth = charPositions[token.Text.Length];
                var runSpan = new SourceSpan(runOffset, token.Text.Length);

                var run = new VisualTextRun(
                    token.Text,
                    textBlob,
                    new SKRect(currentX, 0, currentX + runWidth, lineHeight),
                    paint,
                    runSpan,
                    monospaceStyle,
                    charPositions
                );

                line.Runs.Add(run);
                currentX += runWidth;
                runOffset += token.Text.Length;
            }

            // If the line is empty, add a dummy empty run for caret positioning
            if (line.Runs.Count == 0)
            {
                var runSpan = new SourceSpan(currentCodeOffset, 0);
                var emptyRun = new VisualTextRun(
                    string.Empty,
                    null,
                    new SKRect(Padding, 0, Padding, lineHeight),
                    context.Resources?.CodePlainPaint ?? RenderResources.DefaultPaint,
                    runSpan,
                    monospaceStyle,
                    new float[] { 0 }
                );
                line.Runs.Add(emptyRun);
            }

            line.Bounds = new SKRect(0, currentY, context.MaxWidth, currentY + lineHeight);
            _lines.Add(line);
            currentY += lineHeight;

            currentCodeOffset += lineText.Length + 1; // plus 1 for '\n' split char
        }

        float blockHeight = currentY + Padding + MarginBottom;
        Bounds = new SKRect(0, startY, context.MaxWidth, startY + blockHeight);
        context.CurrentY = startY + blockHeight;
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        // Draw rounded background container
        var bgRect = new SKRect(Bounds.Left, Bounds.Top, Bounds.Right, Bounds.Bottom - MarginBottom);
        canvas.DrawRoundRect(bgRect, 4f, 4f, context.Resources.CodeBackgroundPaint);

        canvas.Save();
        canvas.Translate(Bounds.Left, Bounds.Top);

        foreach (var line in _lines)
        {
            float absoluteLineTop = Bounds.Top + line.Bounds.Top;
            float absoluteLineBottom = Bounds.Top + line.Bounds.Bottom;

            if (absoluteLineBottom >= context.Viewport.Top && absoluteLineTop <= context.Viewport.Bottom)
            {
                float baseline = context.Resources.CodeFont.Spacing * 0.75f;
                foreach (var run in line.Runs)
                {
                    if (run.Text.Length == 0) continue;
                    if (run.TextBlob != null)
                    {
                        canvas.DrawText(run.TextBlob, run.LocalBounds.Left, run.LocalBounds.Top + line.Bounds.Top + baseline, run.Paint);
                    }
                    else
                    {
                        canvas.DrawText(run.Text, run.LocalBounds.Left, run.LocalBounds.Top + line.Bounds.Top + baseline, context.Resources.CodeFont, run.Paint);
                    }
                }
            }
        }

        canvas.Restore();
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

        float lineLocalX = localX;
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
            return new SKRect(Bounds.Left + Padding, Bounds.Top + Padding, Bounds.Left + Padding + 2f, Bounds.Top + Padding + 20f);
        }

        foreach (var line in _lines)
        {
            foreach (var run in line.Runs)
            {
                if (offset < run.Span.Start)
                {
                    float xInRun = run.CharacterPositions[0];
                    float xDoc = Bounds.Left + run.LocalBounds.Left + xInRun;
                    float yTop = Bounds.Top + line.Bounds.Top;
                    float yBottom = Bounds.Top + line.Bounds.Bottom;
                    return new SKRect(xDoc, yTop, xDoc + 2f, yBottom);
                }
                else if (offset >= run.Span.Start && offset <= run.Span.Start + run.Span.Length)
                {
                    int localIdx = Math.Min(offset - run.Span.Start, run.CharacterPositions.Length - 1);
                    float xInRun = run.CharacterPositions[localIdx];
                    float xDoc = Bounds.Left + run.LocalBounds.Left + xInRun;
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

                float runLeftDoc = Bounds.Left + run.LocalBounds.Left + x1;
                float runRightDoc = Bounds.Left + run.LocalBounds.Left + x2;

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

    public enum CodeTokenType
    {
        PlainText,
        Keyword,
        Comment,
        String,
        Number
    }

    public class CodeToken
    {
        public string Text { get; }
        public CodeTokenType Type { get; }

        public CodeToken(string text, CodeTokenType type)
        {
            Text = text;
            Type = type;
        }
    }

    public static List<CodeToken> TokenizeCodeLine(string line)
    {
        var tokens = new List<CodeToken>();
        if (string.IsNullOrEmpty(line)) return tokens;

        int i = 0;
        while (i < line.Length)
        {
            if (char.IsWhiteSpace(line[i]))
            {
                int start = i;
                while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
                tokens.Add(new CodeToken(line.Substring(start, i - start), CodeTokenType.PlainText));
                continue;
            }

            if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/')
            {
                tokens.Add(new CodeToken(line.Substring(i), CodeTokenType.Comment));
                break;
            }

            if (line[i] == '"')
            {
                int start = i;
                i++;
                while (i < line.Length && line[i] != '"')
                {
                    if (line[i] == '\\' && i + 1 < line.Length) i++;
                    i++;
                }
                if (i < line.Length) i++;
                tokens.Add(new CodeToken(line.Substring(start, i - start), CodeTokenType.String));
                continue;
            }

            if (char.IsDigit(line[i]))
            {
                int start = i;
                while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.')) i++;
                tokens.Add(new CodeToken(line.Substring(start, i - start), CodeTokenType.Number));
                continue;
            }

            if (char.IsLetter(line[i]) || line[i] == '_')
            {
                int start = i;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                string word = line.Substring(start, i - start);
                var type = IsKeyword(word) ? CodeTokenType.Keyword : CodeTokenType.PlainText;
                tokens.Add(new CodeToken(word, type));
                continue;
            }

            tokens.Add(new CodeToken(line[i].ToString(), CodeTokenType.PlainText));
            i++;
        }

        return tokens;
    }

    private static readonly HashSet<string> Keywords = new()
    {
        "class", "struct", "interface", "public", "private", "protected", "internal",
        "void", "int", "float", "double", "string", "bool", "new", "return",
        "if", "else", "for", "foreach", "while", "using", "namespace", "var",
        "static", "readonly", "const", "import", "from",
        "def", "fn", "function", "let"
    };

    private static bool IsKeyword(string word)
    {
        return Keywords.Contains(word);
    }

    private static SKPaint ResolveCodePaint(CodeTokenType type, RenderResources resources)
    {
        return type switch
        {
            CodeTokenType.Keyword => resources.CodeKeywordPaint,
            CodeTokenType.Comment => resources.CodeCommentPaint,
            CodeTokenType.String => resources.CodeStringPaint,
            CodeTokenType.Number => resources.CodeNumberPaint,
            _ => resources.CodePlainPaint
        };
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
