using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Chrome.DevTools.Protocol.Inspector;

/// <summary>
/// A lightweight client for a standalone V8 Inspector endpoint. It transports every
/// Runtime, Debugger, Profiler, HeapProfiler and Schema command without requiring a browser target.
/// </summary>
public sealed class V8InspectorClient : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly V8InspectorClientOptions _options;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _receiveCancellation;
    private Task? _receiveTask;
    private int _nextId;
    private int _connectionClosedRaised;
    private SynchronizationContext? _notificationContext;

    public V8InspectorClient(V8InspectorClientOptions? options = null)
    {
        _options = options ?? new V8InspectorClientOptions();
        if (_options.ReceiveBufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Receive buffer size must be positive.");
        if (_options.MaxIncomingMessageSize <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Maximum incoming message size must be positive.");
        if (_options.MaxOutgoingMessageSize <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Maximum outgoing message size must be positive.");
        _socket.Options.KeepAliveInterval = _options.KeepAliveInterval;
        _socket.Options.KeepAliveTimeout = _options.KeepAliveTimeout;
        _options.ConfigureWebSocket?.Invoke(_socket.Options);
    }

    public bool IsConnected => _socket.State == WebSocketState.Open;
    public IReadOnlySet<string> SupportedDomains { get; private set; } = new HashSet<string>();
    public event EventHandler<V8InspectorEventArgs>? EventReceived;
    public event EventHandler<V8InspectorSubscriberExceptionEventArgs>? EventSubscriberFailed;
    public event EventHandler<V8InspectorConnectionClosedEventArgs>? ConnectionClosed;

    public static async Task<IReadOnlyList<V8InspectorTarget>> DiscoverTargetsAsync(
        Uri endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var builder = new UriBuilder(endpoint)
        {
            Scheme = endpoint.Scheme switch { "ws" => "http", "wss" => "https", _ => endpoint.Scheme },
            Path = "/json/list",
            Query = ""
        };

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var json = await client.GetStringAsync(builder.Uri, cancellationToken).ConfigureAwait(false);
        var payload = JsonNode.Parse(json) as JsonArray;
        if (payload is null) return Array.Empty<V8InspectorTarget>();

        return payload.OfType<JsonObject>().Select(item => new V8InspectorTarget(
            item["id"]?.GetValue<string>() ?? "",
            item["type"]?.GetValue<string>() ?? "",
            item["title"]?.GetValue<string>() ?? "",
            item["url"]?.GetValue<string>() ?? "",
            item["webSocketDebuggerUrl"]?.GetValue<string>() ?? "",
            item["devtoolsFrontendUrl"]?.GetValue<string>() ?? "")).ToArray();
    }

    public async Task ConnectAsync(Uri webSocketUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webSocketUri);
        if (IsConnected) throw new InvalidOperationException("The V8 Inspector client is already connected.");

        _notificationContext = SynchronizationContext.Current;
        await _socket.ConnectAsync(webSocketUri, cancellationToken).ConfigureAwait(false);
        _receiveCancellation = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_receiveCancellation.Token);

        try
        {
            var schema = await SendCommandAsync("Schema.getDomains", cancellationToken: cancellationToken).ConfigureAwait(false);
            SupportedDomains = schema["domains"] is JsonArray domains
                ? domains.OfType<JsonObject>()
                    .Select(domain => domain["name"]?.GetValue<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>();
        }
        catch (V8InspectorProtocolException)
        {
            SupportedDomains = new HashSet<string>();
        }
    }

    public bool SupportsDomain(string domain) =>
        SupportedDomains.Count == 0 || SupportedDomains.Contains(domain);

    public async Task<JsonObject> SendCommandAsync(
        string method,
        JsonObject? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected) throw new InvalidOperationException("The V8 Inspector client is not connected.");
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion)) throw new InvalidOperationException("Unable to register Inspector request.");

        using var registration = cancellationToken.Register(() =>
        {
            if (_pending.TryRemove(id, out var request)) request.TrySetCanceled(cancellationToken);
        });

        var request = new JsonObject
        {
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters ?? new JsonObject()
        };
        var bytes = Encoding.UTF8.GetBytes(request.ToJsonString());
        if (bytes.Length > _options.MaxOutgoingMessageSize)
        {
            _pending.TryRemove(id, out _);
            throw new InvalidOperationException(
                $"V8 Inspector request is {bytes.Length} bytes, exceeding the configured {_options.MaxOutgoingMessageSize}-byte limit.");
        }

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _pending.TryRemove(id, out _);
            throw;
        }
        finally
        {
            _sendLock.Release();
        }

        var response = await completion.Task.ConfigureAwait(false);
        if (response["error"] is JsonObject error)
        {
            throw new V8InspectorProtocolException(
                method,
                error["code"]?.GetValue<int>() ?? 0,
                error["message"]?.GetValue<string>() ?? "Unknown V8 Inspector error",
                error["data"]?.DeepClone());
        }
        return response["result"] as JsonObject ?? new JsonObject();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[_options.ReceiveBufferSize];
        Exception? connectionFailure = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    if (message.Length + result.Count > _options.MaxIncomingMessageSize)
                    {
                        throw new WebSocketException(
                            $"V8 Inspector message exceeded the configured {_options.MaxIncomingMessageSize}-byte limit.");
                    }
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text) continue;
                var json = JsonNode.Parse(message.ToArray()) as JsonObject;
                if (json is null) continue;

                if (json["id"] is JsonValue idNode && idNode.TryGetValue<int>(out var id))
                {
                    if (_pending.TryRemove(id, out var completion)) completion.TrySetResult(json);
                    continue;
                }

                var method = json["method"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(method))
                {
                    DispatchEvent(new V8InspectorEventArgs(method, json["params"] as JsonObject ?? new JsonObject()));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            connectionFailure = ex;
            FailPending(ex);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                FailPending(new WebSocketException("The V8 Inspector connection closed."));
            }
            RaiseConnectionClosed(connectionFailure, cancellationToken.IsCancellationRequested);
        }
    }

    private void DispatchEvent(V8InspectorEventArgs args)
    {
        void Dispatch()
        {
            var handlers = EventReceived;
            if (handlers is null) return;
            foreach (EventHandler<V8InspectorEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    RaiseSubscriberFailed(handler, args.Method, ex);
                }
            }
        }

        if (_notificationContext is { } context && context != SynchronizationContext.Current)
        {
            context.Post(_ => Dispatch(), null);
        }
        else
        {
            Dispatch();
        }
    }

    private void RaiseSubscriberFailed(Delegate subscriber, string method, Exception exception)
    {
        var handlers = EventSubscriberFailed;
        if (handlers is null) return;
        var args = new V8InspectorSubscriberExceptionEventArgs(subscriber, method, exception);
        foreach (EventHandler<V8InspectorSubscriberExceptionEventArgs> handler in handlers.GetInvocationList())
        {
            try { handler(this, args); } catch { }
        }
    }

    private void RaiseConnectionClosed(Exception? exception, bool wasRequested)
    {
        if (Interlocked.Exchange(ref _connectionClosedRaised, 1) != 0) return;
        var handlers = ConnectionClosed;
        if (handlers is null) return;
        var args = new V8InspectorConnectionClosedEventArgs(exception, wasRequested);

        void Dispatch()
        {
            foreach (EventHandler<V8InspectorConnectionClosedEventArgs> handler in handlers.GetInvocationList())
            {
                try { handler(this, args); } catch { }
            }
        }

        if (_notificationContext is { } context && context != SynchronizationContext.Current) context.Post(_ => Dispatch(), null);
        else Dispatch();
    }

    private void FailPending(Exception exception)
    {
        foreach (var item in _pending.ToArray())
        {
            if (_pending.TryRemove(item.Key, out var completion)) completion.TrySetException(exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _receiveCancellation?.Cancel();
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
            }
        }
        if (_receiveTask is not null)
        {
            try { await _receiveTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        FailPending(new ObjectDisposedException(nameof(V8InspectorClient)));
        _receiveCancellation?.Dispose();
        _sendLock.Dispose();
        _socket.Dispose();
    }
}

public sealed record V8InspectorTarget(
    string Id,
    string Type,
    string Title,
    string Url,
    string WebSocketDebuggerUrl,
    string DevToolsFrontendUrl);

public sealed class V8InspectorEventArgs(string method, JsonObject parameters) : EventArgs
{
    public string Method { get; } = method;
    public JsonObject Params { get; } = parameters;
}

public sealed class V8InspectorProtocolException(string method, int code, string protocolMessage, JsonNode? protocolData = null)
    : Exception($"V8 Inspector command '{method}' failed ({code}): {protocolMessage}")
{
    public string Method { get; } = method;
    public int Code { get; } = code;
    public JsonNode? ProtocolData { get; } = protocolData;
}

public sealed class V8InspectorClientOptions
{
    public int ReceiveBufferSize { get; init; } = 16 * 1024;
    public int MaxIncomingMessageSize { get; init; } = 64 * 1024 * 1024;
    public int MaxOutgoingMessageSize { get; init; } = 64 * 1024 * 1024;
    public TimeSpan KeepAliveInterval { get; init; } = Timeout.InfiniteTimeSpan;
    public TimeSpan KeepAliveTimeout { get; init; } = Timeout.InfiniteTimeSpan;
    public Action<ClientWebSocketOptions>? ConfigureWebSocket { get; init; }
}

public sealed class V8InspectorSubscriberExceptionEventArgs(
    Delegate subscriber,
    string method,
    Exception exception) : EventArgs
{
    public Delegate Subscriber { get; } = subscriber;
    public string Method { get; } = method;
    public Exception Exception { get; } = exception;
}

public sealed class V8InspectorConnectionClosedEventArgs(Exception? exception, bool wasRequested) : EventArgs
{
    public Exception? Exception { get; } = exception;
    public bool WasRequested { get; } = wasRequested;
}
