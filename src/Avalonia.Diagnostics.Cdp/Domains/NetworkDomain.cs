using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class NetworkDomain
{
    private static readonly ConcurrentDictionary<CdpSession, bool> _enabledSessions = new();
    private static readonly ConcurrentDictionary<string, string> _responseBodies = new();
    private static readonly ConcurrentDictionary<HttpRequestMessage, string> _requestIds = new();
    private static int _nextRequestId = 0;
    private static IDisposable? _diagnosticSubscription;

    public static void Initialize()
    {
        try
        {
            _diagnosticSubscription = DiagnosticListener.AllListeners.Subscribe(new NetworkDiagnosticObserver());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CDP NetworkDomain init failed: {ex.Message}");
        }
    }

    public static void Shutdown()
    {
        _diagnosticSubscription?.Dispose();
        _diagnosticSubscription = null;
        _enabledSessions.Clear();
        _responseBodies.Clear();
        _requestIds.Clear();
    }

    public static void RemoveSession(CdpSession session)
    {
        _enabledSessions.TryRemove(session, out _);
    }

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

            case "getResponseBody":
                {
                    string requestId = @params["requestId"]?.GetValue<string>() ?? "";
                    if (_responseBodies.TryGetValue(requestId, out var body))
                    {
                        return Task.FromResult(new JsonObject
                        {
                            ["body"] = body,
                            ["base64Encoded"] = false
                        });
                    }
                    throw new Exception($"Response body for request ID {requestId} not found in cache.");
                }

            default:
                throw new Exception($"Method Network.{action} is not implemented");
        }
    }

    public static void OnRequestStart(HttpRequestMessage request)
    {
        if (_enabledSessions.IsEmpty) return;

        string requestId = $"req-{System.Threading.Interlocked.Increment(ref _nextRequestId)}";
        _requestIds[request] = requestId;

        var timestamp = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        var wallTime = (double)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var headersObj = MapHeaders(request);
        var requestObj = new JsonObject
        {
            ["url"] = request.RequestUri?.ToString() ?? "",
            ["method"] = request.Method.Method,
            ["headers"] = headersObj,
            ["initialPriority"] = "Medium",
            ["referrerPolicy"] = "no-referrer-when-downgrade"
        };

        var entryParams = new JsonObject
        {
            ["requestId"] = requestId,
            ["loaderId"] = "loader-1",
            ["documentURL"] = "http://localhost:9222/",
            ["request"] = requestObj,
            ["timestamp"] = timestamp,
            ["wallTime"] = wallTime,
            ["initiator"] = new JsonObject { ["type"] = "other" },
            ["type"] = "XHR"
        };

        BroadcastEvent("Network.requestWillBeSent", entryParams);
    }

    public static void OnRequestStop(HttpRequestMessage request, HttpResponseMessage? response)
    {
        if (!_requestIds.TryRemove(request, out string? requestId)) return;
        if (_enabledSessions.IsEmpty) return;

        var timestamp = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        if (response != null)
        {
            var headersObj = MapHeaders(response);
            var mimeType = response.Content?.Headers.ContentType?.MediaType ?? "application/json";

            var responseObj = new JsonObject
            {
                ["url"] = response.RequestMessage?.RequestUri?.ToString() ?? request.RequestUri?.ToString() ?? "",
                ["status"] = (int)response.StatusCode,
                ["statusText"] = response.StatusCode.ToString(),
                ["headers"] = headersObj,
                ["mimeType"] = mimeType,
                ["connectionReused"] = true,
                ["connectionId"] = 0,
                ["encodedDataLength"] = response.Content?.Headers.ContentLength ?? 0,
                ["securityState"] = "secure"
            };

            var responseParams = new JsonObject
            {
                ["requestId"] = requestId,
                ["loaderId"] = "loader-1",
                ["timestamp"] = timestamp,
                ["type"] = "XHR",
                ["response"] = responseObj
            };

            BroadcastEvent("Network.responseReceived", responseParams);

            // Buffer and cache body asynchronously to not block, then fire finished
            if (response.Content != null)
            {
                var content = response.Content;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await content.LoadIntoBufferAsync();
                        string body = await content.ReadAsStringAsync();
                        _responseBodies[requestId] = body;
                    }
                    catch (Exception ex)
                    {
                        _responseBodies[requestId] = $"Error reading body: {ex.Message}";
                    }

                    var finishedParams = new JsonObject
                    {
                        ["requestId"] = requestId,
                        ["timestamp"] = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                        ["encodedDataLength"] = content.Headers.ContentLength ?? 0
                    };
                    BroadcastEvent("Network.loadingFinished", finishedParams);
                });
            }
            else
            {
                var finishedParams = new JsonObject
                {
                    ["requestId"] = requestId,
                    ["timestamp"] = timestamp,
                    ["encodedDataLength"] = 0
                };
                BroadcastEvent("Network.loadingFinished", finishedParams);
            }
        }
        else
        {
            // Failed request
            var finishedParams = new JsonObject
            {
                ["requestId"] = requestId,
                ["timestamp"] = timestamp,
                ["encodedDataLength"] = 0
            };
            BroadcastEvent("Network.loadingFinished", finishedParams);
        }
    }

    private static void BroadcastEvent(string method, JsonObject @params)
    {
        foreach (var session in _enabledSessions.Keys)
        {
            _ = session.SendEventAsync(method, @params);
        }
    }

    private static JsonObject MapHeaders(HttpRequestMessage request)
    {
        var obj = new JsonObject();
        foreach (var header in request.Headers)
        {
            obj[header.Key] = string.Join(", ", header.Value);
        }
        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                obj[header.Key] = string.Join(", ", header.Value);
            }
        }
        return obj;
    }

    private static JsonObject MapHeaders(HttpResponseMessage response)
    {
        var obj = new JsonObject();
        foreach (var header in response.Headers)
        {
            obj[header.Key] = string.Join(", ", header.Value);
        }
        if (response.Content != null)
        {
            foreach (var header in response.Content.Headers)
            {
                obj[header.Key] = string.Join(", ", header.Value);
            }
        }
        return obj;
    }
}

public class NetworkDiagnosticObserver : IObserver<DiagnosticListener>
{
    public void OnCompleted() { }
    public void OnError(Exception error) { }

    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "HttpHandlerDiagnosticListener")
        {
            listener.Subscribe(new HttpKeyValueObserver());
        }
    }
}

public class HttpKeyValueObserver : IObserver<KeyValuePair<string, object?>>
{
    public void OnCompleted() { }
    public void OnError(Exception error) { }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        try
        {
            if (value.Key == "System.Net.Http.HttpRequestOut.Start")
            {
                var request = GetProperty<HttpRequestMessage>(value.Value, "Request");
                if (request != null)
                {
                    NetworkDomain.OnRequestStart(request);
                }
            }
            else if (value.Key == "System.Net.Http.HttpRequestOut.Stop")
            {
                var request = GetProperty<HttpRequestMessage>(value.Value, "Request");
                var response = GetProperty<HttpResponseMessage>(value.Value, "Response");
                if (request != null)
                {
                    NetworkDomain.OnRequestStop(request, response);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing HTTP diagnostic event: {ex.Message}");
        }
    }

    private static T? GetProperty<T>(object? obj, string propertyName) where T : class
    {
        if (obj == null) return null;
        var prop = obj.GetType().GetProperty(propertyName);
        return prop?.GetValue(obj) as T;
    }
}
