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
    public static async Task Main(string[] args)
    {
        int port = 9224;
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

        Console.WriteLine("Proxy running. Press Ctrl+C to stop.");
        await tcs.Task;

        daemon.Stop();
        Console.WriteLine("Proxy stopped.");
    }
}
