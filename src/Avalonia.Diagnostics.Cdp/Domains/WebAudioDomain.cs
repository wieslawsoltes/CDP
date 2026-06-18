using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class WebAudioDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "disable":
            case "enable":
                {
                    return Task.FromResult(new JsonObject());
                }

            case "getRealtimeData":
                {
                    return Task.FromResult(new JsonObject
                    {
                        ["realtimeData"] = new JsonObject
                        {
                            ["currentTime"] = 0.0,
                            ["renderCapacity"] = 0.0,
                            ["callbackIntervalMean"] = 0.0,
                            ["callbackIntervalVariance"] = 0.0
                        }
                    });
                }

            default:
                throw new Exception($"Method WebAudio.{action} is not implemented");
        }
    }
}
