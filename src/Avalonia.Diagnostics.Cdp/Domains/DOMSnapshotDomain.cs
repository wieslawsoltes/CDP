using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class DOMSnapshotDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "captureSnapshot":
            case "disable":
            case "enable":
            case "getSnapshot":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method DOMSnapshot.{action} is not implemented");
        }
    }
}
