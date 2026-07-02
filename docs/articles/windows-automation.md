---
title: Windows Automation
---

# Windows Automation

The Windows backend for `Chrome.DevTools.Automation.OS` uses native UI Automation and Win32 APIs to discover, inspect, and interact with any desktop application. It maps the Windows automation tree to CDP DOM nodes, enabling Chrome DevTools Protocol workflows for non-Avalonia applications.

## Architecture

| CDP Method | Backend API | Output |
| :--- | :--- | :--- |
| `DOM.getDocument` | → Windows Backend → UIAutomation COM | Automation Tree |
| `Input.dispatchMouseEvent` | → User32 SendInput | System Mouse/Keyboard |
| `Page.captureScreenshot` | → PrintWindow / BitBlt | Window Bitmap |

The Windows backend uses three native API families:

1. **UI Automation** (`UIAutomationCore.dll`) — Tree traversal and element inspection
2. **User32** (`user32.dll`) — Input injection and window management
3. **GDI+/User32** — Screenshot capture

## UI Automation

### Tree Discovery

The backend connects to the Windows UI Automation framework via COM interop:

```csharp
[LibraryImport("UIAutomationCore.dll")]
private static partial int UiaGetRootElement(out nint root);
```

From the root element, it walks the automation tree using `TreeWalker` patterns:

```csharp
[LibraryImport("UIAutomationCore.dll")]
private static partial int UiaNavigate(
    nint element,
    NavigateDirection direction,
    out nint sibling);
```

### Element Properties

Each UI Automation element exposes properties mapped to CDP attributes:

| UIA Property | CDP Mapping | Description |
|---|---|---|
| `UIA_ControlTypePropertyId` | `nodeName` | Control type (Button, Edit, etc.) |
| `UIA_NamePropertyId` | `[Text]` attribute | Display name or label |
| `UIA_AutomationIdPropertyId` | `[id]` / `[AutomationId]` | Developer-assigned identifier |
| `UIA_BoundingRectanglePropertyId` | Box model | Screen coordinates and dimensions |
| `UIA_IsEnabledPropertyId` | `[IsEnabled]` | Interactive state |
| `UIA_HasKeyboardFocusPropertyId` | `[IsFocused]` | Focus state |
| `UIA_ValueValuePropertyId` | `[value]` | Current value (text fields, sliders) |
| `UIA_ToggleToggleStatePropertyId` | `[checked]` | Checkbox/toggle state |
| `UIA_ClassNamePropertyId` | `[className]` | Win32 class name |

### Selector Mapping

CSS selectors map to UI Automation property conditions:

| CSS Selector | UIA Equivalent |
|---|---|
| `#myButton` | `AutomationId == "myButton"` |
| `Button` | `ControlType == Button` |
| `[Text="Submit"]` | `Name == "Submit"` |
| `[AutomationId="okBtn"]` | `AutomationId == "okBtn"` |
| `[className="Edit"]` | `ClassName == "Edit"` |

### Pattern Support

UI Automation patterns are used for interaction:

| Pattern | CDP Usage |
|---------|-----------|
| `InvokePattern` | Button clicks, menu item activation |
| `ValuePattern` | Text input, slider adjustment |
| `TogglePattern` | Checkbox state changes |
| `SelectionItemPattern` | List/combo box selection |
| `ScrollPattern` | Scroll operations |
| `ExpandCollapsePattern` | Tree node expansion |

## Input Injection (User32)

### Mouse Events

Mouse input uses `SendInput` with `INPUT_MOUSE`:

```csharp
[LibraryImport("user32.dll", SetLastError = true)]
private static partial uint SendInput(
    uint nInputs,
    Span<INPUT> pInputs,
    int cbSize);
```

The backend translates CDP mouse event types:

| CDP Parameter | SendInput Flag |
|---|---|
| `type: "mousePressed", button: "left"` | `MOUSEEVENTF_LEFTDOWN` |
| `type: "mouseReleased", button: "left"` | `MOUSEEVENTF_LEFTUP` |
| `type: "mousePressed", button: "right"` | `MOUSEEVENTF_RIGHTDOWN` |
| `type: "mouseMoved"` | `MOUSEEVENTF_MOVE \| MOUSEEVENTF_ABSOLUTE` |

Coordinates are converted from pixel positions to the normalized 0–65535 coordinate space:

```csharp
int normalizedX = (int)((x * 65535.0) / screenWidth);
int normalizedY = (int)((y * 65535.0) / screenHeight);
```

### Keyboard Events

Key events use `SendInput` with `INPUT_KEYBOARD`:

```csharp
var input = new INPUT
{
    type = INPUT_KEYBOARD,
    ki = new KEYBDINPUT
    {
        wVk = virtualKeyCode,
        dwFlags = isKeyUp ? KEYEVENTF_KEYUP : 0
    }
};
```

For text insertion, Unicode characters are sent with `KEYEVENTF_UNICODE`:

```csharp
foreach (char c in text)
{
    var down = new INPUT
    {
        type = INPUT_KEYBOARD,
        ki = new KEYBDINPUT
        {
            wScan = c,
            dwFlags = KEYEVENTF_UNICODE
        }
    };
    // ... send down + up
}
```

## Window Management

### Finding Windows

```csharp
[LibraryImport("user32.dll", SetLastError = true)]
private static partial nint FindWindowW(
    [MarshalAs(UnmanagedType.LPWStr)] string? className,
    [MarshalAs(UnmanagedType.LPWStr)] string? windowName);

[LibraryImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
private static partial bool EnumWindows(
    EnumWindowsProc lpEnumFunc,
    nint lParam);
```

### Window Positioning

```csharp
[LibraryImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
private static partial bool GetWindowRect(nint hWnd, out RECT lpRect);

[LibraryImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
private static partial bool SetForegroundWindow(nint hWnd);
```

## Screenshot Capture

Window screenshots use `PrintWindow` or BitBlt:

```csharp
[LibraryImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
private static partial bool PrintWindow(nint hwnd, nint hdcBlt, uint nFlags);
```

The captured bitmap is encoded to PNG for `Page.captureScreenshot` responses.

### High-DPI Support

The backend accounts for display scaling:

```csharp
[LibraryImport("user32.dll")]
private static partial int GetDpiForWindow(nint hwnd);
```

Screenshot coordinates and dimensions are adjusted for the window's DPI setting.

## Performance Considerations

### Interop Design

- Uses `[LibraryImport]` for source-generated, trimming-safe P/Invoke
- `Span<INPUT>` for stack-allocated input arrays (no heap allocation)
- Direct COM vtable calls for UI Automation to avoid COM interop overhead

### Caching

The backend caches the automation tree structure and invalidates on `StructureChangedEvent`:

```csharp
automation.AddStructureChangedEventHandler(
    rootElement,
    TreeScope.Subtree,
    OnStructureChanged);
```

## Supported CDP Methods

| CDP Method | Windows Implementation |
|---|---|
| `DOM.getDocument` | Walk UIAutomation tree |
| `DOM.querySelector` | PropertyCondition search |
| `DOM.getBoxModel` | BoundingRectangleProperty |
| `Input.dispatchMouseEvent` | SendInput (MOUSEINPUT) |
| `Input.insertText` | SendInput (KEYEVENTF_UNICODE) |
| `Input.dispatchKeyEvent` | SendInput (KEYBDINPUT) |
| `Page.captureScreenshot` | PrintWindow → PNG encode |
| `SystemInfo.getInfo` | Environment + WMI queries |

## Next Steps

- [OS Automation Overview](/articles/os-automation) — Cross-platform architecture
- [macOS Automation](/articles/macos-automation) — macOS backend
- [Permissions and Setup](/articles/permissions-setup) — Windows permission requirements
