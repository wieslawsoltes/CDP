using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Chrome.DevTools.Protocol;

namespace CDP.Automation.OS;

public class OsWindowCdpTarget : ICdpTarget
{
    private readonly OSWindow _window;
    public string Id => _window.Id;
    public string Title => $"{_window.Title} ({_window.ProcessName})";
    public string Type => "page";
    public string Url => $"http://localhost:9224/window/{_window.Id}";

    public OsWindowCdpTarget(OSWindow window)
    {
        _window = window;
    }

    public void Activate()
    {
        try
        {
            OSAutomationService.Instance.BringToFront(_window.Id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error bringing window to front: {ex.Message}");
        }
    }

    public void Close()
    {
    }
}

public static class ProxyProgram
{
    private static readonly ConcurrentDictionary<string, OsAutomationCdpSession> _sessionMap = new();

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting OS Automation CDP Emulation Proxy on port 9224...");

        // Initialize OS Automation provider
        OsAutomationProvider.Instance = OSAutomationService.Instance;

        // Setup CdpServer options
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

        // Start CdpServer on port 9224
        CdpServer.Start(9224);
        Console.WriteLine("CdpServer running on port 9224. Press Ctrl+C to stop.");

        // Keep the program running
        var tcs = new TaskCompletionSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            tcs.SetResult();
        };
        await tcs.Task;

        CdpServer.Stop();
        Console.WriteLine("Proxy stopped.");
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
            return new OsAutomationCdpSession(target.Id);
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
