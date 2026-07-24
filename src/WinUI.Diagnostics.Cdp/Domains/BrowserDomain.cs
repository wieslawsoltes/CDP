using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace WinUI.Diagnostics.Cdp.Domains;

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
                        ["product"] = "Uno/WinUI/3.0",
                        ["revision"] = "1.0.0",
                        ["userAgent"] = "WinUI-CDP-Agent",
                        ["jsVersion"] = "Jint/3.0"
                    };
                }

            case "close":
                {
                    if (session.Window != null)
                    {
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
                        {
                            Application.Current?.Exit();
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
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
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
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
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
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
                        {
                            if (targetWindow != null)
                            {
                                var bounds = targetWindow.Bounds;
                                left = bounds.X;
                                top = bounds.Y;
                                width = bounds.Width;
                                height = bounds.Height;
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
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
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
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
                        {
                            int? leftVal = boundsNode["left"]?.GetValue<int>();
                            int? topVal = boundsNode["top"]?.GetValue<int>();
                            int? widthVal = boundsNode["width"]?.GetValue<int>();
                            int? heightVal = boundsNode["height"]?.GetValue<int>();

                            bool resizedAppWindow = false;

                            // Attempt native WinUI AppWindow resizing first
                            var appWinProp = targetWindow.GetType().GetProperty("AppWindow", BindingFlags.Public | BindingFlags.Instance);
                            if (appWinProp != null)
                            {
                                var appWin = appWinProp.GetValue(targetWindow);
                                if (appWin != null)
                                {
                                    int w = widthVal ?? (int)targetWindow.Bounds.Width;
                                    int h = heightVal ?? (int)targetWindow.Bounds.Height;
                                    int l = leftVal ?? (int)targetWindow.Bounds.X;
                                    int t = topVal ?? (int)targetWindow.Bounds.Y;

                                    if (leftVal.HasValue || topVal.HasValue)
                                    {
                                        var moveAndResizeMethod = appWin.GetType().GetMethod("MoveAndResize", BindingFlags.Public | BindingFlags.Instance);
                                        if (moveAndResizeMethod != null)
                                        {
                                            var paramType = moveAndResizeMethod.GetParameters()[0].ParameterType;
                                            var rectInst = Activator.CreateInstance(paramType, new object[] { l, t, w, h });
                                            if (rectInst != null)
                                            {
                                                moveAndResizeMethod.Invoke(appWin, new object[] { rectInst });
                                                resizedAppWindow = true;
                                            }
                                        }
                                    }
                                    else if (widthVal.HasValue || heightVal.HasValue)
                                    {
                                        var resizeMethod = appWin.GetType().GetMethod("Resize", BindingFlags.Public | BindingFlags.Instance);
                                        if (resizeMethod != null)
                                        {
                                            var paramType = resizeMethod.GetParameters()[0].ParameterType;
                                            var sizeInst = Activator.CreateInstance(paramType, new object[] { w, h });
                                            if (sizeInst != null)
                                            {
                                                resizeMethod.Invoke(appWin, new object[] { sizeInst });
                                                resizedAppWindow = true;
                                            }
                                        }
                                    }
                                }
                            }

                            // Fallback to content width/height if AppWindow was unavailable or unhandled
                            if (!resizedAppWindow && targetWindow.Content is FrameworkElement fe)
                            {
                                if (widthVal.HasValue) fe.Width = widthVal.Value;
                                if (heightVal.HasValue) fe.Height = heightVal.Value;
                            }
                        });
                    }
                    return new JsonObject();
                }

            default:
                return await Chrome.DevTools.Protocol.Domains.BrowserDomain.HandleAsync(session, action, @params);
        }
    }
}
