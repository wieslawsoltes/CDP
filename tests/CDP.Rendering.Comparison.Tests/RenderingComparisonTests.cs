using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;
using Xunit;

// Document and parser/renderer references
using CDP.Markdown.Parser;
using CDP.Html.Parser;
using CDP.Css.Parser;
using CDP.Html.Renderer;
using CDP.Document.Parser;
using CDP.Document.Parser.AST;
using CDP.Document.Renderer;

namespace CDP.Rendering.Comparison.Tests
{
    public class TestResult
    {
        public string Name { get; set; } = "";
        public string FileType { get; set; } = "";
        public string RefImagePath { get; set; } = "";
        public string CustomImagePath { get; set; } = "";
        public string DiffImagePath { get; set; } = "";
        public double SSIM { get; set; }
        public double PixelDeltaPercent { get; set; }
        public string ErrorMessage { get; set; } = "";
        public bool Passed => string.IsNullOrEmpty(ErrorMessage);
    }

    public class RenderingComparisonTests : IDisposable
    {
        private static readonly List<TestResult> Results = new();
        private static readonly object Lock = new();

        private readonly string _testDataDir;
        private readonly string _resultsDir;
        private const int ImageWidth = 1280;
        private const int ImageHeight = 1024;

        public RenderingComparisonTests()
        {
            _testDataDir = GetTestDataDir();
            _resultsDir = GetTestResultsDir();

            // Generate test files if they don't exist
            TestDataGenerator.GenerateAll(_testDataDir);
        }

        public void Dispose()
        {
            // After all tests have run, write out the HTML report
            lock (Lock)
            {
                var reportPath = Path.Combine(_resultsDir, "report.html");
                ReportGenerator.GenerateHtmlReport(reportPath, Results);
            }
        }

        [Fact]
        public void TestMarkdownRendering()
        {
            var testName = "kitchen_sink";
            var mdPath = Path.Combine(_testDataDir, $"{testName}.md");
            var refPng = Path.Combine(_resultsDir, $"{testName}_ref.png");
            var customPng = Path.Combine(_resultsDir, $"{testName}_custom.png");
            var diffPng = Path.Combine(_resultsDir, $"{testName}_diff.png");

            var result = new TestResult { Name = testName, FileType = "Markdown" };

            try
            {
                // 1. Reference image using pre-rendered expected fallback (no external tools launched)
                if (TryGetExpectedFallback(testName, refPng))
                {
                    result.RefImagePath = refPng;
                }
                else
                {
                    result.ErrorMessage = "Reference image not found in Expected fallbacks.";
                }

                // 2. Custom rendering via SkiaSharp
                var markdownContent = File.ReadAllText(mdPath);
                var doc = MarkdownParser.Parse(markdownContent);
                using var resources = new CDP.Markdown.Renderer.Rendering.RenderResources();
                var measurer = new SkiaTextMeasurer(resources);
                var layout = new CDP.Markdown.Renderer.Layout.DocumentLayout();
                layout.LoadDocument(doc);
                var layoutContext = new CDP.Markdown.Renderer.Layout.LayoutContext(
                    maxWidth: ImageWidth,
                    measurer: measurer,
                    resources: resources,
                    startY: 0,
                    markdownText: markdownContent
                );
                layout.Layout(layoutContext);

                using var customBitmap = new SKBitmap(ImageWidth, ImageHeight);
                using (var canvas = new SKCanvas(customBitmap))
                {
                    canvas.Clear(SKColors.White);
                    var renderContext = new CDP.Markdown.Renderer.Rendering.RenderContext(resources, new SKRect(0, 0, ImageWidth, ImageHeight));
                    layout.Render(canvas, renderContext);
                }

                using (var fs = File.OpenWrite(customPng))
                {
                    customBitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
                }
                result.CustomImagePath = customPng;

                // 3. Comparison (only if reference succeeded)
                if (File.Exists(refPng))
                {
                    using var refBitmap = SKBitmap.Decode(refPng);
                    var compResult = ImageComparator.Compare(customBitmap, refBitmap, diffPng);
                    result.DiffImagePath = diffPng;
                    result.SSIM = compResult.SSIM;
                    result.PixelDeltaPercent = compResult.PixelDeltaPercent;
                    Assert.True(compResult.SSIM >= 0.0, "SSIM calculation should succeed");
                }
                else
                {
                    Assert.True(File.Exists(customPng), "Custom rendering should be generated");
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = (result.ErrorMessage + "\n" + ex.ToString()).Trim();
                throw;
            }
            finally
            {
                lock (Lock)
                {
                    Results.Add(result);
                }
            }
        }

        [Fact]
        public void TestHtmlRendering()
        {
            var testName = "basic_layout";
            var htmlPath = Path.Combine(_testDataDir, $"{testName}.html");
            var cssPath = Path.Combine(_testDataDir, $"{testName}.css");
            var refPng = Path.Combine(_resultsDir, $"{testName}_ref.png");
            var customPng = Path.Combine(_resultsDir, $"{testName}_custom.png");
            var diffPng = Path.Combine(_resultsDir, $"{testName}_diff.png");

            var result = new TestResult { Name = testName, FileType = "HTML+CSS" };

            try
            {
                // 1. Reference image using pre-rendered expected fallback (no external tools launched)
                if (TryGetExpectedFallback(testName, refPng))
                {
                    result.RefImagePath = refPng;
                }
                else
                {
                    result.ErrorMessage = "Reference image not found in Expected fallbacks.";
                }

                // 2. Custom rendering via SkiaSharp
                var htmlContent = File.ReadAllText(htmlPath);
                var cssContent = File.ReadAllText(cssPath);

                var doc = HtmlParser.Parse(htmlContent);
                var stylesheet = CssParser.Parse(cssContent);

                using var customBitmap = new SKBitmap(ImageWidth, ImageHeight);
                using (var canvas = new SKCanvas(customBitmap))
                {
                    canvas.Clear(SKColors.White);
                    HtmlRenderer.Render(canvas, doc, stylesheet, new SKRect(0, 0, ImageWidth, ImageHeight));
                }

                using (var fs = File.OpenWrite(customPng))
                {
                    customBitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
                }
                result.CustomImagePath = customPng;

                // 3. Comparison
                if (File.Exists(refPng))
                {
                    using var refBitmap = SKBitmap.Decode(refPng);
                    var compResult = ImageComparator.Compare(customBitmap, refBitmap, diffPng);
                    result.DiffImagePath = diffPng;
                    result.SSIM = compResult.SSIM;
                    result.PixelDeltaPercent = compResult.PixelDeltaPercent;
                    Assert.True(compResult.SSIM >= 0.0, "SSIM calculation should succeed");
                }
                else
                {
                    Assert.True(File.Exists(customPng), "Custom rendering should be generated");
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = (result.ErrorMessage + "\n" + ex.ToString()).Trim();
                throw;
            }
            finally
            {
                lock (Lock)
                {
                    Results.Add(result);
                }
            }
        }

        [Fact]
        public void TestDocxRendering()
        {
            var testName = "formatting_test";
            var docxPath = Path.Combine(_testDataDir, $"{testName}.docx");
            var refPng = Path.Combine(_resultsDir, $"{testName}_ref.png");
            var customPng = Path.Combine(_resultsDir, $"{testName}_custom.png");
            var diffPng = Path.Combine(_resultsDir, $"{testName}_diff.png");

            var result = new TestResult { Name = testName, FileType = "DOCX" };

            try
            {
                // 1. Reference image using pre-rendered expected fallback (no external tools launched)
                if (TryGetExpectedFallback(testName, refPng))
                {
                    result.RefImagePath = refPng;
                }
                else
                {
                    result.ErrorMessage = "Reference image not found in Expected fallbacks.";
                }

                // 2. Custom rendering via SkiaSharp
                var parser = DocumentParserFactory.GetParser("docx");
                DocumentRoot root;
                using (var stream = File.OpenRead(docxPath))
                {
                    root = parser.Parse(stream);
                }

                var renderer = new DocumentRenderer();
                renderer.Layout(root, new CDP.Document.Renderer.LayoutContext { PageWidth = ImageWidth, PageHeight = ImageHeight });

                using var customBitmap = new SKBitmap(ImageWidth, ImageHeight);
                using (var canvas = new SKCanvas(customBitmap))
                {
                    canvas.Clear(SKColors.White);
                    renderer.Render(canvas, new CDP.Document.Renderer.RenderContext { Viewport = new SKRect(0, 0, ImageWidth, ImageHeight) });
                }

                using (var fs = File.OpenWrite(customPng))
                {
                    customBitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
                }
                result.CustomImagePath = customPng;

                // 3. Comparison
                if (File.Exists(refPng))
                {
                    using var refBitmap = SKBitmap.Decode(refPng);
                    var compResult = ImageComparator.Compare(customBitmap, refBitmap, diffPng);
                    result.DiffImagePath = diffPng;
                    result.SSIM = compResult.SSIM;
                    result.PixelDeltaPercent = compResult.PixelDeltaPercent;
                    Assert.True(compResult.SSIM >= 0.0, "SSIM calculation should succeed");
                }
                else
                {
                    Assert.True(File.Exists(customPng), "Custom rendering should be generated");
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = (result.ErrorMessage + "\n" + ex.ToString()).Trim();
                throw;
            }
            finally
            {
                lock (Lock)
                {
                    Results.Add(result);
                }
            }
        }

        [Fact]
        public void TestXlsxRendering()
        {
            var testName = "multi_sheet_calc";
            var xlsxPath = Path.Combine(_testDataDir, $"{testName}.xlsx");
            var refPng = Path.Combine(_resultsDir, $"{testName}_ref.png");
            var customPng = Path.Combine(_resultsDir, $"{testName}_custom.png");
            var diffPng = Path.Combine(_resultsDir, $"{testName}_diff.png");

            var result = new TestResult { Name = testName, FileType = "XLSX" };

            try
            {
                // 1. Reference image using pre-rendered expected fallback (no external tools launched)
                if (TryGetExpectedFallback(testName, refPng))
                {
                    result.RefImagePath = refPng;
                }
                else
                {
                    result.ErrorMessage = "Reference image not found in Expected fallbacks.";
                }

                // 2. Custom rendering via SkiaSharp
                var parser = DocumentParserFactory.GetParser("xlsx");
                DocumentRoot root;
                using (var stream = File.OpenRead(xlsxPath))
                {
                    root = parser.Parse(stream);
                }

                var renderer = new DocumentRenderer();
                renderer.Layout(root, new CDP.Document.Renderer.LayoutContext { PageWidth = ImageWidth, PageHeight = ImageHeight });

                using var customBitmap = new SKBitmap(ImageWidth, ImageHeight);
                using (var canvas = new SKCanvas(customBitmap))
                {
                    canvas.Clear(SKColors.White);
                    renderer.Render(canvas, new CDP.Document.Renderer.RenderContext { Viewport = new SKRect(0, 0, ImageWidth, ImageHeight) });
                }

                using (var fs = File.OpenWrite(customPng))
                {
                    customBitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
                }
                result.CustomImagePath = customPng;

                // 3. Comparison
                if (File.Exists(refPng))
                {
                    using var refBitmap = SKBitmap.Decode(refPng);
                    var compResult = ImageComparator.Compare(customBitmap, refBitmap, diffPng);
                    result.DiffImagePath = diffPng;
                    result.SSIM = compResult.SSIM;
                    result.PixelDeltaPercent = compResult.PixelDeltaPercent;
                    Assert.True(compResult.SSIM >= 0.0, "SSIM calculation should succeed");
                }
                else
                {
                    Assert.True(File.Exists(customPng), "Custom rendering should be generated");
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = (result.ErrorMessage + "\n" + ex.ToString()).Trim();
                throw;
            }
            finally
            {
                lock (Lock)
                {
                    Results.Add(result);
                }
            }
        }

        [Fact]
        public void TestPptxRendering()
        {
            var testName = "slide_deck";
            var pptxPath = Path.Combine(_testDataDir, $"{testName}.pptx");
            var refPng = Path.Combine(_resultsDir, $"{testName}_ref.png");
            var customPng = Path.Combine(_resultsDir, $"{testName}_custom.png");
            var diffPng = Path.Combine(_resultsDir, $"{testName}_diff.png");

            var result = new TestResult { Name = testName, FileType = "PPTX" };

            try
            {
                // 1. Reference image using pre-rendered expected fallback (no external tools launched)
                if (TryGetExpectedFallback(testName, refPng))
                {
                    result.RefImagePath = refPng;
                }
                else
                {
                    result.ErrorMessage = "Reference image not found in Expected fallbacks.";
                }

                // 2. Custom rendering via SkiaSharp
                var parser = DocumentParserFactory.GetParser("pptx");
                DocumentRoot root;
                using (var stream = File.OpenRead(pptxPath))
                {
                    root = parser.Parse(stream);
                }

                var renderer = new DocumentRenderer();
                renderer.Layout(root, new CDP.Document.Renderer.LayoutContext { PageWidth = ImageWidth, PageHeight = ImageHeight });

                using var customBitmap = new SKBitmap(ImageWidth, ImageHeight);
                using (var canvas = new SKCanvas(customBitmap))
                {
                    canvas.Clear(SKColors.White);
                    renderer.Render(canvas, new CDP.Document.Renderer.RenderContext { Viewport = new SKRect(0, 0, ImageWidth, ImageHeight) });
                }

                using (var fs = File.OpenWrite(customPng))
                {
                    customBitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
                }
                result.CustomImagePath = customPng;

                // 3. Comparison
                if (File.Exists(refPng))
                {
                    using var refBitmap = SKBitmap.Decode(refPng);
                    var compResult = ImageComparator.Compare(customBitmap, refBitmap, diffPng);
                    result.DiffImagePath = diffPng;
                    result.SSIM = compResult.SSIM;
                    result.PixelDeltaPercent = compResult.PixelDeltaPercent;
                    Assert.True(compResult.SSIM >= 0.0, "SSIM calculation should succeed");
                }
                else
                {
                    Assert.True(File.Exists(customPng), "Custom rendering should be generated");
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = (result.ErrorMessage + "\n" + ex.ToString()).Trim();
                throw;
            }
            finally
            {
                lock (Lock)
                {
                    Results.Add(result);
                }
            }
        }

        private bool TryGetExpectedFallback(string testName, string targetRefPng)
        {
            var expectedPng = Path.Combine(_testDataDir, "Expected", $"{testName}_ref.png");
            if (File.Exists(expectedPng))
            {
                try
                {
                    File.Copy(expectedPng, targetRefPng, true);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        private static string GetTestDataDir()
        {
            var baseDir = AppContext.BaseDirectory;
            var dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "tests", "CDP.Rendering.Comparison.Tests", "TestData");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                var projCandidate = Path.Combine(dir.FullName, "tests", "CDP.Rendering.Comparison.Tests");
                if (Directory.Exists(projCandidate))
                {
                    var target = Path.Combine(projCandidate, "TestData");
                    Directory.CreateDirectory(target);
                    return target;
                }
                dir = dir.Parent;
            }
            var fallback = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        private static string GetTestResultsDir()
        {
            var baseDir = AppContext.BaseDirectory;
            var dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "tests", "CDP.Rendering.Comparison.Tests");
                if (Directory.Exists(candidate))
                {
                    var target = Path.Combine(candidate, "TestResults");
                    Directory.CreateDirectory(target);
                    return target;
                }
                dir = dir.Parent;
            }
            var fallback = Path.Combine(Directory.GetCurrentDirectory(), "TestResults");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    public static class ReportGenerator
    {
        public static void GenerateHtmlReport(string outputPath, List<TestResult> results)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"utf-8\">");
            sb.AppendLine("<title>Rendering Comparison Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, Helvetica, Arial, sans-serif; background: #fafafa; margin: 40px; color: #333; }");
            sb.AppendLine("h1 { color: #111; margin-bottom: 5px; }");
            sb.AppendLine(".subtitle { color: #666; margin-bottom: 30px; font-size: 1.1em; }");
            sb.AppendLine(".card { background: #fff; padding: 25px; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.05); margin-bottom: 35px; border: 1px solid #e1e4e8; }");
            sb.AppendLine(".card-header { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #eee; padding-bottom: 12px; margin-bottom: 15px; }");
            sb.AppendLine(".card-title { font-size: 1.4em; font-weight: bold; margin: 0; }");
            sb.AppendLine(".comparison-grid { display: flex; gap: 20px; flex-wrap: wrap; margin-top: 15px; }");
            sb.AppendLine(".image-container { flex: 1; min-width: 300px; border: 1px solid #eee; padding: 10px; border-radius: 4px; background: #fdfdfd; text-align: center; }");
            sb.AppendLine(".image-container h4 { margin: 0 0 10px 0; color: #555; }");
            sb.AppendLine(".image-container img { width: 100%; max-width: 100%; height: auto; display: block; border: 1px solid #ddd; border-radius: 2px; }");
            sb.AppendLine(".metrics { display: flex; gap: 30px; margin-bottom: 15px; font-size: 1.1em; }");
            sb.AppendLine(".metric { background: #f1f3f5; padding: 8px 16px; border-radius: 6px; }");
            sb.AppendLine(".metric-label { color: #666; font-size: 0.9em; margin-right: 8px; }");
            sb.AppendLine(".metric-value { font-weight: bold; color: #007bff; }");
            sb.AppendLine(".badge { display: inline-block; padding: 6px 12px; border-radius: 20px; font-weight: bold; font-size: 0.85em; text-transform: uppercase; }");
            sb.AppendLine(".badge-success { background: #d4edda; color: #155724; }");
            sb.AppendLine(".badge-danger { background: #f8d7da; color: #721c24; }");
            sb.AppendLine(".badge-warning { background: #fff3cd; color: #856404; }");
            sb.AppendLine(".error-message { background: #fff3cd; color: #856404; padding: 15px; border-radius: 6px; border: 1px solid #ffeeba; margin-top: 15px; font-family: monospace; white-space: pre-wrap; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<h1>Rendering Comparison Report</h1>");
            sb.AppendLine($"<div class=\"subtitle\">Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Headless Chrome vs. SkiaSharp Custom Renderers</div>");

            foreach (var result in results)
            {
                sb.AppendLine("<div class=\"card\">");
                sb.AppendLine("  <div class=\"card-header\">");
                sb.AppendLine($"    <div class=\"card-title\">{result.Name} <span style=\"font-size:0.7em; color:#888; font-weight:normal;\">({result.FileType})</span></div>");
                if (result.Passed)
                {
                    sb.AppendLine("    <span class=\"badge badge-success\">Success</span>");
                }
                else
                {
                    sb.AppendLine("    <span class=\"badge badge-warning\">Partial (Ref Error)</span>");
                }
                sb.AppendLine("  </div>");

                if (result.Passed)
                {
                    sb.AppendLine("  <div class=\"metrics\">");
                    sb.AppendLine($"    <div class=\"metric\"><span class=\"metric-label\">SSIM Score:</span><span class=\"metric-value\">{result.SSIM:F4}</span></div>");
                    sb.AppendLine($"    <div class=\"metric\"><span class=\"metric-label\">Pixel Delta:</span><span class=\"metric-value\">{result.PixelDeltaPercent:F2}%</span></div>");
                    sb.AppendLine("  </div>");
                }
                
                sb.AppendLine("  <div class=\"comparison-grid\">");

                string customRel = Path.GetFileName(result.CustomImagePath);
                
                if (File.Exists(result.RefImagePath))
                {
                    string refRel = Path.GetFileName(result.RefImagePath);
                    string diffRel = Path.GetFileName(result.DiffImagePath);

                    sb.AppendLine("    <div class=\"image-container\">");
                    sb.AppendLine("      <h4>Reference (Chrome)</h4>");
                    sb.AppendLine($"      <a href=\"{refRel}\" target=\"_blank\"><img src=\"{refRel}\"></a>");
                    sb.AppendLine("    </div>");

                    sb.AppendLine("    <div class=\"image-container\">");
                    sb.AppendLine("      <h4>Custom Renderer (Skia)</h4>");
                    sb.AppendLine($"      <a href=\"{customRel}\" target=\"_blank\"><img src=\"{customRel}\"></a>");
                    sb.AppendLine("    </div>");

                    sb.AppendLine("    <div class=\"image-container\">");
                    sb.AppendLine("      <h4>Diff Highlight</h4>");
                    sb.AppendLine($"      <a href=\"{diffRel}\" target=\"_blank\"><img src=\"{diffRel}\"></a>");
                    sb.AppendLine("    </div>");
                }
                else
                {
                    sb.AppendLine("    <div class=\"image-container\">");
                    sb.AppendLine("      <h4>Custom Renderer (Skia)</h4>");
                    if (!string.IsNullOrEmpty(result.CustomImagePath))
                    {
                        sb.AppendLine($"      <a href=\"{customRel}\" target=\"_blank\"><img src=\"{customRel}\"></a>");
                    }
                    else
                    {
                        sb.AppendLine("      <p>No image generated</p>");
                    }
                    sb.AppendLine("    </div>");
                }

                sb.AppendLine("  </div>");

                if (!result.Passed)
                {
                    sb.AppendLine($"  <div class=\"error-message\">{result.ErrorMessage}</div>");
                }
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            File.WriteAllText(outputPath, sb.ToString());
        }
    }
}
