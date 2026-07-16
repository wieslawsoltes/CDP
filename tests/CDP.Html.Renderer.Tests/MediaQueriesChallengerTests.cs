using System;
using System.Collections.Generic;
using Xunit;
using CDP.Css.Parser;
using CDP.Html.Renderer.Style;
using CDP.Html.Parser;

namespace CDP.Html.Renderer.Tests;

public class MediaQueriesChallengerTests
{
    [Fact]
    public void TestBoundaryConditions_ExactEquals()
    {
        // Viewport bounds exactly equal to condition limit (min-width: 600px)
        Assert.True(StyleCascade.EvaluateMediaCondition("(min-width: 600px)", 600f, 600f, "screen"));
        Assert.False(StyleCascade.EvaluateMediaCondition("(min-width: 600px)", 599.9f, 600f, "screen"));

        // Max-width boundary condition (max-width: 600px)
        Assert.True(StyleCascade.EvaluateMediaCondition("(max-width: 600px)", 600f, 600f, "screen"));
        Assert.False(StyleCascade.EvaluateMediaCondition("(max-width: 600px)", 600.1f, 600f, "screen"));

        // Height boundary conditions (min-height: 400px, max-height: 400px)
        Assert.True(StyleCascade.EvaluateMediaCondition("(min-height: 400px)", 600f, 400f, "screen"));
        Assert.False(StyleCascade.EvaluateMediaCondition("(min-height: 400px)", 600f, 399.9f, "screen"));

        Assert.True(StyleCascade.EvaluateMediaCondition("(max-height: 400px)", 600f, 400f, "screen"));
        Assert.False(StyleCascade.EvaluateMediaCondition("(max-height: 400px)", 600f, 400.1f, "screen"));
    }

    [Fact]
    public void TestOrientation_SquareViewport()
    {
        // Width == Height (500x500)
        // portrait: height >= width -> true
        Assert.True(StyleCascade.EvaluateMediaCondition("(orientation: portrait)", 500f, 500f, "screen"));
        
        // landscape: width > height -> false
        Assert.False(StyleCascade.EvaluateMediaCondition("(orientation: landscape)", 500f, 500f, "screen"));
    }

    [Fact]
    public void TestMalformedQueries_NoCrashes()
    {
        // Invalid float parse (min-width: abc)
        // ParseMediaLength("abc") yields 0f. 600f >= 0f -> true.
        Assert.True(StyleCascade.EvaluateMediaCondition("(min-width: abc)", 600f, 600f, "screen"));

        // Negative values (max-width: -10px)
        // ParseMediaLength("-10px") yields -10f. 600f <= -10f -> false.
        Assert.False(StyleCascade.EvaluateMediaCondition("(max-width: -10px)", 600f, 600f, "screen"));

        // Trailing and leading logical operator issues
        // "screen and" splits into ["screen", ""]. Both parts evaluate to true.
        Assert.True(StyleCascade.EvaluateMediaCondition("screen and", 600f, 600f, "screen"));

        // ", screen" splits into ["", "screen"].
        Assert.True(StyleCascade.EvaluateMediaCondition(", screen", 600f, 600f, "screen"));

        // "screen ,"
        Assert.True(StyleCascade.EvaluateMediaCondition("screen ,", 600f, 600f, "screen"));

        // "(min-width: 600px) and"
        Assert.True(StyleCascade.EvaluateMediaCondition("(min-width: 600px) and", 600f, 600f, "screen"));

        // "and (min-width: 600px)"
        Assert.True(StyleCascade.EvaluateMediaCondition("and (min-width: 600px)", 600f, 600f, "screen"));

        // Empty queries
        Assert.True(StyleCascade.EvaluateMediaCondition("", 600f, 600f, "screen"));
        Assert.True(StyleCascade.EvaluateMediaCondition("   ", 600f, 600f, "screen"));
        Assert.True(StyleCascade.EvaluateMediaCondition(null, 600f, 600f, "screen"));
    }

    [Fact]
    public void TestExtremelyNestedMediaBlocks()
    {
        // Test parsing with highly nested media blocks to ensure no stack overflow or exceptions
        int levels = 200;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < levels; i++)
        {
            sb.Append($"@media screen and (min-width: {i}px) {{");
        }
        sb.Append(".item { color: red; }");
        for (int i = 0; i < levels; i++)
        {
            sb.Append("}");
        }

        var sheet = CssParser.Parse(sb.ToString());
        Assert.NotNull(sheet);
        Assert.Single(sheet.Rules);
        
        var rule = sheet.Rules[0];
        Assert.Equal(".item", rule.Selectors[0].Text);
        Assert.Equal("red", rule.Declarations["color"]);
        
        // The compiled MediaCondition should be a chained "and" query
        Assert.NotNull(rule.MediaCondition);
        Assert.Contains("min-width: 199px", rule.MediaCondition);
    }

    [Fact]
    public void TestInvalidInputsAndEdgeCases_NoCrashes()
    {
        // Check for NaN and Infinity values
        Assert.True(StyleCascade.EvaluateMediaCondition("(min-width: 500px)", float.PositiveInfinity, 600f, "screen"));
        Assert.False(StyleCascade.EvaluateMediaCondition("(min-width: 500px)", float.NegativeInfinity, 600f, "screen"));
        Assert.False(StyleCascade.EvaluateMediaCondition("(min-width: 500px)", float.NaN, 600f, "screen"));

        Assert.True(StyleCascade.EvaluateMediaCondition("(max-width: 500px)", float.NegativeInfinity, 600f, "screen"));
        Assert.False(StyleCascade.EvaluateMediaCondition("(max-width: 500px)", float.PositiveInfinity, 600f, "screen"));
        Assert.False(StyleCascade.EvaluateMediaCondition("(max-width: 500px)", float.NaN, 600f, "screen"));

        // Special characters and garbage input
        Assert.False(StyleCascade.EvaluateMediaCondition("(!@#$*)", 600f, 600f, "screen"));
        Assert.True(StyleCascade.EvaluateMediaCondition("(min-width: !@#)", 600f, 600f, "screen"));

        // Extremely long string to test regex performance/timeout
        string longString = new string('a', 10000);
        Assert.False(StyleCascade.EvaluateMediaCondition(longString, 600f, 600f, "screen"));

        // Parser resilience to completely invalid @media syntax
        string badCss = "@media (min-width: { { { { ; ; ;";
        var sheet = CssParser.Parse(badCss);
        Assert.NotNull(sheet); // Should not crash
    }
}
