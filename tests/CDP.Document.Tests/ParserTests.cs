using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using CDP.Document.Parser;
using Xunit;

using W = DocumentFormat.OpenXml.Wordprocessing;
using S = DocumentFormat.OpenXml.Spreadsheet;
using P = DocumentFormat.OpenXml.Presentation;
using Pack = DocumentFormat.OpenXml.Packaging;
using AST = CDP.Document.Parser.AST;

namespace CDP.Document.Tests;

public class ParserTests
{
    [Fact]
    public void TestDocxDocumentParser()
    {
        var parser = new DocxDocumentParser();
        using var stream = CreateDocxStream();
        var root = parser.Parse(stream);

        Assert.NotNull(root);
        Assert.IsType<AST.WordDocument>(root);

        // Verify paragraphs and text runs
        var para = root.Children.OfType<AST.ParagraphBlock>().FirstOrDefault();
        Assert.NotNull(para);

        var firstRun = para.Children.OfType<AST.TextRun>().FirstOrDefault();
        Assert.NotNull(firstRun);
        Assert.Equal("Hello World", firstRun.Text);
        Assert.True(firstRun.Bold);
        Assert.True(firstRun.Italic);
        Assert.True(firstRun.Underline);
        Assert.Equal(14.0, firstRun.FontSize);
        Assert.Equal("FF0000", firstRun.Color);

        // Verify line break
        var breakLine = para.Children.OfType<AST.LineBreakInline>().FirstOrDefault();
        Assert.NotNull(breakLine);

        // Verify table
        var table = root.Children.OfType<AST.TableBlock>().FirstOrDefault();
        Assert.NotNull(table);
        var row = table.Children.OfType<AST.TableRowBlock>().FirstOrDefault();
        Assert.NotNull(row);
        var cell = row.Children.OfType<AST.TableCellBlock>().FirstOrDefault();
        Assert.NotNull(cell);
        var cellPara = cell.Children.OfType<AST.ParagraphBlock>().FirstOrDefault();
        Assert.NotNull(cellPara);
        var cellRun = cellPara.Children.OfType<AST.TextRun>().FirstOrDefault();
        Assert.NotNull(cellRun);
        Assert.Equal("Cell Content", cellRun.Text);
    }

    [Fact]
    public void TestXlsxDocumentParser()
    {
        var parser = new XlsxDocumentParser();
        using var stream = CreateXlsxStream();
        var root = parser.Parse(stream);

        Assert.NotNull(root);
        Assert.IsType<AST.SpreadsheetDocument>(root);

        var sheet = root.Children.OfType<AST.WorksheetNode>().FirstOrDefault();
        Assert.NotNull(sheet);
        Assert.Equal("Sheet 1", sheet.Name);

        var row = sheet.Children.OfType<AST.GridRowNode>().FirstOrDefault();
        Assert.NotNull(row);
        Assert.Equal(0, row.RowIndex);

        var cells = row.Children.OfType<AST.GridCellNode>().ToList();
        Assert.Equal(2, cells.Count);

        // Cell A1
        var cellA1 = cells[0];
        Assert.Equal(0, cellA1.ColumnIndex);
        Assert.Equal("Hello Excel", cellA1.DisplayText);
        Assert.Null(cellA1.Formula);
        Assert.False(cellA1.Bold);

        // Cell B1
        var cellB1 = cells[1];
        Assert.Equal(1, cellB1.ColumnIndex);
        Assert.Equal("42", cellB1.DisplayText);
        Assert.Equal("SUM(A1:A1)", cellB1.Formula);
        Assert.True(cellB1.Bold);
        Assert.Equal(16.0, cellB1.FontSize);
        Assert.Equal("FF0000", cellB1.Color);
    }

    [Fact]
    public void TestPptxDocumentParser()
    {
        using var stream = CreatePptxStream();
        var parser = new PptxDocumentParser();
        var root = parser.Parse(stream);

        Assert.NotNull(root);
        Assert.IsType<AST.PresentationDocument>(root);

        var slideNode = root.Children.OfType<AST.SlideNode>().FirstOrDefault();
        Assert.NotNull(slideNode);
        Assert.Equal(0, slideNode.SlideIndex);

        var shape = slideNode.Children.OfType<AST.ShapeNode>().FirstOrDefault();
        Assert.NotNull(shape);
        Assert.Equal("Rectangle", shape.ShapeType);
        Assert.Equal("Slide Shape Text", shape.Text);
        Assert.Equal(10.0, shape.X);
        Assert.Equal(20.0, shape.Y);
        Assert.Equal(50.0, shape.Width);
        Assert.Equal(30.0, shape.Height);
    }

    [Fact]
    public void TestRtfDocumentParser()
    {
        var parser = new RtfDocumentParser();
        using var stream = CreateRtfStream();
        var root = parser.Parse(stream);

        Assert.NotNull(root);
        Assert.IsType<AST.WordDocument>(root);

        var paras = root.Children.OfType<AST.ParagraphBlock>().ToList();
        Assert.Equal(6, paras.Count);

        // Para 1
        var p1 = paras[0];
        var r1 = p1.Children.OfType<AST.TextRun>().FirstOrDefault();
        Assert.NotNull(r1);
        Assert.Equal("This is normal text. ", r1.Text);
        Assert.False(r1.Bold);

        // Para 2
        var p2 = paras[1];
        var r2 = p2.Children.OfType<AST.TextRun>().FirstOrDefault();
        Assert.NotNull(r2);
        Assert.Equal("This is bold text.", r2.Text);
        Assert.True(r2.Bold);

        // Para 3
        var p3 = paras[2];
        var r3 = p3.Children.OfType<AST.TextRun>().FirstOrDefault();
        Assert.NotNull(r3);
        Assert.Equal("This is italic text.", r3.Text);
        Assert.True(r3.Italic);

        // Para 4
        var p4 = paras[3];
        var r4 = p4.Children.OfType<AST.TextRun>().FirstOrDefault();
        Assert.NotNull(r4);
        Assert.Equal("This is underlined text.", r4.Text);
        Assert.True(r4.Underline);

        // Para 5
        var p5 = paras[4];
        var r5 = p5.Children.OfType<AST.TextRun>().FirstOrDefault();
        Assert.NotNull(r5);
        Assert.Equal("This is 14pt text. ", r5.Text);
        Assert.Equal(14.0, r5.FontSize);

        // Para 6
        var p6 = paras[5];
        var r6 = p6.Children.OfType<AST.TextRun>().FirstOrDefault();
        Assert.NotNull(r6);
        Assert.Equal("This is red text. ", r6.Text);
        Assert.Equal("#FF0000", r6.Color);
    }

    [Fact]
    public void TestDocumentParserFactory()
    {
        var docxParser = DocumentParserFactory.GetParser("docx");
        Assert.IsType<DocxDocumentParser>(docxParser);

        var xlsxParser = DocumentParserFactory.GetParser("xlsx");
        Assert.IsType<XlsxDocumentParser>(xlsxParser);

        var pptxParser = DocumentParserFactory.GetParser(".pptx");
        Assert.IsType<PptxDocumentParser>(pptxParser);

        var rtfParser = DocumentParserFactory.GetParser("RTF");
        Assert.IsType<RtfDocumentParser>(rtfParser);

        Assert.Throws<System.NotSupportedException>(() => DocumentParserFactory.GetParser("unknown"));
    }

    [Fact]
    public void TestRtfEdgeCases()
    {
        var parser = new RtfDocumentParser();

        // 1. Nested skipped groups bug verification (fixed: outer skipped group does NOT leak)
        string rtfNestedSkip = @"{\rtf1 {\fonttbl {\* \nestedskipped } text inside outer skipped group} Normal text}";
        var rootNested = parser.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtfNestedSkip)));
        var runsNested = rootNested.Children.SelectMany(c => (c as AST.ParagraphBlock)?.Children ?? Enumerable.Empty<AST.DocumentNode>())
            .OfType<AST.TextRun>().ToList();
        
        var nestedTexts = runsNested.Select(r => r.Text).ToList();
        Assert.DoesNotContain(" text inside outer skipped group", nestedTexts);
        Assert.Contains(" Normal text", nestedTexts);
        
        // 2. Color table empty entries / index shifting (fixed: indices do not shift, empty slots preserved)
        string rtfColors = @"{\rtf1 {\colortbl;\red255\green0\blue0;;\red0\green255\blue0;} \cf4 Green text \cf2 Red text}";
        var rootColors = parser.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtfColors)));
        var runsColors = rootColors.Children.SelectMany(c => (c as AST.ParagraphBlock)?.Children ?? Enumerable.Empty<AST.DocumentNode>())
            .OfType<AST.TextRun>().ToList();

        Assert.Equal("#00FF00", runsColors[1].Color);
        Assert.Equal("#FF0000", runsColors[2].Color);

        // 3. Hex escape parsing with invalid characters (fixed: malformed escape fails back without consuming xy)
        string rtfHex = @"{\rtf1 \'xy}";
        var rootHex = parser.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtfHex)));
        var runsHex = rootHex.Children.SelectMany(c => (c as AST.ParagraphBlock)?.Children ?? Enumerable.Empty<AST.DocumentNode>())
            .OfType<AST.TextRun>().ToList();

        Assert.Equal(2, runsHex.Count);
        Assert.Equal("\\", runsHex[0].Text);
        Assert.Equal("'xy", runsHex[1].Text);

        // 4. Unicode characters (fixed: parses \uN and skips fallback if specified by uc)
        string rtfUnicode = @"{\rtf1 \u9786?}";
        var rootUnicode = parser.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtfUnicode)));
        var runsUnicode = rootUnicode.Children.SelectMany(c => (c as AST.ParagraphBlock)?.Children ?? Enumerable.Empty<AST.DocumentNode>())
            .OfType<AST.TextRun>().ToList();

        Assert.Single(runsUnicode);
        Assert.Equal("\u263A", runsUnicode[0].Text);

        // 5. Stack overflow on trailing whitespace / newlines (fixed: no stack overflow)
        string rtfNewlines = "{\\rtf1 " + new string('\n', 2000) + "}";
        var rootNewlines = parser.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtfNewlines)));
        Assert.NotNull(rootNewlines);
    }

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
\cf2 This is red text. \par
}";
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtf));
    }
}
