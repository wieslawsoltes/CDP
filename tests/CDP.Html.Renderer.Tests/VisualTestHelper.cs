using System;
using System.IO;
using SkiaSharp;
using Xunit;
using CDP.Html.Parser;
using CDP.Css.Parser;
using CDP.Html.Renderer.Layout;
using CDP.Html.Renderer.Style;

namespace CDP.Html.Renderer.Tests;

public static class VisualTestHelper
{
    private static string GetProjectTestsDirectory()
    {
        var dirsToSearch = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
        foreach (var startDir in dirsToSearch)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "tests", "CDP.Html.Renderer.Tests");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                if (dir.Name == "CDP.Html.Renderer.Tests" && File.Exists(Path.Combine(dir.FullName, "CDP.Html.Renderer.Tests.csproj")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "tests", "CDP.Html.Renderer.Tests");
    }

    public static void AssertVisualMatch(string testName, string html, string css, int width, int height)
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
        {
            return;
        }

        // 1. Render HTML/CSS to SKBitmap
        var doc = HtmlParser.Parse(html);
        var stylesheet = CssParser.Parse(css);
        var styles = StyleCascade.ResolveStyles(doc, stylesheet);
        var rootBox = LayoutTreeBuilder.Build(doc, styles);
        LayoutEngine.Layout(rootBox, width, height);

        using var actualBitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(actualBitmap);
        canvas.Clear(SKColors.White);
        HtmlRenderer.Render(rootBox, canvas, 0f, 0f);

        string testsDir = GetProjectTestsDirectory();
        string expectedPath = Path.Combine(testsDir, "ExpectedImages", $"{testName}.png");
        string actualPath = Path.Combine(testsDir, "ActualImages", $"{testName}.png");
        string diffPath = Path.Combine(testsDir, "DiffImages", $"{testName}.png");

        // Bootstrap/Generate Expected Golden Image if missing
        if (!File.Exists(expectedPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath)!);
            using (var fs = File.OpenWrite(expectedPath))
            {
                actualBitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
            }
            return;
        }

        // 2. Perform Image Comparison
        using var expectedBitmap = SKBitmap.Decode(expectedPath);
        bool matches = CompareBitmaps(actualBitmap, expectedBitmap, out var diffBitmap);

        if (!matches)
        {
            try
            {
                // Save Failure Images
                Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);
                Directory.CreateDirectory(Path.GetDirectoryName(diffPath)!);

                using (var fs = File.OpenWrite(actualPath))
                {
                    actualBitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
                }
                using (var fs = File.OpenWrite(diffPath))
                {
                    diffBitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
                }
            }
            finally
            {
                diffBitmap?.Dispose();
            }

            Assert.Fail($"Visual mismatch detected for '{testName}'. Diff generated at '{diffPath}'.");
        }
        else
        {
            diffBitmap?.Dispose();
        }
    }

    private static bool CompareBitmaps(SKBitmap actual, SKBitmap expected, out SKBitmap diff)
    {
        if (actual.Width != expected.Width || actual.Height != expected.Height)
        {
            diff = new SKBitmap(actual.Width, actual.Height);
            using (var canvas = new SKCanvas(diff))
            {
                canvas.Clear(SKColors.Red);
            }
            return false;
        }

        diff = new SKBitmap(actual.Width, actual.Height);
        bool matches = true;

        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                var p1 = actual.GetPixel(x, y);
                var p2 = expected.GetPixel(x, y);

                if (p1 != p2)
                {
                    matches = false;
                    diff.SetPixel(x, y, SKColors.Red); // Highlight mismatch in Red
                }
                else
                {
                    diff.SetPixel(x, y, new SKColor(p1.Red, p1.Green, p1.Blue, 50)); // Matching pixels dimmed
                }
            }
        }
        return matches;
    }
}
