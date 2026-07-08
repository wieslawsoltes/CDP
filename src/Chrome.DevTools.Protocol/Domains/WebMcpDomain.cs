using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

public static class WebMcpDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        return Task.FromResult(new JsonObject());
    }

    public static void CleanupSession(CdpSession session)
    {
    }
}
