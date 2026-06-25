using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol;

public static class CdpDispatcher
{
    public static async Task<JsonObject> DispatchAsync(CdpSession session, string method, JsonObject @params)
    {
        var dotIndex = method.IndexOf('.');
        if (dotIndex == -1)
        {
            throw new Exception($"Invalid method format: {method}");
        }

        var domain = method.Substring(0, dotIndex);
        var action = method.Substring(dotIndex + 1);

        if (action == "enable" || action == "disable")
        {
            var builtInWithoutEnable = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Memory", "Sources", "Application", "Target", "Emulation", "Tracing", "Browser",
                "WindowChrome", "SystemInfo", "Schema", "Inspector", "Media", "EventBreakpoints",
                "DeviceOrientation", "Ads", "Autofill", "BackgroundService", "Cast", "DeviceAccess",
                "FileSystem", "CrashReportContext", "PerformanceTimeline", "Audits", "DOMSnapshot",
                "DOMStorage", "Preload", "WebAudio", "Tethering"
            };

            if (builtInWithoutEnable.Contains(domain))
            {
                return new JsonObject();
            }
        }

        if (CdpDomainRegistry.TryGetHandler(domain, out var handler))
        {
            return await handler!(session, action, @params);
        }

        if (action == "enable" || action == "disable")
        {
            return new JsonObject();
        }

        throw new Exception($"Domain '{domain}' (method '{method}') is not implemented");
    }
}
