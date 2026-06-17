using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class AccessibilityDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return new JsonObject();

            case "getFullAXTree":
                {
                    var nodes = new JsonArray();
                    if (session.Window != null)
                    {
                        var visuals = new List<Visual>();
                        Traverse(session.Window, visuals);

                        foreach (var visual in visuals)
                        {
                            var node = BuildAXNode(session, visual);
                            nodes.Add(node);
                        }
                    }

                    return new JsonObject { ["nodes"] = nodes };
                }

            case "getAXNode":
            case "getAXNodeAndAncestors":
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

                    var nodes = new JsonArray();
                    if (targetVisual != null)
                    {
                        var current = targetVisual;
                        while (current != null)
                        {
                            var axNode = BuildAXNode(session, current);
                            nodes.Add(axNode);
                            current = current.GetVisualParent();
                        }
                    }

                    return new JsonObject { ["nodes"] = nodes };
                }

            case "getChildAXNodes":
                {
                    string? idStr = @params["id"]?.GetValue<string>();
                    var nodes = new JsonArray();
                    if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out int nodeId))
                    {
                        var visual = session.NodeMap.GetVisual(nodeId);
                        if (visual != null)
                        {
                            foreach (var child in visual.GetVisualChildren())
                            {
                                var childNode = BuildAXNode(session, child);
                                nodes.Add(childNode);
                            }
                        }
                    }
                    return new JsonObject { ["nodes"] = nodes };
                }

            case "getRootAXNode":
                {
                    JsonObject? node = null;
                    if (session.Window != null)
                    {
                        node = BuildAXNode(session, session.Window);
                    }
                    return new JsonObject { ["node"] = node };
                }

            default:
                throw new Exception($"Method Accessibility.{action} is not implemented");
        }
    }

    private static void Traverse(Visual visual, List<Visual> list)
    {
        list.Add(visual);
        foreach (var child in visual.GetVisualChildren())
        {
            Traverse(child, list);
        }
    }

    private static JsonObject BuildAXNode(CdpSession session, Visual visual)
    {
        int visualNodeId = session.NodeMap.GetOrAdd(visual);
        string nodeId = visualNodeId.ToString();

        // ignored: Set to true if the control's AutomationProperties.GetAccessibilityView(control) is None.
        var accessibilityView = AutomationProperties.GetAccessibilityView(visual);
        bool ignored = accessibilityView.ToString().Equals("None", StringComparison.OrdinalIgnoreCase);

        // role: an AXValue object with type="role" and value as the control class name or AutomationProperties.GetControlTypeOverride.
        var overrideType = AutomationProperties.GetControlTypeOverride(visual);
        string roleStr = overrideType?.ToString() ?? visual.GetType().Name;
        var roleJson = new JsonObject
        {
            ["type"] = "role",
            ["value"] = roleStr
        };

        // name: an AXValue object with type="string" and value from AutomationProperties.GetName(control) or control text (like TextBlock.Text).
        string? nameStr = AutomationProperties.GetName(visual);
        if (string.IsNullOrEmpty(nameStr))
        {
            nameStr = GetControlTextOrContent(visual);
        }

        JsonObject? nameJson = null;
        if (nameStr != null)
        {
            nameJson = new JsonObject
            {
                ["type"] = "string",
                ["value"] = nameStr
            };
        }

        // description: an AXValue object with type="string" and value from AutomationProperties.GetHelpText(control) if set.
        string? descriptionStr = AutomationProperties.GetHelpText(visual);
        JsonObject? descriptionJson = null;
        if (!string.IsNullOrEmpty(descriptionStr))
        {
            descriptionJson = new JsonObject
            {
                ["type"] = "string",
                ["value"] = descriptionStr
            };
        }

        // parentId: string parent ID if applicable.
        var parent = visual.GetVisualParent();
        string? parentId = null;
        if (parent != null)
        {
            parentId = session.NodeMap.GetOrAdd(parent).ToString();
        }

        // childIds: array of string child IDs.
        var childIds = new JsonArray();
        foreach (var child in visual.GetVisualChildren())
        {
            childIds.Add(session.NodeMap.GetOrAdd(child).ToString());
        }

        var nodeJson = new JsonObject
        {
            ["nodeId"] = nodeId,
            ["ignored"] = ignored,
            ["role"] = roleJson,
            ["backendDOMNodeId"] = visualNodeId
        };

        if (nameJson != null)
        {
            nodeJson["name"] = nameJson;
        }

        if (descriptionJson != null)
        {
            nodeJson["description"] = descriptionJson;
        }

        if (parentId != null)
        {
            nodeJson["parentId"] = parentId;
        }

        if (childIds.Count > 0)
        {
            nodeJson["childIds"] = childIds;
        }

        return nodeJson;
    }

    private static string? GetControlTextOrContent(Visual visual)
    {
        if (visual is TextBlock textBlock) return textBlock.Text;
        if (visual is TextBox textBox) return textBox.Text;
        
        // Try getting Content
        var contentProp = visual.GetType().GetProperty("Content");
        if (contentProp != null)
        {
            var contentVal = contentProp.GetValue(visual);
            if (contentVal is string str) return str;
        }

        // Try getting Header
        var headerProp = visual.GetType().GetProperty("Header");
        if (headerProp != null)
        {
            var headerVal = headerProp.GetValue(visual);
            if (headerVal is string str) return str;
        }

        return null;
    }
}
