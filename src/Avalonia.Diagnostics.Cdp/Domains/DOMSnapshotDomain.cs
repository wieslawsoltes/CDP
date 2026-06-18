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
            case "disable":
            case "enable":
                {
                    return Task.FromResult(new JsonObject());
                }

            case "getSnapshot":
                {
                    return Task.FromResult(new JsonObject
                    {
                        ["domNodes"] = new JsonArray(),
                        ["layoutTreeNodes"] = new JsonArray(),
                        ["computedStyles"] = new JsonArray()
                    });
                }

            case "captureSnapshot":
                {
                    return Task.FromResult(new JsonObject
                    {
                        ["documents"] = new JsonArray(),
                        ["strings"] = new JsonArray()
                    });
                }

            default:
                throw new Exception($"Method DOMSnapshot.{action} is not implemented");
        }
    }
}
