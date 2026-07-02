---
title: Window Chrome Domain
---

# Window Chrome Domain

The Window Chrome domain provides programmatic control over Avalonia window properties and state. It enables agents and automation scripts to manage window position, size, opacity, title, and lifecycle through CDP commands.

## Overview

Unlike browser CDP where `Browser.getWindowForTarget` provides limited window info, the Window Chrome domain offers full control over Avalonia desktop windows:

| Method | Description |
|---|---|
| `setTopmost` | Pin/unpin window above all others |
| `setOpacity` | Change window transparency |
| `setTitle` | Update the window title bar |
| `dragWindow` | Move window by delta coordinates |
| `minimize` | Minimize to taskbar |
| `maximize` | Maximize to fill screen |
| `restore` | Restore to normal state |
| `close` | Close the window |
| `activate` | Bring window to foreground |
| `getWindowDetails` | Query all window properties |

## Window Targeting

All methods accept an optional `windowId` parameter. If omitted, the command targets the session's connected window. To target a different window, pass its hash-based ID:

```json
{
  "id": 1,
  "method": "WindowChrome.getWindowDetails",
  "params": {
    "windowId": 12345
  }
}
```

## Methods

### WindowChrome.setTopmost

Pin a window so it stays above all other windows:

```json
{
  "id": 1,
  "method": "WindowChrome.setTopmost",
  "params": {
    "topmost": true
  }
}
```

**Response:**

```json
{
  "id": 1,
  "result": {
    "success": true
  }
}
```

### WindowChrome.setOpacity

Set the window's opacity level (0.0 = fully transparent, 1.0 = fully opaque):

```json
{
  "id": 2,
  "method": "WindowChrome.setOpacity",
  "params": {
    "opacity": 0.75
  }
}
```

### WindowChrome.setTitle

Update the window's title text:

```json
{
  "id": 3,
  "method": "WindowChrome.setTitle",
  "params": {
    "title": "My Application - Editing"
  }
}
```

This also updates the CDP target list title via `CdpServer.UpdateTitle`.

### WindowChrome.dragWindow

Move the window by a pixel delta from its current position:

```json
{
  "id": 4,
  "method": "WindowChrome.dragWindow",
  "params": {
    "deltaX": 100,
    "deltaY": -50
  }
}
```

### WindowChrome.minimize

Minimize the window to the taskbar/dock:

```json
{
  "id": 5,
  "method": "WindowChrome.minimize"
}
```

### WindowChrome.maximize

Maximize the window to fill the screen:

```json
{
  "id": 6,
  "method": "WindowChrome.maximize"
}
```

### WindowChrome.restore

Restore the window from minimized or maximized state to normal:

```json
{
  "id": 7,
  "method": "WindowChrome.restore"
}
```

### WindowChrome.close

Close the window:

```json
{
  "id": 8,
  "method": "WindowChrome.close"
}
```

:::warning
Closing the last window may terminate the application. The CDP session will be disconnected after this call.
:::

### WindowChrome.activate

Bring the window to the foreground and give it focus:

```json
{
  "id": 9,
  "method": "WindowChrome.activate"
}
```

### WindowChrome.getWindowDetails

Query the current window state and properties:

```json
{
  "id": 10,
  "method": "WindowChrome.getWindowDetails"
}
```

**Response:**

```json
{
  "id": 10,
  "result": {
    "success": true,
    "topmost": false,
    "opacity": 1.0,
    "title": "My Application",
    "windowState": "normal"
  }
}
```

Window state values: `"normal"`, `"maximized"`, `"minimized"`, `"fullscreen"`.

## Usage Scenarios

### Multi-Window Arrangement

Position multiple windows for side-by-side comparison:

```json
// Window 1: left half
{"id": 1, "method": "Emulation.setDeviceMetricsOverride", "params": {"width": 960, "height": 1080}}
{"id": 2, "method": "WindowChrome.dragWindow", "params": {"deltaX": -500, "deltaY": 0}}

// Window 2: right half (different session)
{"id": 1, "method": "Emulation.setDeviceMetricsOverride", "params": {"width": 960, "height": 1080}}
{"id": 2, "method": "WindowChrome.dragWindow", "params": {"deltaX": 500, "deltaY": 0}}
```

### Always-on-Top Overlay

Create a persistent overlay window:

```json
{"id": 1, "method": "WindowChrome.setTopmost", "params": {"topmost": true}}
{"id": 2, "method": "WindowChrome.setOpacity", "params": {"opacity": 0.85}}
```

### Automated Window Lifecycle

```json
{"id": 1, "method": "WindowChrome.maximize"}
{"id": 2, "method": "Page.captureScreenshot"}
{"id": 3, "method": "WindowChrome.restore"}
{"id": 4, "method": "WindowChrome.minimize"}
// Wait...
{"id": 5, "method": "WindowChrome.restore"}
{"id": 6, "method": "WindowChrome.activate"}
```

## Next Steps

- [Emulation Domain](/articles/emulation-domain) — Viewport and theme emulation
- [Target Domain](/articles/target-domain) — Multi-window target discovery
- [Page Domain](/articles/page-domain) — Screenshots and screencast
