using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class BackgroundServiceDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "clearEvents":
            case "setRecording":
            case "startObserving":
            case "stopObserving":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method BackgroundService.{action} is not implemented");
        }
    }
}
