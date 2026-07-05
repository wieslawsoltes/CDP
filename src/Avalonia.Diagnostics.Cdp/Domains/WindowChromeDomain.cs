using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class WindowChromeDomain
{
    private static async Task InvokeAsync(Action action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(action);
        }
    }

    private static async Task<T> InvokeAsync<T>(Func<T> action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            return action();
        }
        else
        {
            return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(action);
        }
    }

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        bool hasWindowId = @params.ContainsKey("windowId") && @params["windowId"] != null;
        int windowId = @params["windowId"]?.GetValue<int>() ?? 0;
        TopLevel? targetWindow = null;

        await InvokeAsync(() =>
        {
            if (hasWindowId && windowId != 0)
            {
                var windows = CdpServer.GetWindows().ToList();
                targetWindow = windows.Select(w => w.Window).FirstOrDefault(w => Math.Abs(w.GetHashCode()) == windowId);
            }
            else
            {
                targetWindow = session.Window;
            }
        });

        if (targetWindow == null)
        {
            throw new Exception(hasWindowId && windowId != 0 ? $"Target window with ID {windowId} not found" : "Target window not found");
        }

        switch (action)
        {
            case "setTopmost":
                {
                    bool topmost = @params["topmost"]?.GetValue<bool>() ?? false;
                    await InvokeAsync(() =>
                    {
                        if (targetWindow is Window win)
                        {
                            win.Topmost = topmost;
                        }
                    });
                    return new JsonObject { ["success"] = true };
                }

            case "setOpacity":
                {
                    double opacity = @params["opacity"]?.GetValue<double>() ?? 1.0;
                    await InvokeAsync(() =>
                    {
                        if (targetWindow is Window win)
                        {
                            win.Opacity = opacity;
                        }
                    });
                    return new JsonObject { ["success"] = true };
                }

            case "setTitle":
                {
                    string title = @params["title"]?.GetValue<string>() ?? "";
                    await InvokeAsync(() =>
                    {
                        if (targetWindow is Window win)
                        {
                            win.Title = title;
                            CdpServer.UpdateTitle(win, title);
                        }
                    });
                    return new JsonObject { ["success"] = true };
                }

            case "dragWindow":
                {
                    int deltaX = @params["deltaX"]?.GetValue<int>() ?? 0;
                    int deltaY = @params["deltaY"]?.GetValue<int>() ?? 0;
                    await InvokeAsync(() =>
                    {
                        if (targetWindow is Window win)
                        {
                            var currentPos = win.Position;
                            win.Position = new PixelPoint(currentPos.X + deltaX, currentPos.Y + deltaY);
                        }
                    });
                    return new JsonObject { ["success"] = true };
                }

            case "minimize":
                {
                    await InvokeAsync(() =>
                    {
                        if (targetWindow is Window win)
                        {
                            win.WindowState = WindowState.Minimized;
                        }
                    });
                    return new JsonObject { ["success"] = true };
                }

            case "maximize":
                {
                    await InvokeAsync(() =>
                    {
                        if (targetWindow is Window win)
                        {
                            win.WindowState = WindowState.Maximized;
                        }
                    });
                    return new JsonObject { ["success"] = true };
                }

            case "restore":
                {
                    await InvokeAsync(() =>
                    {
                        if (targetWindow is Window win)
                        {
                            win.WindowState = WindowState.Normal;
                        }
                    });
                    return new JsonObject { ["success"] = true };
                }

            case "close":
                {
                    await InvokeAsync(() =>
                    {
                        if (targetWindow is Window win)
                        {
                            win.Close();
                        }
                    });
                    return new JsonObject { ["success"] = true };
                }

            case "activate":
                {
                    await InvokeAsync(() =>
                    {
                        if (targetWindow is Window win)
                        {
                            win.Activate();
                        }
                    });
                    return new JsonObject { ["success"] = true };
                }

            case "getWindowDetails":
                {
                    bool topmost = false;
                    double opacity = 1.0;
                    string title = "";
                    string windowState = "normal";
                    await InvokeAsync(() =>
                    {
                        if (targetWindow is Window win)
                        {
                            topmost = win.Topmost;
                            opacity = win.Opacity;
                            title = win.Title ?? "";
                            windowState = win.WindowState switch
                            {
                                WindowState.Maximized => "maximized",
                                WindowState.Minimized => "minimized",
                                WindowState.FullScreen => "fullscreen",
                                _ => "normal"
                            };
                        }
                    });
                    return new JsonObject
                    {
                        ["success"] = true,
                        ["topmost"] = topmost,
                        ["opacity"] = opacity,
                        ["title"] = title,
                        ["windowState"] = windowState
                    };
                }

            default:
                throw new Exception($"Method WindowChrome.{action} is not implemented");
        }
    }
}
