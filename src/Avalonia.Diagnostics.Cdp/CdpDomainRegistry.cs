using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp;

public static class CdpDomainRegistry
{
    private static readonly ConcurrentDictionary<string, (Func<CdpSession, string, JsonObject, Task<JsonObject>> Handler, string Version)> s_domains = new(StringComparer.OrdinalIgnoreCase);

    static CdpDomainRegistry()
    {
        s_domains["DOM"] = (Domains.DomDomain.HandleAsync, "1.3");
        s_domains["DOMDebugger"] = (Domains.DomDebuggerDomain.HandleAsync, "1.3");
        s_domains["Memory"] = (Domains.MemoryDomain.HandleAsync, "1.3");
        s_domains["Network"] = (Domains.NetworkDomain.HandleAsync, "1.3");
        s_domains["Fetch"] = (Domains.FetchDomain.HandleAsync, "1.3");
        s_domains["Sources"] = (Domains.SourcesDomain.HandleAsync, "1.3");
        s_domains["Application"] = (Domains.ApplicationDomain.HandleAsync, "1.3");
        s_domains["CSS"] = (Domains.CssDomain.HandleAsync, "1.3");
        s_domains["Input"] = (Domains.InputDomain.HandleAsync, "1.3");
        s_domains["Page"] = (Domains.PageDomain.HandleAsync, "1.3");
        s_domains["Overlay"] = (Domains.OverlayDomain.HandleAsync, "1.3");
        s_domains["Runtime"] = (Domains.RuntimeDomain.HandleAsync, "1.3");
        s_domains["Target"] = (Domains.TargetDomain.HandleAsync, "1.3");
        s_domains["Accessibility"] = (Domains.AccessibilityDomain.HandleAsync, "1.3");
        s_domains["Emulation"] = (Domains.EmulationDomain.HandleAsync, "1.3");
        s_domains["Log"] = (Domains.LogDomain.HandleAsync, "1.3");
        s_domains["Console"] = (Domains.ConsoleDomain.HandleAsync, "1.3");
        s_domains["Performance"] = (Domains.PerformanceDomain.HandleAsync, "1.3");
        s_domains["Tracing"] = (Domains.TracingDomain.HandleAsync, "1.3");
        s_domains["Browser"] = (Domains.BrowserDomain.HandleAsync, "1.3");
        s_domains["WindowChrome"] = (Domains.WindowChromeDomain.HandleAsync, "1.3");
        s_domains["SystemInfo"] = (Domains.SystemInfoDomain.HandleAsync, "1.3");
        s_domains["Schema"] = (Domains.SchemaDomain.HandleAsync, "1.3");
        s_domains["Recorder"] = (Domains.RecorderDomain.HandleAsync, "1.3");
        s_domains["Inspector"] = (Domains.InspectorDomain.HandleAsync, "1.3");
        s_domains["Media"] = (Domains.MediaDomain.HandleAsync, "1.3");
        s_domains["EventBreakpoints"] = (Domains.EventBreakpointsDomain.HandleAsync, "1.3");
        s_domains["DeviceOrientation"] = (Domains.DeviceOrientationDomain.HandleAsync, "1.3");
        s_domains["Ads"] = (Domains.AdsDomain.HandleAsync, "1.3");
        s_domains["Autofill"] = (Domains.AutofillDomain.HandleAsync, "1.3");
        s_domains["BackgroundService"] = (Domains.BackgroundServiceDomain.HandleAsync, "1.3");
        s_domains["Cast"] = (Domains.CastDomain.HandleAsync, "1.3");
        s_domains["DeviceAccess"] = (Domains.DeviceAccessDomain.HandleAsync, "1.3");
        s_domains["FileSystem"] = (Domains.FileSystemDomain.HandleAsync, "1.3");
        s_domains["CrashReportContext"] = (Domains.CrashReportContextDomain.HandleAsync, "1.3");
        s_domains["PerformanceTimeline"] = (Domains.PerformanceTimelineDomain.HandleAsync, "1.3");
        s_domains["Audits"] = (Domains.AuditsDomain.HandleAsync, "1.3");
        s_domains["DOMSnapshot"] = (Domains.DOMSnapshotDomain.HandleAsync, "1.3");
        s_domains["DOMStorage"] = (Domains.DOMStorageDomain.HandleAsync, "1.3");
        s_domains["Preload"] = (Domains.PreloadDomain.HandleAsync, "1.3");
        s_domains["WebAudio"] = (Domains.WebAudioDomain.HandleAsync, "1.3");
        s_domains["Tethering"] = (Domains.TetheringDomain.HandleAsync, "1.3");
    }

    public static void Register(string domainName, Func<CdpSession, string, JsonObject, Task<JsonObject>> handler, string version = "1.3")
    {
        if (string.IsNullOrEmpty(domainName)) throw new ArgumentException("Domain name cannot be null or empty", nameof(domainName));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        s_domains[domainName] = (handler, version);

        CdpServer.BroadcastDomainsUpdated();
    }

    public static void Unregister(string domainName)
    {
        if (string.IsNullOrEmpty(domainName)) return;

        if (s_domains.TryRemove(domainName, out _))
        {
            CdpServer.BroadcastDomainsUpdated();
        }
    }

    public static bool TryGetHandler(string domainName, out Func<CdpSession, string, JsonObject, Task<JsonObject>>? handler)
    {
        handler = null;
        if (s_domains.TryGetValue(domainName, out var entry))
        {
            handler = entry.Handler;
            return true;
        }
        return false;
    }

    public static IEnumerable<(string Name, string Version)> GetDomains()
    {
        foreach (var pair in s_domains)
        {
            yield return (pair.Key, pair.Value.Version);
        }
    }
}
