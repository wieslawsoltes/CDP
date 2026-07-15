using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp.Domains;

public static class InputDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "dispatchMouseEvent":
                {
                    string type = @params["type"]?.GetValue<string>() ?? "";
                    double x = GetDoubleOrDefault(@params["x"], 0);
                    double y = GetDoubleOrDefault(@params["y"], 0);
                    string button = @params["button"]?.GetValue<string>() ?? "none";
                    double deltaX = GetDoubleOrDefault(@params["deltaX"], 0);
                    double deltaY = GetDoubleOrDefault(@params["deltaY"], 0);

                    await DispatchMouseEventAsync(session, type, x, y, button, deltaX, deltaY);
                    return new JsonObject();
                }

            case "dispatchKeyEvent":
                {
                    string type = @params["type"]?.GetValue<string>() ?? "";
                    string keyStr = @params["key"]?.GetValue<string>() ?? "";
                    string text = @params["text"]?.GetValue<string>() ?? "";

                    await DispatchKeyEventAsync(session, type, keyStr, text);
                    return new JsonObject();
                }

            case "insertText":
                {
                    string text = @params["text"]?.GetValue<string>() ?? "";
                    await DispatchTextInputAsync(session, text);
                    return new JsonObject();
                }

            case "emulateTouchFromMouseEvent":
            case "synthesizeTapGesture":
            case "synthesizeScrollGesture":
            case "setIgnoreInputEvents":
                return new JsonObject();

            default:
                throw new Exception($"Method Input.{action} is not implemented");
        }
    }

    private static double GetDoubleOrDefault(JsonNode? node, double def)
    {
        if (node == null) return def;
        try { return node.GetValue<double>(); } catch { return def; }
    }

    private static async Task DispatchMouseEventAsync(CdpSession session, string type, double x, double y, string button, double deltaX, double deltaY)
    {
        if (session.Window == null) return;

        var window = session.Window;
        await window.Dispatcher.InvokeAsync(() =>
        {
            var position = new Point(x, y);
            Visual hitRoot = window;

            try
            {
                var screenPoint = window.PointToScreen(position);
                var roots = new List<Visual>();

                var firstWindowTuple = CdpServer.GetWindows().FirstOrDefault();
                var mainWin = firstWindowTuple.Window;
                if (mainWin != null)
                {
                    roots.Add(mainWin);

                    // other active windows
                    foreach (var winInfo in CdpServer.GetWindows())
                    {
                        var win = winInfo.Window;
                        if (win != null && win != mainWin && win.IsVisible)
                        {
                            roots.Add(win);
                        }
                    }

                    // open popup contents
                    var openPopups = new List<Popup>();
                    var visited = new HashSet<Visual>();
                    foreach (var winInfo in CdpServer.GetWindows())
                    {
                        if (winInfo.Window != null)
                        {
                            CdpVisualTreeHelper.FindOpenPopups(winInfo.Window, openPopups, visited);
                        }
                    }
                    foreach (var popup in openPopups)
                    {
                        var content = CdpVisualTreeHelper.GetPopupContent(popup);
                        if (content != null && !roots.Contains(content))
                        {
                            roots.Add(content);
                        }
                    }
                }
                else
                {
                    roots.Add(window);
                }

                for (int i = roots.Count - 1; i >= 0; i--)
                {
                    var root = roots[i];
                    if (root is UIElement uiRoot && uiRoot.IsVisible)
                    {
                        try
                        {
                            var localPoint = uiRoot.PointFromScreen(screenPoint);
                            if (localPoint.X >= 0 && localPoint.X <= uiRoot.RenderSize.Width &&
                                localPoint.Y >= 0 && localPoint.Y <= uiRoot.RenderSize.Height)
                            {
                                hitRoot = uiRoot;
                                position = localPoint;
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            var hit = HitTestElement(hitRoot, position);
            if (hit == null) return;

            var mouseDevice = InputManager.Current.PrimaryMouseDevice;
            var timestamp = Environment.TickCount;

            if (type == "mouseMoved")
            {
                var args = new MouseEventArgs(mouseDevice, timestamp)
                {
                    RoutedEvent = UIElement.MouseMoveEvent,
                    Source = hit
                };
                hit.RaiseEvent(args);
            }
            else if (type == "mousePressed")
            {
                var mouseBtn = GetMouseButton(button);
                var args = new MouseButtonEventArgs(mouseDevice, timestamp, mouseBtn)
                {
                    RoutedEvent = UIElement.MouseDownEvent,
                    Source = hit
                };
                hit.RaiseEvent(args);

                // Try to set focus
                if (hit is UIElement uiElement && uiElement.Focusable)
                {
                    uiElement.Focus();
                }
            }
            else if (type == "mouseReleased")
            {
                var mouseBtn = GetMouseButton(button);
                var args = new MouseButtonEventArgs(mouseDevice, timestamp, mouseBtn)
                {
                    RoutedEvent = UIElement.MouseUpEvent,
                    Source = hit
                };
                hit.RaiseEvent(args);
            }
            else if (type == "mouseWheel")
            {
                var args = new MouseWheelEventArgs(mouseDevice, timestamp, (int)deltaY)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = hit
                };
                hit.RaiseEvent(args);
            }
        });
    }

    private static MouseButton GetMouseButton(string button)
    {
        return button switch
        {
            "left" => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left
        };
    }

    private static Visual? HitTestElement(Visual root, Point point)
    {
        Visual? hit = null;
        VisualTreeHelper.HitTest(root, null, new HitTestResultCallback(result =>
        {
            hit = result.VisualHit as Visual;
            return HitTestResultBehavior.Stop;
        }), new PointHitTestParameters(point));
        return hit;
    }

    private static async Task DispatchKeyEventAsync(CdpSession session, string type, string keyStr, string text)
    {
        if (session.Window == null) return;

        var window = session.Window;
        await window.Dispatcher.InvokeAsync(() =>
        {
            var focused = FocusManager.GetFocusedElement(window) as UIElement ?? window;
            if (focused == null) return;

            if (Enum.TryParse<Key>(keyStr, true, out var key))
            {
                var keyboardDevice = InputManager.Current.PrimaryKeyboardDevice;
                var timestamp = Environment.TickCount;

                if (type == "keyDown" || type == "rawKeyDown")
                {
                    var args = new KeyEventArgs(keyboardDevice, PresentationSource.FromVisual(focused), timestamp, key)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent,
                        Source = focused
                    };
                    focused.RaiseEvent(args);
                }
                else if (type == "keyUp")
                {
                    var args = new KeyEventArgs(keyboardDevice, PresentationSource.FromVisual(focused), timestamp, key)
                    {
                        RoutedEvent = Keyboard.KeyUpEvent,
                        Source = focused
                    };
                    focused.RaiseEvent(args);
                }
            }

            if (!string.IsNullOrEmpty(text) && (type == "keyDown" || type == "rawKeyDown"))
            {
                _ = DispatchTextInputAsync(session, text);
            }
        });
    }

    private static async Task DispatchTextInputAsync(CdpSession session, string text)
    {
        if (session.Window == null) return;

        var window = session.Window;
        await window.Dispatcher.InvokeAsync(() =>
        {
            var focused = FocusManager.GetFocusedElement(window);
            if (focused is TextBox tb)
            {
                tb.Text = tb.Text.Insert(tb.CaretIndex, text);
                tb.CaretIndex += text.Length;
            }
            else if (focused is UIElement ui)
            {
                var textDevice = InputManager.Current.PrimaryKeyboardDevice;
                var composition = new TextComposition(InputManager.Current, ui, text);
                var args = new TextCompositionEventArgs(textDevice, composition)
                {
                    RoutedEvent = TextCompositionManager.TextInputEvent,
                    Source = ui
                };
                ui.RaiseEvent(args);
            }
        });
    }
}
