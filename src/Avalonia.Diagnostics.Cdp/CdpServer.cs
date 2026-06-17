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
    private static readonly ConcurrentDictionary<string, (TopLevel Window, string Title)> _windows = new();

    public static string Register(TopLevel window, string title)
    {
        foreach (var pair in _windows)
        {
            if (pair.Value.Window == window) return pair.Key;
        }

        var id = Guid.NewGuid().ToString();
        _windows[id] = (window, title);
        return id;
    }

    public static void Unregister(TopLevel window)
    {
        var key = _windows.FirstOrDefault(x => x.Value.Window == window).Key;
        if (key != null)
        {
            _windows.TryRemove(key, out _);
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
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();

        Task.Run(ListenLoopAsync);
    }

    public static void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
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
                        var wsContext = await context.AcceptWebSocketAsync(null);
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
                        var wsContext = await context.AcceptWebSocketAsync(null);
                        var session = new CdpSession(wsContext.WebSocket, firstWin.Window);
                        await session.StartAsync();
                        return;
                    }
                }

                response.StatusCode = 404;
                response.Close();
                return;
            }

            var urlPath = request.Url?.AbsolutePath ?? "";
            if (urlPath == "/json/version")
            {
                var versionJson = new JsonObject
                {
                    ["Browser"] = "Avalonia/11.3.12",
                    ["Protocol-Version"] = "1.3",
                    ["User-Agent"] = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
                    ["webSocketDebuggerUrl"] = $"ws://localhost:{_port}/devtools/browser"
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
                        ["devtoolsFrontendUrl"] = $"devtools://devtools/bundled/js_app.html?experiments=true&v8only=true&ws=localhost:{_port}/devtools/page/{win.Id}",
                        ["id"] = win.Id,
                        ["title"] = win.Title,
                        ["type"] = "page",
                        ["url"] = $"http://localhost:{_port}/",
                        ["webSocketDebuggerUrl"] = $"ws://localhost:{_port}/devtools/page/{win.Id}"
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
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }
}
