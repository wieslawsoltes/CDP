using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class SchemaDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getDomains":
                {
                    var domainsArray = new JsonArray
                    {
                        new JsonObject { ["name"] = "Accessibility", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Browser", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Console", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "DOM", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "DOMDebugger", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Emulation", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Input", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Log", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Memory", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Network", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Overlay", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Page", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Performance", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Runtime", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Schema", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "SystemInfo", ["version"] = "1.3" },
                        new JsonObject { ["name"] = "Target", ["version"] = "1.3" }
                    };

                    return Task.FromResult(new JsonObject
                    {
                        ["domains"] = domainsArray
                    });
                }

            default:
                throw new Exception($"Method Schema.{action} is not implemented");
        }
    }
}
