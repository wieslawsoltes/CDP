namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public static class TextLayoutEngine
{
    public class LayoutToken
    {
        public string Text { get; }
        public TextStyle Style { get; }
        public SourceSpan Span { get; }
        public bool IsWhitespace { get; }
        public bool IsLineBreak { get; }
        public bool IsImage { get; }
        public ImageInline? ImageNode { get; }
        public bool IsHtml { get; }
        public string? HtmlText { get; }

        public LayoutToken(
            string text,
            TextStyle style,
            SourceSpan span,
            bool isWhitespace = false,
            bool isLineBreak = false,
            bool isImage = false,
            ImageInline? imageNode = null,
            bool isHtml = false,
            string? htmlText = null)
        {
            Text = text;
            Style = style;
            Span = span;
            IsWhitespace = isWhitespace;
            IsLineBreak = isLineBreak;
            IsImage = isImage;
            ImageNode = imageNode;
            IsHtml = isHtml;
            HtmlText = htmlText;
        }
    }

    public static void LayoutInlines(
        IEnumerable<MarkdownNode> inlines,
        float maxWidth,
        ITextMeasurer measurer,
        RenderResources? resources,
        TextStyle baseStyle,
        List<VisualLine> outLines)
    {
        // 1. Flatten inlines to tokens
        var tokens = FlattenInlines(inlines, baseStyle);
        
        // 2. Perform line wrapping
        var linesOfTokens = WrapTokens(tokens, maxWidth, measurer);
        
        // 3. Build VisualLines and VisualTextRuns
        float currentY = 0;
        foreach (var lineTokens in linesOfTokens)
        {
            if (lineTokens.Count == 0) continue;
            
            var line = new VisualLine();
            
            // Merge contiguous tokens of the same style
            var mergedRuns = MergeTokensToRuns(lineTokens, measurer, resources);
            
            // Calculate line height
            float maxLineHeight = 0;
            foreach (var run in mergedRuns)
            {
                float runHeight = run.IsImage || run.IsHtml ? run.LocalBounds.Height : measurer.GetLineHeight(run.Style);
                if (runHeight > maxLineHeight) maxLineHeight = runHeight;
            }
            if (maxLineHeight == 0) maxLineHeight = 20f;
            
            // Position runs horizontally
            float currentX = 0;
            foreach (var run in mergedRuns)
            {
                var positionedRun = new VisualTextRun(
                    run.Text,
                    run.TextBlob,
                    new SKRect(currentX, 0, currentX + run.LocalBounds.Width, maxLineHeight),
                    run.Paint,
                    run.Span,
                    run.Style,
                    run.CharacterPositions,
                    run.IsImage,
                    run.ImageUrl,
                    run.AltText,
                    run.IsHtml,
                    run.HtmlText
                );
                line.Runs.Add(positionedRun);
                currentX += run.LocalBounds.Width;
            }
            
            line.Bounds = new SKRect(0, currentY, maxWidth, currentY + maxLineHeight);
            outLines.Add(line);
            currentY += maxLineHeight;
        }
    }

    public static List<LayoutToken> FlattenInlines(IEnumerable<MarkdownNode> inlines, TextStyle parentStyle)
    {
        var tokens = new List<LayoutToken>();
        foreach (var node in inlines)
        {
            if (node is LiteralInline literal)
            {
                if (literal.IsHtml)
                {
                    tokens.Add(new LayoutToken(literal.Text ?? string.Empty, parentStyle, literal.Span, isHtml: true, htmlText: literal.Text));
                }
                else
                {
                    tokens.AddRange(TokenizeText(literal.Text, parentStyle, literal.Span));
                }
            }
            else if (node is CodeInline codeInline)
            {
                var style = parentStyle;
                style.IsMonospace = true;
                tokens.AddRange(TokenizeText(codeInline.Code, style, codeInline.Span));
            }
            else if (node is EmphasisInline emphasis)
            {
                var style = parentStyle;
                if (emphasis.IsStrong) style.IsBold = true;
                else style.IsItalic = true;
                tokens.AddRange(FlattenInlines(emphasis.Children, style));
            }
            else if (node is StrikeThroughInline strike)
            {
                var style = parentStyle;
                style.IsStrikeThrough = true;
                tokens.AddRange(FlattenInlines(strike.Children, style));
            }
            else if (node is LinkInline link)
            {
                var style = parentStyle;
                style.IsUnderline = true;
                style.LinkUrl = link.Url;
                tokens.AddRange(FlattenInlines(link.Children, style));
            }
            else if (node is ImageInline image)
            {
                tokens.Add(new LayoutToken(image.AltText ?? string.Empty, parentStyle, image.Span, isImage: true, imageNode: image));
            }
            else if (node is LineBreakInline lineBreak)
            {
                tokens.Add(new LayoutToken("\n", parentStyle, lineBreak.Span, isLineBreak: true));
            }
        }
        return tokens;
    }

    public static List<LayoutToken> TokenizeText(string text, TextStyle style, SourceSpan span)
    {
        var tokens = new List<LayoutToken>();
        if (string.IsNullOrEmpty(text)) return tokens;

        int i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                int start = i;
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }
                string ws = text.Substring(start, i - start);
                tokens.Add(new LayoutToken(ws, style, new SourceSpan(span.Start + start, ws.Length), isWhitespace: true));
            }
            else
            {
                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]))
                {
                    i++;
                }
                string word = text.Substring(start, i - start);
                tokens.Add(new LayoutToken(word, style, new SourceSpan(span.Start + start, word.Length)));
            }
        }
        return tokens;
    }

    private static List<List<LayoutToken>> WrapTokens(List<LayoutToken> tokens, float maxWidth, ITextMeasurer measurer)
    {
        var lines = new List<List<LayoutToken>>();
        var currentLine = new List<LayoutToken>();
        float currentX = 0;

        for (int t = 0; t < tokens.Count; t++)
        {
            var token = tokens[t];

            if (token.IsLineBreak)
            {
                lines.Add(currentLine);
                currentLine = new List<LayoutToken>();
                currentX = 0;
                continue;
            }

            float tokenWidth;
            if (token.IsImage)
            {
                tokenWidth = 150f;
            }
            else if (token.IsHtml)
            {
                tokenWidth = MeasureHtmlWidth(token.HtmlText);
            }
            else
            {
                tokenWidth = measurer.MeasureText(token.Text, token.Style);
            }

            if (currentX + tokenWidth <= maxWidth || currentLine.Count == 0)
            {
                if (token.IsWhitespace && currentLine.Count == 0)
                {
                    continue;
                }
                currentLine.Add(token);
                currentX += tokenWidth;
            }
            else
            {
                if (token.IsWhitespace)
                {
                    lines.Add(currentLine);
                    currentLine = new List<LayoutToken>();
                    currentX = 0;
                    continue;
                }

                if (tokenWidth > maxWidth)
                {
                    if (currentLine.Count > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = new List<LayoutToken>();
                        currentX = 0;
                    }

                    if (token.IsImage || token.IsHtml)
                    {
                        currentLine.Add(token);
                        lines.Add(currentLine);
                        currentLine = new List<LayoutToken>();
                        currentX = 0;
                    }
                    else
                    {
                        var widths = measurer.GetCharacterWidths(token.Text, token.Style);
                        int charIdx = 0;
                        while (charIdx < token.Text.Length)
                        {
                            float acc = 0;
                            int count = 0;
                            while (charIdx + count < token.Text.Length && acc + widths[charIdx + count] <= maxWidth)
                            {
                                acc += widths[charIdx + count];
                                count++;
                            }
                            if (count == 0)
                            {
                                count = 1;
                                acc = widths[charIdx];
                            }

                            string subWord = token.Text.Substring(charIdx, count);
                            var subSpan = new SourceSpan(token.Span.Start + charIdx, count);
                            var subToken = new LayoutToken(subWord, token.Style, subSpan);

                            currentLine.Add(subToken);
                            lines.Add(currentLine);
                            currentLine = new List<LayoutToken>();
                            currentX = 0;

                            charIdx += count;
                        }
                    }
                }
                else
                {
                    lines.Add(currentLine);
                    currentLine = new List<LayoutToken> { token };
                    currentX = tokenWidth;
                }
            }
        }

        if (currentLine.Count > 0)
        {
            lines.Add(currentLine);
        }

        if (lines.Count == 0)
        {
            lines.Add(new List<LayoutToken>());
        }

        return lines;
    }

    private static List<VisualTextRun> MergeTokensToRuns(List<LayoutToken> lineTokens, ITextMeasurer measurer, RenderResources? resources)
    {
        var runs = new List<VisualTextRun>();
        if (lineTokens.Count == 0) return runs;

        var currentTokens = new List<LayoutToken>();
        TextStyle currentStyle = lineTokens[0].Style;
        bool currentIsImage = lineTokens[0].IsImage;
        bool currentIsHtml = lineTokens[0].IsHtml;

        for (int i = 0; i < lineTokens.Count; i++)
        {
            var token = lineTokens[i];
            if (AreStylesEqual(token.Style, currentStyle) && 
                token.IsImage == currentIsImage && !token.IsImage &&
                token.IsHtml == currentIsHtml && !token.IsHtml)
            {
                currentTokens.Add(token);
            }
            else
            {
                if (currentTokens.Count > 0)
                {
                    runs.Add(CreateRunFromTokens(currentTokens, currentStyle, currentIsImage, currentIsHtml, measurer, resources));
                }
                currentTokens = new List<LayoutToken> { token };
                currentStyle = token.Style;
                currentIsImage = token.IsImage;
                currentIsHtml = token.IsHtml;
            }
        }

        if (currentTokens.Count > 0)
        {
            runs.Add(CreateRunFromTokens(currentTokens, currentStyle, currentIsImage, currentIsHtml, measurer, resources));
        }

        return runs;
    }

    private static bool AreStylesEqual(TextStyle a, TextStyle b)
    {
        return a.IsBold == b.IsBold &&
               a.IsItalic == b.IsItalic &&
               a.IsUnderline == b.IsUnderline &&
               a.IsStrikeThrough == b.IsStrikeThrough &&
               a.IsMonospace == b.IsMonospace &&
               a.LinkUrl == b.LinkUrl &&
               a.HeadingLevel == b.HeadingLevel &&
               a.ColorOverride == b.ColorOverride;
    }

    private static VisualTextRun CreateRunFromTokens(
        List<LayoutToken> tokens,
        TextStyle style,
        bool isImage,
        bool isHtml,
        ITextMeasurer measurer,
        RenderResources? resources)
    {
        var sb = new StringBuilder();
        int start = tokens[0].Span.Start;
        int end = tokens[^1].Span.Start + tokens[^1].Span.Length;
        foreach (var token in tokens)
        {
            sb.Append(token.Text);
        }
        string text = sb.ToString();
        var span = new SourceSpan(start, end - start);

        SKPaint paint = resources != null ? ResolvePaint(style, resources) : RenderResources.DefaultPaint;
        SKFont? font = resources != null ? ResolveFont(style, resources) : null;

        if (isImage)
        {
            var imageNode = tokens[0].ImageNode;
            float width = 150f;
            float height = 100f;
            
            int spanLength = span.Length;
            float[] imageCharPositions = new float[spanLength + 1];
            for (int i = 0; i <= spanLength; i++)
            {
                imageCharPositions[i] = spanLength > 0 ? (i * width / spanLength) : 0f;
            }

            return new VisualTextRun(
                text,
                null,
                new SKRect(0, 0, width, height),
                resources?.ImagePlaceholderBorderPaint ?? RenderResources.DefaultPaint,
                span,
                style,
                imageCharPositions,
                isImage: true,
                imageUrl: imageNode?.Url,
                altText: imageNode?.AltText
            );
        }

        if (isHtml)
        {
            float width = MeasureHtmlWidth(tokens[0].HtmlText);
            float height = MeasureHtmlHeight(tokens[0].HtmlText, width);

            int spanLength = span.Length;
            float[] htmlCharPositions = new float[spanLength + 1];
            for (int i = 0; i <= spanLength; i++)
            {
                htmlCharPositions[i] = spanLength > 0 ? (i * width / spanLength) : 0f;
            }

            return new VisualTextRun(
                text,
                null,
                new SKRect(0, 0, width, height),
                paint,
                span,
                style,
                htmlCharPositions,
                isImage: false,
                imageUrl: null,
                altText: null,
                isHtml: true,
                htmlText: tokens[0].HtmlText
            );
        }

        SKTextBlob? textBlob = null;
        if (text.Length > 0 && font != null)
        {
            textBlob = SKTextBlob.Create(text, font);
        }

        var charWidths = measurer.GetCharacterWidths(text, style);
        var charPositions = new float[text.Length + 1];
        charPositions[0] = 0;
        for (int i = 0; i < text.Length; i++)
        {
            charPositions[i + 1] = charPositions[i] + charWidths[i];
        }

        float runWidth = charPositions[text.Length];
        float runHeight = measurer.GetLineHeight(style);

        return new VisualTextRun(
            text,
            textBlob,
            new SKRect(0, 0, runWidth, runHeight),
            paint,
            span,
            style,
            charPositions
        );
    }

    private static float MeasureHtmlWidth(string? html)
    {
        if (string.IsNullOrEmpty(html)) return 0f;
        var doc = CDP.Html.Parser.HtmlParser.Parse(html);
        var stylesheet = CDP.Css.Parser.CssParser.Parse(string.Empty);
        var styles = CDP.Html.Renderer.Style.StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = CDP.Html.Renderer.Layout.LayoutTreeBuilder.Build(doc, styles);
        CDP.Html.Renderer.Layout.LayoutEngine.Layout(rootBox, float.PositiveInfinity, float.PositiveInfinity);
        return rootBox.Width;
    }

    private static float MeasureHtmlHeight(string? html, float width)
    {
        if (string.IsNullOrEmpty(html)) return 0f;
        var doc = CDP.Html.Parser.HtmlParser.Parse(html);
        var stylesheet = CDP.Css.Parser.CssParser.Parse(string.Empty);
        var styles = CDP.Html.Renderer.Style.StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = CDP.Html.Renderer.Layout.LayoutTreeBuilder.Build(doc, styles);
        CDP.Html.Renderer.Layout.LayoutEngine.Layout(rootBox, width, float.PositiveInfinity);
        return rootBox.Height;
    }

    public static SKFont ResolveFont(TextStyle style, RenderResources resources)
    {
        if (style.HeadingLevel > 0)
        {
            return resources.GetHeadingFont(style.HeadingLevel);
        }
        if (style.IsMonospace)
        {
            return resources.CodeFont;
        }
        if (style.IsBold && style.IsItalic)
        {
            return resources.TextBoldItalicFont;
        }
        if (style.IsBold)
        {
            return resources.TextBoldFont;
        }
        if (style.IsItalic)
        {
            return resources.TextItalicFont;
        }
        return resources.TextFont;
    }

    public static SKPaint ResolvePaint(TextStyle style, RenderResources resources)
    {
        if (style.LinkUrl != null)
        {
            return resources.LinkTextPaint;
        }
        if (style.IsMonospace)
        {
            return resources.CodeTextPaint;
        }
        return resources.TextPaint;
    }
}
