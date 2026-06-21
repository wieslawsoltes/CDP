using System;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class TracingDomain
{
    private static readonly ConcurrentDictionary<CdpSession, ActivityListener> _listeners = new();

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "start":
                {
                    StartTracing(session);
                    return new JsonObject();
                }
            case "end":
                {
                    await EndTracingAsync(session);
                    return new JsonObject();
                }
            default:
                throw new Exception($"Method Tracing.{action} is not implemented");
        }
    }

    public static bool IsTracing(CdpSession session)
    {
        return _listeners.ContainsKey(session);
    }

    public static JsonObject CreateManualTraceEvent(string cat, string name, double durationMs)
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
        long dur = (long)(durationMs * 1000);

        return new JsonObject
        {
            ["cat"] = cat,
            ["pid"] = Process.GetCurrentProcess().Id,
            ["tid"] = Environment.CurrentManagedThreadId,
            ["ts"] = ts,
            ["ph"] = "X", // Complete event
            ["name"] = name,
            ["dur"] = dur,
            ["args"] = new JsonObject()
        };
    }

    private static void StartTracing(CdpSession session)
    {
        if (_listeners.ContainsKey(session)) return;

        var listener = new ActivityListener
        {
            ShouldListenTo = source => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
            },
            ActivityStopped = activity =>
            {
                var traceEvent = CreateTraceEvent(activity);
                _ = session.SendEventAsync("Tracing.dataCollected", new JsonObject
                {
                    ["value"] = new JsonArray { traceEvent }
                });
            }
        };

        ActivitySource.AddActivityListener(listener);
        _listeners[session] = listener;
    }

    private static async Task EndTracingAsync(CdpSession session)
    {
        if (_listeners.TryRemove(session, out var listener))
        {
            listener.Dispose();
        }

        await session.SendEventAsync("Tracing.tracingComplete", new JsonObject());
    }

    private static JsonObject CreateTraceEvent(Activity activity)
    {
        long ts = activity.StartTimeUtc.Ticks / (TimeSpan.TicksPerMillisecond / 1000);
        long dur = activity.Duration.Ticks / (TimeSpan.TicksPerMillisecond / 1000);

        var args = new JsonObject();
        foreach (var tag in activity.Tags)
        {
            args[tag.Key] = tag.Value ?? "";
        }

        return new JsonObject
        {
            ["cat"] = activity.Source.Name,
            ["pid"] = Process.GetCurrentProcess().Id,
            ["tid"] = Environment.CurrentManagedThreadId,
            ["ts"] = ts,
            ["ph"] = "X", // Complete event
            ["name"] = activity.OperationName,
            ["dur"] = dur,
            ["args"] = args
        };
    }

    public static void CleanupSession(CdpSession session)
    {
        if (_listeners.TryRemove(session, out var listener))
        {
            listener.Dispose();
        }
    }
}
