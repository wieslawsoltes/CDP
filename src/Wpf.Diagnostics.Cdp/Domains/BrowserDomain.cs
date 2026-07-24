using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;

namespace Wpf.Diagnostics.Cdp.Domains;

public static class BrowserDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getVersion":
                {
                    return new JsonObject
                    {
                        ["protocolVersion"] = "1.3",
                        ["product"] = "WPF/10.0",
                        ["revision"] = "1.0",
                        ["userAgent"] = "Mozilla/5.0 (Windows Presentation Foundation)",
                        ["jsVersion"] = ".NET 10.0"
                    };
                }

            case "close":
                {
                    if (session.Window != null)
                    {
                        await session.Window.Dispatcher.InvokeAsync(() =>
                        {
                            Application.Current?.Shutdown();
                        });
                    }
                    return new JsonObject();
                }

            case "getWindowForTarget":
                {
                    string? targetId = @params["targetId"]?.GetValue<string>();
                    Window? targetWindow = null;

                    if (session.Window != null)
                    {
                        await session.Window.Dispatcher.InvokeAsync(() =>
                        {
                            var windows = CdpServer.GetWindows().ToList();
                            if (!string.IsNullOrEmpty(targetId))
                            {
                                var match = windows.FirstOrDefault(w => w.Id == targetId);
                                targetWindow = match.Window;
                            }
                            else
                            {
                                targetWindow = session.Window;
                            }
                        });
                    }

                    if (targetWindow == null)
                    {
                        throw new Exception($"Target '{targetId}' not found");
                    }

                    int windowId = Math.Abs(targetWindow.GetHashCode());
                    return new JsonObject
                    {
                        ["windowId"] = windowId
                    };
                }

            case "getWindowBounds":
                {
                    int windowId = @params["windowId"]?.GetValue<int>() ?? 0;
                    Window? targetWindow = null;

                    if (session.Window != null)
                    {
                        await session.Window.Dispatcher.InvokeAsync(() =>
                        {
                            var windows = CdpServer.GetWindows().ToList();
                            targetWindow = windows.Select(w => w.Window).FirstOrDefault(w => Math.Abs(w.GetHashCode()) == windowId);
                            if (targetWindow == null)
                            {
                                targetWindow = session.Window;
                            }
                        });
                    }

                    double left = 0, top = 0, width = 800, height = 600;
                    string windowState = "normal";

                    if (session.Window != null)
                    {
                        await session.Window.Dispatcher.InvokeAsync(() =>
                        {
                            if (targetWindow != null)
                            {
                                left = targetWindow.Left;
                                top = targetWindow.Top;
                                width = targetWindow.Width;
                                height = targetWindow.Height;
                                windowState = targetWindow.WindowState.ToString().ToLowerInvariant();
                            }
                        });
                    }

                    return new JsonObject
                    {
                        ["bounds"] = new JsonObject
                        {
                            ["left"] = (int)left,
                            ["top"] = (int)top,
                            ["width"] = (int)width,
                            ["height"] = (int)height,
                            ["windowState"] = windowState
                        }
                    };
                }

            case "setWindowBounds":
                {
                    int windowId = @params["windowId"]?.GetValue<int>() ?? 0;
                    var boundsNode = @params["bounds"] as JsonObject;
                    Window? targetWindow = null;

                    if (session.Window != null)
                    {
                        await session.Window.Dispatcher.InvokeAsync(() =>
                        {
                            var windows = CdpServer.GetWindows().ToList();
                            targetWindow = windows.Select(w => w.Window).FirstOrDefault(w => Math.Abs(w.GetHashCode()) == windowId);
                            if (targetWindow == null)
                            {
                                targetWindow = session.Window;
                            }
                        });
                    }

                    if (boundsNode != null && targetWindow != null && session.Window != null)
                    {
                        await session.Window.Dispatcher.InvokeAsync(() =>
                        {
                            int? leftVal = boundsNode["left"]?.GetValue<int>();
                            int? topVal = boundsNode["top"]?.GetValue<int>();
                            int? widthVal = boundsNode["width"]?.GetValue<int>();
                            int? heightVal = boundsNode["height"]?.GetValue<int>();

                            if (leftVal.HasValue) targetWindow.Left = leftVal.Value;
                            if (topVal.HasValue) targetWindow.Top = topVal.Value;
                            if (widthVal.HasValue) targetWindow.Width = widthVal.Value;
                            if (heightVal.HasValue) targetWindow.Height = heightVal.Value;
                        });
                    }
                    return new JsonObject();
                }

            default:
                return await Chrome.DevTools.Protocol.Domains.BrowserDomain.HandleAsync(session, action, @params);
        }
    }
}
