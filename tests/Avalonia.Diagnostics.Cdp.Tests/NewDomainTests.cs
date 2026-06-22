using System;
using System.ComponentModel;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp.Domains;
using Avalonia.Headless.XUnit;
using Xunit;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using CdpInspectorApp.ViewModels;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class NewDomainTests
{
    [AvaloniaFact]
    public async Task TestEmulationDomainResizing()
    {
        var window = new Window
        {
            Title = "Emulation Test Window",
            Width = 400,
            Height = 300
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // Store original size (should be 400, 300)
        Assert.Equal(400, window.Width);
        Assert.Equal(300, window.Height);

        // Set device metrics override
        var setParams = new JsonObject
        {
            ["width"] = 800,
            ["height"] = 600
        };

        var result = await EmulationDomain.HandleAsync(session, "setDeviceMetricsOverride", setParams);
        Assert.NotNull(result);

        // Verify window size changed
        Assert.Equal(800, window.Width);
        Assert.Equal(600, window.Height);

        // Clear device metrics override
        var clearParams = new JsonObject();
        var clearResult = await EmulationDomain.HandleAsync(session, "clearDeviceMetricsOverride", clearParams);
        Assert.NotNull(clearResult);

        // Verify window size restored
        Assert.Equal(400, window.Width);
        Assert.Equal(300, window.Height);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestPerformanceDomainMetrics()
    {
        var window = new Window
        {
            Title = "Performance Test Window",
            Content = new StackPanel
            {
                Children =
                {
                    new Button { Content = "Button 1" },
                    new Button { Content = "Button 2" }
                }
            }
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // Test enable/disable
        var enableResult = await PerformanceDomain.HandleAsync(session, "enable", new JsonObject());
        Assert.NotNull(enableResult);
        Assert.Empty(enableResult);

        var disableResult = await PerformanceDomain.HandleAsync(session, "disable", new JsonObject());
        Assert.NotNull(disableResult);
        Assert.Empty(disableResult);

        // Test getMetrics
        var getMetricsResult = await PerformanceDomain.HandleAsync(session, "getMetrics", new JsonObject());
        Assert.NotNull(getMetricsResult);
        Assert.True(getMetricsResult.ContainsKey("metrics"));

        var metricsArray = getMetricsResult["metrics"] as JsonArray;
        Assert.NotNull(metricsArray);

        // Check if Timestamp is present
        var timestampMetric = metricsArray.FirstOrDefault(m => m?["name"]?.GetValue<string>() == "Timestamp");
        Assert.NotNull(timestampMetric);
        var timestampValue = timestampMetric["value"]?.GetValue<double>() ?? 0;
        Assert.True(timestampValue > 0);

        // Check if Nodes is present
        var nodesMetric = metricsArray.FirstOrDefault(m => m?["name"]?.GetValue<string>() == "Nodes");
        Assert.NotNull(nodesMetric);
        var nodesValue = nodesMetric["value"]?.GetValue<int>() ?? 0;
        Assert.True(nodesValue > 0);

        // Check if JSHeapUsedSize is present
        var heapUsedMetric = metricsArray.FirstOrDefault(m => m?["name"]?.GetValue<string>() == "JSHeapUsedSize");
        Assert.NotNull(heapUsedMetric);
        var heapUsedValue = heapUsedMetric["value"]?.GetValue<double>() ?? 0;
        Assert.True(heapUsedValue > 0);

        // Check if JSHeapTotalSize is present
        var heapTotalMetric = metricsArray.FirstOrDefault(m => m?["name"]?.GetValue<string>() == "JSHeapTotalSize");
        Assert.NotNull(heapTotalMetric);
        var heapTotalValue = heapTotalMetric["value"]?.GetValue<double>() ?? 0;
        Assert.True(heapTotalValue > 0);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestSchemaDomain()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        var result = await SchemaDomain.HandleAsync(session, "getDomains", new JsonObject());
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("domains"));
        var domains = result["domains"] as JsonArray;
        Assert.NotNull(domains);
        Assert.Contains(domains, d => d?["name"]?.GetValue<string>() == "Schema");
        Assert.Contains(domains, d => d?["name"]?.GetValue<string>() == "Accessibility");
    }

    [AvaloniaFact]
    public async Task TestSystemInfoDomain()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        var getFeatureResult = await SystemInfoDomain.HandleAsync(session, "getFeatureState", new JsonObject { ["featureState"] = "test-feature" });
        Assert.NotNull(getFeatureResult);
        Assert.True(getFeatureResult.ContainsKey("featureEnabled"));
        Assert.False(getFeatureResult["featureEnabled"]?.GetValue<bool>());
    }

    [AvaloniaFact]
    public async Task TestTargetDomain()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        var setDiscoverResult = await TargetDomain.HandleAsync(session, "setDiscoverTargets", new JsonObject { ["discover"] = true });
        Assert.NotNull(setDiscoverResult);

        // If target exists, getTargetInfo will succeed
        var getInfoResult = await TargetDomain.HandleAsync(session, "getTargetInfo", new JsonObject());
        Assert.NotNull(getInfoResult);
    }

    [AvaloniaFact]
    public async Task TestLogDomainStubs()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        var startResult = await LogDomain.HandleAsync(session, "startViolationsReport", new JsonObject());
        Assert.NotNull(startResult);

        var stopResult = await LogDomain.HandleAsync(session, "stopViolationsReport", new JsonObject());
        Assert.NotNull(stopResult);
    }

    [AvaloniaFact]
    public async Task TestAccessibilityDomainNewMethods()
    {
        var window = new Window
        {
            Title = "A11y Test Window",
            Content = new StackPanel
            {
                Children =
                {
                    new Button { Content = "Click Me" }
                }
            }
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // Pre-populate NodeMap by fetching document or just root
        var rootResult = await AccessibilityDomain.HandleAsync(session, "getRootAXNode", new JsonObject());
        Assert.NotNull(rootResult);
        Assert.True(rootResult.ContainsKey("node"));

        var rootNode = rootResult["node"] as JsonObject;
        Assert.NotNull(rootNode);
        var rootNodeIdStr = rootNode["nodeId"]?.GetValue<string>();
        Assert.NotNull(rootNodeIdStr);

        // Fetch children
        var getChildResult = await AccessibilityDomain.HandleAsync(session, "getChildAXNodes", new JsonObject { ["id"] = rootNodeIdStr });
        Assert.NotNull(getChildResult);
        Assert.True(getChildResult.ContainsKey("nodes"));

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestBrowserDomainBounds()
    {
        var window = new Window
        {
            Title = "Browser Window Test",
            Width = 600,
            Height = 400
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var targetRes = await BrowserDomain.HandleAsync(session, "getWindowForTarget", new JsonObject());
        Assert.NotNull(targetRes);
        int windowId = targetRes["windowId"]?.GetValue<int>() ?? 0;
        Assert.True(windowId > 0);

        var boundsRes = await BrowserDomain.HandleAsync(session, "getWindowBounds", new JsonObject { ["windowId"] = windowId });
        Assert.NotNull(boundsRes);
        var bounds = boundsRes["bounds"] as JsonObject;
        Assert.NotNull(bounds);
        Assert.Equal(600, bounds["width"]?.GetValue<int>());
        Assert.Equal(400, bounds["height"]?.GetValue<int>());
 
        var setBoundsParams = new JsonObject
        {
            ["windowId"] = windowId,
            ["bounds"] = new JsonObject
            {
                ["width"] = 700,
                ["height"] = 500
            }
        };
        await BrowserDomain.HandleAsync(session, "setWindowBounds", setBoundsParams);
        Assert.Equal(700, window.Width);
        Assert.Equal(500, window.Height);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestRuntimeDomainNewMethods()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        var isolateRes = await RuntimeDomain.HandleAsync(session, "getIsolateId", new JsonObject());
        Assert.NotNull(isolateRes);
        Assert.Equal("1", isolateRes["isolateId"]?.GetValue<string>());

        var heapRes = await RuntimeDomain.HandleAsync(session, "getHeapUsage", new JsonObject());
        Assert.NotNull(heapRes);
        Assert.True(heapRes["usedSize"]?.GetValue<double>() > 0);
    }

    [AvaloniaFact]
    public async Task TestDomDomainNewMethods()
    {
        var window = new Window
        {
            Title = "DOM Test Window",
            Content = new StackPanel
            {
                Name = "mainPanel"
            }
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // Populate NodeMap
        var docRes = await DomDomain.HandleAsync(session, "getDocument", new JsonObject());
        var rootNode = docRes["root"] as JsonObject;
        Assert.NotNull(rootNode);

        var queryRes = await DomDomain.HandleAsync(session, "querySelector", new JsonObject { ["nodeId"] = 1, ["selector"] = "#mainPanel" });
        int panelId = queryRes["nodeId"]?.GetValue<int>() ?? 0;
        Assert.True(panelId > 0);

        var attrRes = await DomDomain.HandleAsync(session, "getAttributes", new JsonObject { ["nodeId"] = panelId });
        Assert.NotNull(attrRes);
        var attrs = attrRes["attributes"] as JsonArray;
        Assert.NotNull(attrs);
        Assert.Contains(attrs, a => a?.GetValue<string>() == "mainPanel");

        var descRes = await DomDomain.HandleAsync(session, "describeNode", new JsonObject { ["nodeId"] = panelId });
        Assert.NotNull(descRes);
        var node = descRes["node"] as JsonObject;
        Assert.NotNull(node);
        Assert.Equal("StackPanel", node["nodeName"]?.GetValue<string>());

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestCssDomainNewStubs()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        var createRes = await CssDomain.HandleAsync(session, "createStyleSheet", new JsonObject());
        Assert.NotNull(createRes);
        Assert.Equal("1", createRes["styleSheetId"]?.GetValue<string>());

        var setRes = await CssDomain.HandleAsync(session, "setStyleSheetText", new JsonObject());
        Assert.NotNull(setRes);
    }

    [AvaloniaFact]
    public async Task TestDomDebuggerNewStubs()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        var setRes = await DomDebuggerDomain.HandleAsync(session, "setEventListenerBreakpoint", new JsonObject());
        Assert.NotNull(setRes);

        var removeRes = await DomDebuggerDomain.HandleAsync(session, "removeEventListenerBreakpoint", new JsonObject());
        Assert.NotNull(removeRes);
    }

    [AvaloniaFact]
    public async Task TestPageDomainNewMethods()
    {
        var window = new Window
        {
            Title = "Page test",
            Width = 400,
            Height = 300
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var frontRes = await PageDomain.HandleAsync(session, "bringToFront", new JsonObject());
        Assert.NotNull(frontRes);

        var metricsRes = await PageDomain.HandleAsync(session, "getLayoutMetrics", new JsonObject());
        Assert.NotNull(metricsRes);
        Assert.True(metricsRes.ContainsKey("cssLayoutViewport"));

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestNetworkDomainNewStubs()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        var checkRes = await NetworkDomain.HandleAsync(session, "canClearBrowserCache", new JsonObject());
        Assert.NotNull(checkRes);
        Assert.True(checkRes["result"]?.GetValue<bool>());

        var clearRes = await NetworkDomain.HandleAsync(session, "clearBrowserCache", new JsonObject());
        Assert.NotNull(clearRes);
    }

    [AvaloniaFact]
    public async Task TestEmulationDomainNewStubs()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        var checkRes = await EmulationDomain.HandleAsync(session, "canEmulate", new JsonObject());
        Assert.NotNull(checkRes);
        Assert.True(checkRes["result"]?.GetValue<bool>());

        var setRes = await EmulationDomain.HandleAsync(session, "setCPUThrottlingRate", new JsonObject { ["rate"] = 4 });
        Assert.NotNull(setRes);
    }

    [AvaloniaFact]
    public async Task TestDomDomainFlattenedDocument()
    {
        var window = new Window
        {
            Title = "DOM Flatten Test",
            Width = 400,
            Height = 300,
            Content = new Button { Content = "Click Me" }
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var result = await DomDomain.HandleAsync(session, "getFlattenedDocument", new JsonObject());
        Assert.NotNull(result);
        var nodes = result["nodes"] as JsonArray;
        Assert.NotNull(nodes);
        Assert.True(nodes.Count > 0);

        var docNode = nodes[0] as JsonObject;
        Assert.NotNull(docNode);
        Assert.Equal("#document", docNode["nodeName"]?.GetValue<string>());

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestAccessibilityDomainPartialTreeAndQuery()
    {
        var window = new Window
        {
            Title = "AX Test",
            Width = 400,
            Height = 300,
            Content = new Button { Content = "Target Button" }
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var partialRes = await AccessibilityDomain.HandleAsync(session, "getPartialAXTree", new JsonObject
        {
            ["nodeId"] = 1,
            ["fetchRelatives"] = true
        });
        Assert.NotNull(partialRes);
        Assert.NotNull(partialRes["nodes"] as JsonArray);

        var queryRes = await AccessibilityDomain.HandleAsync(session, "queryAXTree", new JsonObject
        {
            ["accessibleName"] = "Target Button"
        });
        Assert.NotNull(queryRes);
        Assert.NotNull(queryRes["nodes"] as JsonArray);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestBrowserDomainCommandLineAndCrash()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        var result = await BrowserDomain.HandleAsync(session, "getBrowserCommandLine", new JsonObject());
        Assert.NotNull(result);
        var args = result["arguments"] as JsonArray;
        Assert.NotNull(args);
        Assert.True(args.Count > 0);
    }

    [AvaloniaFact]
    public async Task TestTargetDomainCreateAndAttach()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        var createRes = await TargetDomain.HandleAsync(session, "createTarget", new JsonObject());
        Assert.NotNull(createRes);
        var targetId = createRes["targetId"]?.GetValue<string>();
        Assert.NotEmpty(targetId ?? "");

        var attachRes = await TargetDomain.HandleAsync(session, "attachToTarget", new JsonObject { ["targetId"] = targetId });
        Assert.NotNull(attachRes);
        var sessionId = attachRes["sessionId"]?.GetValue<string>();
        Assert.NotEmpty(sessionId ?? "");

        var foundSessionId = session.GetSessionIdForTarget(targetId!);
        Assert.Equal(sessionId, foundSessionId);

        var detachRes = await TargetDomain.HandleAsync(session, "detachFromTarget", new JsonObject { ["sessionId"] = sessionId });
        Assert.NotNull(detachRes);
        Assert.Null(session.GetSessionIdForTarget(targetId!));
    }

    [AvaloniaFact]
    public async Task TestTargetDiscoveryAndEvents()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        // 1. Enable discover targets
        var discoverRes = await TargetDomain.HandleAsync(session, "setDiscoverTargets", new JsonObject
        {
            ["discover"] = true
        });
        Assert.NotNull(discoverRes);
        Assert.True(session.DiscoverTargetsEnabled);

        // 2. Get active targets
        var targetsRes = await TargetDomain.HandleAsync(session, "getTargets", new JsonObject());
        Assert.NotNull(targetsRes);
        var targetInfos = targetsRes["targetInfos"] as JsonArray;
        Assert.NotNull(targetInfos);
        
        // 3. Register a new window dynamically
        var win = new Window { Title = "Test Dynamic Window" };
        var targetId = CdpServer.Register(win, "Test Dynamic Window");
        Assert.NotEmpty(targetId);

        // 4. Verify we can get target info
        var infoRes = await TargetDomain.HandleAsync(session, "getTargetInfo", new JsonObject { ["targetId"] = targetId });
        Assert.NotNull(infoRes);
        var targetInfo = infoRes["targetInfo"] as JsonObject;
        Assert.NotNull(targetInfo);
        Assert.Equal(targetId, targetInfo["targetId"]?.GetValue<string>());
        Assert.Equal("Test Dynamic Window", targetInfo["title"]?.GetValue<string>());

        // 5. Close/Unregister target
        var closeRes = await TargetDomain.HandleAsync(session, "closeTarget", new JsonObject { ["targetId"] = targetId });
        Assert.NotNull(closeRes);
        Assert.True(closeRes["success"]?.GetValue<bool>());

        // Clean up
        CdpServer.Unregister(win);
    }

    [AvaloniaFact]
    public async Task TestWindowChromeAndTargetActivation()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        // Create a test window
        var win = new Window { Title = "Window Chrome Test Title", Width = 400, Height = 300 };
        var targetId = CdpServer.Register(win, "Window Chrome Test Title");
        int windowId = Math.Abs(win.GetHashCode());

        // 1. Test Target.activateTarget via Dispatcher
        var activateTargetRes = await CdpDispatcher.DispatchAsync(session, "Target.activateTarget", new JsonObject { ["targetId"] = targetId });
        Assert.NotNull(activateTargetRes);

        // 2. Test WindowChrome.setTitle via Dispatcher
        var setTitleRes = await CdpDispatcher.DispatchAsync(session, "WindowChrome.setTitle", new JsonObject
        {
            ["windowId"] = windowId,
            ["title"] = "Updated Dynamic Title"
        });
        Assert.NotNull(setTitleRes);
        Assert.True(setTitleRes["success"]?.GetValue<bool>());
        Assert.Equal("Updated Dynamic Title", win.Title);

        // 3. Test WindowChrome.setTopmost
        var setTopmostRes = await CdpDispatcher.DispatchAsync(session, "WindowChrome.setTopmost", new JsonObject
        {
            ["windowId"] = windowId,
            ["topmost"] = true
        });
        Assert.NotNull(setTopmostRes);
        Assert.True(setTopmostRes["success"]?.GetValue<bool>());
        Assert.True(win.Topmost);

        // 4. Test WindowChrome.setOpacity
        var setOpacityRes = await CdpDispatcher.DispatchAsync(session, "WindowChrome.setOpacity", new JsonObject
        {
            ["windowId"] = windowId,
            ["opacity"] = 0.75
        });
        Assert.NotNull(setOpacityRes);
        Assert.True(setOpacityRes["success"]?.GetValue<bool>());
        Assert.Equal(0.75, win.Opacity);

        // 5. Test WindowChrome.dragWindow
        var initialPos = win.Position;
        var dragRes = await CdpDispatcher.DispatchAsync(session, "WindowChrome.dragWindow", new JsonObject
        {
            ["windowId"] = windowId,
            ["deltaX"] = 50,
            ["deltaY"] = 50
        });
        Assert.NotNull(dragRes);
        Assert.True(dragRes["success"]?.GetValue<bool>());
        Assert.Equal(initialPos.X + 50, win.Position.X);
        Assert.Equal(initialPos.Y + 50, win.Position.Y);

        // 6. Test WindowChrome.minimize / maximize / restore
        var minRes = await CdpDispatcher.DispatchAsync(session, "WindowChrome.minimize", new JsonObject { ["windowId"] = windowId });
        Assert.NotNull(minRes);
        Assert.True(minRes["success"]?.GetValue<bool>());
        Assert.Equal(WindowState.Minimized, win.WindowState);

        var maxRes = await CdpDispatcher.DispatchAsync(session, "WindowChrome.maximize", new JsonObject { ["windowId"] = windowId });
        Assert.NotNull(maxRes);
        Assert.True(maxRes["success"]?.GetValue<bool>());
        Assert.Equal(WindowState.Maximized, win.WindowState);

        var restoreRes = await CdpDispatcher.DispatchAsync(session, "WindowChrome.restore", new JsonObject { ["windowId"] = windowId });
        Assert.NotNull(restoreRes);
        Assert.True(restoreRes["success"]?.GetValue<bool>());
        Assert.Equal(WindowState.Normal, win.WindowState);

        // 7. Test WindowChrome.activate
        var actRes = await CdpDispatcher.DispatchAsync(session, "WindowChrome.activate", new JsonObject { ["windowId"] = windowId });
        Assert.NotNull(actRes);
        Assert.True(actRes["success"]?.GetValue<bool>());

        // 7.5. Test WindowChrome.getWindowDetails
        var detailsRes = await CdpDispatcher.DispatchAsync(session, "WindowChrome.getWindowDetails", new JsonObject { ["windowId"] = windowId });
        Assert.NotNull(detailsRes);
        Assert.True(detailsRes["success"]?.GetValue<bool>());
        Assert.True(detailsRes["topmost"]?.GetValue<bool>());
        Assert.Equal(0.75, detailsRes["opacity"]?.GetValue<double>());
        Assert.Equal("Updated Dynamic Title", detailsRes["title"]?.GetValue<string>());
        Assert.Equal("normal", detailsRes["windowState"]?.GetValue<string>());

        // 8. Test WindowChrome.close
        var closeRes = await CdpDispatcher.DispatchAsync(session, "WindowChrome.close", new JsonObject { ["windowId"] = windowId });
        Assert.NotNull(closeRes);
        Assert.True(closeRes["success"]?.GetValue<bool>());

        // Clean up
        CdpServer.Unregister(win);
    }

    [AvaloniaFact]
    public async Task TestRound5ComplianceDomains()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        Assert.NotNull(await InspectorDomain.HandleAsync(session, "enable", new JsonObject()));
        Assert.NotNull(await MediaDomain.HandleAsync(session, "enable", new JsonObject()));
        Assert.NotNull(await EventBreakpointsDomain.HandleAsync(session, "disable", new JsonObject()));
        Assert.NotNull(await DeviceOrientationDomain.HandleAsync(session, "clearDeviceOrientationOverride", new JsonObject()));
        
        var adsRes = await AdsDomain.HandleAsync(session, "getAdMetrics", new JsonObject());
        Assert.NotNull(adsRes);
        Assert.NotNull(adsRes["metrics"]);

        Assert.NotNull(await AutofillDomain.HandleAsync(session, "enable", new JsonObject()));
        Assert.NotNull(await BackgroundServiceDomain.HandleAsync(session, "startObserving", new JsonObject()));
        Assert.NotNull(await CastDomain.HandleAsync(session, "enable", new JsonObject()));
        Assert.NotNull(await DeviceAccessDomain.HandleAsync(session, "enable", new JsonObject()));
        
        var fileSystemRes = await FileSystemDomain.HandleAsync(session, "getDirectory", new JsonObject());
        Assert.NotNull(fileSystemRes);
        Assert.NotNull(fileSystemRes["directory"]);

        var crashRes = await CrashReportContextDomain.HandleAsync(session, "getEntries", new JsonObject());
        Assert.NotNull(crashRes);
        Assert.NotNull(crashRes["entries"]);

        Assert.NotNull(await PerformanceTimelineDomain.HandleAsync(session, "enable", new JsonObject()));
    }

    [AvaloniaFact]
    public async Task TestRound6ComplianceDomains()
    {
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, null!);

        Assert.NotNull(await AuditsDomain.HandleAsync(session, "enable", new JsonObject()));
        Assert.NotNull(await DOMSnapshotDomain.HandleAsync(session, "enable", new JsonObject()));
        
        var storageRes = await DOMStorageDomain.HandleAsync(session, "getDOMStorageItems", new JsonObject());
        Assert.NotNull(storageRes);
        Assert.NotNull(storageRes["entries"]);

        Assert.NotNull(await PreloadDomain.HandleAsync(session, "enable", new JsonObject()));

        var audioRes = await WebAudioDomain.HandleAsync(session, "getRealtimeData", new JsonObject());
        Assert.NotNull(audioRes);
        Assert.NotNull(audioRes["realtimeData"]);

        Assert.NotNull(await TetheringDomain.HandleAsync(session, "bind", new JsonObject()));
    }

    [AvaloniaFact]
    public async Task TestChromeDevToolsParityFeatures()
    {
        var window = new Avalonia.Controls.Window
        {
            Title = "Audits Test Window",
            Content = new Avalonia.Controls.StackPanel
            {
                Children =
                {
                    new Avalonia.Controls.Button { Content = "Test Button" },
                    new Avalonia.Controls.Button() // invalid button with no content (A11y violation!)
                }
            }
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // 1. Test DOMStorage backend implementation
        JsonObject CreateStorageId() => new JsonObject
        {
            ["securityOrigin"] = "http://localhost:9222",
            ["isLocalStorage"] = true
        };

        // Set
        await DOMStorageDomain.HandleAsync(session, "setDOMStorageItem", new JsonObject
        {
            ["storageId"] = CreateStorageId(),
            ["key"] = "testKey",
            ["value"] = "testValue"
        });

        // Get
        var itemsRes = await DOMStorageDomain.HandleAsync(session, "getDOMStorageItems", new JsonObject
        {
            ["storageId"] = CreateStorageId()
        });
        Assert.NotNull(itemsRes);
        var entries = itemsRes["entries"] as JsonArray;
        Assert.NotNull(entries);
        Assert.Contains(entries, e => e?[0]?.GetValue<string>() == "testKey" && e?[1]?.GetValue<string>() == "testValue");

        // Remove
        await DOMStorageDomain.HandleAsync(session, "removeDOMStorageItem", new JsonObject
        {
            ["storageId"] = CreateStorageId(),
            ["key"] = "testKey"
        });
        
        // Verify removed
        var itemsRes2 = await DOMStorageDomain.HandleAsync(session, "getDOMStorageItems", new JsonObject
        {
            ["storageId"] = CreateStorageId()
        });
        var entries2 = itemsRes2["entries"] as JsonArray;
        Assert.NotNull(entries2);
        Assert.Empty(entries2);

        // 2. Test Audits runDiagnostics
        var auditsRes = await AuditsDomain.HandleAsync(session, "runDiagnostics", new JsonObject());
        Assert.NotNull(auditsRes);
        Assert.True(auditsRes.ContainsKey("accessibilityScore"));
        Assert.True(auditsRes.ContainsKey("bestPracticesScore"));
        Assert.True(auditsRes.ContainsKey("layoutScore"));
        var issues = auditsRes["issues"] as JsonArray;
        Assert.NotNull(issues);
        Assert.Contains(issues, i => i?["category"]?.GetValue<string>() == "Accessibility" && i?["message"]?.GetValue<string>().Contains("missing an accessible name") == true);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestDOMStorageSessionIsolation()
    {
        var window1 = new Avalonia.Controls.Window { Title = "Window 1" };
        var window2 = new Avalonia.Controls.Window { Title = "Window 2" };
        window1.Show();
        window2.Show();

        using var clientWs1 = new ClientWebSocket();
        using var clientWs2 = new ClientWebSocket();
        var session1 = new CdpSession(clientWs1, window1);
        var session2 = new CdpSession(clientWs2, window2);

        // 1. Test Session Storage (isLocalStorage = false) - should be isolated
        JsonObject CreateSessionStorageId() => new JsonObject
        {
            ["securityOrigin"] = "http://localhost:9222",
            ["isLocalStorage"] = false
        };

        // Set on session 1
        await DOMStorageDomain.HandleAsync(session1, "setDOMStorageItem", new JsonObject
        {
            ["storageId"] = CreateSessionStorageId(),
            ["key"] = "sessionKey",
            ["value"] = "sessionVal1"
        });

        // Get from session 1
        var itemsRes1 = await DOMStorageDomain.HandleAsync(session1, "getDOMStorageItems", new JsonObject
        {
            ["storageId"] = CreateSessionStorageId()
        });
        var entries1 = itemsRes1["entries"] as JsonArray;
        Assert.NotNull(entries1);
        Assert.Contains(entries1, e => e?[0]?.GetValue<string>() == "sessionKey" && e?[1]?.GetValue<string>() == "sessionVal1");

        // Get from session 2 - should not contain sessionKey
        var itemsRes2 = await DOMStorageDomain.HandleAsync(session2, "getDOMStorageItems", new JsonObject
        {
            ["storageId"] = CreateSessionStorageId()
        });
        var entries2 = itemsRes2["entries"] as JsonArray;
        Assert.NotNull(entries2);
        Assert.DoesNotContain(entries2, e => e?[0]?.GetValue<string>() == "sessionKey");

        // 2. Test Local Storage (isLocalStorage = true) - should be shared
        JsonObject CreateLocalStorageId() => new JsonObject
        {
            ["securityOrigin"] = "http://localhost:9222",
            ["isLocalStorage"] = true
        };

        // Set on session 1
        await DOMStorageDomain.HandleAsync(session1, "setDOMStorageItem", new JsonObject
        {
            ["storageId"] = CreateLocalStorageId(),
            ["key"] = "localKey",
            ["value"] = "localVal"
        });

        // Get from session 2 - should find localVal since it's shared across the same origin
        var localItemsRes2 = await DOMStorageDomain.HandleAsync(session2, "getDOMStorageItems", new JsonObject
        {
            ["storageId"] = CreateLocalStorageId()
        });
        var localEntries2 = localItemsRes2["entries"] as JsonArray;
        Assert.NotNull(localEntries2);
        Assert.Contains(localEntries2, e => e?[0]?.GetValue<string>() == "localKey" && e?[1]?.GetValue<string>() == "localVal");

        // Clean up
        await DOMStorageDomain.HandleAsync(session1, "clear", new JsonObject
        {
            ["storageId"] = CreateLocalStorageId()
        });

        window1.Close();
        window2.Close();
    }

    public class MockInspectorCdpService : ICdpService
    {
        public bool IsConnected { get; set; }
        public string ConnectionStatus { get; set; } = "Disconnected";
        public string ConnectedHost { get; set; } = "";
        public string ConnectedTargetId { get; set; } = "";
        public bool IsPreviewScreencastActive { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        private void NotifyPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public Task<System.Collections.Generic.List<TargetItem>> GetTargetsAsync(string host)
        {
            return Task.FromResult(new System.Collections.Generic.List<TargetItem>());
        }

        public Task ConnectAsync(string host, TargetItem target)
        {
            ConnectedHost = host;
            ConnectedTargetId = target.Id;
            IsConnected = true;
            ConnectionStatus = "Connected";
            NotifyPropertyChanged(nameof(IsConnected));
            NotifyPropertyChanged(nameof(ConnectedTargetId));
            NotifyPropertyChanged(nameof(ConnectionStatus));
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            ConnectedHost = "";
            ConnectedTargetId = "";
            IsConnected = false;
            ConnectionStatus = "Disconnected";
            NotifyPropertyChanged(nameof(IsConnected));
            NotifyPropertyChanged(nameof(ConnectedTargetId));
            NotifyPropertyChanged(nameof(ConnectionStatus));
            return Task.CompletedTask;
        }

        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            return Task.FromResult(new JsonObject());
        }
    }

    [Fact]
    public async Task TestConnectionViewModelTargetSwitching()
    {
        var service = new MockInspectorCdpService();
        var vm = new ConnectionViewModel(service);

        var targetA = new TargetItem("Window A", "ws://localhost:9222/a", "id-a");
        var targetB = new TargetItem("Window B", "ws://localhost:9222/b", "id-b");

        vm.Targets.Add(targetA);
        vm.Targets.Add(targetB);

        // 1. Initial State
        Assert.False(vm.IsConnected);
        Assert.Null(vm.SelectedTarget);

        // 2. Select targetA and connect manually
        vm.SelectedTarget = targetA;
        Assert.True(vm.ConnectCommand.CanExecute(null));
        await vm.ConnectAsync();

        Assert.True(vm.IsConnected);
        Assert.Equal("id-a", service.ConnectedTargetId);

        // 3. Select targetB while connected
        // This should auto-trigger ConnectAsync to target B
        vm.SelectedTarget = targetB;
        
        // Wait a small moment for async task to run
        await Task.Delay(50);

        Assert.True(vm.IsConnected);
        Assert.Equal("id-b", service.ConnectedTargetId);

        // 4. ConnectCommand should still be executable if selecting target A again
        vm.SelectedTarget = targetA;
        await Task.Delay(50);
        Assert.Equal("id-a", service.ConnectedTargetId);
    }
}

