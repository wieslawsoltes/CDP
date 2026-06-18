using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class AdsDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getAdMetrics":
                {
                    return Task.FromResult(new JsonObject
                    {
                        ["pubsubMetrics"] = new JsonArray()
                    });
                }

            default:
                throw new Exception($"Method Ads.{action} is not implemented");
        }
    }
}
