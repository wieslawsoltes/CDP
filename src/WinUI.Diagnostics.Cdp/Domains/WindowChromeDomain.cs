using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace WinUI.Diagnostics.Cdp.Domains;

public static class WindowChromeDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        if (session.Window == null) return new JsonObject();
        var window = session.Window;

        switch (action)
        {
            case "minimize":
            case "maximize":
            case "restore":
                return new JsonObject();

            case "close":
                await window.DispatcherQueue.InvokeAsync(() => window.Close());
                return new JsonObject();

            default:
                throw new Exception($"Method WindowChrome.{action} is not implemented");
        }
    }
}
