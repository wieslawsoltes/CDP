using System.Collections.Generic;
using System.Linq;
using CDP.Html.Parser;
using CDP.Html.Renderer.Style;

namespace CDP.Html.Renderer.Layout;

public static class LayoutTreeBuilder
{
    public static LayoutBox Build(HtmlDocument doc, Dictionary<HtmlNode, ComputedStyle> styles)
    {
        var root = BuildBox(doc, styles);
        if (root == null)
        {
            // Fallback if document has display none
            return new LayoutBlockBox { Style = new ComputedStyle { Display = DisplayType.Block } };
        }
        return root;
    }

    private static LayoutBox? BuildBox(HtmlNode node, Dictionary<HtmlNode, ComputedStyle> styles)
    {
        if (!styles.TryGetValue(node, out var style) || style.Display == DisplayType.None)
        {
            return null;
        }

        LayoutBox box;

        if (node is HtmlDocument)
        {
            box = new LayoutBlockBox { Node = node, Style = style };
        }
        else if (node is HtmlTextNode textNode)
        {
            box = new LayoutTextBox { Node = node, Style = style, Text = textNode.Text };
        }
        else if (node is HtmlElement element)
        {
            box = style.Display switch
            {
                DisplayType.Block => new LayoutBlockBox { Node = node, Style = style },
                DisplayType.Flex => new LayoutFlexBox { Node = node, Style = style },
                DisplayType.Inline => new LayoutInlineBox { Node = node, Style = style },
                _ => new LayoutInlineBox { Node = node, Style = style } // Fallback
            };
        }
        else
        {
            return null;
        }

        // Build raw child boxes recursively
        var rawChildren = new List<LayoutBox>();
        foreach (var childNode in node.Children)
        {
            var childBox = BuildBox(childNode, styles);
            if (childBox != null)
            {
                rawChildren.Add(childBox);
            }
        }

        // Process children to insert anonymous block wrappers where needed
        var processedChildren = ProcessChildren(box, rawChildren);
        foreach (var child in processedChildren)
        {
            box.Children.Add(child);
        }

        return box;
    }

    private static List<LayoutBox> ProcessChildren(LayoutBox parent, List<LayoutBox> rawChildren)
    {
        if (rawChildren.Count == 0)
            return rawChildren;

        // Rule 1: If parent is a Flex Container, wrap every inline child in an anonymous block box (flex item)
        if (parent is LayoutFlexBox)
        {
            var processed = new List<LayoutBox>();
            foreach (var child in rawChildren)
            {
                if (child.IsInlineLevel)
                {
                    var anonBlock = new LayoutBlockBox
                    {
                        Style = new ComputedStyle { Display = DisplayType.Block }
                    };
                    anonBlock.AddChild(child);
                    anonBlock.Parent = parent;
                    processed.Add(anonBlock);
                }
                else
                {
                    child.Parent = parent;
                    processed.Add(child);
                }
            }
            return processed;
        }

        // Rule 2: If parent is a Block Container, check if it has a mixture of block and inline children.
        // If it has both, we wrap consecutive inline children in an anonymous block box.
        bool hasBlock = rawChildren.Any(c => c.IsBlockLevel);
        bool hasInline = rawChildren.Any(c => c.IsInlineLevel);

        if (hasBlock && hasInline)
        {
            var processed = new List<LayoutBox>();
            var currentInlineGroup = new List<LayoutBox>();

            void FlushInlineGroup()
            {
                if (currentInlineGroup.Count > 0)
                {
                    var anonBlock = new LayoutBlockBox
                    {
                        Style = new ComputedStyle { Display = DisplayType.Block }
                    };
                    foreach (var inlineBox in currentInlineGroup)
                    {
                        anonBlock.AddChild(inlineBox);
                    }
                    anonBlock.Parent = parent;
                    processed.Add(anonBlock);
                    currentInlineGroup.Clear();
                }
            }

            foreach (var child in rawChildren)
            {
                if (child.IsInlineLevel)
                {
                    currentInlineGroup.Add(child);
                }
                else
                {
                    FlushInlineGroup();
                    child.Parent = parent;
                    processed.Add(child);
                }
            }
            FlushInlineGroup();
            return processed;
        }

        // Otherwise, just set parent references and return
        foreach (var child in rawChildren)
        {
            child.Parent = parent;
        }
        return rawChildren;
    }
}
