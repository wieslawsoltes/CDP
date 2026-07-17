using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Markdig;
using CDP.Markdown.Parser;

using ListBlock = CDP.Markdown.Parser.AST.ListBlock;
using ListItemBlock = CDP.Markdown.Parser.AST.ListItemBlock;
using ParagraphBlock = CDP.Markdown.Parser.AST.ParagraphBlock;
using QuoteBlock = CDP.Markdown.Parser.AST.QuoteBlock;
using CodeBlock = CDP.Markdown.Parser.AST.CodeBlock;
using HeadingBlock = CDP.Markdown.Parser.AST.HeadingBlock;
using TableBlock = CDP.Markdown.Parser.AST.TableBlock;
using TableRowBlock = CDP.Markdown.Parser.AST.TableRowBlock;
using TableCellBlock = CDP.Markdown.Parser.AST.TableCellBlock;
using TableCellAlignment = CDP.Markdown.Parser.AST.TableCellAlignment;
using LiteralInline = CDP.Markdown.Parser.AST.LiteralInline;
using EmphasisInline = CDP.Markdown.Parser.AST.EmphasisInline;
using StrikeThroughInline = CDP.Markdown.Parser.AST.StrikeThroughInline;
using CodeInline = CDP.Markdown.Parser.AST.CodeInline;
using LinkInline = CDP.Markdown.Parser.AST.LinkInline;
using ImageInline = CDP.Markdown.Parser.AST.ImageInline;
using MarkdownNode = CDP.Markdown.Parser.AST.MarkdownNode;

namespace CDP.Markdown.Tests;

public class AdversarialParserTests
{
    [Fact]
    public void TestNullAndWhitespaceInputs()
    {
        Assert.Throws<ArgumentNullException>(() => MarkdownParser.Parse(null!));

        var emptyDoc = MarkdownParser.Parse("");
        Assert.NotNull(emptyDoc);
        Assert.Empty(emptyDoc.Children);

        var whitespaceDoc = MarkdownParser.Parse("   \n\t  \n  ");
        Assert.NotNull(whitespaceDoc);
    }

    [Fact]
    public void TestMismatchedAndBrokenChecklists()
    {
        // Markdig TaskList supports lower and uppercase x: [x], [X].
        var markdown = @"
- [ ] Item 1
- [x] Item 2
- [X] Item 3
- [  ] Item 4
- [?] Item 5
- [-] Item 6
- [] Item 7
";
        var doc = MarkdownParser.Parse(markdown);
        Assert.NotNull(doc);
        var list = Assert.IsType<ListBlock>(doc.Children[0]);
        Assert.Equal(7, list.Children.Count);

        var li1 = Assert.IsType<ListItemBlock>(list.Children[0]);
        Assert.False(li1.IsChecked); // [ ]

        var li2 = Assert.IsType<ListItemBlock>(list.Children[1]);
        Assert.True(li2.IsChecked); // [x]

        var li3 = Assert.IsType<ListItemBlock>(list.Children[2]);
        Assert.True(li3.IsChecked); // [X]

        var li4 = Assert.IsType<ListItemBlock>(list.Children[3]);
        Assert.Null(li4.IsChecked); // [  ] - invalid checklist format

        var li5 = Assert.IsType<ListItemBlock>(list.Children[4]);
        Assert.Null(li5.IsChecked); // [?] - invalid checklist format

        var li6 = Assert.IsType<ListItemBlock>(list.Children[5]);
        Assert.Null(li6.IsChecked); // [-] - invalid checklist format

        var li7 = Assert.IsType<ListItemBlock>(list.Children[6]);
        Assert.Null(li7.IsChecked); // [] - invalid checklist format
    }

    [Fact]
    public void TestBrokenLinksAndImages()
    {
        var markdown = @"
[broken link 1(
[broken link 2(url
[broken link 3] (url)
[broken link 4]
![broken image
![broken image](
";
        var doc = MarkdownParser.Parse(markdown);
        Assert.NotNull(doc);
        
        var p = Assert.IsType<ParagraphBlock>(doc.Children[0]);
        Assert.NotEmpty(p.Children);
    }

    [Fact]
    public void TestCodeBlockWithBackticksRoundtrip()
    {
        var markdown = "```csharp\nvar code = \"```\";\n```";
        var doc1 = MarkdownParser.Parse(markdown);
        var serialized1 = MarkdownSerializer.Serialize(doc1);
        
        var doc2 = MarkdownParser.Parse(serialized1);
        var serialized2 = MarkdownSerializer.Serialize(doc2);
        
        Assert.Equal(serialized1, serialized2);
    }

    [Fact]
    public void TestLinkWithQuotesInTitleRoundtrip()
    {
        var markdown = "[link](url \"title with \\\"quotes\\\"\")";
        var doc = MarkdownParser.Parse(markdown);
        var serialized = MarkdownSerializer.Serialize(doc);
        
        var doc2 = MarkdownParser.Parse(serialized);
        var serialized2 = MarkdownSerializer.Serialize(doc2);
        
        Assert.Equal(serialized, serialized2);
    }

    [Fact]
    public void TestBrokenTableStructures()
    {
        // 1. Mismatched columns (more cells than columns)
        var markdown1 = @"
| Col 1 | Col 2 |
| :--- | :--- |
| Cell 1 | Cell 2 | Cell 3 | Cell 4 |
";
        var doc1 = MarkdownParser.Parse(markdown1);
        var table1 = Assert.IsType<TableBlock>(doc1.Children[0]);
        Assert.Equal(2, table1.Children.Count);
        var row2 = Assert.IsType<TableRowBlock>(table1.Children[1]);
        Assert.Equal(4, row2.Children.Count);
        var cell3 = Assert.IsType<TableCellBlock>(row2.Children[2]);
        Assert.Equal(TableCellAlignment.None, cell3.Alignment);

        // 2. Mismatched columns (fewer cells than columns)
        var markdown2 = @"
| Col 1 | Col 2 | Col 3 |
| :--- | :--- | :--- |
| Cell 1 |
";
        var doc2 = MarkdownParser.Parse(markdown2);
        var table2 = Assert.IsType<TableBlock>(doc2.Children[0]);
        var row2_2 = Assert.IsType<TableRowBlock>(table2.Children[1]);
        Assert.Equal(3, row2_2.Children.Count); // Markdig pads to 3 cells

        // 3. Broken table with no delimiter row
        var markdown3 = @"
| Col 1 | Col 2 |
| Cell 1 | Cell 2 |
";
        var doc3 = MarkdownParser.Parse(markdown3);
        Assert.NotEmpty(doc3.Children);
        Assert.True(doc3.Children[0] is ParagraphBlock);
    }

    [Fact]
    public void TestDeeplyNestedStructures()
    {
        // 1. Deeply nested blockquotes
        var nestingDepth = 60;
        var bqMarkdown = string.Concat(Enumerable.Repeat("> ", nestingDepth)) + "Hello deeply nested blockquote";
        var doc = MarkdownParser.Parse(bqMarkdown);
        Assert.NotNull(doc);
        
        MarkdownNode current = doc;
        for (int i = 0; i < nestingDepth; i++)
        {
            Assert.Single(current.Children);
            current = Assert.IsType<QuoteBlock>(current.Children[0]);
        }
        Assert.Single(current.Children);
        var p = Assert.IsType<ParagraphBlock>(current.Children[0]);
        var lit = Assert.IsType<LiteralInline>(p.Children[0]);
        Assert.Equal("Hello deeply nested blockquote", lit.Text);

        // 2. Deeply nested lists
        var listMarkdown = "";
        for (int i = 0; i < nestingDepth; i++)
        {
            listMarkdown += new string(' ', i * 2) + "- Level " + i + "\n";
        }
        var listDoc = MarkdownParser.Parse(listMarkdown);
        Assert.NotNull(listDoc);
        current = listDoc.Children[0]; // ListBlock
        for (int i = 0; i < nestingDepth; i++)
        {
            var listBlock = Assert.IsType<ListBlock>(current);
            Assert.Single(listBlock.Children);
            var listItem = Assert.IsType<ListItemBlock>(listBlock.Children[0]);
            
            Assert.NotEmpty(listItem.Children);
            
            if (i < nestingDepth - 1)
            {
                Assert.Equal(2, listItem.Children.Count);
                current = Assert.IsType<ListBlock>(listItem.Children[1]);
            }
            else
            {
                Assert.Single(listItem.Children);
            }
        }
    }

    [Fact]
    public void TestMassiveDocumentsStress()
    {
        // Large paragraph stress test
        var paragraphCount = 1000;
        var paraMarkdown = string.Join("\n\n", Enumerable.Repeat("This is a paragraph with **bold** and *italic* text.", paragraphCount));
        var doc = MarkdownParser.Parse(paraMarkdown);
        Assert.Equal(paragraphCount, doc.Children.Count);

        // Large table stress test
        var rowCount = 500;
        var tableMarkdown = "| Col 1 | Col 2 |\n|---|---|\n" + string.Join("\n", Enumerable.Repeat("| Cell A | Cell B |", rowCount));
        var tableDoc = MarkdownParser.Parse(tableMarkdown);
        var table = Assert.IsType<TableBlock>(tableDoc.Children[0]);
        Assert.Equal(rowCount + 1, table.Children.Count); // Header + 500 rows

        // Large code block stress test
        var linesCount = 2000;
        var codeLines = string.Join("\n", Enumerable.Repeat("console.log('stress line');", linesCount));
        var codeMarkdown = $"```javascript\n{codeLines}\n```";
        var codeDoc = MarkdownParser.Parse(codeMarkdown);
        var codeBlock = Assert.IsType<CodeBlock>(codeDoc.Children[0]);
        Assert.Equal("javascript", codeBlock.Language);
        Assert.Contains("stress line", codeBlock.Code);
    }

    [Fact]
    public void TestUnicodeAndSpecialCharacters()
    {
        var markdown = "# Heading 🦄 🚀\n" +
                       "Some Arabic: العربية and Hebrew: עברית\n" +
                       "Zero-width space: a\u200bb and surrogate pairs \uD83D\uDE00\n" +
                       "Plain Text on line 4";
        var doc = MarkdownParser.Parse(markdown);
        Assert.NotNull(doc);
        Assert.Equal(2, doc.Children.Count); // Heading block & single Paragraph block
        
        var heading = Assert.IsType<HeadingBlock>(doc.Children[0]);
        var litHeading = Assert.IsType<LiteralInline>(heading.Children[0]);
        Assert.Contains("🦄 🚀", litHeading.Text);

        var p2 = Assert.IsType<ParagraphBlock>(doc.Children[1]);
        var text = string.Concat(p2.Children.OfType<LiteralInline>().Select(l => l.Text));
        Assert.Contains("العربية", text);
        Assert.Contains("\uD83D\uDE00", text);
    }

    [Fact]
    public void TestHtmlAndEntitiesParsed()
    {
        var markdown = "Special chars: <script>alert('x')</script> & &amp; &lt;";
        var doc = MarkdownParser.Parse(markdown);
        Assert.NotNull(doc);
        var p = Assert.IsType<ParagraphBlock>(doc.Children[0]);
        var serialized = MarkdownSerializer.Serialize(p);
        Assert.Contains("<script>", serialized);
        Assert.Contains("&lt;", serialized);
    }

    [Fact]
    public void TestSerializerWithManualInvalidAST()
    {
        // 1. HeadingBlock with weird level (should clamp to 1-6)
        var hLow = new HeadingBlock { Level = 0 };
        hLow.Children.Add(new LiteralInline { Text = "Low" });
        Assert.Equal("# Low", MarkdownSerializer.Serialize(hLow));

        var hHigh = new HeadingBlock { Level = 10 };
        hHigh.Children.Add(new LiteralInline { Text = "High" });
        Assert.Equal("###### High", MarkdownSerializer.Serialize(hHigh));

        // 2. LinkInline with null Title
        var linkNullTitle = new LinkInline { Url = "http://test", Title = null };
        linkNullTitle.Children.Add(new LiteralInline { Text = "link" });
        Assert.Equal("[link](http://test)", MarkdownSerializer.Serialize(linkNullTitle));

        // 3. Manual empty table block
        var tableEmpty = new TableBlock();
        Assert.Equal("", MarkdownSerializer.Serialize(tableEmpty));

        // 4. ImageInline with null fields
        var imgNull = new ImageInline { Url = null!, AltText = null };
        Assert.Equal("![]()", MarkdownSerializer.Serialize(imgNull));
    }

    [Fact]
    public void TestSerializerWithNullCodeBlockCode()
    {
        var cbNull = new CodeBlock { Code = null! };
        MarkdownSerializer.Serialize(cbNull); // Throws NullReferenceException (bug in serializer)
    }

    [Fact]
    public void TestAdversarialRoundtrip()
    {
        var complexMarkdown = @"# Title

Some text with `inline code` and [links](http://example.com ""title"").

- [ ] Task 1
- [x] Task 2

> Blockquote with nested text.
> Line 2.

| Col A | Col B |
| :--- | ---: |
| A1 | B1 |
| A2 | B2 |

---

And a paragraph at the end with ~~strikethrough~~ and **bold** and *italic*.
";
        var doc1 = MarkdownParser.Parse(complexMarkdown);
        var serialized1 = MarkdownSerializer.Serialize(doc1);
        
        var doc2 = MarkdownParser.Parse(serialized1);
        var serialized2 = MarkdownSerializer.Serialize(doc2);

        Assert.Equal(serialized1, serialized2);
    }

    [Fact]
    public void TestTableWithPipeInCodeInline()
    {
        var markdown = "| Col 1 | Col 2 |\n| :--- | :--- |\n| `a \\| b` | c |\n";
        var doc = MarkdownParser.Parse(markdown);
        Assert.NotNull(doc);
        var table = Assert.IsType<TableBlock>(doc.Children[0]);
        Assert.Equal(2, table.Children.Count); // Header + 1 body row
        
        var bodyRow = Assert.IsType<TableRowBlock>(table.Children[1]);
        var cell1 = Assert.IsType<TableCellBlock>(bodyRow.Children[0]);
        var p = Assert.IsType<ParagraphBlock>(cell1.Children[0]);
        var codeInline = p.Children.OfType<CodeInline>().First();
        Assert.Equal("a \\| b", codeInline.Code);

        // Serialize and verify roundtrip
        var serialized = MarkdownSerializer.Serialize(doc);
        Assert.Contains("`a \\| b`", serialized);

        var doc2 = MarkdownParser.Parse(serialized);
        var table2 = Assert.IsType<TableBlock>(doc2.Children[0]);
        var bodyRow2 = Assert.IsType<TableRowBlock>(table2.Children[1]);
        var cell1_2 = Assert.IsType<TableCellBlock>(bodyRow2.Children[0]);
        var p2 = Assert.IsType<ParagraphBlock>(cell1_2.Children[0]);
        var codeInline2 = p2.Children.OfType<CodeInline>().First();
        Assert.Equal("a \\| b", codeInline2.Code);
    }

    [Fact]
    public void TestTableWithPipeInLink()
    {
        var markdown = "| Col 1 | Col 2 |\n| :--- | :--- |\n| [link \\| with pipe](url) | c |\n";
        var doc = MarkdownParser.Parse(markdown);
        Assert.NotNull(doc);
        var table = Assert.IsType<TableBlock>(doc.Children[0]);
        Assert.Equal(2, table.Children.Count);

        var bodyRow = Assert.IsType<TableRowBlock>(table.Children[1]);
        var cell1 = Assert.IsType<TableCellBlock>(bodyRow.Children[0]);
        var p = Assert.IsType<ParagraphBlock>(cell1.Children[0]);
        var link = p.Children.OfType<LinkInline>().First();
        var linkText = string.Concat(link.Children.OfType<LiteralInline>().Select(c => c.Text));
        Assert.Equal("link | with pipe", linkText);

        // Serialize and verify roundtrip
        var serialized = MarkdownSerializer.Serialize(doc);
        Assert.Contains(@"[link \| with pipe](url)", serialized);

        var doc2 = MarkdownParser.Parse(serialized);
        var table2 = Assert.IsType<TableBlock>(doc2.Children[0]);
        var bodyRow2 = Assert.IsType<TableRowBlock>(table2.Children[1]);
        var cell1_2 = Assert.IsType<TableCellBlock>(bodyRow2.Children[0]);
        var p2 = Assert.IsType<ParagraphBlock>(cell1_2.Children[0]);
        var link2 = p2.Children.OfType<LinkInline>().First();
        var linkText2 = string.Concat(link2.Children.OfType<LiteralInline>().Select(c => c.Text));
        Assert.Equal("link | with pipe", linkText2);
    }

    [Fact]
    public void TestHtmlTagWithUnderscore()
    {
        var markdown = "This is a <span class=\"my_class\">tag</span> and entity &amp;.";
        var doc = MarkdownParser.Parse(markdown);
        Assert.NotNull(doc);
        var p = Assert.IsType<ParagraphBlock>(doc.Children[0]);
        
        // Assert the parsed types and values
        var lit1 = Assert.IsType<LiteralInline>(p.Children[0]);
        Assert.False(lit1.IsHtml);
        Assert.Equal("This is a ", lit1.Text);

        var htmlInline1 = Assert.IsType<LiteralInline>(p.Children[1]);
        Assert.True(htmlInline1.IsHtml);
        Assert.Equal("<span class=\"my_class\">", htmlInline1.Text);

        var lit2 = Assert.IsType<LiteralInline>(p.Children[2]);
        Assert.False(lit2.IsHtml);
        Assert.Equal("tag", lit2.Text);

        var htmlInline2 = Assert.IsType<LiteralInline>(p.Children[3]);
        Assert.True(htmlInline2.IsHtml);
        Assert.Equal("</span>", htmlInline2.Text);

        var lit3 = Assert.IsType<LiteralInline>(p.Children[4]);
        Assert.False(lit3.IsHtml);
        Assert.Equal(" and entity ", lit3.Text);

        var entityInline = Assert.IsType<LiteralInline>(p.Children[5]);
        Assert.True(entityInline.IsHtml);
        Assert.Equal("&amp;", entityInline.Text);

        var lit4 = Assert.IsType<LiteralInline>(p.Children[6]);
        Assert.False(lit4.IsHtml);
        Assert.Equal(".", lit4.Text);

        // Serialize and verify
        var serialized = MarkdownSerializer.Serialize(doc);
        Assert.Contains("<span class=\"my_class\">", serialized);
        Assert.DoesNotContain("<span class=\"my\\_class\">", serialized);
        Assert.Contains("&amp;", serialized);

        var doc2 = MarkdownParser.Parse(serialized);
        var p2 = Assert.IsType<ParagraphBlock>(doc2.Children[0]);
        var htmlInline2_2 = Assert.IsType<LiteralInline>(p2.Children[1]);
        Assert.True(htmlInline2_2.IsHtml);
        Assert.Equal("<span class=\"my_class\">", htmlInline2_2.Text);
    }
}




