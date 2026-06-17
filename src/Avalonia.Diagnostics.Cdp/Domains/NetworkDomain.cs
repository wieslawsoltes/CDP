using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class NetworkDomain
{
    private static readonly ConcurrentDictionary<CdpSession, bool> _enabledSessions = new();
    private static readonly ConcurrentDictionary<string, string> _responseBodies = new();
    private static readonly ConcurrentDictionary<HttpRequestMessage, string> _requestIds = new();
    private static readonly ConcurrentBag<IDisposable> _listenerSubscriptions = new();
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

        while (_listenerSubscriptions.TryTake(out var sub))
        {
            try
            {
                sub.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        _enabledSessions.Clear();
        _responseBodies.Clear();
        _requestIds.Clear();
    }

    public static void RegisterListenerSubscription(IDisposable subscription)
    {
        _listenerSubscriptions.Add(subscription);
    }

    public static void CacheResponseBody(string requestId, string body)
    {
        _responseBodies[requestId] = body;
    }

    public static void BroadcastNetworkEvent(string method, JsonObject @params)
    {
        BroadcastEvent(method, @params);
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
            ["documentURL"] = $"http://localhost:{CdpServer.Port}/",
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

            if (response.Content != null)
            {
                response.Content = new InterceptingHttpContent(response.Content, requestId);
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
            var sub = listener.Subscribe(new HttpKeyValueObserver());
            NetworkDomain.RegisterListenerSubscription(sub);
        }
    }
}

internal class InterceptingHttpContent : HttpContent
{
    private readonly HttpContent _inner;
    private readonly string _requestId;
    private readonly MemoryStream _buffer = new();
    private bool _captured;
    private bool _limitExceeded;
    private const int MaxCaptureLength = 5 * 1024 * 1024; // 5 MB

    public InterceptingHttpContent(HttpContent inner, string requestId)
    {
        _inner = inner;
        _requestId = requestId;

        // Copy headers
        foreach (var header in inner.Headers)
        {
            Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // If it's a known streaming content type, mark as limit exceeded immediately so we don't buffer
        var mediaType = inner.Headers.ContentType?.MediaType;
        if (mediaType != null && IsStreamingMediaType(mediaType))
        {
            _limitExceeded = true;
        }
    }

    private static bool IsStreamingMediaType(string mediaType)
    {
        return mediaType switch
        {
            "text/event-stream" => true,
            "application/x-ndjson" => true,
            "application/json-seq" => true,
            "application/grpc" => true,
            "application/grpc+proto" => true,
            "multipart/x-mixed-replace" => true,
            "application/octet-stream" => true,
            _ => false
        };
    }

    public void TrackData(ReadOnlySpan<byte> data)
    {
        if (_limitExceeded) return;

        lock (_buffer)
        {
            if (_buffer.Length + data.Length > MaxCaptureLength)
            {
                _limitExceeded = true;
                _buffer.SetLength(0); // Free buffer memory
                return;
            }
            _buffer.Write(data);
        }
    }

    public void OnComplete()
    {
        lock (_buffer)
        {
            if (_captured) return;
            _captured = true;

            long contentLength = _inner.Headers.ContentLength ?? 0;
            if (!_limitExceeded && _buffer.Length > 0)
            {
                try
                {
                    var bytes = _buffer.ToArray();
                    string body = Encoding.UTF8.GetString(bytes);
                    NetworkDomain.CacheResponseBody(_requestId, body);
                    contentLength = bytes.Length;
                }
                catch
                {
                    // ignore
                }
            }

            var finishedParams = new JsonObject
            {
                ["requestId"] = _requestId,
                ["timestamp"] = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                ["encodedDataLength"] = contentLength
            };
            NetworkDomain.BroadcastNetworkEvent("Network.loadingFinished", finishedParams);
        }
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        return SerializeToStreamAsync(stream, context, CancellationToken.None);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        using var trackingStream = new TrackingStream(stream, this);
        await _inner.CopyToAsync(trackingStream, context, cancellationToken).ConfigureAwait(false);
        OnComplete();
    }

    protected override Task<Stream> CreateContentReadStreamAsync()
    {
        return CreateContentReadStreamAsync(CancellationToken.None);
    }

    protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
    {
        var stream = await _inner.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new TrackingStream(stream, this);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _inner.Headers.ContentLength ?? 0;
        return _inner.Headers.ContentLength.HasValue;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _buffer.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal class TrackingStream : Stream
{
    private readonly Stream _inner;
    private readonly InterceptingHttpContent _parent;
    private bool _completed;

    public TrackingStream(Stream inner, InterceptingHttpContent parent)
    {
        _inner = inner;
        _parent = parent;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _inner.Read(buffer, offset, count);
        if (read > 0)
        {
            _parent.TrackData(new ReadOnlySpan<byte>(buffer, offset, read));
        }
        else if (read == 0)
        {
            Complete();
        }
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int read = await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        if (read > 0)
        {
            _parent.TrackData(new ReadOnlySpan<byte>(buffer, offset, read));
        }
        else if (read == 0)
        {
            Complete();
        }
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read > 0)
        {
            _parent.TrackData(buffer.Span.Slice(0, read));
        }
        else if (read == 0)
        {
            Complete();
        }
        return read;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            Complete();
        }
        base.Dispose(disposing);
    }

    private void Complete()
    {
        if (!_completed)
        {
            _completed = true;
            _parent.OnComplete();
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
