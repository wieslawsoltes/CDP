using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using DocumentFormat.OpenXml;
using CDP.Document.Parser;
using Xunit;

using W = DocumentFormat.OpenXml.Wordprocessing;
using S = DocumentFormat.OpenXml.Spreadsheet;
using P = DocumentFormat.OpenXml.Presentation;
using Pack = DocumentFormat.OpenXml.Packaging;
using AST = CDP.Document.Parser.AST;

namespace CDP.Document.Tests;

public class RobustnessTests
{
    // ==========================================
    // 1. EMPTY DOCUMENTS & NULL STREAMS
    // ==========================================

    [Fact]
    public void TestDocx_EmptyStream_ThrowsException()
    {
        var parser = new DocxDocumentParser();
        using var stream = new MemoryStream();
        
        // Assert that parsing an empty stream throws OpenXmlPackageException or FileFormatException
        Assert.ThrowsAny<Exception>(() => parser.Parse(stream));
    }

    [Fact]
    public void TestXlsx_EmptyStream_ThrowsException()
    {
        var parser = new XlsxDocumentParser();
        using var stream = new MemoryStream();
        
        Assert.ThrowsAny<Exception>(() => parser.Parse(stream));
    }

    [Fact]
    public void TestPptx_EmptyStream_ThrowsException()
    {
        var parser = new PptxDocumentParser();
        using var stream = new MemoryStream();
        
        Assert.ThrowsAny<Exception>(() => parser.Parse(stream));
    }

    [Fact]
    public void TestRtf_EmptyStream_ReturnsEmptyDocument()
    {
        var parser = new RtfDocumentParser();
        using var stream = new MemoryStream();
        
        var root = parser.Parse(stream);
        Assert.NotNull(root);
        Assert.IsType<AST.WordDocument>(root);
        Assert.Empty(root.Children);
    }

    // ==========================================
    // 2. INVALID OPENXML HEADERS
    // ==========================================

    [Fact]
    public void TestDocx_InvalidHeader_ThrowsException()
    {
        var parser = new DocxDocumentParser();
        var bytes = System.Text.Encoding.UTF8.GetBytes("INVALID_ZIP_ARCHIVE_HEADER_DATA_1234567890");
        using var stream = new MemoryStream(bytes);
        
        Assert.ThrowsAny<Exception>(() => parser.Parse(stream));
    }

    [Fact]
    public void TestXlsx_InvalidHeader_ThrowsException()
    {
        var parser = new XlsxDocumentParser();
        var bytes = System.Text.Encoding.UTF8.GetBytes("INVALID_ZIP_ARCHIVE_HEADER_DATA_1234567890");
        using var stream = new MemoryStream(bytes);
        
        Assert.ThrowsAny<Exception>(() => parser.Parse(stream));
    }

    [Fact]
    public void TestPptx_InvalidHeader_ThrowsException()
    {
        var parser = new PptxDocumentParser();
        var bytes = System.Text.Encoding.UTF8.GetBytes("INVALID_ZIP_ARCHIVE_HEADER_DATA_1234567890");
        using var stream = new MemoryStream(bytes);
        
        Assert.ThrowsAny<Exception>(() => parser.Parse(stream));
    }

    // ==========================================
    // 3. EXTREMELY LARGE SHEET ROWS
    // ==========================================

    [Fact]
    public void TestXlsx_LargeSheetRows_PerformanceAndMemory()
    {
        var parser = new XlsxDocumentParser();
        int numRows = 10000; // Let's test with 10k rows to keep test execution fast but meaningful
        using var stream = CreateLargeXlsxStream(numRows);
        
        var sw = Stopwatch.StartNew();
        var root = parser.Parse(stream);
        sw.Stop();
        
        Assert.NotNull(root);
        var sheet = root.Children.OfType<AST.WorksheetNode>().FirstOrDefault();
        Assert.NotNull(sheet);
        Assert.Equal(numRows, sheet.Children.Count);
        
        // Assert execution time is within reasonable bounds (e.g. under 5 seconds for 10k rows)
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Parsing 10k rows took too long: {sw.ElapsedMilliseconds}ms");
    }

    // ==========================================
    // 4. NESTED GROUP RTFS & SKIPPED GROUP BUG
    // ==========================================

    [Fact]
    public void TestRtf_DeeplyNestedGroups_DoesNotCrash()
    {
        var parser = new RtfDocumentParser();
        // Generate deeply nested groups: { { { ... } } }
        int depth = 500;
        string rtf = new string('{', depth) + "Hello Nested RTF" + new string('}', depth);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtf));

        var root = parser.Parse(stream);
        Assert.NotNull(root);
        var para = root.Children.OfType<AST.ParagraphBlock>().FirstOrDefault();
        Assert.NotNull(para);
        var run = para.Children.OfType<AST.TextRun>().FirstOrDefault();
        Assert.NotNull(run);
        Assert.Equal("Hello Nested RTF", run.Text);
    }

    [Fact]
    public void TestRtf_NestedSkippedGroups_DoesNotLeakContent()
    {
        var parser = new RtfDocumentParser();
        
        // We have a skipped group (\fonttbl) containing a nested skipped group (\*\generator)
        // If skipGroupDepth is incorrectly reset by the inner group ending, the remaining 
        // part of the outer group (i.e. 'Times;') will be wrongly parsed as visible text!
        string rtf = @"{\rtf1\ansi
{\fonttbl {\f0\fnil Arial; {\*\generator Hello} \f1\fnil Times;}}
This is valid text.
}";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtf));
        
        var root = parser.Parse(stream);
        Assert.NotNull(root);
        
        var textRuns = root.Children
            .SelectMany(p => p.Children.OfType<AST.TextRun>())
            .Select(r => r.Text)
            .ToList();
            
        // "This is valid text." should be the only visible text run.
        // If the bug exists, "Times;" or similar font table remnants might be leaked as text runs.
        Assert.Contains("This is valid text.", textRuns);
        
        // Verify that we do not leak metadata/ignored group contents
        Assert.DoesNotContain("Times;", textRuns);
        Assert.DoesNotContain("Arial;", textRuns);
        Assert.DoesNotContain("Hello", textRuns);
    }

    // ==========================================
    // 5. RTF STACK OVERFLOW VULNERABILITY
    // ==========================================

    [Fact]
    public void TestRtf_ConsecutiveNewlines_NoStackOverflow()
    {
        var parser = new RtfDocumentParser();
        
        // An RTF file containing many consecutive newlines can trigger a StackOverflowException
        // due to recursive calls in RtfLexer.NextToken() when text.Length == 0.
        // We test with 20,000 newlines. If a stack overflow occurs, the test process will terminate.
        string newlines = new string('\n', 20000);
        string rtf = "{\\rtf1\\ansi\n" + newlines + "Hello\n}";
        
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtf));
        
        // If there is a stack overflow, the test runner will crash. If it passes or throws a manageable 
        // exception, it handles it (though passing is expected).
        var root = parser.Parse(stream);
        Assert.NotNull(root);
    }

    [Fact]
    public void TestRtf_MismatchedBrackets_DoesNotCrash()
    {
        var parser = new RtfDocumentParser();
        
        // Too many closing brackets
        string rtf1 = "{\\rtf1 Hello}}}}";
        var root1 = parser.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtf1)));
        Assert.NotNull(root1);
        
        // Too many opening brackets
        string rtf2 = "{{{{\\rtf1 Hello";
        var root2 = parser.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtf2)));
        Assert.NotNull(root2);
    }

    // Helper to generate a large Xlsx stream
    private MemoryStream CreateLargeXlsxStream(int numRows)
    {
        var stream = new MemoryStream();
        using (var spreadsheetDoc = Pack.SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = spreadsheetDoc.AddWorkbookPart();
            workbookPart.Workbook = new S.Workbook();

            var worksheetPart = workbookPart.AddNewPart<Pack.WorksheetPart>();
            worksheetPart.Worksheet = new S.Worksheet(new S.SheetData());

            var sheets = spreadsheetDoc.WorkbookPart!.Workbook.AppendChild(new S.Sheets());
            var sheet = new S.Sheet
            {
                Id = spreadsheetDoc.WorkbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet 1"
            };
            sheets.AppendChild(sheet);

            var sheetData = worksheetPart.Worksheet.GetFirstChild<S.SheetData>()!;

            for (uint i = 1; i <= (uint)numRows; i++)
            {
                var row = new S.Row { RowIndex = i };
                var cell = new S.Cell
                {
                    CellReference = $"A{i}",
                    DataType = S.CellValues.String,
                    CellValue = new S.CellValue($"Value {i}")
                };
                row.AppendChild(cell);
                sheetData.AppendChild(row);
            }

            worksheetPart.Worksheet.Save();
            workbookPart.Workbook.Save();
        }
        stream.Position = 0;
        return stream;
    }
}
