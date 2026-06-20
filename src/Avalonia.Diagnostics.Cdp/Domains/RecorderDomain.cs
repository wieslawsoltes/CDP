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
    private readonly EventHandler<PointerEventArgs> _pointerMovedHandler;
    private readonly EventHandler<PointerReleasedEventArgs> _pointerReleasedHandler;
    private readonly EventHandler<RoutedEventArgs> _gotFocusHandler;
    private readonly EventHandler<RoutedEventArgs> _lostFocusHandler;
    private readonly EventHandler<KeyEventArgs> _keyDownHandler;
    private readonly ConcurrentDictionary<TextBox, string> _initialTexts = new();

    private bool _isPointerDown = false;
    private Control? _dragStartControl = null;
    private Point _dragStartPos;
    private bool _isDragging = false;
    private int _clickCount = 1;

    public SessionRecorderState(CdpSession session)
    {
        _session = session;
        _pointerPressedHandler = OnPointerPressed;
        _pointerMovedHandler = OnPointerMoved;
        _pointerReleasedHandler = OnPointerReleased;
        _gotFocusHandler = OnGotFocus;
        _lostFocusHandler = OnLostFocus;
        _keyDownHandler = OnKeyDown;
    }

    public void Attach()
    {
        _session.Window.AddHandler(InputElement.PointerPressedEvent, _pointerPressedHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
        _session.Window.AddHandler(InputElement.PointerMovedEvent, _pointerMovedHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
        _session.Window.AddHandler(InputElement.PointerReleasedEvent, _pointerReleasedHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
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
        _session.Window.RemoveHandler(InputElement.PointerMovedEvent, _pointerMovedHandler);
        _session.Window.RemoveHandler(InputElement.PointerReleasedEvent, _pointerReleasedHandler);
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
            int modifiers = 0;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= 1;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= 2;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= 4;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= 8;

            var step = new JsonObject
            {
                ["type"] = "keydown",
                ["target"] = "main",
                ["key"] = key.ToString(),
                ["modifiers"] = modifiers
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

        _isPointerDown = true;
        _dragStartControl = control;
        _dragStartPos = e.GetPosition(_session.Window);
        _isDragging = false;
        _clickCount = e.ClickCount;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerDown || _dragStartControl == null) return;

        var currentPos = e.GetPosition(_session.Window);
        double dx = currentPos.X - _dragStartPos.X;
        double dy = currentPos.Y - _dragStartPos.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance > 10.0)
        {
            _isDragging = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPointerDown) return;
        _isPointerDown = false;

        var startControl = _dragStartControl;
        _dragStartControl = null;

        if (startControl == null) return;

        var releasePos = e.GetPosition(_session.Window);
        var hitControl = _session.Window.InputHitTest(releasePos) as Control;
        var endControl = hitControl ?? (e.Source as Control);

        if (endControl != null)
        {
            if (endControl == startControl || IsDescendantOf(endControl, startControl))
            {
                var current = endControl as Visual;
                while (current != null)
                {
                    if (current != startControl && !IsDescendantOf(current, startControl))
                    {
                        if (current is Control parentControl)
                        {
                            endControl = parentControl;
                            break;
                        }
                    }
                    current = current.GetVisualParent();
                }
            }
        }
        if (endControl == null) endControl = startControl;

        int modifiers = 0;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= 1;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= 2;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= 4;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= 8;

        if (_isDragging)
        {
            _isDragging = false;
            string sourceSelector = SelectorEngine.GetSelector(startControl);
            string targetSelector = SelectorEngine.GetSelector(endControl);

            var startPos = e.GetPosition(startControl);
            var endPos = e.GetPosition(endControl);

            var step = new JsonObject
            {
                ["type"] = "dragAndDrop",
                ["target"] = "main",
                ["selectors"] = new JsonArray { new JsonArray { sourceSelector } },
                ["targetSelectors"] = new JsonArray { new JsonArray { targetSelector } },
                ["offsetX"] = startPos.X,
                ["offsetY"] = startPos.Y,
                ["targetOffsetX"] = endPos.X,
                ["targetOffsetY"] = endPos.Y,
                ["modifiers"] = modifiers
            };

            _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
            {
                ["step"] = step
            });
        }
        else
        {
            var pos = e.GetPosition(startControl);
            string selector = SelectorEngine.GetSelector(startControl);

            var point = e.GetCurrentPoint(startControl);
            string button = "left";
            if (point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased || 
                point.Properties.IsRightButtonPressed) button = "right";
            else if (point.Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonReleased || 
                     point.Properties.IsMiddleButtonPressed) button = "middle";

            var step = new JsonObject
            {
                ["type"] = "click",
                ["target"] = "main",
                ["selectors"] = new JsonArray { new JsonArray { selector } },
                ["offsetX"] = pos.X,
                ["offsetY"] = pos.Y,
                ["button"] = button,
                ["clickCount"] = _clickCount > 0 ? _clickCount : 1,
                ["modifiers"] = modifiers
            };

            _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
            {
                ["step"] = step
            });
        }
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

    private static bool IsDescendantOf(Visual? visual, Visual target)
    {
        var parent = visual?.GetVisualParent();
        while (parent != null)
        {
            if (parent == target) return true;
            parent = parent.GetVisualParent();
        }
        return false;
    }
}
