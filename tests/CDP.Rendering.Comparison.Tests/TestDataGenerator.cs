using System;
using System.IO;
using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;
using S = DocumentFormat.OpenXml.Spreadsheet;
using P = DocumentFormat.OpenXml.Presentation;
using Pack = DocumentFormat.OpenXml.Packaging;

namespace CDP.Rendering.Comparison.Tests
{
    public static class TestDataGenerator
    {
        public static void GenerateAll(string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            // 1. kitchen_sink.md
            var mdPath = Path.Combine(targetDir, "kitchen_sink.md");
            if (!File.Exists(mdPath))
            {
                File.WriteAllText(mdPath, @"# Kitchen Sink Markdown

This is a paragraph with **bold** text and *italic* text.

## Lists
- Item 1
- Item 2
  - Nested Item 2.1

## Code
Inline code: `var x = 10;`

```csharp
public class HelloWorld
{
    public static void Main()
    {
        Console.WriteLine(""Hello World"");
    }
}
```

## Table
| Header 1 | Header 2 |
|----------|----------|
| Cell 1   | Cell 2   |
");
            }

            // 2. basic_layout.html
            var htmlPath = Path.Combine(targetDir, "basic_layout.html");
            if (!File.Exists(htmlPath))
            {
                File.WriteAllText(htmlPath, @"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <link rel=""stylesheet"" href=""basic_layout.css"">
</head>
<body>
  <div class=""container"">
    <h1>HTML Layout Test</h1>
    <p class=""intro"">This is a paragraph inside a styled container.</p>
    <div class=""box red"">Red Box</div>
    <div class=""box blue"">Blue Box</div>
  </div>
</body>
</html>
");
            }

            // 3. basic_layout.css
            var cssPath = Path.Combine(targetDir, "basic_layout.css");
            if (!File.Exists(cssPath))
            {
                File.WriteAllText(cssPath, @"body {
  font-family: Arial, sans-serif;
  background-color: #f0f0f0;
  margin: 20px;
}
.container {
  background-color: white;
  padding: 20px;
  border: 1px solid #ccc;
  border-radius: 5px;
  max-width: 600px;
}
h1 {
  color: #333;
}
.intro {
  font-size: 16px;
  color: #666;
}
.box {
  display: inline-block;
  width: 100px;
  height: 100px;
  margin: 10px;
  color: white;
  text-align: center;
  line-height: 100px;
}
.red {
  background-color: #ff5252;
}
.blue {
  background-color: #2196f3;
}
");
            }

            // 4. formatting_test.docx
            var docxPath = Path.Combine(targetDir, "formatting_test.docx");
            if (!File.Exists(docxPath))
            {
                using var stream = File.Create(docxPath);
                using var wordDoc = Pack.WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true);
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
                run.AppendChild(new W.Text("Formatting Test - DOCX"));
                p.AppendChild(run);

                p.AppendChild(new W.Run(new W.Break()));
                p.AppendChild(new W.Run(new W.Text("This is normal text after a line break.")));
                body.AppendChild(p);

                var table = new W.Table();
                var row = new W.TableRow();
                var cell = new W.TableCell();
                cell.AppendChild(new W.Paragraph(new W.Run(new W.Text("Cell Content 1"))));
                row.AppendChild(cell);

                var cell2 = new W.TableCell();
                cell2.AppendChild(new W.Paragraph(new W.Run(new W.Text("Cell Content 2"))));
                row.AppendChild(cell2);

                table.AppendChild(row);
                body.AppendChild(table);

                mainPart.Document.Save();
            }

            // 5. multi_sheet_calc.xlsx
            var xlsxPath = Path.Combine(targetDir, "multi_sheet_calc.xlsx");
            if (!File.Exists(xlsxPath))
            {
                using var stream = File.Create(xlsxPath);
                using var spreadsheetDoc = Pack.SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true);
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
                    CellValue = new S.CellValue("A1 Text")
                };
                row.AppendChild(cellA1);

                var cellB1 = new S.Cell
                {
                    CellReference = "B1",
                    DataType = S.CellValues.Number,
                    CellFormula = new S.CellFormula("SUM(A1:A1)"),
                    CellValue = new S.CellValue("100"),
                    StyleIndex = 1
                };
                row.AppendChild(cellB1);

                sheetData.AppendChild(row);

                worksheetPart.Worksheet.Save();
                workbookPart.Workbook.Save();
            }

            // 6. slide_deck.pptx
            var pptxPath = Path.Combine(targetDir, "slide_deck.pptx");
            if (!File.Exists(pptxPath))
            {
                using var stream = File.Create(pptxPath);
                using var presentationDoc = Pack.PresentationDocument.Create(stream, PresentationDocumentType.Presentation, true);
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
                            new DocumentFormat.OpenXml.Drawing.Text("Slide deck text")
                        )
                    )
                );
                sp.AppendChild(textBody);
                shapeTree.AppendChild(sp);

                slidePart.Slide.Save();
                presentationPart.Presentation.Save();
            }
        }
    }
}
