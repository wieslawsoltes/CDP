using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class CastDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "disable":
            case "enable":
            case "setSinkToUse":
            case "startDesktopMirroring":
            case "startTabMirroring":
            case "stopCasting":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method Cast.{action} is not implemented");
        }
    }
}
