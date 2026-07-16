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

    [Fact]
    public void TestCssVariablesResolution()
    {
        var doc = new HtmlDocument();
        var body = new HtmlElement { TagName = "body" };
        var div = new HtmlElement { TagName = "div" };

        doc.Children.Add(body);
        body.Parent = doc;
        body.Children.Add(div);
        div.Parent = body;

        var css = @"
            body {
                --main-color: blue;
                --main-size: 20px;
                --cyclic-a: var(--cyclic-b);
                --cyclic-b: var(--cyclic-a);
            }
            div {
                --sub-color: var(--main-color);
                color: var(--sub-color);
                font-size: var(--undefined-size, 18px);
                background-color: var(--cyclic-a, red);
            }
        ";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);

        var bodyStyle = styles[body];
        Assert.Equal("blue", bodyStyle.CustomProperties["--main-color"]);

        var divStyle = styles[div];
        Assert.Equal(SKColors.Blue, divStyle.Color);
        Assert.Equal(18f, divStyle.FontSize);
        Assert.Equal(SKColors.Red, divStyle.BackgroundColor); // cyclic resolves to fallback
    }

    [Fact]
    public void TestCalcBasicArithmetic()
    {
        var doc = new HtmlDocument();
        var div = new HtmlElement { TagName = "div" };
        doc.Children.Add(div);
        div.Parent = doc;

        var css = @"
            div {
                width: calc(100% - 20px);
                height: calc(200px / 2);
                margin-left: calc(20px + 10%);
                padding-left: calc((50px * 2) - 10px);
            }
        ";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var divStyle = styles[div];

        // Verify units are Calc
        Assert.Equal(LengthUnit.Calc, divStyle.Width.Unit);
        Assert.Equal(LengthUnit.Calc, divStyle.Height.Unit);

        // Resolve against parent size of 200px
        float parentSize = 200f;
        Assert.Equal(180f, divStyle.Width.Resolve(parentSize));
        Assert.Equal(100f, divStyle.Height.Resolve(parentSize));
        Assert.Equal(40f, divStyle.MarginLeft.Resolve(parentSize));
        Assert.Equal(90f, divStyle.PaddingLeft.Resolve(parentSize));
    }

    [Fact]
    public void TestCssVariablesAndCalcCombo()
    {
        var doc = new HtmlDocument();
        var div = new HtmlElement { TagName = "div" };
        doc.Children.Add(div);
        div.Parent = doc;

        var css = @"
            div {
                --offset: 10px;
                --base-width: 100%;
                width: calc(var(--base-width) - var(--offset));
            }
        ";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var divStyle = styles[div];

        Assert.Equal(LengthUnit.Calc, divStyle.Width.Unit);
        Assert.Equal(190f, divStyle.Width.Resolve(200f));
    }

    [Fact]
    public void TestRelativePositioningShift()
    {
        var doc = new HtmlDocument();
        var body = new HtmlElement { TagName = "body" };
        var div = new HtmlElement { TagName = "div" };
        div.Attributes["id"] = "rel";
        doc.Children.Add(body);
        body.Parent = doc;
        body.Children.Add(div);
        div.Parent = body;

        var css = @"
            body { width: 500px; height: 500px; }
            #rel { position: relative; left: 20px; top: 30px; width: 100px; height: 100px; }
        ";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(rootBox, 500f, 500f);

        var relBox = rootBox.Children[0].Children[0];
        Assert.Equal(PositionType.Relative, relBox.Style.Position);
        Assert.Equal(20f, relBox.X);
        Assert.Equal(30f, relBox.Y);
    }

    [Fact]
    public void TestAbsolutePositioningBasic()
    {
        var doc = new HtmlDocument();
        var container = new HtmlElement { TagName = "div" };
        container.Attributes["id"] = "container";
        var abs = new HtmlElement { TagName = "div" };
        abs.Attributes["id"] = "abs";
        doc.Children.Add(container);
        container.Parent = doc;
        container.Children.Add(abs);
        abs.Parent = container;

        var css = @"
            #container { position: relative; width: 300px; height: 300px; padding: 10px; border: 5px solid black; }
            #abs { position: absolute; left: 20px; top: 30px; width: 50px; height: 50px; }
        ";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(rootBox, 500f, 500f);

        var containerBox = rootBox.Children[0];
        var absBox = containerBox.Children[0];

        Assert.Equal(PositionType.Relative, containerBox.Style.Position);
        Assert.Equal(PositionType.Absolute, absBox.Style.Position);

        Assert.Equal(35f, absBox.X);
        Assert.Equal(45f, absBox.Y);
    }

    [Fact]
    public void TestAbsolutePositioningGrandparentContainingBlock()
    {
        var doc = new HtmlDocument();
        var grandparent = new HtmlElement { TagName = "div" };
        grandparent.Attributes["id"] = "grandparent";
        var parent = new HtmlElement { TagName = "div" };
        parent.Attributes["id"] = "parent";
        var abs = new HtmlElement { TagName = "div" };
        abs.Attributes["id"] = "abs";

        doc.Children.Add(grandparent);
        grandparent.Parent = doc;
        grandparent.Children.Add(parent);
        parent.Parent = grandparent;
        parent.Children.Add(abs);
        abs.Parent = parent;

        var css = @"
            #grandparent { position: relative; width: 300px; height: 300px; }
            #parent { position: static; margin-top: 50px; margin-left: 60px; width: 200px; height: 200px; }
            #abs { position: absolute; left: 10px; top: 20px; width: 50px; height: 50px; }
        ";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(rootBox, 500f, 500f);

        var gpBox = rootBox.Children[0];
        var pBox = gpBox.Children[0];
        var absBox = pBox.Children[0];

        Assert.Equal(-50f, absBox.X);
        Assert.Equal(-30f, absBox.Y);
    }

    [Fact]
    public void TestFixedPositioning()
    {
        var doc = new HtmlDocument();
        var parent = new HtmlElement { TagName = "div" };
        parent.Attributes["id"] = "parent";
        var fixedEl = new HtmlElement { TagName = "div" };
        fixedEl.Attributes["id"] = "fixed";

        doc.Children.Add(parent);
        parent.Parent = doc;
        parent.Children.Add(fixedEl);
        fixedEl.Parent = parent;

        var css = @"
            #parent { position: relative; margin-top: 100px; }
            #fixed { position: fixed; left: 15px; top: 25px; width: 50px; height: 50px; }
        ";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(rootBox, 500f, 500f);

        var pBox = rootBox.Children[0];
        var fixedBox = pBox.Children[0];

        Assert.Equal(15f, fixedBox.X);
        Assert.Equal(-75f, fixedBox.Y);
    }

    [Fact]
    public void TestFloatLeftAndRightPositioning()
    {
        var doc = new HtmlDocument();
        var parent = new HtmlElement { TagName = "div" };
        parent.Attributes["id"] = "parent";
        var floatLeft = new HtmlElement { TagName = "div" };
        floatLeft.Attributes["id"] = "left";
        var floatRight = new HtmlElement { TagName = "div" };
        floatRight.Attributes["id"] = "right";
        var staticBlock = new HtmlElement { TagName = "div" };
        staticBlock.Attributes["id"] = "static";

        doc.Children.Add(parent);
        parent.Parent = doc;
        parent.Children.Add(floatLeft);
        floatLeft.Parent = parent;
        parent.Children.Add(floatRight);
        floatRight.Parent = parent;
        parent.Children.Add(staticBlock);
        staticBlock.Parent = parent;

        var css = @"
            #parent { width: 500px; }
            #left { float: left; width: 100px; height: 80px; }
            #right { float: right; width: 150px; height: 90px; }
            #static { width: auto; height: 50px; }
        ";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(rootBox, 500f, 500f);

        var pBox = rootBox.Children[0];
        var leftBox = pBox.Children[0];
        var rightBox = pBox.Children[1];
        var staticBox = pBox.Children[2];

        // Left float should be at X=0, Y=0 (relative to parent content)
        Assert.Equal(0f, leftBox.X);
        Assert.Equal(0f, leftBox.Y);

        // Right float should be at X = 500 - 150 = 350, Y=0
        Assert.Equal(350f, rightBox.X);
        Assert.Equal(0f, rightBox.Y);

        // Static box Y starts at 0, overlaps with float left and float right.
        // It should be positioned horizontally at availLeft=100 and have width=250.
        Assert.Equal(100f, staticBox.X);
        Assert.Equal(250f, staticBox.Width);
        Assert.Equal(0f, staticBox.Y);
    }

    [Fact]
    public void TestClearFlow()
    {
        var doc = new HtmlDocument();
        var parent = new HtmlElement { TagName = "div" };
        var floatLeft = new HtmlElement { TagName = "div" };
        floatLeft.Attributes["id"] = "left";
        var floatRight = new HtmlElement { TagName = "div" };
        floatRight.Attributes["id"] = "right";
        var clearedBlock = new HtmlElement { TagName = "div" };
        clearedBlock.Attributes["id"] = "cleared";

        doc.Children.Add(parent);
        parent.Parent = doc;
        parent.Children.Add(floatLeft);
        floatLeft.Parent = parent;
        parent.Children.Add(floatRight);
        floatRight.Parent = parent;
        parent.Children.Add(clearedBlock);
        clearedBlock.Parent = parent;

        var css = @"
            #left { float: left; width: 100px; height: 80px; }
            #right { float: right; width: 150px; height: 120px; }
            #cleared { clear: both; width: 200px; height: 50px; }
        ";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(rootBox, 500f, 500f);

        var pBox = rootBox.Children[0];
        var leftBox = pBox.Children[0];
        var rightBox = pBox.Children[1];
        var clearedBox = pBox.Children[2];

        // The cleared block has clear: both.
        // It must be placed below both floats.
        // The maximum bottom of the floats is:
        // left float bottom = 80
        // right float bottom = 120
        // So clearedBox should start at Y = 120.
        Assert.Equal(120f, clearedBox.Y);
    }

    [Fact]
    public void TestTextWrappingAroundFloats()
    {
        var doc = new HtmlDocument();
        var parent = new HtmlElement { TagName = "div" };
        var floatLeft = new HtmlElement { TagName = "div" };
        floatLeft.Attributes["id"] = "left";
        var textNode = new HtmlTextNode { Text = "word1 word2 word3 word4 word5 word6" };

        doc.Children.Add(parent);
        parent.Parent = doc;
        parent.Children.Add(floatLeft);
        floatLeft.Parent = parent;
        parent.Children.Add(textNode);
        textNode.Parent = parent;

        // Parent width 300px. Left float width 100px, height 30px.
        // Text node has font-size 10px. Line height is ~12px.
        // Available width next to float is 300 - 100 = 200px.
        var css = @"
            div { width: 300px; font-size: 10px; font-family: Arial; }
            #left { float: left; width: 100px; height: 30px; }
        ";
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(rootBox, 300f, 500f);

        var pBox = rootBox.Children[0];
        var anonBlock = pBox.Children[1];

        // The anonymous block box itself should be shifted to X=100 to clear the left float
        Assert.Equal(100f, anonBlock.X);

        // The line boxes in anonBlock:
        Assert.NotEmpty(anonBlock.LineBoxes);

        // So its LineLeft should be 0 (since anonBlock is already shifted to X=100), and AvailableWidth should be 200.
        var line1 = anonBlock.LineBoxes[0];
        Assert.Equal(0f, line1.LineLeft);
        Assert.Equal(200f, line1.AvailableWidth);

        // The fragments of line1 should start at X >= 0
        Assert.NotEmpty(line1.Fragments);
        Assert.True(line1.Fragments[0].X >= 0f);
    }

    [Fact]
    public void TestVisualVariablesAndCalc()
    {
        string html = @"
        <div class='container'>
            <div class='box box-main'></div>
            <div class='box box-sub'></div>
        </div>";
        string css = @"
            .container {
                --base-padding: 10px;
                --main-color: blue;
                --sub-color: green;
                --width-scale: 50%;
                width: 300px;
                height: 200px;
                background-color: lightgray;
                padding-left: var(--base-padding);
                padding-top: var(--base-padding);
                border: 2px solid black;
            }
            .box {
                --box-height: calc(120px / 2);
                height: var(--box-height);
                margin-bottom: calc(5px * 2);
            }
            .box-main {
                width: calc(var(--width-scale) * 2 - 20px);
                background-color: var(--main-color);
            }
            .box-sub {
                width: calc(var(--width-scale) - 10px);
                background-color: var(--sub-color);
                border: calc(1px * 3) solid orange;
            }
        ";
        VisualTestHelper.AssertVisualMatch("variables_and_calc_visual", html, css, 350, 250);
    }

    [Fact]
    public void TestVisualPositioning()
    {
        string html = @"
        <div class='grandparent'>
            <div class='parent'>
                <div class='relative-box'></div>
                <div class='absolute-box'></div>
                <div class='fixed-box'></div>
            </div>
        </div>";
        string css = @"
            .grandparent {
                position: relative;
                width: 300px;
                height: 300px;
                background-color: lightgray;
                border: 10px solid gray;
                padding: 15px;
            }
            .parent {
                position: static;
                margin-left: 20px;
                margin-top: 20px;
                width: 200px;
                height: 200px;
                background-color: white;
                border: 5px solid black;
            }
            .relative-box {
                position: relative;
                left: 30px;
                top: 10px;
                width: 60px;
                height: 60px;
                background-color: blue;
                border: 2px solid yellow;
            }
            .absolute-box {
                position: absolute;
                left: 10px;
                top: 20px;
                width: 50px;
                height: 50px;
                background-color: red;
                border: 3px solid green;
            }
            .fixed-box {
                position: fixed;
                left: 320px;
                top: 20px;
                width: 40px;
                height: 40px;
                background-color: purple;
                border: 2px solid orange;
            }
        ";
        VisualTestHelper.AssertVisualMatch("positioning_visual", html, css, 400, 400);
    }

    [Fact]
    public void TestVisualFloatsAndClears()
    {
        string html = @"
        <div class='container'>
            <div class='float-l'></div>
            <div class='float-r'></div>
            <div class='text-wrap'>
                Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam.
            </div>
            <div class='cleared-both'></div>
            <div class='static-flow'></div>
        </div>";
        string css = @"
            .container {
                width: 350px;
                height: 350px;
                background-color: lightgray;
                border: 3px solid black;
            }
            .float-l {
                float: left;
                width: 80px;
                height: 80px;
                background-color: red;
                border: 2px solid #8b0000;
            }
            .float-r {
                float: right;
                width: 90px;
                height: 70px;
                background-color: blue;
                border: 2px solid #00008b;
            }
            .text-wrap {
                font-size: 11px;
                font-family: Arial;
                color: black;
            }
            .cleared-both {
                clear: both;
                width: calc(100% - 20px);
                height: 40px;
                background-color: green;
                border: 2px solid #006400;
            }
            .static-flow {
                width: 100px;
                height: 40px;
                background-color: purple;
                border: 2px solid #4b0082;
            }
        ";
        VisualTestHelper.AssertVisualMatch("floats_and_clears_visual", html, css, 400, 400);
    }

    [Fact]
    public void TestHideNonRenderedTags()
    {
        // 1. Arrange
        var doc = new HtmlDocument();
        var html = new HtmlElement { TagName = "html" };
        var head = new HtmlElement { TagName = "head" };
        var style = new HtmlElement { TagName = "style" };
        style.Children.Add(new HtmlTextNode { Text = "body { color: red; }" });
        var script = new HtmlElement { TagName = "script" };
        script.Children.Add(new HtmlTextNode { Text = "console.log('test');" });
        var title = new HtmlElement { TagName = "title" };
        title.Children.Add(new HtmlTextNode { Text = "My Title" });
        var meta = new HtmlElement { TagName = "meta" };
        var link = new HtmlElement { TagName = "link" };
        var body = new HtmlElement { TagName = "body" };
        var p = new HtmlElement { TagName = "p" };
        p.Children.Add(new HtmlTextNode { Text = "Hello" });

        doc.Children.Add(html);
        html.Parent = doc;
        html.Children.Add(head);
        head.Parent = html;
        head.Children.Add(style);
        style.Parent = head;
        head.Children.Add(script);
        script.Parent = head;
        head.Children.Add(title);
        title.Parent = head;
        head.Children.Add(meta);
        meta.Parent = head;
        head.Children.Add(link);
        link.Parent = head;

        html.Children.Add(body);
        body.Parent = html;
        body.Children.Add(p);
        p.Parent = body;

        var stylesheet = CssParser.Parse(string.Empty);

        // 2. Act
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var layoutTree = LayoutTreeBuilder.Build(doc, styles);

        // 3. Assert
        // The resolved display type for style, script, head, meta, link, title should be None
        Assert.Equal(DisplayType.None, styles[style].Display);
        Assert.Equal(DisplayType.None, styles[script].Display);
        Assert.Equal(DisplayType.None, styles[head].Display);
        Assert.Equal(DisplayType.None, styles[meta].Display);
        Assert.Equal(DisplayType.None, styles[link].Display);
        Assert.Equal(DisplayType.None, styles[title].Display);

        // The layout tree should only build box elements for non-None nodes
        // Traversing layout tree to ensure no style or script box exists
        bool HasNodeWithTag(LayoutBox box, string tag)
        {
            if (box.Node is HtmlElement el && el.TagName.Equals(tag, System.StringComparison.OrdinalIgnoreCase))
                return true;
            foreach (var child in box.Children)
            {
                if (HasNodeWithTag(child, tag))
                    return true;
            }
            return false;
        }

        Assert.False(HasNodeWithTag(layoutTree, "style"));
        Assert.False(HasNodeWithTag(layoutTree, "script"));
        Assert.False(HasNodeWithTag(layoutTree, "head"));
        Assert.False(HasNodeWithTag(layoutTree, "meta"));
        Assert.False(HasNodeWithTag(layoutTree, "link"));
        Assert.False(HasNodeWithTag(layoutTree, "title"));
        Assert.True(HasNodeWithTag(layoutTree, "p"));
    }
}

