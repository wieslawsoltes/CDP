using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Chrome.DevTools.Protocol.Domains;

public static class BackgroundServiceDomain
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger("BackgroundServiceDomain");
    private static readonly ConcurrentDictionary<CdpSession, CancellationTokenSource> _observingSessions = new();

    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "clearEvents":
                return Task.FromResult(new JsonObject());

            case "setRecording":
                return Task.FromResult(new JsonObject());

            case "startObserving":
                {
                    string service = @params["service"]?.GetValue<string>() ?? "notifications";
                    StartObserving(session, service);
                    return Task.FromResult(new JsonObject());
                }

            case "stopObserving":
                {
                    StopObserving(session);
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method BackgroundService.{action} is not implemented");
        }
    }

    public static void RemoveSession(CdpSession session)
    {
        StopObserving(session);
    }

    private static void StartObserving(CdpSession session, string service)
    {
        StopObserving(session);

        var cts = new CancellationTokenSource();
        _observingSessions[session] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                int eventCount = 1;
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(5000, cts.Token);

                    var metadata = new JsonArray();
                    string eventName = "";
                    switch (service)
                    {
                        case "notifications":
                            eventName = "notificationclick";
                            metadata.Add(new JsonObject { ["key"] = "Title", ["value"] = $"Mock Notification {eventCount}" });
                            metadata.Add(new JsonObject { ["key"] = "Body", ["value"] = "This is a simulated notification message." });
                            break;
                        case "pushMessaging":
                            eventName = "push";
                            metadata.Add(new JsonObject { ["key"] = "Payload", ["value"] = $"{{\"data\": \"Push Message {eventCount}\"}}" });
                            break;
                        case "backgroundFetch":
                            eventName = "backgroundfetchsuccess";
                            metadata.Add(new JsonObject { ["key"] = "Id", ["value"] = $"fetch-id-{eventCount}" });
                            break;
                        case "backgroundSync":
                            eventName = "sync";
                            metadata.Add(new JsonObject { ["key"] = "Tag", ["value"] = $"sync-tag-{eventCount}" });
                            break;
                        case "periodicBackgroundSync":
                            eventName = "periodicSync";
                            metadata.Add(new JsonObject { ["key"] = "Tag", ["value"] = $"periodic-tag-{eventCount}" });
                            break;
                        default:
                            eventName = "event";
                            metadata.Add(new JsonObject { ["key"] = "Info", ["value"] = $"Mock data {eventCount}" });
                            break;
                    }

                    var bgEvent = new JsonObject
                    {
                        ["timestamp"] = (double)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        ["origin"] = "http://localhost:9222",
                        ["serviceWorkerRegistrationId"] = "1",
                        ["service"] = service,
                        ["eventName"] = eventName,
                        ["instanceId"] = $"inst-{Guid.NewGuid().ToString()[..8]}",
                        ["eventMetadata"] = metadata
                    };

                    var eventParams = new JsonObject
                    {
                        ["backgroundServiceEvent"] = bgEvent
                    };

                    await session.SendEventAsync("BackgroundService.backgroundServiceEventReceived", eventParams);
                    eventCount++;
                }
            }
            catch (OperationCanceledException)
            {
                // Task was canceled
            }
            catch (Exception ex)
            {
                Logger.LogErrorMessage("BackgroundServiceDomain", "Error in simulated BackgroundService event task", ex);
            }
        }, cts.Token);
    }

    private static void StopObserving(CdpSession session)
    {
        if (_observingSessions.TryRemove(session, out var cts))
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch { }
        }
    }
}
