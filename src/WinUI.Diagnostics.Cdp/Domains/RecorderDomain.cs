using System;
using System.IO;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace WinUI.Diagnostics.Cdp.Domains;

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
    private readonly PointerEventHandler _pointerPressedHandler;
    private readonly PointerEventHandler _pointerMovedHandler;
    private readonly PointerEventHandler _pointerReleasedHandler;
    private readonly PointerEventHandler _pointerWheelChangedHandler;
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

    private readonly HashSet<UIElement> _attachedElements = new();

    private void EnsureAttachedToTopLevel()
    {
        var windows = CdpServer.GetWindows().ToList();
        foreach (var target in windows)
        {
            var win = target.Window;
            if (win?.Content != null)
            {
                if (_attachedElements.Add(win.Content))
                {
                    win.Content.PointerPressed += _pointerPressedHandler;
                    win.Content.PointerMoved += _pointerMovedHandler;
                    win.Content.PointerReleased += _pointerReleasedHandler;
                    win.Content.PointerWheelChanged += _pointerWheelChangedHandler;
                    win.Content.GotFocus += _gotFocusHandler;
                    win.Content.LostFocus += _lostFocusHandler;
                    win.Content.KeyDown += _keyDownHandler;
                }

                if (win.Content.XamlRoot != null)
                {
                    try
                    {
                        var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(win.Content.XamlRoot);
                        if (popups != null)
                        {
                            foreach (var popup in popups)
                            {
                                if (popup != null && popup.Child is UIElement childUI && _attachedElements.Add(childUI))
                                {
                                    childUI.PointerPressed += _pointerPressedHandler;
                                    childUI.PointerMoved += _pointerMovedHandler;
                                    childUI.PointerReleased += _pointerReleasedHandler;
                                    childUI.PointerWheelChanged += _pointerWheelChangedHandler;
                                    childUI.GotFocus += _gotFocusHandler;
                                    childUI.LostFocus += _lostFocusHandler;
                                    childUI.KeyDown += _keyDownHandler;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
    }

    public void Attach()
    {
        if (_session.Window?.Content == null) return;

        EnsureAttachedToTopLevel();

        // Emit initial viewport size
        _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
        {
            ["step"] = new JsonObject
            {
                ["type"] = "setViewport",
                ["width"] = _session.Window.Bounds.Width,
                ["height"] = _session.Window.Bounds.Height
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
        foreach (var content in _attachedElements)
        {
            if (content != null)
            {
                content.PointerPressed -= _pointerPressedHandler;
                content.PointerMoved -= _pointerMovedHandler;
                content.PointerReleased -= _pointerReleasedHandler;
                content.PointerWheelChanged -= _pointerWheelChangedHandler;
                content.GotFocus -= _gotFocusHandler;
                content.LostFocus -= _lostFocusHandler;
                content.KeyDown -= _keyDownHandler;
            }
        }
        _attachedElements.Clear();
        _initialTexts.Clear();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_session.InspectModeEnabled) return;

        var key = e.Key;
        var step = new JsonObject
        {
            ["type"] = "keydown",
            ["target"] = "main",
            ["key"] = key.ToString(),
            ["modifiers"] = 0
        };

        _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
        {
            ["step"] = step
        });
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_session.InspectModeEnabled || _session.Window?.Content == null) return;
        EnsureAttachedToTopLevel();

        var pos = e.GetCurrentPoint(_session.Window.Content).Position;
        var hitRes = CdpVisualTreeHelper.HitTestAllRoots(_session.Window, pos, "composite");
        var hit = hitRes.Target ?? VisualTreeHelper.FindElementsInHostCoordinates(pos, _session.Window.Content).FirstOrDefault();
        if (hit == null) return;

        var logical = _session.FindLogicalNode(hit);
        var control = (logical as FrameworkElement) ?? (hit as FrameworkElement);
        if (control == null) return;

        _isPointerDown = true;
        _dragStartControl = control;
        _dragStartPos = pos;
        _dragStartLocalPos = e.GetCurrentPoint(control).Position;
        _isDragging = false;
        _clickCount = 1; // WinUI pointer events don't expose click count directly in standard arguments
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerDown || _dragStartControl == null || _session.Window?.Content == null) return;

        var currentPos = e.GetCurrentPoint(_session.Window.Content).Position;
        double dx = currentPos.X - _dragStartPos.X;
        double dy = currentPos.Y - _dragStartPos.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance > 10.0)
        {
            _isDragging = true;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerDown || _session.Window?.Content == null) return;
        _isPointerDown = false;

        var startControl = _dragStartControl;
        _dragStartControl = null;

        if (startControl == null) return;

        var releasePos = e.GetCurrentPoint(_session.Window.Content).Position;
        var elements = VisualTreeHelper.FindElementsInHostCoordinates(releasePos, _session.Window.Content);
        var endVisual = elements.FirstOrDefault();
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
                var current = endControl as DependencyObject;
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
                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }
        if (endControl == null) endControl = startControl;

        if (_isDragging)
        {
            _isDragging = false;
            string sourceSelector = SelectorEngine.GetSelector(startControl, useAutomation: _useAutomation);
            string targetSelector = SelectorEngine.GetSelector(endControl, useAutomation: _useAutomation);

            var endPos = e.GetCurrentPoint(endControl).Position;

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
                ["modifiers"] = 0
            };

            _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
            {
                ["step"] = step
            });
        }
        else
        {
            var pos = e.GetCurrentPoint(startControl).Position;
            string selector = SelectorEngine.GetSelector(startControl, useAutomation: _useAutomation);

            var step = new JsonObject
            {
                ["type"] = "click",
                ["target"] = "main",
                ["selectors"] = new JsonArray { new JsonArray { selector } },
                ["offsetX"] = pos.X,
                ["offsetY"] = pos.Y,
                ["button"] = "left",
                ["clickCount"] = _clickCount > 0 ? _clickCount : 1,
                ["modifiers"] = 0
            };

            _ = _session.SendEventAsync("Recorder.stepAdded", new JsonObject
            {
                ["step"] = step
            });
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_session.InspectModeEnabled || _session.Window?.Content == null) return;

        var pos = e.GetCurrentPoint(_session.Window.Content).Position;
        var elements = VisualTreeHelper.FindElementsInHostCoordinates(pos, _session.Window.Content);
        var hit = elements.FirstOrDefault();
        if (hit == null) return;

        var logical = _session.FindLogicalNode(hit);
        var control = (logical as FrameworkElement) ?? (hit as FrameworkElement);
        if (control == null) return;

        string selector = SelectorEngine.GetSelector(control, useAutomation: _useAutomation);
        var point = e.GetCurrentPoint(control);
        var delta = point.Properties.MouseWheelDelta;

        if (delta != 0)
        {
            var step = new JsonObject
            {
                ["type"] = "scroll",
                ["target"] = "main",
                ["selectors"] = new JsonArray { new JsonArray { selector } },
                ["deltaX"] = 0,
                ["deltaY"] = -delta
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
        if (source is DependencyObject visual)
        {
            var current = VisualTreeHelper.GetParent(visual);
            while (current != null)
            {
                if (current is TextBox textBox) return textBox;
                current = VisualTreeHelper.GetParent(current);
            }
        }
        return null;
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (_session.InspectModeEnabled) return;

        var textBox = FindTextBox(e.OriginalSource);
        if (textBox != null)
        {
            _initialTexts[textBox] = textBox.Text ?? "";
        }
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (_session.InspectModeEnabled) return;

        var textBox = FindTextBox(e.OriginalSource);
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

    private static bool IsDescendantOf(DependencyObject? visual, DependencyObject target)
    {
        var parent = VisualTreeHelper.GetParent(visual);
        while (parent != null)
        {
            if (parent == target) return true;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return false;
    }
}
