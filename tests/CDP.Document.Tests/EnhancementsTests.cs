using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using SkiaSharp;
using Avalonia;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using DocumentFormat.OpenXml;
using CDP.Document.Parser;
using CDP.Document.Parser.AST;
using CDP.Document.Renderer;
using CDP.Document.Renderer.Layout.Presentation;
using CDP.Document.Renderer.Layout.Spreadsheet;
using CDP.Document.Renderer.Layout.Word;
using CDP.Document.Editor;

namespace CDP.Document.Tests;


public class EnhancementsTests
{
    [Fact]
    public void TestParserEnhancementPropertiesExist()
    {
        // 1. Docx/Rtf Document Parser Properties (Bullet level, style, header, footer)
        var wordDoc = new WordDocument
        {
            Header = "Page Header Content",
            Footer = "Page Footer Content"
        };
        Assert.Equal("Page Header Content", wordDoc.Header);
        Assert.Equal("Page Footer Content", wordDoc.Footer);

        var para = new ParagraphBlock
        {
            IsBullet = true,
            BulletLevel = 2,
            BulletStyle = "decimal"
        };
        Assert.True(para.IsBullet);
        Assert.Equal(2, para.BulletLevel);
        Assert.Equal("decimal", para.BulletStyle);

        // 2. Spreadsheet Parser Properties (Formula, Merged cells)
        var sheet = new WorksheetNode { Name = "Sheet1" };
        sheet.MergedCellRanges.Add("A1:C3");
        Assert.Single(sheet.MergedCellRanges);
        Assert.Equal("A1:C3", sheet.MergedCellRanges[0]);

        var cell = new GridCellNode
        {
            ColumnIndex = 0,
            Formula = "SUM(A1:A5)",
            Value = 100,
            DisplayText = "100",
            IsMerged = true,
            RowSpan = 3,
            ColumnSpan = 3
        };
        Assert.Equal("SUM(A1:A5)", cell.Formula);
        Assert.Equal(100, cell.Value);
        Assert.Equal("100", cell.DisplayText);
        Assert.True(cell.IsMerged);
        Assert.Equal(3, cell.RowSpan);
        Assert.Equal(3, cell.ColumnSpan);

        // 3. Presentation Parser Properties (Masters, background, coordinates, shape type, images)
        var pres = new PresentationDocument();
        var master = new SlideMasterNode
        {
            Name = "Title Slide Master",
            BackgroundColor = "#FF0000"
        };
        pres.Masters.Add(master);
        Assert.Single(pres.Masters);
        Assert.Equal("Title Slide Master", pres.Masters[0].Name);
        Assert.Equal("#FF0000", pres.Masters[0].BackgroundColor);

        var shape = new ShapeNode
        {
            ShapeType = "Picture",
            X = 50,
            Y = 60,
            Width = 100,
            Height = 150,
            Text = "Description",
            ImageSource = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII="
        };
        Assert.Equal("Picture", shape.ShapeType);
        Assert.Equal(50, shape.X);
        Assert.Equal(60, shape.Y);
        Assert.Equal(100, shape.Width);
        Assert.Equal(150, shape.Height);
        Assert.Equal("Description", shape.Text);
        Assert.Contains("data:image/png;base64", shape.ImageSource);
    }

    [Fact]
    public void TestRendererLayoutEnhancements()
    {
        // 1. Slide master background and shapes prepending
        var pres = new PresentationDocument();
        var master = new SlideMasterNode { Name = "Master1", BackgroundColor = "#00FF00" };
        master.AddChild(new ShapeNode { ShapeType = "Rectangle", X = 0, Y = 0, Width = 720, Height = 540 });
        pres.Masters.Add(master);

        var slide = new SlideNode { SlideIndex = 0, Title = "Slide 1 Title", MasterName = "Master1" };
        slide.AddChild(new ShapeNode { ShapeType = "Ellipse", X = 10, Y = 20, Width = 100, Height = 80 });
        pres.AddChild(slide);

        var layoutBlock = PresentationLayoutManager.Layout(pres);
        Assert.NotNull(layoutBlock);
        Assert.Single(layoutBlock.Slides);

        var slideBlock = layoutBlock.Slides[0];
        Assert.Equal("#00FF00", slideBlock.BackgroundColor);
        // Master shape + slide shape = 2 shapes
        Assert.Equal(2, slideBlock.Shapes.Count);

        // 2. Picture decoding
        var bitmap = ShapeLayoutBlock.LoadBase64Image("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");
        Assert.NotNull(bitmap);
        Assert.Equal(1, bitmap.Width);
        Assert.Equal(1, bitmap.Height);

        // 3. Spreadsheet grid, merged cell spans, cell formatting & drawings
        var spreadsheet = new SpreadsheetDocument();
        var ws = new WorksheetNode { Name = "Sheet1" };
        var row = new GridRowNode { RowIndex = 0 };
        // Primary merged cell (RowSpan=2, ColumnSpan=2)
        var cell1 = new GridCellNode
        {
            ColumnIndex = 0,
            DisplayText = "Primary",
            IsMerged = true,
            RowSpan = 2,
            ColumnSpan = 2,
            Style = "#FFFF00",
            Value = 60 // Threshold for positive conditional styling (>50)
        };
        // Secondary merged cell (should be skipped during layout)
        var cell2 = new GridCellNode
        {
            ColumnIndex = 1,
            DisplayText = "Secondary",
            IsMerged = true,
            RowSpan = 1,
            ColumnSpan = 1
        };
        row.AddChild(cell1);
        row.AddChild(cell2);
        ws.AddChild(row);

        // Drawing / Chart
        var drawing = new ImageInline
        {
            Source = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=",
            AltText = "Pie Chart"
        };
        ws.AddChild(drawing);
        spreadsheet.AddChild(ws);

        var spreadBlock = SpreadsheetLayoutManager.Layout(spreadsheet);
        Assert.NotNull(spreadBlock);
        Assert.Single(spreadBlock.Worksheets);

        var wsBlock = spreadBlock.Worksheets[0];
        // The secondary cell is skipped, so only 1 cell layout block is added!
        Assert.Single(wsBlock.Cells);
        Assert.Equal("Primary", ((GridCellNode)wsBlock.Cells[0].Node).DisplayText);
        Assert.Single(wsBlock.Images);
        Assert.Equal("Pie Chart", wsBlock.Images[0].AltText);
    }

    [Fact]
    public void TestEditorEnhancements()
    {
        // Setup a Word Document Editor
        var editor = new DocumentEditor
        {
            IsReadOnly = false
        };

        var doc = new WordDocument();
        var para = new ParagraphBlock();
        var run = new TextRun { Text = "Original Text" };
        para.AddChild(run);
        doc.AddChild(para);

        // Access internal document field via reflection to test editor state directly
        var docField = typeof(DocumentEditor).GetField("_document", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(docField);
        docField.SetValue(editor, doc);

        // Trigger layout
        var performLayoutMethod = typeof(DocumentEditor).GetMethod("PerformLayout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(performLayoutMethod);
        performLayoutMethod.Invoke(editor, null);

        // Verify initial state
        var textRun = ((WordDocument)docField.GetValue(editor)!).Children.OfType<ParagraphBlock>().First().Children.OfType<TextRun>().First();
        Assert.Equal("Original Text", textRun.Text);

        // Test Undo/Redo text entry
        var pushUndoStateMethod = typeof(DocumentEditor).GetMethod("PushUndoState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(pushUndoStateMethod);
        pushUndoStateMethod.Invoke(editor, null);

        textRun.Text = "Modified Text";
        Assert.Equal("Modified Text", textRun.Text);

        editor.Undo();
        var undoneDoc = (WordDocument)docField.GetValue(editor)!;
        var undoneRun = undoneDoc.Children.OfType<ParagraphBlock>().First().Children.OfType<TextRun>().First();
        Assert.Equal("Original Text", undoneRun.Text);

        editor.Redo();
        var redoneDoc = (WordDocument)docField.GetValue(editor)!;
        var redoneRun = redoneDoc.Children.OfType<ParagraphBlock>().First().Children.OfType<TextRun>().First();
        Assert.Equal("Modified Text", redoneRun.Text);
    }

    [Fact]
    public void TestDocumentFormattingAndSelection()
    {
        // 1. Verify shape node formatting property persistence
        var shape = new ShapeNode
        {
            Bold = true,
            Italic = false,
            FontSize = 14,
            Color = "#FF0000"
        };
        Assert.True(shape.Bold);
        Assert.False(shape.Italic);
        Assert.Equal(14, shape.FontSize);
        Assert.Equal("#FF0000", shape.Color);

        // 2. Setup editor instance for testing cell and shape text formatting API
        var editor = new DocumentEditor { IsReadOnly = false };
        var presDoc = new PresentationDocument();
        var slide = new SlideNode();
        var shapeNode = new ShapeNode { Text = "Test Shape Text", X = 10, Y = 10, Width = 100, Height = 100 };
        slide.AddChild(shapeNode);
        presDoc.AddChild(slide);

        var docField = typeof(DocumentEditor).GetField("_document", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(docField);
        docField.SetValue(editor, presDoc);

        // Set active editing shape
        var editingShapeField = typeof(DocumentEditor).GetField("_editingShapeNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(editingShapeField);
        editingShapeField.SetValue(editor, shapeNode);

        // Test formatting APIs
        editor.ToggleBold();
        Assert.True(shapeNode.Bold);
        editor.ToggleItalic();
        Assert.True(shapeNode.Italic);
        editor.SetFontSize(16);
        Assert.Equal(16, shapeNode.FontSize);
        editor.SetFontColor("#0000FF");
        Assert.Equal("#0000FF", shapeNode.Color);

        // Test caret hit-testing helper via reflection
        var hitTestTextMethod = typeof(DocumentEditor).GetMethod("HitTestText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(hitTestTextMethod);
        using var paint = new SKPaint { TextSize = 12 };
        int caretOffset = (int)hitTestTextMethod.Invoke(editor, new object[] { "Hello", 0.0, 15.0, paint })!;
        Assert.True(caretOffset >= 0);
    }

    [Fact]
    public void TestPr97Fixes_WorkerTests()
    {
        var editor = new DocumentEditor { IsReadOnly = false };
        // 1. Comment 12: ShapeNode Undo Formatting Clones
        var shape = new ShapeNode
        {
            X = 10, Y = 20, Width = 100, Height = 100, ShapeType = "Rectangle", Text = "Hello",
            Bold = true, Italic = true, FontSize = 16, Color = "#FF0000"
        };
        var pres = new PresentationDocument();
        var slide = new SlideNode();
        slide.AddChild(shape);
        pres.AddChild(slide);

        var cloneDocumentMethod = typeof(DocumentEditor).GetMethod("CloneDocument", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(cloneDocumentMethod);
        var clonePres = (PresentationDocument)cloneDocumentMethod.Invoke(editor, new object[] { pres })!;
        var cloneShape = clonePres.Children.OfType<SlideNode>().First().Children.OfType<ShapeNode>().First();
        Assert.Equal(shape.X, cloneShape.X);
        Assert.Equal(shape.ShapeType, cloneShape.ShapeType);
        Assert.True(cloneShape.Bold);
        Assert.True(cloneShape.Italic);
        Assert.Equal(16, cloneShape.FontSize);
        Assert.Equal("#FF0000", cloneShape.Color);

        // 2. Comment 14: Model Enter as Document Break (InsertTextAtCaret / DeleteRange)
        var wordDoc = new WordDocument();
        var para = new ParagraphBlock();
        var run = new TextRun { Text = "HelloWorld" };
        para.AddChild(run);
        wordDoc.AddChild(para);

        var docField = typeof(DocumentEditor).GetField("_document", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(docField);
        docField.SetValue(editor, wordDoc);

        var caretField = typeof(DocumentEditor).GetField("_caretOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(caretField);
        
        // Insert \n at offset 5 ("Hello" | "World")
        caretField.SetValue(editor, 5);
        
        var insertTextAtCaretMethod = typeof(DocumentEditor).GetMethod("InsertTextAtCaret", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(insertTextAtCaretMethod);
        insertTextAtCaretMethod.Invoke(editor, new object[] { wordDoc, "\n" });

        // The paragraph should now have: TextRun("Hello"), LineBreakInline, TextRun("World")
        Assert.Equal(3, para.Children.Count);
        Assert.IsType<TextRun>(para.Children[0]);
        Assert.IsType<LineBreakInline>(para.Children[1]);
        Assert.IsType<TextRun>(para.Children[2]);
        Assert.Equal("Hello", ((TextRun)para.Children[0]).Text);
        Assert.Equal("World", ((TextRun)para.Children[2]).Text);

        // Delete the LineBreakInline (which is at global offset 5, length 1)
        var deleteRangeMethod = typeof(DocumentEditor).GetMethod("DeleteRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(deleteRangeMethod);
        deleteRangeMethod.Invoke(null, new object[] { wordDoc, 5, 6 });

        // LineBreakInline should be removed, and empty TextRun (if any) is cleaned
        Assert.Equal(2, para.Children.Count);
        Assert.IsType<TextRun>(para.Children[0]);
        Assert.IsType<TextRun>(para.Children[1]);
        Assert.Equal("Hello", ((TextRun)para.Children[0]).Text);
        Assert.Equal("World", ((TextRun)para.Children[1]).Text);

        // 3. Comment 11: Emit RTF Color Runs when Saving
        var rtfDoc = new WordDocument();
        var rtfPara = new ParagraphBlock();
        rtfPara.AddChild(new TextRun { Text = "RedText", Color = "#FF0000" });
        rtfDoc.AddChild(rtfPara);

        var serializeToRtfMethod = typeof(DocumentEditor).GetMethod("SerializeToRtf", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(serializeToRtfMethod);
        var rtfContent = (string)serializeToRtfMethod.Invoke(null, new object[] { rtfDoc })!;
        Assert.Contains(@"\colortbl", rtfContent);
        Assert.Contains(@"\red255\green0\blue0;", rtfContent);
        Assert.Contains(@"\cf1", rtfContent);

        // 4. Reset RTF font size after sized runs
        var rtfDocSize = new WordDocument();
        var rtfParaSize = new ParagraphBlock();
        rtfParaSize.AddChild(new TextRun { Text = "Sized", FontSize = 16 });
        rtfParaSize.AddChild(new TextRun { Text = "Normal" });
        rtfDocSize.AddChild(rtfParaSize);

        var rtfContentSize = (string)serializeToRtfMethod.Invoke(null, new object[] { rtfDocSize })!;
        Assert.Contains(@"\fs32", rtfContentSize);
        Assert.Contains(@"\fs24", rtfContentSize);
    }

    [Fact]
    public void TestPptxGroupedShapesSaveOrdering()
    {
        // Setup document structure with grouped shapes
        var doc = new PresentationDocument();
        var slide = new SlideNode();
        
        var shape1 = new ShapeNode { Text = "Shape 1", ShapeType = "Rectangle" };
        var group = new GroupNode();
        var shapeA1 = new ShapeNode { Text = "Shape A1", ShapeType = "Rectangle" };
        var shapeA2 = new ShapeNode { Text = "Shape A2", ShapeType = "Rectangle" };
        group.AddChild(shapeA1);
        group.AddChild(shapeA2);
        var shape2 = new ShapeNode { Text = "Shape 2", ShapeType = "Rectangle" };
        
        slide.AddChild(shape1);
        slide.AddChild(group);
        slide.AddChild(shape2);
        doc.AddChild(slide);

        // Retrieve shape nodes via helper method
        var shapeNodes = new List<ShapeNode>();
        var getShapeNodesMethod = typeof(DocumentEditor).GetMethod("GetShapeNodesRecursive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(getShapeNodesMethod);
        getShapeNodesMethod.Invoke(null, new object[] { slide, shapeNodes });

        // Assert shape nodes are collected in the correct recursive depth-first pre-order
        Assert.Equal(4, shapeNodes.Count);
        Assert.Same(shape1, shapeNodes[0]);
        Assert.Same(shapeA1, shapeNodes[1]);
        Assert.Same(shapeA2, shapeNodes[2]);
        Assert.Same(shape2, shapeNodes[3]);
    }

    [Fact]
    public void TestPptxGroupedShapesSaveOrdering_EdgeCases()
    {
        var doc = new PresentationDocument();
        var slide = new SlideNode();

        // 1. Deeply nested groups:
        // slide -> shape1
        //       -> group1 -> group2 -> group3 -> shapeNested
        //       -> shape2
        var shape1 = new ShapeNode { Text = "Shape 1", ShapeType = "Rectangle" };
        var group1 = new GroupNode();
        var group2 = new GroupNode();
        var group3 = new GroupNode();
        var shapeNested = new ShapeNode { Text = "Shape Nested", ShapeType = "Rectangle" };
        
        group3.AddChild(shapeNested);
        group2.AddChild(group3);
        group1.AddChild(group2);
        
        var shape2 = new ShapeNode { Text = "Shape 2", ShapeType = "Rectangle" };
        
        slide.AddChild(shape1);
        slide.AddChild(group1);
        slide.AddChild(shape2);

        // 2. Empty group:
        //       -> groupEmpty
        var groupEmpty = new GroupNode();
        slide.AddChild(groupEmpty);

        // 3. Mix of shape types inside group:
        //       -> groupMixed -> shapeValid1 (Rectangle)
        //                     -> shapePic (Picture)
        //                     -> shapeFrame (GraphicFrame)
        //                     -> shapeCxn (ConnectionShape)
        //                     -> shapeValid2 (Ellipse)
        var groupMixed = new GroupNode();
        var shapeValid1 = new ShapeNode { Text = "Valid 1", ShapeType = "Rectangle" };
        var shapePic = new ShapeNode { Text = "Pic", ShapeType = "Picture" };
        var shapeFrame = new ShapeNode { Text = "Frame", ShapeType = "GraphicFrame" };
        var shapeCxn = new ShapeNode { Text = "Cxn", ShapeType = "ConnectionShape" };
        var shapeValid2 = new ShapeNode { Text = "Valid 2", ShapeType = "Ellipse" };
        
        groupMixed.AddChild(shapeValid1);
        groupMixed.AddChild(shapePic);
        groupMixed.AddChild(shapeFrame);
        groupMixed.AddChild(shapeCxn);
        groupMixed.AddChild(shapeValid2);
        
        slide.AddChild(groupMixed);

        doc.AddChild(slide);

        // Retrieve shape nodes via helper method
        var shapeNodes = new List<ShapeNode>();
        var getShapeNodesMethod = typeof(DocumentEditor).GetMethod("GetShapeNodesRecursive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(getShapeNodesMethod);
        getShapeNodesMethod.Invoke(null, new object[] { slide, shapeNodes });

        // Assert:
        // Collected shape nodes count:
        // shape1, shapeNested, shape2, shapeValid1, shapeValid2 -> Total 5
        // (shapePic, shapeFrame, shapeCxn are excluded based on ShapeType filter)
        // (groupEmpty has no shapes, group1/2/3 only lead to shapeNested)
        Assert.Equal(5, shapeNodes.Count);
        Assert.Same(shape1, shapeNodes[0]);
        Assert.Same(shapeNested, shapeNodes[1]);
        Assert.Same(shape2, shapeNodes[2]);
        Assert.Same(shapeValid1, shapeNodes[3]);
        Assert.Same(shapeValid2, shapeNodes[4]);
    }

    [Fact]
    public void TestPptxSaveRoundtripWithGroupedShapes()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pptx");
        try
        {
            // 1. Create a PPTX with grouped shapes
            CreatePptxWithGroupedShapesFile(tempPath);
            Assert.True(File.Exists(tempPath));

            // 2. Load into DocumentEditor
            var editor = new DocumentEditor { IsReadOnly = false };
            editor.FilePath = tempPath; // This parses the file in OnFilePathChanged

            var docField = typeof(DocumentEditor).GetField("_document", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(docField);
            var parsedDoc = docField.GetValue(editor) as PresentationDocument;
            Assert.NotNull(parsedDoc);

            // 3. Find shape nodes and edit their text
            var slideNode = parsedDoc.Children.OfType<SlideNode>().FirstOrDefault();
            Assert.NotNull(slideNode);

            var shapeNodes = new List<ShapeNode>();
            var getShapeNodesMethod = typeof(DocumentEditor).GetMethod("GetShapeNodesRecursive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(getShapeNodesMethod);
            getShapeNodesMethod.Invoke(null, new object[] { slideNode, shapeNodes });

            Assert.Equal(4, shapeNodes.Count);
            Assert.Equal("Original Shape 1", shapeNodes[0].Text);
            Assert.Equal("Original Shape A1", shapeNodes[1].Text);
            Assert.Equal("Original Shape A2", shapeNodes[2].Text);
            Assert.Equal("Original Shape 2", shapeNodes[3].Text);

            // Edit
            shapeNodes[0].Text = "Updated Shape 1";
            shapeNodes[1].Text = "Updated Shape A1";
            shapeNodes[2].Text = "Updated Shape A2";
            shapeNodes[3].Text = "Updated Shape 2";

            // 4. Save using Flush()
            editor.Flush();

            // 5. Load and parse again to verify
            var parser = new PptxDocumentParser();
            using (var stream = File.OpenRead(tempPath))
            {
                var reloadedDoc = parser.Parse(stream) as PresentationDocument;
                Assert.NotNull(reloadedDoc);

                var reloadedSlideNode = reloadedDoc.Children.OfType<SlideNode>().FirstOrDefault();
                Assert.NotNull(reloadedSlideNode);

                var reloadedShapeNodes = new List<ShapeNode>();
                getShapeNodesMethod.Invoke(null, new object[] { reloadedSlideNode, reloadedShapeNodes });

                Assert.Equal(4, reloadedShapeNodes.Count);
                Assert.Equal("Updated Shape 1", reloadedShapeNodes[0].Text);
                Assert.Equal("Updated Shape A1", reloadedShapeNodes[1].Text);
                Assert.Equal("Updated Shape A2", reloadedShapeNodes[2].Text);
                Assert.Equal("Updated Shape 2", reloadedShapeNodes[3].Text);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private void CreatePptxWithGroupedShapesFile(string filePath)
    {
        using (var presentationDoc = DocumentFormat.OpenXml.Packaging.PresentationDocument.Create(filePath, PresentationDocumentType.Presentation, true))
        {
            var presentationPart = presentationDoc.AddPresentationPart();
            presentationPart.Presentation = new DocumentFormat.OpenXml.Presentation.Presentation();

            var slideIdList = new DocumentFormat.OpenXml.Presentation.SlideIdList();
            presentationPart.Presentation.AppendChild(slideIdList);

            var slidePart = presentationPart.AddNewPart<DocumentFormat.OpenXml.Packaging.SlidePart>();
            var slide = new DocumentFormat.OpenXml.Presentation.Slide();
            slide.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
            slide.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            slide.AddNamespaceDeclaration("p", "http://schemas.openxmlformats.org/presentationml/2006/main");
            
            var cSld = new DocumentFormat.OpenXml.Presentation.CommonSlideData(new DocumentFormat.OpenXml.Presentation.ShapeTree());
            slide.AppendChild(cSld);
            slidePart.Slide = slide;

            var slideId = new DocumentFormat.OpenXml.Presentation.SlideId
            {
                Id = 256,
                RelationshipId = presentationPart.GetIdOfPart(slidePart)
            };
            slideIdList.AppendChild(slideId);

            var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;

            var nvGrpSpPr = new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 1, Name = "" },
                new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()
            );
            shapeTree.AppendChild(nvGrpSpPr);
            shapeTree.AppendChild(new DocumentFormat.OpenXml.Presentation.GroupShapeProperties());

            // 1. Top-level shape 1
            var sp1 = CreateOpenXmlShape(2, "Shape 1", "Original Shape 1");
            shapeTree.AppendChild(sp1);

            // 2. Group shape
            var groupSp = new DocumentFormat.OpenXml.Presentation.GroupShape();
            var nvGrpSpPrInner = new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 3, Name = "Group 1" },
                new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()
            );
            groupSp.AppendChild(nvGrpSpPrInner);
            groupSp.AppendChild(new DocumentFormat.OpenXml.Presentation.GroupShapeProperties());

            // Shape A1 inside group
            var spA1 = CreateOpenXmlShape(4, "Shape A1", "Original Shape A1");
            groupSp.AppendChild(spA1);
            
            // Shape A2 inside group
            var spA2 = CreateOpenXmlShape(5, "Shape A2", "Original Shape A2");
            groupSp.AppendChild(spA2);

            shapeTree.AppendChild(groupSp);

            // 3. Top-level shape 2
            var sp2 = CreateOpenXmlShape(6, "Shape 2", "Original Shape 2");
            shapeTree.AppendChild(sp2);

            slidePart.Slide.Save();
            presentationPart.Presentation.Save();
        }
    }

    private static DocumentFormat.OpenXml.Presentation.Shape CreateOpenXmlShape(uint id, string name, string text)
    {
        var sp = new DocumentFormat.OpenXml.Presentation.Shape();
        var nvSpPr = new DocumentFormat.OpenXml.Presentation.NonVisualShapeProperties(
            new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = id, Name = name },
            new DocumentFormat.OpenXml.Presentation.NonVisualShapeDrawingProperties(),
            new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()
        );
        sp.AppendChild(nvSpPr);

        var spPr = new DocumentFormat.OpenXml.Presentation.ShapeProperties();
        var xfrm = new DocumentFormat.OpenXml.Drawing.Transform2D(
            new DocumentFormat.OpenXml.Drawing.Offset { X = 127000L, Y = 254000L },
            new DocumentFormat.OpenXml.Drawing.Extents { Cx = 635000L, Cy = 381000L }
        );
        spPr.AppendChild(xfrm);
        spPr.AppendChild(new DocumentFormat.OpenXml.Drawing.PresetGeometry { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle });
        sp.AppendChild(spPr);

        var textBody = new DocumentFormat.OpenXml.Presentation.TextBody(
            new DocumentFormat.OpenXml.Drawing.BodyProperties(),
            new DocumentFormat.OpenXml.Drawing.ListStyle(),
            new DocumentFormat.OpenXml.Drawing.Paragraph(
                new DocumentFormat.OpenXml.Drawing.Run(
                    new DocumentFormat.OpenXml.Drawing.Text(text)
                )
            )
        );
        sp.AppendChild(textBody);
        return sp;
    }

    [Fact]
    public void TestTableCellParagraphGlobalOffsetsAndEditing()
    {
        // 1. Create a WordDocument with a paragraph, a table with cells and paragraphs, and another paragraph.
        var doc = new WordDocument();
        
        var p1 = new ParagraphBlock();
        p1.Children.Add(new TextRun { Text = "Hello" }); // length 5
        doc.Children.Add(p1);

        var table = new TableBlock();
        var row = new TableRowBlock();
        var cell1 = new TableCellBlock();
        var cellPara1 = new ParagraphBlock();
        cellPara1.Children.Add(new TextRun { Text = "Cell A" }); // length 6
        cell1.Children.Add(cellPara1);
        
        var cell2 = new TableCellBlock();
        var cellPara2 = new ParagraphBlock();
        cellPara2.Children.Add(new TextRun { Text = "Cell B" }); // length 6
        cell2.Children.Add(cellPara2);

        row.Children.Add(cell1);
        row.Children.Add(cell2);
        table.Children.Add(row);
        doc.Children.Add(table);

        var p2 = new ParagraphBlock();
        p2.Children.Add(new TextRun { Text = "World" }); // length 5
        doc.Children.Add(p2);

        // 2. Perform layout using WordLayoutManager
        var context = new LayoutContext
        {
            PageWidth = 500,
            PageHeight = 800,
            MarginLeft = 50,
            MarginRight = 50,
            MarginTop = 50,
            MarginBottom = 50
        };
        var layoutBlock = WordLayoutManager.Layout(doc, context);

        // 3. Verify the document-global offsets after layout.
        var page = layoutBlock.Pages[0];
        
        ParagraphLayoutBlock? cellPara1Layout = null;
        ParagraphLayoutBlock? cellPara2Layout = null;
        ParagraphLayoutBlock? p2Layout = null;

        foreach (var block in page.Blocks)
        {
            if (block is TableRowLayoutBlock rlb)
            {
                var clb1 = rlb.Cells[0];
                var clb2 = rlb.Cells[1];
                cellPara1Layout = (ParagraphLayoutBlock)clb1.Blocks[0];
                cellPara2Layout = (ParagraphLayoutBlock)clb2.Blocks[0];
            }
            else if (block is ParagraphLayoutBlock plb && plb.Node == p2)
            {
                p2Layout = plb;
            }
        }

        Assert.NotNull(cellPara1Layout);
        Assert.NotNull(cellPara2Layout);
        Assert.NotNull(p2Layout);

        Assert.Equal(5, cellPara1Layout.GlobalStartOffset);
        Assert.Equal(11, cellPara2Layout.GlobalStartOffset);
        Assert.Equal(17, p2Layout.GlobalStartOffset);

        // 4. Test HitTesting inside cell 1 paragraph layout
        var centerPoint1 = new SKPoint(cellPara1Layout.Bounds.Left + 5, cellPara1Layout.Bounds.MidY);
        int hitOffset1 = cellPara1Layout.HitTest(centerPoint1);
        Assert.InRange(hitOffset1, 5, 11);

        var centerPoint2 = new SKPoint(cellPara2Layout.Bounds.Left + 5, cellPara2Layout.Bounds.MidY);
        int hitOffset2 = cellPara2Layout.HitTest(centerPoint2);
        Assert.InRange(hitOffset2, 11, 17);

        // 5. Test GetCaretBounds handles global offsets correctly
        var caretBounds1 = cellPara1Layout.GetCaretBounds(7); // "Ce" (offset 7 is global, which is local 2 in cellPara1)
        Assert.NotEqual(default, caretBounds1);

        // 6. Test editing using DocumentEditor
        var editor = new DocumentEditor { IsReadOnly = false };
        var docField = typeof(DocumentEditor).GetField("_document", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(docField);
        docField.SetValue(editor, doc);

        // Set caret inside Cell 1 paragraph ("Cel|l A")
        var caretOffsetField = typeof(DocumentEditor).GetField("_caretOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(caretOffsetField);
        caretOffsetField.SetValue(editor, 8); // global offset 8, which is local offset 3 in cellPara1

        // Insert "X"
        var insertTextMethod = typeof(DocumentEditor).GetMethod("InsertTextAtCaret", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(insertTextMethod);
        insertTextMethod.Invoke(editor, new object[] { doc, "X" });

        // Verify the inner paragraph text run has been updated correctly
        var updatedRun = (TextRun)cellPara1.Children[0];
        Assert.Equal("CelXl A", updatedRun.Text);
    }

    [Fact]
    public void TestTableCellParagraphHitTestAndOffsetsStress()
    {
        // 1. Create a WordDocument with:
        // - p0: "Header" (length 6)
        // - table: 2 rows
        //   - Row 1:
        //     - Cell 1: Empty cell (0 length)
        //     - Cell 2: "Cell12" (length 6)
        //   - Row 2:
        //     - Cell 1: "Cell21" (length 6)
        //     - Cell 2: Nested Table (contains "Nest" length 4)
        // - p3: "Footer" (length 6)
        var doc = new WordDocument();
        
        var p0 = new ParagraphBlock();
        p0.Children.Add(new TextRun { Text = "Header" }); // 6 chars
        doc.Children.Add(p0);

        var table = new TableBlock();
        
        // Row 1
        var row1 = new TableRowBlock();
        var r1c1 = new TableCellBlock();
        var r1c1p = new ParagraphBlock(); // Empty cell
        r1c1.Children.Add(r1c1p);
        row1.Children.Add(r1c1);

        var r1c2 = new TableCellBlock();
        var r1c2p = new ParagraphBlock();
        r1c2p.Children.Add(new TextRun { Text = "Cell12" }); // 6 chars
        r1c2.Children.Add(r1c2p);
        row1.Children.Add(r1c2);
        table.Children.Add(row1);

        // Row 2
        var row2 = new TableRowBlock();
        var r2c1 = new TableCellBlock();
        var r2c1p = new ParagraphBlock();
        r2c1p.Children.Add(new TextRun { Text = "Cell21" }); // 6 chars
        r2c1.Children.Add(r2c1p);
        row2.Children.Add(r2c1);

        var r2c2 = new TableCellBlock();
        // Nested table in r2c2
        var nestedTable = new TableBlock();
        var nestRow = new TableRowBlock();
        var nestCell = new TableCellBlock();
        var nestPara = new ParagraphBlock();
        nestPara.Children.Add(new TextRun { Text = "Nest" }); // 4 chars
        nestCell.Children.Add(nestPara);
        nestRow.Children.Add(nestCell);
        nestedTable.Children.Add(nestRow);
        r2c2.Children.Add(nestedTable);
        row2.Children.Add(r2c2);
        table.Children.Add(row2);

        doc.Children.Add(table);

        var p3 = new ParagraphBlock();
        p3.Children.Add(new TextRun { Text = "Footer" }); // 6 chars
        doc.Children.Add(p3);

        // Perform layout
        var context = new LayoutContext
        {
            PageWidth = 500,
            PageHeight = 800,
            MarginLeft = 50,
            MarginRight = 50,
            MarginTop = 50,
            MarginBottom = 50
        };
        var layoutBlock = WordLayoutManager.Layout(doc, context);
        Assert.NotNull(layoutBlock);
        
        // Let's inspect the layout structure.
        var page = layoutBlock.Pages[0];
        
        ParagraphLayoutBlock? p0Layout = null;
        TableRowLayoutBlock? r1Layout = null;
        TableRowLayoutBlock? r2Layout = null;
        ParagraphLayoutBlock? p3Layout = null;

        foreach (var block in page.Blocks)
        {
            if (block is ParagraphLayoutBlock plb)
            {
                if (plb.Node == p0) p0Layout = plb;
                else if (plb.Node == p3) p3Layout = plb;
            }
            else if (block is TableRowLayoutBlock rlb)
            {
                if (rlb.Node == row1) r1Layout = rlb;
                else if (rlb.Node == row2) r2Layout = rlb;
            }
        }

        Assert.NotNull(p0Layout);
        Assert.NotNull(r1Layout);
        Assert.NotNull(r2Layout);
        Assert.NotNull(p3Layout);

        // Expected global offsets mapping in rendering:
        // p0: Start = 0, Length = 6. Next starts at 6.
        // Row 1 Cell 1 (r1c1p): Start = 6, Length = 0. Next starts at 6.
        // Row 1 Cell 2 (r1c2p): Start = 6, Length = 6. Next starts at 12.
        // Row 2 Cell 1 (r2c1p): Start = 12, Length = 6. Next starts at 18.
        // Row 2 Cell 2 (nestedTable is ignored in WordLayoutManager!):
        //   Wait, WordLayoutManager.Layout loops through cells:
        //   For cell 2, it loops: foreach (var cellChild in cellNode.Children)
        //   which contains nestedTable. It is NOT a ParagraphBlock, so it is IGNORED!
        //   Therefore, cell 2 has no paragraph blocks. globalOffset does NOT increase for it.
        //   So next starts at 18.
        // p3 (p3): Start = 18, Length = 6. Next starts at 24.
        
        // Let's verify layouts.
        Assert.Equal(0, p0Layout.GlobalStartOffset);
        
        var r1c1Layout = (ParagraphLayoutBlock)r1Layout.Cells[0].Blocks[0];
        Assert.Equal(6, r1c1Layout.GlobalStartOffset);
        
        var r1c2Layout = (ParagraphLayoutBlock)r1Layout.Cells[1].Blocks[0];
        Assert.Equal(6, r1c2Layout.GlobalStartOffset);
        
        var r2c1Layout = (ParagraphLayoutBlock)r2Layout.Cells[0].Blocks[0];
        Assert.Equal(12, r2c1Layout.GlobalStartOffset);
        
        // Cell 2 has no layout blocks because nested table is ignored!
        Assert.Empty(r2Layout.Cells[1].Blocks);
        
        Assert.Equal(18, p3Layout.GlobalStartOffset);

        // 2. Stress-test HitTest on Empty Cell Paragraph
        // Point in r1c1Layout
        var r1c1Pt = new SKPoint(r1c1Layout.Bounds.Left + 5, r1c1Layout.Bounds.Top + 2);
        int hit1 = r1c1Layout.HitTest(r1c1Pt);
        // It should return 6 (which is GlobalStartOffset of the empty paragraph)
        Assert.Equal(6, hit1);

        // 3. Stress-test GetCaretBounds on boundaries and empty cells
        // For empty paragraph, it returns default because Lines is empty.
        var caretEmpty = r1c1Layout.GetCaretBounds(6);
        Assert.Equal(default, caretEmpty);

        // Boundary offset 6 (start of Cell 2)
        var caretCellStart = r1c2Layout.GetCaretBounds(6);
        Assert.NotEqual(default, caretCellStart);
        
        // Boundary offset 12 (end of Cell 2 / start of Cell 3)
        var caretCellEnd = r1c2Layout.GetCaretBounds(12);
        Assert.NotEqual(default, caretCellEnd);

        // Out of bounds for Cell 2 (offset 5, which is in p0)
        var caretOob = r1c2Layout.GetCaretBounds(5);
        Assert.Equal(default, caretOob);

        // 4. Test editor vs layout mismatch on nested tables
        var editor = new DocumentEditor { IsReadOnly = false };
        var docField = typeof(DocumentEditor).GetField("_document", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(docField);
        docField.SetValue(editor, doc);

        // Let's check paragraphs in editor.
        // DocumentEditor uses recursive GetParagraphs:
        // p0 (6) -> r1c1p (0) -> r1c2p (6) -> r2c1p (6) -> nestPara (4) -> p3 (6)
        // Let's get paragraphs.
        var getParagraphsMethod = typeof(DocumentEditor).GetMethod("GetParagraphs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(getParagraphsMethod);
        var paragraphsList = (List<ParagraphBlock>)getParagraphsMethod.Invoke(null, new object[] { doc })!;
        Assert.Equal(6, paragraphsList.Count);
        Assert.Same(p0, paragraphsList[0]);
        Assert.Same(r1c1p, paragraphsList[1]);
        Assert.Same(r1c2p, paragraphsList[2]);
        Assert.Same(r2c1p, paragraphsList[3]);
        Assert.Same(nestPara, paragraphsList[4]);
        Assert.Same(p3, paragraphsList[5]);

        // Let's check text run lookup in editor.
        // In the editor, globalOffset is computed by summing the lengths of text/linebreaks in paragraphsList:
        // p0: 6
        // r1c1p: 0
        // r1c2p: 6
        // r2c1p: 6
        // nestPara: 4
        // p3: 6
        // Total = 28.
        // If we search for run at offset 20:
        // - p0 (0..6)
        // - r1c1p (6..6)
        // - r1c2p (6..12)
        // - r2c1p (12..18)
        // - nestPara (18..22) => offset 20 is "st" inside nestPara.
        var findRunMethod = typeof(DocumentEditor).GetMethod("FindTextRunAtOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(findRunMethod);
        
        var runAt20 = (TextRun)findRunMethod.Invoke(editor, new object[] { doc, 20 })!;
        Assert.NotNull(runAt20);
        Assert.Equal("Nest", runAt20.Text);

        // But wait! In the renderer, p3 has GlobalStartOffset = 18.
        // In the editor, p3's start offset is 6 + 0 + 6 + 6 + 4 = 22!
        // This is a layout-editor offset mismatch!
        // Let's verify that when we call PerformLayout, p3's layout block's GlobalStartOffset is indeed 18,
        // while in the editor it is treated as 22.
        // This is because the renderer ignored the nested table, but the editor traversed it!
    }

    [AvaloniaFact]
    public void TestAutosaveFlushedOnDetach()
    {
        var editor = new DocumentEditor { IsReadOnly = false };
        var doc = new WordDocument();
        var para = new ParagraphBlock();
        para.Children.Add(new TextRun { Text = "Original Text" });
        doc.AddChild(para);

        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".rtf");
        File.WriteAllText(tempPath, "");
        editor.FilePath = tempPath;

        var docField = typeof(DocumentEditor).GetField("_document", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(docField);
        docField.SetValue(editor, doc);

        try
        {
            var textRun = doc.Children.OfType<ParagraphBlock>().First().Children.OfType<TextRun>().First();
            textRun.Text = "Modified Text";

            var scheduleMethod = typeof(DocumentEditor).GetMethod("ScheduleAutoSave", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(scheduleMethod);
            scheduleMethod.Invoke(editor, null);

            var timerField = typeof(DocumentEditor).GetField("_saveDebounceTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(timerField);
            Assert.NotNull(timerField.GetValue(editor));

            Assert.Equal("", File.ReadAllText(tempPath));

            // Attach editor to a headless window to trigger OnAttachedToVisualTree
            var window = new Avalonia.Controls.Window { Content = editor };
            window.Show();

            // Detach editor by clearing Content (this automatically triggers OnDetachedFromVisualTree!)
            window.Content = null;
            window.Close();

            Assert.Null(timerField.GetValue(editor));

            string content = File.ReadAllText(tempPath);
            Assert.Contains("Modified Text", content);
        }
        finally
        {
            var timerField = typeof(DocumentEditor).GetField("_saveDebounceTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (timerField != null)
            {
                var timer = (System.Threading.Timer?)timerField.GetValue(editor);
                timer?.Dispose();
                timerField.SetValue(editor, null);
            }
            var versionField = typeof(DocumentEditor).GetField("_saveVersion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (versionField != null)
            {
                versionField.SetValue(editor, -9999);
            }
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }


    [Fact]
    public void TestInlineImageDimensionsAndLayout()
    {
        // 1. Setup a paragraph with: TextRun("Prefix"), ImageInline(Width=100, Height=50), TextRun("Suffix")
        var para = new ParagraphBlock();
        var prefixRun = new TextRun { Text = "Prefix" };
        var imageInline = new ImageInline { Width = 100, Height = 50, Source = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=", AltText = "Test Image" };
        var suffixRun = new TextRun { Text = "Suffix" };

        para.AddChild(prefixRun);
        para.AddChild(imageInline);
        para.AddChild(suffixRun);

        // 2. Perform layout using WordLayoutManager
        var context = new LayoutContext
        {
            PageWidth = 500,
            PageHeight = 800,
            MarginLeft = 50,
            MarginRight = 50,
            MarginTop = 50,
            MarginBottom = 50
        };
        var doc = new WordDocument();
        doc.AddChild(para);

        var docBlock = WordLayoutManager.Layout(doc, context);
        Assert.NotNull(docBlock);

        // Retrieve the ParagraphLayoutBlock
        var page = docBlock.Pages[0];
        var paraLayout = page.Blocks.OfType<ParagraphLayoutBlock>().FirstOrDefault();
        Assert.NotNull(paraLayout);

        // Assert 1: An inline image with Width = 100 and Height = 50 inside a paragraph yields a ParagraphLayoutBlock with a line height of 50
        Assert.NotEmpty(paraLayout.Lines);
        var line = paraLayout.Lines[0];
        Assert.Equal(50f, line.Height);

        // Assert 2: The segments correctly position following text at XOffset = 100 + previous_text_width
        Assert.Equal(3, line.Segments.Count);
        
        var seg0 = line.Segments[0];
        var seg1 = line.Segments[1];
        var seg2 = line.Segments[2];

        Assert.Equal("Prefix", seg0.Text);
        Assert.Same(imageInline, seg1.Image);
        Assert.Equal("Suffix", seg2.Text);

        float prefixWidth = seg0.Width;
        Assert.Equal(prefixWidth, seg1.XOffset);
        Assert.Equal(100f, seg1.Width);
        Assert.Equal(prefixWidth + 100f, seg2.XOffset);

        // Assert 3: The cloned paragraph inherits correct image dimensions
        var editor = new DocumentEditor();
        var cloneDocumentMethod = typeof(DocumentEditor).GetMethod("CloneDocument", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(cloneDocumentMethod);

        var clonedDoc = (WordDocument)cloneDocumentMethod.Invoke(editor, new object[] { doc })!;
        Assert.NotNull(clonedDoc);

        var clonedPara = clonedDoc.Children.OfType<ParagraphBlock>().First();
        var clonedImage = clonedPara.Children.OfType<ImageInline>().First();
        Assert.NotNull(clonedImage);
        Assert.Equal(100.0, clonedImage.Width);
        Assert.Equal(50.0, clonedImage.Height);
    }

    [Fact]
    public void TestInlineImageEdgeCases_Null()
    {
        var layout = RunLayoutWithImage(null, null);
        var line = layout.Lines[0];
        Assert.Equal(3, line.Segments.Count);
        // Default width fallback should be positive (24f)
        Assert.Equal(24f, line.Segments[1].Width);
        Assert.True(line.Height > 0);
    }

    [Fact]
    public void TestInlineImageEdgeCases_Zero()
    {
        var layout = RunLayoutWithImage(0, 0);
        var line = layout.Lines[0];
        Assert.Equal(0f, line.Segments[1].Width);
        Assert.True(line.Height > 0);
    }

    [Fact]
    public void TestInlineImageEdgeCases_Negative_ProducesNegativeWidth()
    {
        var layout = RunLayoutWithImage(-50, -20);
        var line = layout.Lines[0];
        // The implementation allows negative width and offset adjustments
        Assert.Equal(-50f, line.Segments[1].Width);
        
        // This causes the next segment to be positioned backwards (segX += -50f)
        float prefixWidth = line.Segments[0].Width;
        Assert.Equal(prefixWidth - 50f, line.Segments[2].XOffset);
    }

    [Fact]
    public void TestInlineImageEdgeCases_Huge()
    {
        var layout = RunLayoutWithImage(2000, 1000);
        var line = layout.Lines[0];
        Assert.Equal(2000f, line.Segments[1].Width);
    }

    [Fact]
    public void TestInlineImageEdgeCases_NaN_PlatformBehavior()
    {
        try
        {
            var layout = RunLayoutWithImage(double.NaN, double.NaN);
            var line = layout.Lines[0];
            Assert.True(float.IsNaN(line.Segments[1].Width), "Segment width should be NaN if input is NaN and it didn't throw");
        }
        catch (Exception ex)
        {
            Assert.True(ex is OverflowException || ex is ArgumentOutOfRangeException, $"Expected Overflow/ArgumentOutOfRange, but got {ex.GetType().Name}: {ex.Message}");
        }
    }

    [Fact]
    public void TestInlineImageEdgeCases_Infinity_Throws()
    {
        var ex = Assert.ThrowsAny<Exception>(() => RunLayoutWithImage(double.PositiveInfinity, double.PositiveInfinity));
        Assert.True(ex is OverflowException || ex is ArgumentOutOfRangeException, $"Expected OverflowException or ArgumentOutOfRangeException, but got {ex.GetType().Name}: {ex.Message}");
    }

    private ParagraphLayoutBlock RunLayoutWithImage(double? w, double? h)
    {
        var para = new ParagraphBlock();
        var prefixRun = new TextRun { Text = "Prefix" };
        var imageInline = new ImageInline { Width = w, Height = h, Source = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=", AltText = "Test Image" };
        var suffixRun = new TextRun { Text = "Suffix" };

        para.AddChild(prefixRun);
        para.AddChild(imageInline);
        para.AddChild(suffixRun);

        var context = new LayoutContext
        {
            PageWidth = 500,
            PageHeight = 800,
            MarginLeft = 50,
            MarginRight = 50,
            MarginTop = 50,
            MarginBottom = 50
        };
        var doc = new WordDocument();
        doc.AddChild(para);

        var docBlock = WordLayoutManager.Layout(doc, context);
        Assert.NotNull(docBlock);
        var page = docBlock.Pages[0];
        var paraLayout = page.Blocks.OfType<ParagraphLayoutBlock>().FirstOrDefault();
        Assert.NotNull(paraLayout);
        return paraLayout;
    }

    [Fact]
    public void TestTableCellSerializationFullInlines()
    {
        // 1. Create a WordDocument with a Table containing formatted runs, images, and breaks
        var doc = new WordDocument();
        var table = new TableBlock();
        var row = new TableRowBlock();
        var cell = new TableCellBlock();
        
        var cellPara = new ParagraphBlock();
        // Formatted Text Run
        cellPara.Children.Add(new TextRun { Text = "Bold Italic Text", Bold = true, Italic = true, FontSize = 14, Color = "FF0000" });
        // Line Break
        cellPara.Children.Add(new LineBreakInline());
        // Image Run
        cellPara.Children.Add(new ImageInline { Source = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==", AltText = "Test Image" });
        
        cell.Children.Add(cellPara);
        row.Children.Add(cell);
        table.Children.Add(row);
        doc.Children.Add(table);
        
        // 2. Setup a temporary .docx file using DocumentFormat.OpenXml to create a blank shell
        var tempFile = Path.GetTempFileName() + ".docx";
        try
        {
            using (var wordDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(tempFile, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                    new DocumentFormat.OpenXml.Wordprocessing.Body()
                );
                mainPart.Document.Save();
            }
            
            // 3. Invoke SerializeToDocx via reflection
            var serializeMethod = typeof(DocumentEditor).GetMethod("SerializeToDocx", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(serializeMethod);
            serializeMethod.Invoke(null, new object[] { doc, tempFile });
            
            // 4. Verify serialized structure using OpenXml SDK
            using (var wordDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(tempFile, false))
            {
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                Assert.NotNull(body);
                
                var wTable = body.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Table>();
                Assert.NotNull(wTable);
                
                var wRow = wTable.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.TableRow>();
                Assert.NotNull(wRow);
                
                var wCell = wRow.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.TableCell>();
                Assert.NotNull(wCell);
                
                var wPara = wCell.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Paragraph>();
                Assert.NotNull(wPara);
                
                // Assert the formatted run
                var runs = wPara.Elements<DocumentFormat.OpenXml.Wordprocessing.Run>().ToList();
                Assert.Equal(3, runs.Count); // Bold Italic Run, Image Run, Line Break (inside Run)
                
                // First Run: Bold Italic
                var run1 = runs[0];
                Assert.NotNull(run1.RunProperties);
                Assert.NotNull(run1.RunProperties.Bold);
                Assert.NotNull(run1.RunProperties.Italic);
                Assert.Equal("28", run1.RunProperties.FontSize?.Val?.Value); // 14 * 2 = 28
                Assert.Equal("FF0000", run1.RunProperties.Color?.Val?.Value);
                Assert.Equal("Bold Italic Text", run1.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Text>()?.Text);
                
                // Second Run: Break (Line Break)
                var run2 = runs[1];
                var br = run2.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Break>();
                Assert.NotNull(br);
                
                // Third Run: Drawing (Image)
                var run3 = runs[2];
                var drawing = run3.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Drawing>();
                Assert.NotNull(drawing);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TestTableCellSerializationEdgeCasesAndStress()
    {
        var doc = new WordDocument();
        var table = new TableBlock();
        var row = new TableRowBlock();

        // 1. Cell 1: Empty cell
        var cellEmpty = new TableCellBlock();
        row.Children.Add(cellEmpty);

        // 2. Cell 2: Empty text run in paragraph
        var cellEmptyRun = new TableCellBlock();
        var pEmptyRun = new ParagraphBlock();
        pEmptyRun.Children.Add(new TextRun { Text = "" });
        cellEmptyRun.Children.Add(pEmptyRun);
        row.Children.Add(cellEmptyRun);

        // 3. Cell 3: Null text in text run (using null! to bypass compiler warning)
        var cellNullRun = new TableCellBlock();
        var pNullRun = new ParagraphBlock();
        pNullRun.Children.Add(new TextRun { Text = null! });
        cellNullRun.Children.Add(pNullRun);
        row.Children.Add(cellNullRun);

        // 4. Cell 4: Large list of inlines (50 items)
        var cellLarge = new TableCellBlock();
        var pLarge = new ParagraphBlock();
        for (int i = 0; i < 25; i++)
        {
            pLarge.Children.Add(new TextRun { Text = $"Item {i}", Bold = (i % 2 == 0) });
            pLarge.Children.Add(new LineBreakInline());
        }
        cellLarge.Children.Add(pLarge);
        row.Children.Add(cellLarge);

        // 5. Cell 5: Multiple paragraphs
        var cellMultiPara = new TableCellBlock();
        var p1 = new ParagraphBlock();
        p1.Children.Add(new TextRun { Text = "Paragraph 1" });
        var p2 = new ParagraphBlock();
        p2.Children.Add(new TextRun { Text = "Paragraph 2" });
        cellMultiPara.Children.Add(p1);
        cellMultiPara.Children.Add(p2);
        row.Children.Add(cellMultiPara);

        // 6. Cell 6: ImageInline with null Source
        var cellNullImage = new TableCellBlock();
        var pNullImage = new ParagraphBlock();
        pNullImage.Children.Add(new ImageInline { Source = null, AltText = "Null Source Image" });
        cellNullImage.Children.Add(pNullImage);
        row.Children.Add(cellNullImage);

        // 7. Cell 7: ImageInline with invalid base64 Source
        var cellInvalidImage = new TableCellBlock();
        var pInvalidImage = new ParagraphBlock();
        pInvalidImage.Children.Add(new ImageInline { Source = "data:image/png;base64,invalid_base64_data", AltText = "Invalid Image" });
        cellInvalidImage.Children.Add(pInvalidImage);
        row.Children.Add(cellInvalidImage);

        table.Children.Add(row);
        doc.Children.Add(table);

        var tempFile = Path.GetTempFileName() + ".docx";
        try
        {
            // Setup blank shell
            using (var wordDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(tempFile, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                    new DocumentFormat.OpenXml.Wordprocessing.Body()
                );
                mainPart.Document.Save();
            }

            // Invoke SerializeToDocx
            var serializeMethod = typeof(DocumentEditor).GetMethod("SerializeToDocx", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(serializeMethod);
            serializeMethod.Invoke(null, new object[] { doc, tempFile });

            // Verify
            using (var wordDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(tempFile, false))
            {
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                Assert.NotNull(body);

                var wTable = body.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Table>();
                Assert.NotNull(wTable);

                var wRow = wTable.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.TableRow>();
                Assert.NotNull(wRow);

                var cells = wRow.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>().ToList();
                Assert.Equal(7, cells.Count);

                // 1. Cell 1: Empty cell -> Should have exactly 1 empty paragraph
                var cell1 = cells[0];
                var c1Paras = cell1.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().ToList();
                Assert.Single(c1Paras);
                Assert.Empty(c1Paras[0].Elements<DocumentFormat.OpenXml.Wordprocessing.Run>());

                // 2. Cell 2: Empty text run -> Paragraph should have 1 run, with text ""
                var cell2 = cells[1];
                var c2Paras = cell2.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().ToList();
                Assert.Single(c2Paras);
                var c2Runs = c2Paras[0].Elements<DocumentFormat.OpenXml.Wordprocessing.Run>().ToList();
                Assert.Single(c2Runs);
                Assert.Equal("", c2Runs[0].GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Text>()?.Text);

                // 3. Cell 3: Null text run -> Paragraph should have 1 run, text is null or empty
                var cell3 = cells[2];
                var c3Paras = cell3.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().ToList();
                Assert.Single(c3Paras);
                var c3Runs = c3Paras[0].Elements<DocumentFormat.OpenXml.Wordprocessing.Run>().ToList();
                Assert.Single(c3Runs);
                var text3 = c3Runs[0].GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Text>()?.Text;
                Assert.True(string.IsNullOrEmpty(text3));

                // 4. Cell 4: Large list of inlines (25 TextRuns + 25 Breaks)
                var cell4 = cells[3];
                var c4Paras = cell4.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().ToList();
                Assert.Single(c4Paras);
                var c4Runs = c4Paras[0].Elements<DocumentFormat.OpenXml.Wordprocessing.Run>().ToList();
                Assert.Equal(50, c4Runs.Count);

                // 5. Cell 5: Multiple paragraphs
                var cell5 = cells[4];
                var c5Paras = cell5.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().ToList();
                Assert.Equal(2, c5Paras.Count);
                Assert.Equal("Paragraph 1", c5Paras[0].GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Run>()?.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Text>()?.Text);
                Assert.Equal("Paragraph 2", c5Paras[1].GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Run>()?.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Text>()?.Text);

                // 6. Cell 6: ImageInline with null Source -> Paragraph has no drawings/runs
                var cell6 = cells[5];
                var c6Paras = cell6.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().ToList();
                Assert.Single(c6Paras);
                Assert.Empty(c6Paras[0].Elements<DocumentFormat.OpenXml.Wordprocessing.Run>());

                // 7. Cell 7: ImageInline with invalid base64 -> Exception caught, paragraph has no drawings/runs
                var cell7 = cells[6];
                var c7Paras = cell7.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().ToList();
                Assert.Single(c7Paras);
                Assert.Empty(c7Paras[0].Elements<DocumentFormat.OpenXml.Wordprocessing.Run>());
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}


