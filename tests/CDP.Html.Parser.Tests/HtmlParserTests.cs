using System;
using System.Linq;
using CDP.Html.Parser;
using Xunit;

namespace CDP.Html.Parser.Tests;

public class HtmlParserTests
{
    [Fact]
    public void TestParseSimpleElement()
    {
        string html = "<div></div>";
        var doc = HtmlParser.Parse(html);
        Assert.NotNull(doc);
        Assert.Single(doc.Children);

        var div = doc.Children[0] as HtmlElement;
        Assert.NotNull(div);
        Assert.Equal("div", div.TagName);
        Assert.Empty(div.Attributes);
        Assert.Equal(doc, div.Parent);
        Assert.Equal(0, div.Span.Start);
        Assert.Equal(html.Length, div.Span.Length);
    }

    [Fact]
    public void TestParseAttributes()
    {
        string html = "<div id=\"main\" class='container active' style=\"color: red;\" disabled>Hello</div>";
        var doc = HtmlParser.Parse(html);
        Assert.NotNull(doc);
        Assert.Single(doc.Children);

        var div = doc.Children[0] as HtmlElement;
        Assert.NotNull(div);
        Assert.Equal("div", div.TagName);
        Assert.Equal(4, div.Attributes.Count);
        Assert.Equal("main", div.Attributes["id"]);
        Assert.Equal("container active", div.Attributes["class"]);
        Assert.Equal("color: red;", div.Attributes["style"]);
        Assert.Equal("", div.Attributes["disabled"]);

        Assert.Single(div.Children);
        var textNode = div.Children[0] as HtmlTextNode;
        Assert.NotNull(textNode);
        Assert.Equal("Hello", textNode.Text);
        Assert.Equal(div, textNode.Parent);
    }

    [Fact]
    public void TestVoidTags()
    {
        string html = "<div><img src=\"logo.png\"><br><input type=\"text\"></div>";
        var doc = HtmlParser.Parse(html);
        Assert.NotNull(doc);
        Assert.Single(doc.Children);

        var div = doc.Children[0] as HtmlElement;
        Assert.NotNull(div);
        Assert.Equal(3, div.Children.Count);

        var img = div.Children[0] as HtmlElement;
        Assert.NotNull(img);
        Assert.Equal("img", img.TagName);
        Assert.Equal("logo.png", img.Attributes["src"]);
        Assert.Empty(img.Children);

        var br = div.Children[1] as HtmlElement;
        Assert.NotNull(br);
        Assert.Equal("br", br.TagName);
        Assert.Empty(br.Children);

        var input = div.Children[2] as HtmlElement;
        Assert.NotNull(input);
        Assert.Equal("input", input.TagName);
        Assert.Equal("text", input.Attributes["type"]);
        Assert.Empty(input.Children);
    }

    [Fact]
    public void TestSelfClosingTags()
    {
        string html = "<div />";
        var doc = HtmlParser.Parse(html);
        Assert.NotNull(doc);
        Assert.Single(doc.Children);

        var div = doc.Children[0] as HtmlElement;
        Assert.NotNull(div);
        Assert.Equal("div", div.TagName);
        Assert.Empty(div.Children);
    }

    [Fact]
    public void TestCommentsAreSkipped()
    {
        string html = "<div><!-- This is a comment --><span>Content</span></div>";
        var doc = HtmlParser.Parse(html);
        Assert.NotNull(doc);
        Assert.Single(doc.Children);

        var div = doc.Children[0] as HtmlElement;
        Assert.NotNull(div);
        Assert.Single(div.Children);

        var span = div.Children[0] as HtmlElement;
        Assert.NotNull(span);
        Assert.Equal("span", span.TagName);
    }

    [Fact]
    public void TestUnclosedTagsAreHandledGracefully()
    {
        string html = "<div><p>Hello <b>world";
        var doc = HtmlParser.Parse(html);
        Assert.NotNull(doc);
        Assert.Single(doc.Children);

        var div = doc.Children[0] as HtmlElement;
        Assert.NotNull(div);
        Assert.Single(div.Children);

        var p = div.Children[0] as HtmlElement;
        Assert.NotNull(p);
        Assert.Equal(2, p.Children.Count);

        var text = p.Children[0] as HtmlTextNode;
        Assert.NotNull(text);
        Assert.Equal("Hello ", text.Text);

        var b = p.Children[1] as HtmlElement;
        Assert.NotNull(b);
        Assert.Equal("world", ((HtmlTextNode)b.Children[0]).Text);
    }

    [Fact]
    public void TestEntityDecoding()
    {
        string html = "<div>&lt;Hello &amp; World&gt;</div>";
        var doc = HtmlParser.Parse(html);
        Assert.NotNull(doc);
        var div = doc.Children[0] as HtmlElement;
        Assert.NotNull(div);
        var text = div.Children[0] as HtmlTextNode;
        Assert.NotNull(text);
        Assert.Equal("<Hello & World>", text.Text);
    }

    [Fact]
    public void TestSourceSpans()
    {
        string html = "<div><span class=\"x\">Test</span></div>";
        var doc = HtmlParser.Parse(html);
        Assert.NotNull(doc);

        var div = doc.Children[0] as HtmlElement;
        Assert.NotNull(div);
        Assert.Equal(0, div.Span.Start);
        Assert.Equal(html.Length, div.Span.Length);

        var span = div.Children[0] as HtmlElement;
        Assert.NotNull(span);
        Assert.Equal(5, span.Span.Start);
        Assert.Equal(27, span.Span.Length); // "<span class=\"x\">Test</span>".Length is 27

        var text = span.Children[0] as HtmlTextNode;
        Assert.NotNull(text);
        Assert.Equal(21, text.Span.Start); // index of "Test"
        Assert.Equal(4, text.Span.Length);
    }
}
