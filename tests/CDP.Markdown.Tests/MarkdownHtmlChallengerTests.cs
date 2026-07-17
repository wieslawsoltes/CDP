using System;
using System.Linq;
using Xunit;
using SkiaSharp;
using CDP.Markdown.Parser;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Layout;
using CDP.Markdown.Renderer.Rendering;
using CDP.Html.Parser;
using CDP.Css.Parser;
using CDP.Html.Renderer.Style;

namespace CDP.Markdown.Tests;

public class MarkdownHtmlChallengerTests
{
    private DocumentLayout CreateTestLayout(string markdown, float widthLimit)
    {
        var doc = MarkdownParser.Parse(markdown);
        var measurer = new MockTextMeasurer();
        var layout = new DocumentLayout();
        layout.LoadDocument(doc);
        
        var context = new LayoutContext(
            maxWidth: widthLimit,
            measurer: measurer,
            resources: null,
            startY: 0,
            markdownText: markdown
        );
        layout.Layout(context);
        return layout;
    }

    private CssStyleSheet ExtractStylesFromDoc(HtmlDocument doc)
    {
        var stylesheet = new CssStyleSheet();
        ExtractStylesFromElement(doc, stylesheet);
        return stylesheet;
    }

    private void ExtractStylesFromElement(HtmlNode node, CssStyleSheet stylesheet)
    {
        if (node is HtmlElement element && string.Equals(element.TagName, "style", StringComparison.OrdinalIgnoreCase))
        {
            var textNode = element.Children.OfType<HtmlTextNode>().FirstOrDefault();
            if (textNode != null)
            {
                var parsedSheet = CssParser.Parse(textNode.Text);
                stylesheet.Rules.AddRange(parsedSheet.Rules);
            }
        }
        foreach (var child in node.Children)
        {
            ExtractStylesFromElement(child, stylesheet);
        }
    }

    [Fact]
    public void TestNestedAndMalformedHtmlBlocksRendering()
    {
        // 1. Nested HTML block
        var nestedMarkdown = "<div class=\"outer\">\n  <div class=\"inner\">\n    <p>Nested Paragraph</p>\n  </div>\n</div>";
        var nestedDoc = MarkdownParser.Parse(nestedMarkdown);
        Assert.NotNull(nestedDoc);
        Assert.Single(nestedDoc.Children);
        Assert.IsType<HtmlBlock>(nestedDoc.Children[0]);

        var nestedLayout = CreateTestLayout(nestedMarkdown, 400f);
        Assert.NotEmpty(nestedLayout.Blocks);
        var nestedBlock = Assert.IsType<HtmlLayoutBlock>(nestedLayout.Blocks[0]);
        Assert.True(nestedBlock.Bounds.Height > 0);

        // 2. Malformed HTML block (mismatched tags)
        var mismatchedMarkdown = "<div><span>Malformed Block</div>";
        var mismatchedDoc = MarkdownParser.Parse(mismatchedMarkdown);
        Assert.NotNull(mismatchedDoc);
        Assert.Single(mismatchedDoc.Children);
        Assert.IsType<HtmlBlock>(mismatchedDoc.Children[0]);

        var mismatchedLayout = CreateTestLayout(mismatchedMarkdown, 400f);
        Assert.NotEmpty(mismatchedLayout.Blocks);
        var mismatchedBlock = Assert.IsType<HtmlLayoutBlock>(mismatchedLayout.Blocks[0]);
        Assert.True(mismatchedBlock.Bounds.Height > 0);

        // 3. Malformed HTML block with mismatched tags outer-inner reversed
        var mismatchedReversedMarkdown = "<div><span>Mismatched Tags</div></span>";
        var mismatchedReversedLayout = CreateTestLayout(mismatchedReversedMarkdown, 400f);
        Assert.NotEmpty(mismatchedReversedLayout.Blocks);
        var mismatchedReversedBlock = Assert.IsType<HtmlLayoutBlock>(mismatchedReversedLayout.Blocks[0]);
        Assert.True(mismatchedReversedBlock.Bounds.Height > 0);

        // 4. Unclosed HTML block
        var unclosedMarkdown = "<div class=\"unclosed\">\nNo closing tag";
        var unclosedLayout = CreateTestLayout(unclosedMarkdown, 400f);
        Assert.NotEmpty(unclosedLayout.Blocks);
        var unclosedBlock = Assert.IsType<HtmlLayoutBlock>(unclosedLayout.Blocks[0]);
        Assert.True(unclosedBlock.Bounds.Height > 0);
    }

    [Fact]
    public void TestVeryLongLinesWithInlineSpansAndImages()
    {
        // Construct a very long text line containing inline HTML spans and images
        var longLineMarkdown = new string('a', 500) + 
                               " <span style=\"color: red;\">Inline Span</span> " + 
                               new string('b', 500) + 
                               " ![AltText](http://image.url) " + 
                               new string('c', 500);

        var doc = MarkdownParser.Parse(longLineMarkdown);
        Assert.NotNull(doc);
        Assert.Single(doc.Children);
        Assert.IsType<ParagraphBlock>(doc.Children[0]);

        // Layout with a narrow width limit to force extensive wrapping and verify no exceptions
        var layout = CreateTestLayout(longLineMarkdown, 200f);
        Assert.NotEmpty(layout.Blocks);
        var block = Assert.IsType<ParagraphLayoutBlock>(layout.Blocks[0]);
        
        // Assert that the layout has computed bounds and wrapped into multiple lines
        Assert.True(block.Bounds.Height > 0);
        
        // Make sure caret and selection methods work on this extreme layout
        var caret = layout.GetCaretBounds(100);
        Assert.False(caret.IsEmpty);

        var selection = layout.GetSelectionBounds(10, 1200);
        Assert.NotEmpty(selection);

        var hit = layout.HitTest(new SKPoint(50f, 50f));
        Assert.InRange(hit, 0, longLineMarkdown.Length);
    }

    [Fact]
    public void TestStyleTagParsingWithinMarkdownHtmlBlock()
    {
        var htmlWithStyle = @"<style>
  .highlight { background-color: rgb(255, 255, 0); color: rgb(0, 0, 0); }
</style>
<div class=""highlight"">Styled Text</div>";

        var doc = MarkdownParser.Parse(htmlWithStyle);
        Assert.NotNull(doc);
        Assert.Equal(2, doc.Children.Count);
        
        var styleBlock = Assert.IsType<HtmlBlock>(doc.Children[0]);
        var divBlock = Assert.IsType<HtmlBlock>(doc.Children[1]);

        // 1. Verify HTML parser parses the style block
        var styleHtmlDoc = HtmlParser.Parse(styleBlock.Html);
        Assert.NotNull(styleHtmlDoc);

        // Find the style element
        var styleElement = styleHtmlDoc.Children.OfType<HtmlElement>().FirstOrDefault(e => string.Equals(e.TagName, "style", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(styleElement);

        // Verify the text node inside contains the CSS
        var textNode = Assert.IsType<HtmlTextNode>(styleElement.Children[0]);
        Assert.Contains(".highlight", textNode.Text);

        // 2. Parse the HTML elements combined or separately to test cascading.
        // We can parse the full HTML block as a single HTML document to test resolving styles.
        var htmlDoc = HtmlParser.Parse(htmlWithStyle);
        Assert.NotNull(htmlDoc);

        // Extract stylesheet rules using the helper
        var extractedStylesheet = ExtractStylesFromDoc(htmlDoc);
        Assert.NotNull(extractedStylesheet);
        Assert.Single(extractedStylesheet.Rules);

        var rule = extractedStylesheet.Rules[0];
        Assert.Single(rule.Selectors);
        Assert.Equal(".highlight", rule.Selectors[0].Text);
        Assert.Equal("rgb(255, 255, 0)", rule.Declarations["background-color"]);
        Assert.Equal("rgb(0, 0, 0)", rule.Declarations["color"]);

        // 3. Resolve styles and check if class styles apply to the styled div element
        var styles = StyleCascade.ResolveStyles(htmlDoc, extractedStylesheet);
        var divElement = htmlDoc.Children.OfType<HtmlElement>().FirstOrDefault(e => string.Equals(e.TagName, "div", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(divElement);

        Assert.True(styles.ContainsKey(divElement));
        var computed = styles[divElement];
        Assert.Equal(new SKColor(255, 255, 0), computed.BackgroundColor);
        Assert.Equal(new SKColor(0, 0, 0), computed.Color);
    }
}
