using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;

namespace Wpf.Diagnostics.Cdp.Domains;

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
                        await session.Window.Dispatcher.InvokeAsync(() =>
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
                        await session.Window.Dispatcher.InvokeAsync(() =>
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
                    return new JsonObject();
                }

            case "setEmulatedMedia":
                {
                    return new JsonObject();
                }

            case "setLocaleOverride":
                {
                    string locale = @params["locale"]?.GetValue<string>() ?? "";
                    if (session.Window != null)
                    {
                        await session.Window.Dispatcher.InvokeAsync(() =>
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
