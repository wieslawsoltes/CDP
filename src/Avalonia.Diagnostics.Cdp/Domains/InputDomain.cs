using System;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class InputDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "dispatchMouseEvent":
                {
                    string type = @params["type"]?.GetValue<string>() ?? "";
                    double x = @params["x"]?.GetValue<double>() ?? 0;
                    double y = @params["y"]?.GetValue<double>() ?? 0;
                    string button = @params["button"]?.GetValue<string>() ?? "none";
                    double deltaX = @params["deltaX"]?.GetValue<double>() ?? 0;
                    double deltaY = @params["deltaY"]?.GetValue<double>() ?? 0;
                    int modifiersRaw = @params["modifiers"]?.GetValue<int>() ?? 0;

                    await DispatchMouseEventAsync(session, type, x, y, button, deltaX, deltaY, modifiersRaw);
                    return new JsonObject();
                }

            case "dispatchKeyEvent":
                {
                    string type = @params["type"]?.GetValue<string>() ?? "";
                    string keyStr = @params["key"]?.GetValue<string>() ?? "";
                    string text = @params["text"]?.GetValue<string>() ?? "";
                    int modifiersRaw = @params["modifiers"]?.GetValue<int>() ?? 0;

                    await DispatchKeyEventAsync(session, type, keyStr, text, modifiersRaw);
                    return new JsonObject();
                }

            case "insertText":
                {
                    string text = @params["text"]?.GetValue<string>() ?? "";
                    await DispatchTextInputAsync(session, text);
                    return new JsonObject();
                }

            case "emulateTouchFromMouseEvent":
            case "setIgnoreInputEvents":
            case "synthesizeTapGesture":
            case "synthesizeScrollGesture":
                {
                    return new JsonObject();
                }

            default:
                throw new Exception($"Method Input.{action} is not implemented");
        }
    }

    private static IInputDevice? GetMouseDevice()
    {
        var prop = typeof(MouseDevice).GetProperty("Primary", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return prop?.GetValue(null) as IInputDevice;
    }

    private static IKeyboardDevice? GetKeyboardDevice()
    {
        var prop = typeof(KeyboardDevice).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return prop?.GetValue(null) as IKeyboardDevice;
    }

    private static Action<RawInputEventArgs>? GetInputHandler(TopLevel window)
    {
        var platformImpl = typeof(TopLevel)
            .GetProperty("PlatformImpl", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.GetValue(window);
        if (platformImpl == null) return null;

        var inputProp = platformImpl.GetType().GetProperty("Input", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return inputProp?.GetValue(platformImpl) as Action<RawInputEventArgs>;
    }

    private static async Task DispatchMouseEventAsync(
        CdpSession session,
        string type,
        double x,
        double y,
        string button,
        double deltaX,
        double deltaY,
        int modifiersRaw)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var position = new Point(x, y);
            var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var inputHandler = GetInputHandler(session.Window);
            var mouseDevice = GetMouseDevice();
            var inputRoot = session.Window as IInputRoot;
            if (inputHandler == null || mouseDevice == null || inputRoot == null) return;

            var modifiers = RawInputModifiers.None;
            if ((modifiersRaw & 1) != 0) modifiers |= RawInputModifiers.Alt;
            if ((modifiersRaw & 2) != 0) modifiers |= RawInputModifiers.Control;
            if ((modifiersRaw & 4) != 0) modifiers |= RawInputModifiers.Shift;
            if ((modifiersRaw & 8) != 0) modifiers |= RawInputModifiers.Meta;

            if (type == "mouseWheel")
            {
                var wheelArgs = (RawMouseWheelEventArgs)Activator.CreateInstance(
                    typeof(RawMouseWheelEventArgs),
                    mouseDevice,
                    timestamp,
                    inputRoot,
                    position,
                    new Vector(deltaX, deltaY),
                    modifiers
                )!;
                inputHandler.Invoke(wheelArgs);
                return;
            }

            var eventType = RawPointerEventType.Move;
            if (type == "mousePressed")
            {
                eventType = button.ToLowerInvariant() switch
                {
                    "left" => RawPointerEventType.LeftButtonDown,
                    "right" => RawPointerEventType.RightButtonDown,
                    "middle" => RawPointerEventType.MiddleButtonDown,
                    _ => RawPointerEventType.LeftButtonDown
                };
            }
            else if (type == "mouseReleased")
            {
                eventType = button.ToLowerInvariant() switch
                {
                    "left" => RawPointerEventType.LeftButtonUp,
                    "right" => RawPointerEventType.RightButtonUp,
                    "middle" => RawPointerEventType.MiddleButtonUp,
                    _ => RawPointerEventType.LeftButtonUp
                };
            }

            if (eventType == RawPointerEventType.LeftButtonDown || (type == "mouseMoved" && button.ToLowerInvariant() == "left")) modifiers |= RawInputModifiers.LeftMouseButton;
            if (eventType == RawPointerEventType.RightButtonDown || (type == "mouseMoved" && button.ToLowerInvariant() == "right")) modifiers |= RawInputModifiers.RightMouseButton;
            if (eventType == RawPointerEventType.MiddleButtonDown || (type == "mouseMoved" && button.ToLowerInvariant() == "middle")) modifiers |= RawInputModifiers.MiddleMouseButton;

            var args = (RawPointerEventArgs)Activator.CreateInstance(
                typeof(RawPointerEventArgs),
                mouseDevice,
                timestamp,
                inputRoot,
                eventType,
                position,
                modifiers
            )!;
            inputHandler.Invoke(args);
        });
    }

    private static async Task DispatchKeyEventAsync(
        CdpSession session,
        string type,
        string keyStr,
        string text,
        int modifiersRaw)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var inputHandler = GetInputHandler(session.Window);
            var keyboardDevice = GetKeyboardDevice();
            var inputRoot = session.Window as IInputRoot;
            if (inputHandler == null || keyboardDevice == null || inputRoot == null) return;

            var modifiers = RawInputModifiers.None;
            if ((modifiersRaw & 1) != 0) modifiers |= RawInputModifiers.Alt;
            if ((modifiersRaw & 2) != 0) modifiers |= RawInputModifiers.Control;
            if ((modifiersRaw & 4) != 0) modifiers |= RawInputModifiers.Shift;
            if ((modifiersRaw & 8) != 0) modifiers |= RawInputModifiers.Meta;

            var key = MapCdpKey(keyStr);

            if (type == "keyDown" || type == "rawKeyDown")
            {
                var keyArgs = (RawKeyEventArgs)Activator.CreateInstance(
                    typeof(RawKeyEventArgs),
                    keyboardDevice,
                    timestamp,
                    inputRoot,
                    RawKeyEventType.KeyDown,
                    key,
                    modifiers
                )!;
                inputHandler.Invoke(keyArgs);

                if (!string.IsNullOrEmpty(text))
                {
                    var textArgs = (RawTextInputEventArgs)Activator.CreateInstance(
                        typeof(RawTextInputEventArgs),
                        keyboardDevice,
                        timestamp,
                        inputRoot,
                        text
                    )!;
                    inputHandler.Invoke(textArgs);
                }
            }
            else if (type == "keyUp")
            {
                var keyArgs = (RawKeyEventArgs)Activator.CreateInstance(
                    typeof(RawKeyEventArgs),
                    keyboardDevice,
                    timestamp,
                    inputRoot,
                    RawKeyEventType.KeyUp,
                    key,
                    modifiers
                )!;
                inputHandler.Invoke(keyArgs);
            }
        });
    }

    private static TextBox? FindFocusedTextBox(Visual parent)
    {
        if (parent is TextBox tb && (tb.IsFocused || tb.IsKeyboardFocusWithin)) return tb;

        foreach (var child in parent.GetVisualChildren())
        {
            var found = FindFocusedTextBox(child);
            if (found != null) return found;
        }
        return null;
    }

    private static async Task DispatchTextInputAsync(CdpSession session, string text)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Direct mutation fallback for headless/background execution
            var focusedTextBox = FindFocusedTextBox(session.Window);
            if (focusedTextBox != null)
            {
                focusedTextBox.Text = text;
                return;
            }

            var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var inputHandler = GetInputHandler(session.Window);
            var keyboardDevice = GetKeyboardDevice();
            var inputRoot = session.Window as IInputRoot;

            if (inputHandler == null || keyboardDevice == null || inputRoot == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            var textArgs = (RawTextInputEventArgs)Activator.CreateInstance(
                typeof(RawTextInputEventArgs),
                keyboardDevice,
                timestamp,
                inputRoot,
                text
            )!;
            inputHandler.Invoke(textArgs);
        });
    }

    private static Key MapCdpKey(string keyStr)
    {
        if (string.IsNullOrEmpty(keyStr)) return Key.None;

        string cleaned = keyStr;
        if (keyStr.StartsWith("Key")) cleaned = keyStr.Substring(3);
        else if (keyStr.StartsWith("Digit")) cleaned = "D" + keyStr.Substring(5);

        switch (cleaned.ToLowerInvariant())
        {
            case "enter": return Key.Enter;
            case "escape": return Key.Escape;
            case "tab": return Key.Tab;
            case "space": return Key.Space;
            case "backspace": return Key.Back;
            case "delete": return Key.Delete;
            case "arrowleft":
            case "left": return Key.Left;
            case "arrowright":
            case "right": return Key.Right;
            case "arrowup":
            case "up": return Key.Up;
            case "arrowdown":
            case "down": return Key.Down;
            case "pageup": return Key.PageUp;
            case "pagedown": return Key.PageDown;
            case "home": return Key.Home;
            case "end": return Key.End;
        }

        if (Enum.TryParse<Key>(cleaned, true, out var key))
        {
            return key;
        }

        return Key.None;
    }
}
