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
}
