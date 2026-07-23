using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp.Domains;

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
                        var rootPeer = UIElementAutomationPeer.CreatePeerForElement(session.Window);
                        if (rootPeer != null)
                        {
                            var list = new List<AutomationPeer>();
                            TraversePeers(rootPeer, list);

                            var extraRoots = GetExtraRootPeers(session);
                            foreach (var extraRoot in extraRoots)
                            {
                                if (extraRoot != null)
                                {
                                    TraversePeers(extraRoot, list);
                                }
                            }

                            foreach (var peer in list)
                            {
                                var node = BuildAXNode(session, peer);
                                nodes.Add(node);
                            }
                        }
                    }

                    return new JsonObject { ["nodes"] = nodes };
                }

            case "getAXNode":
            case "getAXNodeAndAncestors":
                {
                    int? nodeId = @params["nodeId"]?.GetValue<int>();
                    int? backendNodeId = @params["backendNodeId"]?.GetValue<int>();

                    Visual? targetVisual = null;
                    if (nodeId.HasValue)
                    {
                        targetVisual = session.NodeMap.GetVisual(nodeId.Value);
                    }
                    else if (backendNodeId.HasValue)
                    {
                        targetVisual = session.NodeMap.GetVisual(backendNodeId.Value);
                    }

                    var nodes = new JsonArray();
                    if (targetVisual != null)
                    {
                        var peer = targetVisual is UIElement targetUI ? UIElementAutomationPeer.CreatePeerForElement(targetUI) : null;
                        if (peer != null)
                        {
                            var current = peer;
                            while (current != null)
                            {
                                nodes.Add(BuildAXNode(session, current));
                                current = current.GetParent();
                            }
                        }
                        else
                        {
                            var current = targetVisual;
                            while (current != null)
                            {
                                nodes.Add(BuildAXNodeFromVisualFallback(session, current));
                                current = VisualTreeHelper.GetParent(current) as Visual;
                            }
                        }
                    }

                    return new JsonObject { ["nodes"] = nodes };
                }

            default:
                throw new Exception($"Method Accessibility.{action} is not implemented");
        }
    }

    private static List<AutomationPeer> GetExtraRootPeers(CdpSession session)
    {
        var list = new List<AutomationPeer>();
        var mainWin = session.Window;

        // 1. Secondary Windows
        foreach (var target in CdpServer.GetWindows())
        {
            var win = target.Window;
            if (win != null && win != mainWin && win.IsVisible)
            {
                var winPeer = UIElementAutomationPeer.CreatePeerForElement(win);
                if (winPeer != null && !list.Contains(winPeer))
                {
                    list.Add(winPeer);
                }
            }
        }

        // 2. Open Popups
        var openPopups = new List<System.Windows.Controls.Primitives.Popup>();
        var visited = new HashSet<Visual>();
        foreach (var target in CdpServer.GetWindows())
        {
            if (target.Window != null)
            {
                CdpVisualTreeHelper.FindOpenPopups(target.Window, openPopups, visited);
            }
        }

        foreach (var popup in openPopups)
        {
            if (popup != null && popup.Child is UIElement childUI)
            {
                var popupPeer = UIElementAutomationPeer.CreatePeerForElement(childUI);
                if (popupPeer != null && !list.Contains(popupPeer))
                {
                    list.Add(popupPeer);
                }
            }
        }

        return list;
    }

    private static string GetPeerId(CdpSession session, AutomationPeer peer)
    {
        if (peer == null) return "";
        if (peer.Owner is Visual visual)
        {
            return session.NodeMap.GetOrAdd(visual).ToString();
        }
        return $"synthetic-{peer.GetHashCode()}";
    }

    private static void TraversePeers(AutomationPeer peer, List<AutomationPeer> list)
    {
        list.Add(peer);
        var children = peer.GetChildren();
        if (children != null)
        {
            foreach (var child in children)
            {
                if (child != null)
                {
                    TraversePeers(child, list);
                }
            }
        }
    }

    private static JsonObject BuildAXNode(CdpSession session, AutomationPeer peer)
    {
        int nodeId = 0;
        var owner = peer.Owner;
        if (owner is Visual visual)
        {
            nodeId = session.NodeMap.GetOrAdd(visual);
        }

        string role = peer.GetAutomationControlType().ToString();
        string name = peer.GetName() ?? "";
        string helpText = peer.GetHelpText() ?? "";

        var properties = new JsonArray
        {
            new JsonObject { ["name"] = "role", ["value"] = new JsonObject { ["type"] = "role", ["value"] = role } },
            new JsonObject { ["name"] = "name", ["value"] = new JsonObject { ["type"] = "computedString", ["value"] = name } }
        };

        if (!string.IsNullOrEmpty(helpText))
        {
            properties.Add(new JsonObject { ["name"] = "description", ["value"] = new JsonObject { ["type"] = "computedString", ["value"] = helpText } });
        }

        var parentPeer = peer.GetParent();
        string? parentId = null;
        if (parentPeer != null)
        {
            parentId = GetPeerId(session, parentPeer);
        }
        else if (session.Window != null && peer.Owner != session.Window)
        {
            var mainPeer = UIElementAutomationPeer.CreatePeerForElement(session.Window);
            if (mainPeer != null)
            {
                parentId = GetPeerId(session, mainPeer);
            }
        }

        var childIds = new JsonArray();
        var children = peer.GetChildren();
        if (children != null)
        {
            foreach (var child in children)
            {
                if (child != null)
                {
                    childIds.Add(GetPeerId(session, child));
                }
            }
        }

        if (session.Window != null && peer.Owner == session.Window)
        {
            var extraRoots = GetExtraRootPeers(session);
            foreach (var extra in extraRoots)
            {
                if (extra != null)
                {
                    string extraId = GetPeerId(session, extra);
                    if (!string.IsNullOrEmpty(extraId) && !childIds.Any(c => c?.GetValue<string>() == extraId))
                    {
                        childIds.Add(extraId);
                    }
                }
            }
        }

        return new JsonObject
        {
            ["axId"] = GetPeerId(session, peer),
            ["parentId"] = parentId,
            ["childIds"] = childIds,
            ["ignored"] = false,
            ["properties"] = properties,
            ["name"] = new JsonObject { ["type"] = "computedString", ["value"] = name },
            ["role"] = new JsonObject { ["type"] = "role", ["value"] = role },
            ["backendDOMNodeId"] = nodeId
        };
    }

    private static JsonObject BuildAXNodeFromVisualFallback(CdpSession session, Visual visual)
    {
        int nodeId = session.NodeMap.GetOrAdd(visual);
        string name = visual is FrameworkElement fe ? fe.Name : visual.GetType().Name;
        string role = "Visual";

        return new JsonObject
        {
            ["axId"] = visual.GetHashCode().ToString(),
            ["parentId"] = (VisualTreeHelper.GetParent(visual) as Visual)?.GetHashCode().ToString(),
            ["ignored"] = false,
            ["properties"] = new JsonArray
            {
                new JsonObject { ["name"] = "role", ["value"] = new JsonObject { ["type"] = "role", ["value"] = role } },
                new JsonObject { ["name"] = "name", ["value"] = new JsonObject { ["type"] = "computedString", ["value"] = name } }
            },
            ["name"] = new JsonObject { ["type"] = "computedString", ["value"] = name },
            ["role"] = new JsonObject { ["type"] = "role", ["value"] = role },
            ["backendDOMNodeId"] = nodeId
        };
    }
}
