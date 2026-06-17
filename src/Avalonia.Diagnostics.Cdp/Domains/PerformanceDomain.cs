using System;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class PerformanceDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                return new JsonObject();

            case "getMetrics":
                {
                    double timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                    double jsHeapUsedSize = Process.GetCurrentProcess().WorkingSet64;
                    double jsHeapTotalSize = GC.GetTotalMemory(false);

                    int nodesCount = await Dispatcher.UIThread.InvokeAsync(() => CountVisuals(session.Window));

                    var metricsArray = new JsonArray
                    {
                        new JsonObject { ["name"] = "Timestamp", ["value"] = timestamp },
                        new JsonObject { ["name"] = "Nodes", ["value"] = nodesCount },
                        new JsonObject { ["name"] = "JSHeapUsedSize", ["value"] = jsHeapUsedSize },
                        new JsonObject { ["name"] = "JSHeapTotalSize", ["value"] = jsHeapTotalSize }
                    };

                    return new JsonObject
                    {
                        ["metrics"] = metricsArray
                    };
                }

            default:
                throw new Exception($"Method Performance.{action} is not implemented");
        }
    }

    private static int CountVisuals(Visual visual)
    {
        int count = 1;
        foreach (var child in visual.GetVisualChildren())
        {
            count += CountVisuals(child);
        }
        return count;
    }
}
