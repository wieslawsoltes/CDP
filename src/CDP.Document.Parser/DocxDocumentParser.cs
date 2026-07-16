using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using CDP.Document.Parser.AST;

using WParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using WTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using WTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using WRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WBreak = DocumentFormat.OpenXml.Wordprocessing.Break;
using WText = DocumentFormat.OpenXml.Wordprocessing.Text;
using WDrawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;

namespace CDP.Document.Parser;

public class DocxDocumentParser : IDocumentParser
{
    public Task<DocumentRoot> ParseAsync(Stream stream)
    {
        return Task.FromResult(Parse(stream));
    }

    public DocumentRoot Parse(Stream stream)
    {
        var docRoot = new WordDocument();
        using var wordDoc = WordprocessingDocument.Open(stream, false);
        
        // Extract Header
        var headerParts = wordDoc.MainDocumentPart?.HeaderParts;
        if (headerParts != null)
        {
            var headerTexts = headerParts.Select(hp => hp.Header?.InnerText).Where(t => !string.IsNullOrEmpty(t));
            docRoot.Header = string.Join("\n", headerTexts);
        }
        
        // Extract Footer
        var footerParts = wordDoc.MainDocumentPart?.FooterParts;
        if (footerParts != null)
        {
            var footerTexts = footerParts.Select(fp => fp.Footer?.InnerText).Where(t => !string.IsNullOrEmpty(t));
            docRoot.Footer = string.Join("\n", footerTexts);
        }

        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body != null)
        {
            ParseContainer(body, docRoot, wordDoc.MainDocumentPart);
        }
        return docRoot;
    }

    private void ParseContainer(DocumentFormat.OpenXml.OpenXmlElement container, DocumentNode parentASTNode, MainDocumentPart? mainPart)
    {
        foreach (var element in container.ChildElements)
        {
            if (element is WParagraph wPara)
            {
                var paraBlock = new ParagraphBlock();
                var numPr = wPara.ParagraphProperties?.NumberingProperties;
                if (numPr != null)
                {
                    var ilvl = numPr.GetFirstChild<NumberingLevelReference>();
                    var numId = numPr.GetFirstChild<NumberingId>();
                    paraBlock.IsBullet = true;
                    paraBlock.BulletLevel = ilvl?.Val?.Value ?? 0;
                    paraBlock.BulletStyle = numId?.Val?.Value.ToString() ?? "bullet";
                }
                parentASTNode.AddChild(paraBlock);
                ParseParagraphChildren(wPara, paraBlock, mainPart);
            }
            else if (element is WTable wTable)
            {
                var tableBlock = new TableBlock();
                parentASTNode.AddChild(tableBlock);
                foreach (var rowElement in wTable.ChildElements.OfType<WTableRow>())
                {
                    var rowBlock = new TableRowBlock();
                    tableBlock.AddChild(rowBlock);
                    foreach (var cellElement in rowElement.ChildElements.OfType<WTableCell>())
                    {
                        var cellBlock = new TableCellBlock();
                        rowBlock.AddChild(cellBlock);
                        ParseContainer(cellElement, cellBlock, mainPart);
                    }
                }
            }
            else if (element is SectionProperties sectPr)
            {
                var sectBlock = new SectionBlock();
                parentASTNode.AddChild(sectBlock);
            }
        }
    }

    private void ParseParagraphChildren(WParagraph wPara, ParagraphBlock paraBlock, MainDocumentPart? mainPart)
    {
        foreach (var child in wPara.ChildElements)
        {
            if (child is WRun wRun)
            {
                // A run can contain text runs, line breaks, or drawings
                foreach (var runChild in wRun.ChildElements)
                {
                    if (runChild is WText wText)
                    {
                        var textRun = new TextRun
                        {
                            Text = wText.Text
                        };
                        ApplyRunProperties(wRun.RunProperties, textRun);
                        paraBlock.AddChild(textRun);
                    }
                    else if (runChild is WBreak)
                    {
                        paraBlock.AddChild(new LineBreakInline());
                    }
                    else if (runChild is WDrawing wDrawing)
                    {
                        ParseDrawing(wDrawing, paraBlock, mainPart);
                    }
                }
            }
            else if (child is WBreak)
            {
                paraBlock.AddChild(new LineBreakInline());
            }
            else if (child is WDrawing wDrawing)
            {
                ParseDrawing(wDrawing, paraBlock, mainPart);
            }
        }
    }

    private void ParseDrawing(WDrawing wDrawing, DocumentNode parent, MainDocumentPart? mainPart)
    {
        var imageInline = new ImageInline();
        var docProperties = wDrawing.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().FirstOrDefault();
        if (docProperties != null)
        {
            imageInline.AltText = docProperties.Description ?? docProperties.Name;
        }
        var extent = wDrawing.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().FirstOrDefault();
        if (extent != null && extent.Cx != null && extent.Cy != null)
        {
            imageInline.Width = extent.Cx.Value / 12700.0;
            imageInline.Height = extent.Cy.Value / 12700.0;
        }
        var blip = wDrawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
        if (blip != null && blip.Embed != null && blip.Embed.Value != null)
        {
            if (mainPart != null)
            {
                try
                {
                    var part = mainPart.GetPartById(blip.Embed.Value);
                    if (part is ImagePart imagePart)
                    {
                        using var imgStream = imagePart.GetStream();
                        using var ms = new MemoryStream();
                        imgStream.CopyTo(ms);
                        var base64 = Convert.ToBase64String(ms.ToArray());
                        imageInline.Source = $"data:{imagePart.ContentType};base64,{base64}";
                    }
                    else
                    {
                        imageInline.Source = blip.Embed.Value;
                    }
                }
                catch
                {
                    imageInline.Source = blip.Embed.Value;
                }
            }
            else
            {
                imageInline.Source = blip.Embed.Value;
            }
        }
        parent.AddChild(imageInline);
    }

    private void ApplyRunProperties(RunProperties? rPr, TextRun textRun)
    {
        if (rPr == null) return;

        // Bold
        if (rPr.Bold != null)
        {
            var val = rPr.Bold.Val;
            textRun.Bold = val == null || val.Value;
        }

        // Italic
        if (rPr.Italic != null)
        {
            var val = rPr.Italic.Val;
            textRun.Italic = val == null || val.Value;
        }

        // Underline
        if (rPr.Underline != null)
        {
            textRun.Underline = true;
        }

        // Font Size
        if (rPr.FontSize?.Val?.Value != null)
        {
            if (double.TryParse(rPr.FontSize.Val.Value, out double sizeHalfPoints))
            {
                textRun.FontSize = sizeHalfPoints / 2.0;
            }
        }

        // Color
        if (rPr.Color?.Val?.Value != null)
        {
            textRun.Color = rPr.Color.Val.Value;
        }
    }
}
