using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

public static class BrowserDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getVersion":
                {
                    return Task.FromResult(new JsonObject
                    {
                        ["protocolVersion"] = "1.3",
                        ["product"] = "ChromeDevToolsProtocol/1.0",
                        ["revision"] = "1.0",
                        ["userAgent"] = "Mozilla/5.0 (Chrome DevTools Protocol)",
                        ["jsVersion"] = ".NET 10.0"
                    });
                }

            case "getBrowserCommandLine":
                {
                    var args = new JsonArray();
                    foreach (var arg in Environment.GetCommandLineArgs())
                    {
                        args.Add(arg);
                    }
                    return Task.FromResult(new JsonObject { ["arguments"] = args });
                }

            case "crash":
                {
                    Environment.Exit(1);
                    return Task.FromResult(new JsonObject());
                }

            case "grantPermissions":
            case "resetPermissions":
            case "setPermission":
            case "setDownloadBehavior":
            case "cancelDownload":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method Browser.{action} is not implemented");
        }
    }
}
