using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using SkiaSharp;
using CDP.Html.Renderer.Style;

namespace CDP.Html.Renderer.Layout;

public static class LayoutEngine
{
    public static void Layout(LayoutBox box, float availableWidth, float availableHeight, BfcContext? bfcContext = null)
    {
        if (!box.NeedsLayout && box.LayoutCacheAvailableWidth == availableWidth && box.LayoutCacheAvailableHeight == availableHeight)
        {
            return;
        }

        // Cache parameters before calculation
        box.LayoutCacheAvailableWidth = availableWidth;
        box.LayoutCacheAvailableHeight = availableHeight;
        box.NeedsLayout = false;

        if (box is LayoutFlexBox flexBox)
        {
            LayoutFlexContainer(flexBox, availableWidth, availableHeight, bfcContext);
        }
        else if (box is LayoutBlockBox blockBox || box.Style.Position == PositionType.Absolute || box.Style.Position == PositionType.Fixed || box.Style.Float != FloatType.None)
        {
            LayoutBlockContainer(box, availableWidth, availableHeight, bfcContext);
        }
        else if (box is LayoutInlineBox inlineBox)
        {
            // Inline box geometry is solved by its parent during IFC
        }
        else if (box is LayoutTextBox textBox)
        {
            // Text box geometry is solved by its parent during IFC
        }

        if (box.Parent == null)
        {
            ResolvePositioning(box, box);
        }
    }

    private static void LayoutBlockContainer(LayoutBox box, float availableWidth, float availableHeight, BfcContext? bfcContext)
    {
        // 1. Resolve dimensions from style
        float resolvedWidth = box.Style.Width.Resolve(availableWidth);
        float resolvedHeight = box.Style.Height.Resolve(availableHeight);

        box.PaddingLeft = box.Style.PaddingLeft.Resolve(availableWidth);
        box.PaddingRight = box.Style.PaddingRight.Resolve(availableWidth);
        box.PaddingTop = box.Style.PaddingTop.Resolve(availableWidth);
        box.PaddingBottom = box.Style.PaddingBottom.Resolve(availableWidth);

        box.BorderLeft = box.Style.BorderLeftWidth;
        box.BorderRight = box.Style.BorderRightWidth;
        box.BorderTop = box.Style.BorderTopWidth;
        box.BorderBottom = box.Style.BorderBottomWidth;

        box.MarginLeft = box.Style.MarginLeft.Resolve(availableWidth);
        box.MarginRight = box.Style.MarginRight.Resolve(availableWidth);
        box.MarginTop = box.Style.MarginTop.Resolve(availableWidth);
        box.MarginBottom = box.Style.MarginBottom.Resolve(availableWidth);

        // Check if size is overridden by parent flex container
        bool widthIsOverridden = false;
        bool heightIsOverridden = false;

        if (box.Parent is LayoutFlexBox flexParent)
        {
            bool flexIsRow = flexParent.Style.FlexDirection == FlexDirection.Row || flexParent.Style.FlexDirection == FlexDirection.RowReverse;
            if (flexIsRow)
            {
                widthIsOverridden = true;
                if (flexParent.Style.AlignItems == AlignItems.Stretch && box.Style.Height.IsAuto)
                {
                    heightIsOverridden = true;
                }
            }
            else
            {
                heightIsOverridden = true;
                if (flexParent.Style.AlignItems == AlignItems.Stretch && box.Style.Width.IsAuto)
                {
                    widthIsOverridden = true;
                }
            }
        }

        if (widthIsOverridden)
        {
            box.Width = availableWidth;
        }
        else if (!box.Style.Width.IsAuto)
        {
            box.Width = resolvedWidth + box.PaddingLeft + box.PaddingRight + box.BorderLeft + box.BorderRight;
        }
        else
        {
            box.Width = availableWidth; // Default block width
        }

        float innerWidth = box.ContentWidth;

        // Establish BFC if box is root, float, absolute/fixed, or flex
        bool establishesNewBfc = box.Style.Float != FloatType.None ||
                                 box.Style.Position == PositionType.Absolute ||
                                 box.Style.Position == PositionType.Fixed ||
                                 box is LayoutFlexBox ||
                                 box.Parent == null;

        BfcContext activeBfc = establishesNewBfc ? new BfcContext(box) : (bfcContext ?? new BfcContext(box));

        // 2. Check if the block establishes an Inline Formatting Context (IFC)
        bool hasInlineChildren = box.Children.Any(c => c.IsInlineLevel);

        if (hasInlineChildren)
        {
            box.LineBoxes.Clear();

            using var paint = new SKPaint();
            var tokens = new List<WordToken>();
            CollectTokens(box, box, tokens, paint);

            float blockXContent = 0f;
            float blockYContent = 0f;
            if (activeBfc != null)
            {
                var pos = GetPositionRelativeToAncestor(box, activeBfc.BfcParent);
                blockXContent = pos.X + box.PaddingLeft + box.BorderLeft - (activeBfc.BfcParent.PaddingLeft + activeBfc.BfcParent.BorderLeft);
                blockYContent = pos.Y + box.PaddingTop + box.BorderTop - (activeBfc.BfcParent.PaddingTop + activeBfc.BfcParent.BorderTop);
            }

            var lines = BuildLineBoxes(tokens, innerWidth, paint, activeBfc, blockXContent, blockYContent);
            box.LineBoxes.AddRange(lines);

            float currentY = box.PaddingTop + box.BorderTop;
            foreach (var line in lines)
            {
                // Align line box fragments based on text-align within the available width
                float shift = 0f;
                float availWidth = line.AvailableWidth > 0 ? line.AvailableWidth : innerWidth;
                if (box.Style.TextAlign == TextAlignType.Center)
                {
                    shift = (availWidth - line.Width) / 2f;
                }
                else if (box.Style.TextAlign == TextAlignType.Right)
                {
                    shift = availWidth - line.Width;
                }

                if (shift > 0f)
                {
                    for (int i = 0; i < line.Fragments.Count; i++)
                    {
                        var frag = line.Fragments[i];
                        frag.X += shift;
                        line.Fragments[i] = frag;
                    }
                }

                for (int i = 0; i < line.Fragments.Count; i++)
                {
                    var frag = line.Fragments[i];
                    frag.X += box.PaddingLeft + box.BorderLeft;
                    frag.Y += currentY;

                    // If it's a child box, set its coordinates
                    if (frag.Box != box && frag.Box.Parent == box)
                    {
                        frag.Box.X = frag.X;
                        frag.Box.Y = frag.Y;
                        frag.Box.Width = frag.Width;
                        frag.Box.Height = frag.Height;
                    }
                    line.Fragments[i] = frag;
                }
                currentY += line.Height;
            }

            if (heightIsOverridden)
            {
                box.Height = availableHeight;
            }
            else if (box.Style.Height.IsAuto)
            {
                box.Height = currentY + box.PaddingBottom + box.BorderBottom;
            }
            else
            {
                box.Height = resolvedHeight + box.PaddingTop + box.PaddingBottom + box.BorderTop + box.BorderBottom;
            }
        }
        else
        {
            // Run BFC Layout (vertical stacking)
            float currentY = box.PaddingTop + box.BorderTop;
            float prevBottomMargin = 0f;

            foreach (var child in box.Children)
            {
                if (child.Style.Position == PositionType.Absolute || child.Style.Position == PositionType.Fixed)
                    continue;

                // 1. Resolve Clear
                if (child.Style.Clear != ClearType.None)
                {
                    float clearYContent = activeBfc.GetClearY(child.Style.Clear);
                    float clearY = clearYContent + box.PaddingTop + box.BorderTop;
                    if (currentY < clearY)
                    {
                        currentY = clearY;
                        prevBottomMargin = 0f;
                    }
                }

                float childMarginTop = child.Style.MarginTop.Resolve(innerWidth);
                child.MarginLeft = child.Style.MarginLeft.Resolve(innerWidth);
                child.MarginRight = child.Style.MarginRight.Resolve(innerWidth);
                child.MarginBottom = child.Style.MarginBottom.Resolve(innerWidth);

                if (child.Style.Float != FloatType.None)
                {
                    // Float Child
                    // 1. Lay out child to determine width/height
                    Layout(child, innerWidth, availableHeight - currentY, activeBfc);

                    // 2. Query available horizontal span
                    float childYContent = currentY - (box.PaddingTop + box.BorderTop);
                    var (availLeft, availRight) = activeBfc.GetAvailableHorizontalSpan(childYContent, child.Height, innerWidth);

                    // 3. Position float
                    float relX;
                    if (child.Style.Float == FloatType.Left)
                    {
                        relX = availLeft + child.MarginLeft;
                    }
                    else // FloatType.Right
                    {
                        relX = availRight - child.Width - child.MarginRight;
                    }

                    child.X = box.PaddingLeft + box.BorderLeft + relX;
                    child.Y = currentY;

                    // 4. Register float bounds in BfcContext
                    float relY = currentY - (box.PaddingTop + box.BorderTop);
                    activeBfc.RegisterFloat(child, relX, relY);

                    // Floats do NOT advance currentY, and do NOT affect prevBottomMargin
                }
                else
                {
                    // Static Child
                    float collapsedMargin = Math.Max(prevBottomMargin, childMarginTop);
                    currentY += collapsedMargin;

                    float childYContent = currentY - (box.PaddingTop + box.BorderTop);
                    var (availLeft, availRight) = activeBfc.GetAvailableHorizontalSpan(childYContent, 1f, innerWidth);
                    float childAvailableWidth = availRight - availLeft;

                    // Tentatively set coordinates before layout so nested elements can calculate offsets relative to BfcParent
                    child.X = box.PaddingLeft + box.BorderLeft + availLeft + child.MarginLeft;
                    child.Y = currentY;

                    Layout(child, childAvailableWidth, availableHeight - currentY, activeBfc);

                    // Re-query with actual child height to position correctly
                    var actualSpan = activeBfc.GetAvailableHorizontalSpan(childYContent, child.Height, innerWidth);
                    availLeft = actualSpan.Left;

                    child.X = box.PaddingLeft + box.BorderLeft + availLeft + child.MarginLeft;
                    child.Y = currentY;

                    currentY += child.Height;
                    prevBottomMargin = child.MarginBottom;
                }
            }

            if (heightIsOverridden)
            {
                box.Height = availableHeight;
            }
            else if (box.Style.Height.IsAuto)
            {
                box.Height = currentY + prevBottomMargin + box.PaddingBottom + box.BorderBottom;
            }
            else
            {
                box.Height = resolvedHeight + box.PaddingTop + box.PaddingBottom + box.BorderTop + box.BorderBottom;
            }
        }
    }

    private static void LayoutFlexContainer(LayoutFlexBox box, float availableWidth, float availableHeight, BfcContext? bfcContext)
    {
        // 1. Resolve container dimensions
        float resolvedWidth = box.Style.Width.Resolve(availableWidth);
        float resolvedHeight = box.Style.Height.Resolve(availableHeight);

        box.PaddingLeft = box.Style.PaddingLeft.Resolve(availableWidth);
        box.PaddingRight = box.Style.PaddingRight.Resolve(availableWidth);
        box.PaddingTop = box.Style.PaddingTop.Resolve(availableWidth);
        box.PaddingBottom = box.Style.PaddingBottom.Resolve(availableWidth);

        box.BorderLeft = box.Style.BorderLeftWidth;
        box.BorderRight = box.Style.BorderRightWidth;
        box.BorderTop = box.Style.BorderTopWidth;
        box.BorderBottom = box.Style.BorderBottomWidth;

        box.MarginLeft = box.Style.MarginLeft.Resolve(availableWidth);
        box.MarginRight = box.Style.MarginRight.Resolve(availableWidth);
        box.MarginTop = box.Style.MarginTop.Resolve(availableWidth);
        box.MarginBottom = box.Style.MarginBottom.Resolve(availableWidth);

        if (!box.Style.Width.IsAuto)
        {
            box.Width = resolvedWidth + box.PaddingLeft + box.PaddingRight + box.BorderLeft + box.BorderRight;
        }
        else
        {
            box.Width = availableWidth; // Default to fill container width
        }

        float innerWidth = box.ContentWidth;

        // Determine direction
        bool isRow = box.Style.FlexDirection == FlexDirection.Row || box.Style.FlexDirection == FlexDirection.RowReverse;

        // Collect flex items and initial sizes
        var flexItems = new List<FlexItem>();
        foreach (var child in box.Children)
        {
            if (child.Style.Position == PositionType.Absolute || child.Style.Position == PositionType.Fixed)
                continue;

            child.PaddingLeft = child.Style.PaddingLeft.Resolve(innerWidth);
            child.PaddingRight = child.Style.PaddingRight.Resolve(innerWidth);
            child.PaddingTop = child.Style.PaddingTop.Resolve(innerWidth);
            child.PaddingBottom = child.Style.PaddingBottom.Resolve(innerWidth);

            child.BorderLeft = child.Style.BorderLeftWidth;
            child.BorderRight = child.Style.BorderRightWidth;
            child.BorderTop = child.Style.BorderTopWidth;
            child.BorderBottom = child.Style.BorderBottomWidth;

            child.MarginLeft = child.Style.MarginLeft.Resolve(innerWidth);
            child.MarginRight = child.Style.MarginRight.Resolve(innerWidth);
            child.MarginTop = child.Style.MarginTop.Resolve(innerWidth);
            child.MarginBottom = child.Style.MarginBottom.Resolve(innerWidth);

            float baseMain = 0f;
            float baseCross = 0f;

            if (isRow)
            {
                if (!child.Style.Width.IsAuto)
                    baseMain = child.Style.Width.Resolve(innerWidth) + child.PaddingLeft + child.PaddingRight + child.BorderLeft + child.BorderRight;
                else if (child.Style.FlexBasis.IsPx)
                    baseMain = child.Style.FlexBasis.Value + child.PaddingLeft + child.PaddingRight + child.BorderLeft + child.BorderRight;
                else
                {
                    Layout(child, innerWidth, float.PositiveInfinity);
                    baseMain = child.Width;
                }

                if (!child.Style.Height.IsAuto)
                    baseCross = child.Style.Height.Resolve(availableHeight) + child.PaddingTop + child.PaddingBottom + child.BorderTop + child.BorderBottom;
                else
                {
                    Layout(child, innerWidth, float.PositiveInfinity);
                    baseCross = child.Height;
                }
            }
            else
            {
                if (!child.Style.Height.IsAuto)
                    baseMain = child.Style.Height.Resolve(availableHeight) + child.PaddingTop + child.PaddingBottom + child.BorderTop + child.BorderBottom;
                else if (child.Style.FlexBasis.IsPx)
                    baseMain = child.Style.FlexBasis.Value + child.PaddingTop + child.PaddingBottom + child.BorderTop + child.BorderBottom;
                else
                {
                    Layout(child, innerWidth, float.PositiveInfinity);
                    baseMain = child.Height;
                }

                if (!child.Style.Width.IsAuto)
                    baseCross = child.Style.Width.Resolve(innerWidth) + child.PaddingLeft + child.PaddingRight + child.BorderLeft + child.BorderRight;
                else
                {
                    Layout(child, innerWidth, float.PositiveInfinity);
                    baseCross = child.Width;
                }
            }

            flexItems.Add(new FlexItem
            {
                Box = child,
                HypotheticalMainSize = baseMain,
                HypotheticalCrossSize = baseCross,
                MainSize = baseMain,
                CrossSize = baseCross
            });
        }

        // Collect into Flex Lines
        var flexLines = new List<FlexLine>();
        var currentLine = new FlexLine();
        float containerMainLimit = isRow ? innerWidth : (box.Style.Height.IsAuto ? float.PositiveInfinity : box.ContentHeight);

        foreach (var item in flexItems)
        {
            float outerMain = item.HypotheticalMainSize + (isRow ? item.Box.MarginLeft + item.Box.MarginRight : item.Box.MarginTop + item.Box.MarginBottom);

            if (box.Style.FlexWrap == FlexWrap.Wrap && currentLine.Items.Count > 0 && currentLine.MainSize + outerMain > containerMainLimit)
            {
                flexLines.Add(currentLine);
                currentLine = new FlexLine();
            }

            currentLine.Items.Add(item);
            currentLine.MainSize += outerMain;
        }
        if (currentLine.Items.Count > 0)
        {
            flexLines.Add(currentLine);
        }

        // Resolve Main-Axis Sizes
        foreach (var line in flexLines)
        {
            float targetMainSize = containerMainLimit;
            if (float.IsInfinity(targetMainSize))
            {
                targetMainSize = line.Items.Sum(item => item.HypotheticalMainSize + (isRow ? item.Box.MarginLeft + item.Box.MarginRight : item.Box.MarginTop + item.Box.MarginBottom));
            }

            float totalHypothetical = line.Items.Sum(item => item.HypotheticalMainSize);
            float totalMargins = line.Items.Sum(item => isRow ? item.Box.MarginLeft + item.Box.MarginRight : item.Box.MarginTop + item.Box.MarginBottom);
            float freeSpace = targetMainSize - totalHypothetical - totalMargins;

            if (freeSpace > 0f)
            {
                float totalGrow = line.Items.Sum(item => item.Box.Style.FlexGrow);
                if (totalGrow > 0f)
                {
                    for (int i = 0; i < line.Items.Count; i++)
                    {
                        var item = line.Items[i];
                        float growShare = (item.Box.Style.FlexGrow / totalGrow) * freeSpace;
                        item.MainSize = item.HypotheticalMainSize + growShare;
                        line.Items[i] = item;
                    }
                }
            }
            else if (freeSpace < 0f)
            {
                float totalShrinkFactor = line.Items.Sum(item => item.Box.Style.FlexShrink * item.HypotheticalMainSize);
                if (totalShrinkFactor > 0f)
                {
                    for (int i = 0; i < line.Items.Count; i++)
                    {
                        var item = line.Items[i];
                        float shrinkFactor = item.Box.Style.FlexShrink * item.HypotheticalMainSize;
                        float shrinkShare = (shrinkFactor / totalShrinkFactor) * freeSpace;
                        item.MainSize = Math.Max(0f, item.HypotheticalMainSize + shrinkShare);
                        line.Items[i] = item;
                    }
                }
            }

            foreach (var item in line.Items)
            {
                if (isRow)
                {
                    item.Box.Width = item.MainSize;
                }
                else
                {
                    item.Box.Height = item.MainSize;
                }
            }
        }

        // Lay out items inside lines and compute line cross sizes
        foreach (var line in flexLines)
        {
            float maxCross = 0f;
            for (int i = 0; i < line.Items.Count; i++)
            {
                var item = line.Items[i];
                if (isRow)
                {
                    Layout(item.Box, item.MainSize, float.PositiveInfinity);
                    item.CrossSize = item.Box.Height;
                }
                else
                {
                    Layout(item.Box, availableWidth, item.MainSize);
                    item.CrossSize = item.Box.Width;
                }

                float outerCross = item.CrossSize + (isRow ? item.Box.MarginTop + item.Box.MarginBottom : item.Box.MarginLeft + item.Box.MarginRight);
                if (outerCross > maxCross)
                {
                    maxCross = outerCross;
                }
                line.Items[i] = item;
            }
            line.CrossSize = maxCross;
        }

        // Resolve Cross-Axis Sizes (AlignItems stretch)
        foreach (var line in flexLines)
        {
            for (int i = 0; i < line.Items.Count; i++)
            {
                var item = line.Items[i];
                var align = box.Style.AlignItems;
                if (align == AlignItems.Stretch)
                {
                    float innerCrossSpace = line.CrossSize - (isRow ? item.Box.MarginTop + item.Box.MarginBottom : item.Box.MarginLeft + item.Box.MarginRight);
                    if (isRow)
                    {
                        if (item.Box.Style.Height.IsAuto)
                        {
                            item.Box.Height = innerCrossSpace;
                            Layout(item.Box, item.Box.Width, item.Box.Height);
                            item.CrossSize = item.Box.Height;
                        }
                    }
                    else
                    {
                        if (item.Box.Style.Width.IsAuto)
                        {
                            item.Box.Width = innerCrossSpace;
                            Layout(item.Box, item.Box.Width, item.Box.Height);
                            item.CrossSize = item.Box.Width;
                        }
                    }
                }
                line.Items[i] = item;
            }
        }

        // Position items
        float currentCrossOffset = isRow ? box.PaddingTop + box.BorderTop : box.PaddingLeft + box.BorderLeft;

        foreach (var line in flexLines)
        {
            float lineMainSize = line.Items.Sum(item => item.MainSize + (isRow ? item.Box.MarginLeft + item.Box.MarginRight : item.Box.MarginTop + item.Box.MarginBottom));
            float containerInnerMain = isRow ? innerWidth : box.ContentHeight;
            if (float.IsInfinity(containerInnerMain) || box.Style.Height.IsAuto && !isRow)
            {
                containerInnerMain = lineMainSize;
            }

            float freeSpace = containerInnerMain - lineMainSize;
            float spacing = 0f;
            float startOffset = 0f;

            switch (box.Style.JustifyContent)
            {
                case JustifyContent.FlexEnd:
                    startOffset = freeSpace;
                    break;
                case JustifyContent.Center:
                    startOffset = freeSpace / 2f;
                    break;
                case JustifyContent.SpaceBetween:
                    if (line.Items.Count > 1)
                    {
                        spacing = freeSpace / (line.Items.Count - 1);
                    }
                    break;
                case JustifyContent.SpaceAround:
                    if (line.Items.Count > 0)
                    {
                        spacing = freeSpace / line.Items.Count;
                        startOffset = spacing / 2f;
                    }
                    break;
                case JustifyContent.SpaceEvenly:
                    if (line.Items.Count > 0)
                    {
                        spacing = freeSpace / (line.Items.Count + 1);
                        startOffset = spacing;
                    }
                    break;
            }

            float currentMainOffset = isRow ? box.PaddingLeft + box.BorderLeft + startOffset : box.PaddingTop + box.BorderTop + startOffset;

            foreach (var item in line.Items)
            {
                float itemMarginMainStart = isRow ? item.Box.MarginLeft : item.Box.MarginTop;
                float itemMarginMainEnd = isRow ? item.Box.MarginRight : item.Box.MarginBottom;

                float itemMarginCrossStart = isRow ? item.Box.MarginTop : item.Box.MarginLeft;
                float itemMarginCrossEnd = isRow ? item.Box.MarginBottom : item.Box.MarginRight;

                float crossOffset = 0f;
                float itemOuterCross = item.CrossSize + itemMarginCrossStart + itemMarginCrossEnd;
                float crossFreeSpace = line.CrossSize - itemOuterCross;

                switch (box.Style.AlignItems)
                {
                    case AlignItems.FlexEnd:
                        crossOffset = crossFreeSpace;
                        break;
                    case AlignItems.Center:
                        crossOffset = crossFreeSpace / 2f;
                        break;
                }

                if (isRow)
                {
                    item.Box.X = currentMainOffset + itemMarginMainStart;
                    item.Box.Y = currentCrossOffset + crossOffset + itemMarginCrossStart;
                    currentMainOffset += item.MainSize + itemMarginMainStart + itemMarginMainEnd + spacing;
                }
                else
                {
                    item.Box.X = currentCrossOffset + crossOffset + itemMarginCrossStart;
                    item.Box.Y = currentMainOffset + itemMarginMainStart;
                    currentMainOffset += item.MainSize + itemMarginMainStart + itemMarginMainEnd + spacing;
                }
            }

            currentCrossOffset += line.CrossSize;
        }

        if (box.Style.Height.IsAuto)
        {
            if (isRow)
            {
                box.Height = currentCrossOffset + box.PaddingBottom + box.BorderBottom;
            }
            else
            {
                float maxColumnHeight = 0f;
                foreach (var child in box.Children)
                {
                    if (child.Style.Position == PositionType.Absolute || child.Style.Position == PositionType.Fixed)
                        continue;

                    float bottomExtent = child.Y + child.Height + child.MarginBottom;
                    if (bottomExtent > maxColumnHeight)
                    {
                        maxColumnHeight = bottomExtent;
                    }
                }
                box.Height = maxColumnHeight + box.PaddingBottom + box.BorderBottom;
            }
        }
        else
        {
            box.Height = resolvedHeight + box.PaddingTop + box.PaddingBottom + box.BorderTop + box.BorderBottom;
        }
    }

    private static void CollectTokens(LayoutBox container, LayoutBox box, List<WordToken> tokens, SKPaint paint)
    {
        if (box is LayoutTextBox textBox)
        {
            string text = textBox.Text;
            int i = 0;
            int len = text.Length;
            while (i < len)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    int start = i;
                    while (i < len && char.IsWhiteSpace(text[i])) i++;
                    tokens.Add(CreateToken(textBox, " ", true, paint));
                }
                else
                {
                    int start = i;
                    while (i < len && !char.IsWhiteSpace(text[i])) i++;
                    tokens.Add(CreateToken(textBox, text.Substring(start, i - start), false, paint));
                }
            }
        }
        else
        {
            float leftSpace = box.MarginLeft + box.PaddingLeft + box.BorderLeft;
            if (leftSpace > 0 && box != container) // Don't add margins/padding of container to the tokens list
            {
                tokens.Add(new WordToken
                {
                    Box = box,
                    Text = "",
                    IsWhitespace = false,
                    Width = leftSpace,
                    Height = 0f,
                    BaselineOffset = 0f
                });
            }

            foreach (var child in box.Children)
            {
                if (child.Style.Position == PositionType.Absolute || child.Style.Position == PositionType.Fixed)
                    continue;

                CollectTokens(container, child, tokens, paint);
            }

            float rightSpace = box.MarginRight + box.PaddingRight + box.BorderRight;
            if (rightSpace > 0 && box != container)
            {
                tokens.Add(new WordToken
                {
                    Box = box,
                    Text = "",
                    IsWhitespace = false,
                    Width = rightSpace,
                    Height = 0f,
                    BaselineOffset = 0f
                });
            }
        }
    }

    private static WordToken CreateToken(LayoutBox box, string text, bool isWhitespace, SKPaint paint)
    {
        var style = box.Style;
        paint.Typeface = SKTypeface.FromFamilyName(style.FontFamily, style.FontWeight, SKFontStyleWidth.Normal, style.FontStyle);
        paint.TextSize = style.FontSize;

        float width = paint.MeasureText(text);
        paint.GetFontMetrics(out var metrics);
        float height = metrics.Descent - metrics.Ascent;
        float baselineOffset = -metrics.Ascent;

        if (style.LineHeight.HasValue)
        {
            float lh = style.LineHeight.Value;
            float leading = (lh - height) / 2f;
            baselineOffset += leading;
            height = lh;
        }
        else
        {
            float lh = style.FontSize * 1.2f;
            float leading = (lh - height) / 2f;
            baselineOffset += leading;
            height = lh;
        }

        return new WordToken
        {
            Box = box,
            Text = text,
            IsWhitespace = isWhitespace,
            Width = width,
            Height = height,
            BaselineOffset = baselineOffset
        };
    }

    private static List<LineBox> BuildLineBoxes(
        List<WordToken> tokens,
        float innerWidth,
        SKPaint paint,
        BfcContext? bfcContext,
        float blockXContent,
        float blockYContent)
    {
        var lines = new List<LineBox>();
        if (tokens.Count == 0) return lines;

        var currentLine = new LineBox();
        float currentLineYContent = blockYContent;

        // Helper to query available span for the current line
        (float LineLeft, float LineRight, float LineWidth) QuerySpan(float estHeight)
        {
            if (bfcContext == null)
            {
                return (0f, innerWidth, innerWidth);
            }
            float parentWidth = bfcContext.BfcParent.ContentWidth;
            var span = bfcContext.GetAvailableHorizontalSpan(currentLineYContent, estHeight, parentWidth);
            float lineLeft = Math.Max(0f, span.Left - blockXContent);
            float lineRight = Math.Min(innerWidth, span.Right - blockXContent);
            return (lineLeft, lineRight, Math.Max(0f, lineRight - lineLeft));
        }

        // Initialize first line's span
        float firstTokenHeight = tokens[0].Height > 0 ? tokens[0].Height : 16f;
        var (lineLeft, lineRight, lineAvailableWidth) = QuerySpan(firstTokenHeight);
        float currentX = lineLeft;

        currentLine.LineLeft = lineLeft;
        currentLine.AvailableWidth = lineAvailableWidth;

        foreach (var token in tokens)
        {
            if (token.IsWhitespace && currentLine.Fragments.Count == 0)
            {
                continue;
            }

            // Estimate height with the token if line is empty or use current line's height
            float currentLineEstHeight = currentLine.Fragments.Count > 0
                ? Math.Max(token.Height, currentLine.Fragments.Max(f => f.Height))
                : token.Height;

            // Re-query span if needed
            var span = QuerySpan(currentLineEstHeight);
            lineLeft = span.LineLeft;
            lineRight = span.LineRight;
            lineAvailableWidth = span.LineWidth;

            if (currentLine.Fragments.Count == 0)
            {
                currentX = lineLeft;
                currentLine.LineLeft = lineLeft;
                currentLine.AvailableWidth = lineAvailableWidth;
            }

            if (currentX + token.Width <= lineRight || currentLine.Fragments.Count == 0)
            {
                if (currentX + token.Width > lineRight && !token.IsWhitespace)
                {
                    BreakAndAddToken(ref currentLine, token, ref currentX, ref currentLineYContent, blockXContent, innerWidth, bfcContext, paint, lines);
                }
                else
                {
                    AddTokenToLine(currentLine, token, ref currentX);
                }
            }
            else
            {
                // Flush line
                FlushLine(currentLine, lines);
                currentLineYContent += currentLine.Height;

                currentLine = new LineBox();

                // Query span for the new line
                span = QuerySpan(token.Height > 0 ? token.Height : 16f);
                lineLeft = span.LineLeft;
                lineRight = span.LineRight;
                lineAvailableWidth = span.LineWidth;
                currentX = lineLeft;

                currentLine.LineLeft = lineLeft;
                currentLine.AvailableWidth = lineAvailableWidth;

                if (token.IsWhitespace)
                {
                    continue;
                }

                if (currentX + token.Width > lineRight)
                {
                    BreakAndAddToken(ref currentLine, token, ref currentX, ref currentLineYContent, blockXContent, innerWidth, bfcContext, paint, lines);
                }
                else
                {
                    AddTokenToLine(currentLine, token, ref currentX);
                }
            }
        }

        FlushLine(currentLine, lines);
        return lines;
    }

    private static void AddTokenToLine(LineBox line, WordToken token, ref float currentX)
    {
        var fragment = new LineFragment
        {
            Box = token.Box,
            Text = token.Text,
            Width = token.Width,
            Height = token.Height,
            BaselineOffset = token.BaselineOffset,
            X = currentX
        };
        line.Fragments.Add(fragment);
        currentX += token.Width;
    }

    private static void BreakAndAddToken(
        ref LineBox currentLine,
        WordToken token,
        ref float currentX,
        ref float currentLineYContent,
        float blockXContent,
        float innerWidth,
        BfcContext? bfcContext,
        SKPaint paint,
        List<LineBox> lines)
    {
        string word = token.Text ?? "";
        var style = token.Box.Style;
        paint.Typeface = SKTypeface.FromFamilyName(style.FontFamily, style.FontWeight, SKFontStyleWidth.Normal, style.FontStyle);
        paint.TextSize = style.FontSize;

        paint.GetFontMetrics(out var metrics);
        float height = metrics.Descent - metrics.Ascent;
        float baselineOffset = -metrics.Ascent;

        if (style.LineHeight.HasValue)
        {
            float lh = style.LineHeight.Value;
            float leading = (lh - height) / 2f;
            baselineOffset += leading;
            height = lh;
        }
        else
        {
            float lh = style.FontSize * 1.2f;
            float leading = (lh - height) / 2f;
            baselineOffset += leading;
            height = lh;
        }

        // Helper inside BreakAndAddToken
        (float left, float right) GetSpan(float y)
        {
            if (bfcContext == null) return (0f, innerWidth);
            float parentWidth = bfcContext.BfcParent.ContentWidth;
            var span = bfcContext.GetAvailableHorizontalSpan(y, height, parentWidth);
            return (Math.Max(0f, span.Left - blockXContent), Math.Min(innerWidth, span.Right - blockXContent));
        }

        var (lineLeft, lineRight) = GetSpan(currentLineYContent);

        int start = 0;
        while (start < word.Length)
        {
            int count = 1;
            while (start + count < word.Length && currentX + paint.MeasureText(word.Substring(start, count + 1)) <= lineRight)
            {
                count++;
            }

            string subWord = word.Substring(start, count);
            float subWidth = paint.MeasureText(subWord);

            var fragment = new LineFragment
            {
                Box = token.Box,
                Text = subWord,
                Width = subWidth,
                Height = height,
                BaselineOffset = baselineOffset,
                X = currentX
            };
            currentLine.Fragments.Add(fragment);
            currentX += subWidth;

            start += count;

            if (start < word.Length)
            {
                FlushLine(currentLine, lines);
                currentLineYContent += currentLine.Height;
                currentLine = new LineBox();

                // Get span for new line
                var nextSpan = GetSpan(currentLineYContent);
                lineLeft = nextSpan.left;
                lineRight = nextSpan.right;
                currentX = lineLeft;

                currentLine.LineLeft = lineLeft;
                currentLine.AvailableWidth = Math.Max(0f, lineRight - lineLeft);
            }
        }
    }

    private static void FlushLine(LineBox line, List<LineBox> lines)
    {
        if (line.Fragments.Count == 0) return;

        float maxHeight = 0f;
        float maxBaseline = 0f;
        float width = 0f;

        foreach (var frag in line.Fragments)
        {
            if (frag.Height > maxHeight) maxHeight = frag.Height;
            if (frag.BaselineOffset > maxBaseline) maxBaseline = frag.BaselineOffset;
            width += frag.Width;
        }

        line.Height = maxHeight;
        line.Baseline = maxBaseline;
        line.Width = width;

        for (int i = 0; i < line.Fragments.Count; i++)
        {
            var frag = line.Fragments[i];
            frag.Y = line.Baseline - frag.BaselineOffset;
            line.Fragments[i] = frag;
        }

        lines.Add(line);
    }

    private struct WordToken
    {
        public LayoutBox Box { get; set; }
        public string? Text { get; set; }
        public bool IsWhitespace { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float BaselineOffset { get; set; }
    }

    private struct FlexItem
    {
        public LayoutBox Box { get; set; }
        public float HypotheticalMainSize { get; set; }
        public float HypotheticalCrossSize { get; set; }
        public float MainSize { get; set; }
        public float CrossSize { get; set; }
    }

    private class FlexLine
    {
        public List<FlexItem> Items { get; } = new();
        public float MainSize { get; set; }
        public float CrossSize { get; set; }
    }

    private static void ResolvePositioning(LayoutBox box, LayoutBox rootBox)
    {
        if (box.Style.Position == PositionType.Absolute || box.Style.Position == PositionType.Fixed)
        {
            var containingBlock = FindContainingBlock(box, rootBox);

            Layout(box, containingBlock.ContentWidth, containingBlock.ContentHeight);

            float cbWidth = containingBlock.ContentWidth;
            float cbHeight = containingBlock.ContentHeight;

            float x_cb = containingBlock.PaddingLeft + containingBlock.BorderLeft;
            if (!box.Style.Left.IsAuto)
            {
                x_cb = containingBlock.PaddingLeft + containingBlock.BorderLeft + box.Style.Left.Resolve(cbWidth);
            }
            else if (!box.Style.Right.IsAuto)
            {
                x_cb = containingBlock.Width - containingBlock.PaddingRight - containingBlock.BorderRight - box.Width - box.Style.Right.Resolve(cbWidth);
            }

            float y_cb = containingBlock.PaddingTop + containingBlock.BorderTop;
            if (!box.Style.Top.IsAuto)
            {
                y_cb = containingBlock.PaddingTop + containingBlock.BorderTop + box.Style.Top.Resolve(cbHeight);
            }
            else if (!box.Style.Bottom.IsAuto)
            {
                y_cb = containingBlock.Height - containingBlock.PaddingBottom - containingBlock.BorderBottom - box.Height - box.Style.Bottom.Resolve(cbHeight);
            }

            var current = box.Parent;
            float accumX = 0f;
            float accumY = 0f;
            while (current != null && current != containingBlock)
            {
                accumX += current.X;
                accumY += current.Y;
                current = current.Parent;
            }

            box.X = x_cb - accumX;
            box.Y = y_cb - accumY;
        }
        else if (box.Style.Position == PositionType.Relative)
        {
            float parentWidth = box.Parent?.ContentWidth ?? 0f;
            float parentHeight = box.Parent?.ContentHeight ?? 0f;

            if (!box.Style.Left.IsAuto)
            {
                box.X += box.Style.Left.Resolve(parentWidth);
            }
            else if (!box.Style.Right.IsAuto)
            {
                box.X -= box.Style.Right.Resolve(parentWidth);
            }

            if (!box.Style.Top.IsAuto)
            {
                box.Y += box.Style.Top.Resolve(parentHeight);
            }
            else if (!box.Style.Bottom.IsAuto)
            {
                box.Y -= box.Style.Bottom.Resolve(parentHeight);
            }
        }

        for (int i = 0; i < box.Children.Count; i++)
        {
            ResolvePositioning(box.Children[i], rootBox);
        }
    }

    private static LayoutBox FindContainingBlock(LayoutBox box, LayoutBox rootBox)
    {
        if (box.Style.Position == PositionType.Fixed)
        {
            return rootBox;
        }

        var parent = box.Parent;
        while (parent != null)
        {
            if (parent.Style.Position != PositionType.Static)
            {
                return parent;
            }
            parent = parent.Parent;
        }
        return rootBox;
    }

    public static (float X, float Y) GetPositionRelativeToAncestor(LayoutBox box, LayoutBox ancestor)
    {
        float x = 0f;
        float y = 0f;
        var curr = box;
        while (curr != null && curr != ancestor)
        {
            x += curr.X;
            y += curr.Y;
            curr = curr.Parent;
        }
        return (x, y);
    }
}
