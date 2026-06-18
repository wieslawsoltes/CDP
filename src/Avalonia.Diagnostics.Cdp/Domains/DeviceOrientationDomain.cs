using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class DeviceOrientationDomain
{
    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "clearDeviceOrientationOverride":
            case "setDeviceOrientationOverride":
                {
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method DeviceOrientation.{action} is not implemented");
        }
    }
}
