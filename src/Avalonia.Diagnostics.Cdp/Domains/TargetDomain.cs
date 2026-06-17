using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class TargetDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getTargets":
                {
                    var targetInfos = CdpServer.GetActiveTargets();
                    return new JsonObject { ["targetInfos"] = targetInfos };
                }

            case "setAutoAttach":
                {
                    // STUB: Return success
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Target.{action} is not implemented");
        }
    }
}
