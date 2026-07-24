using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace WinUI.Diagnostics.Cdp.Domains;

public static class EmulationDomain
{
    private sealed class WindowSizeState
    {
        public double OriginalWidth { get; set; }
        public double OriginalHeight { get; set; }
    }

    private static readonly ConditionalWeakTable<CdpSession, WindowSizeState> _sessionStates = new();

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "setDeviceMetricsOverride":
                {
                    double width = @params["width"]?.GetValue<int>() ?? 0;
                    double height = @params["height"]?.GetValue<int>() ?? 0;

                    if (session.Window != null)
                    {
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
                        {
                            if (session.Window.Content is FrameworkElement fe)
                            {
                                if (!_sessionStates.TryGetValue(session, out _))
                                {
                                    var state = new WindowSizeState
                                    {
                                        OriginalWidth = fe.Width,
                                        OriginalHeight = fe.Height
                                    };
                                    _sessionStates.Add(session, state);
                                }

                                fe.Width = width;
                                fe.Height = height;
                            }
                        });
                    }

                    return new JsonObject();
                }

            case "clearDeviceMetricsOverride":
                {
                    if (session.Window != null)
                    {
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
                        {
                            if (_sessionStates.TryGetValue(session, out var state))
                            {
                                if (session.Window.Content is FrameworkElement fe)
                                {
                                    fe.Width = state.OriginalWidth;
                                    fe.Height = state.OriginalHeight;
                                }
                                _sessionStates.Remove(session);
                            }
                        });
                    }
                    return new JsonObject();
                }

            case "setEmulatedColorSchemeOverride":
                {
                    string colorScheme = @params["colorScheme"]?.GetValue<string>() ?? "";
                    if (session.Window != null)
                    {
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
                        {
                            if (session.Window.Content is FrameworkElement fe)
                            {
                                if (colorScheme.Equals("dark", StringComparison.OrdinalIgnoreCase))
                                {
                                    fe.RequestedTheme = ElementTheme.Dark;
                                }
                                else if (colorScheme.Equals("light", StringComparison.OrdinalIgnoreCase))
                                {
                                    fe.RequestedTheme = ElementTheme.Light;
                                }
                                else
                                {
                                    fe.RequestedTheme = ElementTheme.Default;
                                }
                            }
                        });
                    }
                    return new JsonObject();
                }

            case "setEmulatedMedia":
                {
                    var features = @params["features"] as JsonArray;
                    if (features != null && session.Window != null)
                    {
                        foreach (var featureNode in features)
                        {
                            var name = featureNode?["name"]?.GetValue<string>();
                            var val = featureNode?["value"]?.GetValue<string>();
                            if (name != null && name.Equals("prefers-color-scheme", StringComparison.OrdinalIgnoreCase))
                            {
                                await session.Window.DispatcherQueue.InvokeAsync(() =>
                                {
                                    if (session.Window.Content is FrameworkElement fe)
                                    {
                                        if (val != null && val.Equals("dark", StringComparison.OrdinalIgnoreCase))
                                        {
                                            fe.RequestedTheme = ElementTheme.Dark;
                                        }
                                        else if (val != null && val.Equals("light", StringComparison.OrdinalIgnoreCase))
                                        {
                                            fe.RequestedTheme = ElementTheme.Light;
                                        }
                                        else
                                        {
                                            fe.RequestedTheme = ElementTheme.Default;
                                        }
                                    }
                                });
                            }
                        }
                    }
                    return new JsonObject();
                }

            case "setLocaleOverride":
                {
                    string locale = @params["locale"]?.GetValue<string>() ?? "";
                    if (session.Window != null)
                    {
                        await session.Window.DispatcherQueue.InvokeAsync(() =>
                        {
                            var culture = !string.IsNullOrEmpty(locale)
                                ? new System.Globalization.CultureInfo(locale)
                                : System.Globalization.CultureInfo.InstalledUICulture;

                            System.Globalization.CultureInfo.CurrentCulture = culture;
                            System.Globalization.CultureInfo.CurrentUICulture = culture;
                            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
                            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

                            if (session.Window.Content is FrameworkElement fe)
                            {
                                fe.InvalidateMeasure();
                                fe.InvalidateArrange();
                                fe.UpdateLayout();
                            }
                        });
                    }
                    return new JsonObject();
                }

            default:
                return await Chrome.DevTools.Protocol.Domains.EmulationDomain.HandleAsync(session, action, @params);
        }
    }
}
