using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class DOMStorageDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "clear":
            case "disable":
            case "enable":
            case "removeDOMStorageItem":
            case "setDOMStorageItem":
                {
                    return Task.FromResult(new JsonObject());
                }

            case "getDOMStorageItems":
                {
                    return Task.FromResult(new JsonObject
                    {
                        ["entries"] = new JsonArray()
                    });
                }

            default:
                throw new Exception($"Method DOMStorage.{action} is not implemented");
        }
    }
}
