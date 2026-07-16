using System;
using BenchmarkDotNet.Attributes;
using CDP.Html.Parser;
using CDP.Css.Parser;
using CDP.Html.Renderer.Layout;
using CDP.Html.Renderer.Style;
using UnoptimizedCDP.Html.Parser;
using UnoptimizedCDP.Css.Parser;
using UnoptimizedCDP.Html.Renderer.Style;
using UnoptimizedCDP.Html.Renderer.Layout;

namespace Avalonia.Diagnostics.Cdp.Benchmarks;

[MemoryDiagnoser]
public class HtmlCssBenchmarks
{
    private string _html = string.Empty;
    private string _css = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _html = @"<div id=""container"" class=""main-container"" style=""display: flex; flex-direction: column; margin: 10px; padding: 20px;"">
  <div class=""header"" style=""background-color: rgb(240, 240, 240); padding: 10px; border-bottom: 2px solid rgb(0,0,0);"">
    <h1 id=""title"" class=""primary-text"" style=""font-size: 24px; font-weight: bold; color: rgb(33, 33, 33);"">CDP Performance Benchmarks</h1>
    <p class=""subtitle"" style=""font-size: 14px; font-style: italic; color: rgb(100, 100, 100);"">HTML and CSS engine evaluation (R3)</p>
  </div>
  <div class=""content"" style=""display: flex; flex-direction: row; margin-top: 15px;"">
    <div id=""sidebar"" class=""sidebar-panel"" style=""width: 200px; padding-right: 15px;"">
      <ul class=""nav-list"">
        <li class=""nav-item active""><a href=""#"" style=""color: blue;"">Dashboard</a></li>
        <li class=""nav-item""><a href=""#"" style=""color: gray;"">Settings</a></li>
        <li class=""nav-item""><a href=""#"" style=""color: gray;"">Profile</a></li>
        <li class=""nav-item""><a href=""#"" style=""color: gray;"">Reports</a></li>
      </ul>
    </div>
    <div id=""main-panel"" class=""details-panel"" style=""flex: 1; padding: 10px; background-color: white;"">
      <div class=""card"" style=""margin-bottom: 15px; padding: 15px; border: 1px solid gray;"">
        <h2 class=""card-title"" style=""font-size: 18px; margin-bottom: 8px;"">Section 1: Parser Stats</h2>
        <p class=""card-desc"">The parser utilizes thread-safe pooled strings to minimize allocations and garbage collection overhead.</p>
        <div class=""stat-group"" style=""display: flex; justify-content: justify-content;"">
          <div class=""stat-box"" style=""margin-right: 20px;"">
            <span class=""stat-value"" style=""font-weight: bold;"">30%</span>
            <span class=""stat-label"" style=""font-size: 12px; color: gray;"">Speedup Target</span>
          </div>
          <div class=""stat-box"">
            <span class=""stat-value"" style=""font-weight: bold;"">50%</span>
            <span class=""stat-label"" style=""font-size: 12px; color: gray;"">Memory reduction Target</span>
          </div>
        </div>
      </div>
      <div class=""card"" style=""padding: 15px; border: 1px solid gray;"">
        <h2 class=""card-title"" style=""font-size: 18px; margin-bottom: 8px;"">Section 2: Style Cascade</h2>
        <p class=""card-desc"">Selector matching supports id, class, tag names, and combinator rules like nested child selectors.</p>
      </div>
    </div>
  </div>
  <div class=""footer"" style=""margin-top: 20px; padding-top: 10px; border-top: 1px solid rgb(200, 200, 200); text-align: text-align;"">
    <span class=""copyright"" style=""font-size: 11px; color: gray;"">Copyright 2026 CDP Team</span>
  </div>
</div>";

        _css = @"div { display: block; box-sizing: border-box; }
.main-container { width: 800px; background-color: #ffffff; }
#container { min-height: 600px; }
.header h1 { font-family: sans-serif; font-size: 28px; }
.primary-text { color: #333333; }
#title { margin: 0; }
.subtitle { margin-top: 4px; }
.content { display: flex; }
.sidebar-panel { background-color: #f8f9fa; }
.nav-list { display: block; list-style-type: none; padding: 0; margin: 0; }
.nav-item { padding: 8px 12px; }
.nav-item.active { background-color: #e9ecef; }
.nav-item a { text-decoration: none; font-weight: bold; }
.details-panel { flex-grow: 1; }
.card { border-radius: 4px; }
.card-title { color: #212529; font-weight: 600; }
.card-desc { color: #495057; font-size: 14px; line-height: 1.5; }
.stat-group { display: flex; }
.stat-box { padding: 10px; background-color: #f1f3f5; border-radius: 4px; }
.stat-value { font-size: 20px; color: #007bff; }
.stat-label { display: block; }
.footer .copyright { font-style: italic; }
#main-panel .card h2 { color: #1a252f; }
div > p { margin-bottom: 10px; }";
    }

    [Benchmark(Baseline = true)]
    public void ParseHtmlAndCss_Unoptimized()
    {
        var doc = UnoptimizedHtmlParser.Parse(_html);
        var stylesheet = UnoptimizedCssParser.Parse(_css);
        var styles = UnoptimizedStyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = LayoutTreeBuilder.Build(doc, styles);
        UnoptimizedLayoutEngine.Layout(rootBox, 800f, 600f);
    }

    [Benchmark]
    public void ParseHtmlAndCss_Optimized()
    {
        var doc = HtmlParser.Parse(_html);
        var stylesheet = CssParser.Parse(_css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(rootBox, 800f, 600f);
    }
}
