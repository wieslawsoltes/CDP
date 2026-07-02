---
title: Overlay Domain
---

# Overlay Domain

The `Overlay` domain provides visual tools to highlight user interface components, draw layout bounds, show repaint rectangles, and display element tooltips. In `Avalonia.Diagnostics.Cdp`, this domain allows testing agents and inspection clients to visually inspect the bounds of controls inside the active window.

---

## Overview

The Overlay domain maps Chrome DevTools Protocol (CDP) commands to Avalonia-specific visual feedback. It exposes tools that draw color-coded boxes for margin, padding, border, and content bounds, similar to Chrome's built-in element inspector.

### Key Features
* **Element Highlighting**: Draws box model overlays directly over the targeted Avalonia `Visual`.
* **Tooltip Metadata**: Displays control type, dimensions, and Accessibility properties (such as roles and names).
* **Paint Rectangles**: Highlights dirty/invalidated layout regions during updates.
* **Inspect Mode**: Allows clicking on UI elements to inspect their properties.

---

## Method Reference

### `Overlay.enable` / `Overlay.disable`
Enables or disables the Overlay domain. These methods return empty objects for compatibility with DevTools clients.

**Request:**
```json
{
  "id": 1,
  "method": "Overlay.enable"
}
```

**Response:**
```json
{
  "id": 1,
  "result": {}
}
```

### `Overlay.highlightNode`
Highlights a specific Avalonia control in the visual tree using the provided `nodeId`. This method maps to the internal `HighlightOverlayManager.ShowHighlight` method, which dynamically injects a `HighlightAdorner` into the visual's `AdornerLayer`.

**Request Parameters:**
* `nodeId` (integer): The unique identifier of the node in the DOM tree.
* `highlightConfig` (object, optional): Configuration defining how the highlight is rendered (e.g. colors, showInfo tooltip). Currently, the server accepts these configurations gracefully to maintain protocol compatibility, while relying on optimized default colors and rendering properties.

**Request:**
```json
{
  "id": 2,
  "method": "Overlay.highlightNode",
  "params": {
    "nodeId": 42,
    "highlightConfig": {
      "showInfo": true,
      "contentColor": { "r": 120, "g": 170, "b": 240, "a": 0.25 },
      "paddingColor": { "r": 147, "g": 196, "b": 125, "a": 0.12 },
      "borderColor": { "r": 255, "g": 229, "b": 153, "a": 0.12 },
      "marginColor": { "r": 246, "g": 178, "b": 107, "a": 0.12 }
    }
  }
}
```

**Response:**
```json
{
  "id": 2,
  "result": {}
}
```

### `Overlay.hideHighlight`
Removes any active highlight from the window. This corresponds to `HighlightOverlayManager.HideHighlight`.

**Request:**
```json
{
  "id": 3,
  "method": "Overlay.hideHighlight"
}
```

**Response:**
```json
{
  "id": 3,
  "result": {}
}
```

### `Overlay.setInspectMode`
Configures whether the app should enter "Inspect Mode" (element picker mode). When enabled, hovering or tapping on elements will capture and highlight them.

**Request Parameters:**
* `mode` (string): Set to `"searchForNode"` to enable target inspection, or `"none"` to disable.

**Request:**
```json
{
  "id": 4,
  "method": "Overlay.setInspectMode",
  "params": {
    "mode": "searchForNode"
  }
}
```

**Response:**
```json
{
  "id": 4,
  "result": {}
}
```

### `Overlay.setShowPaintRects`
Enables or disables visual tracking of dirty bounds / layout invalidations. When enabled, any movement or size change in controls triggers a flashing rectangular highlight.

**Request Parameters:**
* `result` (boolean): Set `true` to show paint rectangles, `false` to disable.

**Request:**
```json
{
  "id": 5,
  "method": "Overlay.setShowPaintRects",
  "params": {
    "result": true
  }
}
```

**Response:**
```json
{
  "id": 5,
  "result": {}
}
```

---

## Architectural Details: The AdornerLayer

In Avalonia, the `AdornerLayer` is a visual container situated above normal content in the rendering hierarchy. It is traditionally used to draw visual adornments such as resize handles, focus rings, or selection borders.

`Avalonia.Diagnostics.Cdp` leverages this architecture to overlay developer tools graphics:

1. **Layer Resolution**: When a target `Visual` is highlighted, the manager calls `AdornerLayer.GetAdornerLayer(visual)`.
2. **Adorner Insertion**: A custom control (`HighlightAdorner` or `PaintRectsAdorner`) is created and appended to the `Children` collection of the `AdornerLayer`.
3. **Non-Intrusive Layout**: Both adorners set `IsHitTestVisible = false` and `ClipToBounds = false`. This guarantees that showing an overlay does not interfere with mouse input or UI hit-testing, allowing automation tests and real users to click "through" the overlay.
4. **Layout Tracking**: The adorners subscribe to the `TopLevel` window's `LayoutUpdated` event. If a control shifts or is resized, `InvalidateVisual()` is automatically called to trigger a redraw, keeping the overlay accurately aligned with the control.
5. **Session Coordination**: The `HighlightOverlayManager` keeps track of active adorners per `TopLevel` window via a concurrent dictionary. Selecting a new control automatically cleans up the previously active adorner from the layer, avoiding flickering or stacking.

---

## Box Model Overlays

The `HighlightAdorner` draws color-coded rectangles depicting the full layout properties of the target element.

### Color Visualizer Chart

| Layout Zone | Default Brush Color | Hex Representation (ARGB) | Description |
| :--- | :--- | :--- | :--- |
| **Margin** | Solid Orange (Alpha 32) | `#20F6B26B` | Outer spacing around the control bounds. |
| **Border** | Solid Yellow-green (Alpha 32) | `#20FFE599` | The space occupied by the control's border thickness. |
| **Padding** | Solid Green (Alpha 32) | `#2093C47D` | Inside padding between border and inner content. |
| **Content** | Solid Blue (Alpha 64) with outline (Alpha 200) | `#4078AAF0` / `#C878AAF0` | The primary content box of the element. |

### Box Coordinates Calculation

To draw these layout zones, `HighlightAdorner` retrieves the current control dimensions and uses reflection or layout properties:
* **Margin Box**: Computed by expanding the outer bounds by `Layoutable.Margin`.
* **Border Box**: Corresponds directly to the control's `Bounds.Width` and `Bounds.Height` positioned relative to the window coordinates via `TranslatePoint`.
* **Padding Box**: Inward bounds calculated by subtracting the resolved `BorderThickness` property (if present on the control).
* **Content Box**: The innermost region, calculated by subtracting the resolved `Padding` property.

---

## Tooltip Information

When a node is highlighted, a floating tooltip is drawn next to the element to provide instant context:

### Metadata Content
The tooltip generates a formatted text string containing:
1. **Control Type**: The concrete C# class name (e.g., `Button`, `TextBlock`).
2. **Dimensions**: The layout width and height, formatted as `WidthxHeight` (e.g. `120x35`).
3. **Accessibility Peer Details**:
   * **Role**: If a `ControlAutomationPeer` is active for the element, the automation control type (e.g. `button`, `checkbox`) is appended.
   * **Name**: Extracts the element name from the automation peer, or falls back to `AutomationProperties.GetName(visual)`.

*Example Label:*
```text
Button | 120x35 | Role: button Name: "Submit Button"
```

### Positioning Logic
To prevent the tooltip from clipping outside the visible viewport:
* It attempts to render **above** the target control (`y - text.Height - 6`).
* If the control is located at the very top edge of the window (`y - text.Height - 6 < 0`), the tooltip is automatically flipped to draw **below** the control (`y + h + 6`).
* The background uses a high-contrast dark brush `#DC212121` (Alpha 220, solid dark grey) with white text to ensure readability regardless of the application's underlying theme.

---

## Repaint Rectangles (Paint Rects)

The `Overlay.setShowPaintRects` command enables a performance debugging overlay (`PaintRectsAdorner`) that visualizes visual tree invalidations in real-time.

### Traversal and Change Detection
The `PaintRectsAdorner` recursively traverses the visual tree starting from the window's root. It maintains a dictionary of the previous bounds of all visible visual elements:
1. **Traversing**: It skips overlay layers (like `HighlightAdorner` and `PaintRectsAdorner` themselves) but walks down all visual children.
2. **Comparison**: On layout updates, it compares the current translated screen coordinates and dimensions against its stored `_previousBounds` dictionary.
3. **Detection Rules**:
   * **Size Changes**: If the bounds changed size (width or height), a paint rectangle is spawned with a **Green** color (`#4000FF00`).
   * **Position/Movement Changes**: If the control moved but kept its size, it spawns a paint rectangle with a **Red** color (`#40FF0000`).
   * **First Appearance**: If a node is newly rendered, it spawns a **Green** paint rectangle.

### Rendering and Fade-out Animation
To avoid cluttered overlays, paint rectangles utilize a transient animation:
* **Timer Loop**: A `DispatcherTimer` runs at a standard frame rate of ~60 FPS (16ms interval).
* **Age Limit**: Paint rectangles are tracked inside a list and persist for a maximum duration of **300ms**.
* **Fade Action**: In the `Render` loop, opacity is calculated linearly based on the age of the rect:
  $$\text{Opacity} = 1.0 - \left(\frac{\text{Elapsed Milliseconds}}{300.0}\right)$$
* **Cleanup**: Once a paint rectangle exceeds 300ms, it is removed from the active list. When the list is completely empty, the timer is stopped automatically to save CPU cycles.
