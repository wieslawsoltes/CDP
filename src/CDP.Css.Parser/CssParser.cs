using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace CDP.Css.Parser;

public static class CssParser
{
    private struct SelectorPart
    {
        public int RightStart;
        public int RightLength;
        public char Combinator; // '\0' for none
        public int TextLength;
    }

    private static readonly string[] CommonCssStrings = new[]
    {
        "div", "span", "p", "a", "li", "ul", "ol", "h1", "h2", "h3", "body", "html", "head",
        "title", "meta", "link", "script", "style", "img", "button", "input", "label", "form",
        "table", "tr", "td", "th", "thead", "tbody", "section", "nav", "header", "footer",
        "aside", "main", "canvas", "svg", "hover", "active", "focus", "visited", "link",
        "before", "after", "first-child", "last-child", "nth-child", "only-child", "enabled",
        "disabled", "checked", "required", "valid", "invalid", "root", "empty", "not"
    };

    [ThreadStatic]
    private static Dictionary<string, string>? _threadPool;

    private static Dictionary<string, string> GetThreadPool()
    {
        if (_threadPool == null)
        {
            _threadPool = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in CommonCssStrings)
            {
                _threadPool[s] = s;
            }
        }
        return _threadPool;
    }

    private static string GetPooledString(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return string.Empty;
        }
        var pool = GetThreadPool();
        var lookup = pool.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.TryGetValue(span, out var pooled))
        {
            return pooled;
        }
        string newStr = new string(span);
        if (pool.Count < 1024)
        {
            pool[newStr] = newStr;
        }
        return newStr;
    }

    [ThreadStatic]
    private static StringBuilder? _normalizationBuilder;

    private static StringBuilder GetNormalizationBuilder()
    {
        if (_normalizationBuilder == null)
        {
            _normalizationBuilder = new StringBuilder(512);
        }
        else
        {
            _normalizationBuilder.Clear();
        }
        return _normalizationBuilder;
    }

    public static CssStyleSheet Parse(string cssSource, string? parentMediaCondition = null)
    {
        if (cssSource == null)
            throw new ArgumentNullException(nameof(cssSource));

        cssSource = StripComments(cssSource);

        var stylesheet = new CssStyleSheet();
        ReadOnlySpan<char> cssSpan = cssSource.AsSpan();
        int pos = 0;
        int len = cssSpan.Length;

        while (pos < len)
        {
            // Skip whitespace
            while (pos < len && char.IsWhiteSpace(cssSpan[pos]))
            {
                pos++;
            }
            if (pos >= len)
                break;

            // Check for at-rule
            if (cssSpan[pos] == '@')
            {
                // Find next '{' or ';' to see if it's block-based or statement-based
                int idx = pos;
                int firstBrace = -1;
                int firstSemicolon = -1;
                while (idx < len)
                {
                    if (cssSpan[idx] == '{')
                    {
                        firstBrace = idx;
                        break;
                    }
                    if (cssSpan[idx] == ';')
                    {
                        firstSemicolon = idx;
                        break;
                    }
                    idx++;
                }

                if (firstBrace != -1 && (firstSemicolon == -1 || firstBrace < firstSemicolon))
                {
                    // Block-based at-rule, like @media
                    ReadOnlySpan<char> atRuleHeaderSpan = cssSpan.Slice(pos, firstBrace - pos).Trim();
                    bool isMedia = atRuleHeaderSpan.StartsWith("@media", StringComparison.OrdinalIgnoreCase);

                    // Find matching closing brace
                    int atBraceCount = 1;
                    int searchPos = firstBrace + 1;
                    bool inDoubleQuotes = false;
                    bool inSingleQuotes = false;
                    while (searchPos < len && atBraceCount > 0)
                    {
                        char c = cssSpan[searchPos];
                        if (c == '\\')
                        {
                            if (searchPos + 1 < len)
                            {
                                searchPos += 2;
                            }
                            else
                            {
                                searchPos++;
                            }
                            continue;
                        }
                        if (c == '"' && !inSingleQuotes)
                        {
                            inDoubleQuotes = !inDoubleQuotes;
                        }
                        else if (c == '\'' && !inDoubleQuotes)
                        {
                            inSingleQuotes = !inSingleQuotes;
                        }
                        else if (!inDoubleQuotes && !inSingleQuotes)
                        {
                            if (c == '{')
                            {
                                atBraceCount++;
                            }
                            else if (c == '}')
                            {
                                atBraceCount--;
                            }
                        }
                        searchPos++;
                    }

                    if (isMedia)
                    {
                        // Extract inner CSS and parse recursively
                        int innerStart = firstBrace + 1;
                        int innerLen = searchPos - 1 - innerStart;
                        if (innerLen > 0)
                        {
                            string innerCss = new string(cssSpan.Slice(innerStart, innerLen));
                            string mediaCondition = atRuleHeaderSpan.Slice(6).Trim().ToString(); // Omit "@media"
                            string combinedCondition = string.IsNullOrEmpty(parentMediaCondition)
                                ? mediaCondition
                                : $"{parentMediaCondition} and {mediaCondition}";
                            var subSheet = Parse(innerCss, combinedCondition);
                            stylesheet.Rules.AddRange(subSheet.Rules);
                        }
                    }

                    pos = searchPos; // Skip the entire at-rule block
                }
                else if (firstSemicolon != -1)
                {
                    // Statement-based at-rule, like @import. Skip it.
                    pos = firstSemicolon + 1;
                }
                else
                {
                    // Malformed at-rule, skip to end
                    break;
                }
                continue;
            }

            // Read selectors up to '{'
            int startSelectors = pos;
            int endSelectors = cssSpan.Slice(pos).IndexOf('{');
            if (endSelectors == -1)
            {
                break;
            }
            endSelectors += pos; // adjust index

            ReadOnlySpan<char> selectorsSpan = cssSpan.Slice(startSelectors, endSelectors - startSelectors);
            pos = endSelectors + 1; // skip '{'

            // Parse declarations up to '}'
            int startDecl = pos;
            int braceCount = 1;
            bool declInDoubleQuotes = false;
            bool declInSingleQuotes = false;
            while (pos < len && braceCount > 0)
            {
                char c = cssSpan[pos];
                if (c == '\\')
                {
                    if (pos + 1 < len)
                    {
                        pos += 2;
                    }
                    else
                    {
                        pos++;
                    }
                    continue;
                }
                if (c == '"' && !declInSingleQuotes)
                {
                    declInDoubleQuotes = !declInDoubleQuotes;
                }
                else if (c == '\'' && !declInDoubleQuotes)
                {
                    declInSingleQuotes = !declInSingleQuotes;
                }
                else if (!declInDoubleQuotes && !declInSingleQuotes)
                {
                    if (c == '{')
                    {
                        braceCount++;
                    }
                    else if (c == '}')
                    {
                        braceCount--;
                    }
                }
                pos++;
            }

            if (pos - startDecl - 1 < 0)
                break;

            ReadOnlySpan<char> declSpan = cssSpan.Slice(startDecl, pos - startDecl - 1); // exclude closing '}'

            var rule = new CssRule { MediaCondition = parentMediaCondition };

            // Parse selectors
            ParseSelectorsList(selectorsSpan, rule.Selectors);

            // Parse declarations
            ParseDeclarations(declSpan, rule.Declarations);

            if (rule.Selectors.Count > 0 || rule.Declarations.Count > 0)
            {
                stylesheet.Rules.Add(rule);
            }
        }

        return stylesheet;
    }

    private static string StripComments(string src)
    {
        int firstComment = src.IndexOf("/*", StringComparison.Ordinal);
        if (firstComment < 0)
        {
            return src;
        }

        var sb = new StringBuilder(src.Length);
        int lastIndex = 0;
        int i = firstComment;
        int len = src.Length;
        while (i < len)
        {
            if (i + 2 <= len && src.AsSpan(i, 2).Equals("/*", StringComparison.Ordinal))
            {
                // Append everything up to the comment start
                if (i > lastIndex)
                {
                    sb.Append(src.AsSpan(lastIndex, i - lastIndex));
                }
                i += 2;
                int end = src.IndexOf("*/", i, StringComparison.Ordinal);
                if (end == -1)
                {
                    lastIndex = len;
                    break;
                }
                i = end + 2;
                lastIndex = i;
            }
            else
            {
                i++;
            }
        }
        if (lastIndex < len)
        {
            sb.Append(src.AsSpan(lastIndex, len - lastIndex));
        }
        return sb.ToString();
    }

    private static string NormalizeSelectorText(ReadOnlySpan<char> selectorText)
    {
        selectorText = selectorText.Trim();
        var sb = GetNormalizationBuilder();
        int parenDepth = 0;
        for (int i = 0; i < selectorText.Length; i++)
        {
            char c = selectorText[i];
            if (c == '(')
            {
                parenDepth++;
                sb.Append(c);
            }
            else if (c == ')')
            {
                if (parenDepth > 0) parenDepth--;
                sb.Append(c);
            }
            else if (parenDepth == 0 && (c == '>' || c == '+' || c == '~'))
            {
                while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1]))
                {
                    sb.Length--;
                }
                sb.Append(c);
                while (i + 1 < selectorText.Length && char.IsWhiteSpace(selectorText[i + 1]))
                {
                    i++;
                }
            }
            else if (parenDepth == 0 && char.IsWhiteSpace(c))
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != ' ' && sb[sb.Length - 1] != '>' && sb[sb.Length - 1] != '+' && sb[sb.Length - 1] != '~')
                {
                    sb.Append(' ');
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        
        // Trim the result
        while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1]))
        {
            sb.Length--;
        }
        
        int startOffset = 0;
        while (startOffset < sb.Length && char.IsWhiteSpace(sb[startOffset]))
        {
            startOffset++;
        }
        
        return sb.ToString(startOffset, sb.Length - startOffset);
    }

    private static string CollapseWhitespace(ReadOnlySpan<char> val)
    {
        bool hasWhitespace = false;
        for (int i = 0; i < val.Length; i++)
        {
            if (char.IsWhiteSpace(val[i]))
            {
                hasWhitespace = true;
                break;
            }
        }
        if (!hasWhitespace)
        {
            return new string(val);
        }

        var sb = GetNormalizationBuilder();
        bool lastWasSpace = false;
        for (int i = 0; i < val.Length; i++)
        {
            if (char.IsWhiteSpace(val[i]))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(val[i]);
                lastWasSpace = false;
            }
        }
        
        while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1]))
        {
            sb.Length--;
        }
        
        int startOffset = 0;
        while (startOffset < sb.Length && char.IsWhiteSpace(sb[startOffset]))
        {
            startOffset++;
        }
        
        return sb.ToString(startOffset, sb.Length - startOffset);
    }

    private static void ParseSelectorsList(ReadOnlySpan<char> selectorsSpan, List<CssSelector> selectorsList)
    {
        int pos = 0;
        int len = selectorsSpan.Length;
        while (pos < len)
        {
            int commaIdx = selectorsSpan.Slice(pos).IndexOf(',');
            ReadOnlySpan<char> part;
            if (commaIdx == -1)
            {
                part = selectorsSpan.Slice(pos).Trim();
                pos = len;
            }
            else
            {
                part = selectorsSpan.Slice(pos, commaIdx).Trim();
                pos += commaIdx + 1;
            }

            if (!part.IsEmpty)
            {
                selectorsList.Add(ParseSelector(part));
            }
        }
    }

    private static void ParseDeclarations(ReadOnlySpan<char> declSpan, Dictionary<string, string> declarations)
    {
        int pos = 0;
        int len = declSpan.Length;

        while (pos < len)
        {
            // Skip whitespace
            while (pos < len && char.IsWhiteSpace(declSpan[pos]))
            {
                pos++;
            }
            if (pos >= len)
                break;

            // Read up to ';' or end, skipping string literals
            int startLine = pos;
            while (pos < len)
            {
                char c = declSpan[pos];
                if (c == ';')
                {
                    break;
                }
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    pos++;
                    while (pos < len)
                    {
                        if (declSpan[pos] == '\\')
                        {
                            if (pos + 1 < len)
                            {
                                pos += 2;
                            }
                            else
                            {
                                pos++;
                            }
                        }
                        else if (declSpan[pos] == quote)
                        {
                            pos++;
                            break;
                        }
                        else
                        {
                            pos++;
                        }
                    }
                }
                else
                {
                    pos++;
                }
            }

            ReadOnlySpan<char> lineSpan = declSpan.Slice(startLine, pos - startLine).Trim();
            if (pos < len && declSpan[pos] == ';')
            {
                pos++; // consume ';'
            }

            if (lineSpan.IsEmpty)
                continue;

            int colonIdx = lineSpan.IndexOf(':');
            if (colonIdx != -1)
            {
                ReadOnlySpan<char> nameSpan = lineSpan.Slice(0, colonIdx).Trim();
                ReadOnlySpan<char> valSpan = lineSpan.Slice(colonIdx + 1).Trim();
                if (!nameSpan.IsEmpty)
                {
                    string name = GetPooledString(nameSpan);
                    declarations[name] = CollapseWhitespace(valSpan);
                }
            }
        }
    }

    public static CssSelector ParseSelector(string selectorText)
    {
        if (selectorText == null)
            throw new ArgumentNullException(nameof(selectorText));
        return ParseSelector(selectorText.AsSpan());
    }

    private static CssSelector ParseSelector(ReadOnlySpan<char> selectorSpan)
    {
        string normalized = NormalizeSelectorText(selectorSpan);
        ReadOnlySpan<char> currentText = normalized.AsSpan();

        int currentTextLen = currentText.Length;
        var parts = System.Buffers.ArrayPool<SelectorPart>.Shared.Rent(16);
        int partCount = 0;

        try
        {
            int currentEnd = currentTextLen;
            while (true)
            {
                int lastCombinatorIdx = -1;
                char combinatorChar = '\0';
                int parenDepth = 0;

                for (int i = currentEnd - 1; i >= 0; i--)
                {
                    char c = currentText[i];
                    if (c == ')')
                    {
                        parenDepth++;
                        continue;
                    }
                    if (c == '(')
                    {
                        if (parenDepth > 0) parenDepth--;
                        continue;
                    }

                    if (parenDepth == 0)
                    {
                        if (c == '>' || c == '+' || c == '~')
                        {
                            lastCombinatorIdx = i;
                            combinatorChar = c;
                            break;
                        }
                        if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                        {
                            lastCombinatorIdx = i;
                            combinatorChar = ' ';
                            break;
                        }
                    }
                }

                if (lastCombinatorIdx != -1)
                {
                    int rightStart = lastCombinatorIdx + 1;
                    int rightLen = currentEnd - rightStart;
                    
                    while (rightLen > 0 && char.IsWhiteSpace(currentText[rightStart]))
                    {
                        rightStart++;
                        rightLen--;
                    }
                    while (rightLen > 0 && char.IsWhiteSpace(currentText[rightStart + rightLen - 1]))
                    {
                        rightLen--;
                    }

                    if (partCount >= parts.Length)
                    {
                        var newParts = System.Buffers.ArrayPool<SelectorPart>.Shared.Rent(parts.Length * 2);
                        Array.Copy(parts, newParts, parts.Length);
                        System.Buffers.ArrayPool<SelectorPart>.Shared.Return(parts);
                        parts = newParts;
                    }

                    parts[partCount++] = new SelectorPart
                    {
                        RightStart = rightStart,
                        RightLength = rightLen,
                        Combinator = combinatorChar,
                        TextLength = currentEnd
                    };

                    currentEnd = lastCombinatorIdx;
                    while (currentEnd > 0 && char.IsWhiteSpace(currentText[currentEnd - 1]))
                    {
                        currentEnd--;
                    }
                }
                else
                {
                    int rightStart = 0;
                    int rightLen = currentEnd;
                    while (rightLen > 0 && char.IsWhiteSpace(currentText[rightStart]))
                    {
                        rightStart++;
                        rightLen--;
                    }
                    while (rightLen > 0 && char.IsWhiteSpace(currentText[rightStart + rightLen - 1]))
                    {
                        rightLen--;
                    }

                    if (partCount >= parts.Length)
                    {
                        var newParts = System.Buffers.ArrayPool<SelectorPart>.Shared.Rent(parts.Length * 2);
                        Array.Copy(parts, newParts, parts.Length);
                        System.Buffers.ArrayPool<SelectorPart>.Shared.Return(parts);
                        parts = newParts;
                    }

                    parts[partCount++] = new SelectorPart
                    {
                        RightStart = rightStart,
                        RightLength = rightLen,
                        Combinator = '\0',
                        TextLength = currentEnd
                    };
                    break;
                }
            }

            var selectors = new CssSelector[partCount];
            for (int i = 0; i < partCount; i++)
            {
                var part = parts[i];
                ReadOnlySpan<char> rightSpan = currentText.Slice(part.RightStart, part.RightLength);
                selectors[i] = ParseCompoundSelector(rightSpan);
                selectors[i].Combinator = part.Combinator != '\0' ? part.Combinator.ToString() : null;
                selectors[i].Text = normalized.Substring(0, part.TextLength);
            }

            for (int i = 0; i < partCount - 1; i++)
            {
                selectors[i].ParentSelector = selectors[i + 1];
            }

            for (int i = partCount - 2; i >= 0; i--)
            {
                var selfSpec = selectors[i].Specificity;
                var parentSpec = selectors[i + 1].Specificity;
                selectors[i].Specificity = new Specificity(
                    selfSpec.IdCount + parentSpec.IdCount,
                    selfSpec.ClassCount + parentSpec.ClassCount,
                    selfSpec.TagCount + parentSpec.TagCount
                );
            }

            return selectors[0];
        }
        finally
        {
            System.Buffers.ArrayPool<SelectorPart>.Shared.Return(parts);
        }
    }

    private static CssSelector ParseCompoundSelector(ReadOnlySpan<char> selectorText)
    {
        var selector = new CssSelector { Text = new string(selectorText) };
        int i = 0;
        int len = selectorText.Length;

        int idCount = 0;
        int classCount = 0;
        int tagCount = 0;

        if (i < len && selectorText[i] != '.' && selectorText[i] != '#' && selectorText[i] != ':')
        {
            int start = i;
            while (i < len && selectorText[i] != '.' && selectorText[i] != '#' && selectorText[i] != ':')
            {
                i++;
            }
            ReadOnlySpan<char> tagNameSpan = selectorText.Slice(start, i - start).Trim();
            if (!tagNameSpan.IsEmpty)
            {
                selector.TagName = GetPooledString(tagNameSpan);
                if (selector.TagName != "*")
                {
                    tagCount = 1;
                }
            }
        }

        while (i < len)
        {
            if (selectorText[i] == '.')
            {
                i++;
                int start = i;
                while (i < len && selectorText[i] != '.' && selectorText[i] != '#' && selectorText[i] != ':')
                {
                    i++;
                }
                ReadOnlySpan<char> classNameSpan = selectorText.Slice(start, i - start).Trim();
                if (!classNameSpan.IsEmpty)
                {
                    selector.Classes.Add(GetPooledString(classNameSpan));
                    classCount++;
                }
            }
            else if (selectorText[i] == '#')
            {
                i++;
                int start = i;
                while (i < len && selectorText[i] != '.' && selectorText[i] != '#' && selectorText[i] != ':')
                {
                    i++;
                }
                ReadOnlySpan<char> idSpan = selectorText.Slice(start, i - start).Trim();
                if (!idSpan.IsEmpty)
                {
                    selector.Id = new string(idSpan);
                    idCount++;
                }
            }
            else if (selectorText[i] == ':')
            {
                bool isPseudoElement = false;
                if (i + 1 < len && selectorText[i + 1] == ':')
                {
                    isPseudoElement = true;
                    i += 2;
                }
                else
                {
                    i++;
                }

                int start = i;
                int parenCount = 0;
                while (i < len)
                {
                    char c = selectorText[i];
                    if (c == '\\')
                    {
                        if (i + 1 < len)
                        {
                            i += 2;
                        }
                        else
                        {
                            i++;
                        }
                        continue;
                    }
                    if (c == '(')
                    {
                        parenCount++;
                    }
                    else if (c == ')')
                    {
                        if (parenCount > 0) parenCount--;
                    }
                    else if (parenCount == 0 && (c == '.' || c == '#' || c == ':'))
                    {
                        break;
                    }
                    i++;
                }
                ReadOnlySpan<char> pseudoNameSpan = selectorText.Slice(start, i - start).Trim();
                if (!pseudoNameSpan.IsEmpty)
                {
                    string name = GetPooledString(pseudoNameSpan);
                    selector.PseudoClasses.Add(isPseudoElement ? "::" + name : ":" + name);
                    if (isPseudoElement)
                    {
                        tagCount++;
                    }
                    else
                    {
                        classCount++;
                    }
                }
            }
            else
            {
                i++;
            }
        }

        selector.Specificity = new Specificity(idCount, classCount, tagCount);
        return selector;
    }
}
