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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Avalonia.Diagnostics.Cdp;

public static class CdpServer
{
    private static HttpListener? _listener;
    private static bool _isRunning;
    private static int _port = 9222;
    public static int Port => _port;
    public static System.IO.TextWriter OriginalOut => _originalOut ?? Console.Out;
    public static System.IO.TextWriter OriginalError => _originalError ?? Console.Error;
    private static readonly ConcurrentDictionary<string, (TopLevel Window, string Title)> _windows = new();
    private static readonly ConcurrentDictionary<CdpSession, byte> _sessions = new();
    public static IEnumerable<CdpSession> Sessions => _sessions.Keys;
    private static System.IO.TextWriter? _originalOut;
    private static System.IO.TextWriter? _originalError;
    private static ConsoleRedirector? _redirectedOut;
    private static ConsoleRedirector? _redirectedError;
    private static IDisposable? _windowOpenedSub;
    private static IDisposable? _windowClosedSub;

    public static void AddSession(CdpSession session)
    {
        _sessions[session] = 0;
    }

    public static void RemoveSession(CdpSession session)
    {
        _sessions.TryRemove(session, out _);
    }

    private static void NotifyTargetCreated(string targetId, string title)
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
                        ["type"] = "page",
                        ["title"] = title,
                        ["url"] = $"http://localhost:{_port}/",
                        ["attached"] = true,
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

    public static string Register(TopLevel window, string title)
    {
        foreach (var pair in _windows)
        {
            if (pair.Value.Window == window) return pair.Key;
        }

        var id = Guid.NewGuid().ToString();
        _windows[id] = (window, title);
        NotifyTargetCreated(id, title);
        return id;
    }

    private static void NotifyTargetInfoChanged(string targetId, string title)
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
                        ["type"] = "page",
                        ["title"] = title,
                        ["url"] = $"http://localhost:{_port}/",
                        ["attached"] = true,
                        ["browserContextId"] = "1"
                    }
                });
            }
        }
    }

    public static void UpdateTitle(TopLevel window, string newTitle)
    {
        var key = _windows.FirstOrDefault(x => x.Value.Window == window).Key;
        if (key != null)
        {
            _windows[key] = (window, newTitle);
            NotifyTargetInfoChanged(key, newTitle);
        }
    }

    public static void Unregister(TopLevel window)
    {
        var key = _windows.FirstOrDefault(x => x.Value.Window == window).Key;
        if (key != null)
        {
            _windows.TryRemove(key, out _);
            foreach (var session in _sessions.Keys)
            {
                session.DetachTargetById(key);
            }
            NotifyTargetDestroyed(key);
        }
    }

    public static IEnumerable<(string Id, TopLevel Window, string Title)> GetWindows()
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            return Avalonia.Threading.Dispatcher.UIThread.Invoke(GetWindows);
        }

        var active = new Dictionary<TopLevel, (string Id, string Title)>();

        foreach (var pair in _windows)
        {
            active[pair.Value.Window] = (pair.Key, pair.Value.Title);
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var win in desktop.Windows)
            {
                if (!active.ContainsKey(win))
                {
                    var id = Guid.NewGuid().ToString();
                    var title = win.Title ?? "Avalonia Window";
                    _windows[id] = (win, title);
                    active[win] = (id, title);
                    NotifyTargetCreated(id, title);
                }
            }
        }

        return active.Select(x => (x.Value.Id, x.Key, x.Value.Title));
    }

    public static JsonArray GetActiveTargets()
    {
        var array = new JsonArray();
        foreach (var win in GetWindows())
        {
            array.Add(new JsonObject
            {
                ["targetId"] = win.Id,
                ["type"] = "page",
                ["title"] = win.Title,
                ["url"] = $"http://localhost:{_port}/",
                ["attached"] = true,
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

        // Initialize Network diagnostic listener
        Avalonia.Diagnostics.Cdp.Domains.NetworkDomain.Initialize();

        // Initialize Memory diagnostic tracker
        Avalonia.Diagnostics.Cdp.Domains.MemoryDomain.Initialize();

        // Integrate CDP Log domain hook
        var originalSink = Avalonia.Logging.Logger.Sink;
        var cdpSink = new Avalonia.Diagnostics.Cdp.Domains.CdpLogSink();
        Avalonia.Logging.Logger.Sink = new Avalonia.Diagnostics.Cdp.Domains.CompositeLogSink(originalSink, cdpSink);

        // Intercept Console stdout and stderr
        _originalOut = Console.Out;
        _originalError = Console.Error;
        _redirectedOut = new ConsoleRedirector(_originalOut, "Information");
        _redirectedError = new ConsoleRedirector(_originalError, "Error");
        Console.SetOut(_redirectedOut);
        Console.SetError(_redirectedError);

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        try
        {
            _listener.Start();
        }
        catch (HttpListenerException)
        {
            try { _listener.Close(); } catch { }
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
        }

        _windowOpenedSub = Window.WindowOpenedEvent.AddClassHandler<Window>((w, e) =>
        {
            Register(w, w.Title ?? w.GetType().Name);
        });
        _windowClosedSub = Window.WindowClosedEvent.AddClassHandler<Window>((w, e) =>
        {
            Unregister(w);
        });

        Task.Run(ListenLoopAsync);
    }

    public static void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

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
        _redirectedOut = null;
        _redirectedError = null;

        // Shutdown Network diagnostic listener and Fetch interceptions
        Avalonia.Diagnostics.Cdp.Domains.NetworkDomain.Shutdown();
        Avalonia.Diagnostics.Cdp.Domains.FetchDomain.Shutdown();
        Avalonia.Diagnostics.Cdp.Domains.MemoryDomain.Shutdown();

        // Restore original log sink
        if (Avalonia.Logging.Logger.Sink is Avalonia.Diagnostics.Cdp.Domains.CompositeLogSink composite)
        {
            Avalonia.Logging.Logger.Sink = composite.OriginalSink;
        }

        _windowOpenedSub?.Dispose();
        _windowOpenedSub = null;
        _windowClosedSub?.Dispose();
        _windowClosedSub = null;

        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
        _listener = null;
        _windows.Clear();
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
                if (path.StartsWith("/devtools/page/"))
                {
                    var targetId = path.Substring("/devtools/page/".Length);
                    var winInfo = GetWindows().FirstOrDefault(w => w.Id == targetId);
                    if (winInfo.Window != null)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null, TimeSpan.FromSeconds(10));
                        var session = new CdpSession(wsContext.WebSocket, winInfo.Window);
                        await session.StartAsync();
                        return;
                    }
                }
                else if (path == "/devtools/browser")
                {
                    var firstWin = GetWindows().FirstOrDefault();
                    if (firstWin.Window != null)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null, TimeSpan.FromSeconds(10));
                        var session = new CdpSession(wsContext.WebSocket, firstWin.Window);
                        await session.StartAsync();
                        return;
                    }
                }

                response.StatusCode = 404;
                response.Close();
                return;
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

            var urlPath = request.Url?.AbsolutePath ?? "";
            var host = request.Url?.Authority;
            if (string.IsNullOrEmpty(host))
            {
                host = $"localhost:{_port}";
            }

            if (urlPath == "/json/version")
            {
                var versionJson = new JsonObject
                {
                    ["Browser"] = "Avalonia/11.3.12",
                    ["Protocol-Version"] = "1.3",
                    ["User-Agent"] = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
                    ["webSocketDebuggerUrl"] = $"ws://{host}/devtools/browser"
                };
                SendJsonResponse(response, versionJson);
            }
            else if (urlPath == "/json" || urlPath == "/json/list")
            {
                var list = new JsonArray();
                foreach (var win in GetWindows())
                {
                    list.Add(new JsonObject
                    {
                        ["description"] = "",
                        ["devtoolsFrontendUrl"] = $"devtools://devtools/bundled/inspector.html?ws={host}/devtools/page/{win.Id}",
                        ["id"] = win.Id,
                        ["title"] = win.Title,
                        ["type"] = "page",
                        ["url"] = $"http://{host}/",
                        ["webSocketDebuggerUrl"] = $"ws://{host}/devtools/page/{win.Id}"
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
            Console.WriteLine($"CDP Server error: {ex}");
            response.StatusCode = 500;
            response.Close();
        }
    }

    private static void SendJsonResponse(HttpListenerResponse response, JsonNode json)
    {
        var bytes = Encoding.UTF8.GetBytes(json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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
        Avalonia.Diagnostics.Cdp.Domains.LogDomain.BroadcastLog("Console", _level, line);
    }
}
