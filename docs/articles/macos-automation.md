---
title: macOS Automation
---

# macOS Automation

The macOS backend for `Chrome.DevTools.Automation.OS` uses native Apple accessibility and graphics APIs to discover, inspect, and interact with any desktop application. It maps macOS accessibility tree elements to CDP DOM nodes, enabling the full Chrome DevTools Protocol workflow for non-Avalonia applications.

## Architecture

| CDP Method | Backend API | Output |
| :--- | :--- | :--- |
| `DOM.getDocument` | → macOS Backend → AXUIElement API | Accessibility Tree |
| `Input.dispatchMouseEvent` | → CGEvent API | System Mouse/Keyboard |
| `Page.captureScreenshot` | → CGWindowListCreateImage | Window Bitmap |

The macOS backend uses three native API families:

1. **Accessibility API** (`AXUIElement`) — Tree traversal and element inspection
2. **CoreGraphics** (`CGEvent`) — Mouse and keyboard input injection
3. **CoreGraphics** (`CGWindowListCreateImage`) — Screenshot capture

## Accessibility API (AXUIElement)

### Tree Discovery

The backend starts by obtaining the target application's accessibility element:

```csharp
[LibraryImport("ApplicationServices.framework/ApplicationServices")]
private static partial nint AXUIElementCreateApplication(int pid);
```

From the application element, it recursively traverses the accessibility tree using `AXUIElementCopyAttributeValue`:

```csharp
[LibraryImport("ApplicationServices.framework/ApplicationServices")]
private static partial int AXUIElementCopyAttributeValue(
    nint element,
    nint attribute,
    out nint value);
```

### Element Attributes

Each accessibility element exposes properties mapped to CDP node attributes:

| AX Attribute | CDP Mapping | Description |
|---|---|---|
| `AXRole` | `nodeName` | Element type (e.g., `AXButton`, `AXTextField`) |
| `AXTitle` | `[Text]` attribute | Button label, window title |
| `AXValue` | `[value]` attribute | Text field content, checkbox state |
| `AXDescription` | `[description]` attribute | Accessibility description |
| `AXIdentifier` | `[id]` / `[AutomationId]` | Unique element identifier |
| `AXPosition` | Box model position | Screen coordinates (x, y) |
| `AXSize` | Box model size | Dimensions (width, height) |
| `AXEnabled` | `[IsEnabled]` | Interactive state |
| `AXFocused` | `[IsFocused]` | Focus state |
| `AXChildren` | Child nodes | Subtree elements |

### Selector Mapping

CSS selectors are mapped to accessibility element lookups:

```
#myButton          → Find element where AXIdentifier == "myButton"
Button             → Find elements where AXRole == "AXButton"
[Text="Submit"]    → Find elements where AXTitle == "Submit"
[value="checked"]  → Find elements where AXValue == "checked"
```

## Input Injection (CGEvent)

### Mouse Events

Mouse events are injected using CoreGraphics event APIs:

```csharp
[LibraryImport("CoreGraphics.framework/CoreGraphics")]
private static partial nint CGEventCreateMouseEvent(
    nint source,
    CGEventType mouseType,
    CGPoint mouseCursorPosition,
    CGMouseButton mouseButton);

[LibraryImport("CoreGraphics.framework/CoreGraphics")]
private static partial void CGEventPost(CGEventTapLocation tap, nint eventRef);
```

The backend translates CDP `Input.dispatchMouseEvent` parameters:

| CDP Parameter | CGEvent Mapping |
|---|---|
| `type: "mousePressed"` | `CGEventType.LeftMouseDown` |
| `type: "mouseReleased"` | `CGEventType.LeftMouseUp` |
| `type: "mouseMoved"` | `CGEventType.MouseMoved` |
| `button: "right"` | `CGEventType.RightMouseDown/Up` |
| `x`, `y` | `CGPoint` screen coordinates |

### Keyboard Events

Key events use `CGEventCreateKeyboardEvent`:

```csharp
[LibraryImport("CoreGraphics.framework/CoreGraphics")]
private static partial nint CGEventCreateKeyboardEvent(
    nint source,
    ushort virtualKey,
    [MarshalAs(UnmanagedType.Bool)] bool keyDown);
```

For text insertion, `CGEventKeyboardSetUnicodeString` is used:

```csharp
[LibraryImport("CoreGraphics.framework/CoreGraphics")]
private static partial void CGEventKeyboardSetUnicodeString(
    nint eventRef,
    int stringLength,
    [MarshalAs(UnmanagedType.LPWStr)] string unicodeString);
```

## Screenshot Capture

Window screenshots use `CGWindowListCreateImage`:

```csharp
[LibraryImport("CoreGraphics.framework/CoreGraphics")]
private static partial nint CGWindowListCreateImage(
    CGRect screenBounds,
    CGWindowListOption listOption,
    uint windowID,
    CGWindowImageOption imageOption);
```

The captured `CGImage` is converted to PNG format for the `Page.captureScreenshot` response.

### Capture Options

| Option | Behavior |
|--------|----------|
| `CGWindowListOption.IncludingWindow` | Capture specific window only |
| `CGWindowListOption.OnScreenBelowWindow` | Capture window and everything below |
| `CGWindowImageOption.BoundsIgnoreFraming` | Exclude window shadow |

## Performance Considerations

### Interop Design

The macOS backend uses `[LibraryImport]` (source-generated P/Invoke) instead of `[DllImport]` for:
- Zero-allocation marshalling where possible
- AOT compilation compatibility
- Trimming safety

### Memory Management

CoreFoundation objects obtained through `Copy` or `Create` functions must be released:

```csharp
[LibraryImport("CoreFoundation.framework/CoreFoundation")]
private static partial void CFRelease(nint cf);
```

The backend wraps native handles in `SafeHandle` subclasses to ensure deterministic cleanup.

### Thread Safety

Accessibility API calls must be made from the main thread. The backend marshals calls through `Dispatcher.UIThread.InvokeAsync` when necessary.

## Supported CDP Methods

| CDP Method | macOS Implementation |
|---|---|
| `DOM.getDocument` | Traverse AXUIElement tree |
| `DOM.querySelector` | Search by AXIdentifier, AXRole, AXTitle |
| `DOM.getBoxModel` | Read AXPosition + AXSize |
| `Input.dispatchMouseEvent` | CGEventCreateMouseEvent + CGEventPost |
| `Input.insertText` | CGEventKeyboardSetUnicodeString |
| `Input.dispatchKeyEvent` | CGEventCreateKeyboardEvent |
| `Page.captureScreenshot` | CGWindowListCreateImage → PNG |
| `SystemInfo.getInfo` | sysctl + NSProcessInfo |

## Next Steps

- [OS Automation Overview](/articles/os-automation) — Cross-platform architecture
- [Windows Automation](/articles/windows-automation) — Windows backend
- [Permissions and Setup](/articles/permissions-setup) — Required macOS permissions
