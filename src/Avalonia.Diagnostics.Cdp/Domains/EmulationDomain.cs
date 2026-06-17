using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
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

            default:
                throw new Exception($"Method Emulation.{action} is not implemented");
        }
    }
}
