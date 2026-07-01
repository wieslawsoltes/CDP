using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CDP.Automation.OS;

namespace Chrome.DevTools.Protocol;

public sealed class OsAutomationCdpSession : IDisposable
{
    private readonly string _windowId;
    private readonly IOsAutomation _automation;
    private readonly Dictionary<int, OSNode> _idToNode = new();
    private readonly Dictionary<string, int> _osIdToCdpId = new();
    private int _nextNodeId = 2;
    private OSNode? _rootNode;

    public event EventHandler<CdpEventEventArgs>? EventReceived;

    public void Dispose()
    {
        StopScreencast();
        StopRecordingPolling();
    }

    private System.Threading.CancellationTokenSource? _pollingCts;
    private string? _lastFocusedId;
    private string? _lastFocusedValue;
    private string? _lastFocusedRole;
    private bool _isSimulatingInput;
    private bool _peerClicked;
    private bool _isInputEnabled;
    private bool _isRecordingActive;
    private string? _lastClickedElementId;
    private DateTime _lastClickTime;

    private bool _isMouseDown;
    private double _mouseDownX;
    private double _mouseDownY;
    private bool _hasMovedSinceDown;
    private string _mouseDownButton = "none";

    private System.Threading.CancellationTokenSource? _screencastCts;
    private byte[]? _lastFrameBytes;
    private int _screencastSessionId;

    private void TriggerSimulateInputGuard()
    {
        _isSimulatingInput = true;
        _ = Task.Delay(500).ContinueWith(_ => _isSimulatingInput = false);
    }

    public OsAutomationCdpSession(string windowId)
    {
        _windowId = windowId;
        _automation = OSAutomationService.Instance;
    }

    public async Task<JsonObject> HandleCommandAsync(string method, JsonObject? parameters)
    {
        parameters ??= new JsonObject();
        var dotIndex = method.IndexOf('.');
        if (dotIndex == -1)
        {
            throw new Exception($"Invalid method format: {method}");
        }

        var domain = method.Substring(0, dotIndex);
        var action = method.Substring(dotIndex + 1);

        return domain switch
        {
            "DOM" => await HandleDomDomainAsync(action, parameters),
            "Input" => await HandleInputDomainAsync(action, parameters),
            "Page" => await HandlePageDomainAsync(action, parameters),
            "SystemInfo" => await HandleSystemInfoDomainAsync(action, parameters),
            "Runtime" => await HandleRuntimeDomainAsync(action, parameters),
            "Target" => await HandleTargetDomainAsync(action, parameters),
            "Accessibility" => await HandleAccessibilityDomainAsync(action, parameters),
            "CSS" => await HandleCssDomainAsync(action, parameters),
            "Overlay" => await HandleOverlayDomainAsync(action, parameters),
            "Recorder" => await HandleRecorderDomainAsync(action, parameters),
            _ => new JsonObject()
        };
    }

    private Task<JsonObject> HandleDomDomainAsync(string action, JsonObject parameters)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return Task.FromResult(new JsonObject());

            case "getDocument":
                {
                    _idToNode.Clear();
                    _osIdToCdpId.Clear();
                    _nextNodeId = 2;

                    _rootNode = _automation.GetElementTree(_windowId);
                    var children = new JsonArray();
                    if (_rootNode != null)
                    {
                        children.Add(BuildCdpNode(_rootNode, 1));
                    }

                    var root = new JsonObject
                    {
                        ["nodeId"] = 1,
                        ["backendNodeId"] = 1,
                        ["nodeType"] = 9, // Document Node
                        ["nodeName"] = "#document",
                        ["localName"] = "",
                        ["nodeValue"] = "",
                        ["childNodeCount"] = children.Count,
                        ["children"] = children,
                        ["documentURL"] = "os://localhost/",
                        ["baseURL"] = "os://localhost/"
                    };

                    return Task.FromResult(new JsonObject { ["root"] = root });
                }

            case "querySelector":
                {
                    int nodeId = parameters["nodeId"]?.GetValue<int>() ?? 0;
                    string selector = parameters["selector"]?.GetValue<string>() ?? "";
                    
                    OSNode? searchRoot = null;
                    if (nodeId == 1)
                    {
                        searchRoot = _rootNode;
                    }
                    else if (_idToNode.TryGetValue(nodeId, out var n))
                    {
                        searchRoot = n;
                    }

                    int matchId = 0;
                    if (searchRoot != null)
                    {
                        var match = QuerySelectorInternal(searchRoot, selector);
                        if (match != null)
                        {
                            if (_osIdToCdpId.TryGetValue(match.Id, out int cdpId))
                            {
                                matchId = cdpId;
                            }
                            else
                            {
                                int newId = _nextNodeId++;
                                _idToNode[newId] = match;
                                _osIdToCdpId[match.Id] = newId;
                                matchId = newId;
                            }
                        }
                    }

                    return Task.FromResult(new JsonObject { ["nodeId"] = matchId });
                }

            case "resolveNode":
                {
                    int nodeId = parameters["nodeId"]?.GetValue<int>() ?? 0;
                    var obj = new JsonObject
                    {
                        ["type"] = "object",
                        ["objectId"] = $"node_{nodeId}"
                    };
                    return Task.FromResult(new JsonObject { ["object"] = obj });
                }

            case "getNodeForLocation":
                {
                    int x = parameters["x"]?.GetValue<int>() ?? 0;
                    int y = parameters["y"]?.GetValue<int>() ?? 0;

                    int matchedNodeId = 0;
                    if (_rootNode != null)
                    {
                        int absX = _rootNode.Bounds.Left + x;
                        int absY = _rootNode.Bounds.Top + y;
                        var match = FindDeepestNodeAt(_rootNode, absX, absY);
                        if (match != null && _osIdToCdpId.TryGetValue(match.Id, out int cdpId))
                        {
                            matchedNodeId = cdpId;
                        }
                    }

                    return Task.FromResult(new JsonObject { ["nodeId"] = matchedNodeId });
                }

            case "getBoxModel":
                {
                    int nodeId = parameters["nodeId"]?.GetValue<int>() ?? 0;
                    if (!_idToNode.TryGetValue(nodeId, out var node))
                    {
                        throw new Exception($"Node with ID {nodeId} not found");
                    }

                    double ox = _rootNode != null ? _rootNode.Bounds.Left : 0.0;
                    double oy = _rootNode != null ? _rootNode.Bounds.Top : 0.0;

                    double x1 = node.Bounds.Left - ox;
                    double y1 = node.Bounds.Top - oy;
                    double x2 = node.Bounds.Right - ox;
                    double y2 = node.Bounds.Top - oy;
                    double x3 = node.Bounds.Right - ox;
                    double y3 = node.Bounds.Bottom - oy;
                    double x4 = node.Bounds.Left - ox;
                    double y4 = node.Bounds.Bottom - oy;

                    var quad = new JsonArray { x1, y1, x2, y2, x3, y3, x4, y4 };
                    var model = new JsonObject
                    {
                        ["content"] = (JsonArray)quad.DeepClone()!,
                        ["padding"] = (JsonArray)quad.DeepClone()!,
                        ["border"] = (JsonArray)quad.DeepClone()!,
                        ["margin"] = (JsonArray)quad.DeepClone()!,
                        ["width"] = node.Bounds.Width,
                        ["height"] = node.Bounds.Height
                    };

                    return Task.FromResult(new JsonObject { ["model"] = model });
                }

            case "focus":
                {
                    int nodeId = parameters["nodeId"]?.GetValue<int>() ?? 0;
                    if (_idToNode.TryGetValue(nodeId, out var node))
                    {
                        TriggerSimulateInputGuard();
                        // Simulate focus by clicking at the center of the element bounds (window-relative)
                        double cx = (node.Bounds.Left + node.Bounds.Width / 2.0) - (_rootNode?.Bounds.Left ?? 0);
                        double cy = (node.Bounds.Top + node.Bounds.Height / 2.0) - (_rootNode?.Bounds.Top ?? 0);
                        _automation.SimulateMouseMove(_windowId, cx, cy);
                        _automation.SimulateClick(_windowId, cx, cy);
                    }
                    return Task.FromResult(new JsonObject());
                }

            default:
                return Task.FromResult(new JsonObject());
        }
    }

    private Task<JsonObject> HandleInputDomainAsync(string action, JsonObject parameters)
    {
        switch (action)
        {
            case "enable":
                _isInputEnabled = true;
                StartRecordingPolling();
                return Task.FromResult(new JsonObject());

            case "disable":
                _isInputEnabled = false;
                if (!_isRecordingActive)
                {
                    StopRecordingPolling();
                }
                return Task.FromResult(new JsonObject());

            case "dispatchMouseEvent":
                {
                    TriggerSimulateInputGuard();
                    string type = parameters["type"]?.GetValue<string>() ?? "";
                    double x = parameters["x"]?.GetValue<double>() ?? 0.0;
                    double y = parameters["y"]?.GetValue<double>() ?? 0.0;
                    string button = parameters["button"]?.GetValue<string>() ?? "none";

                    if (type == "mouseMoved")
                    {
                        if (_isMouseDown && !_hasMovedSinceDown && button != "none")
                        {
                            _hasMovedSinceDown = true;
                            _automation.SimulateMouseDown(_windowId, _mouseDownX, _mouseDownY, _mouseDownButton);
                        }

                        if (button != "none")
                        {
                            _automation.SimulateMouseMove(_windowId, x, y);
                        }
                    }
                    else if (type == "mousePressed")
                    {
                        _isMouseDown = true;
                        _mouseDownX = x;
                        _mouseDownY = y;
                        _mouseDownButton = button;
                        _hasMovedSinceDown = false;
                        _peerClicked = false;

                        if (!_automation.UsePeerAutomation)
                        {
                            _automation.SimulateMouseDown(_windowId, x, y, button);
                        }
                    }
                    else if (type == "mouseReleased")
                    {
                        _isMouseDown = false;

                        if (_pollingCts != null)
                        {
                            try
                            {
                                var rootNode = _automation.GetElementTree(_windowId);
                                if (rootNode != null)
                                {
                                    double absoluteX = rootNode.Bounds.Left + x;
                                    double absoluteY = rootNode.Bounds.Top + y;
                                    var match = FindDeepestNodeAt(rootNode, (int)absoluteX, (int)absoluteY);
                                    if (match != null)
                                    {
                                        RaiseStepRecordedEvent("click", match.Id, fromNativeHook: true);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error recording preview pane click step: {ex.Message}");
                            }
                        }

                        if (_automation.UsePeerAutomation && !_hasMovedSinceDown)
                        {
                            _automation.SimulateClick(_windowId, x, y);
                        }
                        else
                        {
                            _automation.SimulateMouseUp(_windowId, x, y, button);
                        }
                    }
                    else if (type == "mouseWheel")
                    {
                        double deltaX = parameters["deltaX"]?.GetValue<double>() ?? 0.0;
                        double deltaY = parameters["deltaY"]?.GetValue<double>() ?? 0.0;
                        _automation.SimulateMouseWheel(_windowId, x, y, deltaX, deltaY);
                    }

                    EventReceived?.Invoke(this, new CdpEventEventArgs("Input.mouseEvent", new JsonObject
                    {
                        ["type"] = type,
                        ["x"] = x,
                        ["y"] = y,
                        ["button"] = button,
                        ["deltaX"] = parameters["deltaX"]?.GetValue<double>() ?? 0.0,
                        ["deltaY"] = parameters["deltaY"]?.GetValue<double>() ?? 0.0,
                        ["clickCount"] = 1
                    }));

                    return Task.FromResult(new JsonObject());
                }

            case "dispatchKeyEvent":
                {
                    TriggerSimulateInputGuard();
                    string type = parameters["type"]?.GetValue<string>() ?? "";
                    string key = parameters["key"]?.GetValue<string>() ?? "";
                    if (type == "keyDown" || type == "rawKeyDown")
                    {
                        _automation.SimulateKeyPress(_windowId, key);
                    }

                    EventReceived?.Invoke(this, new CdpEventEventArgs("Input.keyEvent", new JsonObject
                    {
                        ["type"] = type,
                        ["key"] = key
                    }));

                    return Task.FromResult(new JsonObject());
                }

            case "insertText":
                {
                    TriggerSimulateInputGuard();
                    string text = parameters["text"]?.GetValue<string>() ?? "";

                    if (_pollingCts != null)
                    {
                        try
                        {
                            var focused = _automation.GetFocusedElement(_windowId);
                            if (focused != null)
                            {
                                RaiseStepRecordedEvent("change", focused.Id, text, fromNativeHook: true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error recording preview pane text change step: {ex.Message}");
                        }
                    }

                    _automation.SimulateTypeText(_windowId, text);

                    EventReceived?.Invoke(this, new CdpEventEventArgs("Input.keyEvent", new JsonObject
                    {
                        ["type"] = "textInput",
                        ["text"] = text
                    }));

                    return Task.FromResult(new JsonObject());
                }

            default:
                return Task.FromResult(new JsonObject());
        }
    }

    private Task<JsonObject> HandlePageDomainAsync(string action, JsonObject parameters)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return Task.FromResult(new JsonObject());

            case "captureScreenshot":
                {
                    byte[] bytes = _automation.CaptureWindow(_windowId);
                    string base64 = Convert.ToBase64String(bytes);
                    return Task.FromResult(new JsonObject { ["data"] = base64 });
                }

            case "startScreencast":
                StartScreencast();
                return Task.FromResult(new JsonObject());

            case "stopScreencast":
                StopScreencast();
                return Task.FromResult(new JsonObject());

            case "screencastFrameAck":
                return Task.FromResult(new JsonObject());

            default:
                return Task.FromResult(new JsonObject());
        }
    }

    private Task<JsonObject> HandleSystemInfoDomainAsync(string action, JsonObject parameters)
    {
        if (action == "getInfo")
        {
            return Task.FromResult(new JsonObject
            {
                ["modelName"] = "OS Automation Bridge",
                ["modelVersion"] = "1.0",
                ["commandLine"] = "N/A"
            });
        }
        return Task.FromResult(new JsonObject());
    }

    private Task<JsonObject> HandleRuntimeDomainAsync(string action, JsonObject parameters)
    {
        if (action == "evaluate")
        {
            string expression = parameters["expression"]?.GetValue<string>() ?? "";
            try
            {
                var val = EvaluateOsDomExpression(expression);
                return Task.FromResult(new JsonObject
                {
                    ["result"] = new JsonObject
                    {
                        ["type"] = val is bool ? "boolean" : "string",
                        ["value"] = val is bool b ? b : val.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new JsonObject
                {
                    ["result"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["subtype"] = "error",
                        ["className"] = ex.GetType().FullName,
                        ["description"] = ex.Message
                    },
                    ["exceptionDetails"] = new JsonObject
                    {
                        ["text"] = ex.Message,
                        ["exception"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["className"] = ex.GetType().FullName,
                            ["description"] = ex.Message
                        }
                    }
                });
            }
        }
        else if (action == "getProperties")
        {
            string objectId = parameters["objectId"]?.GetValue<string>() ?? "";
            var results = new JsonArray();
            if (objectId.StartsWith("node_"))
            {
                if (int.TryParse(objectId.Substring(5), out int nodeId) && _idToNode.TryGetValue(nodeId, out var node))
                {
                    void AddProp(string name, string? val, string type = "string")
                    {
                        results.Add(new JsonObject
                        {
                            ["name"] = name,
                            ["value"] = new JsonObject
                            {
                                ["type"] = type,
                                ["value"] = val ?? ""
                            }
                        });
                    }

                    AddProp("id", node.Id);
                    AddProp("name", node.Name);
                    AddProp("role", node.Role);
                    AddProp("text", node.Text);
                    AddProp("bounds", node.Bounds.ToString());

                    foreach (var kvp in node.Attributes)
                    {
                        AddProp(kvp.Key, kvp.Value);
                    }
                }
            }
            return Task.FromResult(new JsonObject { ["result"] = results });
        }
        return Task.FromResult(new JsonObject());
    }

    private Task<JsonObject> HandleTargetDomainAsync(string action, JsonObject parameters)
    {
        return Task.FromResult(new JsonObject());
    }

    private JsonObject BuildCdpNode(OSNode node, int parentId)
    {
        int nodeId = _nextNodeId++;
        _idToNode[nodeId] = node;
        _osIdToCdpId[node.Id] = nodeId;

        var childrenArray = new JsonArray();
        foreach (var child in node.Children)
        {
            childrenArray.Add(BuildCdpNode(child, nodeId));
        }

        var attributesArray = new JsonArray();
        foreach (var kvp in node.Attributes)
        {
            attributesArray.Add(kvp.Key);
            attributesArray.Add(kvp.Value);
        }

        // Add some default attributes from fields
        if (!string.IsNullOrEmpty(node.Text))
        {
            attributesArray.Add("Text");
            attributesArray.Add(node.Text);
            attributesArray.Add("value");
            attributesArray.Add(node.Text);
        }
        attributesArray.Add("id");
        attributesArray.Add(node.Id);
        attributesArray.Add("Id");
        attributesArray.Add(node.Id);
        attributesArray.Add("Name");
        attributesArray.Add(node.Name);
        attributesArray.Add("Role");
        attributesArray.Add(node.Role);
        attributesArray.Add("AutomationId");
        attributesArray.Add(node.Id);
        attributesArray.Add("AccessibilityId");
        attributesArray.Add(node.Id);

        var cdpNode = new JsonObject
        {
            ["nodeId"] = nodeId,
            ["parentId"] = parentId,
            ["backendNodeId"] = nodeId,
            ["nodeType"] = 1, // Element Node
            ["nodeName"] = node.Name,
            ["localName"] = node.Name,
            ["nodeValue"] = "",
            ["childNodeCount"] = node.Children.Count,
            ["attributes"] = attributesArray
        };

        if (childrenArray.Count > 0)
        {
            cdpNode["children"] = childrenArray;
        }

        return cdpNode;
    }

    private OSNode? QuerySelectorInternal(OSNode root, string selector)
    {
        if (string.IsNullOrEmpty(selector)) return null;

        ReadOnlySpan<char> sel = selector.AsSpan();

        // 1. ID selector: #name or [id="name"] or [AutomationId="name"]
        if (sel.StartsWith("#"))
        {
            var id = sel.Slice(1).ToString();
            if (string.Equals(id, "focused_element", StringComparison.OrdinalIgnoreCase))
            {
                var focused = _automation.GetFocusedElement(_windowId);
                if (focused != null)
                {
                    return focused;
                }
            }
            return FindNodeRecursive(root, n => string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase));
        }
        
        if (sel.StartsWith("[id=") || sel.StartsWith("[Id="))
        {
            var clean = selector.Replace("[id=", "").Replace("[Id=", "").Replace("]", "").Replace("\"", "").Trim();
            if (string.Equals(clean, "focused_element", StringComparison.OrdinalIgnoreCase))
            {
                var focused = _automation.GetFocusedElement(_windowId);
                if (focused != null)
                {
                    return focused;
                }
            }
            return FindNodeRecursive(root, n => string.Equals(n.Id, clean, StringComparison.OrdinalIgnoreCase));
        }

        if (sel.StartsWith("[Name="))
        {
            var clean = selector.Replace("[Name=", "").Replace("]", "").Replace("\"", "").Trim();
            return FindNodeRecursive(root, n => string.Equals(n.Name, clean, StringComparison.OrdinalIgnoreCase));
        }

        if (sel.StartsWith("[AccessibilityId=") || sel.StartsWith("[AutomationId=") || sel.StartsWith("[AutomationProperties.AutomationId="))
        {
            var clean = selector
                .Replace("[AccessibilityId=", "")
                .Replace("[AutomationId=", "")
                .Replace("[AutomationProperties.AutomationId=", "")
                .Replace("]", "")
                .Replace("\"", "")
                .Trim();
            return FindNodeRecursive(root, n => string.Equals(n.Id, clean, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Text selector: [Text="value"] or :contains("value")
        if (sel.Contains("[Text=".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            var clean = selector.Replace("[Text=", "").Replace("]", "").Replace("\"", "").Trim();
            return FindNodeRecursive(root, n => string.Equals(n.Text, clean, StringComparison.OrdinalIgnoreCase));
        }

        if (sel.Contains(":contains(".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            string clean = selector;
            int startIdx = selector.IndexOf(":contains(", StringComparison.OrdinalIgnoreCase);
            if (startIdx >= 0)
            {
                int argStart = startIdx + ":contains(".Length;
                int endIdx = selector.IndexOf(')', argStart);
                if (endIdx >= 0)
                {
                    clean = selector.Substring(argStart, endIdx - argStart);
                }
            }
            clean = clean.Trim('\'', '"', ' ');
            return FindNodeRecursive(root, n => n.Text != null && n.Text.Contains(clean, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Name/Role selector
        return FindNodeRecursive(root, n => string.Equals(n.Name, selector, StringComparison.OrdinalIgnoreCase) || string.Equals(n.Role, selector, StringComparison.OrdinalIgnoreCase));
    }

    private OSNode? FindNodeRecursive(OSNode root, Func<OSNode, bool> predicate)
    {
        if (predicate(root)) return root;
        foreach (var child in root.Children)
        {
            var found = FindNodeRecursive(child, predicate);
            if (found != null) return found;
        }
        return null;
    }

    private Task<JsonObject> HandleAccessibilityDomainAsync(string action, JsonObject parameters)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return Task.FromResult(new JsonObject());

            case "getFullAXTree":
                {
                    var nodes = new JsonArray();
                    if (_rootNode != null)
                    {
                        var list = new List<OSNode>();
                        TraverseOSNodes(_rootNode, list);
                        foreach (var node in list)
                        {
                            nodes.Add(BuildAXNode(node));
                        }
                    }
                    return Task.FromResult(new JsonObject { ["nodes"] = nodes });
                }

            case "getAXNode":
            case "getAXNodeAndAncestors":
            case "getPartialAXTree":
                {
                    int? nodeId = parameters["nodeId"]?.GetValue<int>();
                    OSNode? targetNode = null;
                    if (nodeId.HasValue && _idToNode.TryGetValue(nodeId.Value, out var n))
                    {
                        targetNode = n;
                    }
                    else
                    {
                        targetNode = _rootNode;
                    }

                    var nodes = new JsonArray();
                    if (targetNode != null)
                    {
                        var current = targetNode;
                        while (current != null)
                        {
                            nodes.Add(BuildAXNode(current));
                            current = FindParentOSNode(_rootNode, current);
                        }
                    }
                    return Task.FromResult(new JsonObject { ["nodes"] = nodes });
                }

            default:
                return Task.FromResult(new JsonObject());
        }
    }

    private void TraverseOSNodes(OSNode root, List<OSNode> list)
    {
        list.Add(root);
        foreach (var child in root.Children)
        {
            TraverseOSNodes(child, list);
        }
    }

    private OSNode? FindParentOSNode(OSNode? root, OSNode target)
    {
        if (root == null) return null;
        foreach (var child in root.Children)
        {
            if (child == target) return root;
            var found = FindParentOSNode(child, target);
            if (found != null) return found;
        }
        return null;
    }

    private JsonObject BuildAXNode(OSNode node)
    {
        int domNodeId = _osIdToCdpId.TryGetValue(node.Id, out int cdpId) ? cdpId : 0;
        string axId = "ax_" + node.Id;

        var roleJson = new JsonObject
        {
            ["type"] = "role",
            ["value"] = node.Role
        };

        var nameJson = new JsonObject
        {
            ["type"] = "string",
            ["value"] = node.Text
        };

        var propertiesJson = new JsonArray();
        propertiesJson.Add(new JsonObject
        {
            ["name"] = "focusable",
            ["value"] = new JsonObject
            {
                ["type"] = "boolean",
                ["value"] = true
            }
        });
        propertiesJson.Add(new JsonObject
        {
            ["name"] = "focused",
            ["value"] = new JsonObject
            {
                ["type"] = "boolean",
                ["value"] = false
            }
        });
        propertiesJson.Add(new JsonObject
        {
            ["name"] = "disabled",
            ["value"] = new JsonObject
            {
                ["type"] = "boolean",
                ["value"] = false
            }
        });

        if (!string.IsNullOrEmpty(node.Text))
        {
            propertiesJson.Add(new JsonObject
            {
                ["name"] = "value",
                ["value"] = new JsonObject
                {
                    ["type"] = "string",
                    ["value"] = node.Text
                }
            });
        }

        var nodeJson = new JsonObject
        {
            ["nodeId"] = axId,
            ["ignored"] = false,
            ["role"] = roleJson,
            ["name"] = nameJson
        };

        if (domNodeId > 0)
        {
            nodeJson["backendDOMNodeId"] = domNodeId;
        }

        var parentNode = FindParentOSNode(_rootNode, node);
        if (parentNode != null)
        {
            nodeJson["parentId"] = "ax_" + parentNode.Id;
        }

        var childIds = new JsonArray();
        foreach (var child in node.Children)
        {
            childIds.Add("ax_" + child.Id);
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

    private Task<JsonObject> HandleCssDomainAsync(string action, JsonObject parameters)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return Task.FromResult(new JsonObject());

            case "getComputedStyleForNode":
                {
                    int nodeId = parameters["nodeId"]?.GetValue<int>() ?? 0;
                    var style = new JsonArray();

                    if (_idToNode.TryGetValue(nodeId, out var node))
                    {
                        style.Add(new JsonObject { ["name"] = "width", ["value"] = $"{node.Bounds.Width}px" });
                        style.Add(new JsonObject { ["name"] = "height", ["value"] = $"{node.Bounds.Height}px" });
                        style.Add(new JsonObject { ["name"] = "visibility", ["value"] = "visible" });
                        style.Add(new JsonObject { ["name"] = "display", ["value"] = "block" });
                    }

                    return Task.FromResult(new JsonObject { ["computedStyle"] = style });
                }

            default:
                return Task.FromResult(new JsonObject());
        }
    }

    private OSNode? FindDeepestNodeAt(OSNode root, int x, int y)
    {
        if (x < root.Bounds.Left || x > root.Bounds.Right || y < root.Bounds.Top || y > root.Bounds.Bottom)
        {
            return null;
        }

        foreach (var child in root.Children)
        {
            var found = FindDeepestNodeAt(child, x, y);
            if (found != null)
            {
                return found;
            }
        }

        return root;
    }

    private Task<JsonObject> HandleOverlayDomainAsync(string action, JsonObject parameters)
    {
        return Task.FromResult(new JsonObject());
    }

    private Task<JsonObject> HandleRecorderDomainAsync(string action, JsonObject parameters)
    {
        switch (action)
        {
            case "start":
                _isRecordingActive = true;
                StartRecordingPolling();
                return Task.FromResult(new JsonObject());

            case "stop":
                _isRecordingActive = false;
                if (!_isInputEnabled)
                {
                    StopRecordingPolling();
                }
                return Task.FromResult(new JsonObject());

            default:
                return Task.FromResult(new JsonObject());
        }
    }

    private void StartRecordingPolling()
    {
        StopRecordingPolling();
        _pollingCts = new System.Threading.CancellationTokenSource();
        var token = _pollingCts.Token;

        _automation.StartInputCapture(_windowId, (x, y, button) =>
        {
            if (_isSimulatingInput) return;

            double relX = x;
            double relY = y;
            _rootNode = _automation.GetElementTree(_windowId);
            if (_rootNode != null)
            {
                relX = x - _rootNode.Bounds.Left;
                relY = y - _rootNode.Bounds.Top;
            }

            EventReceived?.Invoke(this, new CdpEventEventArgs("Input.mouseEvent", new JsonObject
            {
                ["type"] = "mousePressed",
                ["x"] = relX,
                ["y"] = relY,
                ["button"] = button,
                ["clickCount"] = 1
            }));
            EventReceived?.Invoke(this, new CdpEventEventArgs("Input.mouseEvent", new JsonObject
            {
                ["type"] = "mouseReleased",
                ["x"] = relX,
                ["y"] = relY,
                ["button"] = button,
                ["clickCount"] = 1
            }));

            if (_rootNode != null)
            {
                var match = FindDeepestNodeAt(_rootNode, (int)x, (int)y);
                if (match != null)
                {
                    RaiseStepRecordedEvent("click", match.Id, fromNativeHook: true);
                }
            }
        }, (eventType, elementId, value) =>
        {
            if (_isSimulatingInput) return;

            if (eventType == "change")
            {
                EventReceived?.Invoke(this, new CdpEventEventArgs("Input.keyEvent", new JsonObject
                {
                    ["type"] = "textInput",
                    ["text"] = value ?? ""
                }));
            }

            if (eventType == "focus")
            {
                if (elementId != _lastFocusedId)
                {
                    _lastFocusedId = elementId;
                    _lastFocusedValue = value;
                }
            }
            else if (eventType == "change")
            {
                if (elementId == _lastFocusedId)
                {
                    if (value != _lastFocusedValue)
                    {
                        _lastFocusedValue = value;
                        RaiseStepRecordedEvent("change", elementId, value, fromNativeHook: true);
                    }
                }
                else
                {
                    _lastFocusedId = elementId;
                    _lastFocusedValue = value;
                    RaiseStepRecordedEvent("change", elementId, value, fromNativeHook: true);
                }
            }
        });

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(200, token);
                    if (_isSimulatingInput) continue;

                    var focused = _automation.GetFocusedElement(_windowId);
                    if (focused != null)
                    {
                        if (focused.Role == "AXButton" || focused.Role == "AXCheckBox" || focused.Role == "AXRadioButton" || focused.Role == "AXMenuItem" || focused.Role == "AXPopUpButton" || focused.Role == "button" || focused.Role == "check box" || focused.Role == "radio button")
                        {
                            if (focused.Id != _lastFocusedId)
                            {
                                _lastFocusedId = focused.Id;
                                _lastFocusedValue = focused.Text;
                                _lastFocusedRole = focused.Role;

                                RaiseStepRecordedEvent("click", focused.Id);
                            }
                        }
                        else if (focused.Role == "AXTextField" || focused.Role == "AXTextArea" || focused.Role == "AXComboBox" || focused.Role == "Edit" || focused.Role == "AXScrollArea" || focused.Role == "edit" || focused.Role == "combo box")
                        {
                            if (focused.Id != _lastFocusedId)
                            {
                                _lastFocusedId = focused.Id;
                                _lastFocusedValue = focused.Text;
                                _lastFocusedRole = focused.Role;
                            }
                            else if (focused.Text != _lastFocusedValue)
                            {
                                _lastFocusedValue = focused.Text;
                                RaiseStepRecordedEvent("change", focused.Id, focused.Text);
                            }
                        }
                    }
                }
                catch (TaskCanceledException) {}
                catch (Exception ex)
                {
                    Console.WriteLine($"Error polling focused element: {ex.Message}");
                }
            }
        }, token);
    }

    private void StopRecordingPolling()
    {
        _automation.StopInputCapture();
        _pollingCts?.Cancel();
        _pollingCts = null;
        _lastFocusedId = null;
        _lastFocusedValue = null;
        _lastFocusedRole = null;
    }

    private void RaiseStepRecordedEvent(string type, string elementId, string? value = null, bool fromNativeHook = false)
    {
        if (!_isRecordingActive) return;

        if (type == "click")
        {
            _lastClickedElementId = elementId;
            _lastClickTime = DateTime.UtcNow;
        }
        else if (type == "change")
        {
            if (_lastClickedElementId != null && _lastClickedElementId != elementId)
            {
                if ((DateTime.UtcNow - _lastClickTime).TotalMilliseconds < 1000)
                {
                    Console.WriteLine($"[DEBUG OS AUTOMATION] Suppressing programmatic change step on '{elementId}' due to recent click on '{_lastClickedElementId}'.");
                    return;
                }
            }
        }

        var step = new JsonObject
        {
            ["type"] = type,
            ["selectors"] = new JsonArray { new JsonArray { JsonValue.Create($"#{elementId}") } }
        };
        if (value != null)
        {
            step["value"] = value;
        }

        var eventParams = new JsonObject
        {
            ["step"] = step
        };

        EventReceived?.Invoke(this, new CdpEventEventArgs("Recorder.stepAdded", eventParams));

        if (!fromNativeHook)
        {
            if (type == "click")
            {
                EventReceived?.Invoke(this, new CdpEventEventArgs("Input.mouseEvent", new JsonObject
                {
                    ["type"] = "mousePressed",
                    ["x"] = 0.0,
                    ["y"] = 0.0,
                    ["button"] = "left",
                    ["clickCount"] = 1
                }));
                EventReceived?.Invoke(this, new CdpEventEventArgs("Input.mouseEvent", new JsonObject
                {
                    ["type"] = "mouseReleased",
                    ["x"] = 0.0,
                    ["y"] = 0.0,
                    ["button"] = "left",
                    ["clickCount"] = 1
                }));
            }
            else if (type == "change")
            {
                EventReceived?.Invoke(this, new CdpEventEventArgs("Input.keyEvent", new JsonObject
                {
                    ["type"] = "textInput",
                    ["text"] = value ?? ""
                }));
            }
        }
    }

    private void StartScreencast()
    {
        StopScreencast();
        _screencastCts = new System.Threading.CancellationTokenSource();
        var token = _screencastCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(250, token);

                    byte[] newBytes = _automation.CaptureWindow(_windowId);
                    if (newBytes != null && newBytes.Length > 0)
                    {
                        bool changed = _lastFrameBytes == null || !CompareBytes(_lastFrameBytes, newBytes);
                        if (changed)
                        {
                            _lastFrameBytes = newBytes;

                            double deviceWidth = 800.0;
                            double deviceHeight = 600.0;
                            try
                            {
                                var windows = _automation.GetWindows();
                                int pid = 0;
                                int.TryParse(_windowId, out pid);
                                foreach (var w in windows)
                                {
                                     if (w.Id == _windowId || (pid > 0 && w.ProcessId == pid))
                                     {
                                         deviceWidth = w.Bounds.Width;
                                         deviceHeight = w.Bounds.Height;
                                         break;
                                     }
                                }
                            }
                            catch {}

                            var metadata = new JsonObject
                            {
                                ["offsetTop"] = 0.0,
                                ["pageScaleFactor"] = 1.0,
                                ["deviceWidth"] = deviceWidth,
                                ["deviceHeight"] = deviceHeight,
                                ["scrollOffsetX"] = 0.0,
                                ["scrollOffsetY"] = 0.0,
                                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0
                            };

                            var frameParams = new JsonObject
                            {
                                ["data"] = Convert.ToBase64String(newBytes),
                                ["metadata"] = metadata,
                                ["sessionId"] = _screencastSessionId++
                            };

                            EventReceived?.Invoke(this, new CdpEventEventArgs("Page.screencastFrame", frameParams));
                        }
                    }
                }
                catch (TaskCanceledException) {}
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in screencast: {ex.Message}");
                }
            }
        }, token);
    }

    private void StopScreencast()
    {
        _screencastCts?.Cancel();
        _screencastCts = null;
        _lastFrameBytes = null;
    }

    private bool CompareBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private object EvaluateOsDomExpression(string expression)
    {
        expression = expression.Trim();
        if (_rootNode == null)
        {
            throw new InvalidOperationException("No DOM document available to evaluate expression.");
        }

        // Handle: document.querySelector("...") != null
        if (expression.Contains("document.querySelector") && (expression.Contains("!= null") || expression.Contains("!== null")))
        {
            var selector = ExtractSelectorFromExpression(expression);
            var node = QuerySelectorInternal(_rootNode, selector);
            return node != null;
        }

        // Handle: document.querySelector("...") == null
        if (expression.Contains("document.querySelector") && (expression.Contains("== null") || expression.Contains("=== null")))
        {
            var selector = ExtractSelectorFromExpression(expression);
            var node = QuerySelectorInternal(_rootNode, selector);
            return node == null;
        }

        // Handle: document.querySelector("...") directly (exists check)
        if (expression.StartsWith("document.querySelector(") && expression.EndsWith(")"))
        {
            var selector = ExtractSelectorFromExpression(expression);
            var node = QuerySelectorInternal(_rootNode, selector);
            return node != null ? "Element" : "null";
        }

        throw new NotSupportedException($"Expression evaluation is not supported in OS Automation mode: '{expression}'");
    }

    private string ExtractSelectorFromExpression(string expression)
    {
        int startIdx = expression.IndexOf("document.querySelector(");
        if (startIdx >= 0)
        {
            int openQuote = expression.IndexOf('\"', startIdx);
            if (openQuote < 0) openQuote = expression.IndexOf('\'', startIdx);
            if (openQuote >= 0)
            {
                int closeQuote = expression.IndexOf(expression[openQuote], openQuote + 1);
                if (closeQuote >= 0)
                {
                    return expression.Substring(openQuote + 1, closeQuote - openQuote - 1);
                }
            }
        }
        return "";
    }
}
