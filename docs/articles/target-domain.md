---
title: Target Domain
---

# Target Domain

The `Target` domain allows clients to discover, attach to, and control debuggable target pages and windows. In the context of the CDP Avalonia project, every active top-level desktop window is represented as a separate, independent target. This allows automated agents, developer tools, and testing frameworks to inspect and manipulate multiple windows concurrently.

The core implementation spans across:
* `TargetDomain.cs`: Defines the JSON-RPC message handlers for the `Target` domain.
* `CdpServer.cs` (Chrome.DevTools.Protocol): Handles HTTP discovery endpoints, WebSocket routing, target list tracking, and target lifecycle event broadcasting.
* `CdpServer.cs` (Avalonia.Diagnostics.Cdp): Integrates target tracking directly with the Avalonia UI lifecycle and delegates platform-specific window events.

---

## Target Discovery Endpoints

When the CDP server starts, it exposes standard HTTP endpoints to allow clients to discover available debugger targets.

### `/json` and `/json/list`
These endpoints return a JSON array containing details about all active windows currently registered in the application. Tools like Google Chrome, Puppeteer, or custom inspector applications query this endpoint first to obtain the WebSocket debugger URL for each window.

#### HTTP Response Example
```json
[
  {
    "description": "",
    "devtoolsFrontendUrl": "devtools://devtools/bundled/inspector.html?ws=localhost:9222/devtools/page/3a9a14d5-fb40-410a-bb79-6ea43f9a76bc",
    "id": "3a9a14d5-fb40-410a-bb79-6ea43f9a76bc",
    "title": "Main Window",
    "type": "page",
    "url": "http://localhost:9222/",
    "webSocketDebuggerUrl": "ws://localhost:9222/devtools/page/3a9a14d5-fb40-410a-bb79-6ea43f9a76bc"
  }
]
```

### `/json/version`
Returns browser environment details and the global browser-level WebSocket debug URL (`/devtools/browser`) used for target multiplexing.

#### HTTP Response Example
```json
{
  "Browser": "Chrome/DevTools/Protocol",
  "Protocol-Version": "1.3",
  "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
  "webSocketDebuggerUrl": "ws://localhost:9222/devtools/browser"
}
```

---

## Multi-Window Support and Lifecycle Tracking

The Avalonia integration dynamically listens to window opening and closing events in the application. In `CdpServer.cs` (Avalonia.Diagnostics.Cdp), static event handlers are registered:

```csharp
Window.WindowOpenedEvent.AddClassHandler<Window>((w, e) =>
{
    Register(w, w.Title ?? w.GetType().Name);
});
Window.WindowClosedEvent.AddClassHandler<Window>((w, e) =>
{
    Unregister(w);
});
```

* **Window Opened:** When a new `Window` is shown, the server generates a unique `targetId` (GUID) and registers it as an `AvaloniaCdpTarget`.
* **Window Closed:** When a window is closed, it is automatically removed from the target registry, and all active CDP sessions attached to that target are detached.
* **Fallback Scan:** When retrieving the target list, the server checks the active desktop lifetime (`IClassicDesktopStyleApplicationLifetime`) to register any pre-existing windows that were opened before the server started.

Each target implements the `ICdpTarget` interface and wraps the underlying Avalonia `TopLevel` component, assigning it a type of `"page"`.

---

## Supported Methods

The `TargetDomain.cs` handles requests sent to the `Target` domain. The table below lists the implemented methods:

| CDP Method | Description | Implementation Details |
|---|---|---|
| `Target.getTargets` | Returns list of all active windows and pages. | Queries the target registry and builds target descriptor JSON. |
| `Target.setDiscoverTargets` | Subscribes/unsubscribes to window lifecycle updates. | Enforces event delivery and pushes initial target list. |
| `Target.attachToTarget` | Attaches a session to a target window (requires `flatten=true`). | Generates a new `sessionId` and spawns a target-specific session. |
| `Target.detachFromTarget` | Detaches the debugging session from a target window. | Removes the target session and clears active connections. |
| `Target.activateTarget` | Brings a window to the foreground. | Schedules `Window.Activate()` on the Avalonia UI Thread. |
| `Target.closeTarget` | Programmatically closes a window. | Schedules `Window.Close()` on the Avalonia UI Thread. |
| `Target.createTarget` | Opens a new blank desktop window. | Invokes the registered target factory to show a new window. |
| `Target.setAutoAttach` | Acknowledges auto-attach option. | Standard protocol return compatibility. |

---

## Deep Dive: Key Methods

### 1. Listing Targets (`Target.getTargets`)
Clients send `Target.getTargets` to retrieve the current set of debuggable windows.

#### Request Example
```json
{
  "id": 10,
  "method": "Target.getTargets",
  "params": {}
}
```

#### Response Example
```json
{
  "id": 10,
  "result": {
    "targetInfos": [
      {
        "targetId": "3a9a14d5-fb40-410a-bb79-6ea43f9a76bc",
        "type": "page",
        "title": "Main Window",
        "url": "http://localhost:9222/",
        "attached": true,
        "browserContextId": "1"
      },
      {
        "targetId": "c71e9882-baea-4b72-b5e1-0b5c156291a2",
        "type": "page",
        "title": "Settings Dialog",
        "url": "http://localhost:9222/",
        "attached": true,
        "browserContextId": "1"
      }
    ]
  }
}
```

### 2. Session Multiplexing (`Target.attachToTarget`)
Rather than opening multiple WebSockets, clients can establish a single connection to `/devtools/browser` and multiplex interactions by attaching to targets with the `flatten=true` option. The server instantiates a `CdpTargetSession` and returns a unique `sessionId`.

Subsequent JSON-RPC commands contain a `sessionId` property to tell the server which window target should receive and execute the command.

#### Request Example
```json
{
  "id": 11,
  "method": "Target.attachToTarget",
  "params": {
    "targetId": "c71e9882-baea-4b72-b5e1-0b5c156291a2",
    "flatten": true
  }
}
```

#### Response Example
```json
{
  "id": 11,
  "result": {
    "sessionId": "4b6848be-f331-419b-bc9d-b8d4157ea1b1"
  }
}
```

---

## Target Lifecycle Events

To keep remote clients informed of window dynamics in real-time, the CDP server broadcasts target events to any session that has enabled discovery via `Target.setDiscoverTargets`.

```
                  +-------------+
                  |  CdpServer  |
                  +-------------+
                         |
      [Window Opens]     | -- Target.targetCreated ------> Client
                         |
      [Window Renamed]   | -- Target.targetInfoChanged --> Client
                         |
      [Window Closes]    | -- Target.targetDestroyed ----> Client
```

### `Target.targetCreated`
Fired when a new Avalonia window is opened and registered, or instantly for all existing targets when a client calls `Target.setDiscoverTargets` with `discover=true`.

```json
{
  "method": "Target.targetCreated",
  "params": {
    "targetInfo": {
      "targetId": "c71e9882-baea-4b72-b5e1-0b5c156291a2",
      "type": "page",
      "title": "Settings Dialog",
      "url": "http://localhost:9222/",
      "attached": true,
      "browserContextId": "1"
    }
  }
}
```

### `Target.targetInfoChanged`
Fired when a target's title is modified. In `CdpServer.cs` (Chrome.DevTools.Protocol), changes to `target.Title` (e.g. updating window headers) trigger this notification.

```json
{
  "method": "Target.targetInfoChanged",
  "params": {
    "targetInfo": {
      "targetId": "c71e9882-baea-4b72-b5e1-0b5c156291a2",
      "type": "page",
      "title": "Settings - Page 2",
      "url": "http://localhost:9222/",
      "attached": true,
      "browserContextId": "1"
    }
  }
}
```

### `Target.targetDestroyed`
Fired when a window is closed and unregistered from the server.

```json
{
  "method": "Target.targetDestroyed",
  "params": {
    "targetId": "c71e9882-baea-4b72-b5e1-0b5c156291a2"
  }
}
```

---

## Coordinating with the Browser Domain (Window Bounds Control)

The `Target` domain operates alongside the `Browser` domain to provide physical window layout inspections and alterations. The browser-level window dimensions are managed in `BrowserDomain.cs`.

Because CDP methods in the `Browser` domain require a `windowId` (an integer identifier) rather than a `targetId` (a GUID string), the server coordinates them using two core steps:

### Step 1: Mapping `targetId` to `windowId`
The client requests `Browser.getWindowForTarget`, specifying the `targetId`. The server resolves the `TopLevel` window associated with that target and computes a unique integer hash:

```csharp
int windowId = Math.Abs(targetWindow.GetHashCode());
```

#### Request Example
```json
{
  "id": 12,
  "method": "Browser.getWindowForTarget",
  "params": {
    "targetId": "3a9a14d5-fb40-410a-bb79-6ea43f9a76bc"
  }
}
```

#### Response Example
```json
{
  "id": 12,
  "result": {
    "windowId": 140927891
  }
}
```

### Step 2: Querying and Modifying Window Bounds
Using the returned `windowId`, the client can call `Browser.getWindowBounds` and `Browser.setWindowBounds` to change window sizes, reposition windows on the screen, or change window states.

```csharp
if (targetWindow is Window win)
{
    win.Position = new PixelPoint(leftVal, topVal);
    win.Width = widthVal;
    win.Height = heightVal;
    win.WindowState = stateVal switch
    {
        "maximized" => WindowState.Maximized,
        "minimized" => WindowState.Minimized,
        "fullscreen" => WindowState.FullScreen,
        _ => WindowState.Normal
    };
}
```

#### Bounds Modification Request Example
```json
{
  "id": 13,
  "method": "Browser.setWindowBounds",
  "params": {
    "windowId": 140927891,
    "bounds": {
      "left": 100,
      "top": 100,
      "width": 1024,
      "height": 768,
      "windowState": "normal"
    }
  }
}
```

#### Bounds Modification Response Example
```json
{
  "id": 13,
  "result": {}
}
```

---

## Best Practices for Target Control

* **Flattened Attachments:** Always set `flatten: true` when sending `Target.attachToTarget`. This maintains full compatibility with modern CDP client frameworks.
* **Auto-Attach Handling:** If driving automated tests across popups or multi-dialog flows, enable discovery via `Target.setDiscoverTargets` and monitor the `Target.targetCreated` event streams to capture new dialog contexts as soon as they render.
* **UI Thread Awareness:** Methods that alter target window state (`Target.activateTarget`, `Target.closeTarget`, `Browser.setWindowBounds`) must yield asynchronously and run on the Avalonia UI Thread (`Dispatcher.UIThread`) to prevent cross-threading exceptions.
