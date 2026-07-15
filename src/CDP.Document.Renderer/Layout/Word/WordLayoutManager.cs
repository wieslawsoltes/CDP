using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Document.Parser.AST;
using CDP.Document.Renderer.Text;

namespace CDP.Document.Renderer.Layout.Word;

public class LineSegment
{
    public string Text { get; set; } = string.Empty;
    public TextRun Run { get; set; } = null!;
    public float XOffset { get; set; }
    public float Width { get; set; }
    public int StartOffset { get; set; } // Paragraph-relative start char offset
    public int Length => Text.Length;
    public ImageInline? Image { get; set; }
}

public class ParagraphLine
{
    public string Text { get; set; } = string.Empty;
    public List<LineSegment> Segments { get; set; } = new();
    public float Width { get; set; }
    public float Height { get; set; }
    public float YOffset { get; set; } // Relative to Paragraph bounds top
    public int StartOffset { get; set; } // Paragraph-relative start char offset
    public int Length { get; set; }
}

public class ParagraphLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }
    public List<ParagraphLine> Lines { get; } = new();
    public int GlobalStartOffset { get; set; }

    public ParagraphLayoutBlock(ParagraphBlock node)
    {
        Node = node;
    }

    public void Layout(LayoutContext context)
    {
        // Paragraph layout is handled by WordLayoutManager
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        canvas.Save();
        canvas.Translate(Bounds.Left, Bounds.Top);

        foreach (var line in Lines)
        {
            foreach (var segment in line.Segments)
            {
                if (segment.Image != null && !string.IsNullOrEmpty(segment.Image.Source))
                {
                    var bitmap = Presentation.ShapeLayoutBlock.LoadBase64Image(segment.Image.Source);
                    if (bitmap != null)
                    {
                        var imgRect = new SKRect(segment.XOffset, line.YOffset, segment.XOffset + segment.Width, line.YOffset + line.Height);
                        canvas.DrawBitmap(bitmap, imgRect);
                        continue;
                    }
                }

                using var paint = WordLayoutManager.GetPaint(segment.Run);
                canvas.DrawText(segment.Text, segment.XOffset, line.YOffset + paint.TextSize, paint);

                if (segment.Run.Underline)
                {
                    using var ulPaint = new SKPaint
                    {
                        Color = paint.Color,
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 1
                    };
                    float y = line.YOffset + paint.TextSize + 2;
                    canvas.DrawLine(segment.XOffset, y, segment.XOffset + segment.Width, y, ulPaint);
                }
            }
        }

        // Draw Selection highlight if active
        if (context.SelectionStart != -1 && context.SelectionEnd != -1)
        {
            var selectionRects = new List<SKRect>();
            GetSelectionBounds(context.SelectionStart - GlobalStartOffset, context.SelectionEnd - GlobalStartOffset, selectionRects);
            using var selPaint = new SKPaint { Color = new SKColor(0, 120, 215, 80), Style = SKPaintStyle.Fill };
            foreach (var r in selectionRects)
            {
                // Convert global bounds back to local for rendering
                var localRect = new SKRect(r.Left - Bounds.Left, r.Top - Bounds.Top, r.Right - Bounds.Left, r.Bottom - Bounds.Top);
                canvas.DrawRect(localRect, selPaint);
            }
        }

        // Draw Caret if active
        if (context.DrawCaret && context.CaretOffset >= GlobalStartOffset)
        {
            int localCaret = context.CaretOffset - GlobalStartOffset;
            int totalLen = 0;
            foreach (var line in Lines) totalLen += line.Length;
            if (localCaret >= 0 && localCaret <= totalLen)
            {
                var caretRect = GetCaretBounds(localCaret);
                var localCaretRect = new SKRect(caretRect.Left - Bounds.Left, caretRect.Top - Bounds.Top, caretRect.Right - Bounds.Left, caretRect.Bottom - Bounds.Top);
                using var caretPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill };
                canvas.DrawRect(localCaretRect, caretPaint);
            }
        }

        canvas.Restore();
    }

    public int HitTest(SKPoint point)
    {
        // Point is relative to page/document, convert to local
        float localX = point.X - Bounds.Left;
        float localY = point.Y - Bounds.Top;

        foreach (var line in Lines)
        {
            if (localY >= line.YOffset && localY <= line.YOffset + line.Height)
            {
                float currentX = 0;
                foreach (var segment in line.Segments)
                {
                    using var paint = WordLayoutManager.GetPaint(segment.Run);
                    string text = segment.Text;
                    for (int i = 0; i < text.Length; i++)
                    {
                        float w = paint.MeasureText(text.Substring(i, 1));
                        if (localX >= currentX && localX <= currentX + w)
                        {
                            if (localX > currentX + w / 2)
                            {
                                return segment.StartOffset + i + 1;
                            }
                            return segment.StartOffset + i;
                        }
                        currentX += w;
                    }
                }
                return line.StartOffset + line.Length;
            }
        }
        return 0;
    }

    public SKRect GetCaretBounds(int offset)
    {
        int totalLen = 0;
        foreach (var line in Lines) totalLen += line.Length;
        offset = Math.Clamp(offset, 0, totalLen);

        foreach (var line in Lines)
        {
            if (offset >= line.StartOffset && offset <= line.StartOffset + line.Length)
            {
                float currentX = 0;
                int relOffset = offset - line.StartOffset;

                foreach (var segment in line.Segments)
                {
                    int segRel = relOffset - (segment.StartOffset - line.StartOffset);
                    if (segRel >= 0 && segRel <= segment.Length)
                    {
                        using var paint = WordLayoutManager.GetPaint(segment.Run);
                        float w = paint.MeasureText(segment.Text.Substring(0, segRel));
                        float x = currentX + w;
                        return new SKRect(Bounds.Left + x - 1, Bounds.Top + line.YOffset, Bounds.Left + x + 1, Bounds.Top + line.YOffset + line.Height);
                    }
                    currentX += segment.Width;
                }
                return new SKRect(Bounds.Left + line.Width - 1, Bounds.Top + line.YOffset, Bounds.Left + line.Width + 1, Bounds.Top + line.YOffset + line.Height);
            }
        }
        return new SKRect(Bounds.Left, Bounds.Top, Bounds.Left + 2, Bounds.Top + 12);
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        if (start > end)
        {
            int temp = start;
            start = end;
            end = temp;
        }

        foreach (var line in Lines)
        {
            int lineStart = line.StartOffset;
            int lineEnd = lineStart + line.Length;

            int selStart = Math.Max(start, lineStart);
            int selEnd = Math.Min(end, lineEnd);

            if (selStart < selEnd)
            {
                float startX = -1;
                float endX = -1;

                float currentX = 0;
                foreach (var segment in line.Segments)
                {
                    int segStart = segment.StartOffset;
                    int segEnd = segStart + segment.Length;

                    if (selStart >= segStart && selStart <= segEnd)
                    {
                        using var paint = WordLayoutManager.GetPaint(segment.Run);
                        startX = currentX + paint.MeasureText(segment.Text.Substring(0, selStart - segStart));
                    }

                    if (selEnd >= segStart && selEnd <= segEnd)
                    {
                        using var paint = WordLayoutManager.GetPaint(segment.Run);
                        endX = currentX + paint.MeasureText(segment.Text.Substring(0, selEnd - segStart));
                    }

                    currentX += segment.Width;
                }

                if (startX < 0) startX = 0;
                if (endX < 0) endX = line.Width;

                result.Add(new SKRect(Bounds.Left + startX, Bounds.Top + line.YOffset, Bounds.Left + endX, Bounds.Top + line.YOffset + line.Height));
            }
        }
    }
}

public class TableCellLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }
    public List<IDocumentLayoutBlock> Blocks { get; } = new();

    public TableCellLayoutBlock(TableCellBlock node)
    {
        Node = node;
    }

    public void Layout(LayoutContext context)
    {
        // TableCell content layout is driven by WordLayoutManager
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        // Draw Cell border and background
        using var bgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawRect(Bounds, bgPaint);

        using var borderPaint = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRect(Bounds, borderPaint);

        canvas.Save();
        // Translate content rendering inside margins (padding)
        canvas.ClipRect(Bounds);
        
        foreach (var block in Blocks)
        {
            block.Render(canvas, context);
        }
        canvas.Restore();
    }

    public int HitTest(SKPoint point)
    {
        foreach (var block in Blocks)
        {
            if (block.Bounds.Contains(point))
            {
                return block.HitTest(point);
            }
        }
        return -1;
    }

    public SKRect GetCaretBounds(int offset)
    {
        foreach (var block in Blocks)
        {
            var rect = block.GetCaretBounds(offset);
            if (rect != default) return rect;
        }
        return default;
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        foreach (var block in Blocks)
        {
            block.GetSelectionBounds(start, end, result);
        }
    }
}

public class TableRowLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }
    public List<TableCellLayoutBlock> Cells { get; } = new();

    public TableRowLayoutBlock(TableRowBlock node)
    {
        Node = node;
    }

    public void Layout(LayoutContext context) { }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        foreach (var cell in Cells)
        {
            cell.Render(canvas, context);
        }
    }

    public int HitTest(SKPoint point)
    {
        foreach (var cell in Cells)
        {
            if (cell.Bounds.Contains(point))
            {
                return cell.HitTest(point);
            }
        }
        return -1;
    }

    public SKRect GetCaretBounds(int offset)
    {
        foreach (var cell in Cells)
        {
            var rect = cell.GetCaretBounds(offset);
            if (rect != default) return rect;
        }
        return default;
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        foreach (var cell in Cells)
        {
            cell.GetSelectionBounds(start, end, result);
        }
    }
}

public class TableLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }
    public List<TableRowLayoutBlock> Rows { get; } = new();

    public TableLayoutBlock(TableBlock node)
    {
        Node = node;
    }

    public void Layout(LayoutContext context) { }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        foreach (var row in Rows)
        {
            row.Render(canvas, context);
        }
    }

    public int HitTest(SKPoint point)
    {
        foreach (var row in Rows)
        {
            if (row.Bounds.Contains(point))
            {
                return row.HitTest(point);
            }
        }
        return -1;
    }

    public SKRect GetCaretBounds(int offset)
    {
        foreach (var row in Rows)
        {
            var rect = row.GetCaretBounds(offset);
            if (rect != default) return rect;
        }
        return default;
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        foreach (var row in Rows)
        {
            row.GetSelectionBounds(start, end, result);
        }
    }
}

public class PageLayoutBlock : IDocumentLayoutBlock
{
    public DocumentNode Node { get; }
    public SKRect Bounds { get; set; }
    public int PageIndex { get; set; }
    public List<IDocumentLayoutBlock> Blocks { get; } = new();

    public PageLayoutBlock(DocumentNode node, int pageIndex, SKRect bounds)
    {
        Node = node;
        PageIndex = pageIndex;
        Bounds = bounds;
    }

    public void Layout(LayoutContext context) { }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        // 1. Draw Page Background and shadow
        using var bgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawRect(Bounds, bgPaint);

        using var borderPaint = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRect(Bounds, borderPaint);

        // 2. Draw Header
        using var headerPaint = new SKPaint
        {
            Color = SKColors.DarkGray,
            TextSize = 9,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };
        float headerY = Bounds.Top + 30;
        var wordDoc = Node as WordDocument;
        string headerText = !string.IsNullOrEmpty(wordDoc?.Header) ? wordDoc.Header : "CDP Document Viewer";
        canvas.DrawText(headerText, Bounds.MidX, headerY, headerPaint);
        using var linePaint = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
        canvas.DrawLine(Bounds.Left + 54, headerY + 6, Bounds.Right - 54, headerY + 6, linePaint);

        // 3. Draw Content Blocks
        canvas.Save();
        // Clip content to margins
        SKRect contentClip = new SKRect(
            Bounds.Left + 54,
            Bounds.Top + 54,
            Bounds.Right - 54,
            Bounds.Bottom - 54
        );
        canvas.ClipRect(contentClip);

        foreach (var block in Blocks)
        {
            block.Render(canvas, context);
        }
        canvas.Restore();

        // 4. Draw Footer
        float footerY = Bounds.Bottom - 30;
        canvas.DrawLine(Bounds.Left + 54, footerY - 10, Bounds.Right - 54, footerY - 10, linePaint);
        string footerText = !string.IsNullOrEmpty(wordDoc?.Footer) ? $"{wordDoc.Footer} - Page {PageIndex + 1}" : $"Page {PageIndex + 1}";
        canvas.DrawText(footerText, Bounds.MidX, footerY, headerPaint);
    }

    public int HitTest(SKPoint point)
    {
        foreach (var block in Blocks)
        {
            if (block.Bounds.Contains(point))
            {
                return block.HitTest(point);
            }
        }
        return -1;
    }

    public SKRect GetCaretBounds(int offset)
    {
        foreach (var block in Blocks)
        {
            var rect = block.GetCaretBounds(offset);
            if (rect != default) return rect;
        }
        return default;
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        foreach (var block in Blocks)
        {
            block.GetSelectionBounds(start, end, result);
        }
    }
}

public static class WordLayoutManager
{
    public static int ColumnCount { get; set; } = 1;
    public static float ColumnGap { get; set; } = 18f; // 0.25 in

    public static WordDocumentLayoutBlock Layout(WordDocument doc, LayoutContext context)
    {
        var docBlock = new WordDocumentLayoutBlock(doc);

        context.CurrentPageIndex = 0;
        context.CurrentY = context.MarginTop;

        // Current page setup
        float pageOffsetTop = 0;
        var currentPage = new PageLayoutBlock(doc, 0, new SKRect(0, 0, context.PageWidth, context.PageHeight));
        docBlock.Pages.Add(currentPage);

        int currentColumn = 0;
        float colWidth = (context.ContentWidth - (ColumnCount - 1) * ColumnGap) / ColumnCount;
        float currentX = context.MarginLeft + currentColumn * (colWidth + ColumnGap);

        int globalOffset = 0;

        foreach (var child in doc.Children)
        {
            if (child is ParagraphBlock para)
            {
                // 1. Wrap and prepare paragraph lines
                var paraBlock = LayoutParagraph(para, colWidth, ref globalOffset);

                // Flow lines onto page/column layout
                int lineIdx = 0;
                while (lineIdx < paraBlock.Lines.Count)
                {
                    // Check how many lines fit in the current page/column
                    float remainingHeight = context.MarginTop + context.ContentHeight - context.CurrentY;
                    
                    var fittingLines = new List<ParagraphLine>();
                    float heightUsed = 0;

                    while (lineIdx < paraBlock.Lines.Count)
                    {
                        var line = paraBlock.Lines[lineIdx];
                        if (heightUsed + line.Height <= remainingHeight)
                        {
                            line.YOffset = context.CurrentY - pageOffsetTop - (context.MarginTop - context.MarginTop); // relative to page start or block start?
                            // Let's place it
                            fittingLines.Add(line);
                            heightUsed += line.Height;
                            lineIdx++;
                        }
                        else
                        {
                            break; // doesn't fit in current column/page
                        }
                    }

                    if (fittingLines.Count > 0)
                    {
                        // Create a paragraph block representing this chunk on this page
                        var chunkBlock = new ParagraphLayoutBlock(para)
                        {
                            GlobalStartOffset = paraBlock.GlobalStartOffset + fittingLines[0].StartOffset,
                            Bounds = new SKRect(
                                currentPage.Bounds.Left + currentX,
                                pageOffsetTop + context.CurrentY,
                                currentPage.Bounds.Left + currentX + colWidth,
                                pageOffsetTop + context.CurrentY + heightUsed
                            )
                        };

                        // Position lines relative to block top
                        float localY = 0;
                        foreach (var fl in fittingLines)
                        {
                            fl.YOffset = localY;
                            chunkBlock.Lines.Add(fl);
                            localY += fl.Height;
                        }

                        currentPage.Blocks.Add(chunkBlock);
                        context.CurrentY += heightUsed;
                    }

                    if (lineIdx < paraBlock.Lines.Count)
                    {
                        // Column or page break needed
                        currentColumn++;
                        if (currentColumn >= ColumnCount)
                        {
                            // Page break
                            currentColumn = 0;
                            context.CurrentPageIndex++;
                            pageOffsetTop = context.CurrentPageIndex * (context.PageHeight + 20f); // 20pt spacer between pages
                            currentPage = new PageLayoutBlock(doc, context.CurrentPageIndex, new SKRect(0, pageOffsetTop, context.PageWidth, pageOffsetTop + context.PageHeight));
                            docBlock.Pages.Add(currentPage);
                        }
                        currentX = context.MarginLeft + currentColumn * (colWidth + ColumnGap);
                        context.CurrentY = context.MarginTop;
                    }
                }
            }
            else if (child is TableBlock table)
            {
                // Lay out table rows
                var tableBlock = new TableLayoutBlock(table);
                
                // Determine column widths inside table
                int maxCols = 1;
                foreach (var rowNode in table.Children)
                {
                    if (rowNode is TableRowBlock r)
                        maxCols = Math.Max(maxCols, r.Children.Count);
                }
                float cellWidth = colWidth / maxCols;

                int rowIdx = 0;
                while (rowIdx < table.Children.Count)
                {
                    var rowNode = (TableRowBlock)table.Children[rowIdx];
                    
                    // Measure row height
                    float rowHeight = 0;
                    var cellBlocks = new List<TableCellLayoutBlock>();

                    for (int cellIdx = 0; cellIdx < rowNode.Children.Count; cellIdx++)
                    {
                        var cellNode = (TableCellBlock)rowNode.Children[cellIdx];
                        var cellBlock = new TableCellLayoutBlock(cellNode);
                        
                        // Layout cell contents inside cellWidth (single column layout inside cell)
                        float cellCurrentY = 0;
                        int cellGlobalOffset = 0; // relative to cell
                        foreach (var cellChild in cellNode.Children)
                        {
                            if (cellChild is ParagraphBlock cellPara)
                            {
                                var cellParaBlock = LayoutParagraph(cellPara, cellWidth - 10, ref cellGlobalOffset); // 5pt padding left/right
                                float paraHeight = 0;
                                foreach (var line in cellParaBlock.Lines)
                                {
                                    line.YOffset = cellCurrentY + paraHeight;
                                    paraHeight += line.Height;
                                }
                                cellParaBlock.Bounds = new SKRect(5, cellCurrentY, cellWidth - 5, cellCurrentY + paraHeight);
                                cellBlock.Blocks.Add(cellParaBlock);
                                cellCurrentY += paraHeight;
                            }
                        }

                        rowHeight = Math.Max(rowHeight, cellCurrentY + 10); // 5pt padding top/bottom
                        cellBlocks.Add(cellBlock);
                    }

                    // Check if row fits
                    float remainingHeight = context.MarginTop + context.ContentHeight - context.CurrentY;
                    if (rowHeight > remainingHeight && context.CurrentY > context.MarginTop)
                    {
                        // Doesn't fit, do column/page break
                        currentColumn++;
                        if (currentColumn >= ColumnCount)
                        {
                            currentColumn = 0;
                            context.CurrentPageIndex++;
                            pageOffsetTop = context.CurrentPageIndex * (context.PageHeight + 20f);
                            currentPage = new PageLayoutBlock(doc, context.CurrentPageIndex, new SKRect(0, pageOffsetTop, context.PageWidth, pageOffsetTop + context.PageHeight));
                            docBlock.Pages.Add(currentPage);
                        }
                        currentX = context.MarginLeft + currentColumn * (colWidth + ColumnGap);
                        context.CurrentY = context.MarginTop;
                        remainingHeight = context.MarginTop + context.ContentHeight - context.CurrentY;
                    }

                    // Place the row
                    var rowBlock = new TableRowLayoutBlock(rowNode)
                    {
                        Bounds = new SKRect(
                            currentPage.Bounds.Left + currentX,
                            pageOffsetTop + context.CurrentY,
                            currentPage.Bounds.Left + currentX + colWidth,
                            pageOffsetTop + context.CurrentY + rowHeight
                        )
                    };

                    float cellX = 0;
                    foreach (var cellBlock in cellBlocks)
                    {
                        cellBlock.Bounds = new SKRect(
                            rowBlock.Bounds.Left + cellX,
                            rowBlock.Bounds.Top,
                            rowBlock.Bounds.Left + cellX + cellWidth,
                            rowBlock.Bounds.Bottom
                        );

                        // Offset the blocks inside cell relative to the cell itself
                        foreach (var b in cellBlock.Blocks)
                        {
                            b.Bounds = new SKRect(
                                cellBlock.Bounds.Left + b.Bounds.Left,
                                cellBlock.Bounds.Top + 5 + b.Bounds.Top,
                                cellBlock.Bounds.Left + b.Bounds.Right,
                                cellBlock.Bounds.Top + 5 + b.Bounds.Bottom
                            );
                        }

                        rowBlock.Cells.Add(cellBlock);
                        cellX += cellWidth;
                    }

                    currentPage.Blocks.Add(rowBlock);
                    context.CurrentY += rowHeight;
                    rowIdx++;
                }
            }
        }

        docBlock.Bounds = new SKRect(0, 0, context.PageWidth, pageOffsetTop + context.PageHeight);
        return docBlock;
    }

    private static ParagraphLayoutBlock LayoutParagraph(ParagraphBlock para, float width, ref int globalOffset)
    {
        var paraBlock = new ParagraphLayoutBlock(para)
        {
            GlobalStartOffset = globalOffset
        };

        // Concatenate run texts and map ranges
        var textRuns = new List<TextRun>();
        var textBuilder = new System.Text.StringBuilder();
        
        // We will map inline index to our textruns
        var inlineImages = new Dictionary<int, ImageInline>();

        foreach (var inline in para.Children)
        {
            if (inline is TextRun run)
            {
                textRuns.Add(run);
                textBuilder.Append(run.Text);
            }
            else if (inline is ImageInline img)
            {
                // Create a text run spacer for the image
                var imgRun = new TextRun { Text = "   ", FontSize = 12 };
                textRuns.Add(imgRun);
                inlineImages[textBuilder.Length] = img;
                textBuilder.Append(imgRun.Text);
            }
            else if (inline is LineBreakInline)
            {
                textBuilder.Append("\n");
            }
        }

        string fullText = textBuilder.ToString();
        globalOffset += fullText.Length;

        if (fullText.Length == 0)
        {
            return paraBlock;
        }

        // We wrap using a default paint (from the first run, or Arial 12)
        var defaultRun = textRuns.Count > 0 ? textRuns[0] : new TextRun { FontSize = 12 };
        using var defaultPaint = GetPaint(defaultRun);

        float indent = para.IsBullet ? (para.BulletLevel * 15f + 15f) : 0f;
        var wrappedLines = TextWrappingEngine.WrapText(fullText, width - indent, defaultPaint);
        
        float currentY = 0;
        foreach (var wl in wrappedLines)
        {
            var line = new ParagraphLine
            {
                Text = fullText.Substring(wl.StartIndex, wl.Length),
                StartOffset = wl.StartIndex,
                Length = wl.Length,
                Width = wl.Width,
                Height = wl.Height,
                YOffset = currentY
            };

            // Slice line into styled segments
            int lineOffset = wl.StartIndex;
            int lineRemaining = wl.Length;
            float segX = indent;

            // Draw bullet character on the first line
            if (para.IsBullet && wl == wrappedLines[0])
            {
                string bulletChar = "•";
                if (para.BulletStyle == "decimal")
                {
                    bulletChar = "1.";
                }
                else if (para.BulletLevel == 1)
                {
                    bulletChar = "◦";
                }
                else if (para.BulletLevel >= 2)
                {
                    bulletChar = "▪";
                }

                line.Segments.Add(new LineSegment
                {
                    Text = bulletChar + " ",
                    Run = new TextRun { FontSize = defaultRun.FontSize },
                    XOffset = indent - 12f,
                    Width = 10f,
                    StartOffset = wl.StartIndex
                });
            }

            int runStartOffset = 0;

            foreach (var run in textRuns)
            {
                int runLen = run.Text.Length;
                int runEnd = runStartOffset + runLen;

                // Check intersection between run and line segment
                int start = Math.Max(lineOffset, runStartOffset);
                int end = Math.Min(lineOffset + lineRemaining, runEnd);

                if (start < end)
                {
                    string segText = fullText.Substring(start, end - start);
                    using var rPaint = GetPaint(run);
                    float segW = rPaint.MeasureText(segText);

                    var segment = new LineSegment
                    {
                        Text = segText,
                        Run = run,
                        XOffset = segX,
                        Width = segW,
                        StartOffset = start - wl.StartIndex + wl.StartIndex
                    };

                    // Check if this segment corresponds to an inline image
                    if (inlineImages.TryGetValue(start, out var img))
                    {
                        segment.Image = img;
                        segment.Width = 24f; // Give image a fixed layout width
                        segW = 24f;
                    }

                    line.Segments.Add(segment);
                    segX += segW;
                }

                runStartOffset += runLen;
            }

            paraBlock.Lines.Add(line);
            currentY += wl.Height;
        }

        return paraBlock;
    }

    public static SKPaint GetPaint(TextRun run)
    {
        var paint = new SKPaint
        {
            TextSize = run.FontSize.HasValue ? (float)run.FontSize.Value : 12f,
            IsAntialias = true
        };

        var weight = run.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = run.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

        paint.Typeface = SKTypeface.FromFamilyName("Arial", weight, SKFontStyleWidth.Normal, slant);

        if (!string.IsNullOrEmpty(run.Color) && SKColor.TryParse(run.Color, out var color))
        {
            paint.Color = color;
        }
        else
        {
            paint.Color = SKColors.Black;
        }

        return paint;
    }
}
