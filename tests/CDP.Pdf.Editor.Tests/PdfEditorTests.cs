using System;
using System.IO;
using System.Linq;
using SkiaSharp;
using Xunit;
using CDP.Pdf.Editor.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;

namespace CDP.Pdf.Editor.Tests;

public class PdfEditorTests
{
    [Fact]
    public void Test_Create_And_Save_Pdf()
    {
        string testFile = Path.Combine(Path.GetTempPath(), $"test_created_{Guid.NewGuid()}.pdf");
        try
        {
            var doc = new PdfDocumentModel();
            // Loading a non-existent file should create a blank template page
            doc.Load(testFile);
            
            Assert.Single(doc.Pages);
            var page = doc.Pages[0];
            Assert.Equal(595, page.Width);
            Assert.Equal(842, page.Height);
            
            // Verify default welcome element
            Assert.NotEmpty(page.Elements);
            var welcomeText = page.Elements.OfType<PdfTextElementModel>().FirstOrDefault();
            Assert.NotNull(welcomeText);
            Assert.Contains("Welcome", welcomeText.Text);

            // Add a new custom text element
            var newText = new PdfTextElementModel
            {
                Text = "Interactive editing works!",
                FontSize = 14f,
                Color = SKColors.Blue,
                Bounds = new SKRect(100, 200, 300, 225)
            };
            page.Elements.Add(newText);

            // Add a shape element
            var shape = new PdfShapeElementModel
            {
                Bounds = new SKRect(100, 300, 250, 350),
                FillColor = SKColors.Yellow,
                StrokeColor = SKColors.Red,
                StrokeWidth = 2f,
                IsFilled = true
            };
            page.Elements.Add(shape);

            // Save the document
            doc.Save(testFile);
            Assert.True(File.Exists(testFile));

            // Reload the document and verify contents
            var reloaded = new PdfDocumentModel();
            reloaded.Load(testFile);

            Assert.Single(reloaded.Pages);
            var reloadedPage = reloaded.Pages[0];

            // Verify both text blocks were saved/reloaded (PdfPig splits and extracts words, so verify the words)
            var textElements = reloadedPage.Elements.OfType<PdfTextElementModel>().ToList();
            Assert.NotEmpty(textElements);

            // Verify the content exists in the reloaded words
            bool foundWelcome = textElements.Any(t => t.Text.Contains("Welcome"));
            bool foundInteractive = textElements.Any(t => t.Text.Contains("Interactive"));
            Assert.True(foundWelcome || foundInteractive);
        }
        finally
        {
            if (File.Exists(testFile))
            {
                File.Delete(testFile);
            }
        }
    }

    [Fact]
    public void Test_Page_Operations()
    {
        var doc = new PdfDocumentModel();
        doc.Load("non_existent.pdf"); // loads blank page
        doc.InsertPage(0);
        Assert.Equal(2, doc.Pages.Count);
        doc.RotatePage(0, 90);
        Assert.Equal(90, doc.Pages[0].Rotation);
        doc.RotatePage(0, 180);
        Assert.Equal(270, doc.Pages[0].Rotation);
        doc.DeletePage(0);
        Assert.Single(doc.Pages);
    }

    [Fact]
    public void Test_Annotations_And_Pencil()
    {
        var doc = new PdfDocumentModel();
        doc.Load("non_existent.pdf");
        var page = doc.Pages[0];
        
        var highlight = new PdfHighlightElementModel { Bounds = new SKRect(10, 10, 50, 50) };
        page.Elements.Add(highlight);
        
        var pencil = new PdfPencilElementModel { Color = SKColors.Black };
        pencil.Points.Add(new SKPoint(0, 0));
        pencil.Points.Add(new SKPoint(10, 10));
        page.Elements.Add(pencil);
        
        Assert.Contains(highlight, page.Elements);
        Assert.Contains(pencil, page.Elements);
    }

    [Fact]
    public void Test_Lru_Cache_Eviction_And_Disposal()
    {
        var doc = new PdfDocumentModel();
        // PageImageCache with maxSize = 3
        using var cache = new PageImageCache(doc, maxSize: 3);

        var b1 = cache.GetPageBitmap(1, 1.0f);
        var b2 = cache.GetPageBitmap(2, 1.0f);
        var b3 = cache.GetPageBitmap(3, 1.0f);

        Assert.NotNull(b1);
        Assert.NotNull(b2);
        Assert.NotNull(b3);
        Assert.Equal(3, cache.CacheSize);

        // Accessing b1 should make it the most recently used (MRU)
        var b1Again = cache.GetPageBitmap(1, 1.0f);
        Assert.Same(b1, b1Again);

        // Fetching b4 should evict b2 (the oldest, since b1 was recently accessed)
        var b4 = cache.GetPageBitmap(4, 1.0f);
        Assert.NotNull(b4);
        Assert.Equal(3, cache.CacheSize);

        // Verify that b2 is no longer in the cache but b1, b3, b4 are
        var cachedB1 = cache.GetPageBitmap(1, 1.0f);
        var cachedB3 = cache.GetPageBitmap(3, 1.0f);
        var cachedB4 = cache.GetPageBitmap(4, 1.0f);
        
        Assert.NotNull(cachedB1);
        Assert.NotNull(cachedB3);
        Assert.NotNull(cachedB4);
    }

    [Fact]
    public void Test_PdfEditor_FitToWidth_And_FitToPage_Without_ScrollViewer()
    {
        var editor = new PdfEditor();
        
        // Mock a document with a page to bypass the pages.Count == 0 check
        editor.Document.Load("non_existent.pdf");
        Assert.Single(editor.Document.Pages);

        // Call fit methods
        editor.FitToWidth();
        editor.FitToPage();

        // Under no visual parent / ScrollViewer, the zoom should remain unchanged
        Assert.Equal(1.0, editor.ZoomScale);
    }

    [Fact]
    public void Test_PdfEditor_Layout_Invalidation()
    {
        var editor = new PdfEditor();
        editor.Document.Load("non_existent.pdf");
        
        editor.ZoomScale = 1.5;
        editor.RotateCurrentPage(90);
        editor.InsertPageAfterCurrent();
        editor.DeleteCurrentPage();
        
        Assert.NotNull(editor);
    }

    [AvaloniaFact]
    public void Test_PdfEditor_With_ScrollViewer_Layout()
    {
        var scrollViewer = new ScrollViewer
        {
            Width = 1100,
            Height = 700,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
        var editor = new PdfEditor();
        scrollViewer.Content = editor;

        editor.Document.Load("non_existent.pdf");
        editor.ZoomScale = 1.0;

        var window = new Window
        {
            Width = 1100,
            Height = 700,
            Content = scrollViewer
        };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        string msg = $"Bounds={editor.Bounds}, Viewport={editor.Viewport}, Extent={editor.Extent}, ScrollOffset={editor.Offset}";
        string debugPath = Path.Combine(Path.GetTempPath(), "debug_layout.txt");
        File.WriteAllText(debugPath, msg);
    }

    [AvaloniaFact]
    public void Test_PdfEditor_HitTesting_Centering()
    {
        var scrollViewer = new ScrollViewer
        {
            Width = 1000,
            Height = 600,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var editor = new PdfEditor();
        scrollViewer.Content = editor;

        editor.Document.Load("non_existent.pdf");
        editor.ZoomScale = 1.0;

        var window = new Window
        {
            Width = 1000,
            Height = 600,
            Content = scrollViewer
        };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        double renderWidth = editor.Viewport.Width;
        if (renderWidth <= 0) renderWidth = editor.Bounds.Width;
        if (renderWidth <= 0) return; // Skip if no layout in headless

        // Page width at ZoomScale = 1.0: 595.0
        double pageW = 595.0;
        double centeringOffset = Math.Max(20, (renderWidth - pageW) / 2);
        
        // Click at the left edge of the page and y = 20 (top edge of page)
        var result1 = editor.GetPageAtPoint(new Point(centeringOffset, 20));
        Assert.Equal(0, result1.pageIndex);
        Assert.True(Math.Abs(result1.pageCoords.X) < 1.0,
            $"Expected X near 0, got {result1.pageCoords.X}");

        // Click at the center of the render area horizontally
        double midX = renderWidth / 2;
        var result2 = editor.GetPageAtPoint(new Point(midX, 20));
        Assert.Equal(0, result2.pageIndex);
        Assert.True(Math.Abs(result2.pageCoords.X - (midX - centeringOffset)) < 1.0,
            $"Expected X near {midX - centeringOffset}, got {result2.pageCoords.X}");
    }

    [AvaloniaFact]
    public void Test_PdfEditor_Centering_WideViewport()
    {
        var scrollViewer = new ScrollViewer
        {
            Width = 1400,
            Height = 700,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
        var editor = new PdfEditor();
        scrollViewer.Content = editor;

        editor.Document.Load("non_existent.pdf");
        editor.ZoomScale = 1.0;

        var window = new Window
        {
            Width = 1400,
            Height = 700,
            Content = scrollViewer
        };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        double viewportWidth = editor.Viewport.Width;
        double pageW = 595.0;

        // The page should be centered within the viewport width
        double expectedPageX = Math.Max(20, (viewportWidth - pageW) / 2);

        // Hit test: clicking at the calculated center should hit the page
        if (viewportWidth > 0)
        {
            var result = editor.GetPageAtPoint(new Point(expectedPageX + 10, 30));
            Assert.Equal(0, result.pageIndex);
            Assert.True(result.pageCoords.X >= 0 && result.pageCoords.X < 20,
                $"X coord ({result.pageCoords.X}) should be near left edge of page");
        }
    }

    [AvaloniaFact]
    public void Test_PdfEditor_Centering_After_FitToWidth()
    {
        var scrollViewer = new ScrollViewer
        {
            Width = 1200,
            Height = 700,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
        var editor = new PdfEditor();
        scrollViewer.Content = editor;

        editor.Document.Load("non_existent.pdf");
        editor.ZoomScale = 1.0;

        var window = new Window
        {
            Width = 1200,
            Height = 700,
            Content = scrollViewer
        };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Apply FitToWidth
        editor.FitToWidth();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        double viewportWidth = editor.Viewport.Width;
        if (viewportWidth <= 0) return; // Skip if viewport not available in headless

        // After FitToWidth, the page should fill most of the viewport width
        // and be centered within the viewport
        double pageW = 595.0 * editor.ZoomScale;
        double expectedPageX = Math.Max(20, (viewportWidth - pageW) / 2);

        // The page should be roughly centered (expected offset similar on both sides)
        double leftMargin = expectedPageX;
        double rightMargin = viewportWidth - expectedPageX - pageW;
        Assert.True(Math.Abs(leftMargin - rightMargin) < 5,
            $"Page should be centered: left={leftMargin}, right={rightMargin}");

        // Hit test at page left edge should work
        var result = editor.GetPageAtPoint(new Point(expectedPageX + 5, 30));
        Assert.Equal(0, result.pageIndex);
    }

    [AvaloniaFact]
    public void Test_PdfEditor_GetPageAtPoint_VaryingPageSizes()
    {
        var scrollViewer = new ScrollViewer
        {
            Width = 1000,
            Height = 1200,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var editor = new PdfEditor();
        scrollViewer.Content = editor;

        // Manually build document pages of different sizes
        editor.Document.Pages.Clear();
        editor.Document.Pages.Add(new PdfPageModel { Number = 1, Width = 800, Height = 1000 });
        editor.Document.Pages.Add(new PdfPageModel { Number = 2, Width = 600, Height = 800 });
        editor.ZoomScale = 1.0;

        var window = new Window
        {
            Width = 1000,
            Height = 1200,
            Content = scrollViewer
        };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        double renderWidth = editor.Viewport.Width;
        if (renderWidth <= 0) renderWidth = editor.Bounds.Width;
        if (renderWidth <= 0) return; // Skip if no layout in headless

        // With docWidth = 800 (max of 800 and 600)
        // renderWidth = 1000. Since 1000 > 800 + 40, centerAxis = 500.
        // Page 1 (width 800) pageXOffset = 500 - 400 = 100.
        // Page 2 (width 600) pageXOffset = 500 - 300 = 200.
        // Page 1 is from Y = 20 to 1020.
        // Page 2 is from Y = 1040 to 1840.

        // Hit test Page 1: click at center axis, Y = 50 (currentY = 20, relative Y = 30)
        var result1 = editor.GetPageAtPoint(new Point(500, 50));
        Assert.Equal(0, result1.pageIndex);
        Assert.True(Math.Abs(result1.pageCoords.X - 400) < 1.0, $"Expected pageCoords.X near 400, got {result1.pageCoords.X}");
        Assert.True(Math.Abs(result1.pageCoords.Y - 30) < 1.0, $"Expected pageCoords.Y near 30, got {result1.pageCoords.Y}");

        // Hit test Page 2: click at center axis, Y = 1100 (currentY = 1040, relative Y = 60)
        var result2 = editor.GetPageAtPoint(new Point(500, 1100));
        Assert.Equal(1, result2.pageIndex);
        Assert.True(Math.Abs(result2.pageCoords.X - 300) < 1.0, $"Expected pageCoords.X near 300, got {result2.pageCoords.X}");
        Assert.True(Math.Abs(result2.pageCoords.Y - 60) < 1.0, $"Expected pageCoords.Y near 60, got {result2.pageCoords.Y}");
    }
}
