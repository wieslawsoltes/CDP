using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Logging;

namespace Avalonia.Diagnostics.Cdp.Domains;

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

public class CdpLogSink : ILogSink
{
    public bool IsEnabled(LogEventLevel level, string area) => true;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        LogDomain.BroadcastLog(area, level.ToString(), messageTemplate);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        try
        {
            string formatted = string.Format(messageTemplate, propertyValues);
            LogDomain.BroadcastLog(area, level.ToString(), formatted);
        }
        catch
        {
            LogDomain.BroadcastLog(area, level.ToString(), messageTemplate);
        }
    }
}

public class CompositeLogSink : ILogSink
{
    public ILogSink? OriginalSink { get; }
    private readonly ILogSink _newSink;

    public CompositeLogSink(ILogSink? original, ILogSink newSink)
    {
        OriginalSink = original;
        _newSink = newSink;
    }

    public bool IsEnabled(LogEventLevel level, string area)
    {
        return (OriginalSink?.IsEnabled(level, area) ?? false) || _newSink.IsEnabled(level, area);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        OriginalSink?.Log(level, area, source, messageTemplate);
        _newSink.Log(level, area, source, messageTemplate);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        OriginalSink?.Log(level, area, source, messageTemplate, propertyValues);
        _newSink.Log(level, area, source, messageTemplate, propertyValues);
    }
}
