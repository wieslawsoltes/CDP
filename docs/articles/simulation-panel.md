---
title: Simulation Panel
description: Detailed documentation on the Simulation Panel in CDP Inspector, including live screencast double-buffering, pointer coordinate mapping, key injection, device viewport presets, and network emulation.
---

# Simulation Panel

The **Simulation Panel** is the primary visual workspace within the CDP Inspector. It provides developers and automated agents with a real-time, interactive viewport to see and control the target Avalonia application. Instead of forcing manual layout inspection via static control trees, the Simulation Panel bridges the gap between client and target. It establishes a live screencast of the target window and intercepts local input events to inject them directly into the target application.

This article details the underlying architecture, coordination protocols, and manual control overrides that enable real-time UI simulation.

---

## 1. Overview of the Simulation Workspace

The simulation interface is defined across several key files in the inspector client:
*   **View Layer**: `SimulationView.axaml` constructs the visual layout, housing the navigation bar, emulation presets bar, the screenshot canvas, and advanced manual injection expanders.
*   **Code-Behind**: `SimulationView.axaml.cs` captures pointer, wheel, and keyboard interactions from the local desktop UI and translates them to target coordinates.
*   **ViewModel**: `SimulationViewModel.cs` drives the state machine, manages viewport bounds, coordinates device emulation metrics, and transmits JSON-RPC commands via the CDP service.

The overall topology of the simulation connection is depicted below:

```text
+------------------------------------+          CDP Over WebSocket          +------------------------------------+
|         CDP Inspector App          | <==================================> |         Target Avalonia App        |
|  - SimulationView (Image Canvas)   |  - Page.startScreencast / Frames     |  - Embedded CDP Server             |
|  - Pointer Coordinates Translation |  - Input.dispatchMouseEvent          |  - Native Window Render Capture    |
|  - Modifier Mapping & Device Emul  |  - Emulation.setDeviceMetrics        |  - Input Injection & Event Loop    |
+------------------------------------+                                      +------------------------------------+
```

---

## 2. Screencasting Architecture

To display the target application's window in real-time, the inspector leverages the `Page` protocol domain.

### Enabling the Screencast
When the inspector connects to a target, the `SimulationViewModel` issues the following protocol commands:
1.  **Page.enable**: Activates frame notifications and page lifecycle events.
2.  **Page.startScreencast**: Requests that the target application capture its window contents periodically and push them as base64-encoded frames.

```json
{
  "id": 984,
  "method": "Page.startScreencast",
  "params": {
    "format": "png",
    "everyNthFrame": 1,
    "transferMode": "tiled"
  }
}
```

The parameters configure:
*   `format`: The compression format (e.g., `png` or `jpeg`).
*   `everyNthFrame`: Controls the frame-rate limit (setting it to `1` captures every single rendering frame).
*   `transferMode`: Sets the transfer protocol. Specifying `tiled` optimizes network throughput by transmitting partial image tiles instead of the entire frame when only a subset of the screen changes.

### WriteableBitmap Double-Buffering
To prevent UI thread bottlenecks and image tearing, the `SimulationViewModel` utilizes a double-buffered `WriteableBitmap` layout. When running in `tiled` mode, the inspector maintains two internal bitmaps:
- `_tiledBitmap1`
- `_tiledBitmap2`

These buffers match the target's current pixel dimensions and color format (either `Rgba8888` or `Bgra8888` depending on system SkiaSharp configurations).

When a new frame is received via the `Page.screencastFrame` event:
1.  The `ScreencastReconstructor` service updates the backing pixel data.
2.  The view model switches the active buffer pointer (`_useBitmap1 = !_useBitmap1`).
3.  The inactive bitmap is locked using `targetBitmap.Lock()` to write the updated raw pixels.
4.  The `ScreenshotImage` property binding is updated, notifying the Avalonia rendering loop to swap the displayed image.
5.  An acknowledgment message (`Page.screencastFrameAck`) is sent back to the target with the matching `sessionId` to trigger the next frame capture.

```csharp
if (sessionId != 0)
{
    _ = _cdpService.SendCommandAsync("Page.screencastFrameAck", new JsonObject 
    { 
        ["sessionId"] = sessionId 
    });
}
```

---

## 3. Interactive Coordinates and Input Injection

The Simulation Panel is not a static viewer; it acts as a transparent interaction proxy. Standard mouse gestures performed on the preview image are mapped and sent to the target app.

### Coordinate Translation
The preview image inside the inspector might be scaled or fit to a container, meaning its layout bounds (in device-independent pixels) do not match the target's physical coordinate system.
When an Avalonia pointer event (e.g., `PointerPressed`) fires on `<Image Name="imgScreenshot">`, the coordinates are mapped as follows:

1.  Retrieve the pointer's relative position on the image element:
    ```csharp
    var pointerPoint = e.GetCurrentPoint(img);
    var pos = pointerPoint.Position;
    ```
2.  Read the layout bounds of the image element (`img.Bounds.Width`, `img.Bounds.Height`).
3.  Read the device coordinates (`DeviceWidth`, `DeviceHeight`) received in the screencast frame metadata.
4.  Scale the coordinates:
    ```csharp
    double targetX = pos.X * (simVm.DeviceWidth / imageWidth);
    double targetY = pos.Y * (simVm.DeviceHeight / imageHeight);
    ```

### Mouse Event Dispatch
Once scaled, the coordinates are packaged into an `Input.dispatchMouseEvent` call. The inspector dispatches the following mouse interactions:
*   `mouseMoved`: Triggered when the pointer slides across the preview. To prevent event flooding, a movement threshold of at least 2 pixels is enforced before issuing a CDP command.
*   `mousePressed` / `mouseReleased`: Triggered on clicks. The pointer update kind is mapped to `"left"`, `"right"`, or `"middle"`.
*   `mouseWheel`: Triggered by scroll gestures, translating the wheel delta to vertical and horizontal scrolling bounds.

```json
{
  "id": 13,
  "method": "Input.dispatchMouseEvent",
  "params": {
    "type": "mousePressed",
    "x": 245.5,
    "y": 112.0,
    "button": "left",
    "clickCount": 1,
    "modifiers": 2,
    "buttons": 1
  }
}
```

#### Modifier Key Mapping
Keyboard modifiers active during the mouse gesture are packed into a bitmask integer:
*   `Alt`: `1` (bit 0)
*   `Control`: `2` (bit 1)
*   `Meta` (Command on macOS): `4` (bit 2)
*   `Shift`: `8` (bit 3)

These bits are combined using bitwise OR (`modifiers |= value`) and sent in the `modifiers` parameter, ensuring shortcuts (like Ctrl+Click) are preserved on the target.

### Keyboard Injection
To type into text boxes on the target app:
1.  The user focuses the text field by clicking the element center in the simulation viewport.
2.  Pressing keys inside the simulation panel border fires key listeners (`Border_KeyDown` and `Border_KeyUp`).
3.  Virtual keys are mapped using the `MapKey` helper to match web-compatible virtual key strings (e.g., mapping `Key.Back` to `"Backspace"`, `Key.Left` to `"ArrowLeft"`).
4.  The inspector dispatches `rawKeyDown` and `keyUp` actions using `Input.dispatchKeyEvent`.
5.  Text entry is injected via the `Border_TextInput` hook, which issues the `Input.insertText` command containing the literal string characters.

---

## 4. Viewport Resolution Resizing Presets

To test responsive design layouts and verify how window controls behave on different displays, the simulation bar includes a device emulation panel.

Users can choose from several pre-configured target metrics, or input custom widths and heights:

| Preset Name | Target Width | Target Height | Scale Factor | Mobile Emulation |
| :--- | :---: | :---: | :---: | :---: |
| **Responsive** | *Dynamic* | *Dynamic* | 1.0 | No |
| **iPhone SE** | 375 | 667 | 2.0 | Yes |
| **iPhone 12 Pro** | 390 | 844 | 3.0 | Yes |
| **Pixel 5** | 393 | 851 | 2.75 | Yes |
| **iPad Air** | 820 | 1180 | 2.0 | Yes |
| **Desktop (1080p)** | 1920 | 1080 | 1.0 | No |

### Setting Emulation Override
When a preset is applied, `SimulationViewModel` sends `Emulation.setDeviceMetricsOverride` over CDP:

```json
{
  "id": 972,
  "method": "Emulation.setDeviceMetricsOverride",
  "params": {
    "width": 390,
    "height": 844,
    "deviceScaleFactor": 3.0,
    "mobile": true
  }
}
```

This forces the target Avalonia window wrapper to resize its layout canvas. The target's embedded rendering pipeline re-flows the components according to the new resolution, and subsequent screencast frames reflect the emulated display dimensions.

---

## 5. Network Throttling Emulation

The Simulation Panel integrates network control capabilities to test how applications handle slow connections, packet delays, or sudden offline transitions. These conditions are handled by the `NetworkViewModel.cs` backend and rendered through the `NetworkView.axaml` dropdown interface.

Throttling rules are sent to the target's HTTP handler wrapper via the `Network.emulateNetworkConditions` command.

### Throttling Profiles
The following network profiles are supported:

1.  **No Throttling**: Disables network delays and throughput constraints (default state).
2.  **Fast 3G**: Emulates a typical high-latency mobile connection.
    *   Latency: `100 ms`
    *   Download Throughput: `200,000 bytes/sec` (~1.6 Mbps)
    *   Upload Throughput: `96,000 bytes/sec` (~768 Kbps)
3.  **Slow 3G**: Emulates poor mobile signal conditions.
    *   Latency: `400 ms`
    *   Download Throughput: `47,000 bytes/sec` (~376 Kbps)
    *   Upload Throughput: `47,000 bytes/sec` (~376 Kbps)
4.  **Offline**: Disconnects all outbound HTTP and resource requests on the target's network interface.
    *   Offline Flag: `true`
    *   Latency: `0 ms`
    *   Throughput: `0 bytes/sec`

### Applying Network Throttling
When a profile is selected in the network dropdown, the following message is dispatched:

```json
{
  "id": 522,
  "method": "Network.emulateNetworkConditions",
  "params": {
    "offline": false,
    "latency": 100.0,
    "downloadThroughput": 200000.0,
    "uploadThroughput": 96000.0
  }
}
```

This ensures that the target's external asset fetches, database requests, and API calls suffer the configured latency and speed caps, matching real-world performance.

---

## 6. Advanced Manual Interaction Tools

For scenarios where automated pointer events are insufficient, the Simulation Panel features a collapsible **Advanced Manual Interaction Tools** panel. This contains separate controls to:
*   **Click Selected Element Center**: Issues a click precisely at the center coordinates of the element selected in the DOM Tree panel.
*   **Send Text**: Directs the target to insert a literal string block without triggering key-up/down overhead.
*   **Send Key / Key Modifiers**: Allows sending isolated control keys (like Tab, Backspace, Escape, Arrow keys) with checkboxes for manual modifier combinations (`Ctrl`, `Shift`, `Alt`, `Meta`).
*   **Manual Coordinate Input**: Allows entering numeric `X` and `Y` coordinate text strings to trigger precise mouse drags (`Drag End X`, `Drag End Y`), movements, and multi-click behaviors.

---

## Related Topics

- [Emulation Domain](/articles/emulation-domain) — Viewport override, theme switching, and locale emulation via CDP
- [Window Chrome Domain](/articles/window-chrome-domain) — Window management (topmost, opacity, position, minimize/maximize)
- [Input Domain](/articles/input-domain) — Low-level mouse, keyboard, and touch event dispatch
- [Page Domain](/articles/page-domain) — Screenshots and screencast capture
- [Recorder Overview](/articles/recorder-overview) — Recording interactions for test automation

