using System;
using System.IO;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp.Domains;

public static class RecorderDomain
{
    private static readonly ConcurrentDictionary<CdpSession, SessionRecorderState> _states = new();

    public static Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "start":
                string? selectorMode = @params["selectorMode"]?.GetValue<string>();
                StartRecording(session, selectorMode);
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

    private static void StartRecording(CdpSession session, string? selectorMode = null)
    {
        if (session.Window == null) return;
        if (_states.ContainsKey(session)) return;

        bool useAutomation = selectorMode == "automation";
        var state = new SessionRecorderState(session, useAutomation);
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
    private readonly bool _useAutomation;
    private readonly MouseButtonEventHandler _pointerPressedHandler;
    private readonly MouseEventHandler _pointerMovedHandler;
    private readonly MouseButtonEventHandler _pointerReleasedHandler;
    private readonly MouseWheelEventHandler _pointerWheelChangedHandler;
    private readonly RoutedEventHandler _gotFocusHandler;
    private readonly RoutedEventHandler _lostFocusHandler;
    private readonly KeyEventHandler _keyDownHandler;
    private readonly ConcurrentDictionary<TextBox, string> _initialTexts = new();

    private bool _isPointerDown = false;
    private FrameworkElement? _dragStartControl = null;
    private Point _dragStartPos;
    private Point _dragStartLocalPos;
    private bool _isDragging = false;
    private int _clickCount = 1;

    public SessionRecorderState(CdpSession session, bool useAutomation = false)
    {
        _session = session;
        _useAutomation = useAutomation;
        _pointerPressedHandler = OnPointerPressed;
        _pointerMovedHandler = OnPointerMoved;
        _pointerReleasedHandler = OnPointerReleased;
        _pointerWheelChangedHandler = OnPointerWheelChanged;
        _gotFocusHandler = OnGotFocus;
        _lostFocusHandler = OnLostFocus;
        _keyDownHandler = OnKeyDown;
    }

    private readonly HashSet<Window> _attachedWindows = new();

    private void EnsureAttachedToTopLevel()
    {
        foreach (var target in CdpServer.GetWindows())
        {
            var win = target.Window;
            if (win != null && _attachedWindows.Add(win))
            {
                win.PreviewMouseDown += _pointerPressedHandler;
                win.PreviewMouseMove += _pointerMovedHandler;
                win.PreviewMouseUp += _pointerReleasedHandler;
                win.PreviewMouseWheel += _pointerWheelChangedHandler;
                win.AddHandler(UIElement.GotFocusEvent, _gotFocusHandler);
                win.AddHandler(UIElement.LostFocusEvent, _lostFocusHandler);
                win.PreviewKeyDown += _keyDownHandler;
            }
        }
    }

    public void Attach()
    {
        if (_session.Window == null) return;

        EnsureAttachedToTopLevel();

        // Emit initial viewport size
        _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
        {
            ["step"] = new JsonObject
            {
                ["type"] = "setViewport",
                ["width"] = _session.Window.ActualWidth,
                ["height"] = _session.Window.ActualHeight
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
        foreach (var win in _attachedWindows)
        {
            if (win != null)
            {
                win.PreviewMouseDown -= _pointerPressedHandler;
                win.PreviewMouseMove -= _pointerMovedHandler;
                win.PreviewMouseUp -= _pointerReleasedHandler;
                win.PreviewMouseWheel -= _pointerWheelChangedHandler;
                win.RemoveHandler(UIElement.GotFocusEvent, _gotFocusHandler);
                win.RemoveHandler(UIElement.LostFocusEvent, _lostFocusHandler);
                win.PreviewKeyDown -= _keyDownHandler;
            }
        }
        _attachedWindows.Clear();
        _initialTexts.Clear();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_session.InspectModeEnabled) return;

        var key = e.Key;
        if (key == Key.Enter || key == Key.Escape || key == Key.Tab ||
            key == Key.Back || key == Key.Delete ||
            key == Key.Left || key == Key.Right || key == Key.Up || key == Key.Down)
        {
            int modifiers = 0;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 1;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 2;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= 4;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 8;

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

    private void OnPointerPressed(object sender, MouseButtonEventArgs e)
    {
        if (_session.InspectModeEnabled || _session.Window == null) return;
        EnsureAttachedToTopLevel();

        var pos = e.GetPosition(_session.Window);
        var hitRes = CdpVisualTreeHelper.HitTestAllRoots(_session.Window, pos, "composite");
        var visual = hitRes.Target ?? HitTestElement(_session.Window, pos) ?? (e.Source as Visual);
        if (visual == null) return;

        var logical = _session.FindLogicalNode(visual);
        var control = (logical as FrameworkElement) ?? (visual as FrameworkElement);
        if (control == null) return;

        _isPointerDown = true;
        _dragStartControl = control;
        _dragStartPos = e.GetPosition(_session.Window);
        _dragStartLocalPos = e.GetPosition(control);
        _isDragging = false;
        _clickCount = e.ClickCount;
    }

    private void OnPointerMoved(object sender, MouseEventArgs e)
    {
        if (!_isPointerDown || _dragStartControl == null || _session.Window == null) return;

        var currentPos = e.GetPosition(_session.Window);
        double dx = currentPos.X - _dragStartPos.X;
        double dy = currentPos.Y - _dragStartPos.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance > 10.0)
        {
            _isDragging = true;
        }
    }

    private void OnPointerReleased(object sender, MouseButtonEventArgs e)
    {
        if (!_isPointerDown || _session.Window == null) return;
        _isPointerDown = false;

        var startControl = _dragStartControl;
        _dragStartControl = null;

        if (startControl == null) return;

        var releasePos = e.GetPosition(_session.Window);
        var hit = HitTestElement(_session.Window, releasePos);
        var endVisual = hit ?? (e.Source as Visual);
        FrameworkElement? endControl = null;
        if (endVisual != null)
        {
            var logical = _session.FindLogicalNode(endVisual);
            endControl = (logical as FrameworkElement) ?? (endVisual as FrameworkElement);
        }

        if (endControl != null)
        {
            if (endControl == startControl || IsDescendantOf(endControl, startControl))
            {
                var current = endControl as Visual;
                while (current != null)
                {
                    if (current != startControl && !IsDescendantOf(current, startControl))
                    {
                        if (current is FrameworkElement parentControl)
                        {
                            endControl = parentControl;
                            break;
                        }
                    }
                    current = VisualTreeHelper.GetParent(current) as Visual;
                }
            }
        }
        if (endControl == null) endControl = startControl;

        int modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 1;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 2;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= 4;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 8;

        if (_isDragging)
        {
            _isDragging = false;
            string sourceSelector = SelectorEngine.GetSelector(startControl, useAutomation: _useAutomation);
            string targetSelector = SelectorEngine.GetSelector(endControl, useAutomation: _useAutomation);

            var endPos = e.GetPosition(endControl);

            var step = new JsonObject
            {
                ["type"] = "dragAndDrop",
                ["target"] = "main",
                ["selectors"] = new JsonArray { new JsonArray { sourceSelector } },
                ["targetSelectors"] = new JsonArray { new JsonArray { targetSelector } },
                ["offsetX"] = _dragStartLocalPos.X,
                ["offsetY"] = _dragStartLocalPos.Y,
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
            string selector = SelectorEngine.GetSelector(startControl, useAutomation: _useAutomation);

            string button = e.ChangedButton switch
            {
                MouseButton.Left => "left",
                MouseButton.Right => "right",
                MouseButton.Middle => "middle",
                _ => "left"
            };

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

    private void OnPointerWheelChanged(object sender, MouseWheelEventArgs e)
    {
        if (_session.InspectModeEnabled || _session.Window == null) return;

        var hit = HitTestElement(_session.Window, e.GetPosition(_session.Window));
        var visual = hit ?? (e.Source as Visual);
        if (visual == null) return;

        var logical = _session.FindLogicalNode(visual);
        var control = (logical as FrameworkElement) ?? (visual as FrameworkElement);
        if (control == null) return;

        string selector = SelectorEngine.GetSelector(control, useAutomation: _useAutomation);
        var delta = e.Delta;

        if (delta != 0)
        {
            var step = new JsonObject
            {
                ["type"] = "scroll",
                ["target"] = "main",
                ["selectors"] = new JsonArray { new JsonArray { selector } },
                ["deltaX"] = 0,
                ["deltaY"] = -delta // invert delta for browser-style direction representation
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
            var current = VisualTreeHelper.GetParent(visual) as Visual;
            while (current != null)
            {
                if (current is TextBox textBox) return textBox;
                current = VisualTreeHelper.GetParent(current) as Visual;
            }
        }
        return null;
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (_session.InspectModeEnabled) return;

        var textBox = FindTextBox(e.Source);
        if (textBox != null)
        {
            _initialTexts[textBox] = textBox.Text ?? "";
        }
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
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
                    string selector = SelectorEngine.GetSelector(textBox, useAutomation: _useAutomation);
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

    private static Visual? HitTestElement(Window window, Point point)
    {
        Visual? hit = null;
        VisualTreeHelper.HitTest(window, null, new HitTestResultCallback(result =>
        {
            hit = result.VisualHit as Visual;
            return HitTestResultBehavior.Stop;
        }), new PointHitTestParameters(point));
        return hit;
    }

    private static bool IsDescendantOf(Visual? visual, Visual target)
    {
        var parent = VisualTreeHelper.GetParent(visual) as Visual;
        while (parent != null)
        {
            if (parent == target) return true;
            parent = VisualTreeHelper.GetParent(parent) as Visual;
        }
        return false;
    }
}
