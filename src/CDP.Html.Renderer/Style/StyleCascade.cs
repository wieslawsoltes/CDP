using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using SkiaSharp;
using CDP.Html.Parser;
using CDP.Css.Parser;

namespace CDP.Html.Renderer.Style;

public static class StyleCascade
{
    public static Dictionary<HtmlNode, ComputedStyle> ResolveStyles(
        HtmlDocument doc,
        CssStyleSheet? stylesheet,
        float viewportWidth = float.MaxValue,
        float viewportHeight = float.MaxValue,
        string mediaType = "screen")
    {
        var resolved = new Dictionary<HtmlNode, ComputedStyle>();
        ResolveNode(doc, null, stylesheet ?? new CssStyleSheet(), resolved, viewportWidth, viewportHeight, mediaType);
        return resolved;
    }

    private static void ResolveNode(
        HtmlNode node,
        ComputedStyle? parentStyle,
        CssStyleSheet stylesheet,
        Dictionary<HtmlNode, ComputedStyle> resolved,
        float viewportWidth,
        float viewportHeight,
        string mediaType)
    {
        if (node is HtmlDocument)
        {
            // Document node doesn't render directly, but serves as root
            var docStyle = new ComputedStyle { Display = DisplayType.Block };
            resolved[node] = docStyle;
            foreach (var child in node.Children)
            {
                ResolveNode(child, docStyle, stylesheet, resolved, viewportWidth, viewportHeight, mediaType);
            }
            return;
        }

        if (node is HtmlTextNode textNode)
        {
            // Text nodes inherit everything from parent
            var textStyle = new ComputedStyle();
            if (parentStyle != null)
            {
                textStyle.InheritFrom(parentStyle);
            }
            resolved[textNode] = textStyle;
            return;
        }

        if (node is HtmlElement element)
        {
            var style = new ComputedStyle();

            // Default display based on tag
            string tag = element.TagName.ToLowerInvariant();
            if (tag == "style" || tag == "script" || tag == "head" || tag == "meta" || tag == "link" || tag == "title")
            {
                style.Display = DisplayType.None;
            }
            else if (tag == "div" || tag == "p" || tag == "body" || tag == "html" ||
                     tag == "ul" || tag == "li" || tag == "h1" || tag == "h2" ||
                     tag == "h3" || tag == "h4" || tag == "h5" || tag == "h6" ||
                     tag == "header" || tag == "footer" || tag == "section" || tag == "article")
            {
                style.Display = DisplayType.Block;
            }
            else
            {
                style.Display = DisplayType.Inline;
            }

            // Step 1: Inherit from parent
            if (parentStyle != null)
            {
                style.InheritFrom(parentStyle);
            }

            // Step 2: Apply matching stylesheet rules (sorted by specificity and declaration order)
            int totalSelectors = 0;
            foreach (var rule in stylesheet.Rules)
            {
                totalSelectors += rule.Selectors.Count;
            }

            MatchingRuleInfo[]? rentedArray = null;
            int matchCount = 0;

            if (totalSelectors > 0)
            {
                rentedArray = System.Buffers.ArrayPool<MatchingRuleInfo>.Shared.Rent(totalSelectors);
                foreach (var rule in stylesheet.Rules)
                {
                    if (!EvaluateMediaCondition(rule.MediaCondition, viewportWidth, viewportHeight, mediaType))
                    {
                        continue;
                    }
                    foreach (var selector in rule.Selectors)
                    {
                        if (Matches(selector, element))
                        {
                            rentedArray[matchCount] = new MatchingRuleInfo
                            {
                                Rule = rule,
                                Selector = selector,
                                Index = matchCount
                            };
                            matchCount++;
                        }
                    }
                }
            }

            if (matchCount > 0 && rentedArray != null)
            {
                Array.Sort(rentedArray, 0, matchCount, MatchingRuleInfoComparer.Instance);
            }

            // Get style attribute
            string? inlineStyle = null;
            bool hasInlineStyle = element.Attributes.TryGetValue("style", out inlineStyle) && !string.IsNullOrWhiteSpace(inlineStyle);

            // First Pass: Apply custom variables (starting with "--") from stylesheet rules and inline styles
            if (matchCount > 0 && rentedArray != null)
            {
                for (int i = 0; i < matchCount; i++)
                {
                    var ruleInfo = rentedArray[i];
                    foreach (var decl in ruleInfo.Rule.Declarations)
                    {
                        if (decl.Key.StartsWith("--"))
                        {
                            ApplyDeclaration(style, decl.Key, decl.Value, parentStyle);
                        }
                    }
                }
            }

            if (hasInlineStyle)
            {
                ReadOnlySpan<char> styleSpan = inlineStyle.AsSpan();
                int start = 0;
                while (start < styleSpan.Length)
                {
                    int semi = styleSpan.Slice(start).IndexOf(';');
                    ReadOnlySpan<char> part = semi == -1 ? styleSpan.Slice(start) : styleSpan.Slice(start, semi);
                    start = semi == -1 ? styleSpan.Length : start + semi + 1;

                    int colon = part.IndexOf(':');
                    if (colon != -1)
                    {
                        ReadOnlySpan<char> nameSpan = part.Slice(0, colon).Trim();
                        ReadOnlySpan<char> valSpan = part.Slice(colon + 1).Trim();
                        if (!nameSpan.IsEmpty && nameSpan.StartsWith("--"))
                        {
                            ApplyDeclaration(style, nameSpan.ToString(), valSpan.ToString(), parentStyle);
                        }
                    }
                }
            }

            // Second Pass: Apply standard properties (not starting with "--") from stylesheet rules and inline styles
            if (matchCount > 0 && rentedArray != null)
            {
                for (int i = 0; i < matchCount; i++)
                {
                    var ruleInfo = rentedArray[i];
                    foreach (var decl in ruleInfo.Rule.Declarations)
                    {
                        if (!decl.Key.StartsWith("--"))
                        {
                            ApplyDeclaration(style, decl.Key, decl.Value, parentStyle);
                        }
                    }
                }
            }

            if (hasInlineStyle)
            {
                ReadOnlySpan<char> styleSpan = inlineStyle.AsSpan();
                int start = 0;
                while (start < styleSpan.Length)
                {
                    int semi = styleSpan.Slice(start).IndexOf(';');
                    ReadOnlySpan<char> part = semi == -1 ? styleSpan.Slice(start) : styleSpan.Slice(start, semi);
                    start = semi == -1 ? styleSpan.Length : start + semi + 1;

                    int colon = part.IndexOf(':');
                    if (colon != -1)
                    {
                        ReadOnlySpan<char> nameSpan = part.Slice(0, colon).Trim();
                        ReadOnlySpan<char> valSpan = part.Slice(colon + 1).Trim();
                        if (!nameSpan.IsEmpty && !nameSpan.StartsWith("--"))
                        {
                            ApplyDeclaration(style, nameSpan.ToString(), valSpan.ToString(), parentStyle);
                        }
                    }
                }
            }

            if (rentedArray != null)
            {
                System.Buffers.ArrayPool<MatchingRuleInfo>.Shared.Return(rentedArray);
            }

            resolved[element] = style;

            // Resolve children recursively
            foreach (var child in element.Children)
            {
                ResolveNode(child, style, stylesheet, resolved, viewportWidth, viewportHeight, mediaType);
            }
        }
    }

    public static bool Matches(CssSelector selector, HtmlElement element)
    {
        // 1. Tag name check
        if (!string.IsNullOrEmpty(selector.TagName) && selector.TagName != "*")
        {
            if (!string.Equals(element.TagName, selector.TagName, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // 2. ID check
        if (!string.IsNullOrEmpty(selector.Id))
        {
            if (!element.Attributes.TryGetValue("id", out var elementId) ||
                !string.Equals(elementId, selector.Id, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // 3. Classes check
        foreach (var cls in selector.Classes)
        {
            if (!element.Attributes.TryGetValue("class", out var classAttr))
                return false;

            ReadOnlySpan<char> classSpan = classAttr.AsSpan();
            bool found = false;
            int start = 0;
            while (start < classSpan.Length)
            {
                while (start < classSpan.Length && classSpan[start] == ' ')
                {
                    start++;
                }
                if (start >= classSpan.Length)
                    break;

                int end = start;
                while (end < classSpan.Length && classSpan[end] != ' ')
                {
                    end++;
                }

                ReadOnlySpan<char> currentClass = classSpan.Slice(start, end - start);
                if (currentClass.Equals(cls.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }

                start = end + 1;
            }

            if (!found)
                return false;
        }

        // 4. Parent/Combinator checks
        if (selector.ParentSelector != null)
        {
            if (selector.Combinator == ">")
            {
                // Direct child
                if (element.Parent is not HtmlElement parentElement || !Matches(selector.ParentSelector, parentElement))
                    return false;
            }
            else if (selector.Combinator == "+")
            {
                // Adjacent sibling (immediately preceding element sibling)
                if (element.Parent == null)
                    return false;

                int index = element.Parent.Children.IndexOf(element);
                if (index <= 0)
                    return false;

                HtmlElement? prevElement = null;
                for (int i = index - 1; i >= 0; i--)
                {
                    if (element.Parent.Children[i] is HtmlElement el)
                    {
                        prevElement = el;
                        break;
                    }
                }

                if (prevElement == null || !Matches(selector.ParentSelector, prevElement))
                    return false;
            }
            else if (selector.Combinator == "~")
            {
                // General sibling (any preceding element sibling)
                if (element.Parent == null)
                    return false;

                int index = element.Parent.Children.IndexOf(element);
                if (index <= 0)
                    return false;

                bool matchedSibling = false;
                for (int i = index - 1; i >= 0; i--)
                {
                    if (element.Parent.Children[i] is HtmlElement prevElement)
                    {
                        if (Matches(selector.ParentSelector, prevElement))
                        {
                            matchedSibling = true;
                            break;
                        }
                    }
                }

                if (!matchedSibling)
                    return false;
            }
            else // Descendant selector (combinator == " " or empty/null)
            {
                // Any ancestor
                var curr = element.Parent;
                bool matchedAncestor = false;
                while (curr is HtmlElement parentElement)
                {
                    if (Matches(selector.ParentSelector, parentElement))
                    {
                        matchedAncestor = true;
                        break;
                    }
                    curr = parentElement.Parent;
                }
                if (!matchedAncestor)
                    return false;
            }
        }

        // 5. Pseudo-class check
        if (selector.PseudoClasses.Count > 0)
        {
            foreach (var pc in selector.PseudoClasses)
            {
                var cleanPc = pc.ToLowerInvariant();
                if (cleanPc == ":first-child" || cleanPc == "first-child")
                {
                    if (element.Parent == null)
                        return false;

                    HtmlElement? firstChildElement = null;
                    foreach (var child in element.Parent.Children)
                    {
                        if (child is HtmlElement childEl)
                        {
                            firstChildElement = childEl;
                            break;
                        }
                    }
                    if (firstChildElement != element)
                        return false;
                }
                else if (cleanPc == ":last-child" || cleanPc == "last-child")
                {
                    if (element.Parent == null)
                        return false;

                    HtmlElement? lastChildElement = null;
                    for (int i = element.Parent.Children.Count - 1; i >= 0; i--)
                    {
                        if (element.Parent.Children[i] is HtmlElement childEl)
                        {
                            lastChildElement = childEl;
                            break;
                        }
                    }
                    if (lastChildElement != element)
                        return false;
                }
                else if ((cleanPc.StartsWith(":nth-child(") || cleanPc.StartsWith("nth-child(")) && cleanPc.EndsWith(")"))
                {
                    if (element.Parent == null)
                        return false;

                    int startIdx = cleanPc.StartsWith(":") ? ":nth-child(".Length : "nth-child(".Length;
                    string arg = cleanPc.Substring(startIdx, cleanPc.Length - startIdx - 1);
                    if (!ParseNth(arg, out int a, out int b))
                        return false;

                    int index = 0;
                    bool found = false;
                    foreach (var child in element.Parent.Children)
                    {
                        if (child is HtmlElement childEl)
                        {
                            index++;
                            if (childEl == element)
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                        return false;

                    if (!MatchesNth(index, a, b))
                        return false;
                }
                else if ((cleanPc.StartsWith(":nth-last-child(") || cleanPc.StartsWith("nth-last-child(")) && cleanPc.EndsWith(")"))
                {
                    if (element.Parent == null)
                        return false;

                    int startIdx = cleanPc.StartsWith(":") ? ":nth-last-child(".Length : "nth-last-child(".Length;
                    string arg = cleanPc.Substring(startIdx, cleanPc.Length - startIdx - 1);
                    if (!ParseNth(arg, out int a, out int b))
                        return false;

                    int indexFromStart = 0;
                    int totalElements = 0;
                    bool found = false;
                    foreach (var child in element.Parent.Children)
                    {
                        if (child is HtmlElement childEl)
                        {
                            totalElements++;
                            if (childEl == element)
                            {
                                indexFromStart = totalElements;
                                found = true;
                            }
                        }
                    }

                    if (!found)
                        return false;

                    int indexFromEnd = totalElements - indexFromStart + 1;

                    if (!MatchesNth(indexFromEnd, a, b))
                        return false;
                }
                else
                {
                    // Unsupported dynamic/interactive pseudo-class (e.g. hover, focus, active)
                    return false;
                }
            }
        }

        return true;
    }



    public static void ApplyDeclaration(ComputedStyle style, string name, string value, ComputedStyle? parentStyle)
    {
        name = name.Trim().ToLowerInvariant();
        value = value.Trim();

        if (name.StartsWith("--"))
        {
            style.CustomProperties[name] = value;
            return;
        }

        value = ResolveVariables(value, style);

        switch (name)
        {
            case "display":
                if (Enum.TryParse<DisplayType>(value, true, out var disp))
                    style.Display = disp;
                break;
            case "position":
                if (Enum.TryParse<PositionType>(value, true, out var pos))
                    style.Position = pos;
                break;
            case "color":
                if (TryParseColor(value, out var color))
                    style.Color = color;
                break;
            case "background-color":
                if (TryParseColor(value, out var bgColor))
                    style.BackgroundColor = bgColor;
                break;
            case "font-size":
                style.FontSize = ParseFontSize(value, parentStyle?.FontSize ?? 16f);
                break;
            case "font-family":
                style.FontFamily = value.Replace("\"", "").Replace("'", "").Trim();
                break;
            case "font-weight":
                style.FontWeight = ParseFontWeight(value);
                break;
            case "font-style":
                style.FontStyle = ParseFontStyle(value);
                break;
            case "line-height":
                style.LineHeight = ParseLineHeight(value, style.FontSize);
                break;
            case "text-align":
                if (Enum.TryParse<TextAlignType>(value, true, out var align))
                    style.TextAlign = align;
                break;
            case "width":
                style.Width = ParseLength(value);
                break;
            case "height":
                style.Height = ParseLength(value);
                break;
            case "top":
                style.Top = ParseLength(value);
                break;
            case "right":
                style.Right = ParseLength(value);
                break;
            case "bottom":
                style.Bottom = ParseLength(value);
                break;
            case "left":
                style.Left = ParseLength(value);
                break;
            case "min-width":
                style.MinWidth = ParseLength(value);
                break;
            case "max-width":
                style.MaxWidth = ParseLength(value);
                break;
            case "min-height":
                style.MinHeight = ParseLength(value);
                break;
            case "max-height":
                style.MaxHeight = ParseLength(value);
                break;

            case "margin":
                ParseShorthandLength(value, out var mt, out var mr, out var mb, out var ml);
                style.MarginTop = mt;
                style.MarginRight = mr;
                style.MarginBottom = mb;
                style.MarginLeft = ml;
                break;
            case "margin-top":
                style.MarginTop = ParseLength(value);
                break;
            case "margin-right":
                style.MarginRight = ParseLength(value);
                break;
            case "margin-bottom":
                style.MarginBottom = ParseLength(value);
                break;
            case "margin-left":
                style.MarginLeft = ParseLength(value);
                break;

            case "padding":
                ParseShorthandLength(value, out var pt, out var pr, out var pb, out var pl);
                style.PaddingTop = pt;
                style.PaddingRight = pr;
                style.PaddingBottom = pb;
                style.PaddingLeft = pl;
                break;
            case "padding-top":
                style.PaddingTop = ParseLength(value);
                break;
            case "padding-right":
                style.PaddingRight = ParseLength(value);
                break;
            case "padding-bottom":
                style.PaddingBottom = ParseLength(value);
                break;
            case "padding-left":
                style.PaddingLeft = ParseLength(value);
                break;

            case "border":
                ParseBorderShorthand(value, out var bWidth, out var bColor);
                style.BorderTopWidth = style.BorderRightWidth = style.BorderBottomWidth = style.BorderLeftWidth = bWidth;
                style.BorderTopColor = style.BorderRightColor = style.BorderBottomColor = style.BorderLeftColor = bColor;
                break;
            case "border-width":
                ParseShorthandWidths(value, out var btw, out var brw, out var bbw, out var blw);
                style.BorderTopWidth = btw;
                style.BorderRightWidth = brw;
                style.BorderBottomWidth = bbw;
                style.BorderLeftWidth = blw;
                break;
            case "border-top-width":
                style.BorderTopWidth = ParseWidth(value);
                break;
            case "border-right-width":
                style.BorderRightWidth = ParseWidth(value);
                break;
            case "border-bottom-width":
                style.BorderBottomWidth = ParseWidth(value);
                break;
            case "border-left-width":
                style.BorderLeftWidth = ParseWidth(value);
                break;
            case "border-color":
                ParseShorthandColors(value, out var btc, out var brc, out var bbc, out var blc);
                style.BorderTopColor = btc;
                style.BorderRightColor = brc;
                style.BorderBottomColor = bbc;
                style.BorderLeftColor = blc;
                break;
            case "border-top-color":
                if (TryParseColor(value, out var tc)) style.BorderTopColor = tc;
                break;
            case "border-right-color":
                if (TryParseColor(value, out var rc)) style.BorderRightColor = rc;
                break;
            case "border-bottom-color":
                if (TryParseColor(value, out var bc)) style.BorderBottomColor = bc;
                break;
            case "border-left-color":
                if (TryParseColor(value, out var lc)) style.BorderLeftColor = lc;
                break;

            // Flexbox
            case "flex-direction":
                if (Enum.TryParse<FlexDirection>(value.Replace("-", ""), true, out var fdir))
                    style.FlexDirection = fdir;
                break;
            case "flex-wrap":
                if (Enum.TryParse<FlexWrap>(value.Replace("-", ""), true, out var fwrap))
                    style.FlexWrap = fwrap;
                break;
            case "justify-content":
                if (Enum.TryParse<JustifyContent>(value.Replace("-", ""), true, out var jcont))
                    style.JustifyContent = jcont;
                break;
            case "align-items":
                if (Enum.TryParse<AlignItems>(value.Replace("-", ""), true, out var aitems))
                    style.AlignItems = aitems;
                break;
            case "flex-grow":
                if (float.TryParse(value, out var fg))
                    style.FlexGrow = fg;
                break;
            case "flex-shrink":
                if (float.TryParse(value, out var fs))
                    style.FlexShrink = fs;
                break;
            case "flex-basis":
                style.FlexBasis = ParseLength(value);
                break;
            case "flex":
                ParseFlexShorthand(value, style);
                break;
            case "float":
                if (Enum.TryParse<FloatType>(value, true, out var fl))
                    style.Float = fl;
                break;
            case "clear":
                if (Enum.TryParse<ClearType>(value, true, out var cl))
                    style.Clear = cl;
                break;
        }
    }

    public static CssLength ParseLength(string val)
    {
        val = val.Trim().ToLowerInvariant();
        if (val == "auto" || string.IsNullOrEmpty(val))
            return CssLength.Auto;

        if (val.StartsWith("calc(") && val.EndsWith(")"))
        {
            string expr = val.Substring(5, val.Length - 6);
            return new CssLength(0f, LengthUnit.Calc, expr);
        }

        if (val.EndsWith("px", StringComparison.Ordinal))
        {
            if (float.TryParse(val.AsSpan(0, val.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                return new CssLength(f, LengthUnit.Px);
        }
        else if (val.EndsWith("%", StringComparison.Ordinal))
        {
            if (float.TryParse(val.AsSpan(0, val.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                return new CssLength(f, LengthUnit.Percent);
        }
        else
        {
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                return new CssLength(f, LengthUnit.Px);
        }

        return CssLength.Auto;
    }

    public static float ParseWidth(string val)
    {
        var len = ParseLength(val);
        return len.IsPx ? len.Value : 0f;
    }

    public static bool TryParseColor(string val, out SKColor color)
    {
        color = SKColors.Black;
        val = val.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(val) || val == "transparent")
        {
            color = SKColors.Transparent;
            return true;
        }

        if (val.StartsWith("#"))
        {
            string hex = val.Substring(1);
            if (hex.Length == 3)
            {
                hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
            }
            if (hex.Length == 4)
            {
                hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2], hex[3], hex[3] });
            }
            if (SKColor.TryParse(hex, out color))
            {
                return true;
            }
        }
        else if (val.StartsWith("rgb"))
        {
            try
            {
                int start = val.IndexOf('(');
                int end = val.IndexOf(')');
                if (start != -1 && end != -1 && end > start)
                {
                    string content = val.Substring(start + 1, end - start - 1);
                    var parts = content.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        byte r = byte.Parse(parts[0].Trim());
                        byte g = byte.Parse(parts[1].Trim());
                        byte b = byte.Parse(parts[2].Trim());
                        byte a = 255;
                        if (parts.Length >= 4)
                        {
                            float alpha = float.Parse(parts[3].Trim(), CultureInfo.InvariantCulture);
                            a = (byte)Math.Clamp((int)(alpha * 255), 0, 255);
                        }
                        color = new SKColor(r, g, b, a);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
        else
        {
            // Named colors
            switch (val)
            {
                case "white": color = SKColors.White; return true;
                case "black": color = SKColors.Black; return true;
                case "red": color = SKColors.Red; return true;
                case "green": color = SKColors.Green; return true;
                case "blue": color = SKColors.Blue; return true;
                case "gray":
                case "grey": color = SKColors.Gray; return true;
                case "lightgray":
                case "lightgrey": color = SKColors.LightGray; return true;
                case "darkgray":
                case "darkgrey": color = SKColors.DarkGray; return true;
                case "yellow": color = SKColors.Yellow; return true;
                case "magenta": color = SKColors.Magenta; return true;
                case "cyan": color = SKColors.Cyan; return true;
                case "orange": color = new SKColor(255, 165, 0); return true;
                case "purple": color = new SKColor(128, 0, 128); return true;
            }
        }

        return false;
    }

    private static float ParseFontSize(string val, float parentSize)
    {
        val = val.Trim().ToLowerInvariant();
        if (val.EndsWith("em", StringComparison.Ordinal))
        {
            if (float.TryParse(val.AsSpan(0, val.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var em))
                return em * parentSize;
        }
        else if (val.EndsWith("%", StringComparison.Ordinal))
        {
            if (float.TryParse(val.AsSpan(0, val.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                return (pct / 100f) * parentSize;
        }
        else if (val.EndsWith("px", StringComparison.Ordinal))
        {
            if (float.TryParse(val.AsSpan(0, val.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
                return px;
        }
        else
        {
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
                return px;
        }
        return 16f;
    }

    private static SKFontStyleWeight ParseFontWeight(string val)
    {
        val = val.Trim().ToLowerInvariant();
        if (val == "bold") return SKFontStyleWeight.Bold;
        if (val == "normal") return SKFontStyleWeight.Normal;
        if (int.TryParse(val, out var weightNum))
        {
            return (SKFontStyleWeight)weightNum;
        }
        return SKFontStyleWeight.Normal;
    }

    private static SKFontStyleSlant ParseFontStyle(string val)
    {
        val = val.Trim().ToLowerInvariant();
        if (val == "italic") return SKFontStyleSlant.Italic;
        if (val == "oblique") return SKFontStyleSlant.Oblique;
        return SKFontStyleSlant.Upright;
    }

    private static float? ParseLineHeight(string val, float fontSize)
    {
        val = val.Trim().ToLowerInvariant();
        if (val == "normal") return null;
        if (val.EndsWith("px", StringComparison.Ordinal))
        {
            if (float.TryParse(val.AsSpan(0, val.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
                return px;
        }
        else if (val.EndsWith("%", StringComparison.Ordinal))
        {
            if (float.TryParse(val.AsSpan(0, val.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                return (pct / 100f) * fontSize;
        }
        else
        {
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var mult))
                return mult * fontSize;
        }
        return null;
    }

    private static void ParseShorthandLength(string val, out CssLength t, out CssLength r, out CssLength b, out CssLength l)
    {
        var parts = val.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            t = r = b = l = ParseLength(parts[0]);
        }
        else if (parts.Length == 2)
        {
            t = b = ParseLength(parts[0]);
            r = l = ParseLength(parts[1]);
        }
        else if (parts.Length == 3)
        {
            t = ParseLength(parts[0]);
            r = l = ParseLength(parts[1]);
            b = ParseLength(parts[2]);
        }
        else if (parts.Length >= 4)
        {
            t = ParseLength(parts[0]);
            r = ParseLength(parts[1]);
            b = ParseLength(parts[2]);
            l = ParseLength(parts[3]);
        }
        else
        {
            t = r = b = l = CssLength.Zero;
        }
    }

    private static void ParseShorthandWidths(string val, out float t, out float r, out float b, out float l)
    {
        var parts = val.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            t = r = b = l = ParseWidth(parts[0]);
        }
        else if (parts.Length == 2)
        {
            t = b = ParseWidth(parts[0]);
            r = l = ParseWidth(parts[1]);
        }
        else if (parts.Length == 3)
        {
            t = ParseWidth(parts[0]);
            r = l = ParseWidth(parts[1]);
            b = ParseWidth(parts[2]);
        }
        else if (parts.Length >= 4)
        {
            t = ParseWidth(parts[0]);
            r = ParseWidth(parts[1]);
            b = ParseWidth(parts[2]);
            l = ParseWidth(parts[3]);
        }
        else
        {
            t = r = b = l = 0f;
        }
    }

    private static void ParseShorthandColors(string val, out SKColor t, out SKColor r, out SKColor b, out SKColor l)
    {
        // Simple spaces parsing might split inside rgb(a,b,c) declarations!
        // So we need to parse tokens carefully.
        var parts = SplitWhitespaceOrColors(val);
        if (parts.Count == 1)
        {
            TryParseColor(parts[0], out var c);
            t = r = b = l = c;
        }
        else if (parts.Count == 2)
        {
            TryParseColor(parts[0], out var c1);
            TryParseColor(parts[1], out var c2);
            t = b = c1;
            r = l = c2;
        }
        else if (parts.Count == 3)
        {
            TryParseColor(parts[0], out var c1);
            TryParseColor(parts[1], out var c2);
            TryParseColor(parts[2], out var c3);
            t = c1;
            r = l = c2;
            b = c3;
        }
        else if (parts.Count >= 4)
        {
            TryParseColor(parts[0], out var c1);
            TryParseColor(parts[1], out var c2);
            TryParseColor(parts[2], out var c3);
            TryParseColor(parts[3], out var c4);
            t = c1;
            r = c2;
            b = c3;
            l = c4;
        }
        else
        {
            t = r = b = l = SKColors.Black;
        }
    }

    private static List<string> SplitWhitespaceOrColors(string val)
    {
        var list = new List<string>();
        int i = 0;
        int len = val.Length;
        while (i < len)
        {
            while (i < len && char.IsWhiteSpace(val[i])) i++;
            if (i >= len) break;

            int start = i;
            if (val[i] == 'r' && i + 3 < len && val.AsSpan(i, 3).Equals("rgb", StringComparison.OrdinalIgnoreCase))
            {
                int parens = 0;
                while (i < len)
                {
                    if (val[i] == '(') parens++;
                    else if (val[i] == ')')
                    {
                        parens--;
                        if (parens == 0)
                        {
                            i++;
                            break;
                        }
                    }
                    i++;
                }
                list.Add(val.Substring(start, i - start));
            }
            else
            {
                while (i < len && !char.IsWhiteSpace(val[i])) i++;
                list.Add(val.Substring(start, i - start));
            }
        }
        return list;
    }

    private static void ParseBorderShorthand(string val, out float width, out SKColor color)
    {
        width = 0f;
        color = SKColors.Black;

        var tokens = SplitWhitespaceOrColors(val);
        foreach (var tok in tokens)
        {
            if (tok.EndsWith("px", StringComparison.Ordinal) || float.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                width = ParseWidth(tok);
            }
            else if (TryParseColor(tok, out var c))
            {
                color = c;
            }
            // style token like 'solid', 'dashed' is ignored for our simplified renderer
        }
    }

    private static void ParseFlexShorthand(string val, ComputedStyle style)
    {
        var tokens = val.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 1)
        {
            string t = tokens[0].ToLowerInvariant();
            if (t == "none")
            {
                style.FlexGrow = 0f;
                style.FlexShrink = 0f;
                style.FlexBasis = CssLength.Auto;
            }
            else if (t == "auto")
            {
                style.FlexGrow = 1f;
                style.FlexShrink = 1f;
                style.FlexBasis = CssLength.Auto;
            }
            else if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var fg))
            {
                style.FlexGrow = fg;
                style.FlexShrink = 1f;
                style.FlexBasis = CssLength.Zero;
            }
        }
        else if (tokens.Length == 2)
        {
            if (float.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var fg))
                style.FlexGrow = fg;

            // Second can be flex-shrink or flex-basis
            string t2 = tokens[1].ToLowerInvariant();
            if (float.TryParse(t2, NumberStyles.Float, CultureInfo.InvariantCulture, out var fs))
            {
                style.FlexShrink = fs;
            }
            else
            {
                style.FlexBasis = ParseLength(t2);
            }
        }
        else if (tokens.Length >= 3)
        {
            if (float.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var fg))
                style.FlexGrow = fg;
            if (float.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var fs))
                style.FlexShrink = fs;
            style.FlexBasis = ParseLength(tokens[2]);
        }
    }

    public struct MatchingRuleInfo
    {
        public CssRule Rule;
        public CssSelector Selector;
        public int Index;
    }

    private class MatchingRuleInfoComparer : IComparer<MatchingRuleInfo>
    {
        public static readonly MatchingRuleInfoComparer Instance = new();

        public int Compare(MatchingRuleInfo x, MatchingRuleInfo y)
        {
            int specCompare = x.Selector.Specificity.CompareTo(y.Selector.Specificity);
            if (specCompare != 0)
                return specCompare;
            return x.Index.CompareTo(y.Index);
        }
    }

    private static string ResolveVariables(string input, ComputedStyle style, HashSet<string>? visited = null)
    {
        return ResolveVariablesInternal(input, style, visited) ?? string.Empty;
    }

    private static string? ResolveVariablesInternal(string input, ComputedStyle style, HashSet<string>? visited)
    {
        if (!input.Contains("var(")) return input;
        visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int start = input.IndexOf("var(");
        while (start != -1)
        {
            int end = FindClosingParenthesis(input, start + 4);
            if (end == -1) break;

            string inner = input.Substring(start + 4, end - (start + 4));
            var parts = inner.Split(',', 2);
            string varName = parts[0].Trim();
            string? fallback = parts.Length > 1 ? parts[1].Trim() : null;

            string? resolvedVal = null;
            if (!visited.Contains(varName))
            {
                visited.Add(varName);
                if (style.CustomProperties.TryGetValue(varName, out var val))
                {
                    resolvedVal = ResolveVariablesInternal(val, style, visited);
                }
                visited.Remove(varName);
            }

            if (resolvedVal == null)
            {
                if (fallback != null)
                {
                    resolvedVal = ResolveVariablesInternal(fallback, style, visited);
                }

                if (resolvedVal == null)
                {
                    return null;
                }
            }

            input = input.Substring(0, start) + resolvedVal + input.Substring(end + 1);
            start = input.IndexOf("var(");
        }
        return input;
    }

    private static int FindClosingParenthesis(string input, int startIndex)
    {
        int depth = 1;
        for (int i = startIndex; i < input.Length; i++)
        {
            if (input[i] == '(') depth++;
            else if (input[i] == ')')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    public static bool MatchesNth(int index, int a, int b)
    {
        if (a == 0)
        {
            return index == b;
        }
        return (index - b) % a == 0 && (index - b) / a >= 0;
    }

    public static bool ParseNth(string arg, out int a, out int b)
    {
        a = 0;
        b = 0;
        arg = arg.Trim().ToLowerInvariant().Replace(" ", "");
        if (string.IsNullOrEmpty(arg)) return false;

        if (arg == "odd")
        {
            a = 2;
            b = 1;
            return true;
        }
        if (arg == "even")
        {
            a = 2;
            b = 0;
            return true;
        }

        int nIdx = arg.IndexOf('n');
        if (nIdx == -1)
        {
            if (int.TryParse(arg, out int val))
            {
                a = 0;
                b = val;
                return true;
            }
            return false;
        }

        string aPart = arg.Substring(0, nIdx);
        if (aPart == "" || aPart == "+") a = 1;
        else if (aPart == "-") a = -1;
        else if (!int.TryParse(aPart, out a)) return false;

        string bPart = arg.Substring(nIdx + 1);
        if (bPart == "") b = 0;
        else
        {
            if (bPart.StartsWith("+")) bPart = bPart.Substring(1);
            if (!int.TryParse(bPart, out b)) return false;
        }

        return true;
    }

    public static bool EvaluateMediaCondition(
        string? mediaCondition,
        float viewportWidth,
        float viewportHeight,
        string mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaCondition))
        {
            return true;
        }

        var parts = mediaCondition.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return true;
        }

        foreach (var part in parts)
        {
            if (EvaluateSingleQuery(part, viewportWidth, viewportHeight, mediaType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EvaluateSingleQuery(
        string query,
        float viewportWidth,
        float viewportHeight,
        string mediaType)
    {
        string trimmed = query.Trim();
        bool negate = false;
        if (trimmed.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
        {
            negate = true;
            trimmed = trimmed.Substring(4).Trim();
        }

        var andParts = SplitByAnd(trimmed);
        bool queryResult = true;
        foreach (var part in andParts)
        {
            if (!EvaluatePart(part, viewportWidth, viewportHeight, mediaType))
            {
                queryResult = false;
                break;
            }
        }

        return negate ? !queryResult : queryResult;
    }

    private static List<string> SplitByAnd(string query)
    {
        var parts = System.Text.RegularExpressions.Regex.Split(query, @"\band\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var list = new List<string>(parts.Length);
        foreach (var p in parts)
        {
            list.Add(p.Trim());
        }
        return list;
    }

    private static bool EvaluatePart(
        string part,
        float viewportWidth,
        float viewportHeight,
        string mediaType)
    {
        string clean = part.Trim();
        while (clean.StartsWith('(') && clean.EndsWith(')'))
        {
            clean = clean.Substring(1, clean.Length - 2).Trim();
        }

        if (string.IsNullOrEmpty(clean))
        {
            return true;
        }

        if (clean.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
        {
            return !EvaluatePart(clean.Substring(4), viewportWidth, viewportHeight, mediaType);
        }

        if (string.Equals(clean, "screen", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(mediaType, "screen", StringComparison.OrdinalIgnoreCase);
        }
        if (string.Equals(clean, "print", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(mediaType, "print", StringComparison.OrdinalIgnoreCase);
        }
        if (string.Equals(clean, "all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        int colonIdx = clean.IndexOf(':');
        if (colonIdx != -1)
        {
            string feature = clean.Substring(0, colonIdx).Trim().ToLowerInvariant();
            string valStr = clean.Substring(colonIdx + 1).Trim().ToLowerInvariant();

            if (feature == "min-width")
            {
                return viewportWidth >= ParseMediaLength(valStr);
            }
            if (feature == "max-width")
            {
                return viewportWidth <= ParseMediaLength(valStr);
            }
            if (feature == "min-height")
            {
                return viewportHeight >= ParseMediaLength(valStr);
            }
            if (feature == "max-height")
            {
                return viewportHeight <= ParseMediaLength(valStr);
            }
            if (feature == "orientation")
            {
                if (valStr == "portrait")
                {
                    return viewportHeight >= viewportWidth;
                }
                if (valStr == "landscape")
                {
                    return viewportWidth > viewportHeight;
                }
            }
        }

        return false;
    }

    private static float ParseMediaLength(string valStr)
    {
        valStr = valStr.Trim().ToLowerInvariant();
        if (valStr.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            valStr = valStr.Substring(0, valStr.Length - 2).Trim();
        }
        else if (valStr.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            valStr = valStr.Substring(0, valStr.Length - 2).Trim();
            if (float.TryParse(valStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float emVal))
            {
                return emVal * 16f;
            }
        }

        if (float.TryParse(valStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
        {
            return val;
        }
        return 0f;
    }
}
