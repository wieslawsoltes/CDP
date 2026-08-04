using System.Buffers;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chrome.DevTools.Protocol.Inspector;

/// <summary>
/// Lightweight discovery and WebSocket host for raw V8 Inspector/CDP transports.
/// It deliberately does not route messages through <see cref="CdpDispatcher"/>.
/// </summary>
public sealed class CdpInspectorServer : IAsyncDisposable
{
    private readonly IRawCdpTargetProvider _targets;
    private readonly CdpInspectorVersionInfo _version;
    private readonly CdpInspectorServerOptions _options;
    private readonly SemaphoreSlim _sessionSlots;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, Task> _activeRequests = new();
    private readonly object _sync = new();
    private CancellationTokenSource? _shutdown;
    private HttpListener? _listener;
    private Task? _listenTask;
    private string? _accessToken;
    private long _nextRequestId;

    public CdpInspectorServer(
        IRawCdpTargetProvider targets,
        CdpInspectorVersionInfo version,
        CdpInspectorServerOptions options)
    {
        _targets = targets ?? throw new ArgumentNullException(nameof(targets));
        _version = version ?? throw new ArgumentNullException(nameof(version));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _sessionSlots = new SemaphoreSlim(options.MaxConcurrentSessions, options.MaxConcurrentSessions);
    }

    public bool IsRunning => _listener?.IsListening == true;

    public Uri DiscoveryUri => new($"http://{FormatAddress(_options.Address)}:{_options.Port}/");

    public string AccessToken => _accessToken ?? throw new InvalidOperationException("The server has not been started.");

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_listener is not null)
            {
                throw new InvalidOperationException("The Inspector server is already running.");
            }

            _accessToken = _options.ValidateAndGetAccessToken();
            var shutdown = new CancellationTokenSource();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://{FormatAddress(_options.Address)}:{_options.Port}/");
            try
            {
                listener.Start();
            }
            catch
            {
                listener.Close();
                shutdown.Dispose();
                throw;
            }

            _shutdown = shutdown;
            _listener = listener;
            _listenTask = ListenAsync(listener, shutdown.Token);
        }

        if (_options.LifecycleObserver is not null)
        {
            await _options.LifecycleObserver.ServerStartedAsync(DiscoveryUri, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        HttpListener? listener;
        Task? listenTask;
        CancellationTokenSource? shutdown;
        lock (_sync)
        {
            listener = _listener;
            listenTask = _listenTask;
            shutdown = _shutdown;
            _listener = null;
            _listenTask = null;
            _shutdown = null;
        }

        if (listener is null)
        {
            return;
        }

        shutdown?.Cancel();
        listener.Close();
        if (listenTask is not null)
        {
            try
            {
                await listenTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (HttpListenerException)
            {
            }
        }

        var activeRequests = _activeRequests.Values.ToArray();
        if (activeRequests.Length > 0)
        {
            try
            {
                await Task.WhenAll(activeRequests).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        shutdown?.Dispose();

        if (_options.LifecycleObserver is not null)
        {
            await _options.LifecycleObserver.ServerStoppedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ListenAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (!listener.IsListening)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                // HttpListener.Close completes an outstanding GetContextAsync
                // with ObjectDisposedException on Windows. This is the normal
                // listener shutdown path, equivalent to cancellation on Unix.
                break;
            }

            var requestId = Interlocked.Increment(ref _nextRequestId);
            var requestTask = HandleRequestAsync(context, cancellationToken);
            _activeRequests[requestId] = requestTask;
            _ = ObserveRequestAsync(requestId, requestTask);
        }
    }

    private async Task ObserveRequestAsync(long requestId, Task requestTask)
    {
        try
        {
            await requestTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _activeRequests.TryRemove(requestId, out _);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            if ((context.Request.IsWebSocketRequest || _options.RequireAuthenticationForDiscovery) &&
                !IsAuthorized(context.Request))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.Headers["WWW-Authenticate"] = "Bearer";
                context.Response.Close();
                return;
            }

            var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
            if (context.Request.IsWebSocketRequest)
            {
                await HandleWebSocketAsync(context, path, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                context.Response.Close();
                return;
            }

            if (path == "/json/version")
            {
                await WriteJsonAsync(context.Response, CreateVersionPayload(context.Request), cancellationToken).ConfigureAwait(false);
            }
            else if (path is "/json" or "/json/list")
            {
                await WriteJsonAsync(context.Response, CreateTargetList(context.Request), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            TryClose(context.Response, HttpStatusCode.InternalServerError);
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context, string path, CancellationToken cancellationToken)
    {
        const string targetPrefix = "/devtools/page/";
        if (!path.StartsWith(targetPrefix, StringComparison.Ordinal))
        {
            TryClose(context.Response, HttpStatusCode.NotFound);
            return;
        }

        var targetId = Uri.UnescapeDataString(path[targetPrefix.Length..]);
        if (!_targets.TryGetTarget(targetId, out var target) || target is null)
        {
            TryClose(context.Response, HttpStatusCode.NotFound);
            return;
        }

        var origin = context.Request.Headers["Origin"];
        if (!_options.OriginPolicy.IsAllowed(origin))
        {
            TryClose(context.Response, HttpStatusCode.Forbidden);
            return;
        }

        if (!await _sessionSlots.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            TryClose(context.Response, HttpStatusCode.ServiceUnavailable);
            return;
        }

        var connectionContext = new CdpInspectorConnectionContext(target.Info, context.Request.RemoteEndPoint, origin);
        Exception? error = null;
        WebSocket? webSocket = null;
        var sessionStarted = false;
        try
        {
            var webSocketContext = await context.AcceptWebSocketAsync(
                subProtocol: null,
                receiveBufferSize: _options.ReceiveBufferBytes,
                keepAliveInterval: _options.WebSocketKeepAliveInterval).ConfigureAwait(false);
            webSocket = webSocketContext.WebSocket;

            if (_options.LifecycleObserver is not null)
            {
                await _options.LifecycleObserver.SessionStartedAsync(connectionContext, cancellationToken).ConfigureAwait(false);
            }
            sessionStarted = true;

            await using var transport = await target.OpenTransportAsync(connectionContext, cancellationToken).ConfigureAwait(false);
            await RunRawSessionAsync(webSocket, transport, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            error = ex;
            if (webSocket is not null)
            {
                await TryCloseWebSocketAsync(webSocket, WebSocketCloseStatus.InternalServerError, "Inspector session failed").ConfigureAwait(false);
            }
            else
            {
                TryClose(context.Response, HttpStatusCode.InternalServerError);
            }
        }
        finally
        {
            webSocket?.Dispose();
            if (sessionStarted && _options.LifecycleObserver is not null)
            {
                await _options.LifecycleObserver.SessionStoppedAsync(connectionContext, error, CancellationToken.None).ConfigureAwait(false);
            }
            _sessionSlots.Release();
        }
    }

    private async Task RunRawSessionAsync(WebSocket webSocket, IRawCdpTransport transport, CancellationToken cancellationToken)
    {
        using var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var browserToRuntime = PumpBrowserToRuntimeAsync(webSocket, transport, sessionCancellation.Token);
        var runtimeToBrowser = PumpRuntimeToBrowserAsync(webSocket, transport, sessionCancellation.Token);

        var completed = await Task.WhenAny(browserToRuntime, runtimeToBrowser).ConfigureAwait(false);
        Exception? error = null;
        try
        {
            await completed.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            error = ex;
        }
        sessionCancellation.Cancel();
        try
        {
            await Task.WhenAll(browserToRuntime, runtimeToBrowser).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            await TryCloseWebSocketAsync(webSocket, WebSocketCloseStatus.NormalClosure, "Inspector session closed").ConfigureAwait(false);
        }

        if (error is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    private static async Task TryCloseWebSocketAsync(WebSocket webSocket, WebSocketCloseStatus status, string description)
    {
        if (webSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            await webSocket.CloseOutputAsync(status, description, CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task PumpBrowserToRuntimeAsync(WebSocket webSocket, IRawCdpTransport transport, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(_options.ReceiveBufferBytes);
        try
        {
            while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Inspector messages must be UTF-8 JSON text", CancellationToken.None).ConfigureAwait(false);
                        return;
                    }
                    if (message.Length + result.Count > _options.MaxMessageBytes)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Inspector message exceeds configured limit", CancellationToken.None).ConfigureAwait(false);
                        return;
                    }
                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                await transport.SendAsync(message.GetBuffer().AsMemory(0, checked((int)message.Length)), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task PumpRuntimeToBrowserAsync(WebSocket webSocket, IRawCdpTransport transport, CancellationToken cancellationToken)
    {
        await foreach (var message in transport.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (message.Length > _options.MaxMessageBytes)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Inspector message exceeds configured limit", CancellationToken.None).ConfigureAwait(false);
                return;
            }

            await webSocket.SendAsync(message, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
    }

    private JsonObject CreateVersionPayload(HttpListenerRequest request)
    {
        var authority = request.Url?.Authority ?? $"{FormatAddress(_options.Address)}:{_options.Port}";
        var browserTarget = _targets.GetTargets().FirstOrDefault();
        var targetPath = browserTarget is null ? string.Empty : $"/devtools/page/{Uri.EscapeDataString(browserTarget.Info.Id)}";
        return new JsonObject
        {
            ["Browser"] = _version.Browser,
            ["Protocol-Version"] = _version.ProtocolVersion,
            ["User-Agent"] = _version.UserAgent,
            ["V8-Version"] = _version.V8Version,
            ["WebKit-Version"] = _version.WebKitVersion,
            ["webSocketDebuggerUrl"] = browserTarget is null ? string.Empty : $"ws://{authority}{targetPath}?token={Uri.EscapeDataString(AccessToken)}"
        };
    }

    private JsonArray CreateTargetList(HttpListenerRequest request)
    {
        var authority = request.Url?.Authority ?? $"{FormatAddress(_options.Address)}:{_options.Port}";
        var result = new JsonArray();
        foreach (var target in _targets.GetTargets())
        {
            var escapedId = Uri.EscapeDataString(target.Info.Id);
            var socketPath = $"{authority}/devtools/page/{escapedId}?token={Uri.EscapeDataString(AccessToken)}";
            result.Add(new JsonObject
            {
                ["description"] = target.Info.Description,
                ["devtoolsFrontendUrl"] = $"devtools://devtools/bundled/inspector.html?ws={socketPath}",
                ["id"] = target.Info.Id,
                ["title"] = target.Info.Title,
                ["type"] = target.Info.Type,
                ["url"] = target.Info.Url,
                ["webSocketDebuggerUrl"] = $"ws://{socketPath}"
            });
        }
        return result;
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        var supplied = request.QueryString["token"];
        if (string.IsNullOrEmpty(supplied))
        {
            const string bearer = "Bearer ";
            var authorization = request.Headers["Authorization"];
            if (authorization?.StartsWith(bearer, StringComparison.OrdinalIgnoreCase) == true)
            {
                supplied = authorization[bearer.Length..].Trim();
            }
        }

        if (string.IsNullOrEmpty(supplied) || _accessToken is null)
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(_accessToken);
        var actualBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, JsonNode payload, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = true });
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "application/json; charset=utf-8";
        response.Headers["Cache-Control"] = "no-store";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private static void TryClose(HttpListenerResponse response, HttpStatusCode statusCode)
    {
        try
        {
            response.StatusCode = (int)statusCode;
            response.Close();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static string FormatAddress(IPAddress address) =>
        address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _sessionSlots.Dispose();
    }
}
