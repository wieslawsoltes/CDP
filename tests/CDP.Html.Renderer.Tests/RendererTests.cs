using Xunit;
using SkiaSharp;
using CDP.Html.Parser;
using CDP.Css.Parser;
using CDP.Html.Renderer.Layout;
using CDP.Html.Renderer.Style;
using System.Collections.Generic;

namespace CDP.Html.Renderer.Tests;

public class RendererTests
{
    [Fact]
    public void TestStyleCascadeAndSpecificity()
    {
        // 1. Arrange
        var doc = new HtmlDocument();
        var body = new HtmlElement { TagName = "body" };
        var div = new HtmlElement { TagName = "div" };
        div.Attributes["id"] = "main";
        div.Attributes["class"] = "box active";
        div.Attributes["style"] = "background-color: red;";

        doc.Children.Add(body);
        body.Parent = doc;
        body.Children.Add(div);
        div.Parent = body;

        var css = @"
            div { color: white; background-color: blue; }
            .box { color: yellow; font-size: 20px; }
            #main { color: red; font-size: 24px; }
        ";
        var stylesheet = CssParser.Parse(css);

        // 2. Act
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);

        // 3. Assert
        Assert.True(styles.ContainsKey(div));
        var divStyle = styles[div];

        // Specificity check: #main (1,0,0) beats .box (0,1,0) and div (0,0,1)
        Assert.Equal(SKColors.Red, divStyle.Color);
        Assert.Equal(24f, divStyle.FontSize);

        // Inline style check: style="..." beats #main
        Assert.Equal(SKColors.Red, divStyle.BackgroundColor);
    }

    [Fact]
    public void TestStyleInheritance()
    {
        var doc = new HtmlDocument();
        var body = new HtmlElement { TagName = "body" };
        var div = new HtmlElement { TagName = "div" };
        var text = new HtmlTextNode { Text = "hello" };

        doc.Children.Add(body);
        body.Parent = doc;
        body.Children.Add(div);
        div.Parent = body;
        div.Children.Add(text);
        text.Parent = div;

        var css = "body { color: blue; font-size: 20px; }";
        var stylesheet = CssParser.Parse(css);

        var styles = StyleCascade.ResolveStyles(doc, stylesheet);

        // Assert parent inherited
        var bodyStyle = styles[body];
        Assert.Equal(SKColors.Blue, bodyStyle.Color);
        Assert.Equal(20f, bodyStyle.FontSize);

        var divStyle = styles[div];
        Assert.Equal(SKColors.Blue, divStyle.Color);
        Assert.Equal(20f, divStyle.FontSize);

        var textStyle = styles[text];
        Assert.Equal(SKColors.Blue, textStyle.Color);
        Assert.Equal(20f, textStyle.FontSize);
    }

    [Fact]
    public void TestLayoutBoxTreeAnonymousBlockWrappers()
    {
        var doc = new HtmlDocument();
        var body = new HtmlElement { TagName = "body" };
        var div1 = new HtmlElement { TagName = "div" }; // Block
        var text = new HtmlTextNode { Text = "some text" }; // Inline text
        var div2 = new HtmlElement { TagName = "div" }; // Block

        doc.Children.Add(body);
        body.Parent = doc;
        body.Children.Add(div1);
        div1.Parent = body;
        body.Children.Add(text);
        text.Parent = body;
        body.Children.Add(div2);
        div2.Parent = body;

        var styles = StyleCascade.ResolveStyles(doc, null);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var bodyBox = layoutTree.Children[0];

        Assert.Equal(3, bodyBox.Children.Count);
        Assert.True(bodyBox.Children[0].IsBlockLevel);
        Assert.False(bodyBox.Children[0].IsAnonymous);

        Assert.True(bodyBox.Children[1].IsBlockLevel);
        Assert.True(bodyBox.Children[1].IsAnonymous);
        Assert.Single(bodyBox.Children[1].Children);
        Assert.True(bodyBox.Children[1].Children[0] is LayoutTextBox);

        Assert.True(bodyBox.Children[2].IsBlockLevel);
        Assert.False(bodyBox.Children[2].IsAnonymous);
    }

    [Fact]
    public void TestLayoutDimensionsAndMarginCollapsing()
    {
        var doc = new HtmlDocument();
        var container = new HtmlElement { TagName = "div" };
        var child1 = new HtmlElement { TagName = "div" };
        var child2 = new HtmlElement { TagName = "div" };

        doc.Children.Add(container);
        container.Parent = doc;
        container.Children.Add(child1);
        child1.Parent = container;
        container.Children.Add(child2);
        child2.Parent = container;

        var css = @"
            .container { width: 500px; padding: 10px; border: 2px solid black; }
            .c1 { height: 100px; margin-bottom: 20px; }
            .c2 { height: 150px; margin-top: 30px; }
        ";
        container.Attributes["class"] = "container";
        child1.Attributes["class"] = "c1";
        child2.Attributes["class"] = "c2";

        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var containerBox = layoutTree.Children[0];
        LayoutEngine.Layout(containerBox, 800f, 600f);

        Assert.Equal(524f, containerBox.Width);
        Assert.Equal(500f, containerBox.ContentWidth);

        var box1 = containerBox.Children[0];
        var box2 = containerBox.Children[1];

        Assert.Equal(100f, box1.Height);
        Assert.Equal(150f, box2.Height);

        // Vertical margin collapsing:
        // box1 margin-bottom is 20, box2 margin-top is 30. Collapsed margin = 30.
        // box1 Y starts at container paddingtop + border-top = 10 + 2 = 12.
        Assert.Equal(12f, box1.Y);
        // box1 ends at 12 + 100 = 112.
        // box2 Y starts at 112 + 30 (collapsed margin) = 142.
        Assert.Equal(142f, box2.Y);
    }

    [Fact]
    public void TestTextWrappingAndBaselines()
    {
        var doc = new HtmlDocument();
        var div = new HtmlElement { TagName = "div" };
        var text = new HtmlTextNode { Text = "hello world this is a test of wrapping long words verylongwordthatwillwrap" };

        doc.Children.Add(div);
        div.Parent = doc;
        div.Children.Add(text);
        text.Parent = div;

        var css = "div { width: 100px; font-size: 16px; font-family: Arial; }";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var divBox = layoutTree.Children[0];
        LayoutEngine.Layout(divBox, 100f, 600f);

        Assert.NotEmpty(divBox.LineBoxes);
        Assert.True(divBox.LineBoxes.Count > 1);

        foreach (var line in divBox.LineBoxes)
        {
            Assert.True(line.Height > 0);
            Assert.True(line.Baseline > 0);
        }
    }

    [Fact]
    public void TestFlexboxLayoutRow()
    {
        var doc = new HtmlDocument();
        var flex = new HtmlElement { TagName = "div" };
        var item1 = new HtmlElement { TagName = "div" };
        var item2 = new HtmlElement { TagName = "div" };

        doc.Children.Add(flex);
        flex.Parent = doc;
        flex.Children.Add(item1);
        item1.Parent = flex;
        flex.Children.Add(item2);
        item2.Parent = flex;

        var css = @"
            .flex-container { display: flex; flex-direction: row; width: 400px; justify-content: space-between; }
            .item { width: 100px; height: 50px; }
        ";
        flex.Attributes["class"] = "flex-container";
        item1.Attributes["class"] = "item";
        item2.Attributes["class"] = "item";

        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var flexBox = layoutTree.Children[0];
        LayoutEngine.Layout(flexBox, 800f, 600f);

        var box1 = flexBox.Children[0];
        var box2 = flexBox.Children[1];

        Assert.Equal(0f, box1.X);
        Assert.Equal(300f, box2.X);
        Assert.Equal(50f, box1.Height);
        Assert.Equal(50f, box2.Height);
    }

    [Fact]
    public void TestFlexboxLayoutGrowAndShrink()
    {
        var doc = new HtmlDocument();
        var flex = new HtmlElement { TagName = "div" };
        var item1 = new HtmlElement { TagName = "div" };
        var item2 = new HtmlElement { TagName = "div" };

        doc.Children.Add(flex);
        flex.Parent = doc;
        flex.Children.Add(item1);
        item1.Parent = flex;
        flex.Children.Add(item2);
        item2.Parent = flex;

        var css = @"
            .flex-container { display: flex; flex-direction: row; width: 300px; }
            .item1 { width: 100px; flex-grow: 1; }
            .item2 { width: 100px; flex-grow: 2; }
        ";
        flex.Attributes["class"] = "flex-container";
        item1.Attributes["class"] = "item1";
        item2.Attributes["class"] = "item2";

        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var flexBox = layoutTree.Children[0];
        LayoutEngine.Layout(flexBox, 800f, 600f);

        var box1 = flexBox.Children[0];
        var box2 = flexBox.Children[1];

        Assert.InRange(box1.Width, 133f, 134f);
        Assert.InRange(box2.Width, 166f, 167f);
    }

    [Fact]
    public void TestRenderingDrawLoop()
    {
        var doc = new HtmlDocument();
        var div = new HtmlElement { TagName = "div" };
        div.Attributes["style"] = "background-color: red; border: 2px solid blue; height: 100px;";

        doc.Children.Add(div);
        div.Parent = doc;

        var styles = StyleCascade.ResolveStyles(doc, null);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);
        var box = layoutTree.Children[0];
        LayoutEngine.Layout(box, 200f, 100f);

        using var bitmap = new SKBitmap(300, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        HtmlRenderer.Render(box, canvas, 0f, 0f);

        var pixel = bitmap.GetPixel(10, 10);
        Assert.Equal(SKColors.Red, pixel);

        var borderPixel = bitmap.GetPixel(1, 1);
        Assert.Equal(SKColors.Blue, borderPixel);
    }

    [Fact]
    public void TestNegativePaddingAndMargins()
    {
        var doc = new HtmlDocument();
        var container = new HtmlElement { TagName = "div" };
        var child = new HtmlElement { TagName = "div" };

        doc.Children.Add(container);
        container.Parent = doc;
        container.Children.Add(child);
        child.Parent = container;

        var css = @"
            .container { width: 200px; padding: -10px; margin-top: -20px; }
            .child { height: 50px; margin-top: -30px; }
        ";
        container.Attributes["class"] = "container";
        child.Attributes["class"] = "child";

        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var containerBox = layoutTree.Children[0];
        LayoutEngine.Layout(containerBox, 400f, 400f);

        // Check container resolving
        Assert.Equal(-20f, containerBox.MarginTop);
        Assert.Equal(-10f, containerBox.PaddingLeft);
        Assert.Equal(200f, containerBox.ContentWidth);

        var childBox = containerBox.Children[0];
        // Child Y coordinate: Y = padding-top + border-top + child.MarginTop
        // padding-top is -10f, border-top is 0f, child.MarginTop is -30f.
        // Simplified margin collapsing: Math.Max(0f, -30f) = 0f.
        // Y = -10 + 0 + 0 = -10f.
        Assert.Equal(-10f, childBox.Y);
    }

    [Fact]
    public void TestEmptyTextNodes()
    {
        var doc = new HtmlDocument();
        var div = new HtmlElement { TagName = "div" };
        var text = new HtmlTextNode { Text = "" };
        doc.Children.Add(div);
        div.Parent = doc;
        div.Children.Add(text);
        text.Parent = div;

        var css = "div { width: 100px; padding: 10px; }";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var divBox = layoutTree.Children[0];
        LayoutEngine.Layout(divBox, 100f, 100f);

        // Container's own padding-left/right should NOT be added as spacing tokens.
        // Therefore, there should be no line boxes because the text node is empty.
        Assert.Empty(divBox.LineBoxes);
        Assert.Equal(120f, divBox.Width);
        Assert.Equal(20f, divBox.Height); // padding-top + padding-bottom = 20
    }

    [Fact]
    public void TestContainerPaddingInIfc()
    {
        var doc = new HtmlDocument();
        var div = new HtmlElement { TagName = "div" };
        var text = new HtmlTextNode { Text = "hello" };
        doc.Children.Add(div);
        div.Parent = doc;
        div.Children.Add(text);
        text.Parent = div;

        var css = "div { width: 100px; padding-left: 10px; }";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var divBox = layoutTree.Children[0];
        LayoutEngine.Layout(divBox, 100f, 100f);

        // Under fixed behavior, the text fragment's X position should be exactly padding-left (10f)
        var line = Assert.Single(divBox.LineBoxes);
        var fragment = Assert.Single(line.Fragments);
        Assert.Equal(10f, fragment.X);
    }

    [Fact]
    public void TestRenderingWithTextDrawLoop()
    {
        var doc = new HtmlDocument();
        var div = new HtmlElement { TagName = "div" };
        var text = new HtmlTextNode { Text = "hello" };
        doc.Children.Add(div);
        div.Parent = doc;
        div.Children.Add(text);
        text.Parent = div;

        var css = "div { width: 100px; padding-left: 10px; color: red; }";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var divBox = layoutTree.Children[0];
        LayoutEngine.Layout(divBox, 100f, 100f);

        using var bitmap = new SKBitmap(300, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // This will trigger text rendering path and SKTypeface disposal
        HtmlRenderer.Render(divBox, canvas, 0f, 0f);

        // We just verify it completed successfully and did not crash
        Assert.NotNull(bitmap);
    }


    [Fact]
    public void TestInfiniteWidthContainer()
    {
        var doc = new HtmlDocument();
        var flex = new HtmlElement { TagName = "div" };
        var item1 = new HtmlElement { TagName = "div" };
        var item2 = new HtmlElement { TagName = "div" };
        doc.Children.Add(flex);
        flex.Parent = doc;
        flex.Children.Add(item1);
        item1.Parent = flex;
        flex.Children.Add(item2);
        item2.Parent = flex;

        var css = @"
            .flex-container { display: flex; flex-direction: row; width: auto; }
            .item { width: 100px; height: 50px; }
        ";
        flex.Attributes["class"] = "flex-container";
        item1.Attributes["class"] = "item";
        item2.Attributes["class"] = "item";

        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var flexBox = layoutTree.Children[0];
        LayoutEngine.Layout(flexBox, float.PositiveInfinity, 600f);

        // flexBox width should default to availableWidth = PositiveInfinity
        Assert.True(float.IsPositiveInfinity(flexBox.Width));

        var box1 = flexBox.Children[0];
        var box2 = flexBox.Children[1];
        Assert.Equal(100f, box1.Width);
        Assert.Equal(100f, box2.Width);
        Assert.Equal(0f, box1.X);
        Assert.Equal(100f, box2.X);
    }

    [Fact]
    public void TestFlexboxSpaceDistributionEdgeCases()
    {
        var doc = new HtmlDocument();
        var flex = new HtmlElement { TagName = "div" };
        var item1 = new HtmlElement { TagName = "div" };
        var item2 = new HtmlElement { TagName = "div" };
        doc.Children.Add(flex);
        flex.Parent = doc;
        flex.Children.Add(item1);
        item1.Parent = flex;
        flex.Children.Add(item2);
        item2.Parent = flex;

        // Zero grow/shrink factors
        var css = @"
            .flex-container { display: flex; flex-direction: row; width: 300px; }
            .item1 { width: 200px; flex-grow: 0; flex-shrink: 0; }
            .item2 { width: 200px; flex-grow: 0; flex-shrink: 0; }
        ";
        flex.Attributes["class"] = "flex-container";
        item1.Attributes["class"] = "item1";
        item2.Attributes["class"] = "item2";

        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        var flexBox = layoutTree.Children[0];
        LayoutEngine.Layout(flexBox, 300f, 600f);

        // Since flex-shrink is 0, they should not shrink even though there's overflow (400px > 300px)
        var box1 = flexBox.Children[0];
        var box2 = flexBox.Children[1];
        Assert.Equal(200f, box1.Width);
        Assert.Equal(200f, box2.Width);
    }

    [Fact]
    public void TestLayoutCaching()
    {
        var doc = new HtmlDocument();
        var div = new HtmlElement { TagName = "div" };
        doc.Children.Add(div);
        div.Parent = doc;

        var css = "div { width: 100px; height: 100px; }";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);
        var box = layoutTree.Children[0];

        // 1. Initially needs layout
        Assert.True(box.NeedsLayout);
        Assert.Null(box.LayoutCacheAvailableWidth);
        Assert.Null(box.LayoutCacheAvailableHeight);

        // 2. Lay out once
        LayoutEngine.Layout(box, 200f, 300f);

        Assert.False(box.NeedsLayout);
        Assert.Equal(200f, box.LayoutCacheAvailableWidth);
        Assert.Equal(300f, box.LayoutCacheAvailableHeight);

        // 3. Mutate property directly, and call layout again with same dimensions
        box.Width = 9999f;
        LayoutEngine.Layout(box, 200f, 300f);
        Assert.Equal(9999f, box.Width); // Bypassed recalculation!

        // 4. Lay out with different dimensions (should recalculate)
        LayoutEngine.Layout(box, 250f, 350f);
        Assert.NotEqual(9999f, box.Width); // Recalculated!
        Assert.Equal(250f, box.LayoutCacheAvailableWidth);
        Assert.Equal(350f, box.LayoutCacheAvailableHeight);
    }
}

