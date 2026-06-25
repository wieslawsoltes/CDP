using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Chrome.DevTools.Protocol;

public class CdpTargetSession : IDisposable
{
    protected readonly CdpSession _session;

    public string SessionId { get; }
    public string TargetId { get; }
    public ICdpTarget Target { get; }
    public ConcurrentDictionary<string, object> RemoteObjects { get; } = new();
    public int InspectedNodeId { get; set; } = 0;
    public ConcurrentDictionary<string, string> ScriptsToEvaluateOnNewDocument { get; } = new();
    public ConcurrentDictionary<string, string> ScriptsToEvaluateOnLoad { get; } = new();
    public System.Collections.Generic.List<JsonObject> Cookies { get; } = new();

    public JsonObject? GeolocationOverride { get; set; }
    public JsonObject? DeviceOrientationOverride { get; set; }
    public virtual bool TouchEmulationEnabled { get; set; }
    public bool LifecycleEventsEnabled { get; set; }
    public bool AdBlockingEnabled { get; set; }
    public bool BypassCSP { get; set; }
    public JsonObject? FontFamilies { get; set; }
    public JsonObject? FontSizes { get; set; }
    public string? DownloadBehavior { get; set; }
    public string? DownloadPath { get; set; }
    public bool InterceptFileChooserDialog { get; set; }
    public bool PrerenderingAllowed { get; set; }
    public string? RPHRegistrationMode { get; set; }
    public string? SPCTransactionMode { get; set; }
    public string? WebLifecycleState { get; set; }
    public ConcurrentDictionary<string, string> CompilationCache { get; } = new();
    public System.Collections.Generic.List<JsonObject> NavigationHistory { get; } = new();
    public int NavigationHistoryIndex { get; set; } = -1;
    public object? ScriptSession { get; set; }

    public virtual bool IsDomEnabled { get; set; }
    public virtual bool InspectModeEnabled { get; set; }

    public CdpTargetSession(CdpSession session, string sessionId, string targetId, ICdpTarget target)
    {
        _session = session;
        SessionId = sessionId;
        TargetId = targetId;
        Target = target;
    }

    public virtual void RequestScreencastFrame() { }
    public virtual void StartScreencast(string format = "png", int? quality = null, int? maxWidth = null, int? maxHeight = null, int? everyNthFrame = null) { }
    public virtual void StopScreencast() { }
    public virtual void AcknowledgeScreencastFrame(int sessionId) { }

    public virtual void StartObservingVisualTree() { }
    public virtual void StopObservingVisualTree() { }

    public virtual void Dispose()
    {
        RemoteObjects.Clear();
        ScriptsToEvaluateOnNewDocument.Clear();
        ScriptsToEvaluateOnLoad.Clear();
        Cookies.Clear();
        CompilationCache.Clear();
        NavigationHistory.Clear();
    }
}
