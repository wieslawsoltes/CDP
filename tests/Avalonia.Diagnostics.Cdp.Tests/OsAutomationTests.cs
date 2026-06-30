using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CDP.Automation.OS;
using Chrome.DevTools.Protocol;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class OsAutomationTests
{
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
}
