using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace CdpInspectorApp;

public partial class MainWindow : Window
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private int _messageId = 1;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> _pendingRequests = new();
    private readonly HttpClient _httpClient = new();

    private ObservableCollection<DomNodeModel> _rootNodes = new();
    private ObservableCollection<AttributeModel> _attributes = new();
    private ObservableCollection<PropertyModel> _properties = new();
    private ObservableCollection<LogModel> _logs = new();
    private ObservableCollection<CssPropertyModel> _cssProperties = new();
    private ObservableCollection<ConsoleItemModel> _consoleHistory = new();
    private ObservableCollection<CssPropertyModel> _computedStyles = new();
    private ObservableCollection<EventListenerModel> _eventListeners = new();
    private ObservableCollection<NetworkRequestModel> _networkRequests = new();
    private ObservableCollection<ResourceEntryModel> _resources = new();
    private ObservableCollection<ControlCountModel> _liveControls = new();

    private DomNodeModel? _selectedNode;
    private PropertyModel? _selectedProperty;

    public MainWindow()
    {
        InitializeComponent();

        treeDom.ItemsSource = _rootNodes;
        listAttributes.ItemsSource = _attributes;
        listProperties.ItemsSource = _properties;
        listLogs.ItemsSource = _logs;
        listCssProperties.ItemsSource = _cssProperties;
        listConsole.ItemsSource = _consoleHistory;
        listComputedStyles.ItemsSource = _computedStyles;
        listEventListeners.ItemsSource = _eventListeners;
        lstNetworkRequests.ItemsSource = _networkRequests;
        lstApplicationResources.ItemsSource = _resources;
        lstLiveControls.ItemsSource = _liveControls;

        btnRefreshTargets.Click += BtnRefreshTargets_Click;
        btnConnect.Click += BtnConnect_Click;
        btnDisconnect.Click += BtnDisconnect_Click;
        treeDom.SelectionChanged += TreeDom_SelectionChanged;

        btnFocus.Click += BtnFocus_Click;
        chkHighlight.IsCheckedChanged += ChkHighlight_IsCheckedChanged;
        btnClick.Click += BtnClick_Click;
        btnSendText.Click += BtnSendText_Click;
        btnSendKey.Click += BtnSendKey_Click;

        btnResize.Click += BtnResize_Click;
        btnResizeReset.Click += BtnResizeReset_Click;
        btnCaptureScreenshot.Click += BtnCaptureScreenshot_Click;
        btnRefreshMetrics.Click += BtnRefreshMetrics_Click;
        btnCloseTarget.Click += BtnCloseTarget_Click;

        btnApplyAttr.Click += BtnApplyAttr_Click;
        btnDeleteAttr.Click += BtnDeleteAttr_Click;

        listProperties.SelectionChanged += ListProperties_SelectionChanged;
        btnApplyProperty.Click += BtnApplyProperty_Click;
        btnClearLogs.Click += BtnClearLogs_Click;
        btnApplyStyleText.Click += BtnApplyStyleText_Click;

        btnInspect.Click += BtnInspect_Click;
        btnDeleteControl.Click += BtnDeleteControl_Click;
        btnReload.Click += BtnReload_Click;
        btnSendConsole.Click += BtnSendConsole_Click;
        txtConsoleInput.KeyDown += TxtConsoleInput_KeyDown;
        btnScroll.Click += BtnScroll_Click;

        btnClearNetwork.Click += BtnClearNetwork_Click;
        lstNetworkRequests.SelectionChanged += LstNetworkRequests_SelectionChanged;
        btnRefreshResources.Click += BtnRefreshResources_Click;
        btnAddResource.Click += BtnAddResource_Click;
        btnSaveResource.Click += BtnSaveResource_Click;
        lstApplicationResources.SelectionChanged += LstApplicationResources_SelectionChanged;
        treeWorkspaceFiles.SelectionChanged += TreeWorkspaceFiles_SelectionChanged;
        btnCollectGarbage.Click += BtnCollectGarbage_Click;

        // Populate Keys ComboBox
        cbKeys.ItemsSource = new List<string> { "Enter", "Tab", "Escape", "Space", "Backspace", "Delete", "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "PageUp", "PageDown", "Home", "End" };

        // Populate Application navigation tree
        var appRoot = new AppNavNode("Application");
        var resNode = new AppNavNode("Global Resources");
        appRoot.Children.Add(resNode);
        appRoot.Children.Add(new AppNavNode("Preferences & Themes"));
        
        var appNavItems = new ObservableCollection<AppNavNode> { appRoot };
        treeAppNav.ItemsSource = appNavItems;
        appRoot.IsExpanded = true;
        resNode.IsSelected = true;

        treeAppNav.SelectionChanged += TreeAppNav_SelectionChanged;

        // Scan targets on load
        Dispatcher.UIThread.Post(() => BtnRefreshTargets_Click(null, null!));
    }

    private async void BtnRefreshTargets_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string host = txtHost.Text?.Trim() ?? "http://localhost:9222";
            if (!host.StartsWith("http://") && !host.StartsWith("https://"))
            {
                host = "http://" + host;
            }

            var response = await _httpClient.GetStringAsync($"{host}/json");
            var targets = JsonNode.Parse(response) as JsonArray;
            if (targets == null) return;

            var items = new List<TargetItem>();
            foreach (var target in targets)
            {
                if (target is JsonObject obj)
                {
                    string title = obj["title"]?.GetValue<string>() ?? "Unknown Target";
                    string wsUrl = obj["webSocketDebuggerUrl"]?.GetValue<string>() ?? "";
                    string id = obj["id"]?.GetValue<string>() ?? "";
                    items.Add(new TargetItem(title, wsUrl, id));
                }
            }

            cbTargets.ItemsSource = items;
            if (items.Count > 0)
            {
                cbTargets.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            txtConnectionStatus.Text = "Scan error";
            txtConnectionStatus.Foreground = Brushes.Red;
            Console.WriteLine($"Error scanning targets: {ex.Message}");
        }
    }

    private async void BtnConnect_Click(object? sender, RoutedEventArgs e)
    {
        var selected = cbTargets.SelectedItem as TargetItem;
        if (selected == null || string.IsNullOrEmpty(selected.WebSocketUrl))
        {
            txtConnectionStatus.Text = "Select target first";
            return;
        }

        try
        {
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            txtConnectionStatus.Text = "Connecting...";
            txtConnectionStatus.Foreground = Brushes.Orange;

            await _ws.ConnectAsync(new Uri(selected.WebSocketUrl), CancellationToken.None);

            txtConnectionStatus.Text = "Connected";
            txtConnectionStatus.Foreground = Brushes.Green;

            btnConnect.IsEnabled = false;
            btnDisconnect.IsEnabled = true;
            btnInspect.IsEnabled = true;
            btnReload.IsEnabled = true;

            // Start reading incoming frames
            _ = Task.Run(ReceiveLoopAsync);

            // Enable necessary domains
            await SendCommandAsync("DOM.enable", new JsonObject());
            await SendCommandAsync("CSS.enable", new JsonObject());
            await SendCommandAsync("Log.enable", new JsonObject());
            await SendCommandAsync("Performance.enable", new JsonObject());
            await SendCommandAsync("Memory.enable", new JsonObject());
            await SendCommandAsync("Network.enable", new JsonObject());

            // Build initial tree
            await RefreshDomTreeAsync();

            // Query workspace files
            try
            {
                var sourcesRes = await SendCommandAsync("Sources.getWorkspaceFiles", new JsonObject());
                var files = sourcesRes["files"] as JsonArray;
                if (files != null)
                {
                    LoadWorkspaceFiles(files);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sources failed: {ex.Message}");
            }

            // Query application resources
            BtnRefreshResources_Click(null, null!);
        }
        catch (Exception ex)
        {
            txtConnectionStatus.Text = "Connection failed";
            txtConnectionStatus.Foreground = Brushes.Red;
            Console.WriteLine($"Connection failed: {ex.Message}");
        }
    }

    private async void BtnDisconnect_Click(object? sender, RoutedEventArgs e)
    {
        await DisconnectAsync();
    }

    private async Task DisconnectAsync()
    {
        if (_ws == null) return;

        try
        {
            _cts?.Cancel();
            if (_ws.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
        catch { }
        finally
        {
            _ws.Dispose();
            _ws = null;
            _cts?.Dispose();
            _cts = null;

            txtConnectionStatus.Text = "Disconnected";
            txtConnectionStatus.Foreground = Brushes.Red;

            btnConnect.IsEnabled = true;
            btnDisconnect.IsEnabled = false;
            btnInspect.IsEnabled = false;
            btnInspect.IsChecked = false;
            btnReload.IsEnabled = false;

            _rootNodes.Clear();
            _attributes.Clear();
            _properties.Clear();
            _cssProperties.Clear();
            _consoleHistory.Clear();
            _computedStyles.Clear();
            _networkRequests.Clear();
            _resources.Clear();
            _liveControls.Clear();
            _memoryHistory.Clear();
            canvasMemoryChart.Children.Clear();
            lblNetUrl.Text = "Select a request";
            txtNetReqHeaders.Text = "";
            txtNetResHeaders.Text = "";
            txtNetBody.Text = "";
            lblSourceFileName.Text = "Select a file from workspace";
            txtSourceContent.Text = "";
            _eventListeners.Clear();
            lblPerfDocuments.Text = "--";
            txtSelectedNodeId.Text = "None";
            txtStyleText.Text = "";
            _selectedNode = null;
            _selectedProperty = null;
            lblSelectedProperty.Text = "None";
        }
    }

    private async Task RefreshDomTreeAsync()
    {
        try
        {
            var response = await SendCommandAsync("DOM.getDocument", new JsonObject());
            var root = response["root"] as JsonObject;
            if (root == null) return;

            var rootModel = BuildModel(root);
            _rootNodes.Clear();
            _rootNodes.Add(rootModel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing tree: {ex.Message}");
        }
    }

    private DomNodeModel BuildModel(JsonObject nodeJson)
    {
        int nodeId = nodeJson["nodeId"]?.GetValue<int>() ?? 0;
        string nodeName = nodeJson["nodeName"]?.GetValue<string>() ?? "";
        
        var model = new DomNodeModel(nodeId, nodeName);

        // Attributes
        var attrsNode = nodeJson["attributes"] as JsonArray;
        if (attrsNode != null)
        {
            for (int i = 0; i < attrsNode.Count; i += 2)
            {
                string name = attrsNode[i]?.GetValue<string>() ?? "";
                string val = attrsNode[i + 1]?.GetValue<string>() ?? "";
                model.AttributesList.Add(new AttributeModel(name, val));
            }
        }

        // Children
        var childrenNode = nodeJson["children"] as JsonArray;
        if (childrenNode != null)
        {
            foreach (var child in childrenNode)
            {
                if (child is JsonObject childObj)
                {
                    model.Children.Add(BuildModel(childObj));
                }
            }
        }

        // Setup display name
        string display = nodeName;
        var idAttr = model.AttributesList.FirstOrDefault(a => a.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
        if (idAttr != null) display += $"#{idAttr.Value}";
        var classAttr = model.AttributesList.FirstOrDefault(a => a.Name.Equals("class", StringComparison.OrdinalIgnoreCase));
        if (classAttr != null) display += $".{classAttr.Value.Split(' ').FirstOrDefault()}";
        model.DisplayName = display;

        return model;
    }

    private async void TreeDom_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedNode = treeDom.SelectedItem as DomNodeModel;
        if (_selectedNode == null)
        {
            txtSelectedNodeId.Text = "None";
            _attributes.Clear();
            _properties.Clear();
            _cssProperties.Clear();
            return;
        }

        txtSelectedNodeId.Text = _selectedNode.NodeId.ToString();

        // 1. Load Attributes
        _attributes.Clear();
        foreach (var attr in _selectedNode.AttributesList)
        {
            _attributes.Add(attr);
        }

        // 2. Select Node in CDP
        await SendCommandAsync("DOM.setInspectedNode", new JsonObject { ["nodeId"] = _selectedNode.NodeId });

        // 3. Resolve Node properties
        _properties.Clear();
        _selectedProperty = null;
        lblSelectedProperty.Text = "None";
        txtPropertyValue.Text = "";

        try
        {
            var resolveRes = await SendCommandAsync("DOM.resolveNode", new JsonObject { ["nodeId"] = _selectedNode.NodeId });
            var obj = resolveRes["object"] as JsonObject;
            string objectId = obj?["objectId"]?.GetValue<string>() ?? "";
            
            if (!string.IsNullOrEmpty(objectId))
            {
                var propsRes = await SendCommandAsync("Runtime.getProperties", new JsonObject { ["objectId"] = objectId });
                var results = propsRes["result"] as JsonArray;
                if (results != null)
                {
                    var sorted = results
                        .Select(p => {
                            string name = p?["name"]?.GetValue<string>() ?? "";
                            var valObj = p?["value"] as JsonObject;
                            string val = valObj?["value"]?.ToString() ?? valObj?["description"]?.GetValue<string>() ?? "null";
                            string type = valObj?["type"]?.GetValue<string>() ?? "object";
                            return new PropertyModel(name, val, type);
                        })
                        .OrderBy(p => p.Name)
                        .ToList();

                    foreach (var p in sorted)
                    {
                        _properties.Add(p);
                    }
                }

                // 3b. Resolve Event Listeners
                _eventListeners.Clear();
                try
                {
                    var listenersRes = await SendCommandAsync("DOMDebugger.getEventListeners", new JsonObject { ["objectId"] = objectId });
                    var listeners = listenersRes["listeners"] as JsonArray;
                    if (listeners != null)
                    {
                        foreach (var listener in listeners)
                        {
                            if (listener is JsonObject listenerObj)
                            {
                                string typeName = listenerObj["type"]?.GetValue<string>() ?? "";
                                bool useCapture = listenerObj["useCapture"]?.GetValue<bool>() ?? false;
                                var handler = listenerObj["handler"] as JsonObject;
                                string handlerName = handler?["description"]?.GetValue<string>() ?? "Anonymous";

                                _eventListeners.Add(new EventListenerModel(typeName, handlerName, useCapture));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching event listeners: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching properties: {ex.Message}");
        }

        // 4. Resolve CSS Styles
        _cssProperties.Clear();
        txtStyleText.Text = "";
        try
        {
            var cssRes = await SendCommandAsync("CSS.getMatchedStylesForNode", new JsonObject { ["nodeId"] = _selectedNode.NodeId });
            var inlineStyle = cssRes["inlineStyle"] as JsonObject;
            if (inlineStyle != null)
            {
                var cssProps = inlineStyle["cssProperties"] as JsonArray;
                if (cssProps != null)
                {
                    var fullStyleBuilder = new StringBuilder();
                    foreach (var prop in cssProps)
                    {
                        if (prop is JsonObject propObj)
                        {
                            string name = propObj["name"]?.GetValue<string>() ?? "";
                            string val = propObj["value"]?.GetValue<string>() ?? "";
                            _cssProperties.Add(new CssPropertyModel(name, val));
                            fullStyleBuilder.Append($"{name}: {val}; ");
                        }
                    }
                    txtStyleText.Text = fullStyleBuilder.ToString().Trim();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching CSS styles: {ex.Message}");
        }

        // 5. Resolve Computed Styles
        _computedStyles.Clear();
        try
        {
            var compRes = await SendCommandAsync("CSS.getComputedStyleForNode", new JsonObject { ["nodeId"] = _selectedNode.NodeId });
            var compStyles = compRes["computedStyle"] as JsonArray;
            if (compStyles != null)
            {
                foreach (var prop in compStyles)
                {
                    if (prop is JsonObject propObj)
                    {
                        string name = propObj["name"]?.GetValue<string>() ?? "";
                        string val = propObj["value"]?.GetValue<string>() ?? "";
                        _computedStyles.Add(new CssPropertyModel(name, val));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching computed styles: {ex.Message}");
        }

        // Trigger highlight if enabled
        if (chkHighlight.IsChecked == true)
        {
            await TriggerHighlightAsync();
        }
    }

    private async void BtnFocus_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedNode == null) return;
        try
        {
            await SendCommandAsync("DOM.focus", new JsonObject { ["nodeId"] = _selectedNode.NodeId });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Focus failed: {ex.Message}");
        }
    }

    private async void ChkHighlight_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (chkHighlight.IsChecked == true)
        {
            await TriggerHighlightAsync();
        }
        else
        {
            await SendCommandAsync("Overlay.hideHighlight", new JsonObject());
        }
    }

    private async Task TriggerHighlightAsync()
    {
        if (_selectedNode == null) return;
        try
        {
            var highlightConfig = new JsonObject
            {
                ["contentColor"] = new JsonObject { ["r"] = 80, ["g"] = 150, ["b"] = 240, ["a"] = 0.4 },
                ["borderColor"] = new JsonObject { ["r"] = 80, ["g"] = 150, ["b"] = 240, ["a"] = 0.8 }
            };
            var parameters = new JsonObject
            {
                ["nodeId"] = _selectedNode.NodeId,
                ["highlightConfig"] = highlightConfig
            };
            await SendCommandAsync("Overlay.highlightNode", parameters);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Highlight failed: {ex.Message}");
        }
    }

    private async void BtnClick_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedNode == null) return;
        try
        {
            var boxRes = await SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = _selectedNode.NodeId });
            var model = boxRes["model"] as JsonObject;
            var contentQuad = model?["content"] as JsonArray;
            if (contentQuad != null && contentQuad.Count >= 8)
            {
                double x1 = contentQuad[0]!.GetValue<double>();
                double y1 = contentQuad[1]!.GetValue<double>();
                double x3 = contentQuad[4]!.GetValue<double>();
                double y3 = contentQuad[5]!.GetValue<double>();
                double cx = (x1 + x3) / 2;
                double cy = (y1 + y3) / 2;

                // Press
                await SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                {
                    ["type"] = "mousePressed",
                    ["x"] = cx,
                    ["y"] = cy,
                    ["button"] = "left",
                    ["clickCount"] = 1
                });
                await Task.Delay(50);
                // Release
                await SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
                {
                    ["type"] = "mouseReleased",
                    ["x"] = cx,
                    ["y"] = cy,
                    ["button"] = "left",
                    ["clickCount"] = 1
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Click failed: {ex.Message}");
        }
    }

    private async void BtnSendText_Click(object? sender, RoutedEventArgs e)
    {
        string text = txtInputSim.Text ?? "";
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            await SendCommandAsync("Input.insertText", new JsonObject { ["text"] = text });
            txtInputSim.Text = "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Text input failed: {ex.Message}");
        }
    }

    private async void BtnSendKey_Click(object? sender, RoutedEventArgs e)
    {
        string key = cbKeys.SelectedItem as string ?? cbKeys.Text ?? "";
        if (string.IsNullOrEmpty(key)) return;
        try
        {
            // Key Down
            await SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
            {
                ["type"] = "keyDown",
                ["key"] = key
            });
            await Task.Delay(50);
            // Key Up
            await SendCommandAsync("Input.dispatchKeyEvent", new JsonObject
            {
                ["type"] = "keyUp",
                ["key"] = key
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Keystroke simulation failed: {ex.Message}");
        }
    }

    private async void BtnResize_Click(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(txtWidth.Text, out int w) || !int.TryParse(txtHeight.Text, out int h)) return;
        try
        {
            await SendCommandAsync("Emulation.setDeviceMetricsOverride", new JsonObject
            {
                ["width"] = w,
                ["height"] = h,
                ["deviceScaleFactor"] = 1.0,
                ["mobile"] = false
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Resize failed: {ex.Message}");
        }
    }

    private async void BtnResizeReset_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SendCommandAsync("Emulation.clearDeviceMetricsOverride", new JsonObject());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Reset resize failed: {ex.Message}");
        }
    }

    private async void BtnCaptureScreenshot_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var res = await SendCommandAsync("Page.captureScreenshot", new JsonObject());
            string base64 = res["data"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrEmpty(base64))
            {
                byte[] bytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);
                var bitmap = new Bitmap(ms);
                imgScreenshot.Source = bitmap;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Screenshot capture failed: {ex.Message}");
        }
    }

    private async void BtnRefreshMetrics_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var perfRes = await SendCommandAsync("Performance.getMetrics", new JsonObject());
            var metrics = perfRes["metrics"] as JsonArray;
            if (metrics != null)
            {
                foreach (var m in metrics)
                {
                    string name = m?["name"]?.GetValue<string>() ?? "";
                    double val = m?["value"]?.GetValue<double>() ?? 0;
                    if (name == "Nodes") lblPerfNodes.Text = val.ToString("0");
                    else if (name == "JSHeapUsedSize")
                    {
                        lblPerfMemory.Text = $"{(val / 1024 / 1024):F2} MB";
                        _memoryHistory.Add(val / 1024 / 1024);
                        if (_memoryHistory.Count > 30) _memoryHistory.RemoveAt(0);
                        RenderMemoryChart();
                    }
                    else if (name == "JSHeapTotalSize") lblPerfGc.Text = $"{(val / 1024 / 1024):F2} MB";
                }
            }

            try
            {
                var memRes = await SendCommandAsync("Memory.getDOMCounters", new JsonObject());
                int docs = memRes["documents"]?.GetValue<int>() ?? 0;
                lblPerfDocuments.Text = docs.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Memory counters failed: {ex.Message}");
            }

            var sysRes = await SendCommandAsync("SystemInfo.getProcessInfo", new JsonObject());
            var processInfo = sysRes["processInfo"] as JsonArray;
            if (processInfo != null && processInfo.Count > 0)
            {
                var proc = processInfo[0] as JsonObject;
                int pid = proc?["id"]?.GetValue<int>() ?? 0;
                lblPerfPid.Text = pid.ToString();
            }

            var infoRes = await SendCommandAsync("SystemInfo.getInfo", new JsonObject());
            lblPerfOs.Text = $"{infoRes["modelName"]?.GetValue<string>()} {infoRes["modelVersion"]?.GetValue<string>()}";

            // Query live visual controls
            try
            {
                var liveRes = await SendCommandAsync("Memory.getLiveControls", new JsonObject());
                var controls = liveRes["controls"] as JsonArray;
                if (controls != null)
                {
                    _liveControls.Clear();
                    foreach (var cNode in controls)
                    {
                        if (cNode is JsonObject cObj)
                        {
                            string type = cObj["type"]?.GetValue<string>() ?? "";
                            int count = cObj["count"]?.GetValue<int>() ?? 0;
                            _liveControls.Add(new ControlCountModel { Type = type, Count = count });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Live controls failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Refresh metrics failed: {ex.Message}");
        }
    }

    private async void BtnCloseTarget_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SendCommandAsync("Browser.close", new JsonObject());
            await DisconnectAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Target shutdown failed: {ex.Message}");
        }
    }

    private async void BtnApplyAttr_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedNode == null) return;
        string name = txtAttrName.Text?.Trim() ?? "";
        string val = txtAttrValue.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            await SendCommandAsync("DOM.setAttributeValue", new JsonObject
            {
                ["nodeId"] = _selectedNode.NodeId,
                ["name"] = name,
                ["value"] = val
            });

            // Update local node model representation and tree
            var existing = _selectedNode.AttributesList.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Value = val;
            }
            else
            {
                _selectedNode.AttributesList.Add(new AttributeModel(name, val));
            }
            TreeDom_SelectionChanged(null, null!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Set attribute failed: {ex.Message}");
        }
    }

    private async void BtnDeleteAttr_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedNode == null) return;
        string name = txtAttrName.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            await SendCommandAsync("DOM.removeAttribute", new JsonObject
            {
                ["nodeId"] = _selectedNode.NodeId,
                ["name"] = name
            });

            var existing = _selectedNode.AttributesList.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _selectedNode.AttributesList.Remove(existing);
            }
            TreeDom_SelectionChanged(null, null!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Remove attribute failed: {ex.Message}");
        }
    }

    private void ListProperties_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedProperty = listProperties.SelectedItem as PropertyModel;
        if (_selectedProperty == null)
        {
            lblSelectedProperty.Text = "None";
            txtPropertyValue.Text = "";
            return;
        }

        lblSelectedProperty.Text = _selectedProperty.Name;
        txtPropertyValue.Text = _selectedProperty.Value;
    }

    private async void BtnApplyProperty_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedNode == null || _selectedProperty == null) return;
        string valStr = txtPropertyValue.Text ?? "";

        // Formulate script to mutate C# property using inspected node variable $0
        string formattedValue = valStr;
        if (_selectedProperty.Type == "string")
        {
            // Escape double quotes and wrap in quotes
            formattedValue = $"\"{valStr.Replace("\"", "\\\"")}\"";
        }
        else if (_selectedProperty.Type == "boolean")
        {
            formattedValue = valStr.ToLowerInvariant() == "true" ? "true" : "false";
        }

        string expression = $"$0.{_selectedProperty.Name} = {formattedValue}";
        try
        {
            var res = await SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = expression,
                ["returnByValue"] = true
            });

            // Reload properties
            TreeDom_SelectionChanged(null, null!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Mutate property failed: {ex.Message}");
        }
    }

    private void BtnClearLogs_Click(object? sender, RoutedEventArgs e)
    {
        _logs.Clear();
    }

    private async void BtnDeleteControl_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedNode == null) return;
        try
        {
            await SendCommandAsync("DOM.removeNode", new JsonObject
            {
                ["nodeId"] = _selectedNode.NodeId
            });

            // Clear selection and refresh tree
            _selectedNode = null;
            txtSelectedNodeId.Text = "None";
            _attributes.Clear();
            _properties.Clear();
            _cssProperties.Clear();
            _computedStyles.Clear();
            _eventListeners.Clear();
            lblSelectedProperty.Text = "None";
            txtPropertyValue.Text = "";

            await RefreshDomTreeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Delete control failed: {ex.Message}");
        }
    }

    private async void BtnInspect_Click(object? sender, RoutedEventArgs e)
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;
        bool enabled = btnInspect.IsChecked == true;
        try
        {
            await SendCommandAsync("Overlay.setInspectMode", new JsonObject
            {
                ["mode"] = enabled ? "searchForNode" : "none",
                ["highlightConfig"] = new JsonObject
                {
                    ["contentColor"] = new JsonObject { ["r"] = 80, ["g"] = 150, ["b"] = 240, ["a"] = 0.4 },
                    ["borderColor"] = new JsonObject { ["r"] = 80, ["g"] = 150, ["b"] = 240, ["a"] = 0.8 }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Toggle inspect mode failed: {ex.Message}");
        }
    }

    private async void BtnReload_Click(object? sender, RoutedEventArgs e)
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;
        try
        {
            await SendCommandAsync("Page.reload", new JsonObject());
            await RefreshDomTreeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Reload failed: {ex.Message}");
        }
    }

    private async void BtnSendConsole_Click(object? sender, RoutedEventArgs e)
    {
        string expr = txtConsoleInput.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(expr)) return;

        txtConsoleInput.Text = "";

        try
        {
            var res = await SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = expr,
                ["returnByValue"] = false
            });

            var resultObj = res["result"] as JsonObject;
            string displayResult = "";

            if (resultObj != null)
            {
                if (resultObj.ContainsKey("description"))
                {
                    displayResult = resultObj["description"]?.GetValue<string>() ?? "null";
                }
                else if (resultObj.ContainsKey("value"))
                {
                    displayResult = resultObj["value"]?.ToString() ?? "null";
                }
                else if (resultObj["subtype"]?.GetValue<string>() == "null")
                {
                    displayResult = "null";
                }
                else
                {
                    displayResult = resultObj.ToJsonString();
                }
            }
            else
            {
                displayResult = "Success";
            }

            _consoleHistory.Add(new ConsoleItemModel(expr, displayResult, false));
        }
        catch (Exception ex)
        {
            _consoleHistory.Add(new ConsoleItemModel(expr, ex.Message, true));
        }

        if (_consoleHistory.Count > 0)
        {
            listConsole.ScrollIntoView(_consoleHistory[^1]);
        }
    }

    private void TxtConsoleInput_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            BtnSendConsole_Click(null, null!);
            e.Handled = true;
        }
    }

    private async void BtnScroll_Click(object? sender, RoutedEventArgs e)
    {
        double cx = 100;
        double cy = 100;
        if (_selectedNode != null)
        {
            try
            {
                var boxRes = await SendCommandAsync("DOM.getBoxModel", new JsonObject { ["nodeId"] = _selectedNode.NodeId });
                var model = boxRes["model"] as JsonObject;
                var contentQuad = model?["content"] as JsonArray;
                if (contentQuad != null && contentQuad.Count >= 8)
                {
                    double x1 = contentQuad[0]!.GetValue<double>();
                    double y1 = contentQuad[1]!.GetValue<double>();
                    double x3 = contentQuad[4]!.GetValue<double>();
                    double y3 = contentQuad[5]!.GetValue<double>();
                    cx = (x1 + x3) / 2;
                    cy = (y1 + y3) / 2;
                }
            }
            catch { }
        }

        if (!double.TryParse(txtScrollDeltaY.Text, out double deltaY))
        {
            deltaY = 100;
        }

        try
        {
            await SendCommandAsync("Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseWheel",
                ["x"] = cx,
                ["y"] = cy,
                ["deltaX"] = 0.0,
                ["deltaY"] = deltaY
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Scroll failed: {ex.Message}");
        }
    }

    private void SelectNodeById(int nodeId)
    {
        DeselectAll(_rootNodes);

        var path = new List<DomNodeModel>();
        if (FindNodePath(_rootNodes, nodeId, path))
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                path[i].IsExpanded = true;
            }
            path[^1].IsSelected = true;
        }
    }

    private void DeselectAll(IEnumerable<DomNodeModel> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsSelected = false;
            DeselectAll(node.Children);
        }
    }

    private bool FindNodePath(IEnumerable<DomNodeModel> nodes, int nodeId, List<DomNodeModel> path)
    {
        foreach (var node in nodes)
        {
            path.Add(node);
            if (node.NodeId == nodeId)
            {
                return true;
            }
            if (FindNodePath(node.Children, nodeId, path))
            {
                return true;
            }
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[8192];
        try
        {
            while (_ws != null && _ws.State == WebSocketState.Open && !_cts!.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;

                var jsonStr = Encoding.UTF8.GetString(ms.ToArray());
                var node = JsonNode.Parse(jsonStr) as JsonObject;
                if (node == null) continue;

                if (node.ContainsKey("id"))
                {
                    int id = node["id"]!.GetValue<int>();
                    if (_pendingRequests.TryRemove(id, out var tcs))
                    {
                        tcs.SetResult(node);
                    }
                }
                else if (node.ContainsKey("method"))
                {
                    string method = node["method"]!.GetValue<string>();
                    var parameters = node["params"] as JsonObject;

                    if (method == "Log.entryAdded" && parameters != null)
                    {
                        var entry = parameters["entry"] as JsonObject;
                        if (entry != null)
                        {
                            string text = entry["text"]?.GetValue<string>() ?? "";
                            string level = entry["level"]?.GetValue<string>() ?? "info";
                            double timestampMs = entry["timestamp"]?.GetValue<double>() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)timestampMs).LocalDateTime;

                            Dispatcher.UIThread.Post(() =>
                            {
                                _logs.Add(new LogModel(timestamp, level, text));
                                if (_logs.Count > 100) _logs.RemoveAt(0);
                            });
                        }
                    }
                    else if (method == "Network.requestWillBeSent" && parameters != null)
                    {
                        string requestId = parameters["requestId"]?.GetValue<string>() ?? "";
                        var request = parameters["request"] as JsonObject;
                        if (request != null && !string.IsNullOrEmpty(requestId))
                        {
                            string url = request["url"]?.GetValue<string>() ?? "";
                            string reqMethod = request["method"]?.GetValue<string>() ?? "GET";
                            
                            var reqHeaders = request["headers"] as JsonObject;
                            var sbHeaders = new StringBuilder();
                            if (reqHeaders != null)
                            {
                                foreach (var header in reqHeaders)
                                {
                                    sbHeaders.AppendLine($"{header.Key}: {header.Value}");
                                }
                            }

                            Dispatcher.UIThread.Post(() =>
                            {
                                var existing = _networkRequests.FirstOrDefault(r => r.RequestId == requestId);
                                if (existing == null)
                                {
                                    _networkRequests.Add(new NetworkRequestModel
                                    {
                                        RequestId = requestId,
                                        Url = url,
                                        Method = reqMethod,
                                        Status = "Pending",
                                        Time = "--",
                                        RequestHeaders = sbHeaders.ToString()
                                    });
                                    if (_networkRequests.Count > 100) _networkRequests.RemoveAt(0);
                                }
                            });
                        }
                    }
                    else if (method == "Network.responseReceived" && parameters != null)
                    {
                        string requestId = parameters["requestId"]?.GetValue<string>() ?? "";
                        var response = parameters["response"] as JsonObject;
                        if (response != null && !string.IsNullOrEmpty(requestId))
                        {
                            int status = response["status"]?.GetValue<int>() ?? 200;
                            string statusText = response["statusText"]?.GetValue<string>() ?? "OK";
                            
                            var resHeaders = response["headers"] as JsonObject;
                            var sbHeaders = new StringBuilder();
                            if (resHeaders != null)
                            {
                                foreach (var header in resHeaders)
                                {
                                    sbHeaders.AppendLine($"{header.Key}: {header.Value}");
                                }
                            }

                            Dispatcher.UIThread.Post(() =>
                            {
                                var existing = _networkRequests.FirstOrDefault(r => r.RequestId == requestId);
                                if (existing != null)
                                {
                                    existing.Status = $"{status} {statusText}";
                                    existing.ResponseHeaders = sbHeaders.ToString();
                                }
                            });
                        }
                    }
                    else if (method == "Network.loadingFinished" && parameters != null)
                    {
                        string requestId = parameters["requestId"]?.GetValue<string>() ?? "";
                        Dispatcher.UIThread.Post(() =>
                        {
                            var existing = _networkRequests.FirstOrDefault(r => r.RequestId == requestId);
                            if (existing != null)
                            {
                                existing.Time = "Finished";
                                _ = FetchResponseBodyAsync(existing);
                            }
                        });
                    }
                    else if (method == "Overlay.inspectNodeRequested" && parameters != null)
                    {
                        int backendNodeId = parameters["backendNodeId"]?.GetValue<int>() ?? 0;
                        if (backendNodeId > 0)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                btnInspect.IsChecked = false;
                                SelectNodeById(backendNodeId);
                            });
                        }
                    }
                }
            }
        }
        catch { }
        finally
        {
            Dispatcher.UIThread.Post(() => {
                if (_ws != null) BtnDisconnect_Click(null, null!);
            });
        }
    }

    private async Task<JsonObject> SendCommandAsync(string method, JsonObject parameters)
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
        {
            throw new Exception("Not connected to a target");
        }

        int id = Interlocked.Increment(ref _messageId);
        var request = new JsonObject
        {
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters
        };

        var tcs = new TaskCompletionSource<JsonObject>();
        _pendingRequests[id] = tcs;

        var bytes = Encoding.UTF8.GetBytes(request.ToJsonString());
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

        var response = await tcs.Task;
        if (response.ContainsKey("error"))
        {
            var err = response["error"] as JsonObject;
            throw new Exception(err?["message"]?.GetValue<string>() ?? "Unknown CDP error");
        }

        return response["result"] as JsonObject ?? new JsonObject();
    }

    private async void BtnApplyStyleText_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedNode == null) return;
        string styleText = txtStyleText.Text ?? "";

        try
        {
            var edits = new JsonArray
            {
                new JsonObject
                {
                    ["styleSheetId"] = _selectedNode.NodeId.ToString(),
                    ["range"] = new JsonObject
                    {
                        ["startLine"] = 0,
                        ["startColumn"] = 0,
                        ["endLine"] = 0,
                        ["endColumn"] = 0
                    },
                    ["text"] = styleText
                }
            };

            await SendCommandAsync("CSS.setStyleTexts", new JsonObject { ["edits"] = edits });

            // Reload node info and properties/styles
            TreeDom_SelectionChanged(null, null!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Apply style text failed: {ex.Message}");
        }
    }
}

public class TargetItem
{
    public string Title { get; }
    public string WebSocketUrl { get; }
    public string Id { get; }

    public TargetItem(string title, string wsUrl, string id)
    {
        Title = title;
        WebSocketUrl = wsUrl;
        Id = id;
    }

    public override string ToString() => $"{Title} ({Id.Substring(0, Math.Min(8, Id.Length))})";
}

public class DomNodeModel : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public int NodeId { get; }
    public string NodeName { get; }
    public string DisplayName { get; set; } = "";
    public ObservableCollection<DomNodeModel> Children { get; } = new();
    public ObservableCollection<AttributeModel> AttributesList { get; } = new();

    public IBrush ForegroundBrush => NodeName.StartsWith("#") ? Brushes.Gray : Brushes.CornflowerBlue;

    public DomNodeModel(int nodeId, string nodeName)
    {
        NodeId = nodeId;
        NodeName = nodeName;
    }
}

public class AttributeModel
{
    public string Name { get; }
    public string Value { get; set; }

    public AttributeModel(string name, string val)
    {
        Name = name;
        Value = val;
    }
}

public class PropertyModel
{
    public string Name { get; }
    public string Value { get; set; }
    public string Type { get; }

    public PropertyModel(string name, string val, string type)
    {
        Name = name;
        Value = val;
        Type = type;
    }
}

public class LogModel
{
    public DateTime Timestamp { get; }
    public string Level { get; }
    public string Text { get; }

    public IBrush LevelBrush => Level.ToLowerInvariant() switch
    {
        "warning" => Brushes.Orange,
        "error" => Brushes.Red,
        "verbose" => Brushes.Gray,
        _ => Brushes.Green
    };

    public LogModel(DateTime ts, string level, string text)
    {
        Timestamp = ts;
        Level = level;
        Text = text;
    }
}

public class CssPropertyModel
{
    public string Name { get; }
    public string Value { get; set; }

    public CssPropertyModel(string name, string val)
    {
        Name = name;
        Value = val;
    }
}

public class ConsoleItemModel
{
    public string Expression { get; }
    public string Result { get; }
    public bool IsError { get; }
    public IBrush ResultBrush => IsError ? Brushes.Red : Brushes.LightGreen;

    public ConsoleItemModel(string expr, string res, bool isError = false)
    {
        Expression = expr;
        Result = res;
        IsError = isError;
    }
}

public class EventListenerModel
{
    public string Type { get; }
    public string HandlerName { get; }
    public bool UseCapture { get; }
    public string CaptureText => UseCapture ? "Capture" : "Bubble";

    public EventListenerModel(string type, string handlerName, bool useCapture)
    {
        Type = type;
        HandlerName = handlerName;
        UseCapture = useCapture;
    }
}

public class NetworkRequestModel : System.ComponentModel.INotifyPropertyChanged
{
    private string _status = "Pending";
    private string _time = "--";
    private string _responseHeaders = "";
    private string _responseBody = "";

    public string RequestId { get; set; } = "";
    public string Url { get; set; } = "";
    public string Method { get; set; } = "";
    public string Type { get; set; } = "XHR";
    public string RequestHeaders { get; set; } = "";

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

    public string Time
    {
        get => _time;
        set { _time = value; OnPropertyChanged(nameof(Time)); }
    }

    public string ResponseHeaders
    {
        get => _responseHeaders;
        set { _responseHeaders = value; OnPropertyChanged(nameof(ResponseHeaders)); }
    }

    public string ResponseBody
    {
        get => _responseBody;
        set { _responseBody = value; OnPropertyChanged(nameof(ResponseBody)); }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class WorkspaceFileNode
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsDirectory { get; set; }
    public ObservableCollection<WorkspaceFileNode> Children { get; } = new();
}

public class ResourceEntryModel : System.ComponentModel.INotifyPropertyChanged
{
    private string _value = "";
    public string Key { get; set; } = "";
    public string Type { get; set; } = "";

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(nameof(Value)); }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class ControlCountModel
{
    public string Type { get; set; } = "";
    public int Count { get; set; }
}

public partial class MainWindow : Window
{
    private readonly List<double> _memoryHistory = new();

    private void RenderMemoryChart()
    {
        canvasMemoryChart.Children.Clear();
        if (_memoryHistory.Count < 2) return;

        double max = _memoryHistory.Max();
        double min = _memoryHistory.Min();
        if (max == min) max += 1.0;

        double width = canvasMemoryChart.Bounds.Width;
        double height = canvasMemoryChart.Bounds.Height;
        if (width <= 0) width = 300;
        if (height <= 0) height = 140;

        double stepX = width / (_memoryHistory.Count - 1);
        var points = new List<Avalonia.Point>();

        for (int i = 0; i < _memoryHistory.Count; i++)
        {
            double val = _memoryHistory[i];
            double pct = (val - min) / (max - min);
            double x = i * stepX;
            double y = height - (pct * (height - 30) + 15);
            points.Add(new Avalonia.Point(x, y));
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            var line = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = points[i],
                EndPoint = points[i + 1],
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 2
            };
            canvasMemoryChart.Children.Add(line);
        }
    }

    private void BtnClearNetwork_Click(object? sender, RoutedEventArgs e)
    {
        _networkRequests.Clear();
        lblNetUrl.Text = "Select a request";
        txtNetReqHeaders.Text = "";
        txtNetResHeaders.Text = "";
        txtNetBody.Text = "";
    }

    private void LstNetworkRequests_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = lstNetworkRequests.SelectedItem as NetworkRequestModel;
        if (selected != null)
        {
            lblNetUrl.Text = selected.Url;
            txtNetReqHeaders.Text = selected.RequestHeaders;
            txtNetResHeaders.Text = selected.ResponseHeaders;
            txtNetBody.Text = selected.ResponseBody;
        }
        else
        {
            lblNetUrl.Text = "Select a request";
            txtNetReqHeaders.Text = "";
            txtNetResHeaders.Text = "";
            txtNetBody.Text = "";
        }
    }

    private async Task FetchResponseBodyAsync(NetworkRequestModel req)
    {
        try
        {
            var p = new JsonObject { ["requestId"] = req.RequestId };
            var response = await SendCommandAsync("Network.getResponseBody", p);
            var result = response["result"] as JsonObject;
            if (result != null)
            {
                string body = result["body"]?.GetValue<string>() ?? "";
                Dispatcher.UIThread.Post(() =>
                {
                    req.ResponseBody = body;
                    var selected = lstNetworkRequests.SelectedItem as NetworkRequestModel;
                    if (selected == req)
                    {
                        txtNetBody.Text = body;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching response body: {ex.Message}");
        }
    }

    private void LoadWorkspaceFiles(JsonArray filesArray)
    {
        var root = new WorkspaceFileNode { Name = "Workspace", Path = "", IsDirectory = true };
        foreach (var fileNode in filesArray)
        {
            if (fileNode is not JsonObject fileObj) continue;
            string relPath = fileObj["path"]?.GetValue<string>() ?? "";
            string name = fileObj["name"]?.GetValue<string>() ?? "";
            
            string[] parts = relPath.Split('/');
            var current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                bool isLast = (i == parts.Length - 1);
                
                var existing = current.Children.FirstOrDefault(c => c.Name == part);
                if (existing == null)
                {
                    var newNode = new WorkspaceFileNode
                    {
                        Name = part,
                        Path = string.Join('/', parts, 0, i + 1),
                        IsDirectory = !isLast
                    };
                    current.Children.Add(newNode);
                    current = newNode;
                }
                else
                {
                    current = existing;
                }
            }
        }
        
        treeWorkspaceFiles.ItemsSource = root.Children;
    }

    private async void TreeWorkspaceFiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = treeWorkspaceFiles.SelectedItem as WorkspaceFileNode;
        if (selected != null && !selected.IsDirectory)
        {
            lblSourceFileName.Text = selected.Name;
            txtSourceContent.Text = "Loading content...";
            try
            {
                var p = new JsonObject { ["path"] = selected.Path };
                var response = await SendCommandAsync("Sources.getFileContent", p);
                var result = response["result"] as JsonObject;
                if (result != null)
                {
                    string content = result["content"]?.GetValue<string>() ?? "";
                    txtSourceContent.Text = content;
                }
            }
            catch (Exception ex)
            {
                txtSourceContent.Text = $"Error loading file: {ex.Message}";
            }
        }
        else
        {
            lblSourceFileName.Text = "Select a file from workspace";
            txtSourceContent.Text = "";
        }
    }

    private async void BtnRefreshResources_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var response = await SendCommandAsync("Application.getResources", new JsonObject());
            var result = response["result"] as JsonObject;
            if (result != null)
            {
                var resources = result["resources"] as JsonArray;
                if (resources != null)
                {
                    _resources.Clear();
                    foreach (var resNode in resources)
                    {
                        if (resNode is JsonObject resObj)
                        {
                            _resources.Add(new ResourceEntryModel
                            {
                                Key = resObj["key"]?.GetValue<string>() ?? "",
                                Type = resObj["type"]?.GetValue<string>() ?? "",
                                Value = resObj["value"]?.GetValue<string>() ?? ""
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing resources: {ex.Message}");
        }
    }

    private void LstApplicationResources_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = lstApplicationResources.SelectedItem as ResourceEntryModel;
        if (selected != null)
        {
            txtResourceKey.Text = selected.Key;
            txtResourceValue.Text = selected.Value;
        }
    }

    private void BtnAddResource_Click(object? sender, RoutedEventArgs e)
    {
        txtResourceKey.Text = "NewKey";
        txtResourceValue.Text = "";
        txtResourceKey.Focus();
    }

    private async void BtnSaveResource_Click(object? sender, RoutedEventArgs e)
    {
        string key = txtResourceKey.Text?.Trim() ?? "";
        string val = txtResourceValue.Text ?? "";
        if (string.IsNullOrEmpty(key)) return;

        try
        {
            var p = new JsonObject
            {
                ["key"] = key,
                ["value"] = val
            };
            await SendCommandAsync("Application.setResource", p);
            BtnRefreshResources_Click(null, null!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving resource: {ex.Message}");
        }
    }

    private async void BtnDeleteResource_Click(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        string key = btn?.Tag as string ?? "";
        if (string.IsNullOrEmpty(key)) return;

        try
        {
            var p = new JsonObject { ["key"] = key };
            await SendCommandAsync("Application.deleteResource", p);
            BtnRefreshResources_Click(null, null!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting resource: {ex.Message}");
        }
    }

    private async void BtnCollectGarbage_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SendCommandAsync("Memory.collectGarbage", new JsonObject());
            BtnRefreshMetrics_Click(null, null!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error collecting garbage: {ex.Message}");
        }
    }

    private void TreeAppNav_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = treeAppNav.SelectedItem as AppNavNode;
        if (selected != null && selected.Name == "Global Resources")
        {
            gridResourceEditor.IsVisible = true;
        }
        else
        {
            gridResourceEditor.IsVisible = false;
        }
    }
}

public class AppNavNode : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public string Name { get; set; } = "";
    public ObservableCollection<AppNavNode> Children { get; } = new();

    public AppNavNode(string name)
    {
        Name = name;
    }
}
