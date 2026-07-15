using System;
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using CDP.Markdown.Parser;
using CDP.Document.Parser;
using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;
using S = DocumentFormat.OpenXml.Spreadsheet;
using P = DocumentFormat.OpenXml.Presentation;
using Pack = DocumentFormat.OpenXml.Packaging;

namespace Avalonia.Diagnostics.Cdp.Benchmarks;

[MemoryDiagnoser]
public class ParserBenchmarks
{
    private string _typicalMarkdown = string.Empty;
    private string _largeMarkdown = string.Empty;

    private byte[] _docxBytes = Array.Empty<byte>();
    private byte[] _xlsxBytes = Array.Empty<byte>();
    private byte[] _pptxBytes = Array.Empty<byte>();
    private byte[] _rtfBytes = Array.Empty<byte>();

    private DocxDocumentParser _docxParser = null!;
    private XlsxDocumentParser _xlsxParser = null!;
    private PptxDocumentParser _pptxParser = null!;
    private RtfDocumentParser _rtfParser = null!;

    [GlobalSetup]
    public void Setup()
    {
        // 1. Markdown inputs
        _typicalMarkdown = @"# Title

Some text with `inline code` and [links](http://example.com ""title"").

- [ ] Task 1
- [x] Task 2

> Blockquote with nested text.
> Line 2.

| Col A | Col B |
| :--- | ---: |
| A1 | B1 |
| A2 | B2 |

---

And a paragraph at the end with ~~strikethrough~~ and **bold** and *italic*.
";
        // Create large markdown by repeating typical markdown
        var sb = new StringBuilder();
        for (int i = 0; i < 50; i++)
        {
            sb.AppendLine(_typicalMarkdown);
        }
        _largeMarkdown = sb.ToString();

        // 2. Document parser instances
        _docxParser = new DocxDocumentParser();
        _xlsxParser = new XlsxDocumentParser();
        _pptxParser = new PptxDocumentParser();
        _rtfParser = new RtfDocumentParser();

        // 3. Document bytes generation
        using (var docxStream = CreateDocxStream())
        {
            _docxBytes = docxStream.ToArray();
        }
        using (var xlsxStream = CreateXlsxStream())
        {
            _xlsxBytes = xlsxStream.ToArray();
        }
        using (var pptxStream = CreatePptxStream())
        {
            _pptxBytes = pptxStream.ToArray();
        }
        using (var rtfStream = CreateRtfStream())
        {
            _rtfBytes = rtfStream.ToArray();
        }
    }

    [Benchmark]
    public object ParseMarkdown_Typical()
    {
        return MarkdownParser.Parse(_typicalMarkdown);
    }

    [Benchmark]
    public object ParseMarkdown_Large()
    {
        return MarkdownParser.Parse(_largeMarkdown);
    }

    [Benchmark]
    public object ParseDocx()
    {
        using var stream = new MemoryStream(_docxBytes);
        return _docxParser.Parse(stream);
    }

    [Benchmark]
    public object ParseXlsx()
    {
        using var stream = new MemoryStream(_xlsxBytes);
        return _xlsxParser.Parse(stream);
    }

    [Benchmark]
    public object ParsePptx()
    {
        using var stream = new MemoryStream(_pptxBytes);
        return _pptxParser.Parse(stream);
    }

    [Benchmark]
    public object ParseRtf()
    {
        using var stream = new MemoryStream(_rtfBytes);
        return _rtfParser.Parse(stream);
    }

    // Helper generation methods
    private MemoryStream CreateDocxStream()
    {
        var stream = new MemoryStream();
        using (var wordDoc = Pack.WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new W.Document(new W.Body());
            var body = mainPart.Document.Body!;

            var p = new W.Paragraph();
            var run = new W.Run();
            var runProps = new W.RunProperties();
            runProps.AppendChild(new W.Bold());
            runProps.AppendChild(new W.Italic());
            runProps.AppendChild(new W.Underline());
            runProps.AppendChild(new W.FontSize { Val = "28" });
            runProps.AppendChild(new W.Color { Val = "FF0000" });
            run.AppendChild(runProps);
            run.AppendChild(new W.Text("Hello World"));
            p.AppendChild(run);

            p.AppendChild(new W.Run(new W.Break()));
            p.AppendChild(new W.Run(new W.Text("New Line")));
            body.AppendChild(p);

            var table = new W.Table();
            var row = new W.TableRow();
            var cell = new W.TableCell();
            cell.AppendChild(new W.Paragraph(new W.Run(new W.Text("Cell Content"))));
            row.AppendChild(cell);
            table.AppendChild(row);
            body.AppendChild(table);

            mainPart.Document.Save();
        }
        stream.Position = 0;
        return stream;
    }

    private MemoryStream CreateXlsxStream()
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

            var stylesPart = workbookPart.AddNewPart<Pack.WorkbookStylesPart>();
            var stylesheet = new S.Stylesheet();

            var fonts = new S.Fonts(
                new S.Font(),
                new S.Font(
                    new S.Bold(),
                    new S.FontSize { Val = 16 },
                    new S.Color { Rgb = "FF0000" }
                )
            );
            stylesheet.AppendChild(fonts);

            var cellFormats = new S.CellFormats(
                new S.CellFormat(),
                new S.CellFormat { FontId = 1, ApplyFont = true }
            );
            stylesheet.AppendChild(cellFormats);
            stylesPart.Stylesheet = stylesheet;
            stylesPart.Stylesheet.Save();

            var row = new S.Row { RowIndex = 1 };

            var cellA1 = new S.Cell
            {
                CellReference = "A1",
                DataType = S.CellValues.String,
                CellValue = new S.CellValue("Hello Excel")
            };
            row.AppendChild(cellA1);

            var cellB1 = new S.Cell
            {
                CellReference = "B1",
                DataType = S.CellValues.Number,
                CellFormula = new S.CellFormula("SUM(A1:A1)"),
                CellValue = new S.CellValue("42"),
                StyleIndex = 1
            };
            row.AppendChild(cellB1);

            sheetData.AppendChild(row);

            worksheetPart.Worksheet.Save();
            workbookPart.Workbook.Save();
        }
        stream.Position = 0;
        return stream;
    }

    private MemoryStream CreatePptxStream()
    {
        var stream = new MemoryStream();
        using (var presentationDoc = Pack.PresentationDocument.Create(stream, PresentationDocumentType.Presentation, true))
        {
            var presentationPart = presentationDoc.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();

            var slideIdList = new P.SlideIdList();
            presentationPart.Presentation.AppendChild(slideIdList);

            var slidePart = presentationPart.AddNewPart<Pack.SlidePart>();
            var slide = new P.Slide();
            slide.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
            slide.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            slide.AddNamespaceDeclaration("p", "http://schemas.openxmlformats.org/presentationml/2006/main");
            
            var cSld = new P.CommonSlideData(new P.ShapeTree());
            slide.AppendChild(cSld);
            slidePart.Slide = slide;

            var slideId = new P.SlideId
            {
                Id = 256,
                RelationshipId = presentationPart.GetIdOfPart(slidePart)
            };
            slideIdList.AppendChild(slideId);

            var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;

            var nvGrpSpPr = new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()
            );
            shapeTree.AppendChild(nvGrpSpPr);
            shapeTree.AppendChild(new P.GroupShapeProperties());

            var sp = new P.Shape();
            var nvSpPr = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = 2, Name = "TextBox 1" },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()
            );
            sp.AppendChild(nvSpPr);

            var spPr = new P.ShapeProperties();
            var xfrm = new DocumentFormat.OpenXml.Drawing.Transform2D(
                new DocumentFormat.OpenXml.Drawing.Offset { X = 127000L, Y = 254000L },
                new DocumentFormat.OpenXml.Drawing.Extents { Cx = 635000L, Cy = 381000L }
            );
            spPr.AppendChild(xfrm);
            spPr.AppendChild(new DocumentFormat.OpenXml.Drawing.PresetGeometry { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle });
            sp.AppendChild(spPr);

            var textBody = new P.TextBody(
                new DocumentFormat.OpenXml.Drawing.BodyProperties(),
                new DocumentFormat.OpenXml.Drawing.ListStyle(),
                new DocumentFormat.OpenXml.Drawing.Paragraph(
                    new DocumentFormat.OpenXml.Drawing.Run(
                        new DocumentFormat.OpenXml.Drawing.Text("Slide Shape Text")
                    )
                )
            );
            sp.AppendChild(textBody);
            shapeTree.AppendChild(sp);

            slidePart.Slide.Save();
            presentationPart.Presentation.Save();
        }
        stream.Position = 0;
        return stream;
    }

    private MemoryStream CreateRtfStream()
    {
        string rtf = @"{\rtf1\ansi\deff0
{\colortbl;\red255\green0\blue0;}
This is normal text. \par
\b This is bold text.\b0 \par
\i This is italic text.\i0 \par
\ul This is underlined text.\ulnone \par
\fs28 This is 14pt text. \par
\cf1 This is red text. \par
}";
        return new MemoryStream(Encoding.UTF8.GetBytes(rtf));
    }
}
