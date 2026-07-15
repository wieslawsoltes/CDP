using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using SkiaSharp;
using Avalonia;
using Avalonia.Input;
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
}
