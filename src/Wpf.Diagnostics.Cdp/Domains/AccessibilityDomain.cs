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

    private static void TraversePeers(AutomationPeer peer, List<AutomationPeer> list)
    {
        list.Add(peer);
        var children = peer.GetChildren();
        if (children != null)
        {
            foreach (var child in children)
            {
                TraversePeers(child, list);
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

        return new JsonObject
        {
            ["axId"] = peer.GetHashCode().ToString(),
            ["parentId"] = peer.GetParent()?.GetHashCode().ToString(),
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
