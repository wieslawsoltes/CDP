using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

public static class CrashReportContextDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getEntries":
                {
                    return Task.FromResult(new JsonObject
                    {
                        ["entries"] = new JsonArray()
                    });
                }

            default:
                throw new Exception($"Method CrashReportContext.{action} is not implemented");
        }
    }
}
