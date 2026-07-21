using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Chrome.DevTools.Protocol;

public static class CdpServer
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger("CdpServer");
    private static HttpListener? _listener;
    private static bool _isRunning;
    private static int _port = 9222;
    public static int Port => _port;

    private static bool _waitForDebugger = false;
    public static bool WaitForDebugger
    {
        get => _waitForDebugger;
        set => _waitForDebugger = value;
    }

    private static bool _hasWaitedForDebugger = false;
    public static bool HasWaitedForDebugger
    {
        get => _hasWaitedForDebugger;
        set => _hasWaitedForDebugger = value;
    }

    private static readonly ConcurrentDictionary<string, bool> _waitingTargets = new();

    public static bool IsTargetWaitingForDebugger(string targetId)
    {
        return _waitingTargets.TryGetValue(targetId, out var waiting) && waiting;
    }

    public static void SetTargetWaitingForDebugger(string targetId, bool waiting)
    {
        if (waiting)
        {
            _waitingTargets[targetId] = true;
        }
        else
        {
            _waitingTargets.TryRemove(targetId, out _);
        }
    }

    private static QueuedTextWriter? _queuedOut;
    private static QueuedTextWriter? _queuedError;

    public static System.IO.TextWriter OriginalOut => _queuedOut ?? Console.Out;
    public static System.IO.TextWriter OriginalError => _queuedError ?? Console.Error;

    private static readonly ConcurrentDictionary<string, ICdpTarget> _targets = new();
    private static readonly HashSet<string> _providerTargetIds = new();
    private static readonly ConcurrentDictionary<CdpSession, byte> _sessions = new();
    public static IEnumerable<CdpSession> Sessions => _sessions.Keys;
    private static readonly ConcurrentDictionary<string, BiDiSession> _bidiSessions = new();
    public static ConcurrentDictionary<string, BiDiSession> BiDiSessions => _bidiSessions;

    private static System.IO.TextWriter? _originalOut;
    private static System.IO.TextWriter? _originalError;
    private static ConsoleRedirector? _redirectedOut;
    private static ConsoleRedirector? _redirectedError;

    // Platform-independence delegate hooks
    public static Func<IEnumerable<ICdpTarget>>? TargetProvider { get; set; }
    public static Func<string, string?, Task<ICdpTarget>>? TargetFactory { get; set; }
    public static Func<WebSocket, ICdpTarget?, CdpSession>? SessionFactory { get; set; }
    public static Func<CdpSession, string, string, ICdpTarget, CdpTargetSession>? TargetSessionFactory { get; set; }
    public static Func<Func<Task<JsonObject>>, Task<JsonObject>>? UIThreadInvoker { get; set; }

    public static event Action? ServerStarted;
    public static event Action? ServerStopped;
    public static event Action<CdpSession>? SessionAdded;
    public static event Action<CdpSession>? SessionRemoved;

    public static void AddSession(CdpSession session)
    {
        _sessions[session] = 0;
        SessionAdded?.Invoke(session);
    }

    public static void RemoveSession(CdpSession session)
    {
        if (_sessions.TryRemove(session, out _))
        {
            SessionRemoved?.Invoke(session);
        }
    }

    public static bool IsTargetAttached(string targetId)
    {
        return _sessions.Keys.Any(session => session.IsTargetAttached(targetId));
    }

    private static void NotifyTargetCreated(string targetId, string title, string type = "page")
    {
        foreach (var session in _sessions.Keys)
        {
            if (session.DiscoverTargetsEnabled)
            {
                _ = session.SendEventAsync("Target.targetCreated", new JsonObject
                {
                    ["targetInfo"] = new JsonObject
                    {
                        ["targetId"] = targetId,
                        ["type"] = type,
                        ["title"] = title,
                        ["url"] = $"http://localhost:{_port}/",
                        ["attached"] = IsTargetAttached(targetId),
                        ["browserContextId"] = "1"
                    }
                });
            }
        }
    }

    private static void NotifyTargetDestroyed(string targetId)
    {
        foreach (var session in _sessions.Keys)
        {
            if (session.DiscoverTargetsEnabled)
            {
                _ = session.SendEventAsync("Target.targetDestroyed", new JsonObject
                {
                    ["targetId"] = targetId
                });
            }
        }
    }

    public static string Register(ICdpTarget target)
    {
        Logger.LogServerDebug($"CdpServer.Register target: Id={target.Id}, Title='{target.Title}', Type={target.Type}");
        foreach (var pair in _targets)
        {
            if (pair.Value == target) return pair.Key;
        }

        var id = target.Id;
        _targets[id] = target;
        NotifyTargetCreated(id, target.Title, target.Type);

        if (target.Type == "page")
        {
            var tabTarget = new CdpTabTarget(target);
            _targets[tabTarget.Id] = tabTarget;
            NotifyTargetCreated(tabTarget.Id, tabTarget.Title, tabTarget.Type);
        }

        foreach (var session in _sessions.Keys)
        {
            Logger.LogServerDebug($"Checking session: AutoAttachEnabled={session.AutoAttachEnabled}");
            if (session.AutoAttachEnabled)
            {
                Logger.LogServerDebug($"Auto-attaching target {target.Id} to session");
                session.AutoAttachTarget(target, isNewTarget: true);
                if (target.Type == "page")
                {
                    string tabId = $"tab-{target.Id}";
                    if (_targets.TryGetValue(tabId, out var tabTarget))
                    {
                        session.AutoAttachTarget(tabTarget, isNewTarget: true);
                    }
                }
            }
        }

        return id;
    }

    private static void NotifyTargetInfoChanged(string targetId, string title, string type = "page")
    {
        foreach (var session in _sessions.Keys)
        {
            if (session.DiscoverTargetsEnabled)
            {
                _ = session.SendEventAsync("Target.targetInfoChanged", new JsonObject
                {
                    ["targetInfo"] = new JsonObject
                    {
                        ["targetId"] = targetId,
                        ["type"] = type,
                        ["title"] = title,
                        ["url"] = $"http://localhost:{_port}/",
                        ["attached"] = IsTargetAttached(targetId),
                        ["browserContextId"] = "1"
                    }
                });
            }
        }
    }

    public static void BroadcastDomainsUpdated()
    {
        foreach (var session in _sessions.Keys)
        {
            _ = session.SendEventAsync("Schema.domainsUpdated", new JsonObject());
        }
    }

    public static void UpdateTitle(ICdpTarget target, string newTitle)
    {
        var key = _targets.FirstOrDefault(x => x.Value.Id == target.Id).Key;
        if (key != null)
        {
            _targets[key] = target;
            NotifyTargetInfoChanged(key, newTitle, target.Type);
        }
    }

    public static void Unregister(ICdpTarget target)
    {
        var key = _targets.FirstOrDefault(x => x.Value == target).Key;
        if (key != null)
        {
            _targets.TryRemove(key, out _);
            foreach (var session in _sessions.Keys)
            {
                session.DetachTargetById(key);
            }
            NotifyTargetDestroyed(key);

            string tabKey = $"tab-{key}";
            if (_targets.TryRemove(tabKey, out _))
            {
                foreach (var session in _sessions.Keys)
                {
                    session.DetachTargetById(tabKey);
                }
                NotifyTargetDestroyed(tabKey);
            }
        }
    }

    public static IEnumerable<ICdpTarget> GetTargets()
    {
        if (TargetProvider != null)
        {
            var providerTargets = TargetProvider().ToList();
            var newProviderTargetIds = providerTargets.Select(t => t.Id).ToHashSet();

            List<string> disappearedIds;
            lock (_providerTargetIds)
            {
                disappearedIds = _providerTargetIds.Where(id => !newProviderTargetIds.Contains(id)).ToList();
                foreach (var id in disappearedIds)
                {
                    _providerTargetIds.Remove(id);
                }
            }

            foreach (var id in disappearedIds)
            {
                _targets.TryRemove(id, out _);
                NotifyTargetDestroyed(id);

                foreach (var session in _sessions.Keys)
                {
                    var sessId = session.GetSessionIdForTarget(id);
                    if (sessId != null)
                    {
                        session.DetachTarget(sessId);
                    }
                }
            }

            foreach (var target in providerTargets)
            {
                lock (_providerTargetIds)
                {
                    _providerTargetIds.Add(target.Id);
                }

                if (!_targets.ContainsKey(target.Id))
                {
                    _targets[target.Id] = target;
                    NotifyTargetCreated(target.Id, target.Title, target.Type);

                    foreach (var session in _sessions.Keys)
                    {
                        if (session.AutoAttachEnabled)
                        {
                            session.AutoAttachTarget(target, isNewTarget: true);
                        }
                    }
                }
            }
        }

        return _targets.Values;
    }

    public static JsonArray GetActiveTargets()
    {
        var array = new JsonArray();
        foreach (var target in GetTargets())
        {
            array.Add(new JsonObject
            {
                ["targetId"] = target.Id,
                ["type"] = target.Type,
                ["title"] = target.Title,
                ["url"] = target.Url,
                ["attached"] = IsTargetAttached(target.Id),
                ["browserContextId"] = "1"
            });
        }
        return array;
    }

    public static void Start(int port = 9222)
    {
        if (_isRunning) return;
        _port = port;
        _isRunning = true;
        Logger.ServerStarted(port);

        try
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.Exists(args, arg => arg.Equals("--wait-for-debugger", StringComparison.OrdinalIgnoreCase)))
            {
                _waitForDebugger = true;
            }
        }
        catch { }

        // Initialize Network diagnostic listener
        Chrome.DevTools.Protocol.Domains.NetworkDomain.Initialize();

        // Intercept Console stdout and stderr
        _originalOut = Console.Out;
        _originalError = Console.Error;
        _queuedOut = new QueuedTextWriter(_originalOut);
        _queuedError = new QueuedTextWriter(_originalError);
        _redirectedOut = new ConsoleRedirector(_queuedOut, "Information");
        _redirectedError = new ConsoleRedirector(_queuedError, "Error");
        Console.SetOut(_redirectedOut);
        Console.SetError(_redirectedError);

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Prefixes.Add($"http://localhost:{port}/");

        try
        {
            _listener.Start();
        }
        catch (HttpListenerException)
        {
            try { _listener.Close(); } catch { }
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                _listener.Start();
            }
            catch
            {
                try { _listener.Close(); } catch { }
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
            }
        }

        Task.Run(ListenLoopAsync);

        ServerStarted?.Invoke();
    }

    public static void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        Logger.ServerStopped();

        // Restore Console redirectors
        if (_originalOut != null)
        {
            Console.SetOut(_originalOut);
            _originalOut = null;
        }
        if (_originalError != null)
        {
            Console.SetError(_originalError);
            _originalError = null;
        }
        _queuedOut?.Dispose();
        _queuedOut = null;
        _queuedError?.Dispose();
        _queuedError = null;
        _redirectedOut = null;
        _redirectedError = null;

        // Shutdown Network diagnostic listener and Fetch interceptions
        Chrome.DevTools.Protocol.Domains.NetworkDomain.Shutdown();
        Chrome.DevTools.Protocol.Domains.FetchDomain.Shutdown();

        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
        _listener = null;
        _targets.Clear();

        foreach (var session in _sessions.Keys)
        {
            try { session.Close(); } catch { }
        }
        _sessions.Clear();

        foreach (var session in _bidiSessions.Values)
        {
            try { session.Close(); } catch { }
        }
        _bidiSessions.Clear();

        _waitingTargets.Clear();
        _hasWaitedForDebugger = false;
        _waitForDebugger = false;

        ServerStopped?.Invoke();
    }

    private static async Task ListenLoopAsync()
    {
        while (_isRunning && _listener != null)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleHttpRequestAsync(context));
            }
            catch
            {
                // Listener stopped
            }
        }
    }

    private static async Task HandleHttpRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            if (request.IsWebSocketRequest)
            {
                var path = request.Url?.AbsolutePath ?? "";
                if (path == "/session/bidi" || path.StartsWith("/session/bidi/"))
                {
                    var wsContext = await context.AcceptWebSocketAsync(null, TimeSpan.FromSeconds(10));
                    string? bidiSessionId = null;
                    if (path.StartsWith("/session/bidi/"))
                    {
                        bidiSessionId = path.Substring("/session/bidi/".Length);
                    }
                    else
                    {
                        var query = request.Url?.Query;
                        if (!string.IsNullOrEmpty(query))
                        {
                            var parts = query.TrimStart('?').Split('&');
                            foreach (var part in parts)
                            {
                                var kv = part.Split('=');
                                if (kv.Length == 2 && (kv[0] == "session" || kv[0] == "sessionId"))
                                {
                                    bidiSessionId = Uri.UnescapeDataString(kv[1]);
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (string.IsNullOrEmpty(bidiSessionId))
                    {
                        bidiSessionId = _bidiSessions.Keys.LastOrDefault() ?? Guid.NewGuid().ToString();
                    }
                    
                    if (!_bidiSessions.TryGetValue(bidiSessionId, out var bidiSession))
                    {
                        bidiSession = new BiDiSession(bidiSessionId);
                        _bidiSessions[bidiSessionId] = bidiSession;
                    }
                    
                    try
                    {
                        await bidiSession.StartAsync(wsContext.WebSocket);
                    }
                    finally
                    {
                        _bidiSessions.TryRemove(bidiSessionId, out _);
                    }
                    return;
                }

                if (path.StartsWith("/devtools/page/"))
                {
                    var targetId = path.Substring("/devtools/page/".Length);
                    var targetInfo = GetTargets().FirstOrDefault(w => w.Id == targetId);
                    if (targetInfo != null)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null, TimeSpan.FromSeconds(10));
                        var session = SessionFactory?.Invoke(wsContext.WebSocket, targetInfo)
                                      ?? new CdpSession(wsContext.WebSocket, targetInfo);
                        await session.StartAsync();
                        return;
                    }
                }
                else if (path == "/devtools/browser")
                {
                    var firstTarget = GetTargets().FirstOrDefault();
                    if (firstTarget != null)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null, TimeSpan.FromSeconds(10));
                        var session = SessionFactory?.Invoke(wsContext.WebSocket, firstTarget)
                                      ?? new CdpSession(wsContext.WebSocket, firstTarget);
                        await session.StartAsync();
                        return;
                    }
                }

                response.StatusCode = 404;
                response.Close();
                return;
            }

            var urlPath = request.Url?.AbsolutePath ?? "";
            var host = request.Url?.Authority;
            if (string.IsNullOrEmpty(host))
            {
                host = $"localhost:{_port}";
            }

            if (request.HttpMethod == "OPTIONS")
            {
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                response.StatusCode = 200;
                response.Close();
                return;
            }

            if (request.HttpMethod == "POST" && (urlPath == "/session" || urlPath == "/session/"))
            {
                var sessionId = Guid.NewGuid().ToString();
                var bidiSession = new BiDiSession(sessionId);
                _bidiSessions[sessionId] = bidiSession;
                
                var responseJson = new JsonObject
                {
                    ["value"] = new JsonObject
                    {
                        ["sessionId"] = sessionId,
                        ["capabilities"] = new JsonObject
                        {
                            ["webSocketUrl"] = $"ws://{host}/session/bidi/{sessionId}"
                        }
                    }
                };
                SendJsonResponse(response, responseJson);
                return;
            }

            if (urlPath == "/json/version" || urlPath == "/json/version/")
            {
                var versionJson = new JsonObject
                {
                    ["Browser"] = "Chrome/150.0.6723.116",
                    ["Protocol-Version"] = "1.3",
                    ["User-Agent"] = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
                    ["V8-Version"] = "13.0.245.18",
                    ["WebKit-Version"] = "537.36 (@e092147318721c5f3e58c0e2714246fa4bb6034e)",
                    ["webSocketDebuggerUrl"] = $"ws://{host}/devtools/browser"
                };
                SendJsonResponse(response, versionJson);
            }
            else if (urlPath == "/json" || urlPath == "/json/" || urlPath == "/json/list" || urlPath == "/json/list/")
            {
                var list = new JsonArray();
                foreach (var target in GetTargets())
                {
                    var typeMapped = target.Type == "tab" ? "page" : target.Type;
                    list.Add(new JsonObject
                    {
                        ["description"] = "",
                        ["devtoolsFrontendUrl"] = $"devtools://devtools/bundled/inspector.html?ws={host}/devtools/page/{target.Id}",
                        ["id"] = target.Id,
                        ["title"] = target.Title,
                        ["type"] = typeMapped,
                        ["url"] = target.Url,
                        ["webSocketDebuggerUrl"] = $"ws://{host}/devtools/page/{target.Id}"
                    });
                }
                SendJsonResponse(response, list);
            }
            else
            {
                response.StatusCode = 404;
                response.Close();
            }
        }
        catch (Exception ex)
        {
            Logger.ServerError(ex.Message, ex);
            response.StatusCode = 500;
            response.Close();
        }
    }

    private static void SendJsonResponse(HttpListenerResponse response, JsonNode json)
    {
        var bytes = Encoding.UTF8.GetBytes(json.ToJsonString(new JsonSerializerOptions { MaxDepth = 256, WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() }));
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }
}

public class ConsoleRedirector : System.IO.TextWriter
{
    private readonly System.IO.TextWriter _original;
    private readonly string _level;
    private readonly System.Text.StringBuilder _buffer = new();

    [ThreadStatic]
    private static bool _isRedirecting;

    public ConsoleRedirector(System.IO.TextWriter original, string level)
    {
        _original = original;
        _level = level;
    }

    public override System.Text.Encoding Encoding => _original.Encoding;

    public override void Write(char value)
    {
        _original.Write(value);
        if (_isRedirecting) return;

        _isRedirecting = true;
        try
        {
            lock (_buffer)
            {
                if (value == '\n')
                {
                    var line = _buffer.ToString().TrimEnd('\r');
                    _buffer.Clear();
                    Broadcast(line);
                }
                else
                {
                    _buffer.Append(value);
                }
            }
        }
        finally
        {
            _isRedirecting = false;
        }
    }

    public override void Write(string? value)
    {
        _original.Write(value);
        if (value == null || _isRedirecting) return;

        _isRedirecting = true;
        try
        {
            lock (_buffer)
            {
                int index;
                int start = 0;
                while ((index = value.IndexOf('\n', start)) >= 0)
                {
                    _buffer.Append(value.Substring(start, index - start));
                    var line = _buffer.ToString().TrimEnd('\r');
                    _buffer.Clear();
                    Broadcast(line);
                    start = index + 1;
                }
                if (start < value.Length)
                {
                    _buffer.Append(value.Substring(start));
                }
            }
        }
        finally
        {
            _isRedirecting = false;
        }
    }

    private void Broadcast(string line)
    {
        Chrome.DevTools.Protocol.Domains.LogDomain.BroadcastLog("Console", _level, line);
    }
}

public class CdpTabTarget : ICdpTarget
{
    private readonly ICdpTarget _pageTarget;

    public CdpTabTarget(ICdpTarget pageTarget)
    {
        _pageTarget = pageTarget;
        Id = $"tab-{pageTarget.Id}";
    }

    public string Id { get; }
    public string Title => _pageTarget.Title;
    public string Type => "tab";
    public string Url => _pageTarget.Url;

    public void Activate() => _pageTarget.Activate();
    public void Close() => _pageTarget.Close();
}

public class QueuedTextWriter : System.IO.TextWriter
{
    private readonly System.IO.TextWriter _underlying;
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _queue = new();
    private readonly System.Threading.Tasks.Task _processTask;
    private readonly System.Threading.CancellationTokenSource _cts = new();

    public QueuedTextWriter(System.IO.TextWriter underlying)
    {
        _underlying = underlying;
        _processTask = System.Threading.Tasks.Task.Run(ProcessQueueAsync);
    }

    public override System.Text.Encoding Encoding => _underlying.Encoding;

    public override void Write(char value)
    {
        Write(value.ToString());
    }

    public override void Write(string? value)
    {
        if (value != null)
        {
            _queue.Enqueue(value);
        }
    }

    public override void WriteLine(string? value)
    {
        if (value != null)
        {
            _queue.Enqueue(value + Environment.NewLine);
        }
    }

    private async System.Threading.Tasks.Task ProcessQueueAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var item))
            {
                try
                {
                    _underlying.Write(item);
                    _underlying.Flush();
                }
                catch { }
            }
            else
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(10, _cts.Token);
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore other errors
                }
            }
        }

        // Flush any remaining items in the queue when cancelled
        while (_queue.TryDequeue(out var item))
        {
            try
            {
                _underlying.Write(item);
            }
            catch { }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            try { _processTask.Wait(150); } catch { }
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }
}
