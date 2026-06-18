using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class AutofillDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "disable":
            case "enable":
            case "setAddresses":
            case "trigger":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method Autofill.{action} is not implemented");
        }
    }
}
