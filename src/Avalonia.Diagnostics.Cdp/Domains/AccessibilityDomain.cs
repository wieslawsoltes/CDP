using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
                        var rootPeer = ControlAutomationPeer.CreatePeerForElement(session.Window);
                        if (rootPeer != null)
                        {
                            var list = new List<AutomationPeer>();
                            TraversePeers(rootPeer, list);

                            var extraRoots = GetExtraRootPeers(session);
                            foreach (var extraRoot in extraRoots)
                            {
                                TraversePeers(extraRoot, list);
                            }

                            var addedPeerIds = new HashSet<string>();
                            foreach (var peer in list)
                            {
                                string peerId = GetPeerId(session, peer);
                                if (addedPeerIds.Add(peerId))
                                {
                                    var node = BuildAXNode(session, peer);
                                    nodes.Add(node);
                                }
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

                    var nodes = new JsonArray();
                    if (targetVisual != null)
                    {
                        var peer = targetVisual is Control targetControl ? ControlAutomationPeer.CreatePeerForElement(targetControl) : null;
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
                                current = current.GetVisualParent();
                            }
                        }
                    }

                    return new JsonObject { ["nodes"] = nodes };
                }

            case "getChildAXNodes":
                {
                    string? idStr = @params["id"]?.GetValue<string>();
                    var nodes = new JsonArray();
                    if (!string.IsNullOrEmpty(idStr))
                    {
                        var peersMap = GetPeersMap(session);
                        if (peersMap.TryGetValue(idStr, out var parentPeer))
                        {
                            var children = parentPeer.GetChildren();
                            if (children != null)
                            {
                                foreach (var child in children)
                                {
                                    nodes.Add(BuildAXNode(session, child));
                                }
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
                        var rootPeer = ControlAutomationPeer.CreatePeerForElement(session.Window);
                        if (rootPeer != null)
                        {
                            node = BuildAXNode(session, rootPeer);
                        }
                    }
                    return new JsonObject { ["node"] = node };
                }

            case "getPartialAXTree":
                {
                    int? nodeId = @params["nodeId"]?.GetValue<int>();
                    int? backendNodeId = @params["backendNodeId"]?.GetValue<int>();
                    string? objectId = @params["objectId"]?.GetValue<string>();
                    bool fetchRelatives = @params["fetchRelatives"]?.GetValue<bool>() ?? true;

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

                    var nodes = new JsonArray();
                    if (targetVisual != null)
                    {
                        var peer = targetVisual is Control targetControl ? ControlAutomationPeer.CreatePeerForElement(targetControl) : null;
                        if (peer != null)
                        {
                            var addedNodeIds = new HashSet<string>();

                            void AddNode(AutomationPeer p)
                            {
                                string id = GetPeerId(session, p);
                                if (addedNodeIds.Add(id))
                                {
                                    nodes.Add(BuildAXNode(session, p));
                                }
                            }

                            AddNode(peer);

                            if (fetchRelatives)
                            {
                                // Children
                                var children = peer.GetChildren();
                                if (children != null)
                                {
                                    foreach (var child in children)
                                    {
                                        AddNode(child);
                                    }
                                }

                                // Ancestors and siblings
                                var parent = peer.GetParent();
                                while (parent != null)
                                {
                                    AddNode(parent);

                                    var siblings = parent.GetChildren();
                                    if (siblings != null)
                                    {
                                        foreach (var sibling in siblings)
                                        {
                                            AddNode(sibling);
                                        }
                                    }

                                    parent = parent.GetParent();
                                }
                            }
                        }
                        else
                        {
                            var addedNodeIds = new HashSet<string>();

                            void AddNode(Visual v)
                            {
                                int id = session.NodeMap.GetOrAdd(v);
                                string idStr = id.ToString();
                                if (addedNodeIds.Add(idStr))
                                {
                                    nodes.Add(BuildAXNodeFromVisualFallback(session, v));
                                }
                            }

                            AddNode(targetVisual);

                            if (fetchRelatives)
                            {
                                // Children
                                foreach (var child in targetVisual.GetVisualChildren())
                                {
                                    AddNode(child);
                                }

                                // Ancestors and siblings
                                var parent = targetVisual.GetVisualParent();
                                while (parent != null)
                                {
                                    AddNode(parent);

                                    foreach (var sibling in parent.GetVisualChildren())
                                    {
                                        AddNode(sibling);
                                    }

                                    parent = parent.GetVisualParent();
                                }
                            }
                        }
                    }

                    return new JsonObject { ["nodes"] = nodes };
                }

            case "queryAXTree":
                {
                    int? nodeId = @params["nodeId"]?.GetValue<int>();
                    int? backendNodeId = @params["backendNodeId"]?.GetValue<int>();
                    string? objectId = @params["objectId"]?.GetValue<string>();
                    string? accessibleName = @params["accessibleName"]?.GetValue<string>();
                    string? role = @params["role"]?.GetValue<string>();

                    Visual? rootVisual = null;
                    if (nodeId.HasValue) rootVisual = session.NodeMap.GetVisual(nodeId.Value);
                    else if (backendNodeId.HasValue) rootVisual = session.NodeMap.GetVisual(backendNodeId.Value);
                    else if (!string.IsNullOrEmpty(objectId))
                    {
                        var obj = session.GetObject(objectId);
                        rootVisual = CdpSession.GetVisualFromObject(obj);
                    }

                    if (rootVisual == null) rootVisual = session.Window;

                    var matchedNodes = new JsonArray();
                    if (rootVisual != null)
                    {
                        var rootPeer = rootVisual is Control rootControl ? ControlAutomationPeer.CreatePeerForElement(rootControl) : null;
                        if (rootPeer != null)
                        {
                            var list = new List<AutomationPeer>();
                            TraversePeers(rootPeer, list);

                            foreach (var peer in list)
                            {
                                var axNode = BuildAXNode(session, peer);
                                bool matches = true;

                                if (!string.IsNullOrEmpty(accessibleName))
                                {
                                    var nameNode = axNode["name"]?["value"]?.GetValue<string>();
                                    if (nameNode == null || !nameNode.Contains(accessibleName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        matches = false;
                                    }
                                }

                                if (matches && !string.IsNullOrEmpty(role))
                                {
                                    var roleNode = axNode["role"]?["value"]?.GetValue<string>();
                                    if (roleNode == null || !roleNode.Equals(role, StringComparison.OrdinalIgnoreCase))
                                    {
                                        matches = false;
                                    }
                                }

                                if (matches)
                                {
                                    matchedNodes.Add(axNode);
                                }
                            }
                        }
                    }

                    return new JsonObject { ["nodes"] = matchedNodes };
                }

            default:
                throw new Exception($"Method Accessibility.{action} is not implemented");
        }
    }

    private static void TraversePeers(AutomationPeer peer, List<AutomationPeer> list)
    {
        if (peer == null) return;
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
                var winPeer = ControlAutomationPeer.CreatePeerForElement(win);
                if (winPeer != null)
                {
                    list.Add(winPeer);
                }
            }
        }

        // 2. Open Popups (ContextMenu, Flyout, ToolTip, ComboBox dropdown, PopupRoot)
        var openPopups = new List<Popup>();
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
            if (popup != null)
            {
                var content = CdpVisualTreeHelper.GetPopupContent(popup);
                if (content is Control ctrl)
                {
                    var popupPeer = ControlAutomationPeer.CreatePeerForElement(ctrl);
                    if (popupPeer != null && !list.Contains(popupPeer))
                    {
                        list.Add(popupPeer);
                    }
                }
            }
        }

        return list;
    }

    private static Dictionary<string, AutomationPeer> GetPeersMap(CdpSession session)
    {
        var map = new Dictionary<string, AutomationPeer>();
        if (session.Window != null)
        {
            var rootPeer = ControlAutomationPeer.CreatePeerForElement(session.Window);
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
                    if (peer != null)
                    {
                        string id = GetPeerId(session, peer);
                        if (!string.IsNullOrEmpty(id))
                        {
                            map[id] = peer;
                        }
                    }
                }
            }
        }
        return map;
    }

    private static string GetPeerId(CdpSession session, AutomationPeer peer)
    {
        if (peer == null) return "";
        if (peer is ControlAutomationPeer controlPeer && controlPeer.Owner is Visual visual)
        {
            return session.NodeMap.GetOrAdd(visual).ToString();
        }
        return $"synthetic-{peer.GetHashCode()}";
    }

    private static string MapAutomationRole(AutomationControlType controlType, Visual? visual, AutomationPeer? peer)
    {
        if (visual != null)
        {
            var overrideType = AutomationProperties.GetControlTypeOverride(visual);
            if (overrideType.HasValue)
            {
                controlType = overrideType.Value;
            }
        }

        switch (controlType)
        {
            case AutomationControlType.Button: return "button";
            case AutomationControlType.CheckBox: return "checkbox";
            case AutomationControlType.ComboBox: return "combobox";
            case AutomationControlType.Edit: return "textbox";
            case AutomationControlType.List: return "list";
            case AutomationControlType.ListItem: return "listitem";
            case AutomationControlType.Slider: return "slider";
            case AutomationControlType.Text: return "StaticText";
            case AutomationControlType.Header: return "heading";
            case AutomationControlType.Menu: return "menu";
            case AutomationControlType.MenuItem: return "menuitem";
            case AutomationControlType.ProgressBar: return "progressbar";
            case AutomationControlType.RadioButton: return "radio";
            case AutomationControlType.ScrollBar: return "scrollbar";
            case AutomationControlType.Tab: return "tab";
            case AutomationControlType.TabItem: return "tab";
            case AutomationControlType.ToolTip: return "tooltip";
            case AutomationControlType.Tree: return "tree";
            case AutomationControlType.TreeItem: return "treeitem";
            case AutomationControlType.Window: return "window";
            default:
                if (peer != null)
                {
                    string? className = peer.GetClassName();
                    if (!string.IsNullOrEmpty(className)) return className;
                }
                return visual?.GetType().Name ?? "Unknown";
        }
    }

    private static JsonObject BuildAXNode(CdpSession session, AutomationPeer peer)
    {
        Visual? visual = null;
        if (peer is ControlAutomationPeer controlPeer)
        {
            visual = controlPeer.Owner;
        }

        string nodeId = GetPeerId(session, peer);
        int? backendDOMNodeId = null;
        if (visual != null)
        {
            backendDOMNodeId = session.NodeMap.GetOrAdd(visual);
        }

        bool ignored = false;
        if (peer is NoneAutomationPeer)
        {
            ignored = true;
        }
        else if (!peer.IsControlElement() && !peer.IsContentElement())
        {
            ignored = true;
        }
        else if (visual != null && AutomationProperties.GetAccessibilityView(visual) == AccessibilityView.Raw)
        {
            ignored = true;
        }

        string roleStr = MapAutomationRole(peer.GetAutomationControlType(), visual, peer);
        var roleJson = new JsonObject
        {
            ["type"] = "role",
            ["value"] = roleStr
        };

        string? nameStr = peer.GetName();
        if (string.IsNullOrEmpty(nameStr) && visual != null)
        {
            nameStr = AutomationProperties.GetName(visual);
            if (string.IsNullOrEmpty(nameStr))
            {
                nameStr = GetControlTextOrContent(visual);
            }
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

        string? descriptionStr = null;
        if (visual != null)
        {
            descriptionStr = AutomationProperties.GetHelpText(visual);
        }
        JsonObject? descriptionJson = null;
        if (!string.IsNullOrEmpty(descriptionStr))
        {
            descriptionJson = new JsonObject
            {
                ["type"] = "string",
                ["value"] = descriptionStr
            };
        }

        var parentPeer = peer.GetParent();
        string? parentId = null;
        if (parentPeer != null)
        {
            parentId = GetPeerId(session, parentPeer);
        }
        else if (session.Window != null && peer is ControlAutomationPeer popupCap && popupCap.Owner != session.Window)
        {
            var mainPeer = ControlAutomationPeer.CreatePeerForElement(session.Window);
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
                childIds.Add(GetPeerId(session, child));
            }
        }

        if (session.Window != null && peer is ControlAutomationPeer cap && cap.Owner == session.Window)
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

        var propertiesJson = new JsonArray();

        propertiesJson.Add(new JsonObject
        {
            ["name"] = "focusable",
            ["value"] = new JsonObject
            {
                ["type"] = "boolean",
                ["value"] = peer.IsKeyboardFocusable()
            }
        });

        propertiesJson.Add(new JsonObject
        {
            ["name"] = "focused",
            ["value"] = new JsonObject
            {
                ["type"] = "boolean",
                ["value"] = peer.HasKeyboardFocus()
            }
        });

        propertiesJson.Add(new JsonObject
        {
            ["name"] = "disabled",
            ["value"] = new JsonObject
            {
                ["type"] = "boolean",
                ["value"] = !peer.IsEnabled()
            }
        });

        var rangeProvider = peer.GetProvider<IRangeValueProvider>();
        if (rangeProvider != null)
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "valuemin",
                ["value"] = new JsonObject
                {
                    ["type"] = "number",
                    ["value"] = rangeProvider.Minimum
                }
            });
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "valuemax",
                ["value"] = new JsonObject
                {
                    ["type"] = "number",
                    ["value"] = rangeProvider.Maximum
                }
            });
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "value",
                ["value"] = new JsonObject
                {
                    ["type"] = "number",
                    ["value"] = rangeProvider.Value
                }
            });
        }
        else
        {
            var valueProvider = peer.GetProvider<IValueProvider>();
            if (valueProvider != null)
            {
                propertiesJson.Add(new JsonObject
                {
                    ["name"] = "value",
                    ["value"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["value"] = valueProvider.Value
                    }
                });
            }
        }

        var toggleProvider = peer.GetProvider<IToggleProvider>();
        if (toggleProvider != null)
        {
            string checkedStr = toggleProvider.ToggleState switch
            {
                ToggleState.On => "true",
                ToggleState.Off => "false",
                _ => "mixed"
            };
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "checked",
                ["value"] = new JsonObject
                {
                    ["type"] = "token",
                    ["value"] = checkedStr
                }
            });
        }

        var expandCollapseProvider = peer.GetProvider<IExpandCollapseProvider>();
        if (expandCollapseProvider != null)
        {
            bool expanded = expandCollapseProvider.ExpandCollapseState == ExpandCollapseState.Expanded;
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "expanded",
                ["value"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["value"] = expanded
                }
            });
        }

        var selectionItemProvider = peer.GetProvider<ISelectionItemProvider>();
        if (selectionItemProvider != null)
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "selected",
                ["value"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["value"] = selectionItemProvider.IsSelected
                }
            });
        }

        var selectionProvider = peer.GetProvider<ISelectionProvider>();
        if (selectionProvider != null)
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "multiselectable",
                ["value"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["value"] = selectionProvider.CanSelectMultiple
                }
            });
        }

        if (!string.IsNullOrEmpty(descriptionStr))
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "description",
                ["value"] = new JsonObject
                {
                    ["type"] = "string",
                    ["value"] = descriptionStr
                }
            });
        }

        string? acceleratorKey = peer.GetAcceleratorKey() ?? (visual != null ? AutomationProperties.GetAcceleratorKey(visual) : null);
        if (!string.IsNullOrEmpty(acceleratorKey))
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "keyshortcuts",
                ["value"] = new JsonObject
                {
                    ["type"] = "string",
                    ["value"] = acceleratorKey
                }
            });
        }

        if (visual != null)
        {
            int posInSet = AutomationProperties.GetPositionInSet(visual);
            if (posInSet > 0)
            {
                propertiesJson.Add(new JsonObject
                {
                    ["name"] = "posinset",
                    ["value"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["value"] = posInSet
                    }
                });
            }

            int sizeOfSet = AutomationProperties.GetSizeOfSet(visual);
            if (sizeOfSet > 0)
            {
                propertiesJson.Add(new JsonObject
                {
                    ["name"] = "setsize",
                    ["value"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["value"] = sizeOfSet
                    }
                });
            }

            var liveSetting = AutomationProperties.GetLiveSetting(visual);
            if (liveSetting != AutomationLiveSetting.Off)
            {
                string liveStr = liveSetting switch
                {
                    AutomationLiveSetting.Polite => "polite",
                    AutomationLiveSetting.Assertive => "assertive",
                    _ => "off"
                };
                propertiesJson.Add(new JsonObject
                {
                    ["name"] = "live",
                    ["value"] = new JsonObject
                    {
                        ["type"] = "token",
                        ["value"] = liveStr
                    }
                });
            }

            bool isRequired = AutomationProperties.GetIsRequiredForForm(visual);
            if (isRequired)
            {
                propertiesJson.Add(new JsonObject
                {
                    ["name"] = "required",
                    ["value"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["value"] = true
                    }
                });
            }
        }

        string? localizedControlType = peer.GetLocalizedControlType();
        if (!string.IsNullOrEmpty(localizedControlType))
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "roledescription",
                ["value"] = new JsonObject
                {
                    ["type"] = "string",
                    ["value"] = localizedControlType
                }
            });
        }

        var nodeJson = new JsonObject
        {
            ["nodeId"] = nodeId,
            ["ignored"] = ignored,
            ["role"] = roleJson
        };

        if (backendDOMNodeId.HasValue)
        {
            nodeJson["backendDOMNodeId"] = backendDOMNodeId.Value;
        }

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

        if (propertiesJson.Count > 0)
        {
            nodeJson["properties"] = propertiesJson;
        }

        return nodeJson;
    }

    private static JsonObject BuildAXNodeFromVisualFallback(CdpSession session, Visual visual)
    {
        int visualNodeId = session.NodeMap.GetOrAdd(visual);
        string nodeId = visualNodeId.ToString();

        bool ignored = true;
        var accessibilityView = AutomationProperties.GetAccessibilityView(visual);
        if (accessibilityView == AccessibilityView.Control || accessibilityView == AccessibilityView.Content)
        {
            ignored = false;
        }
        else if (accessibilityView == AccessibilityView.Default)
        {
            var overrideType = AutomationProperties.GetControlTypeOverride(visual);
            var nameStr = AutomationProperties.GetName(visual);
            if (overrideType.HasValue || !string.IsNullOrEmpty(nameStr))
            {
                ignored = false;
            }
        }

        var overrideTypeVal = AutomationProperties.GetControlTypeOverride(visual);
        string roleStr = overrideTypeVal.HasValue
            ? MapAutomationRole(overrideTypeVal.Value, visual, null)
            : visual.GetType().Name;
        var roleJson = new JsonObject
        {
            ["type"] = "role",
            ["value"] = roleStr
        };

        string? nameStrFallback = AutomationProperties.GetName(visual);
        if (string.IsNullOrEmpty(nameStrFallback))
        {
            nameStrFallback = GetControlTextOrContent(visual);
        }

        JsonObject? nameJson = null;
        if (nameStrFallback != null)
        {
            nameJson = new JsonObject
            {
                ["type"] = "string",
                ["value"] = nameStrFallback
            };
        }

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

        var parent = visual.GetVisualParent();
        string? parentId = null;
        if (parent != null)
        {
            parentId = session.NodeMap.GetOrAdd(parent).ToString();
        }

        var childIds = new JsonArray();
        foreach (var child in visual.GetVisualChildren())
        {
            childIds.Add(session.NodeMap.GetOrAdd(child).ToString());
        }

        var propertiesJson = new JsonArray();
        if (visual is Avalonia.Input.IInputElement ie)
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "focusable",
                ["value"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["value"] = ie.Focusable
                }
            });
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "focused",
                ["value"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["value"] = ie.IsFocused
                }
            });
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "disabled",
                ["value"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["value"] = !ie.IsEnabled
                }
            });
        }

        int posInSet = AutomationProperties.GetPositionInSet(visual);
        if (posInSet > 0)
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "posinset",
                ["value"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["value"] = posInSet
                }
            });
        }

        int sizeOfSet = AutomationProperties.GetSizeOfSet(visual);
        if (sizeOfSet > 0)
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "setsize",
                ["value"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["value"] = sizeOfSet
                }
            });
        }

        var liveSetting = AutomationProperties.GetLiveSetting(visual);
        if (liveSetting != AutomationLiveSetting.Off)
        {
            string liveStr = liveSetting switch
            {
                AutomationLiveSetting.Polite => "polite",
                AutomationLiveSetting.Assertive => "assertive",
                _ => "off"
            };
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "live",
                ["value"] = new JsonObject
                {
                    ["type"] = "token",
                    ["value"] = liveStr
                }
            });
        }

        bool isRequired = AutomationProperties.GetIsRequiredForForm(visual);
        if (isRequired)
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "required",
                ["value"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["value"] = true
                }
            });
        }

        string? acceleratorKey = AutomationProperties.GetAcceleratorKey(visual);
        if (!string.IsNullOrEmpty(acceleratorKey))
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "keyshortcuts",
                ["value"] = new JsonObject
                {
                    ["type"] = "string",
                    ["value"] = acceleratorKey
                }
            });
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

        if (propertiesJson.Count > 0)
        {
            nodeJson["properties"] = propertiesJson;
        }

        return nodeJson;
    }

    private static string? GetControlTextOrContent(Visual visual)
    {
        if (visual is TextBlock textBlock) return textBlock.Text;
        if (visual is TextBox textBox) return textBox.Text;
        
        if (visual is HeaderedContentControl headeredControl)
        {
            if (headeredControl.Content is string str) return str;
            if (headeredControl.Header is string hdrStr) return hdrStr;
        }
        else if (visual is ContentControl contentControl)
        {
            if (contentControl.Content is string str) return str;
        }
        else if (visual is HeaderedItemsControl headeredItemsControl)
        {
            if (headeredItemsControl.Header is string str) return str;
        }

        return null;
    }
}
