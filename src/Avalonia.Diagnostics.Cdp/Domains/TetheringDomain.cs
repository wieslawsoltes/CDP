using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class TetheringDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "bind":
            case "unbind":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method Tethering.{action} is not implemented");
        }
    }
}
