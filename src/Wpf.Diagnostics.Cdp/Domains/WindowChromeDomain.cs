using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;

namespace Wpf.Diagnostics.Cdp.Domains;

public static class WindowChromeDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        if (session.Window == null) return new JsonObject();
        var window = session.Window;

        switch (action)
        {
            case "minimize":
                await window.Dispatcher.InvokeAsync(() => window.WindowState = WindowState.Minimized);
                return new JsonObject();

            case "maximize":
                await window.Dispatcher.InvokeAsync(() => window.WindowState = WindowState.Maximized);
                return new JsonObject();

            case "restore":
                await window.Dispatcher.InvokeAsync(() => window.WindowState = WindowState.Normal);
                return new JsonObject();

            case "close":
                await window.Dispatcher.InvokeAsync(() => window.Close());
                return new JsonObject();

            default:
                throw new Exception($"Method WindowChrome.{action} is not implemented");
        }
    }
}
