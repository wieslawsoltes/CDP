using Xunit;
using SkiaSharp;
using CDP.Html.Parser;
using CDP.Css.Parser;
using CDP.Html.Renderer.Layout;
using CDP.Html.Renderer.Style;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace CDP.Html.Renderer.Tests;

public class HtmlRendererStressTests
{
    [Fact]
    public void TestDeepNestingBfc()
    {
        // 1. Arrange: Create a highly nested tree of block divs
        var doc = new HtmlDocument();
        HtmlElement currentElement = new HtmlElement { TagName = "div" };
        doc.Children.Add(currentElement);
        currentElement.Parent = doc;
        currentElement.Attributes["style"] = "padding: 2px; margin: 1px; border: 1px solid black;";

        int nestingDepth = 150;
        for (int i = 0; i < nestingDepth; i++)
        {
            var child = new HtmlElement { TagName = "div" };
            child.Attributes["style"] = "padding: 2px; margin: 1px; border: 1px solid black;";
            currentElement.Children.Add(child);
            child.Parent = currentElement;
            currentElement = child;
        }

        // Add a text node at the deepest level
        var text = new HtmlTextNode { Text = "deep text" };
        currentElement.Children.Add(text);
        text.Parent = currentElement;

        // 2. Act: Resolve styles and compute layout
        var styles = StyleCascade.ResolveStyles(doc, null);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(layoutTree.Children[0], 1000f, 1000f);

        // 3. Assert: Verify the hierarchy exists and was positioned without stack overflow
        Assert.NotNull(layoutTree);
        var rootBox = layoutTree.Children[0];
        Assert.NotNull(rootBox);

        // Traverse down the layout tree
        var currBox = rootBox;
        for (int i = 0; i < nestingDepth; i++)
        {
            Assert.NotEmpty(currBox.Children);
            currBox = currBox.Children[0];
        }
        Assert.True(currBox.Children[0] is LayoutTextBox);
    }

    [Fact]
    public void TestDeepNestingIfc()
    {
        // 1. Arrange: Create a block container with highly nested spans (IFC elements)
        var doc = new HtmlDocument();
        var div = new HtmlElement { TagName = "div" };
        doc.Children.Add(div);
        div.Parent = doc;
        div.Attributes["style"] = "width: 300px;";

        HtmlElement currentSpan = new HtmlElement { TagName = "span" };
        div.Children.Add(currentSpan);
        currentSpan.Parent = div;

        int nestingDepth = 80;
        for (int i = 0; i < nestingDepth; i++)
        {
            var childSpan = new HtmlElement { TagName = "span" };
            currentSpan.Children.Add(childSpan);
            childSpan.Parent = currentSpan;
            currentSpan = childSpan;
        }

        var text = new HtmlTextNode { Text = "nested inline text" };
        currentSpan.Children.Add(text);
        text.Parent = currentSpan;

        // 2. Act: Resolve styles and compute layout
        var styles = StyleCascade.ResolveStyles(doc, null);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(layoutTree.Children[0], 300f, 1000f);

        // 3. Assert: Layout runs successfully and creates line boxes for the text
        Assert.NotNull(layoutTree);
        var divBox = layoutTree.Children[0];
        Assert.NotEmpty(divBox.LineBoxes);
        var reconstructedText = string.Join("", divBox.LineBoxes.SelectMany(l => l.Fragments).Select(f => f.Text));
        Assert.Contains("nested inline text", reconstructedText);
    }

    [Fact]
    public void TestVeryLongWordCharacterSplitting()
    {
        // 1. Arrange: Text content containing an extremely long word that exceeds the container boundary
        var doc = new HtmlDocument();
        var div = new HtmlElement { TagName = "div" };
        var text = new HtmlTextNode { Text = "supercalifragilisticexpialidocious" };
        doc.Children.Add(div);
        div.Parent = doc;
        div.Children.Add(text);
        text.Parent = div;

        // Constraint the div width extremely narrow (30px) to force character splitting
        var css = "div { width: 30px; font-size: 14px; font-family: Arial; }";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        // 2. Act: Run layout
        var divBox = layoutTree.Children[0];
        LayoutEngine.Layout(divBox, 30f, 1000f);

        // 3. Assert: Ensure the word is split across multiple line boxes
        Assert.True(divBox.LineBoxes.Count > 1, $"Expected multiple lines, got {divBox.LineBoxes.Count}");

        // Reconstruct the text from line fragments to verify no characters were lost
        var fragmentsList = divBox.LineBoxes.SelectMany(l => l.Fragments).Select(f => $"'{f.Text}'").ToList();
        var fragmentsStr = string.Join(", ", fragmentsList);
        var reconstructed = string.Join("", divBox.LineBoxes.SelectMany(l => l.Fragments).Select(f => f.Text));
        
        // Print diagnostics to standard output
        Console.WriteLine($"DEBUG: Original Text: '{text.Text}', Length: {text.Text.Length}");
        Console.WriteLine($"DEBUG: Reconstructed: '{reconstructed}', Length: {reconstructed.Length}");
        Console.WriteLine($"DEBUG: Fragments count: {divBox.LineBoxes.SelectMany(l => l.Fragments).Count()}");
        foreach (var frag in divBox.LineBoxes.SelectMany(l => l.Fragments))
        {
            Console.WriteLine($"DEBUG: Fragment: '{frag.Text}', X={frag.X}, Width={frag.Width}");
        }

        // Reconstructed text should be exactly the original word
        Assert.Equal("supercalifragilisticexpialidocious", reconstructed);
    }

    [Fact]
    public void TestFlexboxComplexGrowShrinkWrap()
    {
        // --- PART 1: Wrap + Grow ---
        var doc = new HtmlDocument();
        var flex = new HtmlElement { TagName = "div" };
        var item1 = new HtmlElement { TagName = "div" };
        var item2 = new HtmlElement { TagName = "div" };
        var item3 = new HtmlElement { TagName = "div" };
        var item4 = new HtmlElement { TagName = "div" };

        doc.Children.Add(flex);
        flex.Parent = doc;
        flex.Children.Add(item1);
        item1.Parent = flex;
        flex.Children.Add(item2);
        item2.Parent = flex;
        flex.Children.Add(item3);
        item3.Parent = flex;
        flex.Children.Add(item4);
        item4.Parent = flex;

        var css = @"
            .flex-container { display: flex; flex-direction: row; flex-wrap: wrap; width: 300px; }
            .item1 { width: 150px; flex-grow: 1; }
            .item2 { width: 200px; flex-grow: 1; }
            .item3 { width: 100px; flex-grow: 2; }
            .item4 { width: 250px; flex-grow: 1; }
        ";
        flex.Attributes["class"] = "flex-container";
        item1.Attributes["class"] = "item1";
        item2.Attributes["class"] = "item2";
        item3.Attributes["class"] = "item3";
        item4.Attributes["class"] = "item4";

        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var flexBox = layoutTree.Children[0];
        LayoutEngine.Layout(flexBox, 300f, 1000f);

        var box1 = flexBox.Children[0];
        var box2 = flexBox.Children[1];
        var box3 = flexBox.Children[2];
        var box4 = flexBox.Children[3];

        // Line 1: Item 1 takes 150px. Remaining space = 300 - 150 = 150px.
        // flex-grow = 1, so Item 1 grows to 150 + 150 = 300px.
        Assert.Equal(300f, box1.Width);

        // Line 2: Item 2 (200px) + Item 3 (100px) = 300px. Remaining space = 300 - 300 = 0px.
        // No grow/shrink needed.
        Assert.Equal(200f, box2.Width);
        Assert.Equal(100f, box3.Width);

        // Line 3: Item 4 takes 250px. Remaining space = 300 - 250 = 50px.
        // flex-grow = 1, so Item 4 grows to 250 + 50 = 300px.
        Assert.Equal(300f, box4.Width);

        // --- PART 2: Shrink with Wrap = nowrap ---
        var docShrink = new HtmlDocument();
        var flexShrink = new HtmlElement { TagName = "div" };
        var sitem1 = new HtmlElement { TagName = "div" };
        var sitem2 = new HtmlElement { TagName = "div" };

        docShrink.Children.Add(flexShrink);
        flexShrink.Parent = docShrink;
        flexShrink.Children.Add(sitem1);
        sitem1.Parent = flexShrink;
        flexShrink.Children.Add(sitem2);
        sitem2.Parent = flexShrink;

        var cssShrink = @"
            .flex-container { display: flex; flex-direction: row; flex-wrap: nowrap; width: 300px; }
            .item1 { width: 200px; flex-shrink: 1; }
            .item2 { width: 200px; flex-shrink: 3; }
        ";
        flexShrink.Attributes["class"] = "flex-container";
        sitem1.Attributes["class"] = "item1";
        sitem2.Attributes["class"] = "item2";

        var stylesheetShrink = CssParser.Parse(cssShrink);
        var stylesShrink = StyleCascade.ResolveStyles(docShrink, stylesheetShrink);
        var layoutTreeShrink = LayoutTreeBuilder.Build(docShrink, stylesShrink);

        var flexBoxShrink = layoutTreeShrink.Children[0];
        LayoutEngine.Layout(flexBoxShrink, 300f, 1000f);

        var sbox1 = flexBoxShrink.Children[0];
        var sbox2 = flexBoxShrink.Children[1];

        // Total hypothetical size = 200 + 200 = 400px.
        // Underflow (free space) = 300 - 400 = -100px.
        // Weighted shrink factor:
        // sbox1 factor = 1 * 200 = 200.
        // sbox2 factor = 3 * 200 = 600.
        // Total factor = 800.
        // Shrink amount:
        // sbox1 = -100 * (200 / 800) = -25px. -> Width = 200 - 25 = 175px.
        // sbox2 = -100 * (600 / 800) = -75px. -> Width = 200 - 75 = 125px.
        Assert.Equal(175f, sbox1.Width);
        Assert.Equal(125f, sbox2.Width);
    }

    [Fact]
    public void TestLargeStyleCascadeRules()
    {
        // 1. Arrange: Create a document and a target element
        var doc = new HtmlDocument();
        var body = new HtmlElement { TagName = "body" };
        doc.Children.Add(body);
        body.Parent = doc;

        var div = new HtmlElement { TagName = "div" };
        div.Attributes["id"] = "target";
        div.Attributes["class"] = "box active highlighted container special";
        body.Children.Add(div);
        div.Parent = body;

        // Generate 1500 stylesheet rules to stress matching & specificity engine
        var sb = new StringBuilder();
        for (int i = 0; i < 1490; i++)
        {
            sb.AppendLine($".nonexistent-class-{i} {{ color: rgb({i % 255}, 0, 0); font-size: {10 + (i % 20)}px; }}");
        }

        // Add rules with varied specificity matching our target element
        sb.AppendLine("div { color: rgb(0, 0, 10); font-size: 10px; }"); // Specificity: (0,0,1)
        sb.AppendLine(".box { color: rgb(0, 0, 20); font-size: 12px; }"); // Specificity: (0,1,0)
        sb.AppendLine(".box.active { color: rgb(0, 0, 30); font-size: 14px; }"); // Specificity: (0,2,0)
        sb.AppendLine(".box.active.highlighted { color: rgb(0, 0, 40); font-size: 16px; }"); // Specificity: (0,3,0)
        sb.AppendLine("body > div#target { color: rgb(0, 0, 50); font-size: 18px; }"); // Specificity: (1,0,2) - WINNER
        sb.AppendLine("#target { color: rgb(0, 0, 60); font-size: 20px; }"); // Specificity: (1,0,0)
        sb.AppendLine("body div.box.active.highlighted { color: rgb(0, 0, 70); font-size: 22px; }"); // Specificity: (0,3,2)

        var stylesheet = CssParser.Parse(sb.ToString());

        // 2. Act: Measure style resolution performance
        var stopwatch = Stopwatch.StartNew();
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        stopwatch.Stop();

        // 3. Assert: Style resolution finishes quickly and selects correct specificity winner
        Assert.True(stopwatch.ElapsedMilliseconds < 1500, $"Cascade resolution took too long: {stopwatch.ElapsedMilliseconds}ms");
        Assert.True(styles.ContainsKey(div));

        var divStyle = styles[div];
        // body > div#target (1, 0, 2) has higher specificity than #target (1, 0, 0) and any other rules.
        Assert.Equal(18f, divStyle.FontSize);
        Assert.Equal(new SKColor(0, 0, 50), divStyle.Color);
    }

    [Fact]
    public void TestTypefaceDisposalAndGraphicsBufferMemoryLeak()
    {
        // 1. Arrange
        var doc = new HtmlDocument();
        var div = new HtmlElement { TagName = "div" };
        var text = new HtmlTextNode { Text = "Memory leak test typeface disposal" };
        doc.Children.Add(div);
        div.Parent = doc;
        div.Children.Add(text);
        text.Parent = div;

        var css = "div { width: 100px; padding-left: 10px; color: red; font-family: Courier; }";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);
        var divBox = layoutTree.Children[0];
        LayoutEngine.Layout(divBox, 100f, 100f);

        // Warm up
        for (int i = 0; i < 10; i++)
        {
            using var bitmap = new SKBitmap(300, 200);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            HtmlRenderer.Render(divBox, canvas, 0f, 0f);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        long initialMemory = GC.GetTotalMemory(true);

        // Act: Run 1000 rendering iterations to verify no leak
        for (int i = 0; i < 1000; i++)
        {
            using var bitmap = new SKBitmap(300, 200);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            HtmlRenderer.Render(divBox, canvas, 0f, 0f);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        long finalMemory = GC.GetTotalMemory(true);

        // Assert: Verify no massive memory growth
        long diff = finalMemory - initialMemory;
        Assert.True(diff < 2 * 1024 * 1024, $"Memory leak detected: {diff} bytes leaked.");
    }
}

