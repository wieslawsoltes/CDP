using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

public static class FileSystemDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getDirectory":
                {
                    return Task.FromResult(new JsonObject
                    {
                        ["directory"] = new JsonObject
                        {
                            ["name"] = "root",
                            ["nestedDirectories"] = new JsonArray(),
                            ["nestedFiles"] = new JsonArray()
                        }
                    });
                }

            default:
                throw new Exception($"Method FileSystem.{action} is not implemented");
        }
    }
}
