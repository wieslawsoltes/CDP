using System;
using System.Diagnostics;
using System.Text;
using CDP.Html.Parser;
using Xunit;

namespace CDP.Html.Parser.Tests;

public class HtmlParserStressTests
{
    [Fact]
    public void TestDeepNestingHtml()
    {
        // 100,000 levels of nested div tags
        int depth = 100000;
        var sb = new StringBuilder();
        for (int i = 0; i < depth; i++)
        {
            sb.Append("<div>");
        }
        for (int i = 0; i < depth; i++)
        {
            sb.Append("</div>");
        }

        string html = sb.ToString();

        // Measure time and verify it parses without throwing (especially stack overflow)
        var sw = Stopwatch.StartNew();
        var doc = HtmlParser.Parse(html);
        sw.Stop();

        Assert.NotNull(doc);
        
        // Let's verify the tree depth
        HtmlNode current = doc;
        int actualDepth = 0;
        while (current.Children.Count > 0)
        {
            current = current.Children[0];
            actualDepth++;
        }
        Assert.Equal(depth, actualDepth);
        
        // Log info to console/test output
        Console.WriteLine($"[HTML] Deep Nesting (depth={depth}) parsed successfully in {sw.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void TestLargeHtmlDocument()
    {
        // Generate a 5MB HTML string
        var sb = new StringBuilder();
        sb.Append("<html><body>");
        for (int i = 0; i < 50000; i++)
        {
            sb.Append($"<div id=\"node-{i}\" class=\"card container active\" data-index=\"{i}\">");
            sb.Append($"<!-- Comment index {i} -->");
            sb.Append($"<h2 class=\"title\">Title {i} &amp; header</h2>");
            sb.Append($"<p>Paragraph {i} content text showing entity decoding: &lt;tag&gt; and &quot;quotes&quot;</p>");
            sb.Append("<img src=\"avatar.png\" alt=\"avatar\" />");
            sb.Append("<br>");
            sb.Append("</div>");
        }
        sb.Append("</body></html>");
        string html = sb.ToString();

        var sw = Stopwatch.StartNew();
        var doc = HtmlParser.Parse(html);
        sw.Stop();

        Assert.NotNull(doc);
        Assert.True(doc.Children.Count > 0);
        Console.WriteLine($"[HTML] Large HTML (5MB, 50,000 complex items) parsed in {sw.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void TestAdversarialMalformedHtml()
    {
        // A collection of malformed inputs designed to trip up parser state
        string[] malformedInputs = new[]
        {
            "<div><p><span>unclosed tags",
            "<!-- unclosed comment",
            "<!-- nested <!-- comments --> -->",
            "<div><span>overlapping</div></span>",
            "<div class=>empty attribute value",
            "<div class=foo bar=baz>unquoted attributes",
            "<div class=\"unclosed quote>text",
            "<$>invalid tag name",
            "<>empty tag name",
            "</>empty end tag",
            "</div missing start tag",
            "<!---->empty comment",
            "<div   id  =  \"spaced\"   class  =  'spaced'   disabled   >spaces in attributes</div>",
            "<a href=\"url?a=1&b=2\">attributes with entities</a>",
            "&lt;div&gt;raw entities outside tag&lt;/div&gt;",
            null // Should throw ArgumentNullException
        };

        foreach (var input in malformedInputs)
        {
            if (input == null)
            {
                Assert.Throws<ArgumentNullException>(() => HtmlParser.Parse(input!));
            }
            else
            {
                var doc = HtmlParser.Parse(input);
                Assert.NotNull(doc); // Must not throw
            }
        }
    }

    [Fact]
    public void TestScriptTagParsing()
    {
        string html = "<script>if (a < b && c > d) { console.log('hello'); }</script>";
        var doc = HtmlParser.Parse(html);
        Assert.NotNull(doc);
        
        // Let's see what was parsed
        Assert.Single(doc.Children);
        var script = doc.Children[0] as HtmlElement;
        Assert.NotNull(script);
        Assert.Equal("script", script.TagName);
        Assert.Single(script.Children);
        
        var textNode = script.Children[0] as HtmlTextNode;
        Assert.NotNull(textNode);
        Assert.Equal("if (a < b && c > d) { console.log('hello'); }", textNode.Text);
    }

    [Fact]
    public void TestHtmlPerformanceAndAllocations()
    {
        string snippet = "<div class=\"card\"><h2 class=\"title\">Header &amp; title</h2><p>Description text</p><img src=\"img.png\" /></div>";
        
        // Warmup
        for (int i = 0; i < 100; i++)
        {
            HtmlParser.Parse(snippet);
        }

        // Measure time and GC collections for 10,000 iterations
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
            HtmlParser.Parse(snippet);
        }
        sw.Stop();

        long afterAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        int gen0After = GC.CollectionCount(0);
        int gen1After = GC.CollectionCount(1);
        int gen2After = GC.CollectionCount(2);

        long totalAllocated = afterAllocatedBytes - beforeAllocatedBytes;
        double allocPerIteration = (double)totalAllocated / iterations;
        double timePerIterationUs = (double)sw.Elapsed.TotalMilliseconds * 1000 / iterations;

        Console.WriteLine($"[HTML Performance] Iterations: {iterations}");
        Console.WriteLine($"[HTML Performance] Total Time: {sw.ElapsedMilliseconds} ms ({timePerIterationUs:F2} μs/parse)");
        Console.WriteLine($"[HTML Performance] Memory Allocated: {totalAllocated / (1024.0 * 1024.0):F2} MB ({allocPerIteration:F2} bytes/parse)");
        Console.WriteLine($"[HTML Performance] GC Collections (Gen 0/1/2): {gen0After - gen0Before}/{gen1After - gen1Before}/{gen2After - gen2Before}");
    }
}
