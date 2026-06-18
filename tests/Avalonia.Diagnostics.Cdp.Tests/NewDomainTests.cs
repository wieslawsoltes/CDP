using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp.Domains;
using Avalonia.Headless.XUnit;
using Xunit;

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
        Assert.NotEmpty(createRes["targetId"]?.GetValue<string>() ?? "");

        var attachRes = await TargetDomain.HandleAsync(session, "attachToTarget", new JsonObject());
        Assert.NotNull(attachRes);
        Assert.Equal("session-1", attachRes["sessionId"]?.GetValue<string>());
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
}
