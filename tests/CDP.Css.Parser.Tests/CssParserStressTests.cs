using System;
using System.Diagnostics;
using System.Text;
using CDP.Css.Parser;
using Xunit;

namespace CDP.Css.Parser.Tests;

public class CssParserStressTests
{
    private void RunDepthTest(int depth)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < depth; i++)
        {
            sb.Append("div > ");
        }
        sb.Append("span");

        string selectorText = sb.ToString();
        var selector = CssParser.ParseSelector(selectorText);
        Assert.NotNull(selector);
        Assert.Equal("span", selector.TagName);

        int parentCount = 0;
        var current = selector;
        while (current.ParentSelector != null)
        {
            Assert.Equal(">", current.Combinator);
            current = current.ParentSelector;
            parentCount++;
        }
        Assert.Equal(depth, parentCount);
    }

    [Fact]
    public void TestDeepSelectorCombinators_1000()
    {
        RunDepthTest(1000);
    }

    [Fact]
    public void TestDeepSelectorCombinators_2000()
    {
        RunDepthTest(2000);
    }

    [Fact]
    public void TestDeepSelectorCombinators_5000()
    {
        RunDepthTest(5000);
    }

    [Fact]
    public void TestDeepSelectorCombinators_10000()
    {
        RunDepthTest(10000);
    }

    [Fact]
    public void TestLargeCssStylesheet()
    {
        // Generate a 5MB CSS stylesheet
        var sb = new StringBuilder();
        for (int i = 0; i < 20000; i++)
        {
            sb.Append($@"
                /* Comment for rule {i} */
                .card-{i}, #item-{i} > div.content, body.theme-dark .container-{i} span.title {{
                    color: rgb({i % 255}, 100, 150);
                    margin-{(i % 4 == 0 ? "top" : "bottom")}: {i % 10}px;
                    padding: 8px 16px;
                    display: flex;
                    border: 1px solid /* inner comment */ #efefef;
                }}
            ");
        }

        string css = sb.ToString();

        var sw = Stopwatch.StartNew();
        var stylesheet = CssParser.Parse(css);
        sw.Stop();

        Assert.NotNull(stylesheet);
        Assert.True(stylesheet.Rules.Count > 0);
        Console.WriteLine($"[CSS] Large CSS stylesheet (5MB, 20,000 rules) parsed in {sw.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void TestAdversarialCssInputs()
    {
        // Malformed inputs to verify parser robustness
        string[] malformedInputs = new[]
        {
            "div { color: red;", // Unclosed brace
            "div { color: red; } /* unclosed comment",
            "div { color: red; } /* nested /* comment */ and after */",
            "div >> span { color: blue; }", // Invalid combinators
            ", , div { color: green; }", // Empty/comma selectors
            "div { color:; border: 1px solid; :blue; }", // Empty decl values or names
            "div { color: red;;; margin: 0; }", // Extra semicolons
            " { color: red; }", // Empty selector before brace
            "div { }", // Empty rule
            "div > > > span { color: red; }", // Multiple combinators with spaces
            null // Should throw ArgumentNullException
        };

        foreach (var input in malformedInputs)
        {
            if (input == null)
            {
                Assert.Throws<ArgumentNullException>(() => CssParser.Parse(input!));
            }
            else
            {
                var stylesheet = CssParser.Parse(input);
                Assert.NotNull(stylesheet); // Must not throw
            }
        }
    }

    [Fact]
    public void TestBracesInStringLiterals()
    {
        string css = @"
            div::before {
                content: ""}"";
                color: red;
            }
        ";

        var sheet = CssParser.Parse(css);
        Assert.NotNull(sheet);
        Assert.Single(sheet.Rules);
        var rule = sheet.Rules[0];
        Assert.Equal("div::before", rule.Selectors[0].Text);
        Assert.Equal("\"}\"", rule.Declarations["content"]);
        Assert.Equal("red", rule.Declarations["color"]);
    }

    [Fact]
    public void TestCssPerformanceAndAllocations()
    {
        string cssSnippet = @"
            div.card, #main-container > p.active {
                color: #333;
                background-color: #fff;
                padding: 10px;
                border-radius: 4px;
            }
        ";

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            CssParser.Parse(cssSnippet);
        }

        int iterations = 10000;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long beforeAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            CssParser.Parse(cssSnippet);
        }
        sw.Stop();

        long afterAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        int gen0After = GC.CollectionCount(0);
        int gen1After = GC.CollectionCount(1);
        int gen2After = GC.CollectionCount(2);

        long totalAllocated = afterAllocatedBytes - beforeAllocatedBytes;
        double allocPerIteration = (double)totalAllocated / iterations;
        double timePerIterationUs = (double)sw.Elapsed.TotalMilliseconds * 1000 / iterations;

        Console.WriteLine($"[CSS Performance] Iterations: {iterations}");
        Console.WriteLine($"[CSS Performance] Total Time: {sw.ElapsedMilliseconds} ms ({timePerIterationUs:F2} μs/parse)");
        Console.WriteLine($"[CSS Performance] Memory Allocated: {totalAllocated / (1024.0 * 1024.0):F2} MB ({allocPerIteration:F2} bytes/parse)");
        Console.WriteLine($"[CSS Performance] GC Collections (Gen 0/1/2): {gen0After - gen0Before}/{gen1After - gen1Before}/{gen2After - gen2Before}");
    }

    [Fact]
    public void TestDeepNestedMediaRules()
    {
        int depth = 1000; // Stress test nested media rules at a safe recursion depth
        var sb = new StringBuilder();
        for (int i = 0; i < depth; i++)
        {
            sb.Append($"@media screen and (min-width: {i}px) {{");
        }
        sb.Append("div { color: red; }");
        for (int i = 0; i < depth; i++)
        {
            sb.Append("}");
        }

        string css = sb.ToString();
        var stylesheet = CssParser.Parse(css);
        Assert.NotNull(stylesheet);
        Assert.Single(stylesheet.Rules);
        Assert.Equal("red", stylesheet.Rules[0].Declarations["color"]);
    }
}
