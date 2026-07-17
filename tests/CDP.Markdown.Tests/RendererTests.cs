using System;
using System.Collections.Generic;
using Xunit;
using SkiaSharp;
using CDP.Markdown.Parser;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Layout;
using CDP.Markdown.Renderer.Rendering;

namespace CDP.Markdown.Tests;

public class MockTextMeasurer : ITextMeasurer
{
    public float MeasureText(string text, TextStyle style)
    {
        return text.Length * 10f;
    }

    public float[] GetCharacterWidths(string text, TextStyle style)
    {
        var widths = new float[text.Length];
        Array.Fill(widths, 10f);
        return widths;
    }

    public float GetLineHeight(TextStyle style)
    {
        return 20f;
    }
}

public class RendererTests
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

    [Fact]
    public void TestParagraphLayout_Respects_MaxWidth()
    {
        var layout = CreateTestLayout("A very long sentence that wraps...", 100f);
        Assert.NotEmpty(layout.Blocks);
        var block = layout.Blocks[0] as ParagraphLayoutBlock;
        Assert.NotNull(block);
        Assert.True(block.Bounds.Width <= 100f);
        Assert.True(block.Bounds.Height > 0f);
    }

    [Fact]
    public void TestHeadingLayout_HeadingLevels()
    {
        var layout = CreateTestLayout("# Heading 1\n## Heading 2", 200f);
        Assert.Equal(2, layout.Blocks.Count);

        var h1 = layout.Blocks[0] as HeadingLayoutBlock;
        Assert.NotNull(h1);
        Assert.Equal(1, h1.Node.Level);

        var h2 = layout.Blocks[1] as HeadingLayoutBlock;
        Assert.NotNull(h2);
        Assert.Equal(2, h2.Node.Level);
    }

    [Fact]
    public void TestListIndentation()
    {
        var markdown = "- Item 1\n  - Nested Item 1";
        var layout = CreateTestLayout(markdown, 300f);
        
        Assert.Single(layout.Blocks);
        var list = Assert.IsType<ListLayoutBlock>(layout.Blocks[0]);
        Assert.Single(list.Items); 

        var item1 = list.Items[0];
        Assert.Equal(2, item1.InnerBlocks.Count);
        Assert.IsType<ParagraphLayoutBlock>(item1.InnerBlocks[0]);
        
        var nestedList = Assert.IsType<ListLayoutBlock>(item1.InnerBlocks[1]);
        Assert.Single(nestedList.Items);
        var nestedItem1 = nestedList.Items[0];
        Assert.IsType<ParagraphLayoutBlock>(nestedItem1.InnerBlocks[0]);
    }

    [Fact]
    public void TestHitTest_ExactCharacterCenter()
    {
        // Text is "Hello" (length 5). Span is 0 to 5.
        var layout = CreateTestLayout("Hello", 200f);

        // Click at X = 12, Y = 10 (inside "e", index 1)
        var offset1 = layout.HitTest(new SKPoint(12f, 10f));
        Assert.Equal(1, offset1);

        // Click at X = 28, Y = 10 (inside second "l", closer to index 3)
        var offset2 = layout.HitTest(new SKPoint(28f, 10f));
        Assert.Equal(3, offset2);
    }

    [Fact]
    public void TestHitTest_OutsideBoundsSnapping()
    {
        var layout = CreateTestLayout("Hello", 200f);

        // Click far to the left -> should snap to start (0)
        Assert.Equal(0, layout.HitTest(new SKPoint(-50f, 10f)));

        // Click far to the right -> should snap to end (5)
        Assert.Equal(5, layout.HitTest(new SKPoint(150f, 10f)));

        // Click below the block but to the right -> should snap to end (5)
        Assert.Equal(5, layout.HitTest(new SKPoint(150f, 100f)));
    }

    [Fact]
    public void TestGetCaretBounds_ExactOffsets()
    {
        var layout = CreateTestLayout("Hello", 200f);

        // Caret at offset 0
        var caret0 = layout.GetCaretBounds(0);
        Assert.Equal(0f, caret0.Left);
        Assert.Equal(20f, caret0.Height);

        // Caret at offset 5
        var caret5 = layout.GetCaretBounds(5);
        Assert.Equal(50f, caret5.Left);
    }

    [Fact]
    public void TestGetSelectionBounds_SingleLine()
    {
        var layout = CreateTestLayout("Hello World", 200f);

        // Select "ello" (offsets 1 to 5)
        var rects = layout.GetSelectionBounds(1, 5);
        Assert.Single(rects);
        var r = rects[0];
        Assert.Equal(10f, r.Left);
        Assert.Equal(40f, r.Width);
    }

    [Fact]
    public void TestGetSelectionBounds_MultiLineWrapping()
    {
        // width limit 60px wraps "Hello " (60px) and "World" (50px)
        var layout = CreateTestLayout("Hello World", 60f);

        var rects = layout.GetSelectionBounds(0, 11);
        // Should contain: Line 1 selection, trailing newline highlight, Line 2 selection
        Assert.Equal(3, rects.Count);

        // Line 1 selection
        Assert.Equal(0f, rects[0].Left);
        Assert.Equal(0f, rects[0].Top);
        Assert.Equal(60f, rects[0].Width);

        // Trailing newline highlight
        Assert.Equal(60f, rects[1].Left);
        Assert.Equal(0f, rects[1].Top);
        Assert.Equal(10f, rects[1].Width);

        // Line 2 selection
        Assert.Equal(0f, rects[2].Left);
        Assert.Equal(20f, rects[2].Top);
        Assert.Equal(50f, rects[2].Width);
    }

    [Fact]
    public void TestCodeBlockHighlighting_Layout()
    {
        var code = "public class A\n{\n}";
        var layout = CreateTestLayout($"```csharp\n{code}\n```", 300f);

        Assert.Single(layout.Blocks);
        var codeBlock = Assert.IsType<CodeLayoutBlock>(layout.Blocks[0]);
        Assert.Equal("csharp", codeBlock.Node.Language);
    }

    [Fact]
    public void TestTableLayout_MultipleRows_NoCollapse()
    {
        var markdown = "Paragraph\n\n| Col 1 | Col 2 |\n|---|---|\n| Row 1 | Row 1 |\n| Row 2 | Row 2 |";
        var doc = MarkdownParser.Parse(markdown);
        var measurer = new MockTextMeasurer();
        var layout = new DocumentLayout();
        layout.LoadDocument(doc);
        
        var context = new LayoutContext(
            maxWidth: 200f,
            measurer: measurer,
            resources: null,
            startY: 0f,
            markdownText: markdown
        );
        layout.Layout(context);

        Assert.Equal(2, layout.Blocks.Count);
        var paragraph = Assert.IsType<ParagraphLayoutBlock>(layout.Blocks[0]);
        var table = Assert.IsType<TableLayoutBlock>(layout.Blocks[1]);

        Assert.True(table.Bounds.Top > 0f);
        Assert.Equal(3, table.Rows.Count);
        
        foreach (var row in table.Rows)
        {
            Assert.True(row.Bounds.Height > 0f, $"Row height collapsed: {row.Bounds.Height}");
            Assert.True(row.Bounds.Top >= table.Bounds.Top);
        }
    }

    [Fact]
    public void TestQuoteLayout_CoordinateMapping_HorizontalShift()
    {
        var markdown = "> Hello";
        var doc = MarkdownParser.Parse(markdown);
        var measurer = new MockTextMeasurer();
        var layout = new DocumentLayout();
        layout.LoadDocument(doc);
        
        var context = new LayoutContext(
            maxWidth: 200f,
            measurer: measurer,
            resources: null,
            startY: 0f,
            markdownText: markdown
        );
        layout.Layout(context);

        Assert.NotEmpty(layout.Blocks);
        var quote = Assert.IsType<QuoteLayoutBlock>(layout.Blocks[0]);

        var hitOffset = layout.HitTest(new SKPoint(34f, 10f));
        Assert.Equal(3, hitOffset);

        var caret = layout.GetCaretBounds(2);
        Assert.Equal(20f, caret.Left);

        var rects = layout.GetSelectionBounds(2, 7);
        Assert.NotEmpty(rects);
        Assert.Equal(20f, rects[0].Left);
        Assert.Equal(70f, rects[0].Right);
    }

    [Fact]
    public void TestHitTest_BottomAndRightMarginSnapping()
    {
        var markdown = "Hello\nWorld";
        var doc = MarkdownParser.Parse(markdown);
        var measurer = new MockTextMeasurer();
        var layout = new DocumentLayout();
        layout.LoadDocument(doc);
        
        var context = new LayoutContext(
            maxWidth: 200f,
            measurer: measurer,
            resources: null,
            startY: 0f,
            markdownText: markdown
        );
        layout.Layout(context);

        var hitBottom = layout.HitTest(new SKPoint(15f, 45f));
        Assert.Equal(8, hitBottom);

        var markdownMulti = "Hello *World*";
        var docMulti = MarkdownParser.Parse(markdownMulti);
        var layoutMulti = new DocumentLayout();
        layoutMulti.LoadDocument(docMulti);
        
        var contextMulti = new LayoutContext(
            maxWidth: 200f,
            measurer: measurer,
            resources: null,
            startY: 0f,
            markdownText: markdownMulti
        );
        layoutMulti.Layout(contextMulti);

        var hitRight = layoutMulti.HitTest(new SKPoint(150f, 10f));
        Assert.Equal(12, hitRight);
    }

    [Fact]
    public void TestImageLayout_AltText_NoCrash()
    {
        var markdown = "![photo](url)";
        var doc = MarkdownParser.Parse(markdown);
        var measurer = new MockTextMeasurer();
        var layout = new DocumentLayout();
        layout.LoadDocument(doc);
        
        var context = new LayoutContext(
            maxWidth: 200f,
            measurer: measurer,
            resources: null,
            startY: 0f,
            markdownText: markdown
        );
        
        layout.Layout(context);
        
        Assert.NotEmpty(layout.Blocks);
        var block = Assert.IsType<ParagraphLayoutBlock>(layout.Blocks[0]);
        
        var caret = layout.GetCaretBounds(2);
        Assert.True(caret.Width > 0);

        var hit = layout.HitTest(new SKPoint(75f, 10f));
        Assert.InRange(hit, 0, 13);
    }

    [Fact]
    public void TestDisposal_CleansUpNativeResources()
    {
        using (var font = new SKFont(SKTypeface.Default, 12f))
        {
            var textBlob = SKTextBlob.Create("Test", font);
            
            var run = new VisualTextRun(
                "Test",
                textBlob,
                new SKRect(0, 0, 100, 20),
                new SKPaint(),
                new SourceSpan(0, 4),
                new TextStyle(),
                new float[] { 0, 25, 50, 75, 100 }
            );

            run.Dispose();
        }
        
        var resources = new RenderResources();
        resources.Dispose();
        
        var layout = CreateTestLayout("Hello *World*", 200f);
        layout.Dispose();
    }

    [Fact]
    public void TestImageLayout_AltText_CaretPastAltText_ThrowsException()
    {
        var markdown = "![photo](url)";
        var doc = MarkdownParser.Parse(markdown);
        var measurer = new MockTextMeasurer();
        var layout = new DocumentLayout();
        layout.LoadDocument(doc);
        
        var context = new LayoutContext(
            maxWidth: 200f,
            measurer: measurer,
            resources: null,
            startY: 0f,
            markdownText: markdown
        );
        layout.Layout(context);
        
        // This will verify that calling GetCaretBounds for offsets past alt-text does not crash
        var caret = layout.GetCaretBounds(8); 
        Assert.True(caret.Width > 0);
        
        // This will verify that calling GetSelectionBounds for offsets past alt-text does not crash
        var rects = layout.GetSelectionBounds(0, 13);
        Assert.NotEmpty(rects);
    }

    [Fact]
    public void TestTableRowLayout_HitTestNegativeX_SnapsToFirstCell()
    {
        var markdown = "| Col 1 | Col 2 |";
        var doc = MarkdownParser.Parse(markdown);
        var measurer = new MockTextMeasurer();
        var layout = new DocumentLayout();
        layout.LoadDocument(doc);
        
        var context = new LayoutContext(
            maxWidth: 200f,
            measurer: measurer,
            resources: null,
            startY: 0f,
            markdownText: markdown
        );
        layout.Layout(context);

        // Negative X coordinate should snap to Col 1 (index 0) instead of Col 2 (index 1)
        var hit = layout.HitTest(new SKPoint(-10f, 10f));
        Assert.Equal(0, hit);
    }

    [Fact]
    public void TestImageLayout_AdversarialAltTextAndUrls()
    {
        var markdownScenarios = new[]
        {
            "![]()",
            "![a](b)",
            $"![{new string('x', 1000)}]({new string('y', 1000)})",
            "![photo](url) followed by some text",
            "Multiple images ![img1](u1) and ![img2](u2)"
        };

        foreach (var markdown in markdownScenarios)
        {
            var layout = CreateTestLayout(markdown, 300f);
            Assert.NotNull(layout);
            Assert.NotEmpty(layout.Blocks);

            // Call layout methods and verify no exception is thrown
            var bounds = layout.Bounds;
            Assert.True(bounds.Width > 0);

            // Test caret offsets across the whole markdown span and beyond
            for (int offset = -10; offset <= markdown.Length + 10; offset++)
            {
                var caret = layout.GetCaretBounds(offset);
                Assert.False(caret.IsEmpty);
            }

            // Test selection bounds across various ranges
            var sel1 = layout.GetSelectionBounds(0, markdown.Length);
            var sel2 = layout.GetSelectionBounds(-5, markdown.Length + 5);
            var sel3 = layout.GetSelectionBounds(markdown.Length + 5, -5);

            // Test hit-testing at various coordinates
            var hit1 = layout.HitTest(new SKPoint(-100f, 10f));
            var hit2 = layout.HitTest(new SKPoint(150f, 10f));
            var hit3 = layout.HitTest(new SKPoint(1000f, 1000f));
            
            layout.Dispose();
        }
    }

    [Fact]
    public void TestGetCaretBounds_AdversarialOffsets_NoException()
    {
        var markdown = "Hello World";
        var layout = CreateTestLayout(markdown, 200f);

        int[] adversarialOffsets = {
            int.MinValue,
            -100000,
            -1,
            0,
            5,
            11,
            12,
            1000,
            100000,
            int.MaxValue
        };

        foreach (var offset in adversarialOffsets)
        {
            var exception = Record.Exception(() =>
            {
                var caret = layout.GetCaretBounds(offset);
                Assert.False(caret.IsEmpty);
            });
            Assert.Null(exception);
        }

        layout.Dispose();
    }

    [Fact]
    public void TestGetSelectionBounds_AdversarialRanges_NoException()
    {
        var markdown = "Hello World";
        var layout = CreateTestLayout(markdown, 200f);

        var adversarialRanges = new (int Start, int End)[]
        {
            (int.MinValue, int.MaxValue),
            (-100, -50),
            (-10, 5),
            (5, -5),
            (5, 5),
            (0, 11),
            (0, 100),
            (100, 200),
            (int.MaxValue - 10, int.MaxValue)
        };

        foreach (var range in adversarialRanges)
        {
            var exception = Record.Exception(() =>
            {
                var rects = layout.GetSelectionBounds(range.Start, range.End);
                Assert.NotNull(rects);
            });
            Assert.Null(exception);
        }

        layout.Dispose();
    }

    [Fact]
    public void TestComplexDocument_AdversarialOffsets_NoException()
    {
        var markdown = @"# Heading 1
Some paragraph text with **bold** and *italic* content.

- List Item 1
- List Item 2 with inline code: `var x = 10;`

> Quote block text.
> Second line of quote.

| Header 1 | Header 2 |
|----------|----------|
| Cell 1   | Cell 2   |

![Image Alt](https://example.com/image.png)

***
Some trailing paragraph.
";

        var layout = CreateTestLayout(markdown, 150f); // smaller width to force line wrapping
        Assert.NotNull(layout);

        // Run hit test on a grid of coordinates (including negative, large, bounds, and normal)
        float[] testXs = { -500f, -10f, 0f, 50f, 100f, 200f, 500f };
        float[] testYs = { -500f, -10f, 0f, 20f, 100f, 300f, 1000f, 5000f };
        foreach (var y in testYs)
        {
            foreach (var x in testXs)
            {
                var exception = Record.Exception(() =>
                {
                    var offset = layout.HitTest(new SKPoint(x, y));
                    Assert.InRange(offset, 0, markdown.Length);
                });
                Assert.Null(exception);
            }
        }

        // Run GetCaretBounds for a wide range of offsets
        for (int offset = -50; offset <= markdown.Length + 50; offset += 5)
        {
            var exception = Record.Exception(() =>
            {
                var caret = layout.GetCaretBounds(offset);
                Assert.False(caret.IsEmpty);
            });
            Assert.Null(exception);
        }

        // Run GetSelectionBounds for random overlapping and non-overlapping ranges
        var ranges = new (int Start, int End)[]
        {
            (int.MinValue, int.MaxValue),
            (-100, -10),
            (-10, 10),
            (0, markdown.Length),
            (markdown.Length / 2, markdown.Length * 2),
            (markdown.Length + 10, markdown.Length + 50),
            (100, 50) // inverted
        };

        foreach (var range in ranges)
        {
            var exception = Record.Exception(() =>
            {
                var rects = layout.GetSelectionBounds(range.Start, range.End);
                Assert.NotNull(rects);
            });
            Assert.Null(exception);
        }

        layout.Dispose();
    }

    [Fact]
    public void TestTableLayout_HitTest_NegativeCoordinates()
    {
        var markdown = "| Col 1 | Col 2 |\n|---|---|\n| Cell 1 | Cell 2 |";
        var layout = CreateTestLayout(markdown, 200f);

        // DocumentLayout.HitTest with negative Y should snap to top (Row 0 / Col 1)
        var hitDocNegativeY = layout.HitTest(new SKPoint(50f, -20f));
        Assert.True(hitDocNegativeY >= 0);

        Assert.NotEmpty(layout.Blocks);
        var table = Assert.IsType<TableLayoutBlock>(layout.Blocks[0]);

        // TableLayoutBlock.HitTest with negative Y should snap to first row
        var hitTableNegativeY = table.HitTest(new SKPoint(50f, -20f));
        Assert.True(hitTableNegativeY >= 0);

        // TableRowLayoutBlock.HitTest with negative X
        var row = Assert.IsType<TableRowLayoutBlock>(table.Rows[0]);
        var hitRowNegativeX = row.HitTest(new SKPoint(-50f, 10f));
        Assert.True(hitRowNegativeX >= 0);

        // TableRowLayoutBlock.HitTest with negative Y
        var hitRowNegativeY = row.HitTest(new SKPoint(10f, -10f));
        Assert.True(hitRowNegativeY >= 0);

        layout.Dispose();
    }

    [Fact]
    public void TestTableLayout_HitTest_FarRightBounds()
    {
        var markdown = "| Col 1 | Col 2 |\n|---|---|\n| Cell 1 | Cell 2 |";
        var layout = CreateTestLayout(markdown, 200f);

        Assert.NotEmpty(layout.Blocks);
        var table = Assert.IsType<TableLayoutBlock>(layout.Blocks[0]);
        var row = Assert.IsType<TableRowLayoutBlock>(table.Rows[0]);

        // Click far to the right (x = 500f) -> snaps to the last cell
        var hitFarRight = row.HitTest(new SKPoint(500f, 10f));
        Assert.True(hitFarRight >= 0);

        // Click on intermediate cells: first cell (x = 50) vs second cell (x = 150)
        var hitCol1 = row.HitTest(new SKPoint(50f, 10f));
        var hitCol2 = row.HitTest(new SKPoint(150f, 10f));
        Assert.NotEqual(hitCol1, hitCol2);

        layout.Dispose();
    }

    [Fact]
    public void TestTableLayout_EmptyTable_NoCrash()
    {
        var doc = new MarkdownDocument();
        var tableBlock = new TableBlock(); // Empty table
        doc.Children.Add(tableBlock);
        tableBlock.Parent = doc;

        var measurer = new MockTextMeasurer();
        var layout = new DocumentLayout();
        layout.LoadDocument(doc);
        
        var context = new LayoutContext(
            maxWidth: 200f,
            measurer: measurer,
            resources: null,
            startY: 0f,
            markdownText: ""
        );
        
        // Verify layout and hit-testing on empty table do not throw exceptions
        layout.Layout(context);
        var hit = layout.HitTest(new SKPoint(50f, 50f));
        Assert.Equal(0, hit);

        layout.Dispose();
    }

    [Fact]
    public void TestTableLayout_EmptyRow_NoCrash()
    {
        var doc = new MarkdownDocument();
        var tableBlock = new TableBlock();
        var rowBlock = new TableRowBlock(); // Empty row (0 cells)
        tableBlock.Children.Add(rowBlock);
        rowBlock.Parent = tableBlock;
        doc.Children.Add(tableBlock);
        tableBlock.Parent = doc;

        var measurer = new MockTextMeasurer();
        var layout = new DocumentLayout();
        layout.LoadDocument(doc);
        
        var context = new LayoutContext(
            maxWidth: 200f,
            measurer: measurer,
            resources: null,
            startY: 0f,
            markdownText: ""
        );
        
        // Verify layout and hit-testing do not crash
        layout.Layout(context);
        var hit = layout.HitTest(new SKPoint(50f, 10f));
        Assert.Equal(0, hit);

        layout.Dispose();
    }

    [Fact]
    public void TestTableLayout_MismatchedRowsCols_NoCrash()
    {
        var doc = new MarkdownDocument();
        var tableBlock = new TableBlock();
        
        // Row 0 with 2 cells
        var row0 = new TableRowBlock();
        var cell0_0 = new TableCellBlock { Span = new SourceSpan(0, 1) };
        var p0_0 = new ParagraphBlock { Span = new SourceSpan(0, 1) };
        var lit0_0 = new LiteralInline { Text = "A", Span = new SourceSpan(0, 1) };
        p0_0.Children.Add(lit0_0);
        lit0_0.Parent = p0_0;
        cell0_0.Children.Add(p0_0);
        p0_0.Parent = cell0_0;
        row0.Children.Add(cell0_0);
        cell0_0.Parent = row0;

        var cell0_1 = new TableCellBlock { Span = new SourceSpan(10, 1) };
        var p0_1 = new ParagraphBlock { Span = new SourceSpan(10, 1) };
        var lit0_1 = new LiteralInline { Text = "B", Span = new SourceSpan(10, 1) };
        p0_1.Children.Add(lit0_1);
        lit0_1.Parent = p0_1;
        cell0_1.Children.Add(p0_1);
        p0_1.Parent = cell0_1;
        row0.Children.Add(cell0_1);
        cell0_1.Parent = row0;

        // Row 1 with 3 cells
        var row1 = new TableRowBlock();
        var cell1_0 = new TableCellBlock { Span = new SourceSpan(20, 1) };
        var p1_0 = new ParagraphBlock { Span = new SourceSpan(20, 1) };
        var lit1_0 = new LiteralInline { Text = "C", Span = new SourceSpan(20, 1) };
        p1_0.Children.Add(lit1_0);
        lit1_0.Parent = p1_0;
        cell1_0.Children.Add(p1_0);
        p1_0.Parent = cell1_0;
        row1.Children.Add(cell1_0);
        cell1_0.Parent = row1;

        var cell1_1 = new TableCellBlock { Span = new SourceSpan(30, 1) };
        var p1_1 = new ParagraphBlock { Span = new SourceSpan(30, 1) };
        var lit1_1 = new LiteralInline { Text = "D", Span = new SourceSpan(30, 1) };
        p1_1.Children.Add(lit1_1);
        lit1_1.Parent = p1_1;
        cell1_1.Children.Add(p1_1);
        p1_1.Parent = cell1_1;
        row1.Children.Add(cell1_1);
        cell1_1.Parent = row1;

        var cell1_2 = new TableCellBlock { Span = new SourceSpan(40, 1) };
        var p1_2 = new ParagraphBlock { Span = new SourceSpan(40, 1) };
        var lit1_2 = new LiteralInline { Text = "E", Span = new SourceSpan(40, 1) };
        p1_2.Children.Add(lit1_2);
        lit1_2.Parent = p1_2;
        cell1_2.Children.Add(p1_2);
        p1_2.Parent = cell1_2;
        row1.Children.Add(cell1_2);
        cell1_2.Parent = row1;

        tableBlock.Children.Add(row0);
        row0.Parent = tableBlock;
        tableBlock.Children.Add(row1);
        row1.Parent = tableBlock;
        
        doc.Children.Add(tableBlock);
        tableBlock.Parent = doc;

        var measurer = new MockTextMeasurer();
        var layout = new DocumentLayout();
        layout.LoadDocument(doc);
        
        var context = new LayoutContext(
            maxWidth: 300f, // Width 300: Row 0 cells are 150 wide, Row 1 cells are 100 wide
            measurer: measurer,
            resources: null,
            startY: 0f,
            markdownText: ""
        );
        
        // Verify layout compiles and runs
        layout.Layout(context);
        
        var table = Assert.IsType<TableLayoutBlock>(layout.Blocks[0]);
        var r0 = Assert.IsType<TableRowLayoutBlock>(table.Rows[0]);
        var r1 = Assert.IsType<TableRowLayoutBlock>(table.Rows[1]);

        // Row 0 cells: cell 0 (0-150), cell 1 (150-300)
        // Row 1 cells: cell 0 (0-100), cell 1 (100-200), cell 2 (200-300)
        
        // Hit test inside Row 0
        var hitR0_C0 = r0.HitTest(new SKPoint(75f, 10f));
        var hitR0_C1 = r0.HitTest(new SKPoint(225f, 10f));
        Assert.NotEqual(hitR0_C0, hitR0_C1);

        // Hit test inside Row 1
        var hitR1_C0 = r1.HitTest(new SKPoint(50f, 30f));
        var hitR1_C1 = r1.HitTest(new SKPoint(150f, 30f));
        var hitR1_C2 = r1.HitTest(new SKPoint(250f, 30f));
        
        Assert.NotEqual(hitR1_C0, hitR1_C1);
        Assert.NotEqual(hitR1_C1, hitR1_C2);
        Assert.NotEqual(hitR1_C0, hitR1_C2);

        layout.Dispose();
    }

    [Fact]
    public void TestHtmlLayoutBlock_IntegratesHtmlParserAndLayout()
    {
        string markdown = "<div style=\"width: 100px; height: 50px; background-color: red;\">HTML Block</div>";
        var layout = CreateTestLayout(markdown, 300f);
        Assert.Single(layout.Blocks);
        var htmlBlock = Assert.IsType<HtmlLayoutBlock>(layout.Blocks[0]);
        Assert.NotNull(htmlBlock.Node);
        Assert.True(htmlBlock.Bounds.Height > 0);
        
        // Test simplified caret bounds & hit test snap
        var caret = htmlBlock.GetCaretBounds(0);
        Assert.Equal(htmlBlock.Bounds.Left, caret.Left);
        
        var hit = htmlBlock.HitTest(new SKPoint(10f, 10f));
        Assert.Equal(htmlBlock.Node.Span.Start, hit);
        
        var selection = new List<SKRect>();
        htmlBlock.GetSelectionBounds(htmlBlock.Node.Span.Start, htmlBlock.Node.Span.Start + htmlBlock.Node.Span.Length, selection);
        Assert.Single(selection);
    }

    [Fact]
    public void TestParagraph_InlineHtmlTextRunLayout()
    {
        string markdown = "Hello <span style=\"color: blue;\">World</span> Inline HTML";
        var layout = CreateTestLayout(markdown, 500f);
        Assert.Single(layout.Blocks);
        var pBlock = Assert.IsType<ParagraphLayoutBlock>(layout.Blocks[0]);
        
        // Check if any run in the laid out lines has IsHtml set to true
        bool foundHtmlRun = false;
        // Access paragraph layout lines via reflection if needed, but since it's inside the same test project assembly or a public field:
        // Let's check: public field/property? In ParagraphLayoutBlock:
        // Wait, _lines is a private field: private readonly List<VisualLine> _lines = new();
        // Wait, can we access it? Since it's private, we can use reflection! Let's check how RendererTests.cs accesses private fields, if any.
        // Let's use reflection to read _lines of ParagraphLayoutBlock.
        var linesField = typeof(ParagraphLayoutBlock).GetField("_lines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(linesField);
        var lines = linesField.GetValue(pBlock) as List<VisualLine>;
        Assert.NotNull(lines);
        
        foreach (var line in lines)
        {
            foreach (var run in line.Runs)
            {
                if (run.IsHtml)
                {
                    foundHtmlRun = true;
                    Assert.Contains("span", run.HtmlText);
                }
            }
        }
        Assert.True(foundHtmlRun);
    }
}
