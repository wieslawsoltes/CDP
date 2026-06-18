using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class AuditsDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "checkFormsIssues":
            case "disable":
            case "enable":
            case "getEncodedResponse":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method Audits.{action} is not implemented");
        }
    }
}
