---
title: Architecture
---

# Architecture

This document provides a technical deep-dive into the architecture of the **Chrome DevTools Protocol (CDP) support for Avalonia UI**. It explains how the system bridges standard browser-debugging protocols and AI-driven automation agents with the native Avalonia UI visual tree and property system, as well as how it integrates native operating system automation.

---

## 1. High-Level Overview

At its core, the project embeds a lightweight, high-performance HTTP and WebSocket server directly within an Avalonia application. This embedded server acts as an in-process debugger gateway, translating standard Chrome DevTools Protocol (CDP) JSON-RPC messages into safe, thread-marshalled operations on the live Avalonia visual tree.

This approach permits off-the-shelf browser-testing frameworks (like Playwright and Puppeteer), Chromium-based DevTools frontends, and AI coding agents to inspect, control, query, and test desktop applications without requiring heavy or platform-specific OS automation frameworks.

### The Dual-CDP Topology

The project is designed to support a dual-CDP orchestration layout, typically configured as follows:

*   **Target Application (`CdpSampleApp`)**: Embeds the CDP server, exposing its control endpoints via loopback (default port `9222`).
*   **Inspector Client (`CdpInspectorApp`)**: Acts as a custom DevTools frontend. It connects to the Target Application's WebSocket endpoint. At the same time, the Inspector runs its own embedded CDP server on port `9223` to facilitate self-inspection and testing of the inspector itself.

---

## 2. Architecture Diagram

The flow of messages, target resolution, and thread-boundary crossings are illustrated below.

### Component Relationship Diagram

The CDP server runs inside the target application process and bridges external clients to the Avalonia UI thread:

| Step | From | To | Description |
|------|------|----|-------------|
| 1 | **CDP Client** (Playwright / DevTools / Agent) | **CdpServer** | HTTP connection or WebSocket upgrade |
| 2 | CdpServer | **CdpSession** | Spawns a WebSocket message loop per connection |
| 3 | CdpSession | **CdpTargetSession** | Routes messages to the correct page target |
| 4 | CdpTargetSession | **CdpDispatcher** | Resolves the domain and action from the method name |
| 5 | CdpDispatcher | **CdpDomainRegistry** | Looks up the registered handler delegate |
| 6 | CdpDomainRegistry | **Domain Handlers** | Executes the handler (DOM, CSS, Input, Page, Overlay, Runtime, etc.) |
| 7 | Domain Handlers | **Avalonia UI Thread** | Marshals code to the UI thread via `UIThreadInvoker` |
| 8 | Avalonia UI Thread | **Visual & Logical Trees** | Manipulates or inspects the live control tree |

### Physical Message Flow Pipeline

```
[CDP Client]
     │
     │ (WebSocket / HTTP: OPTIONS, GET /json)
     ▼
[HttpListener Router]  ◄─── Handles CORS & upgrades requests
     │
     │ (Upgraded WebSocket Connection)
     ▼
[CdpSession Message Loop]  ◄─── Accumulates frames in worker thread pool
     │
     │ (Parse JSON-RPC Envelope: id, method, params, sessionId)
     ▼
[CdpServer.UIThreadInvoker]  ◄─── Crosses thread boundary to Avalonia UI Thread
     │
     │ (Invokes on UI Thread)
     ▼
[CdpDomainRegistry & Handlers]  ◄─── Evaluates C#, queries tree, injects inputs
     │
     │ (Returns JsonObject result or throws Exception)
     ▼
[CdpSession]
     │
     │ (Encodes JSON-RPC: { id, result } or { id, error })
     ▼
[CDP Client]
```

---

## 3. Server Architecture

The core server is driven by the `CdpServer` class inside `Chrome.DevTools.Protocol.Server`. It uses a custom HTTP server wrapper built on standard .NET `HttpListener` primitives to remain lightweight, cross-platform, and free from external ASP.NET Core dependencies.

### HttpListener & Loopback Binding

The server binds exclusively to the loopback interface on a user-defined port (default is `9222`):
*   `http://localhost:{port}/`
*   `http://127.0.0.1:{port}/`

On operating systems such as macOS, the host configuration explicitly resolves loopback endpoints to the IPv4 address `127.0.0.1`. This side-steps a known issue where Chromium's target-discovery daemon fails to properly handshake over the IPv6 loopback (`::1`).

### CORS & Preflight Support

Standard Chromium DevTools require Cross-Origin Resource Sharing (CORS) access to query target lists. The server handles this automatically:
*   **OPTIONS Preflight**: The server intercepts HTTP `OPTIONS` requests and returns a `200 OK` response with headers permitting wildcard origins (`Access-Control-Allow-Origin: *`), methods (`GET, POST, OPTIONS`), and headers (`Content-Type`).
*   **Response Headers**: Every HTTP response produced by target discovery includes CORS headers to prevent browser blocks.

### WebSocket Upgrade Flow

When the server identifies an incoming connection as a WebSocket upgrade request (`request.IsWebSocketRequest` is `true`), it inspects the request path:
1.  **Page Target** (`/devtools/page/{targetId}`): Locates the matching `ICdpTarget` (which maps to an active Avalonia Window). It upgrades the connection via `AcceptWebSocketAsync`, invokes the `SessionFactory` to instantiate a `CdpSession`, and starts the session message loop.
2.  **Browser Target** (`/devtools/browser`): Resolves the primary active page target and upgrades the connection to handle browser-wide operations.

---

## 4. Session Management

Each active WebSocket connection is managed by a distinct `CdpSession` instance. It is responsible for orchestrating the JSON-RPC message pump, tracking session state, and recycling resources upon disconnect.

### The JSON-RPC Message Loop

When `CdpSession.StartAsync()` is invoked:
1.  It begins an asynchronous loop reading from the WebSocket into an 8KB buffer.
2.  Incoming chunks are written to a `MemoryStream` until `WebSocketReceiveResult.EndOfMessage` is set to `true`.
3.  The accumulated bytes are decoded into a UTF-8 string and parsed into a `JsonNode` (specifically a `JsonObject`).
4.  The loop extracts:
    *   `id`: The transaction identifier (integer).
    *   `method`: The protocol command name (e.g., `DOM.getDocument`).
    *   `params`: The command arguments (JSON object).
    *   `sessionId`: An optional target routing identifier (used when the client is attached to multiple targets over a single connection).
5.  It invokes the `CdpDispatcher` to execute the command, then returns either a formatted JSON-RPC result envelope or a standard error envelope back to the client.

#### Response and Error Shapes

*   **Success Response**:
    ```json
    {
      "id": 42,
      "result": { ... }
    }
    ```
*   **Error Response**:
    ```json
    {
      "id": 42,
      "error": {
        "code": -32603,
        "message": "Detailed exception description"
      }
    }
    ```

### Lifetime & Resource Tracking

To keep the application memory-footprint lean, `CdpSession` maintains several local registries:
*   **`RemoteObjects`**: A dictionary mapping string keys (e.g., `object:12`) to live C# objects referenced in evaluation contexts.
*   **`NodeMap`**: A bidirectional translation cache mapping Avalonia control references to unique node IDs.
*   **Domain Subscriptions**: Unsubscribes the connection from global notification streams (such as logs, network capture, page screencasts, and profiling counters) when the session terminates.

---

## 5. Domain System

The protocol logic is split into separate **Domain Handlers**. The handlers process incoming methods, execute logic against the UI state, and emit events.

### Domain Registry and Dispatcher

```
[CdpSession] ──► [CdpDispatcher] ──► [CdpDomainRegistry] ──► [Domain Handler]
```

*   `CdpDomainRegistry`: Exposes a static, concurrent dictionary linking domain names (like `DOM`, `CSS`, `Input`, `Page`, etc.) to a delegate of signature `Func<CdpSession, string, JsonObject, Task<JsonObject>>`.
*   `CdpDispatcher.DispatchAsync`: Splits the incoming method name by the dot character (e.g., `DOM.getDocument` -> domain `DOM`, action `getDocument`). It verifies the domain registration and routes the payload to the handler delegate.

### Native Domain Implementations

Avalonia-specific behavior is implemented in the `Avalonia.Diagnostics.Cdp.Domains` namespace. The server registers 18 domain handlers:

**Core Domains:**
*   **`DomDomain`**: Traverses the visual tree, queries selectors, computes boxes, and reports element properties.
*   **`CssDomain`**: Exposes styling details, compiles matched style rules, and reflects property updates.
*   **`InputDomain`**: Converts web mouse, keyboard, touch, and scroll events into native Avalonia raw input signals.
*   **`PageDomain`**: Renders high-DPI screenshots and manages screencast loops.
*   **`OverlayDomain`**: Draws real-time inspection highlights and metadata overlays onto the target window's adorner layer.
*   **`RuntimeDomain`**: Evaluates C# expressions using Roslyn scripting and resolves remote object references.

**Diagnostic Domains:**
*   **`MemoryDomain`**: Tracks live control allocations, detached controls, and retainer paths for leak detection.
*   **`PerformanceDomain`**: Collects CPU, memory, FPS, and DOM node count metrics.
*   **`AccessibilityDomain`**: Maps Avalonia automation peers to the accessibility tree format.
*   **`NetworkDomain`**: Intercepts outbound HttpClient requests for request/response monitoring.

**Utility Domains:**
*   **`ApplicationDomain`**: Queries and mutates `Application.Current.Resources` (brushes, colors, styles).
*   **`EmulationDomain`**: Resizes windows, switches theme variants, and overrides locale settings.
*   **`WindowChromeDomain`**: Controls window topmost, opacity, title, position, minimize/maximize/close.
*   **`BrowserDomain`**: Reports version info, window bounds, and manages target lifecycle.
*   **`AuditsDomain`**: Runs accessibility, best-practices, and layout audits with scored reports.
*   **`DebuggerDomain`**: Manages breakpoints, conditional evaluation, and pause/resume flow control.
*   **`DomDebuggerDomain`**: Introspects Avalonia routed event handlers via reflection.
*   **`RecorderDomain`**: Intercepts user interactions for step recording with configurable drag thresholds.
*   **`LogDomain`**: Bridges Avalonia's log system to CDP clients via composite log sink.

---

## 6. Target Discovery

The system exposes target discovery endpoints matching standard Chromium behavior to allow client applications to inspect what windows are available.

### /json Endpoints

*   **`/json` & `/json/list`**: Returns a JSON array listing all available windows.
    ```json
    [
      {
        "description": "",
        "devtoolsFrontendUrl": "devtools://devtools/bundled/inspector.html?ws={host}/devtools/page/3a5e8f...",
        "id": "3a5e8f21-72f1-4ab3-938b-945731ffc210",
        "title": "Application Main Window",
        "type": "page",
        "url": "http://localhost:9222/",
        "webSocketDebuggerUrl": "ws://{host}/devtools/page/3a5e8f21-72f1-4ab3-938b-945731ffc210"
      }
    ]
    ```
*   **`/json/version`**: Exposes protocol and client metadata, including the browser-level WebSocket URL (`ws://{host}/devtools/browser`).

### Automatic Window Tracking

The Avalonia server hooks directly into window life cycles:
1.  It registers class handlers on `Window.WindowOpenedEvent` and `Window.WindowClosedEvent`.
2.  Opening a window creates a new `AvaloniaCdpTarget` and registers it with the server.
3.  The server broadcasts a `Target.targetCreated` protocol event over all active sessions to notify clients of the new page.
4.  Closing the window cleans up the target registration and broadcasts `Target.targetDestroyed`.

---

## 7. Visual Tree Mapping

The `DOM` domain handles the serialization of Avalonia's visual components into the HTML-like tree structure expected by browser-automation clients.

### Tree Traversal Modes

The session exposes two visual parsing strategies, controlled by client parameters:
*   **Visual Tree (`pierce: true` / logical tree disabled)**: Traverses the complete tree of `Visual` nodes, including control templates, control chrome, and borders.
*   **Logical Tree (`pierce: false` / logical tree enabled)**: Traverses only user-declared logical elements (`ILogical`), mirroring the high-level semantic layout structure of the application.

### Bidirectional Node ID Mapping

Browser clients identify DOM nodes using integer identifiers starting at `1`. Inside `CdpSession`, the `NodeMap` maintains bidirectional mapping:
*   Translates Avalonia control instances to unique `nodeId` values.
*   Resolves `nodeId` parameters in client commands (e.g., `DOM.focus`, `DOM.getBoxModel`) back to the original `Visual` or `Control` object reference.

### Attribute Mapping

When compiling a tree node, control characteristics are transformed into key-value attribute lists:

| HTML Attribute Name | Source Avalonia Property / Context |
| :--- | :--- |
| `id` | `Control.Name` |
| `class` | Concat list of active classes in `Control.Classes` |
| `text` | Text content extracted from `TextBlock.Text`, `TextBox.Text`, or content controls |
| `AutomationProperties.AutomationId` | Custom automation identifier for stable selection |
| `is-visible` | Evaluates control visibility state |

### Selector Engine and Queries

When clients send queries using `DOM.querySelector` or `DOM.querySelectorAll`, the `SelectorEngine` parses and resolves the selector string:
1.  **Type matching**: Selector names like `Button` match control types.
2.  **ID matching**: `#myButton` matches controls where `Name == "myButton"`.
3.  **Class matching**: `.primary` searches active `Classes`.
4.  **Attribute matching**: Filters nodes by specific attributes (e.g. `[AutomationId="submit"]`).
5.  **Hierarchical traversal**: Resolves descendant and parent-child hierarchies (e.g., `Grid > Border > TextBlock`).

---

## 8. Thread Safety & Dispatcher Marshalling

Desktop application frameworks restrict UI interactions (such as reading layout coordinates, checking properties, or traversing the visual tree) to a single, dedicated execution thread—the main UI thread.

### The Threading Boundary

WebSocket message loops and HTTP listeners run in the background on .NET thread pool worker threads. Modifying or inspecting Avalonia controls directly from these background threads causes application crashes or data corruption.

```
 [Background Threads]                         [UI Thread]
┌─────────────────────┐                     ┌─────────────────────┐
│  WebSocket Loop     │                     │  Avalonia UI Loop   │
│  CdpSession         │                     │  Visual Tree        │
│  HTTP Listeners     │                     │  Control Layout     │
└──────────┬──────────┘                     └──────────▲──────────┘
           │                                           │
           │  1. CheckAccess() == false                │
           ├───────────────────────────────────────────┤
           │  2. Dispatcher.UIThread.InvokeAsync()     │
           │                                           │
           ▼                                           │
  [Execution Swapped] ─────────────────────────────────┘
```

### The UI Thread Invoker

The boundary is bridged via the `CdpServer.UIThreadInvoker` delegate:
1.  When a method is dispatched, the dispatcher delegates control execution to the registered invoker.
2.  In the Avalonia implementation, this invoker points to:
    ```csharp
    Chrome.DevTools.Protocol.CdpServer.UIThreadInvoker = async (action) =>
    {
        if (Dispatcher.UIThread.CheckAccess()) return await action();
        return await Dispatcher.UIThread.InvokeAsync(action);
    };
    ```
3.  This checks if the executing context is already on the UI thread (`CheckAccess()`). If not, it marshals the operation using `Dispatcher.UIThread.InvokeAsync` and asynchronously blocks the background session worker until the UI thread completes execution and yields the JSON response.

---

## 9. OS Automation Architecture

When the target application does not run an embedded CDP server (e.g., legacy or pre-compiled desktop apps), the inspector uses **OS Automation Mode** by targeting the `os://` URI scheme.

### Client-Side Interception

Inside `CdpService`, if the connection address begins with `os://`, the client:
1.  Bypasses HTTP requests and TCP loopback sockets entirely.
2.  Queries the operating system window managers directly for active window references.
3.  Spawns an in-process **`OsAutomationCdpSession`** instance linked to the target window.

The connection routing works as follows:

**Connection Path Selection:**

```
CdpService Client
  ├─ ws:// or http:// scheme → Standard WebSocket → Embedded CdpServer → Target App UI
  └─ os:// scheme → OsAutomationCdpSession → OSAutomationService
                                               ├─ macOS → CoreGraphics / AXUIElement
                                               ├─ Windows → UIAutomationCore / User32
                                               └─ Linux → X11 / AT-SPI2 → Native Desktop Window
```

| Connection Scheme | Path | Controls |
|-------------------|------|----------|
| `ws://` or `http://` | Standard WebSocket → Embedded CdpServer | Target App Visual Tree |
| `os://` | OsAutomationCdpSession → Platform-specific P/Invoke | Native Desktop Window |

### Emulating CDP via Native APIs

The `OsAutomationCdpSession` acts as a local protocol emulator. It intercepts standard commands and delegates them to the `IOsAutomation` backend:

*   **`DOM.getDocument` / `querySelector`**: Requests the window accessibility element tree (`GetElementTree`) and builds a simulated CDP document hierarchy mapping native roles to simulated tags (e.g., standard buttons represent `<Button>` elements).
*   **`DOM.getBoxModel`**: Obtains element coordinate geometries in screen space and converts them into client-relative coordinates.
*   **`Input.dispatchMouseEvent` / `insertText`**: Uses native OS APIs to simulate physical pointer movements, mouse clicks, keystrokes, and text input directly into the window.
*   **`Page.captureScreenshot`**: Intercepts screen frame capture commands and returns window pixel arrays retrieved using native OS screen recording APIs.

### Platform Adapters

The OS-level interactions are routed to platform-specific drivers:
1.  **macOS (`MacOsAutomation`)**: Interfaces with Apple's ApplicationServices and AppKit libraries via native P/Invoke, using `AXUIElement` for accessibility queries and `CGEventCreate` for input injection.
2.  **Windows (`WindowsAutomation`)**: Integrates with the Windows `UIAutomationCore` engine and simulates inputs using standard Win32 `SendInput` / `PostMessage` methods.
3.  **Linux (`LinuxAutomation`)**: Interfaces with standard `X11` server bindings (`libX11`, `libXtst`) to navigate windows and simulate inputs.

### System Permissions Model

Since OS Automation interacts directly with other desktop processes, it is subject to operating system permission frameworks:
*   **Accessibility Permissions**: Required on both macOS and Windows to query active windows, fetch element trees, and inspect UI hierarchies.
*   **Screen Recording Permissions**: Required on macOS to successfully capture composite window pixels. If missing, the screen recording APIs return blank frames.
*   **Input Monitoring Permissions**: Required to set native system-wide hooks to record pointer clicks and actions during test creation.
