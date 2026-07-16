using System;
using CDP.Html.Parser;
using Xunit;

namespace CDP.Html.Parser.Tests;

public class EmpiricalChallengeTests
{
    [Fact]
    public void TestScriptTagParsing_Bug()
    {
        // Issue: < inside <script> tags is treated as a new HTML node starting tag
        string html = "<script>if (a < b && c > d) { console.log('hello'); }</script>";
        var doc = HtmlParser.Parse(html);
        
        // Assert that the script tag has a single text child containing the full script
        var script = Assert.IsType<HtmlElement>(doc.Children[0]);
        Assert.Equal("script", script.TagName);
        
        // This assertion fails because the parser splits the text at the '<' character
        var child = Assert.Single(script.Children);
        var textNode = Assert.IsType<HtmlTextNode>(child);
        Assert.Equal("if (a < b && c > d) { console.log('hello'); }", textNode.Text);
    }

    [Fact]
    public void TestUnquotedAttributeWithSlash_Bug()
    {
        // Issue: Unquoted attribute value parser stops on '/' character, failing to parse path-like values
        string html = "<img src=/assets/image.png />";
        var doc = HtmlParser.Parse(html);
        
        var img = Assert.IsType<HtmlElement>(doc.Children[0]);
        Assert.Equal("img", img.TagName);
        
        // It should parse src="/assets/image.png"
        // This assertion fails because src is parsed as "" and it creates a new attribute "assets" or "image.png"
        Assert.True(img.Attributes.ContainsKey("src"), "Attributes should contain 'src'");
        Assert.Equal("/assets/image.png", img.Attributes["src"]);
    }
}
