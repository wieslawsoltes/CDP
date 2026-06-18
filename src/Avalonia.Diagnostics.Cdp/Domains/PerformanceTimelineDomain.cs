using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class PerformanceTimelineDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method PerformanceTimeline.{action} is not implemented");
        }
    }
}
