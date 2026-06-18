using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class MediaDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
            case "disable":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method Media.{action} is not implemented");
        }
    }
}
