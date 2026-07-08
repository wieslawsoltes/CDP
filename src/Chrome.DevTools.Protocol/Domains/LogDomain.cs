using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

public static class LogDomain
{
    private static readonly ConcurrentDictionary<CdpSession, bool> _enabledSessions = new();

    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
                _enabledSessions[session] = true;
                return Task.FromResult(new JsonObject());

            case "disable":
                _enabledSessions.TryRemove(session, out _);
                return Task.FromResult(new JsonObject());

            case "clear":
            case "startViolationsReport":
            case "stopViolationsReport":
                return Task.FromResult(new JsonObject());

            default:
                throw new Exception($"Method Log.{action} is not implemented");
        }
    }

    public static void RemoveSession(CdpSession session)
    {
        _enabledSessions.TryRemove(session, out _);
    }

    public static bool HasEnabledSessions => !_enabledSessions.IsEmpty;

    public static void BroadcastLog(string area, string level, string text)
    {
        if (_enabledSessions.IsEmpty) return;

        var entry = new JsonObject
        {
            ["source"] = "app",
            ["level"] = MapLogLevel(level),
            ["text"] = $"[{area}] {text}",
            ["timestamp"] = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var entryParams = new JsonObject { ["entry"] = entry };

        foreach (var session in _enabledSessions.Keys)
        {
            _ = session.SendEventAsync("Log.entryAdded", entryParams);
        }
    }

    private static string MapLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "verbose" => "verbose",
            "debug" => "verbose",
            "information" => "info",
            "warning" => "warning",
            "error" => "error",
            "fatal" => "error",
            _ => "info"
        };
    }
}
