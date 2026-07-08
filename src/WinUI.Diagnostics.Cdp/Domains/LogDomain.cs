using System;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace WinUI.Diagnostics.Cdp.Domains;

public static class LogDomain
{
    private static CdpTraceListener? _listener;

    private class CdpTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                BroadcastLog(message, "info");
            }
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                BroadcastLog(message, "info");
            }
        }
    }

    public static void Initialize()
    {
        _listener = new CdpTraceListener();
        Trace.Listeners.Add(_listener);
    }

    public static void Shutdown()
    {
        if (_listener != null)
        {
            Trace.Listeners.Remove(_listener);
            _listener = null;
        }
    }

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
            case "disable":
            case "clear":
                return new JsonObject();

            default:
                throw new Exception($"Method Log.{action} is not implemented");
        }
    }

    private static void BroadcastLog(string message, string level)
    {
        var entry = new JsonObject
        {
            ["source"] = "xml",
            ["level"] = level,
            ["text"] = message,
            ["timestamp"] = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var notification = new JsonObject { ["entry"] = entry };
        foreach (var session in CdpServer.Sessions)
        {
            _ = session.SendEventAsync("Log.entryAdded", notification);
        }
    }
}
