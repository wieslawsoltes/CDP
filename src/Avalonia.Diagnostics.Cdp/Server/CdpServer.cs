using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Chrome.DevTools.Protocol;

namespace Avalonia.Diagnostics.Cdp;

public class AvaloniaCdpTarget : ICdpTarget
{
    public TopLevel Window { get; }
    public string Id { get; }
    public string Title { get; set; }
    public string Type => "page";
    public string Url => $"http://localhost:{Chrome.DevTools.Protocol.CdpServer.Port}/";

    public AvaloniaCdpTarget(TopLevel window, string id, string title)
    {
        Window = window;
        Id = id;
        Title = title;
    }

    public void Activate()
    {
        if (Window is Window win)
        {
            Dispatcher.UIThread.Post(() => win.Activate());
        }
    }

    public void Close()
    {
        if (Window is Window win)
        {
            Dispatcher.UIThread.Post(() => win.Close());
        }
    }
}

public static class CdpServer
{
    private static readonly ConcurrentDictionary<TopLevel, AvaloniaCdpTarget> _targets = new();

    public static int Port => Chrome.DevTools.Protocol.CdpServer.Port;
    public static System.IO.TextWriter OriginalOut => Chrome.DevTools.Protocol.CdpServer.OriginalOut;
    public static System.IO.TextWriter OriginalError => Chrome.DevTools.Protocol.CdpServer.OriginalError;

    static CdpServer()
    {
        // Set up delegates on the core server
        Chrome.DevTools.Protocol.CdpServer.UIThreadInvoker = async (action) =>
        {
            if (Dispatcher.UIThread.CheckAccess()) return await action();
            return await Dispatcher.UIThread.InvokeAsync(action);
        };

        Chrome.DevTools.Protocol.CdpServer.TargetProvider = GetAvaloniaTargets;
        Chrome.DevTools.Protocol.CdpServer.TargetFactory = CreateAvaloniaTarget;
        Chrome.DevTools.Protocol.CdpServer.SessionFactory = (webSocket, target) =>
        {
            var avTarget = target as AvaloniaCdpTarget;
            return new CdpSession(webSocket, avTarget?.Window);
        };
        Chrome.DevTools.Protocol.CdpServer.TargetSessionFactory = (session, sessionId, targetId, target) =>
        {
            var avTarget = target as AvaloniaCdpTarget;
            return new CdpTargetSession((CdpSession)session, sessionId, targetId, avTarget?.Window);
        };

        // Subscribe to server start/stop to handle logging/sinks
        Chrome.DevTools.Protocol.CdpServer.ServerStarted += OnServerStarted;
        Chrome.DevTools.Protocol.CdpServer.ServerStopped += OnServerStopped;

        // Register Avalonia-specific domains
        CdpDomainRegistry.Register("DOM", (s, a, p) => Domains.DomDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("DOMDebugger", (s, a, p) => Domains.DomDebuggerDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Memory", (s, a, p) => Domains.MemoryDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("CSS", (s, a, p) => Domains.CssDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Input", (s, a, p) => Domains.InputDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Page", (s, a, p) => Domains.PageDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Overlay", (s, a, p) => Domains.OverlayDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Runtime", (s, a, p) => Domains.RuntimeDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Accessibility", (s, a, p) => Domains.AccessibilityDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Emulation", (s, a, p) => Domains.EmulationDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Performance", (s, a, p) => Domains.PerformanceDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Browser", (s, a, p) => Domains.BrowserDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("WindowChrome", (s, a, p) => Domains.WindowChromeDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Recorder", (s, a, p) => Domains.RecorderDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Audits", (s, a, p) => Domains.AuditsDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Application", (s, a, p) => Domains.ApplicationDomain.HandleAsync((CdpSession)s, a, p));

        Window.WindowOpenedEvent.AddClassHandler<Window>((w, e) =>
        {
            Register(w, w.Title ?? w.GetType().Name);
        });
        Window.WindowClosedEvent.AddClassHandler<Window>((w, e) =>
        {
            Unregister(w);
        });
    }

    private static void OnServerStarted()
    {
        // Initialize Memory diagnostics
        Avalonia.Diagnostics.Cdp.Domains.MemoryDomain.Initialize();

        // Integrate CDP Log domain hook
        var originalSink = Avalonia.Logging.Logger.Sink;
        var cdpSink = new Avalonia.Diagnostics.Cdp.Domains.CdpLogSink();
        Avalonia.Logging.Logger.Sink = new Avalonia.Diagnostics.Cdp.Domains.CompositeLogSink(originalSink, cdpSink);
    }

    private static void OnServerStopped()
    {
        Avalonia.Diagnostics.Cdp.Domains.MemoryDomain.Shutdown();

        // Restore original log sink
        if (Avalonia.Logging.Logger.Sink is Avalonia.Diagnostics.Cdp.Domains.CompositeLogSink composite)
        {
            Avalonia.Logging.Logger.Sink = composite.OriginalSink;
        }
    }

    public static string Register(TopLevel window, string title)
    {
        var target = _targets.GetOrAdd(window, w => new AvaloniaCdpTarget(w, Guid.NewGuid().ToString(), title));
        return Chrome.DevTools.Protocol.CdpServer.Register(target);
    }

    public static void Unregister(TopLevel window)
    {
        if (_targets.TryRemove(window, out var target))
        {
            Chrome.DevTools.Protocol.CdpServer.Unregister(target);
        }
    }

    public static AvaloniaCdpTarget GetOrCreateTarget(TopLevel window)
    {
        _ = Port;
        return _targets.GetOrAdd(window, w =>
        {
            var target = new AvaloniaCdpTarget(w, Guid.NewGuid().ToString(), (w as Window)?.Title ?? w.GetType().Name);
            Chrome.DevTools.Protocol.CdpServer.Register(target);
            return target;
        });
    }

    public static AvaloniaCdpTarget GetOrCreateTarget(TopLevel window, string targetId)
    {
        _ = Port;
        return _targets.GetOrAdd(window, w =>
        {
            var target = new AvaloniaCdpTarget(w, targetId, (w as Window)?.Title ?? w.GetType().Name);
            Chrome.DevTools.Protocol.CdpServer.Register(target);
            return target;
        });
    }

    public static void UpdateTitle(TopLevel window, string newTitle)
    {
        if (_targets.TryGetValue(window, out var target))
        {
            target.Title = newTitle;
            Chrome.DevTools.Protocol.CdpServer.UpdateTitle(target, newTitle);
        }
    }

    private static IEnumerable<ICdpTarget> GetAvaloniaTargets()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.Invoke(GetAvaloniaTargets);
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var win in desktop.Windows)
            {
                if (!_targets.ContainsKey(win))
                {
                    var title = win.Title ?? "Avalonia Window";
                    var target = new AvaloniaCdpTarget(win, Guid.NewGuid().ToString(), title);
                    _targets[win] = target;
                }
            }
        }

        return _targets.Values;
    }

    private static async Task<ICdpTarget> CreateAvaloniaTarget(string url, string? title)
    {
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var w = new Window
            {
                Title = title ?? "Dynamic CDP Window",
                Width = 400,
                Height = 300
            };
            w.Show();
            var target = GetOrCreateTarget(w);
            return (ICdpTarget)target;
        });
    }

    public static void EnsureInitialized()
    {
        // No-op to trigger static constructor and register domains
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

    public static void AddSession(CdpSession session)
    {
        Chrome.DevTools.Protocol.CdpServer.AddSession(session);
    }

    public static void RemoveSession(CdpSession session)
    {
        Chrome.DevTools.Protocol.CdpServer.RemoveSession(session);
    }
    
    public static IEnumerable<(string Id, TopLevel Window, string Title)> GetWindows()
    {
        return _targets.Select(x => (x.Value.Id, x.Key, x.Value.Title));
    }

    public static JsonArray GetActiveTargets()
    {
        return Chrome.DevTools.Protocol.CdpServer.GetActiveTargets();
    }
}
