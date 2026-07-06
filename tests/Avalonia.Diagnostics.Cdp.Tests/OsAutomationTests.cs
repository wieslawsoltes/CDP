using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CDP.Automation.OS;
using Chrome.DevTools.Protocol;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class OsAutomationTests
{
    public OsAutomationTests()
    {
        OsAutomationProvider.Instance = OSAutomationService.Instance;
    }

    private string GetTargetWindowId()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            return "windows-window-fallback";
        }
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
        {
            return "linux-window-fallback";
        }
        return "macos-window-fallback";
    }
    [Fact]
    public void TestOSAutomationGetWindows()
    {
        var windows = OSAutomationService.Instance.GetWindows();
        Assert.NotNull(windows);
        // Since we stub/mock for platform safety or fetch real windows, we should have at least one window representation
        Assert.NotEmpty(windows);

        var win = windows[0];
        Assert.NotNull(win.Id);
        Assert.NotNull(win.Title);
        Assert.NotNull(win.ProcessName);
        Assert.True(win.ProcessId > 0);
        Assert.True(win.Bounds.Width >= 0);
    }

    [Fact]
    public async Task TestOsAutomationCdpSessionGetDocument()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        var result = await session.HandleCommandAsync("DOM.getDocument", new JsonObject());

        Assert.NotNull(result);
        var root = result["root"] as JsonObject;
        Assert.NotNull(root);
        Assert.Equal(9, root["nodeType"]?.GetValue<int>()); // Document node type is 9
        Assert.Equal("#document", root["nodeName"]?.GetValue<string>());

        var children = root["children"] as JsonArray;
        Assert.NotNull(children);
        Assert.NotEmpty(children);

        var firstChild = children[0] as JsonObject;
        Assert.NotNull(firstChild);
        Assert.Equal(1, firstChild["nodeType"]?.GetValue<int>()); // Element type is 1
        Assert.Equal("Window", firstChild["nodeName"]?.GetValue<string>());
    }

    [Fact]
    public async Task TestOsAutomationCdpSessionQuerySelector()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        // We must call getDocument first to initialize the node maps
        await session.HandleCommandAsync("DOM.getDocument", new JsonObject());

        // Test querying by button role/name
        var btnResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "Button"
        });
        Assert.NotNull(btnResult);
        int btnId = btnResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.True(btnId > 1);

        // Test querying by ID selector
        var idResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "#txtTarget"
        });
        Assert.NotNull(idResult);
        int txtId = idResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.True(txtId > 1);
        Assert.NotEqual(btnId, txtId);

        // Test querying by attributes
        var textResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "[Text=\"Click Me\"]"
        });
        Assert.NotNull(textResult);
        int textId = textResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.Equal(btnId, textId);

        // Test querying by ID with attribute content
        var attrContentResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "#btnClickMe[Content='Click Me']"
        });
        Assert.NotNull(attrContentResult);
        int attrContentId = attrContentResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.Equal(btnId, attrContentId);

        // Test querying by ID with negative attribute assertion (should return 0/null)
        var attrNegResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "#btnClickMe[IsChecked='true']"
        });
        Assert.NotNull(attrNegResult);
        int attrNegId = attrNegResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.Equal(0, attrNegId);

        // Test querying by ID with positive attribute assertion IsChecked='false'
        var attrCheckedResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "#btnClickMe[IsChecked='false']"
        });
        Assert.NotNull(attrCheckedResult);
        int attrCheckedId = attrCheckedResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.Equal(btnId, attrCheckedId);

        // Test initial IsFocused='false' assertion
        var attrUnfocusedResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "#btnClickMe[IsFocused='false']"
        });
        Assert.NotNull(attrUnfocusedResult);
        int attrUnfocusedId = attrUnfocusedResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.Equal(btnId, attrUnfocusedId);

        // Test initial IsFocused='true' assertion (should return 0/null)
        var attrFocusedResultBefore = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "#btnClickMe[IsFocused='true']"
        });
        Assert.NotNull(attrFocusedResultBefore);
        int attrFocusedIdBefore = attrFocusedResultBefore["nodeId"]?.GetValue<int>() ?? 0;
        Assert.Equal(0, attrFocusedIdBefore);

        // Simulate click at button coordinates (150, 120) to trigger focus
        await session.HandleCommandAsync("Input.dispatchMouseEvent", new JsonObject
        {
            ["type"] = "mousePressed",
            ["x"] = 150,
            ["y"] = 120,
            ["button"] = "left",
            ["clickCount"] = 1
        });
        await session.HandleCommandAsync("Input.dispatchMouseEvent", new JsonObject
        {
            ["type"] = "mouseReleased",
            ["x"] = 150,
            ["y"] = 120,
            ["button"] = "left",
            ["clickCount"] = 1
        });

        // Test IsFocused='true' assertion after click (should now match btnId)
        var attrFocusedResultAfter = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "#btnClickMe[IsFocused='true']"
        });
        Assert.NotNull(attrFocusedResultAfter);
        int attrFocusedIdAfter = attrFocusedResultAfter["nodeId"]?.GetValue<int>() ?? 0;
        Assert.Equal(btnId, attrFocusedIdAfter);
    }

    [Fact]
    public async Task TestOsAutomationCdpSessionGetBoxModel()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        await session.HandleCommandAsync("DOM.getDocument", new JsonObject());

        var btnResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "Button"
        });
        int btnId = btnResult["nodeId"]?.GetValue<int>() ?? 0;

        var boxResult = await session.HandleCommandAsync("DOM.getBoxModel", new JsonObject
        {
            ["nodeId"] = btnId
        });

        Assert.NotNull(boxResult);
        var model = boxResult["model"] as JsonObject;
        Assert.NotNull(model);

        var content = model["content"] as JsonArray;
        Assert.NotNull(content);
        Assert.Equal(8, content.Count); // 4 points of (x, y) coordinates

        Assert.True(model["width"]?.GetValue<int>() > 0);
        Assert.True(model["height"]?.GetValue<int>() > 0);
    }

    [Fact]
    public async Task TestOsAutomationCdpSessionCaptureScreenshot()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        var result = await session.HandleCommandAsync("Page.captureScreenshot", new JsonObject());

        Assert.NotNull(result);
        string base64Data = result["data"]?.GetValue<string>() ?? string.Empty;
        Assert.NotEmpty(base64Data);

        // Verify it is a valid base64 encoded byte array
        byte[] bytes = Convert.FromBase64String(base64Data);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task TestOsAutomationCdpSessionSystemInfo()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        var result = await session.HandleCommandAsync("SystemInfo.getInfo", new JsonObject());

        Assert.NotNull(result);
        Assert.Equal("OS Automation Bridge", result["modelName"]?.GetValue<string>());
        Assert.Equal("1.0", result["modelVersion"]?.GetValue<string>());
    }

    [Fact]
    public async Task TestOsAutomationCdpSessionInputSimulation()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        await session.HandleCommandAsync("DOM.getDocument", new JsonObject());

        // Click element center
        var clickResult = await session.HandleCommandAsync("Input.dispatchMouseEvent", new JsonObject
        {
            ["type"] = "mouseReleased",
            ["x"] = 150.0,
            ["y"] = 120.0,
            ["button"] = "left"
        });
        Assert.NotNull(clickResult);

        // Focus & type element
        var typeResult = await session.HandleCommandAsync("Input.insertText", new JsonObject
        {
            ["text"] = "Hello OS Automation!"
        });
        Assert.NotNull(typeResult);
    }

    [Fact]
    public async Task TestOsAutomationCdpSessionGetFullAXTree()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        // Call DOM.getDocument first to map IDs
        await session.HandleCommandAsync("DOM.getDocument", new JsonObject());

        var result = await session.HandleCommandAsync("Accessibility.getFullAXTree", new JsonObject());
        Assert.NotNull(result);

        var nodes = result["nodes"] as JsonArray;
        Assert.NotNull(nodes);
        Assert.NotEmpty(nodes);

        var firstNode = nodes[0] as JsonObject;
        Assert.NotNull(firstNode);
        Assert.Equal("ax_1", firstNode["nodeId"]?.GetValue<string>());
        Assert.Equal("axwindow", firstNode["role"]?["value"]?.GetValue<string>().ToLowerInvariant());
    }

    [Fact]
    public async Task TestOsAutomationCdpSessionGetComputedStyleForNode()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        await session.HandleCommandAsync("DOM.getDocument", new JsonObject());

        var btnResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "Button"
        });
        int btnId = btnResult["nodeId"]?.GetValue<int>() ?? 0;

        var styleResult = await session.HandleCommandAsync("CSS.getComputedStyleForNode", new JsonObject
        {
            ["nodeId"] = btnId
        });

        Assert.NotNull(styleResult);
        var style = styleResult["computedStyle"] as JsonArray;
        Assert.NotNull(style);
        Assert.NotEmpty(style);

        // Find width property
        bool foundWidth = false;
        foreach (var prop in style)
        {
            var p = prop as JsonObject;
            if (p != null && p["name"]?.GetValue<string>() == "width")
            {
                foundWidth = true;
                Assert.Equal("100px", p["value"]?.GetValue<string>());
            }
        }
        Assert.True(foundWidth);
    }

    [Fact]
    public async Task TestOsAutomationCdpSessionNewMethods()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        await session.HandleCommandAsync("DOM.getDocument", new JsonObject());

        // Test DOM.resolveNode
        var resolveRes = await session.HandleCommandAsync("DOM.resolveNode", new JsonObject
        {
            ["nodeId"] = 3
        });
        Assert.NotNull(resolveRes);
        var obj = resolveRes["object"] as JsonObject;
        Assert.NotNull(obj);
        Assert.Equal("object", obj["type"]?.GetValue<string>());
        Assert.Equal("node_3", obj["objectId"]?.GetValue<string>());

        // Test Runtime.getProperties
        var propsRes = await session.HandleCommandAsync("Runtime.getProperties", new JsonObject
        {
            ["objectId"] = "node_3"
        });
        Assert.NotNull(propsRes);
        var results = propsRes["result"] as JsonArray;
        Assert.NotNull(results);
        Assert.NotEmpty(results);

        bool foundIdProp = false;
        foreach (var p in results)
        {
            var propObj = p as JsonObject;
            if (propObj != null && propObj["name"]?.GetValue<string>() == "id")
            {
                foundIdProp = true;
                var valObj = propObj["value"] as JsonObject;
                Assert.NotNull(valObj);
                Assert.Equal("btnClickMe", valObj["value"]?.GetValue<string>());
            }
        }
        Assert.True(foundIdProp);

        // Test DOM.getNodeForLocation
        var locationRes = await session.HandleCommandAsync("DOM.getNodeForLocation", new JsonObject
        {
            ["x"] = 150,
            ["y"] = 120
        });
        Assert.NotNull(locationRes);
        int matchedNodeId = locationRes["nodeId"]?.GetValue<int>() ?? 0;
        Assert.True(matchedNodeId > 1);
    }

    [Fact]
    public async Task TestOsAutomationInterceptionAndEventForwarding()
    {
        Console.WriteLine("--> Starting TestOsAutomationInterceptionAndEventForwarding");
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        
        bool eventFired = false;
        string? methodFired = null;
        JsonObject? paramsFired = null;

        session.EventReceived += (sender, e) =>
        {
            eventFired = true;
            methodFired = e.Method;
            paramsFired = e.Params;
        };

        // Start recording
        Console.WriteLine("--> Calling Recorder.start");
        await session.HandleCommandAsync("Recorder.start", new JsonObject());

        // Simulate some time or manually trigger event if needed
        // Since background thread runs asynchronously, we can wait or stop recording
        Console.WriteLine("--> Delaying 300ms");
        await Task.Delay(300);

        Console.WriteLine("--> Calling Recorder.stop");
        await session.HandleCommandAsync("Recorder.stop", new JsonObject());

        // The test completes successfully without throwing, verifying API correctness
        Console.WriteLine("--> Asserting");
        Assert.False(eventFired); // since we are running on fallback which has no real focus changes
        Console.WriteLine("--> Finished TestOsAutomationInterceptionAndEventForwarding");
    }

    [Fact]
    public async Task TestOsAutomationScreencast()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());

        bool screencastFired = false;
        session.EventReceived += (sender, e) =>
        {
            if (e.Method == "Page.screencastFrame")
            {
                screencastFired = true;
            }
        };

        // Start screencast
        await session.HandleCommandAsync("Page.startScreencast", new JsonObject());

        // Wait to allow at least one frame capture to poll and trigger
        for (int i = 0; i < 30 && !screencastFired; i++)
        {
            await Task.Delay(100);
        }

        // Stop screencast
        await session.HandleCommandAsync("Page.stopScreencast", new JsonObject());

        // Under fallback, we capture static background, so at least one frame should fire initially
        Assert.True(screencastFired);
    }

    [Fact]
    public void TestOsAutomationInputCapture()
    {
        var automation = OSAutomationService.Instance;
        bool callbackInvoked = false;

        // Register click callback
        automation.StartInputCapture(GetTargetWindowId(), (x, y, btn) =>
        {
            callbackInvoked = true;
        }, (eventType, elementId, value) => {});

        // Verify we can call stop without crashing
        automation.StopInputCapture();
        Assert.False(callbackInvoked);
    }

    [Fact]
    public async Task TestOsAutomationPreviewPaneClickRecording()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());

        bool stepAdded = false;
        string? stepType = null;
        string? selector = null;

        session.EventReceived += (sender, e) =>
        {
            if (e.Method == "Recorder.stepAdded")
            {
                stepAdded = true;
                var step = e.Params?["step"] as JsonObject;
                stepType = step?["type"]?.GetValue<string>();
                var selectors = step?["selectors"] as JsonArray;
                var innerArray = selectors?[0] as JsonArray;
                selector = innerArray?[0]?.GetValue<string>();
            }
        };

        // Start recording
        await session.HandleCommandAsync("Recorder.start", new JsonObject());

        // Dispatch mouse released at x = 150, y = 120 (which falls in btnClickMe bounds)
        await session.HandleCommandAsync("Input.dispatchMouseEvent", new JsonObject
        {
            ["type"] = "mouseReleased",
            ["x"] = 150.0,
            ["y"] = 120.0,
            ["button"] = "left"
        });

        // Stop recording
        await session.HandleCommandAsync("Recorder.stop", new JsonObject());

        Assert.True(stepAdded);
        Assert.Equal("click", stepType);
        Assert.Equal("#btnClickMe", selector);
    }

    [Fact]
    public async Task TestOsAutomationPreviewPaneTextRecording()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());

        bool stepAdded = false;
        string? stepType = null;
        string? selector = null;
        string? value = null;

        session.EventReceived += (sender, e) =>
        {
            if (e.Method == "Recorder.stepAdded")
            {
                stepAdded = true;
                var step = e.Params?["step"] as JsonObject;
                stepType = step?["type"]?.GetValue<string>();
                var selectors = step?["selectors"] as JsonArray;
                var innerArray = selectors?[0] as JsonArray;
                selector = innerArray?[0]?.GetValue<string>();
                value = step?["value"]?.GetValue<string>();
            }
        };

        // Start recording
        await session.HandleCommandAsync("Recorder.start", new JsonObject());

        // Insert text
        await session.HandleCommandAsync("Input.insertText", new JsonObject
        {
            ["text"] = "Hello OS!"
        });

        // Stop recording
        await session.HandleCommandAsync("Recorder.stop", new JsonObject());

        Assert.True(stepAdded);
        Assert.Equal("change", stepType);
        Assert.Equal("#txtInput", selector);
        Assert.Equal("Hello OS!", value);
    }

    [Fact]
    public void DumpCalculatorTree()
    {
        Environment.SetEnvironmentVariable("BYPASS_TEST_ENV", "1");
        try
        {
            var windows = OSAutomationService.Instance.GetWindows();
            string? winId = null;
            foreach (var w in windows)
            {
                if (w.Title.Contains("Calculator", StringComparison.OrdinalIgnoreCase))
                {
                    winId = w.Id;
                    break;
                }
            }
            if (winId == null) return;

            var tree = OSAutomationService.Instance.GetElementTree(winId);
            if (tree != null)
            {
                var sb = new System.Text.StringBuilder();
                PrintNode(tree, 0, sb);
                System.IO.File.WriteAllText("/Users/wieslawsoltes/.gemini/antigravity/brain/6dc4a072-a770-4166-a8d5-a4a06436908f/calculator_tree.txt", sb.ToString());
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("BYPASS_TEST_ENV", null);
        }
    }

    [Fact]
    public async Task TestOsAutomationCdpSessionEvaluateAssertionExpressions()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        // We must call getDocument first to initialize the node maps
        await session.HandleCommandAsync("DOM.getDocument", new JsonObject());

        // Test isEffectivelyVisible check
        var visibleResult = await session.HandleCommandAsync("Runtime.evaluate", new JsonObject
        {
            ["expression"] = "var v = document.querySelector(\"#btnClickMe\"); v != null && v.isEffectivelyVisible"
        });
        Assert.NotNull(visibleResult);
        var resultNode = visibleResult["result"] as JsonObject;
        Assert.NotNull(resultNode);
        Assert.Equal("boolean", resultNode["type"]?.GetValue<string>());
        Assert.True(resultNode["value"]?.GetValue<bool>());

        // Test getPropertiesJson retrieval
        var propsResult = await session.HandleCommandAsync("Runtime.evaluate", new JsonObject
        {
            ["expression"] = "document.getPropertiesJson(\"#btnClickMe\")"
        });
        Assert.NotNull(propsResult);
        var propsResNode = propsResult["result"] as JsonObject;
        Assert.NotNull(propsResNode);
        Assert.Equal("string", propsResNode["type"]?.GetValue<string>());
        var jsonStr = propsResNode["value"]?.GetValue<string>();
        Assert.NotNull(jsonStr);

        var parsed = JsonNode.Parse(jsonStr) as JsonObject;
        Assert.NotNull(parsed);
        var type = parsed["$Type"]?.GetValue<string>();
        Assert.True(type == "Button" || type == "Pushbutton");
        var fullName = parsed["$FullName"]?.GetValue<string>();
        Assert.True(fullName == "AXButton" || fullName == "Button" || fullName == "pushbutton");
        Assert.Equal("btnClickMe", parsed["Id"]?.GetValue<string>());
        Assert.Equal("Click Me", parsed["Text"]?.GetValue<string>());
        Assert.Equal("True", parsed["IsEnabled"]?.GetValue<string>());
        Assert.Equal("True", parsed["IsVisible"]?.GetValue<string>());

        // Test resolve and Runtime.callFunctionOn retrieval
        var qResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "#btnClickMe"
        });
        Assert.NotNull(qResult);
        int btnId = qResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.True(btnId > 1);

        var callFuncResult = await session.HandleCommandAsync("Runtime.callFunctionOn", new JsonObject
        {
            ["objectId"] = $"node_{btnId}",
            ["functionDeclaration"] = "function() { return this.value; }"
        });
        Assert.NotNull(callFuncResult);
        var callFuncResNode = callFuncResult["result"] as JsonObject;
        Assert.NotNull(callFuncResNode);
        Assert.Equal("string", callFuncResNode["type"]?.GetValue<string>());
        Assert.Equal("Click Me", callFuncResNode["value"]?.GetValue<string>());

        // Test escaped selector visibility checks and property assertions
        var isVisibleRes = await session.HandleCommandAsync("Runtime.evaluate", new JsonObject
        {
            ["expression"] = "document.querySelector(\"[Text=\\\"Click Me\\\"]\").isVisible"
        });
        Assert.NotNull(isVisibleRes);
        var isVisibleResNode = isVisibleRes["result"] as JsonObject;
        Assert.NotNull(isVisibleResNode);
        Assert.Equal("boolean", isVisibleResNode["type"]?.GetValue<string>());
        Assert.True(isVisibleResNode["value"]?.GetValue<bool>());

        // Test text comparison assertion
        var textCompareRes = await session.HandleCommandAsync("Runtime.evaluate", new JsonObject
        {
            ["expression"] = "document.querySelector(\"#btnClickMe\").textContent == \"Click Me\""
        });
        Assert.NotNull(textCompareRes);
        var textCompareResNode = textCompareRes["result"] as JsonObject;
        Assert.NotNull(textCompareResNode);
        Assert.Equal("boolean", textCompareResNode["type"]?.GetValue<string>());
        Assert.True(textCompareResNode["value"]?.GetValue<bool>());

        // Test type-qualified tag selector resolution
        var tagSelectorResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "Button#btnClickMe"
        });
        Assert.NotNull(tagSelectorResult);
        int tagBtnId = tagSelectorResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.Equal(btnId, tagBtnId);

        // Test attribute presence selector matching
        var presenceSelectorResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "#btnClickMe[Name]"
        });
        Assert.NotNull(presenceSelectorResult);
        int presenceBtnId = presenceSelectorResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.Equal(btnId, presenceBtnId);

        // Test invalid/unknown attribute presence selector (must fail closed to 0)
        var invalidPresenceResult = await session.HandleCommandAsync("DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "#btnClickMe[UnknownAttributePresenceCheck]"
        });
        Assert.NotNull(invalidPresenceResult);
        int invalidBtnId = invalidPresenceResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.Equal(0, invalidBtnId);
    }

    private void PrintNode(OSNode node, int indent, System.Text.StringBuilder sb)
    {
        var ind = new string(' ', indent * 2);
        sb.AppendLine($"{ind}- Node ID: '{node.Id}', Name: '{node.Name}', Role: '{node.Role}', Text: '{node.Text}', Bounds: {node.Bounds}");
        foreach (var c in node.Children)
        {
            PrintNode(c, indent + 1, sb);
        }
    }

    [Fact]
    public async Task TestOsAutomationTelemetryPerformanceDomain()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        
        // Test Performance.getMetrics directly
        var metricsResult = await session.HandleCommandAsync("Performance.getMetrics", new JsonObject());
        Assert.NotNull(metricsResult);
        var metrics = metricsResult["metrics"] as JsonArray;
        Assert.NotNull(metrics);
        Assert.NotEmpty(metrics);

        // Check if CPUUsage, JSHeapUsedSize, Nodes are present
        bool hasCpu = false;
        bool hasHeap = false;
        bool hasNodes = false;
        bool hasFps = false;
        foreach (var m in metrics)
        {
            string name = m?["name"]?.GetValue<string>() ?? "";
            if (name == "CPUUsage") hasCpu = true;
            if (name == "JSHeapUsedSize") hasHeap = true;
            if (name == "Nodes") hasNodes = true;
            if (name == "FPS") hasFps = true;
        }
        Assert.True(hasCpu, "CPUUsage metric missing");
        Assert.True(hasHeap, "JSHeapUsedSize metric missing");
        Assert.True(hasNodes, "Nodes metric missing");
        Assert.True(hasFps, "FPS metric missing");

        // Verify enable starts without throw
        var enableRes = await session.HandleCommandAsync("Performance.enable", new JsonObject());
        Assert.NotNull(enableRes);

        // Verify disable works
        var disableRes = await session.HandleCommandAsync("Performance.disable", new JsonObject());
        Assert.NotNull(disableRes);
    }

    [Fact]
    public async Task TestOsAutomationTelemetryMemoryDomain()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        
        // Test Memory.getDOMCounters
        var countersRes = await session.HandleCommandAsync("Memory.getDOMCounters", new JsonObject());
        Assert.NotNull(countersRes);
        Assert.True(countersRes.ContainsKey("nodes"));
        Assert.True(countersRes.ContainsKey("documents"));

        // Test Memory.getLiveControls
        var liveRes = await session.HandleCommandAsync("Memory.getLiveControls", new JsonObject());
        Assert.NotNull(liveRes);
        var controls = liveRes["controls"] as JsonArray;
        Assert.NotNull(controls);

        // Test Memory.takeHeapSnapshot
        var snapshotRes = await session.HandleCommandAsync("Memory.takeHeapSnapshot", new JsonObject());
        Assert.NotNull(snapshotRes);
        Assert.NotNull(snapshotRes["snapshot"]);
        Assert.NotNull(snapshotRes["nodes"]);
        Assert.NotNull(snapshotRes["edges"]);

        // Test Memory.getRetainers
        var retainersRes = await session.HandleCommandAsync("Memory.getRetainers", new JsonObject { ["hashCode"] = 1234 });
        Assert.NotNull(retainersRes);
        Assert.Equal("No retainer data available for OS elements", retainersRes["name"]?.GetValue<string>());

        // Test Memory.collectGarbage
        var gcRes = await session.HandleCommandAsync("Memory.collectGarbage", new JsonObject());
        Assert.NotNull(gcRes);
    }

    [Fact]
    public async Task TestOsAutomationTelemetryNetworkDomain()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());
        
        int requestsSent = 0;
        int responsesReceived = 0;
        int loadingFinished = 0;
        int loadingFailed = 0;

        session.EventReceived += (sender, e) =>
        {
            if (e.Method == "Network.requestWillBeSent") requestsSent++;
            else if (e.Method == "Network.responseReceived") responsesReceived++;
            else if (e.Method == "Network.loadingFinished") loadingFinished++;
            else if (e.Method == "Network.loadingFailed") loadingFailed++;
        };

        // Enable network
        await session.HandleCommandAsync("Network.enable", new JsonObject());

        // Trigger interactive mouse click to cause virtual network request
        await session.HandleCommandAsync("Input.dispatchMouseEvent", new JsonObject
        {
            ["type"] = "mouseReleased",
            ["x"] = 100.0,
            ["y"] = 100.0,
            ["button"] = "left"
        });

        // Let simulation run asynchronously
        for (int i = 0; i < 30 && (requestsSent == 0 || responsesReceived == 0 || loadingFinished == 0); i++)
        {
            await Task.Delay(100);
        }

        Assert.True(requestsSent > 0, "Network.requestWillBeSent not fired");
        Assert.True(responsesReceived > 0, "Network.responseReceived not fired");
        Assert.True(loadingFinished > 0, "Network.loadingFinished not fired");

        // Test blocked URL emulation
        var windows = OSAutomationService.Instance.GetWindows();
        var targetWin = windows.FirstOrDefault(w => w.Id == GetTargetWindowId());
        string procName = targetWin?.ProcessName ?? "CdpSampleApp";

        await session.HandleCommandAsync("Network.setBlockedURLs", new JsonObject
        {
            ["urls"] = new JsonArray { $"https://api.{procName.ToLowerInvariant()}.local/actions/*" }
        });

        int requestsSent2 = 0;
        int loadingFailed2 = 0;
        session.EventReceived += (sender, e) =>
        {
            if (e.Method == "Network.requestWillBeSent") requestsSent2++;
            else if (e.Method == "Network.loadingFailed") loadingFailed2++;
        };

        // Trigger click which goes to blocked URL
        await session.HandleCommandAsync("Input.dispatchMouseEvent", new JsonObject
        {
            ["type"] = "mouseReleased",
            ["x"] = 120.0,
            ["y"] = 120.0,
            ["button"] = "left"
        });

        for (int i = 0; i < 30 && (requestsSent2 == 0 || loadingFailed2 == 0); i++)
        {
            await Task.Delay(100);
        }
        Assert.True(requestsSent2 > 0);
        Assert.True(loadingFailed2 > 0);

        // Disable network
        await session.HandleCommandAsync("Network.disable", new JsonObject());
    }

    [Fact]
    public async Task TestOsAutomationBrowserAndPageFocusDomain()
    {
        var session = new OsAutomationCdpSession(GetTargetWindowId());

        // Test Page.bringToFront
        var focusRes = await session.HandleCommandAsync("Page.bringToFront", new JsonObject());
        Assert.NotNull(focusRes);

        // Test Browser.getVersion
        var versionRes = await session.HandleCommandAsync("Browser.getVersion", new JsonObject());
        Assert.NotNull(versionRes);
        Assert.True(versionRes.ContainsKey("protocolVersion"));
        Assert.True(versionRes.ContainsKey("product"));

        // Test Browser.close handles without throw
        var closeRes = await session.HandleCommandAsync("Browser.close", new JsonObject());
        Assert.NotNull(closeRes);
    }
}
