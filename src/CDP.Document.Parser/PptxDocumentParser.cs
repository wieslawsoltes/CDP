using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Drawing;
using CDP.Document.Parser.AST;

using Shape = DocumentFormat.OpenXml.Presentation.Shape;
using GroupShape = DocumentFormat.OpenXml.Presentation.GroupShape;
using Picture = DocumentFormat.OpenXml.Presentation.Picture;
using GraphicFrame = DocumentFormat.OpenXml.Presentation.GraphicFrame;
using ConnectionShape = DocumentFormat.OpenXml.Presentation.ConnectionShape;

namespace CDP.Document.Parser;

public class PptxDocumentParser : IDocumentParser
{
    public Task<DocumentRoot> ParseAsync(Stream stream)
    {
        return Task.FromResult(Parse(stream));
    }

    public DocumentRoot Parse(Stream stream)
    {
        var docRoot = new CDP.Document.Parser.AST.PresentationDocument();
        using var pptDoc = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(stream, false);
        var presentationPart = pptDoc.PresentationPart;
        if (presentationPart == null || presentationPart.Presentation == null) return docRoot;

        // Parse slide masters
        foreach (var masterPart in presentationPart.SlideMasterParts)
        {
            var masterNode = new SlideMasterNode
            {
                Name = masterPart.Uri.ToString()
            };
            docRoot.Masters.Add(masterNode);
            
            var shapeTree = masterPart.SlideMaster.CommonSlideData?.ShapeTree;
            if (shapeTree != null)
            {
                ParseShapeTreeElements(shapeTree.ChildElements, masterNode, masterPart);
            }
            
            var bg = masterPart.SlideMaster.CommonSlideData?.Background;
            if (bg != null)
            {
                var color = bg.Descendants<DocumentFormat.OpenXml.Drawing.RgbColorModelHex>().FirstOrDefault();
                if (color?.Val != null)
                {
                    masterNode.BackgroundColor = "#" + color.Val.Value;
                }
            }
        }

        var slideIdList = presentationPart.Presentation.SlideIdList;
        if (slideIdList == null) return docRoot;

        int slideIndex = 0;
        foreach (var slideIdObj in slideIdList.Cast<SlideId>())
        {
            if (slideIdObj.RelationshipId?.Value == null) continue;
            var slidePart = presentationPart.GetPartById(slideIdObj.RelationshipId.Value) as SlidePart;
            if (slidePart == null || slidePart.Slide == null) continue;

            var slideNode = new SlideNode
            {
                SlideIndex = slideIndex++
            };
            docRoot.AddChild(slideNode);

            slideNode.Title = GetSlideTitle(slidePart);
            
            var layoutPart = slidePart.SlideLayoutPart;
            if (layoutPart != null)
            {
                slideNode.MasterName = layoutPart.SlideLayout?.CommonSlideData?.Name?.Value ?? layoutPart.Uri.ToString();
            }

            var shapeTree = slidePart.Slide.CommonSlideData?.ShapeTree;
            if (shapeTree != null)
            {
                ParseShapeTreeElements(shapeTree.ChildElements, slideNode, slidePart);
            }
        }

        return docRoot;
    }

    private static string? GetSlideTitle(SlidePart slidePart)
    {
        if (slidePart.Slide == null) return null;
        var shapeTree = slidePart.Slide.CommonSlideData?.ShapeTree;
        if (shapeTree == null) return null;

        foreach (var shape in shapeTree.Descendants<Shape>())
        {
            var ph = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
            if (ph != null && ph.Type != null &&
                (ph.Type.Value == PlaceholderValues.Title || ph.Type.Value == PlaceholderValues.CenteredTitle))
            {
                return shape.TextBody?.InnerText;
            }
        }
        return null;
    }

    private void ParseShapeTreeElements(System.Collections.Generic.IEnumerable<OpenXmlElement> elements, DocumentNode parentASTNode, OpenXmlPart part)
    {
        foreach (var element in elements)
        {
            if (element is GroupShape grpSp)
            {
                var groupNode = new GroupNode();
                ApplyGroupTransform(grpSp.GroupShapeProperties?.TransformGroup, groupNode);
                parentASTNode.AddChild(groupNode);

                ParseShapeTreeElements(grpSp.ChildElements, groupNode, part);
            }
            else if (element is Shape sp)
            {
                var presetGeom = sp.ShapeProperties?.GetFirstChild<DocumentFormat.OpenXml.Drawing.PresetGeometry>();
                string shapeType = "Custom";
                if (presetGeom != null)
                {
                    var prstAttr = presetGeom.GetAttribute("prst", string.Empty);
                    if (prstAttr.Value != null)
                    {
                        string rawVal = prstAttr.Value;
                        if (rawVal.Equals("rect", StringComparison.OrdinalIgnoreCase))
                        {
                            shapeType = "Rectangle";
                        }
                        else if (!string.IsNullOrEmpty(rawVal))
                        {
                            shapeType = char.ToUpper(rawVal[0]) + rawVal.Substring(1);
                        }
                    }
                    else if (presetGeom.Preset != null && presetGeom.Preset.HasValue)
                    {
                        shapeType = presetGeom.Preset.Value.ToString();
                    }
                }

                var shapeNode = new ShapeNode
                {
                    ShapeType = shapeType,
                    Text = sp.TextBody?.InnerText
                };
                ApplyTransform(sp.ShapeProperties?.Transform2D, shapeNode);
                parentASTNode.AddChild(shapeNode);
            }
            else if (element is Picture pic)
            {
                var shapeNode = new ShapeNode
                {
                    ShapeType = "Picture",
                    Text = pic.NonVisualPictureProperties?.NonVisualDrawingProperties?.Description ?? pic.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name
                };

                var embedId = pic.BlipFill?.Blip?.Embed?.Value;
                if (embedId != null)
                {
                    try
                    {
                        var imgPart = part.GetPartById(embedId);
                        if (imgPart is ImagePart imagePart)
                        {
                            using var imgStream = imagePart.GetStream();
                            using var ms = new MemoryStream();
                            imgStream.CopyTo(ms);
                            var base64 = Convert.ToBase64String(ms.ToArray());
                            shapeNode.ImageSource = $"data:{imagePart.ContentType};base64,{base64}";
                        }
                    }
                    catch { }
                }

                ApplyTransform(pic.ShapeProperties?.Transform2D, shapeNode);
                parentASTNode.AddChild(shapeNode);
            }
            else if (element is GraphicFrame gf)
            {
                var shapeNode = new ShapeNode
                {
                    ShapeType = "GraphicFrame"
                };
                ApplyTransform(gf.Transform, shapeNode);
                parentASTNode.AddChild(shapeNode);
            }
            else if (element is ConnectionShape cs)
            {
                var shapeNode = new ShapeNode
                {
                    ShapeType = "ConnectionShape"
                };
                ApplyTransform(cs.ShapeProperties?.Transform2D, shapeNode);
                parentASTNode.AddChild(shapeNode);
            }
        }
    }

    private static void ApplyTransform(DocumentFormat.OpenXml.Drawing.Transform2D? xfrm, ShapeNode node)
    {
        if (xfrm == null) return;

        if (xfrm.Offset?.X?.Value != null)
        {
            node.X = xfrm.Offset.X.Value / 12700.0;
        }
        if (xfrm.Offset?.Y?.Value != null)
        {
            node.Y = xfrm.Offset.Y.Value / 12700.0;
        }
        if (xfrm.Extents?.Cx?.Value != null)
        {
            node.Width = xfrm.Extents.Cx.Value / 12700.0;
        }
        if (xfrm.Extents?.Cy?.Value != null)
        {
            node.Height = xfrm.Extents.Cy.Value / 12700.0;
        }
    }

    private static void ApplyTransform(DocumentFormat.OpenXml.Presentation.Transform? xfrm, ShapeNode node)
    {
        if (xfrm == null) return;

        if (xfrm.Offset?.X?.Value != null)
        {
            node.X = xfrm.Offset.X.Value / 12700.0;
        }
        if (xfrm.Offset?.Y?.Value != null)
        {
            node.Y = xfrm.Offset.Y.Value / 12700.0;
        }
        if (xfrm.Extents?.Cx?.Value != null)
        {
            node.Width = xfrm.Extents.Cx.Value / 12700.0;
        }
        if (xfrm.Extents?.Cy?.Value != null)
        {
            node.Height = xfrm.Extents.Cy.Value / 12700.0;
        }
    }

    private static void ApplyGroupTransform(DocumentFormat.OpenXml.Drawing.TransformGroup? xfrm, GroupNode node)
    {
        if (xfrm == null) return;

        if (xfrm.Offset?.X?.Value != null)
        {
            node.X = xfrm.Offset.X.Value / 12700.0;
        }
        if (xfrm.Offset?.Y?.Value != null)
        {
            node.Y = xfrm.Offset.Y.Value / 12700.0;
        }
        if (xfrm.Extents?.Cx?.Value != null)
        {
            node.Width = xfrm.Extents.Cx.Value / 12700.0;
        }
        if (xfrm.Extents?.Cy?.Value != null)
        {
            node.Height = xfrm.Extents.Cy.Value / 12700.0;
        }
    }
}
