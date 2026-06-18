using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class MemoryDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getDOMCounters":
                {
                    int documents = CdpServer.GetActiveTargets().Count;
                    int nodes = await Dispatcher.UIThread.InvokeAsync(() => CountVisuals(session.Window));
                    int jsEventListeners = 0;

                    return new JsonObject
                    {
                        ["documents"] = documents,
                        ["nodes"] = nodes,
                        ["jsEventListeners"] = jsEventListeners
                    };
                }

            case "getLiveControls":
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var counts = new Dictionary<string, int>();
                    foreach (var win in CdpServer.GetWindows())
                    {
                        CountControlTypes(win.Window, counts);
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
                });

            case "collectGarbage":
            case "forciblyPurgeJavaScriptMemory":
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                return new JsonObject();

            case "setPressureNotificationsSuppressed":
            case "simulatePressureNotification":
            case "prepareForLeakDetection":
                {
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Memory.{action} is not implemented");
        }
    }

    private static void CountControlTypes(Visual visual, Dictionary<string, int> counts)
    {
        string typeName = visual.GetType().Name;
        counts[typeName] = counts.TryGetValue(typeName, out int c) ? c + 1 : 1;
        
        foreach (var child in visual.GetVisualChildren())
        {
            CountControlTypes(child, counts);
        }
    }

    private static int CountVisuals(Avalonia.Visual visual)
    {
        int count = 1;
        foreach (var child in visual.GetVisualChildren())
        {
            count += CountVisuals(child);
        }
        return count;
    }
}
