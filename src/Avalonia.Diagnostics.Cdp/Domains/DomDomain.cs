using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Avalonia.LogicalTree;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class DomDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
                session.StartObservingVisualTree();
                return new JsonObject();
            case "disable":
                session.StopObservingVisualTree();
                return new JsonObject();

            case "getDocument":
                {
                    bool pierce = @params["pierce"]?.GetValue<bool>() ?? true;
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
                        if (obj is Visual v)
                        {
                            targetVisual = v;
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

            case "requestChildNodes":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    int depth = @params["depth"]?.GetValue<int>() ?? 1;
                    await RequestChildNodesAsync(session, nodeId, depth);
                    return new JsonObject();
                }

            case "querySelector":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    string selector = @params["selector"]?.GetValue<string>() ?? "";
                    var root = nodeId == 1 ? session.Window : session.NodeMap.GetVisual(nodeId);
                    if (root == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var match = SelectorEngine.QuerySelector(root, selector, session.UseLogicalTree);
                    int matchId = match != null ? session.NodeMap.GetOrAdd(match) : 0;
                    return new JsonObject { ["nodeId"] = matchId };
                }

            case "querySelectorAll":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    string selector = @params["selector"]?.GetValue<string>() ?? "";
                    var root = nodeId == 1 ? session.Window : session.NodeMap.GetVisual(nodeId);
                    if (root == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var matches = SelectorEngine.QuerySelectorAll(root, selector, session.UseLogicalTree);
                    var nodeIds = new JsonArray();
                    foreach (var match in matches)
                    {
                        nodeIds.Add(session.NodeMap.GetOrAdd(match));
                    }
                    return new JsonObject { ["nodeIds"] = nodeIds };
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

            case "resolveNode":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var objectId = session.RegisterObject(visual);
                    var remoteObj = new JsonObject
                    {
                        ["type"] = "object",
                        ["subtype"] = "node",
                        ["className"] = visual.GetType().FullName,
                        ["description"] = $"{visual.GetType().Name} (ID={nodeId})",
                        ["objectId"] = objectId
                    };
                    return new JsonObject { ["object"] = remoteObj };
                }

            case "getBoxModel":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual == null)
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }
                    var model = GetBoxModel(session, visual);
                    return new JsonObject { ["model"] = model };
                }

            case "getNodeForLocation":
                {
                    int x = @params["x"]?.GetValue<int>() ?? 0;
                    int y = @params["y"]?.GetValue<int>() ?? 0;
                    var hit = session.Window.InputHitTest(new Point(x, y)) as Visual;
                    int hitId = hit != null ? session.NodeMap.GetOrAdd(hit) : 0;
                    return new JsonObject { ["nodeId"] = hitId };
                }

            case "focus":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual is Avalonia.Input.IInputElement inputElement)
                    {
                        inputElement.Focus();
                    }
                    return new JsonObject();
                }

            case "setInspectedNode":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    session.InspectedNodeId = nodeId;
                    return new JsonObject();
                }

            case "setAttributeValue":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    string name = @params["name"]?.GetValue<string>() ?? "";
                    string value = @params["value"]?.GetValue<string>() ?? "";
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual is Control control)
                    {
                        ApplyAttributeValue(control, name, value);
                    }
                    return new JsonObject();
                }

            case "removeAttribute":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    string name = @params["name"]?.GetValue<string>() ?? "";
                    var visual = session.NodeMap.GetVisual(nodeId);
                    if (visual is Control control)
                    {
                        if (name.Equals("class", StringComparison.OrdinalIgnoreCase))
                        {
                            control.Classes.Clear();
                        }
                        else if (name.Equals("name", StringComparison.OrdinalIgnoreCase) || name.Equals("id", StringComparison.OrdinalIgnoreCase))
                        {
                            control.Name = null;
                        }
                    }
                    return new JsonObject();
                }
 
            case "removeNode":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var visual = session.NodeMap.GetVisual(nodeId);
                        if (visual is Control control)
                        {
                            var parent = control.Parent;
                            if (parent is Panel panel)
                            {
                                panel.Children.Remove(control);
                            }
                            else if (parent is ContentControl contentControl)
                            {
                                if (contentControl.Content == control) contentControl.Content = null;
                            }
                            else if (parent is Decorator decorator)
                            {
                                if (decorator.Child == control) decorator.Child = null;
                            }
                            else if (control.Parent == null && visual.GetVisualParent() is Panel visualPanel)
                            {
                                visualPanel.Children.Remove(control);
                            }
                        }
                    });
                    return new JsonObject();
                }

            case "performSearch":
                {
                    string query = @params["query"]?.GetValue<string>() ?? "";
                    var results = new List<int>();

                    try
                    {
                        var matches = SelectorEngine.QuerySelectorAll(session.Window, query, session.UseLogicalTree);
                        foreach (var match in matches)
                        {
                            results.Add(session.NodeMap.GetOrAdd(match));
                        }
                    }
                    catch { }

                    if (results.Count == 0)
                    {
                        SearchVisualTree(session.Window, query, session, results);
                    }

                    string searchId = Guid.NewGuid().ToString();
                    _searchResults[searchId] = results;

                    return new JsonObject
                    {
                        ["searchId"] = searchId,
                        ["resultCount"] = results.Count
                    };
                }

            case "getSearchResults":
                {
                    string searchId = @params["searchId"]?.GetValue<string>() ?? "";
                    int fromIndex = @params["fromIndex"]?.GetValue<int>() ?? 0;
                    int toIndex = @params["toIndex"]?.GetValue<int>() ?? 0;

                    var nodeIds = new JsonArray();
                    if (_searchResults.TryGetValue(searchId, out var results))
                    {
                        for (int i = fromIndex; i < Math.Min(toIndex, results.Count); i++)
                        {
                            nodeIds.Add(results[i]);
                        }
                    }

                    return new JsonObject { ["nodeIds"] = nodeIds };
                }

            case "discardSearchResults":
                {
                    string searchId = @params["searchId"]?.GetValue<string>() ?? "";
                    _searchResults.TryRemove(searchId, out _);
                    return new JsonObject();
                }

            case "getFlattenedDocument":
                {
                    bool pierce = @params["pierce"]?.GetValue<bool>() ?? true;
                    session.UseLogicalTree = !pierce;
                    int depth = @params["depth"]?.GetValue<int>() ?? -1;
                    var flatList = new List<JsonObject>();

                    var documentChildrenIds = new JsonArray { session.NodeMap.GetOrAdd(session.Window) };
                    var docNode = new JsonObject
                    {
                        ["nodeId"] = 1,
                        ["backendNodeId"] = 1,
                        ["nodeType"] = 9,
                        ["nodeName"] = "#document",
                        ["localName"] = "",
                        ["nodeValue"] = "",
                        ["childNodeCount"] = 1,
                        ["childIds"] = documentChildrenIds,
                        ["documentURL"] = $"http://localhost:{CdpServer.Port}/",
                        ["baseURL"] = $"http://localhost:{CdpServer.Port}/"
                    };
                    flatList.Add(docNode);

                    FlattenDomNode(session.Window, session, 1, depth, flatList);

                    var nodesJson = new JsonArray();
                    foreach (var n in flatList)
                    {
                        nodesJson.Add(n);
                    }

                    return new JsonObject { ["nodes"] = nodesJson };
                }

            case "requestNode":
                {
                    string objectId = @params["objectId"]?.GetValue<string>() ?? "";
                    var obj = session.GetObject(objectId);
                    if (obj is Visual visual)
                    {
                        int nodeId = session.NodeMap.GetOrAdd(visual);
                        return new JsonObject { ["nodeId"] = nodeId };
                    }
                    throw new Exception("Resolved object is not a Visual");
                }

            case "scrollIntoViewIfNeeded":
                {
                    int? nodeId = @params["nodeId"]?.GetValue<int>();
                    int? backendNodeId = @params["backendNodeId"]?.GetValue<int>();
                    string? objectId = @params["objectId"]?.GetValue<string>();

                    Visual? targetVisual = null;
                    if (nodeId.HasValue) targetVisual = session.NodeMap.GetVisual(nodeId.Value);
                    else if (backendNodeId.HasValue) targetVisual = session.NodeMap.GetVisual(backendNodeId.Value);
                    else if (!string.IsNullOrEmpty(objectId))
                    {
                        if (session.GetObject(objectId) is Visual v) targetVisual = v;
                    }

                    if (targetVisual is Control control)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => control.BringIntoView());
                    }
                    return new JsonObject();
                }

            case "setNodeValue":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    string value = @params["value"]?.GetValue<string>() ?? "";
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var visual = session.NodeMap.GetVisual(nodeId);
                        if (visual is TextBlock textBlock) textBlock.Text = value;
                        else if (visual is TextBox textBox) textBox.Text = value;
                    });
                    return new JsonObject();
                }

            case "setNodeName":
                {
                    int nodeId = @params["nodeId"]?.GetValue<int>() ?? 0;
                    string name = @params["name"]?.GetValue<string>() ?? "";
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var visual = session.NodeMap.GetVisual(nodeId);
                        if (visual is Control control) control.Name = name;
                    });
                    return new JsonObject { ["nodeId"] = nodeId };
                }

            case "setOuterHTML":
                {
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method DOM.{action} is not implemented");
        }
    }

    private static void FlattenDomNode(Visual visual, CdpSession session, int currentDepth, int maxDepth, List<JsonObject> flatList)
    {
        int nodeId = session.NodeMap.GetOrAdd(visual);
        var children = GetChildren(visual, session).ToList();
        var childIds = new JsonArray();
        foreach (var child in children)
        {
            childIds.Add(session.NodeMap.GetOrAdd(child));
        }

        var attributes = BuildAttributes(visual);
        var parent = GetParent(visual, session);
        var node = new JsonObject
        {
            ["nodeId"] = nodeId,
            ["parentId"] = parent != null ? session.NodeMap.GetOrAdd(parent) : 1,
            ["backendNodeId"] = nodeId,
            ["nodeType"] = 1,
            ["nodeName"] = visual.GetType().Name,
            ["localName"] = visual.GetType().Name.ToLowerInvariant(),
            ["nodeValue"] = "",
            ["childNodeCount"] = children.Count,
            ["attributes"] = attributes
        };

        if (childIds.Count > 0)
        {
            node["childIds"] = childIds;
        }

        flatList.Add(node);

        bool recursive = maxDepth == -1 || currentDepth < maxDepth;
        if (recursive)
        {
            foreach (var child in children)
            {
                FlattenDomNode(child, session, currentDepth + 1, maxDepth, flatList);
            }
        }
    }

    private static JsonObject BuildDocumentNode(CdpSession session, int maxDepth)
    {
        var children = new JsonArray();
        children.Add(BuildDomNode(session.Window, session, 1, maxDepth));

        return new JsonObject
        {
            ["nodeId"] = 1,
            ["backendNodeId"] = 1,
            ["nodeType"] = 9, // Document Node
            ["nodeName"] = "#document",
            ["localName"] = "",
            ["nodeValue"] = "",
            ["childNodeCount"] = 1,
            ["children"] = children,
            ["documentURL"] = $"http://localhost:{CdpServer.Port}/",
            ["baseURL"] = $"http://localhost:{CdpServer.Port}/"
        };
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
            ["nodeName"] = visual.GetType().Name,
            ["localName"] = visual.GetType().Name.ToLowerInvariant(),
            ["nodeValue"] = "",
            ["childNodeCount"] = children.Count,
            ["attributes"] = attributes
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

        if (visual is Control control)
        {
            if (!string.IsNullOrEmpty(control.Name))
            {
                sb.Append(" id=\"").Append(control.Name).Append("\"");
            }
            if (control.Classes.Count > 0)
            {
                sb.Append(" class=\"").Append(string.Join(" ", control.Classes)).Append("\"");
            }
            string? text = GetControlTextOrContent(control);
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
            sb.Append(" />");
        }

        return sb.ToString();
    }

    internal static string? GetControlTextOrContent(Control control)
    {
        if (control is TextBlock textBlock) return textBlock.Text;
        if (control is TextBox textBox) return textBox.Text;
        
        // Try getting Content
        var contentProp = control.GetType().GetProperty("Content");
        if (contentProp != null)
        {
            var contentVal = contentProp.GetValue(control);
            if (contentVal is string str) return str;
        }

        // Try getting Header
        var headerProp = control.GetType().GetProperty("Header");
        if (headerProp != null)
        {
            var headerVal = headerProp.GetValue(control);
            if (headerVal is string str) return str;
        }

        return null;
    }

    private static Thickness GetThicknessProperty(Visual visual, string propertyName)
    {
        var prop = visual.GetType().GetProperty(propertyName);
        if (prop != null)
        {
            var val = prop.GetValue(visual);
            if (val is Thickness thickness)
            {
                return thickness;
            }
        }
        return default;
    }

    private static JsonObject GetBoxModel(CdpSession session, Visual visual)
    {
        double x = 0, y = 0, w = 0, h = 0;
        
        if (visual == session.Window)
        {
            w = session.Window.Bounds.Width;
            h = session.Window.Bounds.Height;
        }
        else
        {
            var origin = new Point(0, 0);
            var translated = visual.TranslatePoint(origin, session.Window);
            if (translated.HasValue)
            {
                x = translated.Value.X;
                y = translated.Value.Y;
            }
            else
            {
                x = visual.Bounds.X;
                y = visual.Bounds.Y;
            }
            w = visual.Bounds.Width;
            h = visual.Bounds.Height;
        }

        var margin = GetThicknessProperty(visual, "Margin");
        var border = GetThicknessProperty(visual, "BorderThickness");
        var padding = GetThicknessProperty(visual, "Padding");

        // Border box matches the control's Bounds
        var borderQuad = new JsonArray { x, y, x + w, y, x + w, y + h, x, y + h };

        // Margin box extends outwards from the border box
        double ml = x - margin.Left;
        double mt = y - margin.Top;
        double mr = x + w + margin.Right;
        double mb = y + h + margin.Bottom;
        var marginQuad = new JsonArray { ml, mt, mr, mt, mr, mb, ml, mb };

        // Padding box sits inside the border box
        double pl = x + border.Left;
        double pt = y + border.Top;
        double pr = x + w - border.Right;
        double pb = y + h - border.Bottom;
        var paddingQuad = new JsonArray { pl, pt, pr, pt, pr, pb, pl, pb };

        // Content box sits inside the padding box
        double cl = pl + padding.Left;
        double ct = pt + padding.Top;
        double cr = pr - padding.Right;
        double cb = pb - padding.Bottom;
        var contentQuad = new JsonArray { cl, ct, cr, ct, cr, cb, cl, cb };

        return new JsonObject
        {
            ["content"] = contentQuad,
            ["padding"] = paddingQuad,
            ["border"] = borderQuad,
            ["margin"] = marginQuad,
            ["width"] = w,
            ["height"] = h
        };
    }

    private static void ApplyAttributeValue(Control control, string name, string value)
    {
        if (name.Equals("class", StringComparison.OrdinalIgnoreCase))
        {
            control.Classes.Clear();
            var classes = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var cls in classes)
            {
                control.Classes.Add(cls);
            }
        }
        else if (name.Equals("name", StringComparison.OrdinalIgnoreCase) || name.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            control.Name = value;
        }
        else if (name.Equals("text", StringComparison.OrdinalIgnoreCase))
        {
            if (control is TextBlock textBlock)
            {
                textBlock.Text = value;
            }
            else if (control is TextBox textBox)
            {
                textBox.Text = value;
            }
            else
            {
                var contentProp = control.GetType().GetProperty("Content", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (contentProp != null && contentProp.CanWrite && contentProp.PropertyType == typeof(string))
                {
                    contentProp.SetValue(control, value);
                }
            }
        }
        else
        {
            CssDomain.SetControlProperty(control, name, value);
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<int>> _searchResults = new();

    private static void SearchVisualTree(Visual parent, string query, CdpSession session, List<int> results)
    {
        bool isMatch = false;
        string typeName = parent.GetType().Name;
        if (typeName.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            isMatch = true;
        }
        else if (parent is Control c)
        {
            if (!string.IsNullOrEmpty(c.Name) && c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
            }
            else if (c is TextBlock tb && !string.IsNullOrEmpty(tb.Text) && tb.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
            }
            else if (c is ContentControl cc && cc.Content is string s && s.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
            }
        }

        if (isMatch)
        {
            results.Add(session.NodeMap.GetOrAdd(parent));
        }

        foreach (var child in GetChildren(parent, session))
        {
            SearchVisualTree(child, query, session, results);
        }
    }

    private static IEnumerable<Visual> GetChildren(Visual visual, CdpSession session)
    {
        if (session.UseLogicalTree && visual is ILogical logical)
        {
            return logical.LogicalChildren.OfType<Visual>();
        }
        return visual.GetVisualChildren().Where(c => !(c is HighlightAdorner));
    }

    private static Visual? GetParent(Visual visual, CdpSession session)
    {
        if (session.UseLogicalTree)
        {
            var current = (visual as ILogical)?.LogicalParent;
            while (current != null)
            {
                if (current is Visual v)
                {
                    return v;
                }
                current = current.LogicalParent;
            }
            return null;
        }
        return visual.GetVisualParent();
    }

    public static JsonArray BuildAttributes(Visual visual)
    {
        var attributes = new JsonArray();
        attributes.Add("type");
        attributes.Add(visual.GetType().FullName ?? visual.GetType().Name);

        if (visual is Control control)
        {
            if (!string.IsNullOrEmpty(control.Name))
            {
                attributes.Add("name");
                attributes.Add(control.Name);
                attributes.Add("id");
                attributes.Add(control.Name);
            }

            if (control.Classes.Count > 0)
            {
                attributes.Add("class");
                attributes.Add(string.Join(" ", control.Classes));
            }

            string? text = GetControlTextOrContent(control);
            if (!string.IsNullOrEmpty(text))
            {
                attributes.Add("text");
                attributes.Add(text);
            }

            attributes.Add("bounds");
            attributes.Add($"{control.Bounds.X},{control.Bounds.Y},{control.Bounds.Width},{control.Bounds.Height}");
            
            attributes.Add("isenabled");
            attributes.Add(control.IsEnabled.ToString().ToLowerInvariant());
            
            attributes.Add("isvisible");
            attributes.Add(control.IsVisible.ToString().ToLowerInvariant());

            var automationName = control.GetValue(AutomationProperties.NameProperty);
            if (!string.IsNullOrEmpty(automationName))
            {
                attributes.Add("accessibility-name");
                attributes.Add(automationName);
            }
            
            var automationHelp = control.GetValue(AutomationProperties.HelpTextProperty);
            if (!string.IsNullOrEmpty(automationHelp))
            {
                attributes.Add("accessibility-help");
                attributes.Add(automationHelp);
            }
        }
        return attributes;
    }
}
