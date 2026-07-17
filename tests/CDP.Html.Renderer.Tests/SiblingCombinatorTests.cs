using Xunit;
using CDP.Html.Parser;
using CDP.Css.Parser;
using CDP.Html.Renderer.Style;
using System;
using System.Collections.Generic;

namespace CDP.Html.Renderer.Tests;

public class SiblingCombinatorTests
{
    private HtmlElement CreateElement(string tagName, string? id = null, string? className = null)
    {
        var el = new HtmlElement { TagName = tagName };
        if (id != null) el.Attributes["id"] = id;
        if (className != null) el.Attributes["class"] = className;
        return el;
    }

    private void LinkParentChild(HtmlNode parent, HtmlNode child)
    {
        parent.Children.Add(child);
        child.Parent = parent;
    }

    [Fact]
    public void TestAdjacentSiblingCombinator_BasicAndIndexZero()
    {
        // Setup: parent -> [ div1, span2, p3 ]
        var parent = CreateElement("body");
        var div1 = CreateElement("div", id: "div1");
        var span2 = CreateElement("span", id: "span2");
        var p3 = CreateElement("p", id: "p3");

        LinkParentChild(parent, div1);
        LinkParentChild(parent, span2);
        LinkParentChild(parent, p3);

        // 1. Basic adjacent match: div + span -> matches span2
        var sel1 = CssParser.ParseSelector("div + span");
        Assert.True(StyleCascade.Matches(sel1, span2));

        // 2. Div1 is at index 0, so it has no preceding sibling. "div" as a selector shouldn't match anything preceding it.
        var sel2 = CssParser.ParseSelector("p + div");
        Assert.False(StyleCascade.Matches(sel2, div1));

        // 3. Test that index 0 element does not match any sibling combinator targeting preceding siblings.
        var sel3 = CssParser.ParseSelector("span + div");
        Assert.False(StyleCascade.Matches(sel3, div1));
        var sel4 = CssParser.ParseSelector("span ~ div");
        Assert.False(StyleCascade.Matches(sel4, div1));
    }

    [Fact]
    public void TestGeneralSiblingCombinator_Basic()
    {
        // Setup: parent -> [ div1, span2, p3, span4 ]
        var parent = CreateElement("body");
        var div1 = CreateElement("div", id: "div1");
        var span2 = CreateElement("span", id: "span2");
        var p3 = CreateElement("p", id: "p3");
        var span4 = CreateElement("span", id: "span4");

        LinkParentChild(parent, div1);
        LinkParentChild(parent, span2);
        LinkParentChild(parent, p3);
        LinkParentChild(parent, span4);

        // div ~ span should match both span2 and span4
        var sel = CssParser.ParseSelector("div ~ span");
        Assert.True(StyleCascade.Matches(sel, span2));
        Assert.True(StyleCascade.Matches(sel, span4));

        // p ~ span should match span4, but NOT span2
        var sel2 = CssParser.ParseSelector("p ~ span");
        Assert.True(StyleCascade.Matches(sel2, span4));
        Assert.False(StyleCascade.Matches(sel2, span2));

        // span ~ p should match p3 (since span2 precedes it)
        var sel3 = CssParser.ParseSelector("span ~ p");
        Assert.True(StyleCascade.Matches(sel3, p3));
    }

    [Fact]
    public void TestChainedCombinators()
    {
        // Setup: parent -> [ div1, span2, p3, section4 ]
        var parent = CreateElement("body");
        var div1 = CreateElement("div");
        var span2 = CreateElement("span");
        var p3 = CreateElement("p");
        var section4 = CreateElement("section");

        LinkParentChild(parent, div1);
        LinkParentChild(parent, span2);
        LinkParentChild(parent, p3);
        LinkParentChild(parent, section4);

        // Chain 1: div + span + p ~ section -> matches section4
        // (div + span matches span2; span2 + p matches p3; p3 ~ section matches section4)
        var sel1 = CssParser.ParseSelector("div + span + p ~ section");
        Assert.True(StyleCascade.Matches(sel1, section4));

        // Chain 2: div + span + p + section -> matches section4
        var sel2 = CssParser.ParseSelector("div + span + p + section");
        Assert.True(StyleCascade.Matches(sel2, section4));

        // Chain 3 (broken chain): div + p + section
        // (div + p would require p to be immediately after div, which is false since span2 is in between)
        var sel3 = CssParser.ParseSelector("div + p + section");
        Assert.False(StyleCascade.Matches(sel3, section4));
    }

    [Fact]
    public void TestExtremeDomTree_HundredsOfSiblings()
    {
        // Construct a parent with 500 children.
        // Elements: 499 divs followed by 1 span at the end.
        var parent = CreateElement("body");
        var divs = new List<HtmlElement>();
        for (int i = 0; i < 499; i++)
        {
            var d = CreateElement("div", className: "sibling-div");
            divs.Add(d);
            LinkParentChild(parent, d);
        }
        var lastSpan = CreateElement("span", id: "last-span");
        LinkParentChild(parent, lastSpan);

        // 1. Check adjacent sibling combinator matching the last element: div + span
        var sel1 = CssParser.ParseSelector("div + span");
        Assert.True(StyleCascade.Matches(sel1, lastSpan));

        // 2. Check general sibling combinator: div ~ span
        var sel2 = CssParser.ParseSelector("div ~ span");
        Assert.True(StyleCascade.Matches(sel2, lastSpan));

        // 3. Chain of 5 general siblings: div ~ div ~ div ~ div ~ span
        var sel3 = CssParser.ParseSelector("div ~ div ~ div ~ div ~ span");
        Assert.True(StyleCascade.Matches(sel3, lastSpan));

        // 4. Performance verification: run matching 1000 times to ensure no exponential slowdown.
        for (int i = 0; i < 1000; i++)
        {
            bool match = StyleCascade.Matches(sel2, lastSpan);
            Assert.True(match);
        }
    }

    [Fact]
    public void TestCombinatorsWithPseudoClasses()
    {
        // Setup: parent -> [ div1, span2, p3, span4 ]
        var parent = CreateElement("body");
        var div1 = CreateElement("div");
        var span2 = CreateElement("span");
        var p3 = CreateElement("p");
        var span4 = CreateElement("span");

        LinkParentChild(parent, div1);
        LinkParentChild(parent, span2);
        LinkParentChild(parent, p3);
        LinkParentChild(parent, span4);

        // 1. div:first-child + span:nth-child(2) -> matches span2
        var sel1 = CssParser.ParseSelector("div:first-child + span:nth-child(2)");
        Assert.True(StyleCascade.Matches(sel1, span2));

        // 2. div:first-child ~ span:nth-last-child(1) -> matches span4
        var sel2 = CssParser.ParseSelector("div:first-child ~ span:nth-last-child(1)");
        Assert.True(StyleCascade.Matches(sel2, span4));

        // 3. div:last-child + span (div is not last child, so it should fail)
        var sel3 = CssParser.ParseSelector("div:last-child + span");
        Assert.False(StyleCascade.Matches(sel3, span2));
    }

    [Fact]
    public void TestRobustnessAndInvalidInputs()
    {
        // 1. Disconnected element (no parent)
        var orphan = CreateElement("div");
        var sel = CssParser.ParseSelector("div + span");
        // Should not crash and should return false
        Assert.False(StyleCascade.Matches(sel, orphan));

        // 2. Element parent is not an HtmlElement (e.g. parent is HtmlDocument)
        var doc = new HtmlDocument();
        var rootDiv = CreateElement("div");
        LinkParentChild(doc, rootDiv);
        var sel2 = CssParser.ParseSelector("div ~ div");
        // Should not crash and should return false
        Assert.False(StyleCascade.Matches(sel2, rootDiv));

        // 3. Malformed selector (ParentSelector is null but combinator is '+')
        var badSelector = new CssSelector
        {
            TagName = "span",
            Combinator = "+",
            ParentSelector = null
        };
        var parent = CreateElement("body");
        var div = CreateElement("div");
        var span = CreateElement("span");
        LinkParentChild(parent, div);
        LinkParentChild(parent, span);

        // Should evaluate without crashing (ignoring ParentSelector requirement since it's null)
        Assert.True(StyleCascade.Matches(badSelector, span));

        // 4. Element parent Children does not contain the element itself (inconsistent tree state)
        var parent2 = CreateElement("body");
        var div2 = CreateElement("div");
        var span2 = CreateElement("span");
        // We set Parent link but do NOT add to Children list
        span2.Parent = parent2;
        // Should return false cleanly and not crash (IndexOutOfRangeException / ArgumentOutOfRangeException)
        Assert.False(StyleCascade.Matches(sel, span2));
    }
}
