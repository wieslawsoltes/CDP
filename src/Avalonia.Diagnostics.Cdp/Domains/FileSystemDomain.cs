using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class FileSystemDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getDirectory":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method FileSystem.{action} is not implemented");
        }
    }
}
