using System;
using System.Collections.Generic;
using System.Net;
using CDP.Html.Parser;

namespace UnoptimizedCDP.Html.Parser;

public static class UnoptimizedHtmlParser
{
    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    };

    private static string GetPooledString(ReadOnlySpan<char> span)
    {
        return span.IsEmpty ? string.Empty : new string(span);
    }

    private static string DecodeHtml(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return string.Empty;
        }
        if (span.IndexOf('&') < 0)
        {
            return new string(span);
        }
        return WebUtility.HtmlDecode(new string(span));
    }

    public static HtmlDocument Parse(string htmlSource)
    {
        if (htmlSource == null)
            throw new ArgumentNullException(nameof(htmlSource));

        var doc = new HtmlDocument();
        doc.Span = new SourceSpan(0, htmlSource.Length);

        var stack = new Stack<HtmlNode>();
        stack.Push(doc);

        int pos = 0;
        int len = htmlSource.Length;

        while (pos < len)
        {
            // Check if we are inside a script or style block
            if (stack.Peek() is HtmlElement currentElement &&
                (string.Equals(currentElement.TagName, "script", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(currentElement.TagName, "style", StringComparison.OrdinalIgnoreCase)))
            {
                string targetTagName = currentElement.TagName;
                int endTagPos = -1;
                int searchPos = pos;
                while (searchPos < len)
                {
                    int nextCloseStart = htmlSource.IndexOf("</", searchPos, StringComparison.Ordinal);
                    if (nextCloseStart == -1)
                    {
                        break;
                    }

                    int tagPos = nextCloseStart + 2;
                    if (tagPos + targetTagName.Length <= len &&
                        htmlSource.AsSpan(tagPos, targetTagName.Length).Equals(targetTagName, StringComparison.OrdinalIgnoreCase))
                    {
                        int tempPos = tagPos + targetTagName.Length;
                        while (tempPos < len && char.IsWhiteSpace(htmlSource[tempPos]))
                        {
                            tempPos++;
                        }
                        if (tempPos < len && htmlSource[tempPos] == '>')
                        {
                            endTagPos = nextCloseStart;
                            break;
                        }
                    }
                    searchPos = nextCloseStart + 2;
                }

                if (endTagPos != -1)
                {
                    if (endTagPos > pos)
                    {
                        string blockDecodedText = DecodeHtml(htmlSource.AsSpan(pos, endTagPos - pos));
                        var textNode = new HtmlTextNode
                        {
                            Text = blockDecodedText,
                            Parent = currentElement,
                            Span = new SourceSpan(pos, endTagPos - pos)
                        };
                        currentElement.Children.Add(textNode);
                        pos = endTagPos;
                        continue;
                    }
                }
                else
                {
                    if (pos < len)
                    {
                        string blockDecodedText = DecodeHtml(htmlSource.AsSpan(pos, len - pos));
                        var textNode = new HtmlTextNode
                        {
                            Text = blockDecodedText,
                            Parent = currentElement,
                            Span = new SourceSpan(pos, len - pos)
                        };
                        currentElement.Children.Add(textNode);
                    }
                    pos = len;
                    continue;
                }
            }

            // Skip HTML comments
            if (pos + 4 <= len && htmlSource.AsSpan(pos, 4).Equals("<!--", StringComparison.Ordinal))
            {
                pos += 4;
                int endIdx = htmlSource.IndexOf("-->", pos, StringComparison.Ordinal);
                if (endIdx == -1)
                {
                    pos = len;
                }
                else
                {
                    pos = endIdx + 3;
                }
                continue;
            }

            // Parse end tag
            if (pos + 2 <= len && htmlSource.AsSpan(pos, 2).Equals("</", StringComparison.Ordinal))
            {
                pos += 2;
                // Read tag name
                int nameStart = pos;
                while (pos < len && IsTagChar(htmlSource[pos]))
                {
                    pos++;
                }
                string tagName = GetPooledString(htmlSource.AsSpan(nameStart, pos - nameStart));

                // Find closing '>'
                while (pos < len && htmlSource[pos] != '>')
                {
                    pos++;
                }
                if (pos < len && htmlSource[pos] == '>')
                {
                    pos++; // consume '>'
                }

                if (!string.IsNullOrEmpty(tagName))
                {
                    // Find the matching element in stack
                    HtmlNode? matchedNode = null;
                    foreach (var node in stack)
                    {
                        if (node is HtmlElement element && string.Equals(element.TagName, tagName, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedNode = element;
                            break;
                        }
                    }

                    if (matchedNode != null)
                    {
                        while (stack.Count > 0)
                        {
                            var popped = stack.Pop();
                            popped.Span = new SourceSpan(popped.Span.Start, pos - popped.Span.Start);
                            if (popped == matchedNode)
                            {
                                break;
                            }
                        }
                    }
                }
                continue;
            }

            // Parse start tag
            if (htmlSource[pos] == '<')
            {
                int tagStart = pos;
                pos++; // consume '<'

                // Read tag name
                int nameStart = pos;
                while (pos < len && IsTagChar(htmlSource[pos]))
                {
                    pos++;
                }
                string tagName = GetPooledString(htmlSource.AsSpan(nameStart, pos - nameStart));

                if (string.IsNullOrEmpty(tagName))
                {
                    // It's not a valid start tag, treat '<' as text
                    var textNode = new HtmlTextNode
                    {
                        Text = "<",
                        Parent = stack.Peek(),
                        Span = new SourceSpan(tagStart, 1)
                    };
                    stack.Peek().Children.Add(textNode);
                    continue;
                }

                var element = new HtmlElement
                {
                    TagName = tagName,
                    Parent = stack.Peek()
                };

                // Parse attributes
                bool selfClosing = false;
                while (pos < len)
                {
                    // Skip whitespace
                    while (pos < len && char.IsWhiteSpace(htmlSource[pos]))
                    {
                        pos++;
                    }

                    if (pos >= len)
                        break;

                    if (htmlSource[pos] == '>')
                    {
                        pos++;
                        break;
                    }

                    if (pos + 2 <= len && htmlSource.AsSpan(pos, 2).Equals("/>", StringComparison.Ordinal))
                    {
                        selfClosing = true;
                        pos += 2;
                        break;
                    }

                    // Parse attribute name
                    int attrNameStart = pos;
                    while (pos < len && IsAttributeNameChar(htmlSource[pos]))
                    {
                        pos++;
                    }
                    string attrName = GetPooledString(htmlSource.AsSpan(attrNameStart, pos - attrNameStart));

                    if (string.IsNullOrEmpty(attrName))
                    {
                        pos++;
                        continue;
                    }

                    // Skip whitespace
                    while (pos < len && char.IsWhiteSpace(htmlSource[pos]))
                    {
                        pos++;
                    }

                    string attrValue = string.Empty;
                    if (pos < len && htmlSource[pos] == '=')
                    {
                        pos++; // consume '='
                        // Skip whitespace
                        while (pos < len && char.IsWhiteSpace(htmlSource[pos]))
                        {
                            pos++;
                        }

                        if (pos < len)
                        {
                            if (htmlSource[pos] == '"' || htmlSource[pos] == '\'')
                            {
                                char quote = htmlSource[pos];
                                pos++; // consume quote
                                int valStart = pos;
                                while (pos < len && htmlSource[pos] != quote)
                                {
                                    pos++;
                                }
                                attrValue = DecodeHtml(htmlSource.AsSpan(valStart, pos - valStart));
                                if (pos < len)
                                {
                                    pos++; // consume quote
                                }
                            }
                            else
                            {
                                // Unquoted value
                                int valStart = pos;
                                while (pos < len)
                                {
                                    char c = htmlSource[pos];
                                    if (char.IsWhiteSpace(c) || c == '>')
                                    {
                                        break;
                                    }
                                    if (c == '/' && pos + 1 < len && htmlSource[pos + 1] == '>')
                                    {
                                        break;
                                    }
                                    pos++;
                                }
                                attrValue = DecodeHtml(htmlSource.AsSpan(valStart, pos - valStart));
                            }
                        }
                    }

                    element.Attributes[attrName] = attrValue;
                }

                element.Span = new SourceSpan(tagStart, pos - tagStart);
                stack.Peek().Children.Add(element);

                bool isVoid = VoidTags.Contains(tagName);
                if (!selfClosing && !isVoid)
                {
                    stack.Push(element);
                }
                continue;
            }

            // Parse text node
            int textStart = pos;
            while (pos < len && htmlSource[pos] != '<')
            {
                pos++;
            }
            string decodedText = DecodeHtml(htmlSource.AsSpan(textStart, pos - textStart));
            if (!string.IsNullOrEmpty(decodedText))
            {
                var textNode = new HtmlTextNode
                {
                    Text = decodedText,
                    Parent = stack.Peek(),
                    Span = new SourceSpan(textStart, pos - textStart)
                };
                stack.Peek().Children.Add(textNode);
            }
        }

        // Close any remaining open nodes on the stack
        while (stack.Count > 1)
        {
            var popped = stack.Pop();
            popped.Span = new SourceSpan(popped.Span.Start, len - popped.Span.Start);
        }

        return doc;
    }

    private static bool IsTagChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '-' || c == ':' || c == '_';
    }

    private static bool IsAttributeNameChar(char c)
    {
        return c != '=' && c != '>' && c != '/' && !char.IsWhiteSpace(c);
    }
}
