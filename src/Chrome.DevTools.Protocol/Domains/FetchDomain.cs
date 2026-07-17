using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Chrome.DevTools.Protocol.Domains;

public static class FetchDomain
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger("FetchDomain");
    private static readonly ConcurrentDictionary<CdpSession, List<RequestPattern>> _enabledSessions = new();
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<InterceptResult>> _pendingInterceptions = new();

    public static bool IsEnabled => !_enabledSessions.IsEmpty;

    public static void RemoveSession(CdpSession session)
    {
        _enabledSessions.TryRemove(session, out _);
    }

    public static void Shutdown()
    {
        _enabledSessions.Clear();
        foreach (var tcs in _pendingInterceptions.Values)
        {
            tcs.TrySetResult(new InterceptResult { Action = InterceptAction.Continue });
        }
        _pendingInterceptions.Clear();
    }

    public static void RegisterPendingInterception(string interceptionId, TaskCompletionSource<InterceptResult> tcs)
    {
        _pendingInterceptions[interceptionId] = tcs;
    }

    public static void RemovePendingInterception(string interceptionId)
    {
        _pendingInterceptions.TryRemove(interceptionId, out _);
    }

    public static bool ShouldIntercept(HttpRequestMessage request, out string stage)
    {
        stage = "Request";
        if (!IsEnabled) return false;

        var url = request.RequestUri?.ToString() ?? "";

        // Check each enabled session's patterns
        foreach (var patterns in _enabledSessions.Values)
        {
            if (patterns == null || patterns.Count == 0)
            {
                // If Fetch is enabled but no patterns are specified, all requests are intercepted.
                return true;
            }

            foreach (var pattern in patterns)
            {
                bool matches = MatchesPattern(url, pattern.UrlPattern);
                if (matches)
                {
                    if (pattern.RequestStage != null)
                    {
                        stage = pattern.RequestStage;
                    }
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MatchesPattern(string url, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return true; // Empty pattern means match everything
        if (pattern == "*") return true;

        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(url, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void BroadcastRequestPaused(string interceptionId, HttpRequestMessage request)
    {
        var timestamp = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        var headersObj = new JsonObject();
        foreach (var header in request.Headers)
        {
            headersObj[header.Key] = string.Join(", ", header.Value);
        }
        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                headersObj[header.Key] = string.Join(", ", header.Value);
            }
        }

        var requestObj = new JsonObject
        {
            ["url"] = request.RequestUri?.ToString() ?? "",
            ["method"] = request.Method.Method,
            ["headers"] = headersObj,
            ["initialPriority"] = "Medium",
            ["referrerPolicy"] = "no-referrer-when-downgrade"
        };

        var pausedParams = new JsonObject
        {
            ["requestId"] = interceptionId, // in Fetch domain, requestId is used for interceptionId
            ["request"] = requestObj,
            ["resourceType"] = "XHR",
            ["frameId"] = "frame-1"
        };

        foreach (var session in _enabledSessions.Keys)
        {
            _ = session.SendEventAsync("Fetch.requestPaused", pausedParams);
        }
    }

    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "enable":
                {
                    var patternsList = new List<RequestPattern>();
                    var patternsArray = @params["patterns"]?.AsArray();
                    if (patternsArray != null)
                    {
                        foreach (var node in patternsArray)
                        {
                            if (node == null) continue;
                            patternsList.Add(new RequestPattern
                            {
                                UrlPattern = node["urlPattern"]?.GetValue<string>(),
                                ResourceType = node["resourceType"]?.GetValue<string>(),
                                RequestStage = node["requestStage"]?.GetValue<string>()
                            });
                        }
                    }
                    _enabledSessions[session] = patternsList;
                    return Task.FromResult(new JsonObject());
                }

            case "disable":
                {
                    _enabledSessions.TryRemove(session, out _);
                    return Task.FromResult(new JsonObject());
                }

            case "continueRequest":
                {
                    var requestId = @params["requestId"]?.GetValue<string>() ?? "";
                    if (_pendingInterceptions.TryGetValue(requestId, out var tcs))
                    {
                        var url = @params["url"]?.GetValue<string>();
                        var method = @params["method"]?.GetValue<string>();
                        
                        Dictionary<string, string>? modifiedHeaders = null;
                        var headersArray = @params["headers"]?.AsArray();
                        if (headersArray != null)
                        {
                            modifiedHeaders = new Dictionary<string, string>();
                            foreach (var hNode in headersArray)
                            {
                                var name = hNode?["name"]?.GetValue<string>();
                                var value = hNode?["value"]?.GetValue<string>();
                                if (name != null && value != null)
                                {
                                    modifiedHeaders[name] = value;
                                }
                            }
                        }

                        tcs.TrySetResult(new InterceptResult
                        {
                            Action = InterceptAction.Continue,
                            ModifiedUrl = url,
                            ModifiedMethod = method,
                            ModifiedHeaders = modifiedHeaders
                        });
                    }
                    return Task.FromResult(new JsonObject());
                }

            case "fulfillRequest":
                {
                    var requestId = @params["requestId"]?.GetValue<string>() ?? "";
                    Logger.LogFetchDebug($"fulfillRequest called for requestId: {requestId}");
                    if (_pendingInterceptions.TryGetValue(requestId, out var tcs))
                    {
                        Logger.LogFetchDebug($"Found pending interception for requestId: {requestId}");
                        var responseCode = @params["responseCode"]?.GetValue<int>() ?? 200;
                        
                        var responseHeaders = new Dictionary<string, string>();
                        var headersArray = @params["responseHeaders"]?.AsArray();
                        if (headersArray != null)
                        {
                            foreach (var hNode in headersArray)
                            {
                                var name = hNode?["name"]?.GetValue<string>();
                                var value = hNode?["value"]?.GetValue<string>();
                                if (name != null && value != null)
                                {
                                    responseHeaders[name] = value;
                                }
                            }
                        }

                        var bodyStr = @params["body"]?.GetValue<string>();
                        byte[]? bodyBytes = null;
                        if (!string.IsNullOrEmpty(bodyStr))
                        {
                            try
                            {
                                bodyBytes = Convert.FromBase64String(bodyStr);
                            }
                            catch
                            {
                                bodyBytes = Encoding.UTF8.GetBytes(bodyStr);
                            }
                        }

                        tcs.TrySetResult(new InterceptResult
                        {
                            Action = InterceptAction.Fulfill,
                            ResponseCode = responseCode,
                            ResponseHeaders = responseHeaders,
                            BodyBytes = bodyBytes
                        });
                    }
                    else
                    {
                        Logger.LogWarningMessage("FetchDomain", $"WARNING: pending interception NOT found for requestId: {requestId}!");
                        Logger.LogFetchDebug($"Current pending interception IDs: {string.Join(", ", _pendingInterceptions.Keys)}");
                    }
                    return Task.FromResult(new JsonObject());
                }

            case "failRequest":
                {
                    var requestId = @params["requestId"]?.GetValue<string>() ?? "";
                    if (_pendingInterceptions.TryGetValue(requestId, out var tcs))
                    {
                        var errorReason = @params["errorReason"]?.GetValue<string>() ?? "Failed";
                        tcs.TrySetResult(new InterceptResult
                        {
                            Action = InterceptAction.Fail,
                            ErrorReason = errorReason
                        });
                    }
                    return Task.FromResult(new JsonObject());
                }

            default:
                throw new Exception($"Method Fetch.{action} is not implemented");
        }
    }
}

public class RequestPattern
{
    public string? UrlPattern { get; set; }
    public string? ResourceType { get; set; }
    public string? RequestStage { get; set; }
}

public enum InterceptAction
{
    Continue,
    Fulfill,
    Fail
}

public class InterceptResult
{
    public InterceptAction Action { get; set; }
    public int ResponseCode { get; set; }
    public Dictionary<string, string>? ResponseHeaders { get; set; }
    public byte[]? BodyBytes { get; set; }
    public string? ErrorReason { get; set; }
    public string? ModifiedUrl { get; set; }
    public string? ModifiedMethod { get; set; }
    public Dictionary<string, string>? ModifiedHeaders { get; set; }
}
