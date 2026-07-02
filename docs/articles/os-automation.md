---
title: OS Automation Overview
---

# OS Automation Overview

The **CDP.Automation.OS** package extends the Chrome DevTools Protocol (CDP) for Avalonia to work with **any desktop application**, not just those that embed a CDP server. It achieves this by emulating CDP domains in-process using native operating system accessibility and automation APIs, exposing every visible window as a virtual CDP target.

This enables the inspector to connect to, inspect, control, and record interactions against arbitrary third-party applications — from native macOS apps built with AppKit and SwiftUI to Windows Win32, WPF, and UWP applications — all through the same familiar CDP protocol that agents and tools already speak.

---

## Core Concept: CDP Emulation via OS APIs

Traditional CDP connections use HTTP and WebSocket to communicate with a target application that runs its own CDP server. The OS Automation layer replaces this network transport with an in-process translation layer:

1. The **inspector** detects the `os://` connection scheme.
2. Instead of opening a WebSocket, it instantiates an `OsAutomationCdpSession` directly.
3. Each CDP command (e.g., `DOM.getDocument`, `Input.dispatchMouseEvent`) is translated into calls against native OS automation APIs.
4. Responses are formatted as standard CDP JSON-RPC results, making the difference transparent to the rest of the inspector.

This design means that any tool or agent that speaks CDP can automate any desktop application — no code changes to the target application are needed.

---

## Architecture

The following diagram shows how the OS Automation layer integrates with the inspector and native operating system APIs:

**CDP Inspector App**

| Component | Routes to | Via |
| :--- | :--- | :--- |
| `ConnectionViewModel` | → `ICdpService` | interface |
| `CdpService` (Facade) | → `OsAutomationCdpSession` | `os://` host |
| `CdpService` (Facade) | → `ClientWebSocket` | `http/ws` host |

**CDP.Automation.OS**

| Component | Routes to | Platform |
| :--- | :--- | :--- |
| `OsAutomationCdpSession` | → `OSAutomationService` | all |
| `OSAutomationService` | → `MacOsAutomation` | macOS |
| `OSAutomationService` | → `WindowsAutomation` | Windows |
| `OSAutomationService` | → `LinuxAutomation` | Linux |

**Native Operating System**

| Backend | Native API | OS Target |
| :--- | :--- | :--- |
| `MacOsAutomation` | → CoreGraphics / AXUIElement | macOS Accessibility |
| `WindowsAutomation` | → UIAutomationCore / Win32 | Windows UIA |
| `LinuxAutomation` | → X11 / libX11 | Linux X11 |

### Key components

| Component | Responsibility |
| :--- | :--- |
| `CdpService` | The connection facade inside the inspector. Detects `os://` URLs and routes commands to `OsAutomationCdpSession` instead of a WebSocket. |
| `OsAutomationCdpSession` | Translates CDP JSON-RPC method calls into `IOsAutomation` interface calls and formats results as CDP-compatible JSON responses. |
| `OSAutomationService` | A static factory that detects the current operating system at runtime and creates the appropriate platform-specific `IOsAutomation` implementation, wrapped in an `EnrichedOsAutomation` decorator. |
| `MacOsAutomation` | macOS backend using AXUIElement (Accessibility API), CoreGraphics, and CGEvent for tree traversal, screenshots, and input injection. |
| `WindowsAutomation` | Windows backend using UIAutomation COM interfaces, User32 for window management, and SendInput for mouse/keyboard injection. |
| `LinuxAutomation` | Linux backend using X11/libX11 for window enumeration, tree traversal, and input simulation. |
| `EnrichedOsAutomation` | A decorator that augments the native window list with process-level fallback entries for applications that do not expose visible windows through the OS window manager. |

---

## The `os://` Connection Scheme

The `os://` URL scheme is the entry point for OS Automation mode. When the inspector's host field contains an `os://` URL, the `CdpService` activates OS Automation instead of network-based CDP.

### Target Discovery

When `CdpService.GetTargetsAsync` receives an `os://` host:

1. It calls `OSAutomationService.Instance.GetWindows()` to enumerate all visible OS windows.
2. Each window is returned as a `TargetItem` with the URL format `os://{windowId}`.
3. The inspector displays these as connectable targets, just like network-based CDP targets.

```csharp
// Target scanning with os:// host
var targets = await cdpService.GetTargetsAsync("os://localhost");
// Returns: [TargetItem("Finder", "os://1234", "1234"), ...]
```

### Connection

When `CdpService.ConnectAsync` receives an `os://` target:

1. It creates an `OsAutomationCdpSession` with the target window ID.
2. It subscribes to the session's `EventReceived` handler for CDP events.
3. It checks `HasAccessibilityPermission()` and `HasScreenCapturePermission()` to report permission status.
4. Commands are dispatched directly through `HandleCommandAsync` — no WebSocket involved.

---

## Virtual CDP Pages

Each operating system window is represented as a **virtual CDP page**. The OS Automation layer maps OS concepts to CDP concepts:

| CDP Concept | OS Automation Mapping |
| :--- | :--- |
| Page / Target | An OS window identified by its native window ID (CGWindowNumber on macOS, HWND on Windows) |
| DOM Document | The accessibility tree rooted at the window, with `nodeType = 9` for the document root |
| DOM Node | An accessibility element (`AXUIElement` on macOS, `IUIAutomationElement` on Windows) |
| Node ID | An incrementing integer assigned by the session, mapped from the OS element's native ID |
| Element Tag Name | The accessibility role (e.g., `AXButton`, `Button`, `AXTextField`) |
| Element Attributes | Accessibility properties: `id`, `Name`, `AutomationId`, `Role`, `Text`, etc. |
| Box Model | The element's screen-space bounding rectangle from the accessibility API |
| Screenshot | A window capture via CoreGraphics (macOS) or GDI BitBlt (Windows) |

### Document URL

Virtual CDP documents use `os://localhost/` as their `documentURL` and `baseURL`, distinguishing them from network-based CDP pages.

---

## Emulated CDP Domains

The `OsAutomationCdpSession` emulates the following CDP domains:

### DOM Domain

Provides visual tree inspection by mapping the OS accessibility tree to CDP DOM nodes.

| Method | Description |
| :--- | :--- |
| `DOM.enable` / `DOM.disable` | Enables or disables the DOM domain (no-op for setup). |
| `DOM.getDocument` | Returns the full accessibility tree as a CDP document with nested child nodes. |
| `DOM.querySelector` | Finds an element by CSS-style selector (`#id`, role name, attribute selectors). |
| `DOM.querySelectorAll` | Returns all matching elements for a selector. |
| `DOM.getBoxModel` | Returns the screen-space bounding rectangle of an element as a CDP box model quad. |
| `DOM.getOuterHTML` | Generates pseudo-HTML markup from the accessibility tree. |
| `DOM.resolveNode` | Resolves a node ID to a Runtime remote object reference. |
| `DOM.focus` | Sets focus on the target accessibility element. |
| `DOM.setInspectedNode` | Binds a node to the `$0` evaluation context variable. |

### Input Domain

Translates CDP input events into native OS-level mouse and keyboard actions.

| Method | Description |
| :--- | :--- |
| `Input.enable` / `Input.disable` | Enables or disables the Input domain and starts/stops input capture for recording. |
| `Input.dispatchMouseEvent` | Dispatches `mouseMoved`, `mousePressed`, `mouseReleased`, and `mouseWheel` events by injecting native OS events. |
| `Input.dispatchKeyEvent` | Dispatches key press and release events via native keyboard simulation. |
| `Input.insertText` | Types text into the focused element using native text injection. |

### Page Domain

Provides screenshot capture and lifecycle management.

| Method | Description |
| :--- | :--- |
| `Page.enable` / `Page.disable` | Enables or disables the Page domain. |
| `Page.captureScreenshot` | Captures the target window as a PNG image using native screen capture APIs and returns it as base64-encoded data. |
| `Page.startScreencast` | Begins periodic frame capture for live preview streaming. |
| `Page.stopScreencast` | Stops the screencast frame loop. |
| `Page.screencastFrameAck` | Acknowledges receipt of a screencast frame. |
| `Page.bringToFront` | Raises the target window to the foreground. |

### SystemInfo Domain

Reports operating system and process-level information.

| Method | Description |
| :--- | :--- |
| `SystemInfo.getInfo` | Returns platform name, architecture, and a machine description string. |

### Runtime Domain

Provides expression evaluation and object inspection.

| Method | Description |
| :--- | :--- |
| `Runtime.enable` / `Runtime.disable` | Enables or disables the Runtime domain and emits `executionContextCreated` events. |
| `Runtime.evaluate` | Evaluates simple property lookups against the accessibility tree and returns results. |
| `Runtime.callFunctionOn` | Calls helper functions on mapped remote objects. |
| `Runtime.getProperties` | Lists properties of a node's accessibility attributes. |

### Additional Emulated Domains

| Domain | Description |
| :--- | :--- |
| `Target` | Returns the connected window as a single CDP target with an `os://` URL. |
| `Accessibility` | Exposes the accessibility tree structure and element properties. |
| `CSS` | Returns computed style information derived from element bounds and attributes. |
| `Overlay` | Accepts highlight/hide commands (visual overlay is rendered in the inspector preview). |
| `Recorder` | Supports recording and playback of user interactions through input capture. |
| `Performance` | Provides process-level performance metrics (CPU, memory) via polling. |
| `Memory` | Reports working set and private byte metrics for the target process. |
| `Network` | Emulates network domain for compatibility (offline simulation, URL blocking). |
| `Browser` | Provides basic browser-compatible metadata responses. |

---

## Data Model

### `OSWindow`

Represents a discovered OS window.

```csharp
public sealed class OSWindow
{
    public required string Id { get; init; }         // Native window ID
    public required string Title { get; init; }      // Window title
    public required string ProcessName { get; init; } // Process name
    public required int ProcessId { get; init; }     // OS process ID
    public required SKRectI Bounds { get; init; }    // Screen-space bounds
}
```

### `OSNode`

Represents a node in the accessibility element tree.

```csharp
public sealed class OSNode
{
    public required string Id { get; set; }              // Unique element ID
    public required string Name { get; set; }            // Element name/role
    public string Text { get; set; } = string.Empty;     // Visible text content
    public string Role { get; set; } = string.Empty;     // Accessibility role
    public SKRectI Bounds { get; set; }                  // Bounding rectangle
    public List<OSNode> Children { get; } = new();       // Child elements
    public Dictionary<string, string> Attributes { get; } // Additional attributes
}
```

### `IOsAutomation` Interface

The core abstraction implemented by each platform backend:

```csharp
public interface IOsAutomation
{
    bool MovePhysicalCursor { get; set; }
    bool UsePeerAutomation { get; set; }
    bool UseAccessibilityEvents { get; set; }
    IReadOnlyList<OSWindow> GetWindows();
    OSNode? GetElementTree(string windowId);
    void SimulateClick(string windowId, double x, double y, string? nodeId = null);
    void SimulateMouseMove(string windowId, double x, double y);
    void SimulateMouseDown(string windowId, double x, double y, string button);
    void SimulateMouseUp(string windowId, double x, double y, string button);
    void SimulateMouseWheel(string windowId, double x, double y,
                            double deltaX, double deltaY);
    void SimulateKeyPress(string windowId, string key, int modifiers = 0);
    void SimulateTypeText(string windowId, string text);
    byte[] CaptureWindow(string windowId);
    OSNode? GetFocusedElement(string windowId);
    bool HasScreenCapturePermission();
    bool HasAccessibilityPermission();
    void StartInputCapture(string windowId,
                           Action<double, double, string> onClick,
                           Action<string, string, string?> onAccessibilityEvent);
    void StopInputCapture();
    OSProcessMetrics? GetProcessMetrics(int pid);
    void BringToFront(string windowId);
}
```

---

## Installation

Install the NuGet package in your project:

```bash
dotnet add package Chrome.DevTools.Automation.OS --prerelease
```

Or add a package reference manually:

```xml
<ItemGroup>
  <PackageReference Include="Chrome.DevTools.Automation.OS" Version="x.y.z" />
</ItemGroup>
```

## Quick Start

### Listing Windows

```csharp
using CDP.Automation.OS;

var windows = OSAutomationService.Instance.GetWindows();
foreach (var win in windows)
{
    Console.WriteLine($"Window: {win.Title} (PID: {win.ProcessId})");
}
```

### Inspecting an Element Tree

```csharp
var tree = OSAutomationService.Instance.GetElementTree(windowId);
if (tree != null)
{
    PrintTree(tree, indent: 0);
}

void PrintTree(OSNode node, int indent)
{
    Console.WriteLine($"{new string(' ', indent)}<{node.Name}> {node.Text}");
    foreach (var child in node.Children)
    {
        PrintTree(child, indent + 2);
    }
}
```

### Simulating Input

```csharp
var automation = OSAutomationService.Instance;

// Click at coordinates (100, 200) in the target window
automation.SimulateClick(windowId, 100.0, 200.0);

// Type text into the focused element
automation.SimulateTypeText(windowId, "Hello from OS Automation!");

// Capture a screenshot
byte[] pngBytes = automation.CaptureWindow(windowId);
File.WriteAllBytes("screenshot.png", pngBytes);
```

### Checking Permissions

```csharp
var automation = OSAutomationService.Instance;

if (!automation.HasAccessibilityPermission())
{
    Console.WriteLine("Accessibility permission not granted.");
}

if (!automation.HasScreenCapturePermission())
{
    Console.WriteLine("Screen capture permission not granted.");
}
```

---

## Platform Support

| Platform | Backend Class | Native APIs | Status |
| :--- | :--- | :--- | :--- |
| **macOS** | `MacOsAutomation` | AXUIElement, CoreGraphics, CGEvent | Full support |
| **Windows** | `WindowsAutomation` | UIAutomation COM, User32, GDI32 | Full support |
| **Linux** | `LinuxAutomation` | X11, libX11 | Basic support |

See the platform-specific documentation for detailed information:

- [macOS Automation](macos-automation.md) — AXUIElement tree traversal, CoreGraphics screenshots, CGEvent input injection
- [Windows Automation](windows-automation.md) — UIAutomation COM, User32 input, GDI window capture
- [Permissions and Setup](permissions-setup.md) — OS-level permission requirements and verification

---

## Design Principles

1. **Zero target modification**: The target application requires no code changes, instrumentation, or embedded servers.
2. **Protocol compatibility**: CDP responses match standard Chrome DevTools Protocol shapes, so existing tools and agents work without modification.
3. **High performance**: The `os://` scheme bypasses network sockets entirely. Commands are dispatched as direct method calls with minimal allocation.
4. **Trimming safety**: All implementations use `[LibraryImport]` instead of `[DllImport]` for high-performance marshalling compatible with .NET assembly trimming (`PublishTrimmed`).
5. **Graceful degradation**: If OS permissions are not granted, the system returns fallback window entries and reports permission status through the connection status display.
