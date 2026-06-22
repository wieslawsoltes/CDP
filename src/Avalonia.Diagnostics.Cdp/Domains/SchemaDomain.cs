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
                    var domainsArray = new JsonArray();
                    foreach (var domain in CdpDomainRegistry.GetDomains())
                    {
                        domainsArray.Add(new JsonObject
                        {
                            ["name"] = domain.Name,
                            ["version"] = domain.Version
                        });
                    }

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
