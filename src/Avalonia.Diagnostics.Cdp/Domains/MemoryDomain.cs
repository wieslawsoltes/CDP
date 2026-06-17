using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;

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

            default:
                throw new Exception($"Method Memory.{action} is not implemented");
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
