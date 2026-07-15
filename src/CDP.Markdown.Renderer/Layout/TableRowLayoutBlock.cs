namespace CDP.Markdown.Renderer.Layout;

using System;
using System.Collections.Generic;
using SkiaSharp;
using CDP.Markdown.Parser.AST;
using CDP.Markdown.Renderer.Rendering;

public class TableRowLayoutBlock : ILayoutBlock
{
    public TableRowBlock Node { get; }
    MarkdownBlock ILayoutBlock.Node => Node;
    public SKRect Bounds { get; private set; }
    public List<TableCellLayoutBlock> Cells { get; } = new();

    public TableRowLayoutBlock(TableRowBlock node)
    {
        Node = node;
        foreach (var child in node.Children)
        {
            if (child is TableCellBlock cell)
            {
                Cells.Add(new TableCellLayoutBlock(cell));
            }
        }
    }

    public void Layout(LayoutContext context)
    {
        float startY = context.CurrentY;
        float cellWidth = Cells.Count > 0 ? context.MaxWidth / Cells.Count : context.MaxWidth;
        float maxHeight = 0;

        float currentX = 0;
        for (int i = 0; i < Cells.Count; i++)
        {
            var cell = Cells[i];
            var cellContext = new LayoutContext(cellWidth, context.Measurer, context.Resources, startY, context.MarkdownText, currentX);
            cell.Layout(cellContext);
            float height = cell.Bounds.Height;
            if (height > maxHeight) maxHeight = height;
            currentX += cellWidth;
        }

        Bounds = new SKRect(0, startY, context.MaxWidth, startY + maxHeight);
        context.CurrentY = startY + maxHeight;
    }

    public void Render(SKCanvas canvas, RenderContext context)
    {
        if (Node.IsHeader)
        {
            canvas.DrawRect(Bounds, context.Resources.CodeBackgroundPaint);
        }

        foreach (var cell in Cells)
        {
            cell.Render(canvas, context);
        }
    }

    public int HitTest(SKPoint docPoint)
    {
        if (Cells.Count == 0) return Node.Span.Start;
        float x = docPoint.X;

        if (x < Cells[0].Bounds.Left)
        {
            return Cells[0].HitTest(docPoint);
        }

        foreach (var cell in Cells)
        {
            if (x >= cell.Bounds.Left && x < cell.Bounds.Right)
            {
                return cell.HitTest(docPoint);
            }
        }
        return Cells[^1].HitTest(docPoint);
    }

    public SKRect GetCaretBounds(int offset)
    {
        foreach (var cell in Cells)
        {
            int start = cell.Node.Span.Start;
            int end = cell.Node.Span.Start + cell.Node.Span.Length;
            if (offset >= start && offset <= end)
            {
                return cell.GetCaretBounds(offset);
            }
        }
        if (Cells.Count > 0)
        {
            return Cells[^1].GetCaretBounds(offset);
        }
        return new SKRect(Bounds.Left, Bounds.Top, Bounds.Left + 2f, Bounds.Bottom);
    }

    public void GetSelectionBounds(int start, int end, IList<SKRect> result)
    {
        foreach (var cell in Cells)
        {
            cell.GetSelectionBounds(start, end, result);
        }
    }

    public void Dispose()
    {
        foreach (var cell in Cells)
        {
            cell.Dispose();
        }
        Cells.Clear();
    }
}
