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
}
