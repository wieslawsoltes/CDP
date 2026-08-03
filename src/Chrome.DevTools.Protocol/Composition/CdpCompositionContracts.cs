namespace Chrome.DevTools.Protocol;

/// <summary>Framework-neutral mutation operations used by managed CDP domains.</summary>
public interface ICdpMutationEngine
{
    bool CanMutate(object target);
    Task<bool> SetAttributeAsync(object target, string name, string value);
    Task<bool> RemoveAttributeAsync(object target, string name);
    Task<bool> RemoveNodeAsync(object target);
    Task<bool> SetOuterHtmlAsync(object target, string outerHtml);
    Task<string?> GetOuterHtmlAsync(object target);
}

/// <summary>Unwraps optional runtime-specific values without introducing a runtime dependency.</summary>
public interface ICdpRemoteObjectAdapter
{
    bool TryUnwrap(object value, out object? unwrapped);
}

public static class CdpRemoteObjectAdapters
{
    private static readonly object Sync = new();
    private static readonly List<ICdpRemoteObjectAdapter> Adapters = new();

    public static void Register(ICdpRemoteObjectAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        lock (Sync)
        {
            if (!Adapters.Contains(adapter))
            {
                Adapters.Add(adapter);
            }
        }
    }

    public static bool Unregister(ICdpRemoteObjectAdapter adapter)
    {
        lock (Sync)
        {
            return Adapters.Remove(adapter);
        }
    }

    public static object? Unwrap(object value)
    {
        ICdpRemoteObjectAdapter[] snapshot;
        lock (Sync)
        {
            snapshot = Adapters.ToArray();
        }

        foreach (var adapter in snapshot)
        {
            if (adapter.TryUnwrap(value, out var unwrapped))
            {
                return unwrapped;
            }
        }

        return value;
    }
}

public static class CdpSessionCleanupRegistry
{
    private static readonly object Sync = new();
    private static readonly List<Action<CdpSession>> Handlers = new();

    public static void Register(Action<CdpSession> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (Sync)
        {
            if (!Handlers.Contains(handler))
            {
                Handlers.Add(handler);
            }
        }
    }

    internal static void Cleanup(CdpSession session)
    {
        Action<CdpSession>[] snapshot;
        lock (Sync)
        {
            snapshot = Handlers.ToArray();
        }

        foreach (var handler in snapshot)
        {
            handler(session);
        }
    }
}
