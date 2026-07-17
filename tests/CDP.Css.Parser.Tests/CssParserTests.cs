using System;
using System.Linq;
using CDP.Css.Parser;
using Xunit;

namespace CDP.Css.Parser.Tests;

public class CssParserTests
{
    [Fact]
    public void TestParseSimpleSelector()
    {
        var tagSel = CssParser.ParseSelector("div");
        Assert.Equal("div", tagSel.TagName);
        Assert.Empty(tagSel.Classes);
        Assert.Null(tagSel.Id);
        Assert.Equal(new Specificity(0, 0, 1), tagSel.Specificity);

        var classSel = CssParser.ParseSelector(".btn");
        Assert.Null(classSel.TagName);
        Assert.Single(classSel.Classes);
        Assert.Equal("btn", classSel.Classes[0]);
        Assert.Null(classSel.Id);
        Assert.Equal(new Specificity(0, 1, 0), classSel.Specificity);

        var idSel = CssParser.ParseSelector("#header");
        Assert.Null(idSel.TagName);
        Assert.Empty(idSel.Classes);
        Assert.Equal("header", idSel.Id);
        Assert.Equal(new Specificity(1, 0, 0), idSel.Specificity);
    }

    [Fact]
    public void TestUniversalSelectorSpecificity()
    {
        var starSel = CssParser.ParseSelector("*");
        Assert.Equal("*", starSel.TagName);
        Assert.Equal(new Specificity(0, 0, 0), starSel.Specificity);
    }

    [Fact]
    public void TestParseCompoundSelector()
    {
        var sel = CssParser.ParseSelector("div.btn.primary#submit");
        Assert.Equal("div", sel.TagName);
        Assert.Equal(2, sel.Classes.Count);
        Assert.Contains("btn", sel.Classes);
        Assert.Contains("primary", sel.Classes);
        Assert.Equal("submit", sel.Id);
        Assert.Equal(new Specificity(1, 2, 1), sel.Specificity);
    }

    [Fact]
    public void TestParseCombinatorSelectors()
    {
        var descendant = CssParser.ParseSelector("body div.content p");
        Assert.Equal("p", descendant.TagName);
        Assert.Equal(" ", descendant.Combinator);
        Assert.NotNull(descendant.ParentSelector);

        var parent = descendant.ParentSelector;
        Assert.Equal("div", parent.TagName);
        Assert.Single(parent.Classes, "content");
        Assert.Equal(" ", parent.Combinator);
        Assert.NotNull(parent.ParentSelector);

        var grandParent = parent.ParentSelector;
        Assert.Equal("body", grandParent.TagName);
        Assert.Null(grandParent.Combinator);
        Assert.Null(grandParent.ParentSelector);

        // Specificity: body (0,0,1) + div.content (0,1,1) + p (0,0,1) = (0,1,3)
        Assert.Equal(new Specificity(0, 1, 3), descendant.Specificity);
    }

    [Fact]
    public void TestParseChildSelector()
    {
        var child = CssParser.ParseSelector("div > p.active");
        Assert.Equal("p", child.TagName);
        Assert.Single(child.Classes, "active");
        Assert.Equal(">", child.Combinator);
        Assert.NotNull(child.ParentSelector);

        var parent = child.ParentSelector;
        Assert.Equal("div", parent.TagName);
        Assert.Null(parent.Combinator);

        // Specificity: div (0,0,1) + p.active (0,1,1) = (0,1,2)
        Assert.Equal(new Specificity(0, 1, 2), child.Specificity);
    }

    [Fact]
    public void TestSpecificityOrdering()
    {
        var s1 = new Specificity(0, 0, 1); // div
        var s2 = new Specificity(0, 1, 0); // .btn
        var s3 = new Specificity(0, 1, 1); // div.btn
        var s4 = new Specificity(1, 0, 0); // #header
        var s5 = new Specificity(1, 1, 0); // #header.btn

        Assert.True(s1 < s2);
        Assert.True(s2 < s3);
        Assert.True(s3 < s4);
        Assert.True(s4 < s5);

        var list = new[] { s4, s2, s5, s1, s3 };
        var sorted = list.OrderBy(s => s).ToArray();

        Assert.Equal(s1, sorted[0]);
        Assert.Equal(s2, sorted[1]);
        Assert.Equal(s3, sorted[2]);
        Assert.Equal(s4, sorted[3]);
        Assert.Equal(s5, sorted[4]);
    }

    [Fact]
    public void TestParseStylesheet()
    {
        string css = @"
            /* Global reset */
            * {
                box-sizing: border-box;
            }

            body, html {
                margin: 0;
                padding: 0;
            }

            div.card {
                background-color: #ffffff;
                border: 1px solid /* grey */ #ccc;
                border-radius: 4px;
                padding: 16px;
            }

            #main-header {
                color: red;
            }
        ";

        var sheet = CssParser.Parse(css);
        Assert.NotNull(sheet);
        Assert.Equal(4, sheet.Rules.Count);

        // Rule 0
        var r0 = sheet.Rules[0];
        Assert.Single(r0.Selectors);
        Assert.Equal("*", r0.Selectors[0].TagName);
        Assert.Equal("border-box", r0.Declarations["box-sizing"]);

        // Rule 1
        var r1 = sheet.Rules[1];
        Assert.Equal(2, r1.Selectors.Count);
        Assert.Equal("body", r1.Selectors[0].TagName);
        Assert.Equal("html", r1.Selectors[1].TagName);
        Assert.Equal("0", r1.Declarations["margin"]);
        Assert.Equal("0", r1.Declarations["padding"]);

        // Rule 2
        var r2 = sheet.Rules[2];
        Assert.Single(r2.Selectors);
        var cardSel = r2.Selectors[0];
        Assert.Equal("div", cardSel.TagName);
        Assert.Single(cardSel.Classes, "card");
        Assert.Equal("#ffffff", r2.Declarations["background-color"]);
        Assert.Equal("1px solid #ccc", r2.Declarations["border"]);
        Assert.Equal("4px", r2.Declarations["border-radius"]);
        Assert.Equal("16px", r2.Declarations["padding"]);

        // Rule 3
        var r3 = sheet.Rules[3];
        Assert.Single(r3.Selectors);
        Assert.Equal("main-header", r3.Selectors[0].Id);
        Assert.Equal("red", r3.Declarations["color"]);
    }

    [Fact]
    public void TestNthChildSelectorParsing()
    {
        var css = @"
            span:nth-child(3n+1) { color: green; }
            div:nth-last-child(2n + 3) { color: blue; }
        ";
        var sheet = CssParser.Parse(css);
        Assert.NotNull(sheet);
        Assert.Equal(2, sheet.Rules.Count);

        var r0 = sheet.Rules[0];
        Assert.Single(r0.Selectors);
        var s0 = r0.Selectors[0];
        Assert.Equal("span", s0.TagName);
        Assert.Single(s0.PseudoClasses);
        Assert.Equal(":nth-child(3n+1)", s0.PseudoClasses[0]);

        var r1 = sheet.Rules[1];
        Assert.Single(r1.Selectors);
        var s1 = r1.Selectors[0];
        Assert.Equal("div", s1.TagName);
        Assert.Single(s1.PseudoClasses);
        Assert.Equal(":nth-last-child(2n + 3)", s1.PseudoClasses[0]);
    }

    [Fact]
    public void TestParseSiblingCombinators()
    {
        // 1. Basic adjacent and general sibling combinators
        var sPlus = CssParser.ParseSelector("div + span");
        Assert.Equal("span", sPlus.TagName);
        Assert.Equal("+", sPlus.Combinator);
        Assert.NotNull(sPlus.ParentSelector);
        Assert.Equal("div", sPlus.ParentSelector.TagName);

        var sTilde = CssParser.ParseSelector("div ~ p");
        Assert.Equal("p", sTilde.TagName);
        Assert.Equal("~", sTilde.Combinator);
        Assert.NotNull(sTilde.ParentSelector);
        Assert.Equal("div", sTilde.ParentSelector.TagName);

        // 2. Chained combinators
        var sChain = CssParser.ParseSelector("div + span + p ~ section");
        Assert.Equal("section", sChain.TagName);
        Assert.Equal("~", sChain.Combinator);
        Assert.NotNull(sChain.ParentSelector);

        var pNode = sChain.ParentSelector;
        Assert.Equal("p", pNode.TagName);
        Assert.Equal("+", pNode.Combinator);
        Assert.NotNull(pNode.ParentSelector);

        var spanNode = pNode.ParentSelector;
        Assert.Equal("span", spanNode.TagName);
        Assert.Equal("+", spanNode.Combinator);
        Assert.NotNull(spanNode.ParentSelector);

        var divNode = spanNode.ParentSelector;
        Assert.Equal("div", divNode.TagName);
        Assert.Null(divNode.Combinator);
        Assert.Null(divNode.ParentSelector);

        // 3. Sibling combinators mixed with pseudo-classes containing operators
        var sMixed = CssParser.ParseSelector("div:first-child + span:nth-child(2n+1)");
        Assert.Equal("span", sMixed.TagName);
        Assert.Equal("+", sMixed.Combinator);
        Assert.Single(sMixed.PseudoClasses);
        Assert.Equal(":nth-child(2n+1)", sMixed.PseudoClasses[0]);
        Assert.NotNull(sMixed.ParentSelector);
        Assert.Equal("div", sMixed.ParentSelector.TagName);
        Assert.Single(sMixed.ParentSelector.PseudoClasses);
        Assert.Equal(":first-child", sMixed.ParentSelector.PseudoClasses[0]);
    }

    [Fact]
    public void TestParseMediaQueriesAndNestedConditions()
    {
        string css = @"
            @media screen {
                body { color: red; }
                @media (min-width: 600px) {
                    div { color: blue; }
                }
            }
            @media print, (max-width: 400px) {
                span { color: green; }
            }
        ";

        var sheet = CssParser.Parse(css);
        Assert.NotNull(sheet);
        Assert.Equal(3, sheet.Rules.Count);

        // Rule 0: body
        var r0 = sheet.Rules[0];
        Assert.Equal("body", r0.Selectors[0].TagName);
        Assert.Equal("screen", r0.MediaCondition);

        // Rule 1: div
        var r1 = sheet.Rules[1];
        Assert.Equal("div", r1.Selectors[0].TagName);
        Assert.Equal("screen and (min-width: 600px)", r1.MediaCondition);

        // Rule 2: span
        var r2 = sheet.Rules[2];
        Assert.Equal("span", r2.Selectors[0].TagName);
        Assert.Equal("print, (max-width: 400px)", r2.MediaCondition);
    }
}

