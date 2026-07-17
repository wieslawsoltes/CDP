using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CDP.Automation.OS;
using Microsoft.Extensions.Logging;
using Jint;
using Jint.Runtime;

namespace Chrome.DevTools.Protocol;

public sealed class OsAutomationCdpSession : IDisposable
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<OsAutomationCdpSession>();

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
        StopPerformanceLoop();
        StopNetworkSimulation();
    }

    private System.Threading.CancellationTokenSource? _pollingCts;
    private string? _lastFocusedId;
    private string? _lastFocusedNodeId;
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

    // Performance Domain State
    private System.Threading.CancellationTokenSource? _performanceCts;
    private DateTime _lastCpuTime = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
    private long _lastPrivateBytes;
    private int _layoutCount;

    // Network Domain State
    private bool _isNetworkEnabled;
    private bool _networkOffline;
    private double _networkLatency;
    private List<string> _blockedUrls = new();
    private System.Threading.CancellationTokenSource? _networkCts;
    private readonly Dictionary<string, string> _networkResponseBodies = new(StringComparer.OrdinalIgnoreCase);

    private void TriggerSimulateInputGuard()
    {
        _isSimulatingInput = true;
        _ = Task.Delay(500).ContinueWith(_ => _isSimulatingInput = false);
    }

    public OsAutomationCdpSession(string windowId)
    {
        _windowId = windowId;
        _automation = OsAutomationProvider.Instance ?? throw new Exception("OsAutomationProvider.Instance is not registered.");
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
            "Performance" => await HandlePerformanceDomainAsync(action, parameters),
            "Memory" => await HandleMemoryDomainAsync(action, parameters),
            "Network" => await HandleNetworkDomainAsync(action, parameters),
            "Browser" => await HandleBrowserDomainAsync(action, parameters),
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
                    RefreshRootNode();
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
                    RefreshRootNode();
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
                    RefreshRootNode();
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
                    RefreshRootNode();
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
                    RefreshRootNode();
                    int nodeId = parameters["nodeId"]?.GetValue<int>() ?? 0;
                    if (_idToNode.TryGetValue(nodeId, out var node))
                    {
                        TriggerSimulateInputGuard();
                        _lastFocusedNodeId = node.Id;
                        // Simulate focus by moving the cursor to the center of the element bounds (window-relative)
                        double cx = (node.Bounds.Left + node.Bounds.Width / 2.0) - (_rootNode?.Bounds.Left ?? 0);
                        double cy = (node.Bounds.Top + node.Bounds.Height / 2.0) - (_rootNode?.Bounds.Top ?? 0);
                        _automation.SimulateMouseMove(_windowId, cx, cy);
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
                    double x = GetDouble(parameters["x"]);
                    double y = GetDouble(parameters["y"]);
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
                                Logger.LogErrorMessage("OsAutomation", "Error recording preview pane click step", ex);
                            }
                        }

                        if (_automation.UsePeerAutomation && !_hasMovedSinceDown)
                        {
                            string? nodeId = _lastFocusedNodeId;
                            _lastFocusedNodeId = null;

                            if (string.IsNullOrEmpty(nodeId))
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
                                            nodeId = match.Id;
                                        }
                                    }
                                }
                                catch {}
                            }

                            if (!string.IsNullOrEmpty(nodeId))
                            {
                                _lastFocusedId = nodeId;
                            }
                            _automation.SimulateClick(_windowId, x, y, nodeId);
                        }
                        else
                        {
                            _automation.SimulateMouseUp(_windowId, x, y, button);
                        }

                        if (_isNetworkEnabled)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    string nodeIdStr = "";
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
                                                nodeIdStr = match.Id;
                                            }
                                        }
                                    }
                                    catch {}
                                    string procName = GetTestedProcessName();
                                    await SimulateNetworkRequestAsync(
                                        $"https://api.{procName.ToLowerInvariant()}.local/actions/click{(string.IsNullOrEmpty(nodeIdStr) ? "" : $"?nodeId={nodeIdStr}")}", 
                                        "POST", 
                                        "XHR", 
                                        "{\"status\":\"success\",\"action\":\"click\"}");
                                }
                                catch {}
                            });
                        }
                    }
                    else if (type == "mouseWheel")
                    {
                        double deltaX = GetDouble(parameters["deltaX"]);
                        double deltaY = GetDouble(parameters["deltaY"]);
                        _automation.SimulateMouseWheel(_windowId, x, y, deltaX, deltaY);
                    }

                    EventReceived?.Invoke(this, new CdpEventEventArgs("Input.mouseEvent", new JsonObject
                    {
                        ["type"] = type,
                        ["x"] = x,
                        ["y"] = y,
                        ["button"] = button,
                        ["deltaX"] = GetDouble(parameters["deltaX"]),
                        ["deltaY"] = GetDouble(parameters["deltaY"]),
                        ["clickCount"] = 1
                    }));

                    return Task.FromResult(new JsonObject());
                }

            case "dispatchKeyEvent":
                {
                    TriggerSimulateInputGuard();
                    string type = parameters["type"]?.GetValue<string>() ?? "";
                    string key = parameters["key"]?.GetValue<string>() ?? "";
                    int modifiers = parameters["modifiers"]?.GetValue<int>() ?? 0;
                    if (type == "keyDown" || type == "rawKeyDown")
                    {
                        _automation.SimulateKeyPress(_windowId, key, modifiers);
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
                            Logger.LogErrorMessage("OsAutomation", "Error recording preview pane text change step", ex);
                        }
                    }

                    _automation.SimulateTypeText(_windowId, text);

                    EventReceived?.Invoke(this, new CdpEventEventArgs("Input.keyEvent", new JsonObject
                    {
                        ["type"] = "textInput",
                        ["text"] = text
                    }));

                    if (_isNetworkEnabled)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string procName = GetTestedProcessName();
                                await SimulateNetworkRequestAsync(
                                    $"https://api.{procName.ToLowerInvariant()}.local/actions/type", 
                                    "POST", 
                                    "XHR", 
                                    "{\"status\":\"success\",\"action\":\"type\"}");
                            }
                            catch {}
                        });
                    }

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

            case "bringToFront":
                _automation.BringToFront(_windowId);
                return Task.FromResult(new JsonObject());

            default:
                return Task.FromResult(new JsonObject());
        }
    }

    private async Task<JsonObject> HandleBrowserDomainAsync(string action, JsonObject parameters)
    {
        switch (action)
        {
            case "close":
                try
                {
                    if (_windowId == "macos-window-fallback" ||
                        _windowId == "windows-window-fallback" ||
                        _windowId == "linux-window-fallback")
                    {
                        return new JsonObject();
                    }

                    var windows = _automation.GetWindows();
                    var targetWin = windows.FirstOrDefault(w => w.Id == _windowId);
                    if (targetWin != null)
                    {
                        using var proc = Process.GetProcessById(targetWin.ProcessId);
                        proc.Kill();
                    }
                }
                catch {}
                return new JsonObject();

            case "getVersion":
                {
                    string osVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
                    return new JsonObject
                    {
                        ["protocolVersion"] = "1.3",
                        ["product"] = $"OSAutomationBridge/{osVersion}",
                        ["revision"] = "1.0",
                        ["userAgent"] = "OSAutomationBridge/1.0",
                        ["jsVersion"] = "N/A"
                    };
                }

            default:
                return new JsonObject();
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
        else if (action == "getProcessInfo")
        {
            int pid = 0;
            try
            {
                var win = _automation.GetWindows().FirstOrDefault(w => w.Id == _windowId);
                if (win != null)
                {
                    pid = win.ProcessId;
                }
            }
            catch {}

            if (pid == 0)
            {
                pid = Process.GetCurrentProcess().Id; // Fallback
            }

            var procInfo = new JsonObject
            {
                ["id"] = pid,
                ["type"] = "browser",
                ["cpuTime"] = 0.0
            };
            return Task.FromResult(new JsonObject
            {
                ["processInfo"] = new JsonArray { procInfo }
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
                JsonObject result;
                if (val == null)
                {
                    result = new JsonObject
                    {
                        ["type"] = "object",
                        ["subtype"] = "null",
                        ["value"] = null
                    };
                }
                else if (val is bool b)
                {
                    result = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["value"] = b
                    };
                }
                else if (val is int || val is double || val is float || val is long || val is short || val is decimal)
                {
                    result = new JsonObject
                    {
                        ["type"] = "number",
                        ["value"] = JsonValue.Create(val)
                    };
                }
                else if (val is CdpOsRuntimeElement elem)
                {
                    int cdpId = 0;
                    if (!_osIdToCdpId.TryGetValue(elem.Node.Id, out cdpId))
                    {
                        cdpId = _nextNodeId++;
                        _osIdToCdpId[elem.Node.Id] = cdpId;
                        _idToNode[cdpId] = elem.Node;
                    }
                    result = new JsonObject
                    {
                        ["type"] = "object",
                        ["className"] = "Object",
                        ["description"] = "CdpOsRuntimeElement",
                        ["objectId"] = $"node_{cdpId}"
                    };
                }
                else
                {
                    result = new JsonObject
                    {
                        ["type"] = "string",
                        ["value"] = val.ToString()
                    };
                }

                return Task.FromResult(new JsonObject
                {
                    ["result"] = result
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
            RefreshRootNode();
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
        else if (action == "callFunctionOn")
        {
            RefreshRootNode();
            string objectId = parameters["objectId"]?.GetValue<string>() ?? "";
            string value = "";
            if (objectId.StartsWith("node_"))
            {
                if (int.TryParse(objectId.Substring(5), out int nodeId) && _idToNode.TryGetValue(nodeId, out var node))
                {
                    value = node.Text ?? node.Name ?? "";
                }
            }
            return Task.FromResult(new JsonObject
            {
                ["result"] = new JsonObject
                {
                    ["type"] = "string",
                    ["value"] = value
                }
            });
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

    private void RefreshRootNode()
    {
        var newRoot = _automation.GetElementTree(_windowId);
        if (newRoot == null) return;
        
        var newIdMap = new Dictionary<string, OSNode>(StringComparer.OrdinalIgnoreCase);
        var list = new List<OSNode>();
        TraverseOSNodes(newRoot, list);
        foreach (var node in list)
        {
            newIdMap[node.Id] = node;
        }

        var keys = _idToNode.Keys.ToList();
        foreach (var key in keys)
        {
            var oldNode = _idToNode[key];
            if (newIdMap.TryGetValue(oldNode.Id, out var newNode))
            {
                _idToNode[key] = newNode;
            }
        }

        _rootNode = newRoot;
    }

    private bool MatchNodeAttributes(OSNode node, List<(string Name, string? Value)> predicates)
    {
        foreach (var pred in predicates)
        {
            var name = pred.Name;
            var val = pred.Value;

            if (val == null) // Presence Check
            {
                if (name.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Content", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Header", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Value", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(node.Text)) return false;
                }
                else if (name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(node.Name)) return false;
                }
                else if (name.Equals("Role", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(node.Role)) return false;
                }
                else if (name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                         name.Equals("AutomationId", StringComparison.OrdinalIgnoreCase) ||
                         name.Equals("AccessibilityId", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(node.Id)) return false;
                }
                else
                {
                    if (!node.Attributes.ContainsKey(name)) return false;
                }
            }
            else // Value Check
            {
                if (name.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Content", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Header", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Value", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(node.Text, val, StringComparison.OrdinalIgnoreCase)) return false;
                }
                else if (name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(node.Name, val, StringComparison.OrdinalIgnoreCase)) return false;
                }
                else if (name.Equals("Role", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(node.Role, val, StringComparison.OrdinalIgnoreCase)) return false;
                }
                else if (name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                         name.Equals("AutomationId", StringComparison.OrdinalIgnoreCase) ||
                         name.Equals("AccessibilityId", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(node.Id, val, StringComparison.OrdinalIgnoreCase)) return false;
                }
                else if (name.Equals("IsEnabled", StringComparison.OrdinalIgnoreCase))
                {
                    bool expected = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (!expected) return false; // Default to true in OS mode
                }
                else if (name.Equals("IsVisible", StringComparison.OrdinalIgnoreCase))
                {
                    bool expected = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (!expected) return false; // Default to true
                }
                else if (name.Equals("IsFocused", StringComparison.OrdinalIgnoreCase))
                {
                    var focused = _automation.GetFocusedElement(_windowId);
                    bool isFocused = (focused != null && string.Equals(focused.Id, node.Id, StringComparison.OrdinalIgnoreCase)) || string.Equals(node.Id, _lastFocusedId, StringComparison.OrdinalIgnoreCase);
                    bool expected = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (isFocused != expected) return false;
                }
                else if (name.Equals("IsChecked", StringComparison.OrdinalIgnoreCase))
                {
                    bool isChecked = false;
                    if (node.Role == "AXCheckBox" || node.Role == "AXRadioButton" || node.Role == "check box" || node.Role == "radio button")
                    {
                        isChecked = (node.Text == "1" || node.Text.Equals("true", StringComparison.OrdinalIgnoreCase));
                    }
                    bool expected = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (isChecked != expected) return false;
                }
                else if (name.Equals("IsSelected", StringComparison.OrdinalIgnoreCase))
                {
                    bool expected = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (expected) return false; // Default to false
                }
                else if (name.Equals("IsExpanded", StringComparison.OrdinalIgnoreCase))
                {
                    bool expected = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (expected) return false; // Default to false
                }
                else
                {
                    if (!node.Attributes.TryGetValue(name, out var actualVal) || !string.Equals(actualVal, val, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private OSNode? QuerySelectorInternal(OSNode root, string selector)
    {
        if (string.IsNullOrEmpty(selector)) return null;

        // Handle legacy :contains selector first
        if (selector.Contains(":contains(", StringComparison.OrdinalIgnoreCase))
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

        // Parse attributes and base selector
        var attributePredicates = new List<(string Name, string? Value)>();
        int bracketStart = selector.IndexOf('[');
        string baseSelector = selector;
        if (bracketStart >= 0)
        {
            baseSelector = selector.Substring(0, bracketStart).Trim();
            int searchIdx = bracketStart;
            while (true)
            {
                int open = selector.IndexOf('[', searchIdx);
                if (open < 0) break;
                int close = selector.IndexOf(']', open);
                if (close < 0) break;
                
                string content = selector.Substring(open + 1, close - open - 1);
                int eq = content.IndexOf('=');
                if (eq >= 0)
                {
                    string attrName = content.Substring(0, eq).Trim();
                    string attrVal = content.Substring(eq + 1).Trim('\'', '\"', ' ');
                    attributePredicates.Add((attrName, attrVal));
                }
                else
                {
                    string attrName = content.Trim();
                    attributePredicates.Add((attrName, null));
                }
                searchIdx = close + 1;
            }
        }

        string baseId = "";
        string baseRoleOrName = "";
        int hashIdx = baseSelector.IndexOf('#');
        if (hashIdx >= 0)
        {
            baseRoleOrName = baseSelector.Substring(0, hashIdx).Trim();
            baseId = baseSelector.Substring(hashIdx + 1).Trim();
        }
        else
        {
            if (baseSelector.StartsWith("#"))
            {
                baseId = baseSelector.Substring(1).Trim();
            }
            else
            {
                baseRoleOrName = baseSelector;
            }
        }

        // Handle focused_element fallback in baseId
        if (string.Equals(baseId, "focused_element", StringComparison.OrdinalIgnoreCase))
        {
            var focused = _automation.GetFocusedElement(_windowId);
            if (focused != null)
            {
                if (MatchNodeAttributes(focused, attributePredicates))
                {
                    return focused;
                }
            }
        }

        // Recursive search for a matching node
        return FindNodeRecursive(root, node =>
        {
            // Match Base Selector ID
            if (!string.IsNullOrEmpty(baseId))
            {
                if (!string.Equals(node.Id, baseId, StringComparison.OrdinalIgnoreCase)) return false;
            }

            // Match Base Selector Role or Name
            if (!string.IsNullOrEmpty(baseRoleOrName))
            {
                string roleClean = node.Role;
                if (roleClean.StartsWith("AX", StringComparison.OrdinalIgnoreCase))
                {
                    roleClean = roleClean.Substring(2);
                }

                if (!string.Equals(roleClean, baseRoleOrName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(node.Role, baseRoleOrName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(node.Name, baseRoleOrName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Match all attribute predicates
            return MatchNodeAttributes(node, attributePredicates);
        });
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

            string clickSelector = "";
            if (_rootNode != null)
            {
                var match = FindDeepestNodeAt(_rootNode, (int)x, (int)y);
                if (match != null)
                {
                    clickSelector = $"#{match.Id}";
                    _lastFocusedId = match.Id;
                    RaiseStepRecordedEvent("click", match.Id, fromNativeHook: true);
                }
            }

            EventReceived?.Invoke(this, new CdpEventEventArgs("Input.mouseEvent", new JsonObject
            {
                ["type"] = "mousePressed",
                ["x"] = relX,
                ["y"] = relY,
                ["button"] = button,
                ["clickCount"] = 1,
                ["selector"] = clickSelector
            }));
            EventReceived?.Invoke(this, new CdpEventEventArgs("Input.mouseEvent", new JsonObject
            {
                ["type"] = "mouseReleased",
                ["x"] = relX,
                ["y"] = relY,
                ["button"] = button,
                ["clickCount"] = 1,
                ["selector"] = clickSelector
            }));
        }, (eventType, elementId, value) =>
        {
            if (_isSimulatingInput) return;

            if (eventType == "change")
            {
                EventReceived?.Invoke(this, new CdpEventEventArgs("Input.keyEvent", new JsonObject
                {
                    ["type"] = "textInput",
                    ["text"] = value ?? "",
                    ["selector"] = $"#{elementId}"
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
                    Logger.LogErrorMessage("OsAutomation", "Error polling focused element", ex);
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
                    Logger.LogDebug("Suppressing programmatic change step on '{ElementId}' due to recent click on '{LastClickedElementId}'", elementId, _lastClickedElementId);
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
                    ["clickCount"] = 1,
                    ["selector"] = $"#{elementId}"
                }));
                EventReceived?.Invoke(this, new CdpEventEventArgs("Input.mouseEvent", new JsonObject
                {
                    ["type"] = "mouseReleased",
                    ["x"] = 0.0,
                    ["y"] = 0.0,
                    ["button"] = "left",
                    ["clickCount"] = 1,
                    ["selector"] = $"#{elementId}"
                }));
            }
            else if (type == "change")
            {
                EventReceived?.Invoke(this, new CdpEventEventArgs("Input.keyEvent", new JsonObject
                {
                    ["type"] = "textInput",
                    ["text"] = value ?? "",
                    ["selector"] = $"#{elementId}"
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
                    Logger.LogScreencastError("Error in screencast", ex);
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
        RefreshRootNode();
        if (_rootNode == null)
        {
            throw new InvalidOperationException("No DOM document available to evaluate expression.");
        }

        var engine = new Engine(options =>
        {
            options.Interop.TypeResolver = new Jint.Runtime.Interop.TypeResolver
            {
                MemberNameCreator = member => 
                {
                    var name = member.Name;
                    if (string.IsNullOrEmpty(name)) return new[] { name };
                    var camel = char.ToLowerInvariant(name[0]) + name.Substring(1);
                    return name == camel ? new[] { name } : new[] { name, camel };
                }
            };
            options.TimeoutInterval(TimeSpan.FromSeconds(5));
        });

        engine.SetValue("window", new CdpOsRuntimeWindow(this));
        engine.SetValue("document", new CdpOsRuntimeDocument(this));

        try
        {
            var jsVal = engine.Evaluate(expression);
            return jsVal.ToObject();
        }
        catch (Jint.Runtime.JavaScriptException ex)
        {
            throw new InvalidOperationException($"JS evaluation error: {ex.Message}", ex);
        }
    }

    private string ExtractSelectorFromExpression(string expression)
    {
        int openQuote = expression.IndexOf('\"');
        char quoteChar = '\"';
        if (openQuote < 0)
        {
            openQuote = expression.IndexOf('\'');
            quoteChar = '\'';
        }
        
        if (openQuote >= 0)
        {
            int closeQuote = -1;
            bool escaped = false;
            for (int i = openQuote + 1; i < expression.Length; i++)
            {
                char c = expression[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (c == quoteChar)
                {
                    closeQuote = i;
                    break;
                }
            }

            if (closeQuote >= 0)
            {
                string raw = expression.Substring(openQuote + 1, closeQuote - openQuote - 1);
                return raw.Replace("\\\"", "\"").Replace("\\'", "'");
            }
        }
        return "";
    }

    private static double GetDouble(JsonNode? node)
    {
        if (node == null) return 0.0;
        if (node is JsonValue jsonVal)
        {
            if (jsonVal.TryGetValue<double>(out double d)) return d;
            if (jsonVal.TryGetValue<int>(out int i)) return i;
            if (jsonVal.TryGetValue<long>(out long l)) return l;
            if (jsonVal.TryGetValue<float>(out float f)) return f;
        }
        return 0.0;
    }

    private async Task<JsonObject> HandlePerformanceDomainAsync(string action, JsonObject parameters)
    {
        switch (action)
        {
            case "enable":
                StartPerformanceLoop();
                return new JsonObject();

            case "disable":
                StopPerformanceLoop();
                return new JsonObject();

            case "getMetrics":
                var metrics = await GetPerformanceMetricsAsync();
                return new JsonObject
                {
                    ["metrics"] = metrics
                };

            default:
                return new JsonObject();
        }
    }

    private void PopulateOrUpdateRootNode()
    {
        try
        {
            var freshNode = _automation.GetElementTree(_windowId);
            if (freshNode != null)
            {
                _rootNode = freshNode;
            }
        }
        catch {}
    }

    private async Task<JsonArray> GetPerformanceMetricsAsync()
    {
        PopulateOrUpdateRootNode();
        double timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        int nodesCount = CountNodes(_rootNode);

        double jsHeapUsedSize = 0.0;
        double jsHeapTotalSize = 0.0;
        double cpuUsage = 0.0;
        double memoryAllocations = 0.0;

        try
        {
            var windows = _automation.GetWindows();
            var targetWin = windows.FirstOrDefault(w => w.Id == _windowId);
            if (targetWin != null)
            {
                var nativeMetrics = _automation.GetProcessMetrics(targetWin.ProcessId);
                if (nativeMetrics != null)
                {
                    jsHeapUsedSize = nativeMetrics.WorkingSet;
                    jsHeapTotalSize = nativeMetrics.PrivateBytes;
                    cpuUsage = nativeMetrics.CpuUsage;

                    long currentPrivateBytes = nativeMetrics.PrivateBytes;
                    if (_lastPrivateBytes > 0)
                    {
                        memoryAllocations = (double)(currentPrivateBytes - _lastPrivateBytes) / (1024 * 1024);
                    }
                    _lastPrivateBytes = currentPrivateBytes;
                }
                else
                {
                    using var proc = Process.GetProcessById(targetWin.ProcessId);
                    jsHeapUsedSize = proc.WorkingSet64;
                    jsHeapTotalSize = proc.PrivateMemorySize64;
                    cpuUsage = GetCpuUsage(proc);

                    long currentPrivateBytes = proc.PrivateMemorySize64;
                    if (_lastPrivateBytes > 0)
                    {
                        memoryAllocations = (double)(currentPrivateBytes - _lastPrivateBytes) / (1024 * 1024);
                    }
                    _lastPrivateBytes = currentPrivateBytes;
                }
            }
        }
        catch
        {
            // Fallback to current process
            try
            {
                using var proc = Process.GetCurrentProcess();
                jsHeapUsedSize = proc.WorkingSet64;
                jsHeapTotalSize = proc.PrivateMemorySize64;
            }
            catch {}
        }

        _layoutCount++;
        double fps = 60.0 - new Random().NextDouble() * 0.5;

        var metricsArray = new JsonArray
        {
            new JsonObject { ["name"] = "Timestamp", ["value"] = timestamp },
            new JsonObject { ["name"] = "Nodes", ["value"] = nodesCount },
            new JsonObject { ["name"] = "JSHeapUsedSize", ["value"] = jsHeapUsedSize },
            new JsonObject { ["name"] = "JSHeapTotalSize", ["value"] = jsHeapTotalSize },
            new JsonObject { ["name"] = "CPUUsage", ["value"] = cpuUsage },
            new JsonObject { ["name"] = "LayoutCount", ["value"] = _layoutCount },
            new JsonObject { ["name"] = "LayoutDuration", ["value"] = 0.002 },
            new JsonObject { ["name"] = "FPS", ["value"] = fps },
            new JsonObject { ["name"] = "FrameDuration", ["value"] = 1.0 / fps },
            new JsonObject { ["name"] = "DispatcherQueueDelay", ["value"] = 0.0 },
            new JsonObject { ["name"] = "UIThreadBlockingTime", ["value"] = 0.0 },
            new JsonObject { ["name"] = "MemoryAllocations", ["value"] = memoryAllocations }
        };

        return metricsArray;
    }

    private void StartPerformanceLoop()
    {
        StopPerformanceLoop();
        _performanceCts = new System.Threading.CancellationTokenSource();
        var token = _performanceCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, token);
                    if (token.IsCancellationRequested) break;

                    var metrics = await GetPerformanceMetricsAsync();
                    EventReceived?.Invoke(this, new CdpEventEventArgs("Performance.metrics", new JsonObject
                    {
                        ["metrics"] = metrics,
                        ["title"] = "metrics"
                    }));
                }
                catch (TaskCanceledException) {}
                catch (Exception ex)
                {
                    Logger.LogErrorMessage("OsAutomation", "Error in virtual performance loop", ex);
                }
            }
        }, token);
    }

    private void StopPerformanceLoop()
    {
        _performanceCts?.Cancel();
        _performanceCts?.Dispose();
        _performanceCts = null;
    }

    private double GetCpuUsage(Process process)
    {
        var now = DateTime.UtcNow;
        try
        {
            var cpuTime = process.TotalProcessorTime;
            if (_lastTotalProcessorTime == TimeSpan.Zero)
            {
                _lastCpuTime = now;
                _lastTotalProcessorTime = cpuTime;
                return 0.0;
            }
            var elapsed = now - _lastCpuTime;
            if (elapsed.TotalMilliseconds <= 0) return 0.0;
            var usage = (cpuTime - _lastTotalProcessorTime).TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100;
            _lastCpuTime = now;
            _lastTotalProcessorTime = cpuTime;
            return Math.Min(100.0, Math.Max(0.0, usage));
        }
        catch
        {
            return 0.0;
        }
    }

    private static int CountNodes(OSNode? node)
    {
        if (node == null) return 0;
        int count = 1;
        foreach (var child in node.Children)
        {
            count += CountNodes(child);
        }
        return count;
    }

    private async Task<JsonObject> HandleMemoryDomainAsync(string action, JsonObject parameters)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return new JsonObject();

            case "getDOMCounters":
                {
                    PopulateOrUpdateRootNode();
                    int docs = 1;
                    int nodes = CountNodes(_rootNode);
                    return new JsonObject
                    {
                        ["documents"] = docs,
                        ["nodes"] = nodes,
                        ["jsEventListeners"] = 0
                    };
                }

            case "getLiveControls":
                {
                    PopulateOrUpdateRootNode();
                    var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    if (_rootNode != null)
                    {
                        TraverseRolesForLiveControls(_rootNode, counts);
                    }
                    var array = new JsonArray();
                    foreach (var pair in counts)
                    {
                        array.Add(new JsonObject
                        {
                            ["type"] = pair.Key,
                            ["count"] = pair.Value
                        });
                    }
                    return new JsonObject { ["controls"] = array };
                }

            case "getDetachedControls":
                return new JsonObject { ["detachedControls"] = new JsonArray() };

            case "takeHeapSnapshot":
                {
                    var snapshotMeta = new JsonObject
                    {
                        ["meta"] = new JsonObject
                        {
                            ["node_fields"] = new JsonArray { "type", "name", "id", "self_size", "edge_count", "trace_node_id" },
                            ["node_types"] = new JsonArray
                            {
                                new JsonArray { "hidden", "array", "string", "object", "code", "closure", "regexp", "number", "native", "synthetic", "concatenated string", "sliced string" },
                                "string",
                                "number",
                                "number",
                                "number",
                                "number"
                            },
                            ["edge_fields"] = new JsonArray { "type", "name_or_index", "to_node" },
                            ["edge_types"] = new JsonArray
                            {
                                new JsonArray { "context", "element", "property", "internal", "hidden", "shortcut" },
                                "string_or_number",
                                "number"
                            }
                        },
                        ["node_count"] = 0,
                        ["edge_count"] = 0
                    };
                    return new JsonObject
                    {
                        ["snapshot"] = snapshotMeta,
                        ["nodes"] = new JsonArray(),
                        ["edges"] = new JsonArray(),
                        ["strings"] = new JsonArray()
                    };
                }

            case "getRetainers":
                return new JsonObject
                {
                    ["name"] = "No retainer data available for OS elements",
                    ["type"] = "System",
                    ["hashCode"] = 0,
                    ["retainers"] = new JsonArray()
                };

            case "collectGarbage":
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                return new JsonObject();

            default:
                return new JsonObject();
        }
    }

    private void TraverseRolesForLiveControls(OSNode node, Dictionary<string, int> counts)
    {
        string role = node.Role ?? "Unknown";
        counts[role] = counts.TryGetValue(role, out int c) ? c + 1 : 1;
        foreach (var child in node.Children)
        {
            TraverseRolesForLiveControls(child, counts);
        }
    }

    private Task<JsonObject> HandleNetworkDomainAsync(string action, JsonObject parameters)
    {
        switch (action)
        {
            case "enable":
                _isNetworkEnabled = true;
                StartNetworkSimulation();
                return Task.FromResult(new JsonObject());

            case "disable":
                _isNetworkEnabled = false;
                StopNetworkSimulation();
                return Task.FromResult(new JsonObject());

            case "emulateNetworkConditions":
                _networkOffline = parameters["offline"]?.GetValue<bool>() ?? false;
                _networkLatency = parameters["latency"]?.GetValue<double>() ?? 0.0;
                return Task.FromResult(new JsonObject());

            case "setBlockedURLs":
                {
                    var urlsArray = parameters["urls"]?.AsArray();
                    var list = new List<string>();
                    if (urlsArray != null)
                    {
                        foreach (var urlNode in urlsArray)
                        {
                            if (urlNode != null)
                            {
                                list.Add(urlNode.GetValue<string>());
                            }
                        }
                    }
                    _blockedUrls = list;
                    return Task.FromResult(new JsonObject());
                }

            case "getResponseBody":
                {
                    string requestId = parameters["requestId"]?.GetValue<string>() ?? "";
                    string body = "{}";
                    lock (_networkResponseBodies)
                    {
                        _networkResponseBodies.TryGetValue(requestId, out body);
                    }
                    return Task.FromResult(new JsonObject
                    {
                        ["body"] = body ?? "{}",
                        ["base64Encoded"] = false
                    });
                }

            default:
                return Task.FromResult(new JsonObject());
        }
    }

    private bool IsUrlBlocked(string url)
    {
        foreach (var pattern in _blockedUrls)
        {
            if (MatchesPattern(url, pattern))
            {
                return true;
            }
        }
        return false;
    }

    private static bool MatchesPattern(string url, string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return false;
        if (pattern == "*") return true;

        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(url, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task SimulateNetworkRequestAsync(string url, string method, string type, string responseBody, int status = 200, string statusText = "OK")
    {
        if (!_isNetworkEnabled) return;

        string reqId = "virtual_req_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        lock (_networkResponseBodies)
        {
            _networkResponseBodies[reqId] = responseBody;
        }

        double timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        var reqHeaders = new JsonObject
        {
            ["User-Agent"] = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)",
            ["Accept"] = "application/json"
        };

        var requestObj = new JsonObject
        {
            ["url"] = url,
            ["method"] = method,
            ["headers"] = reqHeaders
        };

        EventReceived?.Invoke(this, new CdpEventEventArgs("Network.requestWillBeSent", new JsonObject
        {
            ["requestId"] = reqId,
            ["request"] = requestObj,
            ["type"] = type,
            ["timestamp"] = timestamp
        }));

        if (_networkOffline)
        {
            await Task.Delay(10);
            EventReceived?.Invoke(this, new CdpEventEventArgs("Network.loadingFailed", new JsonObject
            {
                ["requestId"] = reqId,
                ["timestamp"] = timestamp + 0.01,
                ["errorText"] = "net::ERR_INTERNET_DISCONNECTED"
            }));
            return;
        }

        if (IsUrlBlocked(url))
        {
            await Task.Delay(10);
            EventReceived?.Invoke(this, new CdpEventEventArgs("Network.loadingFailed", new JsonObject
            {
                ["requestId"] = reqId,
                ["timestamp"] = timestamp + 0.01,
                ["errorText"] = "net::ERR_BLOCKED_BY_CLIENT"
            }));
            return;
        }

        double delay = _networkLatency > 0 ? _networkLatency : new Random().Next(50, 250);
        await Task.Delay((int)delay);

        double timestamp2 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        var resHeaders = new JsonObject
        {
            ["Content-Type"] = "application/json",
            ["Server"] = "VirtualOSAutomationServer/1.0",
            ["Cache-Control"] = "no-cache"
        };

        var responseObj = new JsonObject
        {
            ["status"] = status,
            ["statusText"] = statusText,
            ["headers"] = resHeaders
        };

        EventReceived?.Invoke(this, new CdpEventEventArgs("Network.responseReceived", new JsonObject
        {
            ["requestId"] = reqId,
            ["response"] = responseObj,
            ["type"] = type,
            ["timestamp"] = timestamp2
        }));

        await Task.Delay(10);
        double timestamp3 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        EventReceived?.Invoke(this, new CdpEventEventArgs("Network.loadingFinished", new JsonObject
        {
            ["requestId"] = reqId,
            ["timestamp"] = timestamp3
        }));
    }

    private static readonly string[] MockUrls = new[]
    {
        "https://api.github.com/repos/wieslawsoltes/CDP/commits",
        "https://api.github.com/repos/wieslawsoltes/CDP/issues",
        "https://config.cdp-automation-os.local/client-config.json",
        "https://telemetry.cdp-automation-os.local/v1/track",
        "https://api.github.com/repos/wieslawsoltes/CDP/releases/latest"
    };

    private static readonly string[] MockBodies = new[]
    {
        "[{\"sha\":\"a1b2c3d4\",\"commit\":{\"message\":\"Simulated commit message\",\"author\":{\"name\":\"agent\"}}}]",
        "[{\"number\":123,\"title\":\"Simulated issue title\",\"state\":\"open\"}]",
        "{\"environment\":\"production\",\"telemetryEnabled\":true,\"logLevel\":\"info\"}",
        "{\"status\":\"success\",\"recorded\":true}",
        "{\"tag_name\":\"v2.1.0\",\"name\":\"v2.1.0 release\"}"
    };

    private string GetTestedProcessName()
    {
        try
        {
            var windows = _automation.GetWindows();
            var targetWin = windows.FirstOrDefault(w => w.Id == _windowId);
            if (targetWin != null && !string.IsNullOrEmpty(targetWin.ProcessName))
            {
                return targetWin.ProcessName;
            }
        }
        catch {}
        return "CdpSampleApp";
    }

    private void StartNetworkSimulation()
    {
        StopNetworkSimulation();
        _networkCts = new System.Threading.CancellationTokenSource();
        var token = _networkCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                string procName = GetTestedProcessName();
                await Task.Delay(500, token);
                if (token.IsCancellationRequested) return;
                await SimulateNetworkRequestAsync($"https://config.{procName.ToLowerInvariant()}.local/client-config.json", "GET", "Fetch", MockBodies[2]);
                await Task.Delay(800, token);
                if (token.IsCancellationRequested) return;
                await SimulateNetworkRequestAsync($"https://api.github.com/repos/{procName}/app/releases/latest", "GET", "Fetch", MockBodies[4]);
            }
            catch {}
        }, token);

        _ = Task.Run(async () =>
        {
            var rand = new Random();
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(rand.Next(5000, 8000), token);
                    if (token.IsCancellationRequested) break;

                    string procName = GetTestedProcessName();
                    string[] dynamicUrls = new[]
                    {
                        $"https://api.github.com/repos/{procName}/app/commits",
                        $"https://api.github.com/repos/{procName}/app/issues",
                        $"https://config.{procName.ToLowerInvariant()}.local/client-config.json",
                        $"https://telemetry.{procName.ToLowerInvariant()}.local/v1/track",
                        $"https://api.github.com/repos/{procName}/app/releases/latest"
                    };

                    int idx = rand.Next(dynamicUrls.Length);
                    string url = dynamicUrls[idx];
                    string body = MockBodies[idx];
                    string method = url.Contains("/track") ? "POST" : "GET";

                    await SimulateNetworkRequestAsync(url, method, "Fetch", body);
                }
                catch (TaskCanceledException) {}
                catch (Exception ex)
                {
                    Logger.LogErrorMessage("OsAutomation", "Error in virtual network simulation", ex);
                }
            }
        }, token);
    }

    private void StopNetworkSimulation()
    {
        _networkCts?.Cancel();
        _networkCts?.Dispose();
        _networkCts = null;
    }

    private List<OSNode> QuerySelectorAllInternal(OSNode root, string selector)
    {
        var results = new List<OSNode>();
        if (string.IsNullOrEmpty(selector)) return results;

        // Parse attributes and base selector
        var attributePredicates = new List<(string Name, string? Value)>();
        int bracketStart = selector.IndexOf('[');
        string baseSelector = selector;
        if (bracketStart >= 0)
        {
            baseSelector = selector.Substring(0, bracketStart).Trim();
            int searchIdx = bracketStart;
            while (true)
            {
                int open = selector.IndexOf('[', searchIdx);
                if (open < 0) break;
                int close = selector.IndexOf(']', open);
                if (close < 0) break;
                
                string content = selector.Substring(open + 1, close - open - 1);
                int eq = content.IndexOf('=');
                if (eq >= 0)
                {
                    string attrName = content.Substring(0, eq).Trim();
                    string attrVal = content.Substring(eq + 1).Trim('\'', '\"', ' ');
                    attributePredicates.Add((attrName, attrVal));
                }
                else
                {
                    string attrName = content.Trim();
                    attributePredicates.Add((attrName, null));
                }
                searchIdx = close + 1;
            }
        }

        string baseId = "";
        string baseRoleOrName = "";
        int hashIdx = baseSelector.IndexOf('#');
        if (hashIdx >= 0)
        {
            baseRoleOrName = baseSelector.Substring(0, hashIdx).Trim();
            baseId = baseSelector.Substring(hashIdx + 1).Trim();
        }
        else
        {
            if (baseSelector.StartsWith("#"))
            {
                baseId = baseSelector.Substring(1).Trim();
            }
            else
            {
                baseRoleOrName = baseSelector;
            }
        }

        // Recursive search for matching nodes
        TraverseOSNodesInternal(root, node =>
        {
            // Match Base Selector ID
            if (!string.IsNullOrEmpty(baseId))
            {
                if (!string.Equals(node.Id, baseId, StringComparison.OrdinalIgnoreCase)) return;
            }

            // Match Base Selector Role or Name
            if (!string.IsNullOrEmpty(baseRoleOrName))
            {
                string roleClean = node.Role;
                if (roleClean.StartsWith("AX", StringComparison.OrdinalIgnoreCase))
                {
                    roleClean = roleClean.Substring(2);
                }

                if (!string.Equals(roleClean, baseRoleOrName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(node.Role, baseRoleOrName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(node.Name, baseRoleOrName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            // Match all attribute predicates
            if (MatchNodeAttributes(node, attributePredicates))
            {
                results.Add(node);
            }
        });

        return results;
    }

    private void TraverseOSNodesInternal(OSNode root, Action<OSNode> action)
    {
        action(root);
        foreach (var child in root.Children)
        {
            TraverseOSNodesInternal(child, action);
        }
    }

    private bool IsNodeFocused(OSNode node)
    {
        var focused = _automation.GetFocusedElement(_windowId);
        return (focused != null && string.Equals(focused.Id, node.Id, StringComparison.OrdinalIgnoreCase)) || string.Equals(node.Id, _lastFocusedId, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CdpOsRuntimeWindow
    {
        private readonly OsAutomationCdpSession _session;

        public CdpOsRuntimeWindow(OsAutomationCdpSession session)
        {
            _session = session;
        }

        public CdpOsRuntimeDocument Document => new(_session);
    }

    private sealed class CdpOsRuntimeDocument
    {
        private readonly OsAutomationCdpSession _session;

        public CdpOsRuntimeDocument(OsAutomationCdpSession session)
        {
            _session = session;
        }

        public string Title => "OS Automation Target";

        public string ReadyState => "complete";

        public CdpOsRuntimeElement? Body => _session._rootNode != null ? new CdpOsRuntimeElement(_session, _session._rootNode) : null;

        public CdpOsRuntimeElement? QuerySelector(string selector)
        {
            if (_session._rootNode == null) return null;
            var node = _session.QuerySelectorInternal(_session._rootNode, selector);
            return node != null ? new CdpOsRuntimeElement(_session, node) : null;
        }

        public CdpOsRuntimeElement[] QuerySelectorAll(string selector)
        {
            if (_session._rootNode == null) return Array.Empty<CdpOsRuntimeElement>();
            var nodes = _session.QuerySelectorAllInternal(_session._rootNode, selector);
            return nodes.Select(n => new CdpOsRuntimeElement(_session, n)).ToArray();
        }

        public CdpOsRuntimeElement? GetElementById(string id)
        {
            return QuerySelector($"[id=\"{id}\"]");
        }

        public string GetPropertiesJson(string selector)
        {
            if (_session._rootNode == null) return "{}";
            var node = _session.QuerySelectorInternal(_session._rootNode, selector);
            if (node == null) return "{}";

            var dict = new Dictionary<string, object?>();
            var typeName = node.Role;
            if (typeName.StartsWith("AX")) typeName = typeName.Substring(2);
            if (!string.IsNullOrEmpty(typeName))
            {
                typeName = char.ToUpper(typeName[0]) + typeName.Substring(1);
            }

            dict["$Type"] = typeName;
            dict["$FullName"] = node.Role;
            dict["Text"] = node.Text;
            dict["Value"] = node.Text;
            dict["IsEnabled"] = "True";
            dict["Id"] = node.Id;
            dict["IsVisible"] = (node.Bounds.Width > 0 && node.Bounds.Height > 0) ? "True" : "False";
            
            bool isChecked = false;
            if (node.Role == "AXCheckBox" || node.Role == "AXRadioButton" || node.Role == "check box" || node.Role == "radio button")
            {
                isChecked = node.Text == "1" || node.Text.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            dict["IsChecked"] = isChecked.ToString();

            return System.Text.Json.JsonSerializer.Serialize(dict);
        }
    }

    private sealed class CdpOsRuntimeElement
    {
        private readonly OsAutomationCdpSession _session;
        private readonly OSNode _node;

        public CdpOsRuntimeElement(OsAutomationCdpSession session, OSNode node)
        {
            _session = session;
            _node = node;
        }

        public OSNode Node => _node;
        public string Id => _node.Id;
        public string Name => _node.Name;
        public string TextContent => _node.Text ?? _node.Name ?? "";
        public string InnerText => TextContent;
        public string Value => TextContent;
        public string Role => _node.Role;

        public bool IsVisible => _node.Bounds.Width > 0 && _node.Bounds.Height > 0;
        public bool IsEffectivelyVisible => IsVisible;
        
        public bool IsEnabled => true;

        public bool IsChecked
        {
            get
            {
                if (_node.Role == "AXCheckBox" || _node.Role == "AXRadioButton" || _node.Role == "check box" || _node.Role == "radio button")
                {
                    return _node.Text == "1" || _node.Text.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }
        }

        public bool IsFocused => _session.IsNodeFocused(_node);

        public bool IsSelected => false;
        public bool IsExpanded => false;

        public string? GetAttribute(string name)
        {
            if (_node.Attributes.TryGetValue(name, out var val))
            {
                return val;
            }
            return null;
        }

        public CdpOsRuntimeElement? QuerySelector(string selector)
        {
            var node = _session.QuerySelectorInternal(_node, selector);
            return node != null ? new CdpOsRuntimeElement(_session, node) : null;
        }

        public CdpOsRuntimeElement[] QuerySelectorAll(string selector)
        {
            var nodes = _session.QuerySelectorAllInternal(_node, selector);
            return nodes.Select(n => new CdpOsRuntimeElement(_session, n)).ToArray();
        }
    }
}
