using System;
using System.IO;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    private readonly EventHandler<PointerPressedEventArgs> _pointerPressedHandler;
    private readonly EventHandler<PointerEventArgs> _pointerMovedHandler;
    private readonly EventHandler<PointerReleasedEventArgs> _pointerReleasedHandler;
    private readonly EventHandler<PointerWheelEventArgs> _pointerWheelChangedHandler;
    private readonly EventHandler<RoutedEventArgs> _gotFocusHandler;
    private readonly EventHandler<RoutedEventArgs> _lostFocusHandler;
    private readonly EventHandler<KeyEventArgs> _keyDownHandler;
    private readonly ConcurrentDictionary<TextBox, string> _initialTexts = new();

    private bool _isPointerDown = false;
    private Control? _dragStartControl = null;
    private Point _dragStartPos;
    private Point _dragStartLocalPos;
    private bool _isDragging = false;
    private int _clickCount = 1;

    private readonly HashSet<TopLevel> _attachedTopLevels = new();

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

    private void EnsureAttachedToTopLevel(TopLevel? topLevel)
    {
        if (topLevel == null || _attachedTopLevels.Contains(topLevel)) return;
        try
        {
            topLevel.AddHandler(InputElement.PointerPressedEvent, _pointerPressedHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
            topLevel.AddHandler(InputElement.PointerMovedEvent, _pointerMovedHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
            topLevel.AddHandler(InputElement.PointerReleasedEvent, _pointerReleasedHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
            topLevel.AddHandler(InputElement.PointerWheelChangedEvent, _pointerWheelChangedHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
            topLevel.AddHandler(InputElement.GotFocusEvent, _gotFocusHandler, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
            topLevel.AddHandler(InputElement.LostFocusEvent, _lostFocusHandler, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
            topLevel.AddHandler(InputElement.KeyDownEvent, _keyDownHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
            _attachedTopLevels.Add(topLevel);
        }
        catch { }
    }

    private void EnsureAttachedToAllTopLevelsAndPopups()
    {
        EnsureAttachedToTopLevel(_session.Window);

        foreach (var target in CdpServer.GetWindows())
        {
            if (target.Window != null)
            {
                EnsureAttachedToTopLevel(target.Window);
            }
        }

        try
        {
            var openPopups = new List<Popup>();
            var visited = new HashSet<Visual>();
            CdpVisualTreeHelper.FindOpenPopups(_session.Window, openPopups, visited);

            foreach (var popup in openPopups)
            {
                var content = CdpVisualTreeHelper.GetPopupContent(popup);
                if (content != null)
                {
                    var topLevel = TopLevel.GetTopLevel(content);
                    if (topLevel != null)
                    {
                        EnsureAttachedToTopLevel(topLevel);
                    }
                }
            }
        }
        catch { }
    }

    public void Attach()
    {
        EnsureAttachedToAllTopLevelsAndPopups();

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
        foreach (var topLevel in _attachedTopLevels.ToList())
        {
            try
            {
                topLevel.RemoveHandler(InputElement.PointerPressedEvent, _pointerPressedHandler);
                topLevel.RemoveHandler(InputElement.PointerMovedEvent, _pointerMovedHandler);
                topLevel.RemoveHandler(InputElement.PointerReleasedEvent, _pointerReleasedHandler);
                topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, _pointerWheelChangedHandler);
                topLevel.RemoveHandler(InputElement.GotFocusEvent, _gotFocusHandler);
                topLevel.RemoveHandler(InputElement.LostFocusEvent, _lostFocusHandler);
                topLevel.RemoveHandler(InputElement.KeyDownEvent, _keyDownHandler);
            }
            catch { }
        }
        _attachedTopLevels.Clear();
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

    private Visual? ResolveHitVisual(object? sender, RoutedEventArgs e, Point position)
    {
        EnsureAttachedToAllTopLevelsAndPopups();
        Visual? visual = null;
        var topLevel = (sender as TopLevel) ?? TopLevel.GetTopLevel(e.Source as Visual);
        if (topLevel != null)
        {
            EnsureAttachedToTopLevel(topLevel);
            visual = topLevel.InputHitTest(position) as Visual;
        }

        if (visual == null)
        {
            visual = e.Source as Visual;
        }

        if (visual == null && _session.Window != null)
        {
            var res = CdpVisualTreeHelper.HitTestAllRoots(_session.Window, position, _session.TargetViewMode);
            visual = res.HitVisual;
            if (visual != null)
            {
                var hitTop = TopLevel.GetTopLevel(visual);
                if (hitTop != null) EnsureAttachedToTopLevel(hitTop);
            }
        }

        return visual;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_session.InspectModeEnabled) return;
        EnsureAttachedToAllTopLevelsAndPopups();

        var topLevel = (sender as TopLevel) ?? TopLevel.GetTopLevel(e.Source as Visual) ?? _session.Window;
        var pos = e.GetPosition(topLevel);
        var visual = ResolveHitVisual(sender, e, pos);
        if (visual == null) return;

        var logical = _session.FindLogicalNode(visual);
        var control = (logical as Control) ?? (visual as Control);
        if (control == null) return;

        _isPointerDown = true;
        _dragStartControl = control;
        _dragStartPos = pos;
        _dragStartLocalPos = e.GetPosition(control);
        _isDragging = false;
        _clickCount = e.ClickCount;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerDown || _dragStartControl == null) return;

        var topLevel = (sender as TopLevel) ?? TopLevel.GetTopLevel(e.Source as Visual) ?? _session.Window;
        var currentPos = e.GetPosition(topLevel);
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

        var topLevel = (sender as TopLevel) ?? TopLevel.GetTopLevel(e.Source as Visual) ?? _session.Window;
        var releasePos = e.GetPosition(topLevel);
        var endVisual = ResolveHitVisual(sender, e, releasePos);
        Control? endControl = null;
        if (endVisual != null)
        {
            var logical = _session.FindLogicalNode(endVisual);
            endControl = (logical as Control) ?? (endVisual as Control);
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

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_session.InspectModeEnabled) return;

        var topLevel = (sender as TopLevel) ?? TopLevel.GetTopLevel(e.Source as Visual) ?? _session.Window;
        var pos = e.GetPosition(topLevel);
        var visual = ResolveHitVisual(sender, e, pos);
        if (visual == null) return;

        var logical = _session.FindLogicalNode(visual);
        var control = (logical as Control) ?? (visual as Control);
        if (control == null) return;

        string selector = SelectorEngine.GetSelector(control, useAutomation: _useAutomation);
        var delta = e.Delta;

        if (delta.X != 0.0 || delta.Y != 0.0)
        {
            var step = new JsonObject
            {
                ["type"] = "scroll",
                ["target"] = "main",
                ["selectors"] = new JsonArray { new JsonArray { selector } },
                ["deltaX"] = delta.X * 100.0,
                ["deltaY"] = delta.Y * 100.0
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
