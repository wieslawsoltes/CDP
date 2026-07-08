using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Chrome.DevTools.Protocol;

namespace WinUI.Diagnostics.Cdp;

public class WinUiCdpTarget : ICdpTarget
{
    public Window Window { get; }
    public string Id { get; }
    public string Title { get; set; }
    public string Type => "page";
    public string Url => $"http://localhost:{Chrome.DevTools.Protocol.CdpServer.Port}/";

    public WinUiCdpTarget(Window window, string id, string title)
    {
        Window = window;
        Id = id;
        Title = title;
    }

    public void Activate()
    {
        Window.DispatcherQueue.TryEnqueue(() =>
        {
            Window.Activate();
        });
    }

    public void Close()
    {
        Window.DispatcherQueue.TryEnqueue(() =>
        {
            Window.Close();
        });
    }
}

public static class DispatcherQueueExtensions
{
    public static Task<T> InvokeAsync<T>(this DispatcherQueue dispatcher, Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>();
        bool enqueued = dispatcher.TryEnqueue(() =>
        {
            try
            {
                tcs.SetResult(action());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        if (!enqueued)
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue action on DispatcherQueue"));
        }
        return tcs.Task;
    }

    public static Task InvokeAsync(this DispatcherQueue dispatcher, Action action)
    {
        var tcs = new TaskCompletionSource();
        bool enqueued = dispatcher.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        if (!enqueued)
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue action on DispatcherQueue"));
        }
        return tcs.Task;
    }
}

public static class CdpServer
{
    private static readonly ConcurrentDictionary<Window, WinUiCdpTarget> _targets = new();

    public static int Port => Chrome.DevTools.Protocol.CdpServer.Port;
    public static System.IO.TextWriter OriginalOut => Chrome.DevTools.Protocol.CdpServer.OriginalOut;
    public static System.IO.TextWriter OriginalError => Chrome.DevTools.Protocol.CdpServer.OriginalError;

    public static bool WaitForDebugger
    {
        get => Chrome.DevTools.Protocol.CdpServer.WaitForDebugger;
        set => Chrome.DevTools.Protocol.CdpServer.WaitForDebugger = value;
    }

    static CdpServer()
    {
        // Set up delegates on the core server
        Chrome.DevTools.Protocol.CdpServer.UIThreadInvoker = async (action) =>
        {
            // If we have an active target window, use its DispatcherQueue
            var target = _targets.Keys.FirstOrDefault();
            if (target != null)
            {
                if (target.DispatcherQueue.HasThreadAccess) return await action();
                return await target.DispatcherQueue.InvokeAsync(action).Unwrap();
            }
            return await action();
        };

        Chrome.DevTools.Protocol.CdpServer.TargetProvider = GetWinUiTargets;
        Chrome.DevTools.Protocol.CdpServer.TargetFactory = CreateWinUiTarget;
        Chrome.DevTools.Protocol.CdpServer.SessionFactory = (webSocket, target) =>
        {
            var winUiTarget = target as WinUiCdpTarget;
            return new CdpSession(webSocket, winUiTarget?.Window);
        };
        Chrome.DevTools.Protocol.CdpServer.TargetSessionFactory = (session, sessionId, targetId, target) =>
        {
            if (target is WinUiCdpTarget winUiTarget)
            {
                return new CdpTargetSession((CdpSession)session, sessionId, targetId, winUiTarget.Window);
            }
            return new Chrome.DevTools.Protocol.CdpTargetSession((CdpSession)session, sessionId, targetId, target);
        };

        Chrome.DevTools.Protocol.CdpServer.ServerStarted += OnServerStarted;
        Chrome.DevTools.Protocol.CdpServer.ServerStopped += OnServerStopped;

        // Register WinUI/Uno-specific domains
        CdpDomainRegistry.Register("DOM", (s, a, p) => Domains.DomDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Memory", (s, a, p) => Domains.MemoryDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("CSS", (s, a, p) => Domains.CssDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Input", (s, a, p) => Domains.InputDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Page", (s, a, p) => Domains.PageDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Overlay", (s, a, p) => Domains.OverlayDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Runtime", (s, a, p) => Domains.RuntimeDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Accessibility", (s, a, p) => Domains.AccessibilityDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Performance", (s, a, p) => Domains.PerformanceDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("WindowChrome", (s, a, p) => Domains.WindowChromeDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Recorder", (s, a, p) => Domains.RecorderDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Mvvm", (s, a, p) => Domains.MvvmDomain.HandleAsync((CdpSession)s, a, p));
    }

    private static void OnServerStarted()
    {
        Domains.MemoryDomain.Initialize();
        Domains.LogDomain.Initialize();
    }

    private static void OnServerStopped()
    {
        Domains.MemoryDomain.Shutdown();
        Domains.LogDomain.Shutdown();
        _targets.Clear();
    }

    public static string Register(Window window, string title)
    {
        var target = _targets.GetOrAdd(window, w =>
        {
            var t = new WinUiCdpTarget(w, Guid.NewGuid().ToString(), title);
            w.Closed += (sender, args) =>
            {
                Unregister(w);
            };
            return t;
        });
        return Chrome.DevTools.Protocol.CdpServer.Register(target);
    }

    public static void Unregister(Window window)
    {
        if (_targets.TryRemove(window, out var target))
        {
            Chrome.DevTools.Protocol.CdpServer.Unregister(target);
        }
    }

    public static WinUiCdpTarget GetOrCreateTarget(Window window)
    {
        return _targets.GetOrAdd(window, w =>
        {
            var target = new WinUiCdpTarget(w, Guid.NewGuid().ToString(), "WinUI/Uno Window");
            w.Closed += (sender, args) => Unregister(w);
            Chrome.DevTools.Protocol.CdpServer.Register(target);
            return target;
        });
    }

    public static void UpdateTitle(Window window, string newTitle)
    {
        if (_targets.TryGetValue(window, out var target))
        {
            target.Title = newTitle;
            Chrome.DevTools.Protocol.CdpServer.UpdateTitle(target, newTitle);
        }
    }

    private static IEnumerable<ICdpTarget> GetWinUiTargets()
    {
        return _targets.Values;
    }

    private static Task<ICdpTarget> CreateWinUiTarget(string url, string? title)
    {
        throw new NotSupportedException("Creating dynamic target windows is not supported on WinUI/Uno. Please register windows using CdpServer.Register.");
    }

    public static void EnsureInitialized()
    {
        // No-op to trigger static constructor
    }

    public static void Start(int port = 9222)
    {
        Chrome.DevTools.Protocol.CdpServer.Start(port);
    }

    public static void Stop()
    {
        Chrome.DevTools.Protocol.CdpServer.Stop();
    }

    public static IEnumerable<CdpSession> Sessions => Chrome.DevTools.Protocol.CdpServer.Sessions.Cast<CdpSession>();

    public static IEnumerable<(string Id, Window Window, string Title)> GetWindows()
    {
        return _targets.Select(x => (x.Value.Id, x.Key, x.Value.Title));
    }

    public static JsonArray GetActiveTargets()
    {
        return Chrome.DevTools.Protocol.CdpServer.GetActiveTargets();
    }
}
