using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;

namespace WinUI.Diagnostics.Cdp.Domains;

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
                    if (session.Window?.Content == null)
                    {
                        return new JsonObject { ["nodes"] = new JsonArray() };
                    }
                    var nodes = await session.Window.DispatcherQueue.InvokeAsync(() =>
                    {
                        var arr = new JsonArray();
                        WalkAccessibilityTree(session.Window.Content, session, arr);

                        var windows = CdpServer.GetWindows().ToList();
                        var mainWin = session.Window;
                        foreach (var target in windows)
                        {
                            var win = target.Window;
                            if (win != null && win != mainWin && win.Content != null)
                            {
                                WalkAccessibilityTree(win.Content, session, arr);
                            }
                        }

                        foreach (var target in windows)
                        {
                            var win = target.Window;
                            if (win != null && win.Content != null && win.Content.XamlRoot != null)
                            {
                                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(win.Content.XamlRoot);
                                if (popups != null)
                                {
                                    foreach (var popup in popups)
                                    {
                                        if (popup != null && popup.Child is UIElement popupChild)
                                        {
                                            WalkAccessibilityTree(popupChild, session, arr);
                                        }
                                    }
                                }
                            }
                        }

                        return arr;
                    });
                    return new JsonObject { ["nodes"] = nodes };
                }

            case "getAXNode":
            case "getAXNodeAndAncestors":
                {
                    int? nodeId = @params["nodeId"]?.GetValue<int>();
                    int? backendNodeId = @params["backendNodeId"]?.GetValue<int>();

                    if (session.Window == null) return new JsonObject { ["nodes"] = new JsonArray() };
                    var dispatcher = session.Window.DispatcherQueue;
                    var nodes = await dispatcher.InvokeAsync(() =>
                    {
                        var arr = new JsonArray();
                        int resolvedNodeId = nodeId ?? backendNodeId ?? 0;
                        var targetVisual = session.NodeMap.GetVisual(resolvedNodeId) as UIElement;
                        if (targetVisual != null)
                        {
                            var current = targetVisual;
                            while (current != null)
                            {
                                arr.Add(BuildAXNodeFromElement(session, current));
                                current = VisualTreeHelper.GetParent(current) as UIElement;
                            }
                        }
                        return arr;
                    });

                    return new JsonObject { ["nodes"] = nodes };
                }

            default:
                throw new Exception($"Method Accessibility.{action} is not implemented");
        }
    }

    private static JsonObject BuildAXNodeFromElement(CdpSession session, UIElement element)
    {
        int nodeId = session.NodeMap.GetOrAdd(element);
        var name = AutomationProperties.GetName(element);
        var autoId = AutomationProperties.GetAutomationId(element);
        var help = AutomationProperties.GetHelpText(element);

        string displayName = string.IsNullOrEmpty(name) ? (string.IsNullOrEmpty(autoId) ? element.GetType().Name : autoId) : name;
        string role = element.GetType().Name;

        var properties = new JsonArray
        {
            new JsonObject { ["name"] = "role", ["value"] = new JsonObject { ["type"] = "role", ["value"] = role } },
            new JsonObject { ["name"] = "name", ["value"] = new JsonObject { ["type"] = "computedString", ["value"] = displayName } }
        };

        if (!string.IsNullOrEmpty(help))
        {
            properties.Add(new JsonObject { ["name"] = "description", ["value"] = new JsonObject { ["type"] = "computedString", ["value"] = help } });
        }

        return new JsonObject
        {
            ["axId"] = element.GetHashCode().ToString(),
            ["parentId"] = (VisualTreeHelper.GetParent(element) as UIElement)?.GetHashCode().ToString(),
            ["ignored"] = false,
            ["properties"] = properties,
            ["name"] = new JsonObject { ["type"] = "computedString", ["value"] = displayName },
            ["role"] = new JsonObject { ["type"] = "role", ["value"] = role },
            ["backendDOMNodeId"] = nodeId
        };
    }

    private static void WalkAccessibilityTree(UIElement element, CdpSession session, JsonArray list)
    {
        var name = AutomationProperties.GetName(element);
        var help = AutomationProperties.GetHelpText(element);
        var autoId = AutomationProperties.GetAutomationId(element);

        if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(autoId))
        {
            int nodeId = session.NodeMap.GetOrAdd(element);
            var nodeJson = new JsonObject
            {
                ["nodeId"] = nodeId.ToString(),
                ["role"] = new JsonObject { ["type"] = "role", ["value"] = element.GetType().Name },
                ["name"] = new JsonObject { ["type"] = "name", ["value"] = string.IsNullOrEmpty(name) ? autoId : name }
            };
            if (!string.IsNullOrEmpty(help))
            {
                nodeJson["description"] = new JsonObject { ["type"] = "description", ["value"] = help };
            }
            list.Add(nodeJson);
        }

        int count = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(element, i) is UIElement child)
            {
                WalkAccessibilityTree(child, session, list);
            }
        }
    }
}
