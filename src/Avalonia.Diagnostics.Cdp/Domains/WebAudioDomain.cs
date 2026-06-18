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
                        ["realtimeData"] = new JsonObject()
                    });
                }

            default:
                throw new Exception($"Method WebAudio.{action} is not implemented");
        }
    }
}
