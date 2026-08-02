using Chrome.DevTools.Protocol.Domains;

namespace Chrome.DevTools.Protocol;

public static class CdpProfilingComposition
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0) return;
        CdpDomainRegistry.Register("Profiler", ProfilerDomain.HandleAsync);
        CdpSessionCleanupRegistry.Register(ProfilerDomain.CleanupSession);
    }
}
