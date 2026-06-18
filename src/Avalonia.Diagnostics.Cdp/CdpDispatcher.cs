using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Diagnostics.Cdp.Domains;

namespace Avalonia.Diagnostics.Cdp;

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
            if (domain != "DOM" && domain != "CSS" && domain != "Page" && domain != "Overlay" &&
                domain != "Runtime" && domain != "Accessibility" && domain != "Log" && domain != "Performance" &&
                domain != "Network" && domain != "Recorder" && domain != "Console")
            {
                return new JsonObject();
            }
        }

        switch (domain)
        {
            case "DOM":
                return await DomDomain.HandleAsync(session, action, @params);
            case "DOMDebugger":
                return await DomDebuggerDomain.HandleAsync(session, action, @params);
            case "Memory":
                return await MemoryDomain.HandleAsync(session, action, @params);
            case "Network":
                return await NetworkDomain.HandleAsync(session, action, @params);
            case "Sources":
                return await SourcesDomain.HandleAsync(session, action, @params);
            case "Application":
                return await ApplicationDomain.HandleAsync(session, action, @params);
            case "CSS":
                return await CssDomain.HandleAsync(session, action, @params);
            case "Input":
                return await InputDomain.HandleAsync(session, action, @params);
            case "Page":
                return await PageDomain.HandleAsync(session, action, @params);
            case "Overlay":
                return await OverlayDomain.HandleAsync(session, action, @params);
            case "Runtime":
                return await RuntimeDomain.HandleAsync(session, action, @params);
            case "Target":
                return await TargetDomain.HandleAsync(session, action, @params);
            case "Accessibility":
                return await AccessibilityDomain.HandleAsync(session, action, @params);
            case "Emulation":
                return await EmulationDomain.HandleAsync(session, action, @params);
            case "Log":
                return await LogDomain.HandleAsync(session, action, @params);
            case "Console":
                return await ConsoleDomain.HandleAsync(session, action, @params);
            case "Performance":
                return await PerformanceDomain.HandleAsync(session, action, @params);
            case "Browser":
                return await BrowserDomain.HandleAsync(session, action, @params);
            case "SystemInfo":
                return await SystemInfoDomain.HandleAsync(session, action, @params);
            case "Schema":
                return await SchemaDomain.HandleAsync(session, action, @params);
            case "Recorder":
                return await RecorderDomain.HandleAsync(session, action, @params);
            case "Inspector":
                return await InspectorDomain.HandleAsync(session, action, @params);
            case "Media":
                return await MediaDomain.HandleAsync(session, action, @params);
            case "EventBreakpoints":
                return await EventBreakpointsDomain.HandleAsync(session, action, @params);
            case "DeviceOrientation":
                return await DeviceOrientationDomain.HandleAsync(session, action, @params);
            case "Ads":
                return await AdsDomain.HandleAsync(session, action, @params);
            case "Autofill":
                return await AutofillDomain.HandleAsync(session, action, @params);
            case "BackgroundService":
                return await BackgroundServiceDomain.HandleAsync(session, action, @params);
            case "Cast":
                return await CastDomain.HandleAsync(session, action, @params);
            case "DeviceAccess":
                return await DeviceAccessDomain.HandleAsync(session, action, @params);
            case "FileSystem":
                return await FileSystemDomain.HandleAsync(session, action, @params);
            case "CrashReportContext":
                return await CrashReportContextDomain.HandleAsync(session, action, @params);
            case "PerformanceTimeline":
                return await PerformanceTimelineDomain.HandleAsync(session, action, @params);
            case "Audits":
                return await AuditsDomain.HandleAsync(session, action, @params);
            case "DOMSnapshot":
                return await DOMSnapshotDomain.HandleAsync(session, action, @params);
            case "DOMStorage":
                return await DOMStorageDomain.HandleAsync(session, action, @params);
            case "Preload":
                return await PreloadDomain.HandleAsync(session, action, @params);
            case "WebAudio":
                return await WebAudioDomain.HandleAsync(session, action, @params);
            case "Tethering":
                return await TetheringDomain.HandleAsync(session, action, @params);
            default:
                if (action == "enable" || action == "disable")
                {
                    return new JsonObject();
                }
                throw new Exception($"Domain '{domain}' (method '{method}') is not implemented");
        }
    }
}
