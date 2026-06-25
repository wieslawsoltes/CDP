using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

public static class EmulationDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "setTouchEmulationEnabled":
                {
                    session.TouchEmulationEnabled = @params["enabled"]?.GetValue<bool>() ?? false;
                    return Task.FromResult(new JsonObject());
                }

            case "canEmulate":
                {
                    return Task.FromResult(new JsonObject { ["result"] = true });
                }

            case "setGeolocationOverride":
                {
                    session.GeolocationOverride = new JsonObject
                    {
                        ["latitude"] = @params["latitude"]?.GetValue<double>() ?? 0.0,
                        ["longitude"] = @params["longitude"]?.GetValue<double>() ?? 0.0,
                        ["accuracy"] = @params["accuracy"]?.GetValue<double>() ?? 0.0
                    };
                    return Task.FromResult(new JsonObject());
                }

            case "clearGeolocationOverride":
                {
                    session.GeolocationOverride = null;
                    return Task.FromResult(new JsonObject());
                }

            case "setCPUThrottlingRate":
            case "setFocusEmulationEnabled":
            case "setAutoDarkModeOverride":
            case "setUserAgentOverride":
            case "setNavigatorOverrides":
            case "setDefaultBackgroundColorOverride":
            case "setEmitTouchEventsForMouse":
            case "setDocumentCookieDisabled":
            case "setScriptExecutionDisabled":
            case "setScrollbarsHidden":
            case "setTimezoneOverride":
            case "setIdleOverride":
            case "clearIdleOverride":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method Emulation.{action} is not implemented");
        }
    }
}
