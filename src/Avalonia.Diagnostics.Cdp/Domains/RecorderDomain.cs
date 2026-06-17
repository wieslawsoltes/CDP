using System;
using System.IO;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class RecorderDomain
{
    private static readonly ConcurrentDictionary<CdpSession, SessionRecorderState> _states = new();

    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "start":
                StartRecording(session);
                return Task.FromResult(new JsonObject());
            case "stop":
                StopRecording(session);
                return Task.FromResult(new JsonObject());
            default:
                throw new Exception($"Method Recorder.{action} is not implemented");
        }
    }

    public static void RemoveSession(CdpSession session)
    {
        StopRecording(session);
    }

    private static void StartRecording(CdpSession session)
    {
        if (_states.ContainsKey(session)) return;

        var state = new SessionRecorderState(session);
        if (_states.TryAdd(session, state))
        {
            state.Attach();
        }
    }

    private static void StopRecording(CdpSession session)
    {
        if (_states.TryRemove(session, out var state))
        {
            state.Detach();
        }
    }
}

internal class SessionRecorderState
{
    private readonly CdpSession _session;
    private readonly EventHandler<PointerPressedEventArgs> _pointerPressedHandler;
    private readonly EventHandler<RoutedEventArgs> _gotFocusHandler;
    private readonly EventHandler<RoutedEventArgs> _lostFocusHandler;
    private readonly EventHandler<KeyEventArgs> _keyDownHandler;
    private readonly ConcurrentDictionary<TextBox, string> _initialTexts = new();

    public SessionRecorderState(CdpSession session)
    {
        _session = session;
        _pointerPressedHandler = OnPointerPressed;
        _gotFocusHandler = OnGotFocus;
        _lostFocusHandler = OnLostFocus;
        _keyDownHandler = OnKeyDown;
    }

    public void Attach()
    {
        _session.Window.AddHandler(InputElement.PointerPressedEvent, _pointerPressedHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
        _session.Window.AddHandler(InputElement.GotFocusEvent, _gotFocusHandler, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
        _session.Window.AddHandler(InputElement.LostFocusEvent, _lostFocusHandler, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
        _session.Window.AddHandler(InputElement.KeyDownEvent, _keyDownHandler, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Emit initial viewport size
        var size = _session.Window.ClientSize;
        _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
        {
            ["step"] = new JsonObject
            {
                ["type"] = "setViewport",
                ["width"] = size.Width,
                ["height"] = size.Height
            }
        });

        // Emit initial navigation step
        _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
        {
            ["step"] = new JsonObject
            {
                ["type"] = "navigate",
                ["url"] = $"http://localhost:{CdpServer.Port}/"
            }
        });
    }

    public void Detach()
    {
        _session.Window.RemoveHandler(InputElement.PointerPressedEvent, _pointerPressedHandler);
        _session.Window.RemoveHandler(InputElement.GotFocusEvent, _gotFocusHandler);
        _session.Window.RemoveHandler(InputElement.LostFocusEvent, _lostFocusHandler);
        _session.Window.RemoveHandler(InputElement.KeyDownEvent, _keyDownHandler);
        _initialTexts.Clear();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_session.InspectModeEnabled) return;

        var key = e.Key;
        if (key == Key.Enter || key == Key.Escape || key == Key.Tab ||
            key == Key.Back || key == Key.Delete ||
            key == Key.Left || key == Key.Right || key == Key.Up || key == Key.Down)
        {
            var step = new JsonObject
            {
                ["type"] = "keydown",
                ["target"] = "main",
                ["key"] = key.ToString()
            };

            _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
            {
                ["step"] = step
            });
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_session.InspectModeEnabled) return;

        var control = e.Source as Control;
        if (control == null) return;

        string selector = SelectorEngine.GetSelector(control);
        var pos = e.GetPosition(control);

        var step = new JsonObject
        {
            ["type"] = "click",
            ["target"] = "main",
            ["selectors"] = new JsonArray { new JsonArray { selector } },
            ["offsetX"] = pos.X,
            ["offsetY"] = pos.Y
        };

        _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
        {
            ["step"] = step
        });
    }

    private static TextBox? FindTextBox(object? source)
    {
        if (source is TextBox tb) return tb;
        if (source is Visual visual)
        {
            Visual? current = visual.GetVisualParent();
            while (current != null)
            {
                if (current is TextBox textBox) return textBox;
                current = current.GetVisualParent();
            }
        }
        return null;
    }

    private void OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (_session.InspectModeEnabled) return;

        var textBox = FindTextBox(e.Source);
        if (textBox != null)
        {
            _initialTexts[textBox] = textBox.Text ?? "";
        }
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_session.InspectModeEnabled) return;

        var textBox = FindTextBox(e.Source);
        if (textBox != null)
        {
            if (_initialTexts.TryRemove(textBox, out var initialText))
            {
                var currentText = textBox.Text ?? "";
                if (currentText != initialText)
                {
                    string selector = SelectorEngine.GetSelector(textBox);
                    var step = new JsonObject
                    {
                        ["type"] = "change",
                        ["target"] = "main",
                        ["selectors"] = new JsonArray { new JsonArray { selector } },
                        ["value"] = currentText
                    };

                    _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
                    {
                        ["step"] = step
                    });
                }
            }
        }
    }
}
