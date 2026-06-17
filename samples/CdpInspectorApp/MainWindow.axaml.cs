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

    private DomNodeModel? _selectedNode;
    private PropertyModel? _selectedProperty;

    public MainWindow()
    {
        InitializeComponent();

        treeDom.ItemsSource = _rootNodes;
        listAttributes.ItemsSource = _attributes;
        listProperties.ItemsSource = _properties;
        listLogs.ItemsSource = _logs;

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

        // Populate Keys ComboBox
        cbKeys.ItemsSource = new List<string> { "Enter", "Tab", "Escape", "Space", "Backspace", "Delete", "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "PageUp", "PageDown", "Home", "End" };

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

            // Start reading incoming frames
            _ = Task.Run(ReceiveLoopAsync);

            // Enable necessary domains
            await SendCommandAsync("DOM.enable", new JsonObject());
            await SendCommandAsync("CSS.enable", new JsonObject());
            await SendCommandAsync("Log.enable", new JsonObject());
            await SendCommandAsync("Performance.enable", new JsonObject());

            // Build initial tree
            await RefreshDomTreeAsync();
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

            _rootNodes.Clear();
            _attributes.Clear();
            _properties.Clear();
            txtSelectedNodeId.Text = "None";
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
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching properties: {ex.Message}");
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
                    else if (name == "JSHeapUsedSize") lblPerfMemory.Text = $"{(val / 1024 / 1024):F2} MB";
                    else if (name == "JSHeapTotalSize") lblPerfGc.Text = $"{(val / 1024 / 1024):F2} MB";
                }
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

public class DomNodeModel
{
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
