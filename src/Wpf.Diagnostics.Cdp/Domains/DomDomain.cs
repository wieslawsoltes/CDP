using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp.Domains;

public static class DomDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
                {
                    bool slim = @params["slim"]?.GetValue<bool>() ?? false;
                    session.UseSlimTree = slim;
                    return new JsonObject();
                }
            case "disable":
                session.UseSlimTree = false;
                return new JsonObject();

            case "getDocument":
                {
                    bool pierce = @params["pierce"]?.GetValue<bool>() ?? false;
                    session.UseLogicalTree = !pierce;
                    int depth = @params["depth"]?.GetValue<int>() ?? -1;
                    var rootNode = BuildDocumentNode(session, depth);
                    return new JsonObject { ["root"] = rootNode };
                }

            case "getAttributes":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var attributes = BuildAttributes(visual);
                    return new JsonObject { ["attributes"] = attributes };
                }

            case "describeNode":
                {
                    int? nodeId = @params["nodeId"]?.GetValue<int>();
                    int? backendNodeId = @params["backendNodeId"]?.GetValue<int>();
                    string? objectId = @params["objectId"]?.GetValue<string>();

                    Visual? targetVisual = null;
                    if (nodeId.HasValue)
                    {
                        targetVisual = session.NodeMap.GetVisual(nodeId.Value);
                    }
                    else if (backendNodeId.HasValue)
                    {
                        targetVisual = session.NodeMap.GetVisual(backendNodeId.Value);
                    }
                    else if (!string.IsNullOrEmpty(objectId))
                    {
                        var obj = session.GetObject(objectId);
                        if (obj is CdpRuntimeDocument)
                        {
                            var depthVal = @params["depth"]?.GetValue<int>() ?? 0;
                            var documentNode = BuildDocumentNode(session, depthVal);
                            return new JsonObject { ["node"] = documentNode };
                        }
                        else if (obj is CdpRuntimeWindow)
                        {
                            targetVisual = session.Window;
                        }
                        else
                        {
                            targetVisual = CdpSession.GetVisualFromObject(obj);
                        }
                    }

                    if (targetVisual == null)
                    {
                        throw new Exception("Node not found");
                    }

                    var depth = @params["depth"]?.GetValue<int>() ?? 0;
                    var node = BuildDomNode(targetVisual, session, 0, depth);
                    return new JsonObject { ["node"] = node };
                }

            case "resolveNode":
                {
                    int? nodeId = @params["nodeId"]?.GetValue<int>();
                    int? backendNodeId = @params["backendNodeId"]?.GetValue<int>();
                    string objectGroup = @params["objectGroup"]?.GetValue<string>() ?? "console";

                    Visual? targetVisual = null;
                    if (nodeId.HasValue)
                    {
                        targetVisual = session.NodeMap.GetVisual(nodeId.Value);
                    }
                    else if (backendNodeId.HasValue)
                    {
                        targetVisual = session.NodeMap.GetVisual(backendNodeId.Value);
                    }

                    if (targetVisual == null)
                    {
                        throw new Exception("Node not found");
                    }

                    var elementObj = new CdpRuntimeElement(targetVisual);
                    var runtimeObj = session.RegisterObject(elementObj, objectGroup);
                    return new JsonObject { ["object"] = runtimeObj };
                }

            case "querySelector":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    string selector = @params["selector"]?.GetValue<string>() ?? "";
                    var root = session.NodeMap.GetVisual(nodeId);
                    if (root == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }

                    var matched = SelectorEngine.QuerySelector(root, selector, session.UseLogicalTree);
                    int matchedNodeId = matched != null ? session.NodeMap.GetOrAdd(matched) : 0;
                    return new JsonObject { ["nodeId"] = matchedNodeId };
                }

            case "querySelectorAll":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    string selector = @params["selector"]?.GetValue<string>() ?? "";
                    var root = session.NodeMap.GetVisual(nodeId);
                    if (root == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }

                    var matched = SelectorEngine.QuerySelectorAll(root, selector, session.UseLogicalTree);
                    var nodeIds = new JsonArray();
                    foreach (var m in matched)
                    {
                        nodeIds.Add(session.NodeMap.GetOrAdd(m));
                    }
                    return new JsonObject { ["nodeIds"] = nodeIds };
                }

            case "requestChildNodes":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    int depth = @params["depth"]?.GetValue<int>() ?? 1;
                    _ = RequestChildNodesAsync(session, nodeId, depth);
                    return new JsonObject();
                }

            case "getBoxModel":
                {
                    int? nodeId = @params["nodeId"]?.GetValue<int>();
                    int? backendNodeId = @params["backendNodeId"]?.GetValue<int>();
                    string? objectId = @params["objectId"]?.GetValue<string>();

                    Visual? targetVisual = null;
                    if (nodeId.HasValue)
                    {
                        targetVisual = session.NodeMap.GetVisual(nodeId.Value);
                    }
                    else if (backendNodeId.HasValue)
                    {
                        targetVisual = session.NodeMap.GetVisual(backendNodeId.Value);
                    }
                    else if (!string.IsNullOrEmpty(objectId))
                    {
                        var obj = session.GetObject(objectId);
                        targetVisual = CdpSession.GetVisualFromObject(obj);
                    }

                    if (targetVisual == null)
                    {
                        throw new Exception("Node not found");
                    }

                    var model = BuildBoxModel(targetVisual, session);
                    return new JsonObject { ["model"] = model };
                }

            case "focus":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual is UIElement ui)
                    {
                        ui.Dispatcher.BeginInvoke(() => ui.Focus());
                    }
                    return new JsonObject();
                }

            case "getOuterHTML":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    string html = GetOuterHtml(visual, session);
                    return new JsonObject { ["outerHTML"] = html };
                }

            default:
                throw new Exception($"Method DOM.{action} is not implemented");
        }
    }

    private static JsonObject BuildDocumentNode(CdpSession session, int maxDepth)
    {
        var rootNode = new JsonObject
        {
            ["nodeId"] = 1,
            ["backendNodeId"] = 1,
            ["nodeType"] = 9, // Document Node
            ["nodeName"] = "#document",
            ["localName"] = "",
            ["nodeValue"] = "",
            ["childNodeCount"] = session.Window != null ? 1 : 0,
            ["documentURL"] = $"http://localhost:{CdpServer.Port}/",
            ["baseURL"] = $"http://localhost:{CdpServer.Port}/",
            ["xmlVersion"] = ""
        };

        if (session.Window != null)
        {
            var children = new JsonArray();
            children.Add(BuildDomNode(session.Window, session, 1, maxDepth));
            rootNode["children"] = children;
        }

        return rootNode;
    }

    public static JsonObject BuildDomNode(Visual visual, CdpSession session, int currentDepth, int maxDepth)
    {
        int nodeId = session.NodeMap.GetOrAdd(visual);
        var children = GetChildren(visual, session).ToList();
        var childrenJson = new JsonArray();

        bool recursive = maxDepth == -1 || currentDepth < maxDepth;
        if (recursive)
        {
            foreach (var child in children)
            {
                childrenJson.Add(BuildDomNode(child, session, currentDepth + 1, maxDepth));
            }
        }

        var attributes = BuildAttributes(visual);

        var node = new JsonObject
        {
            ["nodeId"] = nodeId,
            ["backendNodeId"] = nodeId,
            ["nodeType"] = 1, // Element Node
            ["nodeName"] = GetMappedTagName(visual),
            ["localName"] = GetLocalName(visual),
            ["nodeValue"] = "",
            ["childNodeCount"] = children.Count,
            ["attributes"] = attributes,
            ["frameId"] = session.Target?.Id ?? "main-frame-id"
        };

        if (childrenJson.Count > 0)
        {
            node["children"] = childrenJson;
        }

        return node;
    }

    private static async Task RequestChildNodesAsync(CdpSession session, int nodeId, int depth)
    {
        var visual = session.NodeMap.GetVisual(nodeId);
        if (visual == null) return;

        var nodesJson = new JsonArray();
        foreach (var child in GetChildren(visual, session))
        {
            nodesJson.Add(BuildDomNode(child, session, 1, depth));
        }

        var notification = new JsonObject
        {
            ["parentId"] = nodeId,
            ["nodes"] = nodesJson
        };

        await session.SendEventAsync("DOM.setChildNodes", notification);
    }

    public static string GetOuterHtml(Visual visual, CdpSession session)
    {
        var name = visual.GetType().Name;
        var sb = new StringBuilder();
        sb.Append("<").Append(name);

        if (visual is FrameworkElement ctrl)
        {
            if (!string.IsNullOrEmpty(ctrl.Name))
            {
                sb.Append(" id=\"").Append(ctrl.Name).Append("\"");
            }
            string? text = GetControlTextOrContent(ctrl);
            if (!string.IsNullOrEmpty(text))
            {
                sb.Append(" text=\"").Append(System.Web.HttpUtility.HtmlEncode(text)).Append("\"");
            }
        }

        var children = GetChildren(visual, session).ToList();
        if (children.Count > 0)
        {
            sb.Append(">");
            foreach (var child in children)
            {
                sb.Append(GetOuterHtml(child, session));
            }
            sb.Append("</").Append(name).Append(">");
        }
        else
        {
            string? txtContent = visual is FrameworkElement fe ? GetControlTextOrContent(fe) : null;
            if (!string.IsNullOrEmpty(txtContent))
            {
                sb.Append(">").Append(System.Web.HttpUtility.HtmlEncode(txtContent)).Append("</").Append(name).Append(">");
            }
            else
            {
                sb.Append(" />");
            }
        }

        return sb.ToString();
    }

    public static string? GetControlTextOrContent(FrameworkElement ctrl)
    {
        if (ctrl is TextBlock textBlock)
        {
            return textBlock.Text;
        }
        if (ctrl is TextBox textBox)
        {
            return textBox.Text;
        }
        if (ctrl is ContentControl contentControl && contentControl.Content is string contentStr)
        {
            return contentStr;
        }
        if (ctrl is HeaderedContentControl headeredControl && headeredControl.Header is string headerStr)
        {
            return headerStr;
        }
        if (ctrl is HeaderedItemsControl itemsControl && itemsControl.Header is string headerItemsStr)
        {
            return headerItemsStr;
        }
        if (ctrl is ContentPresenter contentPresenter && contentPresenter.Content is string contentPresenterStr)
        {
            return contentPresenterStr;
        }
        return null;
    }

    private static string GetLocalName(Visual visual)
    {
        return visual.GetType().Name.ToLowerInvariant();
    }

    private static string GetMappedTagName(Visual visual)
    {
        var mapped = visual.GetType().Name.ToUpperInvariant();
        if (mapped == "INPUT" || mapped == "BUTTON")
        {
            return mapped.ToLowerInvariant();
        }
        return mapped;
    }

    private static IEnumerable<Visual> GetChildren(Visual visual, CdpSession session)
    {
        return CdpVisualTreeHelper.GetChildren(visual, session.UseLogicalTree);
    }


    private static JsonArray BuildAttributes(Visual visual)
    {
        var attrs = new JsonArray();
        if (visual is FrameworkElement ctrl)
        {
            if (!string.IsNullOrEmpty(ctrl.Name))
            {
                attrs.Add("id");
                attrs.Add(ctrl.Name);
                attrs.Add("Name");
                attrs.Add(ctrl.Name);
            }

            var axId = AutomationProperties.GetAutomationId(ctrl);
            if (!string.IsNullOrEmpty(axId))
            {
                attrs.Add("AutomationId");
                attrs.Add(axId);
                attrs.Add("AccessibilityId");
                attrs.Add(axId);
            }

            string? txt = GetControlTextOrContent(ctrl);
            if (!string.IsNullOrEmpty(txt))
            {
                attrs.Add("text");
                attrs.Add(txt);
            }
        }
        return attrs;
    }

    private static JsonObject BuildBoxModel(Visual visual, CdpSession session)
    {
        double width = 0;
        double height = 0;
        Thickness margin = default;
        Thickness padding = default;
        Thickness borderThickness = default;

        if (visual is UIElement ui)
        {
            width = ui.RenderSize.Width;
            height = ui.RenderSize.Height;
        }

        if (visual is FrameworkElement fe)
        {
            margin = fe.Margin;
        }

        var propBorder = visual.GetType().GetProperty("BorderThickness");
        if (propBorder != null && propBorder.PropertyType == typeof(Thickness))
        {
            borderThickness = (Thickness)propBorder.GetValue(visual)!;
        }

        var propPadding = visual.GetType().GetProperty("Padding");
        if (propPadding != null && propPadding.PropertyType == typeof(Thickness))
        {
            padding = (Thickness)propPadding.GetValue(visual)!;
        }

        double x = 0;
        double y = 0;

        if (session.Window != null)
        {
            try
            {
                var pt = (visual as UIElement)?.TranslatePoint(new Point(0, 0), session.Window);
                if (pt.HasValue)
                {
                    x = pt.Value.X;
                    y = pt.Value.Y;
                }
            }
            catch { }
        }

        double borderL = borderThickness.Left;
        double borderT = borderThickness.Top;
        double borderR = borderThickness.Right;
        double borderB = borderThickness.Bottom;

        double padL = padding.Left;
        double padT = padding.Top;
        double padR = padding.Right;
        double padB = padding.Bottom;

        // Content Rect
        double cx1 = x + borderL + padL, cy1 = y + borderT + padT;
        double cx2 = cx1 + Math.Max(0, width - borderL - borderR - padL - padR), cy2 = cy1;
        double cx3 = cx2, cy3 = cy1 + Math.Max(0, height - borderT - borderB - padT - padB);
        double cx4 = cx1, cy4 = cy3;

        // Padding Rect
        double px1 = x + borderL, py1 = y + borderT;
        double px2 = px1 + Math.Max(0, width - borderL - borderR), py2 = py1;
        double px3 = px2, py3 = py1 + Math.Max(0, height - borderT - borderB);
        double px4 = px1, py4 = py3;

        // Border Rect
        double bx1 = x, by1 = y;
        double bx2 = x + width, by2 = y;
        double bx3 = x + width, by3 = y + height;
        double bx4 = x, by4 = y + height;

        // Margin Rect
        double mx1 = x - margin.Left, my1 = y - margin.Top;
        double mx2 = x + width + margin.Right, my2 = y - margin.Top;
        double mx3 = x + width + margin.Right, my3 = y + height + margin.Bottom;
        double mx4 = x - margin.Left, my4 = y + height + margin.Bottom;

        return new JsonObject
        {
            ["content"] = new JsonArray { cx1, cy1, cx2, cy2, cx3, cy3, cx4, cy4 },
            ["padding"] = new JsonArray { px1, py1, px2, py2, px3, py3, px4, py4 },
            ["border"] = new JsonArray { bx1, by1, bx2, by2, bx3, by3, bx4, by4 },
            ["margin"] = new JsonArray { mx1, my1, mx2, my2, mx3, my3, mx4, my4 },
            ["width"] = width,
            ["height"] = height
        };
    }
}
