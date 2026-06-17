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

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class DomDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return new JsonObject();

            case "getDocument":
                {
                    int depth = @params["depth"]?.GetValue<int>() ?? -1;
                    var rootNode = BuildDocumentNode(session, depth);
                    return new JsonObject { ["root"] = rootNode };
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
                    var match = SelectorEngine.QuerySelector(root, selector);
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
                    var matches = SelectorEngine.QuerySelectorAll(root, selector);
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
                    string html = GetOuterHtml(visual);
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

            case "performSearch":
                {
                    string query = @params["query"]?.GetValue<string>() ?? "";
                    var results = new List<int>();

                    try
                    {
                        var matches = SelectorEngine.QuerySelectorAll(session.Window, query);
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

            default:
                throw new Exception($"Method DOM.{action} is not implemented");
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
            ["documentURL"] = "http://localhost:9222/",
            ["baseURL"] = "http://localhost:9222/"
        };
    }

    public static JsonObject BuildDomNode(Visual visual, CdpSession session, int currentDepth, int maxDepth)
    {
        int nodeId = session.NodeMap.GetOrAdd(visual);
        var visualChildren = visual.GetVisualChildren().ToList();
        var childrenJson = new JsonArray();

        bool recursive = maxDepth == -1 || currentDepth < maxDepth;
        if (recursive)
        {
            foreach (var child in visualChildren)
            {
                childrenJson.Add(BuildDomNode(child, session, currentDepth + 1, maxDepth));
            }
        }

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

        var node = new JsonObject
        {
            ["nodeId"] = nodeId,
            ["backendNodeId"] = nodeId,
            ["nodeType"] = 1, // Element Node
            ["nodeName"] = visual.GetType().Name,
            ["localName"] = visual.GetType().Name.ToLowerInvariant(),
            ["nodeValue"] = "",
            ["childNodeCount"] = visualChildren.Count,
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
        foreach (var child in visual.GetVisualChildren())
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

    public static string GetOuterHtml(Visual visual)
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

        var children = visual.GetVisualChildren().ToList();
        if (children.Count > 0)
        {
            sb.Append(">");
            foreach (var child in children)
            {
                sb.Append(GetOuterHtml(child));
            }
            sb.Append("</").Append(name).Append(">");
        }
        else
        {
            sb.Append(" />");
        }

        return sb.ToString();
    }

    private static string? GetControlTextOrContent(Control control)
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

        var contentQuad = new JsonArray { x, y, x + w, y, x + w, y + h, x, y + h };
        var paddingQuad = new JsonArray { x, y, x + w, y, x + w, y + h, x, y + h };
        var borderQuad = new JsonArray { x, y, x + w, y, x + w, y + h, x, y + h };
        var marginQuad = new JsonArray { x, y, x + w, y, x + w, y + h, x, y + h };

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

        foreach (var child in parent.GetVisualChildren())
        {
            SearchVisualTree(child, query, session, results);
        }
    }
}
