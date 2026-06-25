using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.Cdp.Domains;

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

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (!_sessionStates.TryGetValue(session, out _))
                        {
                            var state = new WindowSizeState
                            {
                                OriginalWidth = session.Window.Width,
                                OriginalHeight = session.Window.Height
                            };
                            _sessionStates.Add(session, state);
                        }

                        session.Window.Width = width;
                        session.Window.Height = height;
                    });

                    return new JsonObject();
                }

            case "clearDeviceMetricsOverride":
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_sessionStates.TryGetValue(session, out var state))
                        {
                            session.Window.Width = state.OriginalWidth;
                            session.Window.Height = state.OriginalHeight;
                            _sessionStates.Remove(session);
                        }
                    });
                    return new JsonObject();
                }

            case "setEmulatedColorSchemeOverride":
                {
                    string colorScheme = @params["colorScheme"]?.GetValue<string>() ?? "";
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (Application.Current != null)
                        {
                            if (colorScheme.Equals("dark", StringComparison.OrdinalIgnoreCase))
                            {
                                Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                            }
                            else if (colorScheme.Equals("light", StringComparison.OrdinalIgnoreCase))
                            {
                                Application.Current.RequestedThemeVariant = ThemeVariant.Light;
                            }
                            else
                            {
                                Application.Current.RequestedThemeVariant = ThemeVariant.Default;
                            }
                        }
                    });
                    return new JsonObject();
                }

            case "setEmulatedMedia":
                {
                    var features = @params["features"] as JsonArray;
                    if (features != null)
                    {
                        foreach (var featureNode in features)
                        {
                            var name = featureNode?["name"]?.GetValue<string>();
                            var val = featureNode?["value"]?.GetValue<string>();
                            if (name != null && name.Equals("prefers-color-scheme", StringComparison.OrdinalIgnoreCase))
                            {
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    if (Application.Current != null)
                                    {
                                        if (val != null && val.Equals("dark", StringComparison.OrdinalIgnoreCase))
                                        {
                                            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                                        }
                                        else if (val != null && val.Equals("light", StringComparison.OrdinalIgnoreCase))
                                        {
                                            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
                                        }
                                        else
                                        {
                                            Application.Current.RequestedThemeVariant = ThemeVariant.Default;
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
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var culture = !string.IsNullOrEmpty(locale)
                            ? new System.Globalization.CultureInfo(locale)
                            : System.Globalization.CultureInfo.InstalledUICulture;

                        System.Globalization.CultureInfo.CurrentCulture = culture;
                        System.Globalization.CultureInfo.CurrentUICulture = culture;
                        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
                        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

                        foreach (var win in CdpServer.GetWindows())
                        {
                            win.Window.InvalidateVisual();
                        }
                    });
                    return new JsonObject();
                }

            default:
                return await Chrome.DevTools.Protocol.Domains.EmulationDomain.HandleAsync(session, action, @params);
        }
    }
}
