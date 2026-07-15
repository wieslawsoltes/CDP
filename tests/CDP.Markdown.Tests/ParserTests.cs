using System;
using System.Linq;
using Xunit;
using CDP.Markdown.Parser;
using CDP.Markdown.Parser.AST;

namespace CDP.Markdown.Tests;

public class ParserTests
{
    [Fact]
    public void TestHeadingParsing()
    {
        var markdown = "# Heading 1\n## Heading 2\n### Heading 3\n#### Heading 4\n##### Heading 5\n###### Heading 6";
        var doc = MarkdownParser.Parse(markdown);

        Assert.NotNull(doc);
        Assert.Equal(6, doc.Children.Count);

        for (int i = 0; i < 6; i++)
        {
            var heading = Assert.IsType<HeadingBlock>(doc.Children[i]);
            Assert.Equal(i + 1, heading.Level);
            Assert.Equal(doc, heading.Parent);
            Assert.Single(heading.Children);
            var literal = Assert.IsType<LiteralInline>(heading.Children[0]);
            Assert.Equal($"Heading {i + 1}", literal.Text);
            Assert.Equal(heading, literal.Parent);

            // Spans should be populated
            Assert.True(heading.Span.Length > 0);
            Assert.True(literal.Span.Length > 0);
        }
    }

    [Fact]
    public void TestParagraphAndInlines()
    {
        var markdown = "This is *italic*, **bold**, ~~strikethrough~~, `code`, [link](url \"title\"), and ![image](img).\nSoft break\nHard break  \nDone";
        var doc = MarkdownParser.Parse(markdown);

        Assert.NotNull(doc);
        Assert.Single(doc.Children);

        var p = Assert.IsType<ParagraphBlock>(doc.Children[0]);
        
        // Asserting the child structure of the paragraph
        Assert.NotEmpty(p.Children);

        var italic = Assert.IsType<EmphasisInline>(p.Children.First(c => c is EmphasisInline emp && !emp.IsStrong));
        Assert.False(italic.IsStrong);
        Assert.Equal("italic", Assert.IsType<LiteralInline>(italic.Children[0]).Text);

        var bold = Assert.IsType<EmphasisInline>(p.Children.First(c => c is EmphasisInline emp && emp.IsStrong));
        Assert.True(bold.IsStrong);
        Assert.Equal("bold", Assert.IsType<LiteralInline>(bold.Children[0]).Text);

        var strikethrough = Assert.IsType<StrikeThroughInline>(p.Children.First(c => c is StrikeThroughInline));
        Assert.Equal("strikethrough", Assert.IsType<LiteralInline>(strikethrough.Children[0]).Text);

        var code = Assert.IsType<CodeInline>(p.Children.First(c => c is CodeInline));
        Assert.Equal("code", code.Code);

        var link = Assert.IsType<LinkInline>(p.Children.First(c => c is LinkInline));
        Assert.Equal("url", link.Url);
        Assert.Equal("title", link.Title);
        Assert.Equal("link", Assert.IsType<LiteralInline>(link.Children[0]).Text);

        var img = Assert.IsType<ImageInline>(p.Children.First(c => c is ImageInline));
        Assert.Equal("img", img.Url);
        Assert.Equal("image", img.AltText);

        var softBreak = Assert.IsType<LineBreakInline>(p.Children.First(c => c is LineBreakInline lb && !lb.IsHard));
        Assert.False(softBreak.IsHard);

        var hardBreak = Assert.IsType<LineBreakInline>(p.Children.Last(c => c is LineBreakInline lb && lb.IsHard));
        Assert.True(hardBreak.IsHard);
    }

    [Fact]
    public void TestListsAndChecklists()
    {
        var markdown = "- Item 1\n- Item 2\n\n1. First\n2. Second\n\n- [ ] Unchecked\n- [x] Checked";
        var doc = MarkdownParser.Parse(markdown);

        Assert.NotNull(doc);
        Assert.Equal(3, doc.Children.Count);

        // 1. Unordered List
        var list1 = Assert.IsType<ListBlock>(doc.Children[0]);
        Assert.False(list1.IsOrdered);
        Assert.Equal("-", list1.BulletChar);
        Assert.Equal(2, list1.Children.Count);
        
        var li1 = Assert.IsType<ListItemBlock>(list1.Children[0]);
        Assert.Null(li1.IsChecked);
        var lit1 = Assert.IsType<LiteralInline>(li1.Children[0].Children[0]);
        Assert.Equal("Item 1", lit1.Text);

        // 2. Ordered List
        var list2 = Assert.IsType<ListBlock>(doc.Children[1]);
        Assert.True(list2.IsOrdered);
        Assert.Equal(1, list2.StartIndex);
        Assert.Equal(2, list2.Children.Count);
        
        var li2 = Assert.IsType<ListItemBlock>(list2.Children[0]);
        Assert.Null(li2.IsChecked);

        // 3. Checklist
        var list3 = Assert.IsType<ListBlock>(doc.Children[2]);
        Assert.False(list3.IsOrdered);
        Assert.Equal(2, list3.Children.Count);

        var check1 = Assert.IsType<ListItemBlock>(list3.Children[0]);
        Assert.False(check1.IsChecked);
        Assert.Equal("Unchecked", Assert.IsType<LiteralInline>(check1.Children[0].Children[0]).Text);

        var check2 = Assert.IsType<ListItemBlock>(list3.Children[1]);
        Assert.True(check2.IsChecked);
        Assert.Equal("Checked", Assert.IsType<LiteralInline>(check2.Children[0].Children[0]).Text);
    }

    [Fact]
    public void TestCodeAndQuoteBlocks()
    {
        var markdown = "> Quote block\n> Second line\n\n```csharp\nvar a = 1;\n```\n\n    indented code\n";
        var doc = MarkdownParser.Parse(markdown);

        Assert.NotNull(doc);
        Assert.Equal(3, doc.Children.Count);

        var quote = Assert.IsType<QuoteBlock>(doc.Children[0]);
        Assert.NotEmpty(quote.Children);

        var fencedCode = Assert.IsType<CodeBlock>(doc.Children[1]);
        Assert.Equal("csharp", fencedCode.Language);
        Assert.Contains("var a = 1;", fencedCode.Code);

        var indentedCode = Assert.IsType<CodeBlock>(doc.Children[2]);
        Assert.Null(indentedCode.Language);
        Assert.Contains("indented code", indentedCode.Code);
    }

    [Fact]
    public void TestTables()
    {
        var markdown = "| Col 1 | Col 2 | Col 3 |\n| :--- | :---: | ---: |\n| Cell 1 | Cell 2 | Cell 3 |";
        var doc = MarkdownParser.Parse(markdown);

        Assert.NotNull(doc);
        Assert.Single(doc.Children);

        var table = Assert.IsType<TableBlock>(doc.Children[0]);
        Assert.Equal(2, table.Children.Count); // Header row and 1 body row (delimiter is parsed as layout definition, not a table row node)

        var headerRow = Assert.IsType<TableRowBlock>(table.Children[0]);
        Assert.True(headerRow.IsHeader);
        Assert.Equal(3, headerRow.Children.Count);

        var cell1 = Assert.IsType<TableCellBlock>(headerRow.Children[0]);
        var cell2 = Assert.IsType<TableCellBlock>(headerRow.Children[1]);
        var cell3 = Assert.IsType<TableCellBlock>(headerRow.Children[2]);

        Assert.Equal(TableCellAlignment.Left, cell1.Alignment);
        Assert.Equal(TableCellAlignment.Center, cell2.Alignment);
        Assert.Equal(TableCellAlignment.Right, cell3.Alignment);

        var bodyRow = Assert.IsType<TableRowBlock>(table.Children[1]);
        Assert.False(bodyRow.IsHeader);
    }

    [Fact]
    public void TestRoundtripSerialization()
    {
        var original = "# Heading\n\nParagraph with **bold** text.\n\n- [ ] Task 1\n- [x] Task 2\n\n```csharp\nint x = 42;\n```\n\n| H1 | H2 |\n| :--- | :---: |\n| A | B |\n\n> Quote\n> block\n";
        
        var doc = MarkdownParser.Parse(original);
        var serialized = MarkdownSerializer.Serialize(doc);

        // Reparse the serialized version
        var doc2 = MarkdownParser.Parse(serialized);
        var serialized2 = MarkdownSerializer.Serialize(doc2);

        // The two serialized outputs should match perfectly
        Assert.Equal(serialized, serialized2);
    }

    [Fact]
    public void TestHtmlBlockParsing()
    {
        var markdown = "<div class=\"test\">\n  <img src=\"test.png\" />\n</div>";
        var doc = MarkdownParser.Parse(markdown);

        Assert.NotNull(doc);
        Assert.Single(doc.Children);

        var html = Assert.IsType<HtmlBlock>(doc.Children[0]);
        Assert.Contains("<div class=\"test\">", html.Html);
        Assert.Contains("<img src=\"test.png\" />", html.Html);
    }
}
