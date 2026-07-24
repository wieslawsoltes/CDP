using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Chrome.DevTools.Protocol;

namespace Wpf.Diagnostics.Cdp;

public class WpfCdpTarget : ICdpTarget
{
    public Window Window { get; }
    public string Id { get; }
    public string Title { get; set; }
    public string Type => "page";
    public string Url => $"http://localhost:{Chrome.DevTools.Protocol.CdpServer.Port}/";

    public WpfCdpTarget(Window window, string id, string title)
    {
        Window = window;
        Id = id;
        Title = title;
    }

    public void Activate()
    {
        Window.Dispatcher.BeginInvoke(() =>
        {
            if (Window.WindowState == WindowState.Minimized)
            {
                Window.WindowState = WindowState.Normal;
            }
            Window.Activate();
            Window.Focus();
        });
    }

    public void Close()
    {
        Window.Dispatcher.BeginInvoke(() => Window.Close());
    }
}

public static class CdpServer
{
    private static readonly ConcurrentDictionary<Window, WpfCdpTarget> _targets = new();
    private static Window? _primaryWindow;

    public static Window? GetPrimaryWindow()
    {
        if (System.Windows.Application.Current != null)
        {
            var mainWin = System.Windows.Application.Current.MainWindow;
            if (mainWin != null) return mainWin;
        }
        if (_primaryWindow != null && _targets.ContainsKey(_primaryWindow))
        {
            return _primaryWindow;
        }
        return _targets.Keys.FirstOrDefault();
    }

    public static int Port => Chrome.DevTools.Protocol.CdpServer.Port;
    public static System.IO.TextWriter OriginalOut => Chrome.DevTools.Protocol.CdpServer.OriginalOut;
    public static System.IO.TextWriter OriginalError => Chrome.DevTools.Protocol.CdpServer.OriginalError;

    public static bool WaitForDebugger
    {
        get => Chrome.DevTools.Protocol.CdpServer.WaitForDebugger;
        set => Chrome.DevTools.Protocol.CdpServer.WaitForDebugger = value;
    }

    private static readonly ConcurrentDictionary<string, DispatcherFrame> _pausedFrames = new();

    public static bool ShouldWaitForDebugger(Window window)
    {
        if (Chrome.DevTools.Protocol.CdpServer.WaitForDebugger && !Chrome.DevTools.Protocol.CdpServer.HasWaitedForDebugger)
        {
            return true;
        }

        if (_targets.TryGetValue(window, out var target))
        {
            if (Chrome.DevTools.Protocol.CdpServer.IsTargetWaitingForDebugger(target.Id))
            {
                return true;
            }
        }

        return false;
    }

    public static void ResumeTarget(string targetId)
    {
        Chrome.DevTools.Protocol.CdpServer.SetTargetWaitingForDebugger(targetId, false);
        Chrome.DevTools.Protocol.CdpServer.HasWaitedForDebugger = true;

        if (_pausedFrames.TryRemove(targetId, out var frame))
        {
            frame.Continue = false;
        }
    }

    static CdpServer()
    {
        // Set up delegates on the core server
        Chrome.DevTools.Protocol.CdpServer.UIThreadInvoker = async (action) =>
        {
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess()) return await action();
            return await dispatcher.InvokeAsync(action).Task;
        };

        Chrome.DevTools.Protocol.CdpServer.TargetProvider = GetWpfTargets;
        Chrome.DevTools.Protocol.CdpServer.TargetFactory = CreateWpfTarget;
        Chrome.DevTools.Protocol.CdpServer.SessionFactory = (webSocket, target) =>
        {
            var wpfTarget = target as WpfCdpTarget;
            return new CdpSession(webSocket, wpfTarget?.Window);
        };
        Chrome.DevTools.Protocol.CdpServer.TargetSessionFactory = (session, sessionId, targetId, target) =>
        {
            if (target is WpfCdpTarget wpfTarget)
            {
                return new CdpTargetSession((CdpSession)session, sessionId, targetId, wpfTarget.Window);
            }
            return new Chrome.DevTools.Protocol.CdpTargetSession((CdpSession)session, sessionId, targetId, target);
        };

        // Subscribe to server start/stop to handle logging/sinks
        Chrome.DevTools.Protocol.CdpServer.ServerStarted += OnServerStarted;
        Chrome.DevTools.Protocol.CdpServer.ServerStopped += OnServerStopped;

        // Register WPF-specific domains
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
        CdpDomainRegistry.Register("XamlLsp", (s, a, p) => Domains.XamlLspDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Application", (s, a, p) => Domains.ApplicationDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Audits", (s, a, p) => Domains.AuditsDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Browser", (s, a, p) => Domains.BrowserDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Debugger", (s, a, p) => Domains.DebuggerDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("DOMDebugger", (s, a, p) => Domains.DomDebuggerDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("Emulation", (s, a, p) => Domains.EmulationDomain.HandleAsync((CdpSession)s, a, p));
        CdpDomainRegistry.Register("WebMCP", (s, a, p) => Domains.WebMcpDomain.HandleAsync((CdpSession)s, a, p));

        // Use EventManager to hook loaded events on all Window controls globally
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window w)
        {
            Console.WriteLine($"[CDP SERVER DEBUG] Window Loaded for: {w.GetType().FullName}, Title: '{w.Title}'");
            var targetId = Register(w, w.Title ?? w.GetType().Name);

            w.Closed += (s, ev) =>
            {
                Console.WriteLine($"[CDP SERVER DEBUG] Window Closed for: {w.GetType().FullName}");
                Unregister(w);
            };

            if (ShouldWaitForDebugger(w))
            {
                Chrome.DevTools.Protocol.CdpServer.SetTargetWaitingForDebugger(targetId, true);
                var frame = new DispatcherFrame();
                _pausedFrames[targetId] = frame;
                Dispatcher.PushFrame(frame);
            }
        }
    }

    private static void OnServerStarted()
    {
        Domains.MemoryDomain.Initialize();
        // Hook logging if appropriate (using Trace or Console redirection)
        Domains.LogDomain.Initialize();
    }

    private static void OnServerStopped()
    {
        Domains.MemoryDomain.Shutdown();
        Domains.LogDomain.Shutdown();
        _targets.Clear();

        foreach (var frame in _pausedFrames.Values)
        {
            frame.Continue = false;
        }
        _pausedFrames.Clear();
    }

    public static string Register(Window window, string title)
    {
        if (_primaryWindow == null)
        {
            _primaryWindow = window;
        }
        var target = _targets.GetOrAdd(window, w => new WpfCdpTarget(w, Guid.NewGuid().ToString(), title));
        return Chrome.DevTools.Protocol.CdpServer.Register(target);
    }

    public static void Unregister(Window window)
    {
        if (_targets.TryRemove(window, out var target))
        {
            Chrome.DevTools.Protocol.CdpServer.Unregister(target);
            if (_primaryWindow == window)
            {
                _primaryWindow = _targets.Keys.FirstOrDefault();
            }
        }
    }

    public static WpfCdpTarget GetOrCreateTarget(Window window)
    {
        _ = Port;
        if (_primaryWindow == null)
        {
            _primaryWindow = window;
        }
        return _targets.GetOrAdd(window, w =>
        {
            var target = new WpfCdpTarget(w, Guid.NewGuid().ToString(), w.Title ?? w.GetType().Name);
            Chrome.DevTools.Protocol.CdpServer.Register(target);
            return target;
        });
    }

    public static WpfCdpTarget GetOrCreateTarget(Window window, string targetId)
    {
        _ = Port;
        if (_primaryWindow == null)
        {
            _primaryWindow = window;
        }
        return _targets.GetOrAdd(window, w =>
        {
            var target = new WpfCdpTarget(w, targetId, w.Title ?? w.GetType().Name);
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

    private static IEnumerable<ICdpTarget> GetWpfTargets()
    {
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if (!dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(GetWpfTargets);
        }

        if (Application.Current != null)
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win.IsVisible && !_targets.ContainsKey(win))
                {
                    var title = win.Title ?? "WPF Window";
                    var target = new WpfCdpTarget(win, Guid.NewGuid().ToString(), title);
                    _targets[win] = target;
                }
            }
        }

        return _targets.Values;
    }

    private static async Task<ICdpTarget> CreateWpfTarget(string url, string? title)
    {
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if (dispatcher.CheckAccess())
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
        }
        return await dispatcher.InvokeAsync(() =>
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
        }).Task;
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

    public static IEnumerable<(string Id, Window Window, string Title)> GetWindows()
    {
        return _targets.Select(x => (x.Value.Id, x.Key, x.Value.Title));
    }

    public static JsonArray GetActiveTargets()
    {
        return Chrome.DevTools.Protocol.CdpServer.GetActiveTargets();
    }
}
