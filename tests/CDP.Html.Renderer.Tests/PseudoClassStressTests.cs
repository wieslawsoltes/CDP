using Xunit;
using CDP.Html.Parser;
using CDP.Css.Parser;
using CDP.Html.Renderer.Style;
using System;
using System.Collections.Generic;

namespace CDP.Html.Renderer.Tests;

public class PseudoClassStressTests
{
    [Theory]
    [InlineData("2147483647", 0, 2147483647, true)]
    [InlineData("2147483648", 0, 0, false)]
    [InlineData("-2147483648", 0, -2147483648, true)]
    [InlineData("-2147483649", 0, 0, false)]
    [InlineData("2147483647n+1", 2147483647, 1, true)]
    [InlineData("2147483648n+1", 0, 0, false)]
    [InlineData("2n+2147483647", 2, 2147483647, true)]
    [InlineData("2n+2147483648", 0, 0, false)]
    [InlineData("-2147483648n-1", -2147483648, -1, true)]
    [InlineData("-2147483649n-1", 0, 0, false)]
    public void TestParseNthIntegerOverflow(string formula, int expectedA, int expectedB, bool expectedSuccess)
    {
        bool success = StyleCascade.ParseNth(formula, out int a, out int b);
        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedA, a);
            Assert.Equal(expectedB, b);
        }
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("2n+-1", true)]
    [InlineData("+3", true)]
    [InlineData("-n", true)]
    [InlineData("n-", false)]
    [InlineData("+-n+1", false)]
    [InlineData("2n+", false)]
    [InlineData("n+foo", false)]
    [InlineData("foon+1", false)]
    public void TestParseNthMalformedFormulas(string formula, bool expectedSuccess)
    {
        bool success = StyleCascade.ParseNth(formula, out int a, out int b);
        Assert.Equal(expectedSuccess, success);
    }

    [Fact]
    public void TestMatchesNthBoundaryConditions()
    {
        Assert.True(StyleCascade.MatchesNth(5, 0, 5));
        Assert.False(StyleCascade.MatchesNth(4, 0, 5));
        Assert.False(StyleCascade.MatchesNth(-5, 0, 5));

        Assert.True(StyleCascade.MatchesNth(5, 1, 0));
        Assert.True(StyleCascade.MatchesNth(0, 1, 0));
        Assert.False(StyleCascade.MatchesNth(-1, 1, 0));

        Assert.False(StyleCascade.MatchesNth(-1, 2, 1));
        Assert.False(StyleCascade.MatchesNth(-2, 2, 0));

        Assert.True(StyleCascade.MatchesNth(2000000000, 2, 0));
        Assert.False(StyleCascade.MatchesNth(2000000001, 2, 0));
    }

    [Fact]
    public void TestStyleResolutionWithMalformedSelectors()
    {
        var doc = new HtmlDocument();
        var parent = new HtmlElement { TagName = "div" };
        doc.Children.Add(parent);
        parent.Parent = doc;

        for (int i = 0; i < 10; i++)
        {
            var child = new HtmlElement { TagName = "span" };
            parent.Children.Add(child);
            child.Parent = parent;
        }

        var css = @"
            span:nth-child(2n+-1) { color: red; }
            span:nth-child(+-n+1) { color: blue; }
            span:nth-child(n-) { color: green; }
            span:nth-child(2147483648) { color: yellow; }
            span:nth-child(   ) { color: purple; }
            span:nth-child { color: orange; }
            span:nth-child() { color: pink; }
        ";

        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        Assert.NotNull(styles);
    }

    [Fact]
    public void TestDeeplyNestedTreesWithComplexPseudoClasses()
    {
        var doc = new HtmlDocument();
        HtmlElement current = new HtmlElement { TagName = "div" };
        doc.Children.Add(current);
        current.Parent = doc;

        List<HtmlElement> elements = new List<HtmlElement> { current };

        int nestingDepth = 80;
        for (int i = 0; i < nestingDepth; i++)
        {
            var child1 = new HtmlElement { TagName = "div" };
            var child2 = new HtmlElement { TagName = "span" };
            current.Children.Add(child1);
            child1.Parent = current;
            current.Children.Add(child2);
            child2.Parent = current;
            
            elements.Add(child1);
            elements.Add(child2);
            current = child1;
        }

        var css = @"
            div:nth-child(odd) > span:nth-child(even) { color: red; }
            div:nth-child(2n+1) > div:nth-child(odd) { font-size: 12px; }
            div:nth-last-child(3n) { display: block; }
            span:first-child { display: inline; }
            div:last-child { display: flex; }
        ";

        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        Assert.NotNull(styles);

        foreach (var element in elements)
        {
            Assert.True(styles.ContainsKey(element));
        }
    }
}
