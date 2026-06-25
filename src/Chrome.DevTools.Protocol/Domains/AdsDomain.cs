using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

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
                        ["metrics"] = new JsonObject
                        {
                            ["viewportAdDensityByArea"] = 0,
                            ["averageViewportAdDensityByArea"] = 0.0,
                            ["viewportAdCount"] = 0,
                            ["averageViewportAdCount"] = 0.0,
                            ["totalAdCpuTime"] = 0.0,
                            ["totalAdNetworkBytes"] = 0.0
                        }
                    });
                }

            default:
                throw new Exception($"Method Ads.{action} is not implemented");
        }
    }
}
