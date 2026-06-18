using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class DeviceAccessDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "cancelPrompt":
            case "disable":
            case "enable":
            case "selectPrompt":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method DeviceAccess.{action} is not implemented");
        }
    }
}
