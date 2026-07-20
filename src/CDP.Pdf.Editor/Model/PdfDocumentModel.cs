using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Rendering.Skia;

namespace CDP.Pdf.Editor.Model;

public abstract class PdfElementModel
{
    public SKRect Bounds { get; set; }
    public SKColor Color { get; set; } = SKColors.Black;
    
    // Original element tracking and masking
    public bool IsOriginal { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public bool IsModified { get; set; } = false;
    public SKRect OriginalBounds { get; set; }
    public bool IsGranular { get; set; } = false;

    public abstract void Render(SKCanvas canvas, float pageHeight);
    public abstract void WriteTo(PdfDocumentBuilder builder, PdfPageBuilder pageBuilder, float pageHeight);
}

public class PdfTextElementModel : PdfElementModel
{
    public string Text { get; set; } = "";
    public float FontSize { get; set; } = 12f;
    public string FontName { get; set; } = "Helvetica";

    public override void Render(SKCanvas canvas, float pageHeight)
    {
        using var paint = new SKPaint
        {
            Color = Color,
            TextSize = FontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(FontName)
        };
        canvas.DrawText(Text, Bounds.Left, Bounds.Bottom - paint.FontMetrics.Descent, paint);
    }

    public override void WriteTo(PdfDocumentBuilder builder, PdfPageBuilder pageBuilder, float pageHeight)
    {
        double pdfX = Bounds.Left;
        double pdfY = pageHeight - Bounds.Bottom;

        pageBuilder.SetTextAndFillColor(Color.Red, Color.Green, Color.Blue);
        
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        pageBuilder.AddText(Text, FontSize, new PdfPoint(pdfX, pdfY), font);
    }
}

public class PdfShapeElementModel : PdfElementModel
{
    public bool IsFilled { get; set; } = true;
    public SKColor FillColor { get; set; } = SKColors.LightGray;
    public SKColor StrokeColor { get; set; } = SKColors.Black;
    public float StrokeWidth { get; set; } = 1f;

    public override void Render(SKCanvas canvas, float pageHeight)
    {
        if (IsFilled)
        {
            using var fillPaint = new SKPaint { Color = FillColor, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRect(Bounds, fillPaint);
        }
        using var strokePaint = new SKPaint { Color = StrokeColor, Style = SKPaintStyle.Stroke, StrokeWidth = StrokeWidth, IsAntialias = true };
        canvas.DrawRect(Bounds, strokePaint);
    }

    public override void WriteTo(PdfDocumentBuilder builder, PdfPageBuilder pageBuilder, float pageHeight)
    {
        double pdfLeft = Bounds.Left;
        double pdfBottom = pageHeight - Bounds.Bottom;
        double pdfWidth = Bounds.Width;
        double pdfHeight = Bounds.Height;

        if (IsFilled)
        {
            pageBuilder.SetTextAndFillColor(FillColor.Red, FillColor.Green, FillColor.Blue);
            pageBuilder.DrawRectangle(new PdfPoint(pdfLeft, pdfBottom), pdfWidth, pdfHeight, (double)StrokeWidth);
        }
        
        pageBuilder.SetStrokeColor(StrokeColor.Red, StrokeColor.Green, StrokeColor.Blue);
        pageBuilder.DrawRectangle(new PdfPoint(pdfLeft, pdfBottom), pdfWidth, pdfHeight, (double)StrokeWidth);
    }
}

public class PdfHighlightElementModel : PdfElementModel
{
    public override void Render(SKCanvas canvas, float pageHeight)
    {
        using var paint = new SKPaint { Color = new SKColor(255, 255, 0, 100), Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRect(Bounds, paint);
    }
    public override void WriteTo(PdfDocumentBuilder builder, PdfPageBuilder pageBuilder, float pageHeight)
    {
        double pdfLeft = Bounds.Left;
        double pdfBottom = pageHeight - Bounds.Bottom;
        pageBuilder.SetTextAndFillColor(255, 255, 0);
        pageBuilder.DrawRectangle(new PdfPoint(pdfLeft, pdfBottom), Bounds.Width, Bounds.Height, 0);
    }
}

public class PdfUnderlineElementModel : PdfElementModel
{
    public override void Render(SKCanvas canvas, float pageHeight)
    {
        using var paint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
        canvas.DrawLine(new SKPoint(Bounds.Left, Bounds.Bottom), new SKPoint(Bounds.Right, Bounds.Bottom), paint);
    }
    public override void WriteTo(PdfDocumentBuilder builder, PdfPageBuilder pageBuilder, float pageHeight)
    {
        double pdfLeft = Bounds.Left;
        double pdfBottom = pageHeight - Bounds.Bottom;
        pageBuilder.SetStrokeColor(255, 0, 0);
        pageBuilder.DrawLine(new PdfPoint(pdfLeft, pdfBottom), new PdfPoint(pdfLeft + Bounds.Width, pdfBottom), 2);
    }
}

public class PdfStickyNoteElementModel : PdfElementModel
{
    public string CommentText { get; set; } = "Note";
    public override void Render(SKCanvas canvas, float pageHeight)
    {
        using var paint = new SKPaint { Color = new SKColor(255, 215, 0), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var stroke = new SKPaint { Color = new SKColor(184, 134, 11), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        
        using var path = new SKPath();
        float left = Bounds.Left;
        float top = Bounds.Top;
        float w = Bounds.Width;
        float h = Bounds.Height;
        
        path.MoveTo(left + 2, top + 2);
        path.LineTo(left + w - 2, top + 2);
        path.LineTo(left + w - 2, top + h - 6);
        path.LineTo(left + 8, top + h - 6);
        path.LineTo(left + 2, top + h - 2);
        path.LineTo(left + 2, top + h - 6);
        path.Close();
        
        canvas.DrawPath(path, paint);
        canvas.DrawPath(path, stroke);
        
        using var linePaint = new SKPaint { Color = new SKColor(184, 134, 11), StrokeWidth = 1f };
        canvas.DrawLine(left + 5, top + 5, left + w - 5, top + 5, linePaint);
        canvas.DrawLine(left + 5, top + 9, left + w - 5, top + 9, linePaint);
    }
    public override void WriteTo(PdfDocumentBuilder builder, PdfPageBuilder pageBuilder, float pageHeight)
    {
        double pdfLeft = Bounds.Left;
        double pdfBottom = pageHeight - Bounds.Bottom;
        pageBuilder.SetTextAndFillColor(255, 255, 0);
        pageBuilder.DrawRectangle(new PdfPoint(pdfLeft, pdfBottom), Bounds.Width, Bounds.Height, 0);
        pageBuilder.SetTextAndFillColor(0, 0, 0);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        pageBuilder.AddText("Note", 12, new PdfPoint(pdfLeft + 5, pdfBottom + 5), font);
    }
}

public class PdfPencilElementModel : PdfElementModel
{
    public List<SKPoint> Points { get; set; } = new();
    public override void Render(SKCanvas canvas, float pageHeight)
    {
        if (Points.Count < 2) return;
        using var paint = new SKPaint { Color = Color, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
        using var path = new SKPath();
        path.MoveTo(Points[0]);
        for (int i = 1; i < Points.Count; i++) path.LineTo(Points[i]);
        canvas.DrawPath(path, paint);
    }
    public override void WriteTo(PdfDocumentBuilder builder, PdfPageBuilder pageBuilder, float pageHeight)
    {
        if (Points.Count < 2) return;
        pageBuilder.SetStrokeColor(Color.Red, Color.Green, Color.Blue);
        for (int i = 0; i < Points.Count - 1; i++)
        {
            var p1 = Points[i];
            var p2 = Points[i+1];
            pageBuilder.DrawLine(new PdfPoint(p1.X, pageHeight - p1.Y), new PdfPoint(p2.X, pageHeight - p2.Y), 2);
        }
    }
}

public class PdfFormFieldElementModel : PdfElementModel
{
    public string FieldType { get; set; } = "Text";
    public string Value { get; set; } = "";
    public override void Render(SKCanvas canvas, float pageHeight)
    {
        using var stroke = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        canvas.DrawRect(Bounds, stroke);
        using var fill = new SKPaint { Color = new SKColor(200, 220, 255, 128), Style = SKPaintStyle.Fill };
        canvas.DrawRect(Bounds, fill);
        if (FieldType == "Text" && !string.IsNullOrEmpty(Value))
        {
            using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 12f };
            canvas.DrawText(Value, Bounds.Left + 5, Bounds.Bottom - 5, textPaint);
        }
        else if (FieldType == "Checkbox" && Value == "True")
        {
            using var cross = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
            canvas.DrawLine(new SKPoint(Bounds.Left, Bounds.Top), new SKPoint(Bounds.Right, Bounds.Bottom), cross);
            canvas.DrawLine(new SKPoint(Bounds.Right, Bounds.Top), new SKPoint(Bounds.Left, Bounds.Bottom), cross);
        }
    }
    public override void WriteTo(PdfDocumentBuilder builder, PdfPageBuilder pageBuilder, float pageHeight)
    {
        double pdfLeft = Bounds.Left;
        double pdfBottom = pageHeight - Bounds.Bottom;
        pageBuilder.SetStrokeColor(0, 0, 255);
        pageBuilder.DrawRectangle(new PdfPoint(pdfLeft, pdfBottom), Bounds.Width, Bounds.Height, 1.5);
        if (FieldType == "Text" && !string.IsNullOrEmpty(Value))
        {
            pageBuilder.SetTextAndFillColor(0, 0, 0);
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            pageBuilder.AddText(Value, 12, new PdfPoint(pdfLeft + 5, pdfBottom + 5), font);
        }
    }
}

public class PdfPageModel
{
    public int Rotation { get; set; } = 0;
    public int Number { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public List<PdfElementModel> Elements { get; } = new();
}

public class PdfDocumentModel : IDisposable
{
    public string? FilePath { get; private set; }
    public List<PdfPageModel> Pages { get; } = new();
    
    private PdfDocument? _document;

    public void Load(string filePath)
    {
        FilePath = filePath;
        Pages.Clear();
        _document?.Dispose();
        _document = null;

        if (!File.Exists(filePath))
        {
            var blankPage = new PdfPageModel
            {
                Number = 1,
                Width = 595,
                Height = 842
            };
            blankPage.Elements.Add(new PdfTextElementModel
            {
                Text = "Welcome to the PDF WYSIWYG Editor!",
                FontSize = 18f,
                Color = SKColors.DarkSlateGray,
                Bounds = new SKRect(50, 80, 450, 110),
                IsOriginal = true,
                OriginalBounds = new SKRect(50, 80, 450, 110)
            });
            Pages.Add(blankPage);
            return;
        }

        _document = PdfDocument.Open(filePath);
        _document.AddSkiaPageFactory();

        foreach (var page in _document.GetPages())
        {
            var pageModel = new PdfPageModel
            {
                Number = page.Number,
                Width = (double)page.Width,
                Height = (double)page.Height
            };

            float pageHeight = (float)page.Height;

            // Extract words and group them into lines
            var words = page.GetWords().ToList();
            var lineGroups = new List<List<Word>>();
            
            foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
            {
                var targetGroup = lineGroups.FirstOrDefault(g => Math.Abs(g[0].BoundingBox.Bottom - word.BoundingBox.Bottom) < 3.0);
                if (targetGroup == null)
                {
                    targetGroup = new List<Word>();
                    lineGroups.Add(targetGroup);
                }
                targetGroup.Add(word);
            }

            foreach (var group in lineGroups)
            {
                var sortedWords = group.OrderBy(w => w.BoundingBox.Left).ToList();
                string textContent = string.Join(" ", sortedWords.Select(w => w.Text));
                
                var firstWord = sortedWords.First();
                var lastWord = sortedWords.Last();
                
                float left = (float)firstWord.BoundingBox.Left;
                float right = (float)lastWord.BoundingBox.Right;
                float top = pageHeight - (float)sortedWords.Max(w => w.BoundingBox.Top);
                float bottom = pageHeight - (float)sortedWords.Min(w => w.BoundingBox.Bottom);

                var textEl = new PdfTextElementModel
                {
                    Text = textContent,
                    FontSize = sortedWords.Max(w => (w.Letters.Count > 0 ? (float)w.Letters[0].FontSize : 12f)),
                    FontName = sortedWords.First().Letters.Count > 0 ? sortedWords.First().Letters[0].FontName : "Helvetica",
                    Color = SKColors.Black,
                    Bounds = SKRect.Create(left, top, right - left, bottom - top),
                    IsOriginal = true,
                    OriginalBounds = SKRect.Create(left, top, right - left, bottom - top)
                };

                pageModel.Elements.Add(textEl);
            }

            // Extract granular words
            foreach (var word in words)
            {
                var box = word.BoundingBox;
                float left = (float)box.Left;
                float bottom = pageHeight - (float)box.Bottom;
                float right = (float)box.Right;
                float top = pageHeight - (float)box.Top;
                
                var textEl = new PdfTextElementModel
                {
                    Text = word.Text,
                    FontSize = word.Letters.Count > 0 ? (float)word.Letters[0].FontSize : 12f,
                    FontName = word.Letters.Count > 0 ? word.Letters[0].FontName : "Helvetica",
                    Color = SKColors.Black,
                    Bounds = SKRect.Create(left, top, right - left, bottom - top),
                    IsOriginal = true,
                    OriginalBounds = SKRect.Create(left, top, right - left, bottom - top),
                    IsGranular = true
                };
                pageModel.Elements.Add(textEl);
            }

            // Extract images
            int imgIdx = 1;
            foreach (var img in page.GetImages())
            {
                var box = img.Bounds;
                float left = (float)box.Left;
                float bottom = pageHeight - (float)box.Bottom;
                float right = (float)box.Right;
                float top = pageHeight - (float)box.Top;

                var imgEl = new PdfImageElementModel
                {
                    ImageId = $"Image_{imgIdx++}",
                    Bounds = SKRect.Create(left, top, right - left, bottom - top),
                    IsOriginal = true,
                    OriginalBounds = SKRect.Create(left, top, right - left, bottom - top)
                };
                pageModel.Elements.Add(imgEl);
            }

            // Extract shapes
            foreach (var path in page.ExperimentalAccess.Paths)
            {
                var boundingBox = path.GetBoundingRectangle();
                if (boundingBox.HasValue)
                {
                    var box = boundingBox.Value;
                    
                    if (box.Width <= 0f && box.Height <= 0f) continue;
                    if (box.Width >= pageModel.Width * 0.95 && box.Height >= pageModel.Height * 0.95) continue;

                    float left = (float)box.Left;
                    float bottom = pageHeight - (float)box.Bottom;
                    float right = (float)box.Right;
                    float top = pageHeight - (float)box.Top;

                    var shapeEl = new PdfShapeElementModel
                    {
                        IsFilled = path.IsFilled,
                        FillColor = SKColors.LightGray,
                        StrokeColor = SKColors.Black,
                        StrokeWidth = 1f,
                        Bounds = SKRect.Create(left, top, right - left, bottom - top),
                        IsOriginal = true,
                        OriginalBounds = SKRect.Create(left, top, right - left, bottom - top)
                    };
                    pageModel.Elements.Add(shapeEl);
                }
            }

            Pages.Add(pageModel);
        }
    }

    private readonly object _renderLock = new();

    public SKBitmap? RenderPageToBitmap(int pageNumber, float scale)
    {
        if (_document == null)
        {
            return new SKBitmap(10, 10);
        }
        lock (_renderLock)
        {
            try
            {
                return _document.GetPageAsSKBitmap(pageNumber, scale, UglyToad.PdfPig.Graphics.Colors.RGBColor.White);
            }
            catch
            {
                return new SKBitmap(10, 10);
            }
        }
    }

    public void RotatePage(int pageIndex, int degrees)
    {
        if (pageIndex >= 0 && pageIndex < Pages.Count)
        {
            Pages[pageIndex].Rotation = (Pages[pageIndex].Rotation + degrees) % 360;
        }
    }

    public void InsertPage(int pageIndex)
    {
        var blankPage = new PdfPageModel { Number = Pages.Count + 1, Width = 595, Height = 842 };
        if (pageIndex >= 0 && pageIndex <= Pages.Count) Pages.Insert(pageIndex, blankPage);
        else Pages.Add(blankPage);
    }

    public void DeletePage(int pageIndex)
    {
        if (pageIndex >= 0 && pageIndex < Pages.Count) Pages.RemoveAt(pageIndex);
    }

    public void Save(string filePath)
    {
        using var builder = new PdfDocumentBuilder();
        foreach (var pageModel in Pages)
        {
            var pageBuilder = builder.AddPage(pageModel.Width, pageModel.Height);
            float pageHeight = (float)pageModel.Height;

            foreach (var element in pageModel.Elements)
            {
                if (!element.IsDeleted)
                {
                    element.WriteTo(builder, pageBuilder, pageHeight);
                }
            }
        }

        byte[] fileBytes = builder.Build();
        File.WriteAllBytes(filePath, fileBytes);
        FilePath = filePath;
    }

    public void Dispose()
    {
        _document?.Dispose();
    }
}

public class PdfImageElementModel : PdfElementModel
{
    public string ImageId { get; set; } = "";
    public override void Render(SKCanvas canvas, float pageHeight)
    {
        using var paint = new SKPaint { Color = new SKColor(41, 128, 185, 100), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        canvas.DrawRect(Bounds, paint);
    }
    public override void WriteTo(PdfDocumentBuilder builder, PdfPageBuilder pageBuilder, float pageHeight)
    {
    }
}
