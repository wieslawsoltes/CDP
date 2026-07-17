using System;
using CDP.Css.Parser;
using Xunit;

namespace CDP.Css.Parser.Tests;

public class EmpiricalChallengeTests
{
    [Fact]
    public void TestMediaQueries_Bug()
    {
        // Issue: Media queries are parsed as normal rules, resulting in a mangled selector and broken declaration dictionary
        string css = "@media (max-width: 600px) { body { color: red; } }";
        var stylesheet = CssParser.Parse(css);
        
        // A correct parser should either skip, ignore, or properly parse media queries.
        // Currently, it creates a rule where:
        // Selector = "@media (max-width: 600px)"
        // Declarations contains "body { color" as a key and "red" as a value.
        Assert.NotEmpty(stylesheet.Rules);
        var rule = stylesheet.Rules[0];
        
        // This is a dummy assertion that highlights the broken state
        Assert.DoesNotContain("body { color", rule.Declarations.Keys);
    }

    [Fact]
    public void TestPseudoClasses_Bug()
    {
        // Issue: Pseudo-classes are parsed as part of the class name or tag name instead of being stripped or handled
        string css = ".btn:hover { color: blue; }";
        var stylesheet = CssParser.Parse(css);
        
        Assert.Single(stylesheet.Rules);
        var rule = stylesheet.Rules[0];
        var selector = Assert.Single(rule.Selectors);
        
        // The class name should be "btn", and ":hover" should be parsed as a pseudo-class or stripped.
        // Currently, classes contains "btn:hover", which is incorrect.
        var className = Assert.Single(selector.Classes);
        Assert.Equal("btn", className);
    }

    [Fact]
    public void TestSiblingCombinators_Bug()
    {
        // Issue: Adjacent sibling '+' and general sibling '~' combinators are not supported, parsed as tag names
        string css = "div + span { color: green; }";
        var stylesheet = CssParser.Parse(css);
        
        Assert.Single(stylesheet.Rules);
        var rule = stylesheet.Rules[0];
        var selector = Assert.Single(rule.Selectors);
        
        // The parser should recognize "+" as a combinator.
        // Currently, it parses "+" as a tag name of a parent selector.
        Assert.Equal("+", selector.Combinator);
    }
}
