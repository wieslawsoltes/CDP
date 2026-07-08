using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Chrome.DevTools.Protocol;

namespace CDP.Automation.OS;

public class OsAutomationDaemon
{
    private static readonly ConcurrentDictionary<string, OsAutomationCdpSession> _sessionMap = new();
    private readonly int _port;
    private bool _isRunning;

    public OsAutomationDaemon(int port = 9222)
    {
        _port = port;
    }

    public static async Task Main(string[] args)
    {
        int port = 9222;
        if (args.Length > 0 && int.TryParse(args[0], out int parsedPort))
        {
            port = parsedPort;
        }

        var daemon = new OsAutomationDaemon(port);
        daemon.Start();

        var tcs = new TaskCompletionSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            tcs.SetResult();
        };

        Console.WriteLine("Press Ctrl+C to exit.");
        await tcs.Task;

        daemon.Stop();
    }

    public void Start()
    {
        if (_isRunning) return;

        Console.WriteLine($"Starting OS Automation CDP Emulation Daemon on port {_port}...");

        // Initialize OS Automation provider
        OsAutomationProvider.Instance = OSAutomationService.Instance;

        // Setup CdpServer options to support native window discovery.
        // Expose all active desktop windows as page targets under `/json/list`.
        CdpServer.TargetProvider = () =>
        {
            try
            {
                var windows = OSAutomationService.Instance.GetWindows();
                return windows.Select(w => new OsWindowCdpTarget(w));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting windows: {ex.Message}");
                return Enumerable.Empty<ICdpTarget>();
            }
        };

        // Register domain handlers by routing them to OsAutomationCdpSession
        string[] domains = new[]
        {
            "DOM", "Input", "Page", "SystemInfo", "Runtime", 
            "Accessibility", "CSS", "Overlay", "Recorder", "Performance", 
            "Memory", "Network", "Browser"
        };

        foreach (var domain in domains)
        {
            var d = domain; // closure
            CdpDomainRegistry.Register(d, (s, a, p) => HandleDomainAsync(s, d, a, p));
        }

        // Subscribe to session removal to clean up daemon-scoped window sessions
        CdpServer.SessionRemoved += OnCdpSessionRemoved;

        // Start CdpServer on port
        CdpServer.Start(_port);
        _isRunning = true;
        Console.WriteLine($"CDP Server running on port {_port}.");
    }

    public void Stop()
    {
        if (!_isRunning) return;

        CdpServer.SessionRemoved -= OnCdpSessionRemoved;
        CdpServer.Stop();

        foreach (var osSession in _sessionMap.Values)
        {
            try
            {
                osSession.Dispose();
            }
            catch { }
        }
        _sessionMap.Clear();
        _isRunning = false;
        Console.WriteLine("CDP Server stopped.");
    }

    private static void OnCdpSessionRemoved(CdpSession session)
    {
        var prefix = $"{session.GetHashCode()}_";
        var keysToRemove = _sessionMap.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keysToRemove)
        {
            if (_sessionMap.TryRemove(key, out var osSession))
            {
                try
                {
                    osSession.Dispose();
                }
                catch { }
            }
        }
    }

    private static OsAutomationCdpSession GetOrCreateOsSession(CdpSession session)
    {
        var targetSession = session.CurrentTargetSession;
        string targetId = targetSession != null ? targetSession.TargetId : (session.Target?.Id ?? "");
        string key = $"{session.GetHashCode()}_{targetId}";

        return _sessionMap.GetOrAdd(key, _ =>
        {
            var target = (targetSession?.Target ?? session.Target) as OsWindowCdpTarget;
            if (target == null)
            {
                throw new Exception($"Session target is not OsWindowCdpTarget. TargetId: {targetId}");
            }
            var osSession = new OsAutomationCdpSession(target.Id);
            
            // Forward events (like Page.screencastFrame) back to the client session using the correlated targetSession
            osSession.EventReceived += async (s, e) =>
            {
                try
                {
                    if (session != null)
                    {
                        await session.SendEventAsync(e.Method, e.Params, targetSession);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error forwarding event {e.Method}: {ex.Message}");
                }
            };

            return osSession;
        });
    }

    private static async Task<JsonObject> HandleDomainAsync(CdpSession session, string domain, string action, JsonObject @params)
    {
        try
        {
            var osSession = GetOrCreateOsSession(session);
            var fullMethod = $"{domain}.{action}";
            return await osSession.HandleCommandAsync(fullMethod, @params);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling command {domain}.{action}: {ex.Message}");
            throw;
        }
    }
}
