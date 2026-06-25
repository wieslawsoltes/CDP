using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

public static class EventBreakpointsDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "disable":
            case "removeInstrumentationBreakpoint":
            case "setInstrumentationBreakpoint":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method EventBreakpoints.{action} is not implemented");
        }
    }
}
