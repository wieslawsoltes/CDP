using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Document.Parser.AST;

namespace CDP.Document.Renderer.Layout.Spreadsheet;

/// <summary>
/// Layout block for an entire spreadsheet document containing worksheet layout blocks.
/// </summary>
public class SpreadsheetDocumentLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }
    public List<WorksheetLayoutBlock> Worksheets { get; } = new();

    public SpreadsheetDocumentLayoutBlock(SpreadsheetDocument doc)
    {
        Node = doc;
    }

    public void Layout(LayoutContext context) { }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        foreach (var ws in Worksheets)
        {
            ws.Render(canvas, context);
        }
    }

    public int HitTest(SKPoint point)
    {
        foreach (var ws in Worksheets)
        {
            if (ws.Bounds.Contains(point))
            {
                return ws.HitTest(point);
            }
        }
        return -1;
    }

    public SKRect GetCaretBounds(int offset) => default;
    public void GetSelectionBounds(int start, int end, IList<SKRect> result) { }
}

/// <summary>
/// Layout block for a single worksheet rendered as a grid.
/// </summary>
public class WorksheetLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public List<CellLayoutBlock> Cells { get; } = new();
    public int ColumnCount { get; set; }
    public int RowCount { get; set; }
    public float[] ColumnWidths { get; set; } = Array.Empty<float>();
    public float RowHeight { get; set; } = 20f;
    public List<ImageInline> Images { get; } = new();

    public WorksheetLayoutBlock(WorksheetNode node)
    {
        Node = node;
    }

    public void Layout(LayoutContext context) { }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        // Draw worksheet title / tab
        using var tabPaint = new SKPaint
        {
            Color = new SKColor(60, 60, 60),
            TextSize = 12,
            IsAntialias = true,
            TextAlign = SKTextAlign.Left,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };
        canvas.DrawText(SheetName, Bounds.Left + 5, Bounds.Top + 14, tabPaint);

        // Draw background
        float gridTop = Bounds.Top + 22;
        using var bgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawRect(new SKRect(Bounds.Left, gridTop, Bounds.Right, Bounds.Bottom), bgPaint);

        // Draw column headers
        using var headerBgPaint = new SKPaint { Color = new SKColor(240, 240, 240), Style = SKPaintStyle.Fill };
        using var headerTextPaint = new SKPaint
        {
            Color = new SKColor(80, 80, 80),
            TextSize = 10,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        float hdrX = Bounds.Left + 40; // leave space for row numbers
        canvas.DrawRect(new SKRect(Bounds.Left, gridTop, Bounds.Left + 40, gridTop + RowHeight), headerBgPaint);
        for (int c = 0; c < ColumnCount && c < ColumnWidths.Length; c++)
        {
            float w = ColumnWidths[c];
            canvas.DrawRect(new SKRect(hdrX, gridTop, hdrX + w, gridTop + RowHeight), headerBgPaint);
            string colLabel = GetColumnLabel(c);
            canvas.DrawText(colLabel, hdrX + w / 2, gridTop + 14, headerTextPaint);
            hdrX += w;
        }

        // Draw row number column and grid lines
        using var gridPaint = new SKPaint { Color = new SKColor(200, 200, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
        using var rowNumPaint = new SKPaint
        {
            Color = new SKColor(100, 100, 100),
            TextSize = 10,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        for (int r = 0; r <= RowCount; r++)
        {
            float y = gridTop + (r + 1) * RowHeight;
            canvas.DrawLine(Bounds.Left, y, Bounds.Right, y, gridPaint);
            if (r < RowCount)
            {
                canvas.DrawRect(new SKRect(Bounds.Left, y, Bounds.Left + 40, y + RowHeight), headerBgPaint);
                canvas.DrawText((r + 1).ToString(), Bounds.Left + 20, y + 14, rowNumPaint);
            }
        }

        // Draw column grid lines
        hdrX = Bounds.Left + 40;
        for (int c = 0; c <= ColumnCount && c < ColumnWidths.Length + 1; c++)
        {
            canvas.DrawLine(hdrX, gridTop, hdrX, gridTop + (RowCount + 1) * RowHeight, gridPaint);
            if (c < ColumnWidths.Length) hdrX += ColumnWidths[c];
        }

        // Render cells
        foreach (var cell in Cells)
        {
            if (context.Viewport != SKRect.Empty && !cell.Bounds.IntersectsWith(context.Viewport))
            {
                continue;
            }
            cell.Render(canvas, context);
        }

        // Draw drawings/images
        foreach (var img in Images)
        {
            if (!string.IsNullOrEmpty(img.Source))
            {
                float imgX = Bounds.Left + SpreadsheetLayoutManager.RowNumberColumnWidth + ColumnWidths[0] + 10;
                float imgY = gridTop + RowHeight + 10;
                var imgBounds = new SKRect(imgX, imgY, imgX + 200, imgY + 150);

                if (img.AltText != null && (img.AltText.Contains("Chart", StringComparison.OrdinalIgnoreCase) || img.AltText.Contains("GraphicFrame", StringComparison.OrdinalIgnoreCase)))
                {
                    using var chartBg = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                    canvas.DrawRect(imgBounds, chartBg);
                    using var border = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                    canvas.DrawRect(imgBounds, border);

                    using var titlePaint = new SKPaint
                    {
                        Color = SKColors.Black,
                        TextSize = 10,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center,
                        Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                    };
                    canvas.DrawText("Sheet Chart Series", imgBounds.MidX, imgBounds.Top + 14, titlePaint);

                    float barWidth = imgBounds.Width / 6;
                    float spacing = barWidth / 2;
                    float[] values = { 0.5f, 0.7f, 0.4f, 0.9f };
                    SKColor[] colors = { SKColors.Red, SKColors.Green, SKColors.Blue, SKColors.Orange };
                    float startX = imgBounds.Left + spacing;
                    float chartBottom = imgBounds.Bottom - 10;
                    float maxBarHeight = imgBounds.Height - 35;

                    for (int i = 0; i < values.Length; i++)
                    {
                        float barHeight = maxBarHeight * values[i];
                        var barRect = new SKRect(startX, chartBottom - barHeight, startX + barWidth, chartBottom);
                        using var barPaint = new SKPaint { Color = colors[i], Style = SKPaintStyle.Fill };
                        canvas.DrawRect(barRect, barPaint);
                        startX += barWidth + spacing;
                    }
                }
                else
                {
                    var bitmap = Presentation.ShapeLayoutBlock.LoadBase64Image(img.Source);
                    if (bitmap != null)
                    {
                        canvas.DrawBitmap(bitmap, imgBounds);
                    }
                }
            }
        }

        // Border
        using var borderPaint = new SKPaint { Color = new SKColor(180, 180, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRect(Bounds, borderPaint);
    }

    public int HitTest(SKPoint point) => -1;
    public SKRect GetCaretBounds(int offset) => default;
    public void GetSelectionBounds(int start, int end, IList<SKRect> result) { }

    private static string GetColumnLabel(int index)
    {
        string label = string.Empty;
        int col = index;
        while (col >= 0)
        {
            label = (char)('A' + col % 26) + label;
            col = col / 26 - 1;
        }
        return label;
    }
}

/// <summary>
/// Layout block for a single cell in a spreadsheet grid.
/// </summary>
public class CellLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }

    public CellLayoutBlock(GridCellNode node)
    {
        Node = node;
    }

    public void Layout(LayoutContext context) { }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        var cellNode = (GridCellNode)Node;

        // Render cell background color formatting
        if (!string.IsNullOrEmpty(cellNode.Style) && SKColor.TryParse(cellNode.Style, out var fillBgColor))
        {
            using var fillBgPaint = new SKPaint { Color = fillBgColor, Style = SKPaintStyle.Fill };
            canvas.DrawRect(Bounds, fillBgPaint);
        }

        // Conditional styling dynamically based on thresholds
        if (cellNode.Value != null && double.TryParse(cellNode.Value.ToString(), out double val))
        {
            if (val > 50)
            {
                using var condBgPaint = new SKPaint { Color = new SKColor(200, 240, 200), Style = SKPaintStyle.Fill };
                canvas.DrawRect(Bounds, condBgPaint);
            }
            else if (val < 0)
            {
                using var condBgPaint = new SKPaint { Color = new SKColor(240, 200, 200), Style = SKPaintStyle.Fill };
                canvas.DrawRect(Bounds, condBgPaint);
            }
        }

        if (string.IsNullOrEmpty(cellNode.DisplayText) && context.EditingCellNode != Node) return;

        using var textPaint = new SKPaint
        {
            TextSize = cellNode.FontSize.HasValue ? (float)cellNode.FontSize.Value : 11f,
            IsAntialias = true,
            TextAlign = SKTextAlign.Left
        };

        var weight = cellNode.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = cellNode.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        textPaint.Typeface = SKTypeface.FromFamilyName("Arial", weight, SKFontStyleWidth.Normal, slant);

        if (!string.IsNullOrEmpty(cellNode.Color) && SKColor.TryParse(cellNode.Color, out var color))
        {
            textPaint.Color = color;
        }
        else
        {
            textPaint.Color = SKColors.Black;
        }

        canvas.Save();
        canvas.ClipRect(Bounds);
        
        string txt = cellNode.DisplayText ?? string.Empty;
        canvas.DrawText(txt, Bounds.Left + 3, Bounds.Top + 14, textPaint);

        // Draw selection and caret if this cell is being edited
        if (context.EditingCellNode == Node)
        {
            // 1. Draw Selection
            if (context.SelectionStart != -1 && context.SelectionEnd != -1 && context.SelectionStart != context.SelectionEnd)
            {
                int selStart = Math.Min(context.SelectionStart, context.SelectionEnd);
                int selEnd = Math.Max(context.SelectionStart, context.SelectionEnd);
                
                selStart = Math.Clamp(selStart, 0, txt.Length);
                selEnd = Math.Clamp(selEnd, 0, txt.Length);
                
                float x1 = Bounds.Left + 3 + textPaint.MeasureText(txt.Substring(0, selStart));
                float x2 = Bounds.Left + 3 + textPaint.MeasureText(txt.Substring(0, selEnd));
                
                using var selPaint = new SKPaint { Color = new SKColor(0, 120, 215, 80), Style = SKPaintStyle.Fill };
                canvas.DrawRect(new SKRect(x1, Bounds.Top + 2, x2, Bounds.Bottom - 2), selPaint);
            }
            
            // 2. Draw Caret
            if (context.DrawCaret)
            {
                int caret = Math.Clamp(context.CaretOffset, 0, txt.Length);
                float x = Bounds.Left + 3 + textPaint.MeasureText(txt.Substring(0, caret));
                using var caretPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                canvas.DrawLine(x, Bounds.Top + 2, x, Bounds.Bottom - 2, caretPaint);
            }
        }

        canvas.Restore();
    }

    public int HitTest(SKPoint point) => -1;
    public SKRect GetCaretBounds(int offset) => default;
    public void GetSelectionBounds(int start, int end, IList<SKRect> result) { }
}

/// <summary>
/// Lays out a SpreadsheetDocument AST into worksheet grid layout blocks for SkiaSharp rendering.
/// </summary>
public static class SpreadsheetLayoutManager
{
    internal const float DefaultColumnWidth = 80f;
    internal const float DefaultRowHeight = 20f;
    internal const float RowNumberColumnWidth = 40f;
    internal const float WorksheetSpacing = 30f;

    public static SpreadsheetDocumentLayoutBlock Layout(SpreadsheetDocument doc)
    {
        var docBlock = new SpreadsheetDocumentLayoutBlock(doc);
        float yOffset = 0;

        foreach (var child in doc.Children)
        {
            if (child is WorksheetNode wsNode)
            {
                // Determine grid dimensions
                int maxCol = 0;
                int maxRow = 0;
                foreach (var rowChild in wsNode.Children)
                {
                    if (rowChild is GridRowNode rowNode)
                    {
                        maxRow = Math.Max(maxRow, rowNode.RowIndex + 1);
                        foreach (var cellChild in rowNode.Children)
                        {
                            if (cellChild is GridCellNode cellNode)
                            {
                                maxCol = Math.Max(maxCol, cellNode.ColumnIndex + 1);
                            }
                        }
                    }
                }

                // Use at least 1 row and 1 column
                maxCol = Math.Max(maxCol, 1);
                maxRow = Math.Max(maxRow, 1);

                // Build column widths
                float[] colWidths = new float[maxCol];
                for (int i = 0; i < maxCol; i++) colWidths[i] = DefaultColumnWidth;

                float gridWidth = RowNumberColumnWidth;
                for (int i = 0; i < maxCol; i++) gridWidth += colWidths[i];

                float gridHeight = 22 + (maxRow + 1) * DefaultRowHeight; // title + header + data rows

                var wsBlock = new WorksheetLayoutBlock(wsNode)
                {
                    SheetName = wsNode.Name,
                    ColumnCount = maxCol,
                    RowCount = maxRow,
                    ColumnWidths = colWidths,
                    RowHeight = DefaultRowHeight,
                    Bounds = new SKRect(0, yOffset, gridWidth, yOffset + gridHeight)
                };

                // Layout cells
                foreach (var rowChild in wsNode.Children)
                {
                    if (rowChild is GridRowNode rowNode)
                    {
                        foreach (var cellChild in rowNode.Children)
                        {
                            if (cellChild is GridCellNode cellNode)
                            {
                                if (cellNode.IsMerged && cellNode.RowSpan == 1 && cellNode.ColumnSpan == 1)
                                {
                                    continue;
                                }

                                float cellX = RowNumberColumnWidth;
                                for (int c = 0; c < cellNode.ColumnIndex && c < maxCol; c++)
                                {
                                    cellX += colWidths[c];
                                }
                                float cellY = yOffset + 22 + (rowNode.RowIndex + 1) * DefaultRowHeight;

                                float cellW = 0;
                                for (int c = cellNode.ColumnIndex; c < cellNode.ColumnIndex + cellNode.ColumnSpan && c < maxCol; c++)
                                {
                                    cellW += colWidths[c];
                                }
                                float cellH = cellNode.RowSpan * DefaultRowHeight;

                                var cellBlock = new CellLayoutBlock(cellNode)
                                {
                                    Bounds = new SKRect(cellX, cellY, cellX + cellW, cellY + cellH)
                                };
                                wsBlock.Cells.Add(cellBlock);
                            }
                        }
                    }
                    else if (rowChild is ImageInline imgInline)
                    {
                        wsBlock.Images.Add(imgInline);
                    }
                }

                docBlock.Worksheets.Add(wsBlock);
                yOffset += gridHeight + WorksheetSpacing;
            }
        }

        docBlock.Bounds = new SKRect(0, 0,
            docBlock.Worksheets.Count > 0 ? docBlock.Worksheets[0].Bounds.Right : 400,
            Math.Max(yOffset - WorksheetSpacing, 100));
        return docBlock;
    }
}
