using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class BrowserDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getVersion":
                {
                    return new JsonObject
                    {
                        ["protocolVersion"] = "1.3",
                        ["product"] = "Avalonia/11.3.12",
                        ["revision"] = "1.0",
                        ["userAgent"] = "Mozilla/5.0 (Avalonia UI)",
                        ["jsVersion"] = ".NET 10.0"
                    };
                }

            case "close":
                {
                    if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Browser.{action} is not implemented");
        }
    }
}
