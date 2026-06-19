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
    private static readonly IInputDevice s_touchDevice =
        (IInputDevice)Activator.CreateInstance(typeof(TouchDevice), nonPublic: true)!;

    private static RawTouchEventArgs CreateRawTouchEventArgs(
        IInputDevice device,
        ulong timestamp,
        IInputRoot root,
        RawPointerEventType type,
        Point position,
        RawInputModifiers modifiers,
        long touchPointId)
    {
        var ctor = typeof(RawTouchEventArgs).GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(IInputDevice), typeof(ulong), typeof(IInputRoot), typeof(RawPointerEventType), typeof(Point), typeof(RawInputModifiers), typeof(long) },
            null
        );
        if (ctor == null)
        {
            throw new Exception("Could not find RawTouchEventArgs constructor.");
        }
        return (RawTouchEventArgs)ctor.Invoke(new object[] { device, timestamp, root, type, position, modifiers, touchPointId });
    }

    private static RawKeyEventArgs CreateRawKeyEventArgs(
        IInputDevice device,
        ulong timestamp,
        IInputRoot root,
        RawKeyEventType type,
        Key key,
        RawInputModifiers modifiers)
    {
        var ctor9 = typeof(RawKeyEventArgs).GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[]
            {
                typeof(IInputDevice),
                typeof(ulong),
                typeof(IInputRoot),
                typeof(RawKeyEventType),
                typeof(Key),
                typeof(RawInputModifiers),
                typeof(PhysicalKey),
                typeof(string),
                typeof(KeyDeviceType)
            },
            null
        );

        if (ctor9 != null)
        {
            return (RawKeyEventArgs)ctor9.Invoke(new object[]
            {
                device,
                timestamp,
                root,
                type,
                key,
                modifiers,
                PhysicalKey.None,
                "",
                KeyDeviceType.Keyboard
            });
        }

        var ctor6 = typeof(RawKeyEventArgs).GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[]
            {
                typeof(IInputDevice),
                typeof(ulong),
                typeof(IInputRoot),
                typeof(RawKeyEventType),
                typeof(Key),
                typeof(RawInputModifiers)
            },
            null
        );

        if (ctor6 != null)
        {
            return (RawKeyEventArgs)ctor6.Invoke(new object[]
            {
                device,
                timestamp,
                root,
                type,
                key,
                modifiers
            });
        }

        throw new Exception("Could not find suitable RawKeyEventArgs constructor.");
    }

    private static double GetDoubleOrDefault(System.Text.Json.Nodes.JsonNode? node, double defaultValue)
    {
        if (node == null) return defaultValue;
        try
        {
            return node.GetValue<double>();
        }
        catch
        {
            try
            {
                return node.GetValue<int>();
            }
            catch
            {
                return defaultValue;
            }
        }
    }

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
                    int modifiersRaw = @params["modifiers"]?.GetValue<int>() ?? 0;

                    await DispatchMouseEventAsync(session, type, x, y, button, deltaX, deltaY, modifiersRaw);
                    return new JsonObject();
                }

            case "dispatchKeyEvent":
                {
                    string type = @params["type"]?.GetValue<string>() ?? "";
                    string keyStr = @params["key"]?.GetValue<string>() ?? "";
                    string codeStr = @params["code"]?.GetValue<string>() ?? "";
                    string text = @params["text"]?.GetValue<string>() ?? "";
                    int modifiersRaw = @params["modifiers"]?.GetValue<int>() ?? 0;

                    await DispatchKeyEventAsync(session, type, keyStr, codeStr, text, modifiersRaw);
                    return new JsonObject();
                }

            case "insertText":
                {
                    string text = @params["text"]?.GetValue<string>() ?? "";
                    await DispatchTextInputAsync(session, text);
                    return new JsonObject();
                }

            case "emulateTouchFromMouseEvent":
                {
                    string type = @params["type"]?.GetValue<string>() ?? "";
                    double x = GetDoubleOrDefault(@params["x"], 0);
                    double y = GetDoubleOrDefault(@params["y"], 0);
                    string button = @params["button"]?.GetValue<string>() ?? "none";
                    double deltaX = GetDoubleOrDefault(@params["deltaX"], 0);
                    double deltaY = GetDoubleOrDefault(@params["deltaY"], 0);
                    int modifiersRaw = @params["modifiers"]?.GetValue<int>() ?? 0;
                    int clickCount = @params["clickCount"]?.GetValue<int>() ?? 0;

                    await EmulateTouchFromMouseEventAsync(session, type, x, y, button, deltaX, deltaY, modifiersRaw, clickCount);
                    return new JsonObject();
                }

            case "synthesizeTapGesture":
                {
                    double x = GetDoubleOrDefault(@params["x"], 0);
                    double y = GetDoubleOrDefault(@params["y"], 0);
                    int tapCount = @params["tapCount"]?.GetValue<int>() ?? 1;
                    int duration = @params["duration"]?.GetValue<int>() ?? 50;
                    string gestureSourceType = @params["gestureSourceType"]?.GetValue<string>() ?? "default";

                    await SynthesizeTapGestureAsync(session, x, y, tapCount, duration, gestureSourceType);
                    return new JsonObject();
                }

            case "synthesizeScrollGesture":
                {
                    double x = GetDoubleOrDefault(@params["x"], 0);
                    double y = GetDoubleOrDefault(@params["y"], 0);
                    double xDistance = GetDoubleOrDefault(@params["xDistance"], 0);
                    double yDistance = GetDoubleOrDefault(@params["yDistance"], 0);
                    double speed = GetDoubleOrDefault(@params["speed"], 800);
                    string gestureSourceType = @params["gestureSourceType"]?.GetValue<string>() ?? "default";

                    await SynthesizeScrollGestureAsync(session, x, y, xDistance, yDistance, speed, gestureSourceType);
                    return new JsonObject();
                }

            case "synthesizePinchGesture":
                {
                    double x = GetDoubleOrDefault(@params["x"], 0);
                    double y = GetDoubleOrDefault(@params["y"], 0);
                    double scaleFactor = GetDoubleOrDefault(@params["scaleFactor"], 1.0);
                    double speed = GetDoubleOrDefault(@params["relativeSpeed"], 800);
                    string gestureSourceType = @params["gestureSourceType"]?.GetValue<string>() ?? "default";

                    await SynthesizePinchGestureAsync(session, x, y, scaleFactor, speed, gestureSourceType);
                    return new JsonObject();
                }

            case "setIgnoreInputEvents":
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
        if (session.TouchEmulationEnabled)
        {
            await EmulateTouchFromMouseEventAsync(session, type, x, y, button, deltaX, deltaY, modifiersRaw, 1);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var position = new Point(x, y);
            var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var inputHandler = GetInputHandler(session.Window);
            var mouseDevice = GetMouseDevice();
            var inputRoot = typeof(TopLevel)
                .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(session.Window) as IInputRoot;
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
        session.RequestScreencastFrame();
    }

    private static async Task EmulateTouchFromMouseEventAsync(
        CdpSession session,
        string type,
        double x,
        double y,
        string button,
        double deltaX,
        double deltaY,
        int modifiersRaw,
        int clickCount)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var position = new Point(x, y);
            var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var inputHandler = GetInputHandler(session.Window);
            var inputRoot = typeof(TopLevel)
                .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(session.Window) as IInputRoot;
            if (inputHandler == null || inputRoot == null) return;

            var modifiers = RawInputModifiers.None;
            if ((modifiersRaw & 1) != 0) modifiers |= RawInputModifiers.Alt;
            if ((modifiersRaw & 2) != 0) modifiers |= RawInputModifiers.Control;
            if ((modifiersRaw & 4) != 0) modifiers |= RawInputModifiers.Shift;
            if ((modifiersRaw & 8) != 0) modifiers |= RawInputModifiers.Meta;

            if (type == "mouseMoved" && (button == "none" || string.IsNullOrEmpty(button)))
            {
                return;
            }

            if (type == "mouseWheel")
            {
                var mouseDevice = GetMouseDevice();
                if (mouseDevice != null)
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
                }
                return;
            }

            RawPointerEventType eventType = type switch
            {
                "mousePressed" => RawPointerEventType.TouchBegin,
                "mouseReleased" => RawPointerEventType.TouchEnd,
                "mouseMoved" => RawPointerEventType.TouchUpdate,
                _ => RawPointerEventType.TouchUpdate
            };

            var args = CreateRawTouchEventArgs(
                s_touchDevice,
                timestamp,
                inputRoot,
                eventType,
                position,
                modifiers,
                1
            );
            inputHandler.Invoke(args);
        });
        session.RequestScreencastFrame();
    }

    private static async Task SynthesizeTapGestureAsync(
        CdpSession session,
        double x,
        double y,
        int tapCount,
        int duration,
        string gestureSourceType)
    {
        bool useTouch = gestureSourceType == "touch" || gestureSourceType == "default";

        for (int i = 0; i < tapCount; i++)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var inputHandler = GetInputHandler(session.Window);
                var mouseDevice = GetMouseDevice();
                var inputRoot = typeof(TopLevel)
                    .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.GetValue(session.Window) as IInputRoot;

                if (inputHandler == null || inputRoot == null) return;

                var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (useTouch)
                {
                    var touchDown = CreateRawTouchEventArgs(
                        s_touchDevice,
                        timestamp,
                        inputRoot,
                        RawPointerEventType.TouchBegin,
                        new Point(x, y),
                        RawInputModifiers.None,
                        1
                    );
                    inputHandler.Invoke(touchDown);
                }
                else
                {
                    if (mouseDevice == null) return;
                    var mouseDown = (RawPointerEventArgs)Activator.CreateInstance(
                        typeof(RawPointerEventArgs),
                        mouseDevice,
                        timestamp,
                        inputRoot,
                        RawPointerEventType.LeftButtonDown,
                        new Point(x, y),
                        RawInputModifiers.LeftMouseButton
                    )!;
                    inputHandler.Invoke(mouseDown);
                }
            });

            await Task.Delay(duration);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var inputHandler = GetInputHandler(session.Window);
                var mouseDevice = GetMouseDevice();
                var inputRoot = typeof(TopLevel)
                    .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.GetValue(session.Window) as IInputRoot;

                if (inputHandler == null || inputRoot == null) return;

                var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (useTouch)
                {
                    var touchUp = CreateRawTouchEventArgs(
                        s_touchDevice,
                        timestamp,
                        inputRoot,
                        RawPointerEventType.TouchEnd,
                        new Point(x, y),
                        RawInputModifiers.None,
                        1
                    );
                    inputHandler.Invoke(touchUp);
                }
                else
                {
                    if (mouseDevice == null) return;
                    var mouseUp = (RawPointerEventArgs)Activator.CreateInstance(
                        typeof(RawPointerEventArgs),
                        mouseDevice,
                        timestamp,
                        inputRoot,
                        RawPointerEventType.LeftButtonUp,
                        new Point(x, y),
                        RawInputModifiers.None
                    )!;
                    inputHandler.Invoke(mouseUp);
                }
            });

            if (i < tapCount - 1)
            {
                await Task.Delay(100);
            }
        }
        session.RequestScreencastFrame();
    }

    private static async Task SynthesizeScrollGestureAsync(
        CdpSession session,
        double x,
        double y,
        double xDistance,
        double yDistance,
        double speed,
        string gestureSourceType)
    {
        bool useTouch = gestureSourceType == "touch" || gestureSourceType == "default";

        if (useTouch)
        {
            double d = Math.Sqrt(xDistance * xDistance + yDistance * yDistance);
            double effectiveSpeed = speed <= 0 ? 800 : speed;
            double durationSec = d / effectiveSpeed;
            int durationMs = Math.Max(100, Math.Min(1000, (int)(durationSec * 1000)));
            int steps = Math.Max(5, durationMs / 16);
            int stepDelay = durationMs / steps;

            // Touch Begin
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var inputHandler = GetInputHandler(session.Window);
                var inputRoot = typeof(TopLevel)
                    .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.GetValue(session.Window) as IInputRoot;

                if (inputHandler == null || inputRoot == null) return;

                var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var touchDown = CreateRawTouchEventArgs(
                    s_touchDevice,
                    timestamp,
                    inputRoot,
                    RawPointerEventType.TouchBegin,
                    new Point(x, y),
                    RawInputModifiers.None,
                    1
                );
                inputHandler.Invoke(touchDown);
            });

            await Task.Delay(stepDelay);

            // Interpolate Touch Updates
            for (int i = 1; i < steps; i++)
            {
                double progress = (double)i / steps;
                double px = x - (xDistance * progress);
                double py = y - (yDistance * progress);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var inputHandler = GetInputHandler(session.Window);
                    var inputRoot = typeof(TopLevel)
                        .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        ?.GetValue(session.Window) as IInputRoot;

                    if (inputHandler == null || inputRoot == null) return;

                    var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var touchMove = CreateRawTouchEventArgs(
                        s_touchDevice,
                        timestamp,
                        inputRoot,
                        RawPointerEventType.TouchUpdate,
                        new Point(px, py),
                        RawInputModifiers.None,
                        1
                    );
                    inputHandler.Invoke(touchMove);
                });

                await Task.Delay(stepDelay);
            }

            // Touch End
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var inputHandler = GetInputHandler(session.Window);
                var inputRoot = typeof(TopLevel)
                    .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.GetValue(session.Window) as IInputRoot;

                if (inputHandler == null || inputRoot == null) return;

                var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var touchUp = CreateRawTouchEventArgs(
                    s_touchDevice,
                    timestamp,
                    inputRoot,
                    RawPointerEventType.TouchEnd,
                    new Point(x - xDistance, y - yDistance),
                    RawInputModifiers.None,
                    1
                );
                inputHandler.Invoke(touchUp);
            });
        }
        else
        {
            // Mouse Wheel scroll gesture simulation
            double stepX = xDistance / 5.0;
            double stepY = yDistance / 5.0;

            for (int i = 0; i < 5; i++)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var inputHandler = GetInputHandler(session.Window);
                    var mouseDevice = GetMouseDevice();
                    var inputRoot = typeof(TopLevel)
                        .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        ?.GetValue(session.Window) as IInputRoot;

                    if (inputHandler == null || mouseDevice == null || inputRoot == null) return;

                    var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var wheelArgs = (RawMouseWheelEventArgs)Activator.CreateInstance(
                        typeof(RawMouseWheelEventArgs),
                        mouseDevice,
                        timestamp,
                        inputRoot,
                        new Point(x, y),
                        new Vector(stepX, stepY),
                        RawInputModifiers.None
                    )!;
                    inputHandler.Invoke(wheelArgs);
                });

                await Task.Delay(20);
            }
        }

        session.RequestScreencastFrame();
    }

    private static async Task SynthesizePinchGestureAsync(
        CdpSession session,
        double x,
        double y,
        double scaleFactor,
        double speed,
        string gestureSourceType)
    {
        bool useTouch = gestureSourceType == "touch" || gestureSourceType == "default";
        if (!useTouch)
        {
            return;
        }

        double initialOffset = 50.0;
        double startX1 = x - initialOffset;
        double startX2 = x + initialOffset;
        double endX1 = x - (initialOffset * scaleFactor);
        double endX2 = x + (initialOffset * scaleFactor);

        double totalDistance = Math.Abs(endX1 - startX1) + Math.Abs(endX2 - startX2);
        double effectiveSpeed = speed <= 0 ? 800 : speed;
        double durationSec = totalDistance / effectiveSpeed;
        int durationMs = Math.Max(100, Math.Min(1000, (int)(durationSec * 1000)));
        int steps = Math.Max(5, durationMs / 16);
        int stepDelay = durationMs / steps;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var inputHandler = GetInputHandler(session.Window);
            var inputRoot = typeof(TopLevel)
                .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(session.Window) as IInputRoot;

            if (inputHandler == null || inputRoot == null) return;

            var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var touchDown1 = CreateRawTouchEventArgs(
                s_touchDevice,
                timestamp,
                inputRoot,
                RawPointerEventType.TouchBegin,
                new Point(startX1, y),
                RawInputModifiers.None,
                1
            );
            inputHandler.Invoke(touchDown1);

            var touchDown2 = CreateRawTouchEventArgs(
                s_touchDevice,
                timestamp,
                inputRoot,
                RawPointerEventType.TouchBegin,
                new Point(startX2, y),
                RawInputModifiers.None,
                2
            );
            inputHandler.Invoke(touchDown2);
        });

        await Task.Delay(stepDelay);

        for (int i = 1; i < steps; i++)
        {
            double progress = (double)i / steps;
            double px1 = startX1 + (endX1 - startX1) * progress;
            double px2 = startX2 + (endX2 - startX2) * progress;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var inputHandler = GetInputHandler(session.Window);
                var inputRoot = typeof(TopLevel)
                    .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.GetValue(session.Window) as IInputRoot;

                if (inputHandler == null || inputRoot == null) return;

                var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var touchMove1 = CreateRawTouchEventArgs(
                    s_touchDevice,
                    timestamp,
                    inputRoot,
                    RawPointerEventType.TouchUpdate,
                    new Point(px1, y),
                    RawInputModifiers.None,
                    1
                );
                inputHandler.Invoke(touchMove1);

                var touchMove2 = CreateRawTouchEventArgs(
                    s_touchDevice,
                    timestamp,
                    inputRoot,
                    RawPointerEventType.TouchUpdate,
                    new Point(px2, y),
                    RawInputModifiers.None,
                    2
                );
                inputHandler.Invoke(touchMove2);
            });

            await Task.Delay(stepDelay);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var inputHandler = GetInputHandler(session.Window);
            var inputRoot = typeof(TopLevel)
                .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(session.Window) as IInputRoot;

            if (inputHandler == null || inputRoot == null) return;

            var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var touchUp1 = CreateRawTouchEventArgs(
                s_touchDevice,
                timestamp,
                inputRoot,
                RawPointerEventType.TouchEnd,
                new Point(endX1, y),
                RawInputModifiers.None,
                1
            );
            inputHandler.Invoke(touchUp1);

            var touchUp2 = CreateRawTouchEventArgs(
                s_touchDevice,
                timestamp,
                inputRoot,
                RawPointerEventType.TouchEnd,
                new Point(endX2, y),
                RawInputModifiers.None,
                2
            );
            inputHandler.Invoke(touchUp2);
        });

        session.RequestScreencastFrame();
    }

    private static async Task DispatchKeyEventAsync(
        CdpSession session,
        string type,
        string keyStr,
        string codeStr,
        string text,
        int modifiersRaw)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var inputHandler = GetInputHandler(session.Window);
            var keyboardDevice = GetKeyboardDevice();
            var inputRoot = typeof(TopLevel)
                .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(session.Window) as IInputRoot;
            if (inputHandler == null || keyboardDevice == null || inputRoot == null) return;

            var modifiers = RawInputModifiers.None;
            if ((modifiersRaw & 1) != 0) modifiers |= RawInputModifiers.Alt;
            if ((modifiersRaw & 2) != 0) modifiers |= RawInputModifiers.Control;
            if ((modifiersRaw & 4) != 0) modifiers |= RawInputModifiers.Shift;
            if ((modifiersRaw & 8) != 0) modifiers |= RawInputModifiers.Meta;

            var key = MapCdpKey(keyStr, codeStr);

            if (type == "keyDown" || type == "rawKeyDown")
            {
                var keyArgs = CreateRawKeyEventArgs(
                    keyboardDevice,
                    timestamp,
                    inputRoot,
                    RawKeyEventType.KeyDown,
                    key,
                    modifiers
                );
                inputHandler.Invoke(keyArgs);
            }
            else if (type == "char")
            {
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
                var keyArgs = CreateRawKeyEventArgs(
                    keyboardDevice,
                    timestamp,
                    inputRoot,
                    RawKeyEventType.KeyUp,
                    key,
                    modifiers
                );
                inputHandler.Invoke(keyArgs);
            }
        });
        session.RequestScreencastFrame();
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
            var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var inputHandler = GetInputHandler(session.Window);
            var keyboardDevice = GetKeyboardDevice();
            var inputRoot = typeof(TopLevel)
                .GetProperty("InputRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(session.Window) as IInputRoot;

            if (inputHandler == null || keyboardDevice == null || inputRoot == null || string.IsNullOrEmpty(text))
            {
                // Fallback to direct mutation if input infrastructure is missing
                var focusedTextBox = FindFocusedTextBox(session.Window);
                if (focusedTextBox != null && !string.IsNullOrEmpty(text))
                {
                    int start = Math.Min(focusedTextBox.SelectionStart, focusedTextBox.SelectionEnd);
                    int end = Math.Max(focusedTextBox.SelectionStart, focusedTextBox.SelectionEnd);
                    string currentText = focusedTextBox.Text ?? "";
                    if (start >= 0 && end <= currentText.Length)
                    {
                        focusedTextBox.Text = currentText.Substring(0, start) + text + currentText.Substring(end);
                        focusedTextBox.CaretIndex = start + text.Length;
                        focusedTextBox.SelectionStart = focusedTextBox.CaretIndex;
                        focusedTextBox.SelectionEnd = focusedTextBox.CaretIndex;
                    }
                    else
                    {
                        focusedTextBox.Text += text;
                    }
                }
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
        session.RequestScreencastFrame();
    }

    private static Key MapCdpKey(string keyStr, string codeStr)
    {
        if (!string.IsNullOrEmpty(codeStr))
        {
            Key key = MapSingleCdpKeyString(codeStr);
            if (key != Key.None) return key;
        }

        if (!string.IsNullOrEmpty(keyStr))
        {
            return MapSingleCdpKeyString(keyStr);
        }

        return Key.None;
    }

    private static Key MapSingleCdpKeyString(string keyStr)
    {
        if (keyStr.Length == 1 && char.IsDigit(keyStr[0]))
        {
            return (Key)((int)Key.D0 + (keyStr[0] - '0'));
        }

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
            case "insert": return Key.Insert;
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

            // Common symbols mapping by code/key
            case ",":
            case "comma": return Key.OemComma;
            case ".":
            case "period": return Key.OemPeriod;
            case ";":
            case "semicolon": return Key.OemSemicolon;
            case "'":
            case "quote": return Key.OemQuotes;
            case "-":
            case "minus": return Key.OemMinus;
            case "=":
            case "equal": return Key.OemPlus;
            case "[":
            case "bracketleft": return Key.OemOpenBrackets;
            case "]":
            case "bracketright": return Key.OemCloseBrackets;
            case "\\":
            case "backslash": return Key.OemPipe;
            case "/":
            case "slash": return Key.OemQuestion;
            case "`":
            case "backquote": return Key.OemTilde;

            // Modifiers mapping by code/key
            case "shiftleft": return Key.LeftShift;
            case "shiftright": return Key.RightShift;
            case "controlleft": return Key.LeftCtrl;
            case "controlright": return Key.RightCtrl;
            case "altleft": return Key.LeftAlt;
            case "altright": return Key.RightAlt;
            case "metaleft": return Key.LWin;
            case "metaright": return Key.RWin;
        }

        if (Enum.TryParse<Key>(cleaned, true, out var key))
        {
            return key;
        }

        return Key.None;
    }
}
