using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Avalonia.Diagnostics.Cdp.Domains;

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
                        ["product"] = "Avalonia/11.3.12",
                        ["revision"] = "1.0",
                        ["userAgent"] = "Mozilla/5.0 (Avalonia UI)",
                        ["jsVersion"] = ".NET 10.0"
                    };
                }

            case "close":
                {
                    if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                    return new JsonObject();
                }

            case "getWindowForTarget":
                {
                    return new JsonObject
                    {
                        ["windowId"] = 1
                    };
                }

            case "getWindowBounds":
                {
                    double left = 0, top = 0, width = 800, height = 600;
                    string windowState = "normal";

                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (session.Window is Window win)
                        {
                            left = win.Position.X;
                            top = win.Position.Y;
                            width = win.Width;
                            height = win.Height;
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
                    var boundsNode = @params["bounds"] as JsonObject;
                    if (boundsNode != null)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (session.Window is Window win)
                            {
                                int? leftVal = boundsNode["left"]?.GetValue<int>();
                                int? topVal = boundsNode["top"]?.GetValue<int>();
                                int? widthVal = boundsNode["width"]?.GetValue<int>();
                                int? heightVal = boundsNode["height"]?.GetValue<int>();
                                string? stateVal = boundsNode["windowState"]?.GetValue<string>();

                                if (leftVal.HasValue && topVal.HasValue)
                                {
                                    win.Position = new PixelPoint(leftVal.Value, topVal.Value);
                                }
                                if (widthVal.HasValue) win.Width = widthVal.Value;
                                if (heightVal.HasValue) win.Height = heightVal.Value;

                                if (stateVal != null)
                                {
                                    win.WindowState = stateVal switch
                                    {
                                        "maximized" => WindowState.Maximized,
                                        "minimized" => WindowState.Minimized,
                                        "fullscreen" => WindowState.FullScreen,
                                        _ => WindowState.Normal
                                    };
                                }
                            }
                        });
                    }
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Browser.{action} is not implemented");
        }
    }
}
