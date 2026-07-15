using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using CDP.Document.Parser.AST;

namespace CDP.Document.Parser;

public class XlsxDocumentParser : IDocumentParser
{
    public Task<DocumentRoot> ParseAsync(Stream stream)
    {
        return Task.FromResult(Parse(stream));
    }

    public DocumentRoot Parse(Stream stream)
    {
        var spreadsheetAST = new CDP.Document.Parser.AST.SpreadsheetDocument();
        using var spreadsheetDoc = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(stream, false);
        var workbookPart = spreadsheetDoc.WorkbookPart;
        if (workbookPart == null || workbookPart.Workbook == null) return spreadsheetAST;

        var sheets = workbookPart.Workbook.Sheets;
        if (sheets == null) return spreadsheetAST;

        var stylesPart = workbookPart.WorkbookStylesPart;

        // Cache SharedStringTable once per workbook to allow O(1) lookups
        string[]? sharedStrings = null;
        var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;
        if (sharedStringTable != null)
        {
            sharedStrings = sharedStringTable.Elements<SharedStringItem>()
                .Select(item => item?.InnerText ?? string.Empty)
                .ToArray();
        }

        // Cache Fonts and CellFormats element lists once per workbook
        System.Collections.Generic.List<CellFormat>? cellFormatList = null;
        System.Collections.Generic.List<Font>? fontList = null;
        if (stylesPart?.Stylesheet != null)
        {
            if (stylesPart.Stylesheet.CellFormats != null)
            {
                cellFormatList = stylesPart.Stylesheet.CellFormats.Elements<CellFormat>().ToList();
            }
            if (stylesPart.Stylesheet.Fonts != null)
            {
                fontList = stylesPart.Stylesheet.Fonts.Elements<Font>().ToList();
            }
        }

        foreach (var sheetObj in sheets.Cast<Sheet>())
        {
            if (sheetObj.Id?.Value == null) continue;

            var worksheetPart = workbookPart.GetPartById(sheetObj.Id.Value) as WorksheetPart;
            if (worksheetPart == null || worksheetPart.Worksheet == null) continue;

            var worksheetNode = new WorksheetNode
            {
                Name = sheetObj.Name?.Value ?? string.Empty
            };
            spreadsheetAST.AddChild(worksheetNode);

            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().FirstOrDefault();
            if (sheetData != null)
            {
                foreach (var rowElement in sheetData.Elements<Row>())
                {
                    var gridRowNode = new GridRowNode
                    {
                        RowIndex = rowElement.RowIndex != null ? (int)rowElement.RowIndex.Value - 1 : 0
                    };
                    worksheetNode.AddChild(gridRowNode);

                    foreach (var cellElement in rowElement.Elements<Cell>())
                    {
                        var colRef = cellElement.CellReference?.Value ?? string.Empty;
                        var colIndex = GetColumnIndex(colRef);

                        var cellNode = new GridCellNode
                        {
                            ColumnIndex = colIndex,
                            Formula = cellElement.CellFormula?.Text,
                            DisplayText = GetCellValue(cellElement, sharedStrings)
                        };
                        cellNode.Value = cellNode.DisplayText;

                        ApplyCellStyles(cellElement, cellFormatList, fontList, cellNode);

                        gridRowNode.AddChild(cellNode);
                    }
                }
            }

            // Extract Merged Cells
            var mergeCells = worksheetPart.Worksheet.Elements<MergeCells>().FirstOrDefault();
            if (mergeCells != null)
            {
                foreach (var mCell in mergeCells.Elements<MergeCell>())
                {
                    if (mCell.Reference?.Value != null)
                    {
                        worksheetNode.MergedCellRanges.Add(mCell.Reference.Value);
                    }
                }
                ProcessMergedCells(worksheetNode);
            }

            // Extract Drawings/Images
            var drawingsPart = worksheetPart.DrawingsPart;
            if (drawingsPart != null && drawingsPart.WorksheetDrawing != null)
            {
                foreach (var pic in drawingsPart.WorksheetDrawing.Descendants<DocumentFormat.OpenXml.Drawing.Spreadsheet.Picture>())
                {
                    var blip = pic.BlipFill?.Blip;
                    if (blip != null && blip.Embed != null && blip.Embed.Value != null)
                    {
                        try
                        {
                            var imgPart = drawingsPart.GetPartById(blip.Embed.Value);
                            if (imgPart is ImagePart imagePart)
                            {
                                using var imgStream = imagePart.GetStream();
                                using var ms = new MemoryStream();
                                imgStream.CopyTo(ms);
                                var base64 = Convert.ToBase64String(ms.ToArray());
                                
                                var imageNode = new ImageInline
                                {
                                    Source = $"data:{imagePart.ContentType};base64,{base64}",
                                    AltText = pic.NonVisualPictureProperties?.NonVisualDrawingProperties?.Description ?? pic.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name
                                };
                                worksheetNode.AddChild(imageNode);
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        return spreadsheetAST;
    }

    private static void ProcessMergedCells(WorksheetNode worksheetNode)
    {
        foreach (var range in worksheetNode.MergedCellRanges)
        {
            var parts = range.Split(':');
            if (parts.Length != 2) continue;
            string startRef = parts[0];
            string endRef = parts[1];
            
            int startCol = GetColumnIndex(startRef);
            int startRow = GetRowIndex(startRef);
            int endCol = GetColumnIndex(endRef);
            int endRow = GetRowIndex(endRef);
            
            var primaryCell = FindCell(worksheetNode, startCol, startRow);
            if (primaryCell != null)
            {
                primaryCell.RowSpan = endRow - startRow + 1;
                primaryCell.ColumnSpan = endCol - startCol + 1;
                primaryCell.IsMerged = true;
            }
            
            for (int r = startRow; r <= endRow; r++)
            {
                for (int c = startCol; c <= endCol; c++)
                {
                    if (r == startRow && c == startCol) continue;
                    var cell = FindCell(worksheetNode, c, r);
                    if (cell != null)
                    {
                        cell.IsMerged = true;
                    }
                }
            }
        }
    }

    private static int GetRowIndex(string cellRef)
    {
        string numPart = new string(cellRef.Where(char.IsDigit).ToArray());
        if (int.TryParse(numPart, out int r)) return r - 1;
        return 0;
    }

    private static GridCellNode? FindCell(WorksheetNode ws, int col, int row)
    {
        foreach (var child in ws.Children)
        {
            if (child is GridRowNode rNode && rNode.RowIndex == row)
            {
                foreach (var cellChild in rNode.Children)
                {
                    if (cellChild is GridCellNode cNode && cNode.ColumnIndex == col)
                    {
                        return cNode;
                    }
                }
            }
        }
        return null;
    }

    private static int GetColumnIndex(string cellReference)
    {
        if (string.IsNullOrEmpty(cellReference)) return 0;
        int index = 0;
        foreach (char c in cellReference)
        {
            if (char.IsLetter(c))
            {
                index = index * 26 + (char.ToUpper(c) - 'A' + 1);
            }
            else
            {
                break;
            }
        }
        return index - 1;
    }

    private static string GetCellValue(Cell cell, string[]? sharedStrings)
    {
        if (cell.DataType != null && cell.DataType.Value == CellValues.InlineString)
        {
            return cell.InlineString?.Text?.Text ?? cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty;
        }

        var val = cell.CellValue?.Text;
        if (val == null) return string.Empty;

        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            if (sharedStrings != null && int.TryParse(val, out int sharedIndex))
            {
                if (sharedIndex >= 0 && sharedIndex < sharedStrings.Length)
                {
                    return sharedStrings[sharedIndex];
                }
            }
        }
        return val;
    }

    private static void ApplyCellStyles(Cell cell, System.Collections.Generic.List<CellFormat>? cellFormatList, System.Collections.Generic.List<Font>? fontList, GridCellNode cellNode)
    {
        if (cell.StyleIndex == null || cellFormatList == null || fontList == null) return;

        if (cell.StyleIndex.Value >= cellFormatList.Count) return;

        var cellFormat = cellFormatList[(int)cell.StyleIndex.Value];
        if (cellFormat == null || cellFormat.FontId == null) return;

        if (cellFormat.FontId.Value >= fontList.Count) return;

        var font = fontList[(int)cellFormat.FontId.Value];
        if (font == null) return;

        cellNode.Bold = font.Bold != null && (font.Bold.Val == null || font.Bold.Val.Value);
        cellNode.Italic = font.Italic != null && (font.Italic.Val == null || font.Italic.Val.Value);
        
        if (font.FontSize?.Val?.Value != null)
        {
            cellNode.FontSize = font.FontSize.Val.Value;
        }

        if (font.Color?.Rgb?.Value != null)
        {
            cellNode.Color = font.Color.Rgb.Value;
        }
    }
}
