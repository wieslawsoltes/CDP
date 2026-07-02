---
title: Input Domain
---

# Input Domain

The `Input` domain enables programmatic simulation of user interactions—mouse events, keyboard events, text insertion, touch gestures, and scroll/pinch operations—against an Avalonia application through the Chrome DevTools Protocol. Every dispatched event is marshalled onto the Avalonia UI thread and injected into the platform input pipeline as raw input events, making the application respond exactly as it would to physical user interaction.

## Enabling and Disabling

Before the CDP server will emit `Input.mouseEvent` and `Input.keyEvent` notifications for user-originated interactions, the domain must be enabled on the session. Dispatching input (sending commands) works regardless of whether the domain is enabled, but observing input requires it.

### Input.enable

Subscribes the session to pointer and keyboard events on the target window. The server hooks Avalonia's `PointerPressed`, `PointerReleased`, `PointerMoved`, `PointerWheelChanged`, `KeyDown`, and `KeyUp` routed events at both the Tunnel and Bubble routing stages, including handled events.

```json
{ "id": 1, "method": "Input.enable" }
```

Response:

```json
{ "id": 1, "result": {} }
```

### Input.disable

Unsubscribes the session from all input event notifications and removes the event handlers from the target window.

```json
{ "id": 2, "method": "Input.disable" }
```

Response:

```json
{ "id": 2, "result": {} }
```

## Mouse Events

### Input.dispatchMouseEvent

Dispatches a synthetic mouse event into the Avalonia input pipeline. The event is processed on the UI thread as a `RawPointerEventArgs` (or `RawMouseWheelEventArgs` for scroll), passing through Avalonia's full hit-testing, routing, and event-handling chain. After dispatch, a screencast frame is automatically requested to capture the resulting visual state.

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `type` | `string` | Yes | Event type: `"mouseMoved"`, `"mousePressed"`, `"mouseReleased"`, or `"mouseWheel"` |
| `x` | `number` | Yes | X coordinate in device-independent pixels, relative to the window's top-left corner |
| `y` | `number` | Yes | Y coordinate in device-independent pixels, relative to the window's top-left corner |
| `button` | `string` | No | Mouse button: `"left"`, `"right"`, `"middle"`, or `"none"` (default: `"none"`) |
| `clickCount` | `integer` | No | Number of clicks (used by Avalonia for double-click detection) |
| `deltaX` | `number` | No | Horizontal scroll delta (only used when `type` is `"mouseWheel"`) |
| `deltaY` | `number` | No | Vertical scroll delta (only used when `type` is `"mouseWheel"`) |
| `modifiers` | `integer` | No | Bitmask of active modifier keys (see [Modifier Keys](#modifier-keys)) |
| `buttons` | `integer` | No | Bitmask of currently pressed mouse buttons: `1` = left, `2` = right, `4` = middle |

#### Mouse Move

Move the pointer to a specific position. This generates a `RawPointerEventType.Move` event in Avalonia, triggering `PointerMoved` and hover state changes (`:pointerover` pseudo-class).

```json
{
  "id": 10,
  "method": "Input.dispatchMouseEvent",
  "params": {
    "type": "mouseMoved",
    "x": 150.0,
    "y": 75.0,
    "button": "none"
  }
}
```

#### Mouse Press

Press a mouse button at the specified coordinates. Maps to `RawPointerEventType.LeftButtonDown`, `RightButtonDown`, or `MiddleButtonDown` depending on the `button` parameter.

```json
{
  "id": 11,
  "method": "Input.dispatchMouseEvent",
  "params": {
    "type": "mousePressed",
    "x": 150.0,
    "y": 75.0,
    "button": "left",
    "clickCount": 1
  }
}
```

#### Mouse Release

Release a mouse button. Maps to `RawPointerEventType.LeftButtonUp`, `RightButtonUp`, or `MiddleButtonUp`.

```json
{
  "id": 12,
  "method": "Input.dispatchMouseEvent",
  "params": {
    "type": "mouseReleased",
    "x": 150.0,
    "y": 75.0,
    "button": "left",
    "clickCount": 1
  }
}
```

#### Mouse Wheel Scroll

Dispatch a wheel scroll event. Creates a `RawMouseWheelEventArgs` in Avalonia with the specified delta vector.

```json
{
  "id": 13,
  "method": "Input.dispatchMouseEvent",
  "params": {
    "type": "mouseWheel",
    "x": 200.0,
    "y": 300.0,
    "deltaX": 0,
    "deltaY": -3
  }
}
```

Positive `deltaY` scrolls up, negative scrolls down. Positive `deltaX` scrolls right, negative scrolls left.

### CDP-to-Avalonia Mouse Event Mapping

The following table shows how CDP mouse event types map to Avalonia's raw pointer event types:

| CDP `type` | CDP `button` | Avalonia `RawPointerEventType` |
|------------|-------------|-------------------------------|
| `mouseMoved` | any | `Move` |
| `mousePressed` | `left` | `LeftButtonDown` |
| `mousePressed` | `right` | `RightButtonDown` |
| `mousePressed` | `middle` | `MiddleButtonDown` |
| `mouseReleased` | `left` | `LeftButtonUp` |
| `mouseReleased` | `right` | `RightButtonUp` |
| `mouseReleased` | `middle` | `MiddleButtonUp` |
| `mouseWheel` | — | `RawMouseWheelEventArgs` (separate type) |

When the session has touch emulation enabled (`TouchEmulationEnabled`), all `dispatchMouseEvent` calls are automatically redirected to `emulateTouchFromMouseEvent`, producing `RawTouchEventArgs` instead of `RawPointerEventArgs`:

| CDP `type` | Avalonia Touch Event Type |
|------------|--------------------------|
| `mousePressed` | `TouchBegin` |
| `mouseReleased` | `TouchEnd` |
| `mouseMoved` | `TouchUpdate` |

## Keyboard Events

### Input.dispatchKeyEvent

Dispatches a synthetic keyboard event. Key down events produce `RawKeyEventArgs` with `RawKeyEventType.KeyDown`, and if the `text` parameter is provided on a `keyDown` event, a follow-up `RawTextInputEventArgs` is also dispatched. The `char` type dispatches only a `RawTextInputEventArgs`. Key up events produce `RawKeyEventArgs` with `RawKeyEventType.KeyUp`.

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `type` | `string` | Yes | Event type: `"keyDown"`, `"rawKeyDown"`, `"keyUp"`, or `"char"` |
| `key` | `string` | No | The key value (e.g., `"Enter"`, `"a"`, `"ArrowLeft"`) |
| `code` | `string` | No | The physical key code (e.g., `"KeyA"`, `"Digit1"`, `"ShiftLeft"`) |
| `text` | `string` | No | Text generated by the key press (used for `keyDown` and `char` types) |
| `modifiers` | `integer` | No | Bitmask of active modifier keys (see [Modifier Keys](#modifier-keys)) |

The `code` parameter takes priority over `key` for resolving the Avalonia `Key` enum value. If `code` is provided and maps to a valid key, it is used; otherwise `key` is used as a fallback.

#### Key Down

```json
{
  "id": 20,
  "method": "Input.dispatchKeyEvent",
  "params": {
    "type": "keyDown",
    "key": "a",
    "code": "KeyA",
    "text": "a"
  }
}
```

When `type` is `"keyDown"` and `text` is non-empty, the server dispatches both a `RawKeyEventArgs(KeyDown)` and a `RawTextInputEventArgs` in sequence. This mirrors the behavior of a physical key press that produces a character.

#### Key Up

```json
{
  "id": 21,
  "method": "Input.dispatchKeyEvent",
  "params": {
    "type": "keyUp",
    "key": "a",
    "code": "KeyA"
  }
}
```

#### Character Input

The `"char"` type dispatches only a `RawTextInputEventArgs`, without a preceding key down event. Use this when you need to inject text characters without associated key state changes.

```json
{
  "id": 22,
  "method": "Input.dispatchKeyEvent",
  "params": {
    "type": "char",
    "text": "Hello"
  }
}
```

### Key Mapping

The CDP server maps key strings from the standard [UI Events KeyboardEvent.key](https://developer.mozilla.org/en-US/docs/Web/API/UI_Events/Keyboard_event_key_values) and [KeyboardEvent.code](https://developer.mozilla.org/en-US/docs/Web/API/UI_Events/Keyboard_event_code_values) specifications to Avalonia `Key` enum values. The mapping handles:

- **Letter keys**: `"KeyA"` through `"KeyZ"` → `Key.A` through `Key.Z`
- **Digit keys**: `"Digit0"` through `"Digit9"` → `Key.D0` through `Key.D9`; also single-character digits `"0"` through `"9"`
- **Navigation**: `"ArrowLeft"`, `"ArrowRight"`, `"ArrowUp"`, `"ArrowDown"`, `"Home"`, `"End"`, `"PageUp"`, `"PageDown"`
- **Editing**: `"Enter"`, `"Escape"`, `"Tab"`, `"Space"`, `"Backspace"`, `"Delete"`, `"Insert"`
- **Symbols**: `","` / `"Comma"`, `"."` / `"Period"`, `";"` / `"Semicolon"`, `"'"` / `"Quote"`, `"-"` / `"Minus"`, `"="` / `"Equal"`, `"["` / `"BracketLeft"`, `"]"` / `"BracketRight"`, `"\\"` / `"Backslash"`, `"/"` / `"Slash"`, `` "`" `` / `"Backquote"`
- **Modifier codes**: `"ShiftLeft"`, `"ShiftRight"`, `"ControlLeft"`, `"ControlRight"`, `"AltLeft"`, `"AltRight"`, `"MetaLeft"`, `"MetaRight"`

Any key string that does not match these explicit mappings is attempted via `Enum.TryParse<Key>` as a final fallback.

## Text Input

### Input.insertText

Inserts text directly into the focused control. This is the simplest and most reliable method for entering text into `TextBox` controls and similar text input elements.

The server creates a `RawTextInputEventArgs` and dispatches it through the platform input handler. If the input infrastructure is unavailable (e.g., headless environments), the method falls back to directly mutating the focused `TextBox`'s `Text` property, respecting the current selection range.

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `text` | `string` | Yes | The text string to insert at the current cursor position |

```json
{
  "id": 30,
  "method": "Input.insertText",
  "params": {
    "text": "Hello, World!"
  }
}
```

Response:

```json
{ "id": 30, "result": {} }
```

The fallback behavior for `insertText` works as follows:

1. The server locates the focused `TextBox` by traversing the visual tree.
2. If a selection exists (start ≠ end), the selected text is replaced with the inserted text.
3. If no selection exists, the text is inserted at the current caret position.
4. The caret position is updated to the end of the inserted text.

## Touch Emulation

### Input.emulateTouchFromMouseEvent

Converts mouse event parameters into touch events. This is useful for testing touch-based UI interactions on desktop platforms. When a session has touch emulation enabled, `dispatchMouseEvent` calls are automatically routed through this method.

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `type` | `string` | Yes | Event type: `"mousePressed"`, `"mouseReleased"`, or `"mouseMoved"` |
| `x` | `number` | Yes | X coordinate |
| `y` | `number` | Yes | Y coordinate |
| `button` | `string` | No | Mouse button (default: `"none"`) |
| `deltaX` | `number` | No | Horizontal scroll delta |
| `deltaY` | `number` | No | Vertical scroll delta |
| `modifiers` | `integer` | No | Modifier key bitmask |
| `clickCount` | `integer` | No | Number of clicks |

```json
{
  "id": 40,
  "method": "Input.emulateTouchFromMouseEvent",
  "params": {
    "type": "mousePressed",
    "x": 100.0,
    "y": 200.0,
    "button": "left",
    "clickCount": 1
  }
}
```

Touch emulation ignores `mouseMoved` events when `button` is `"none"` (no active touch contact), and routes `mouseWheel` events to the standard mouse wheel handler.

## Gesture Synthesis

### Input.synthesizeTapGesture

Synthesizes one or more tap gestures at the specified coordinates. Each tap is a press-then-release sequence with a configurable hold duration and an inter-tap delay of 100 ms.

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `x` | `number` | Yes | X coordinate of the tap |
| `y` | `number` | Yes | Y coordinate of the tap |
| `tapCount` | `integer` | No | Number of taps (default: `1`) |
| `duration` | `integer` | No | Duration in milliseconds to hold each tap (default: `50`) |
| `gestureSourceType` | `string` | No | `"touch"` (default), `"mouse"`, or `"default"` |

```json
{
  "id": 50,
  "method": "Input.synthesizeTapGesture",
  "params": {
    "x": 150.0,
    "y": 75.0,
    "tapCount": 2,
    "duration": 50,
    "gestureSourceType": "touch"
  }
}
```

When `gestureSourceType` is `"touch"` or `"default"`, the gesture uses `TouchBegin`/`TouchEnd` events. When set to `"mouse"`, it uses `LeftButtonDown`/`LeftButtonUp` events.

### Input.synthesizeScrollGesture

Synthesizes a scroll gesture over a distance at a given speed. For touch source, this produces a sequence of `TouchBegin` → interpolated `TouchUpdate` → `TouchEnd` events. For mouse source, this produces a series of wheel events.

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `x` | `number` | Yes | Starting X coordinate |
| `y` | `number` | Yes | Starting Y coordinate |
| `xDistance` | `number` | No | Horizontal scroll distance in pixels (default: `0`) |
| `yDistance` | `number` | No | Vertical scroll distance in pixels (default: `0`) |
| `speed` | `number` | No | Scroll speed in pixels per second (default: `800`) |
| `gestureSourceType` | `string` | No | `"touch"` (default), `"mouse"`, or `"default"` |

```json
{
  "id": 51,
  "method": "Input.synthesizeScrollGesture",
  "params": {
    "x": 200.0,
    "y": 300.0,
    "xDistance": 0,
    "yDistance": -200,
    "speed": 800,
    "gestureSourceType": "default"
  }
}
```

For touch gestures, the server computes the duration from the distance and speed, clamps it to 100–1000 ms, and generates at least 5 interpolation steps at ~16 ms intervals (targeting 60 fps). For mouse gestures, the distance is divided into 5 wheel events with 20 ms spacing.

### Input.synthesizePinchGesture

Synthesizes a two-finger pinch gesture centered at the specified coordinates. Only supported with touch gesture source.

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `x` | `number` | Yes | Center X coordinate of the pinch |
| `y` | `number` | Yes | Center Y coordinate of the pinch |
| `scaleFactor` | `number` | Yes | Scale factor: `>1` zooms in (spread), `<1` zooms out (pinch) |
| `relativeSpeed` | `number` | No | Speed in pixels per second (default: `800`) |
| `gestureSourceType` | `string` | No | Must be `"touch"` or `"default"` (mouse gestures are ignored) |

```json
{
  "id": 52,
  "method": "Input.synthesizePinchGesture",
  "params": {
    "x": 400.0,
    "y": 300.0,
    "scaleFactor": 2.0,
    "relativeSpeed": 800,
    "gestureSourceType": "default"
  }
}
```

The two touch points start 50 pixels to the left and right of center, and move to `50 × scaleFactor` pixels from center over the computed duration, interpolated across a minimum of 5 steps.

## Modifier Keys

Modifier keys are specified as a bitmask integer in the `modifiers` parameter, shared across mouse and keyboard events:

| Bit | Value | Modifier | Avalonia Mapping |
|-----|-------|----------|------------------|
| 0 | `1` | Alt | `RawInputModifiers.Alt` |
| 1 | `2` | Ctrl | `RawInputModifiers.Control` |
| 2 | `4` | Meta (Cmd/Win) | `RawInputModifiers.Meta` |
| 3 | `8` | Shift | `RawInputModifiers.Shift` |

Combine modifiers by summing their values. For example, Ctrl+Shift = `2 + 8 = 10`.

### Example: Ctrl+Click

```json
[
  {
    "id": 60,
    "method": "Input.dispatchMouseEvent",
    "params": {
      "type": "mouseMoved",
      "x": 200.0,
      "y": 100.0,
      "button": "none",
      "modifiers": 2
    }
  },
  {
    "id": 61,
    "method": "Input.dispatchMouseEvent",
    "params": {
      "type": "mousePressed",
      "x": 200.0,
      "y": 100.0,
      "button": "left",
      "clickCount": 1,
      "modifiers": 2
    }
  },
  {
    "id": 62,
    "method": "Input.dispatchMouseEvent",
    "params": {
      "type": "mouseReleased",
      "x": 200.0,
      "y": 100.0,
      "button": "left",
      "clickCount": 1,
      "modifiers": 2
    }
  }
]
```

### Example: Shift+Arrow Selection

```json
[
  {
    "id": 63,
    "method": "Input.dispatchKeyEvent",
    "params": {
      "type": "keyDown",
      "key": "Shift",
      "code": "ShiftLeft",
      "modifiers": 8
    }
  },
  {
    "id": 64,
    "method": "Input.dispatchKeyEvent",
    "params": {
      "type": "keyDown",
      "key": "ArrowRight",
      "code": "ArrowRight",
      "modifiers": 8
    }
  },
  {
    "id": 65,
    "method": "Input.dispatchKeyEvent",
    "params": {
      "type": "keyUp",
      "key": "ArrowRight",
      "code": "ArrowRight",
      "modifiers": 8
    }
  },
  {
    "id": 66,
    "method": "Input.dispatchKeyEvent",
    "params": {
      "type": "keyUp",
      "key": "Shift",
      "code": "ShiftLeft",
      "modifiers": 0
    }
  }
]
```

## Common Scenarios

### Simulating a Click on an Element

The standard click simulation pattern involves three steps:

1. **Get element coordinates** using `DOM.getBoxModel`
2. **Calculate the center point** from the content quad
3. **Send the event sequence**: `mouseMoved` → `mousePressed` → `mouseReleased`

#### Step 1: Find the element and get its box model

```json
{ "id": 100, "method": "DOM.getDocument", "params": { "depth": 0 } }
```

```json
{ "id": 101, "method": "DOM.querySelector", "params": { "nodeId": 1, "selector": "#btnSubmit" } }
```

Suppose this returns `{"nodeId": 42}`.

```json
{ "id": 102, "method": "DOM.getBoxModel", "params": { "nodeId": 42 } }
```

Response:

```json
{
  "id": 102,
  "result": {
    "model": {
      "content": [60, 100, 200, 100, 200, 140, 60, 140],
      "width": 140,
      "height": 40
    }
  }
}
```

The `content` array contains four corner points as `[x1, y1, x2, y2, x3, y3, x4, y4]`. Calculate the center:

```
centerX = (60 + 200 + 200 + 60) / 4 = 130
centerY = (100 + 100 + 140 + 140) / 4 = 120
```

#### Step 2: Dispatch the click sequence

```json
{
  "id": 103,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseMoved", "x": 130.0, "y": 120.0, "button": "none" }
}
```

```json
{
  "id": 104,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mousePressed", "x": 130.0, "y": 120.0, "button": "left", "clickCount": 1 }
}
```

```json
{
  "id": 105,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseReleased", "x": 130.0, "y": 120.0, "button": "left", "clickCount": 1 }
}
```

### Simulating Text Entry

The recommended approach for text entry combines `DOM.focus` with `Input.insertText`:

#### Step 1: Focus the target text input

```json
{ "id": 110, "method": "DOM.getDocument", "params": { "depth": 0 } }
```

```json
{ "id": 111, "method": "DOM.querySelector", "params": { "nodeId": 1, "selector": "#txtUsername" } }
```

```json
{ "id": 112, "method": "DOM.focus", "params": { "nodeId": 49 } }
```

#### Step 2: Insert text

```json
{ "id": 113, "method": "Input.insertText", "params": { "text": "admin@example.com" } }
```

#### Alternative: Character-by-character input

For scenarios requiring individual key events (e.g., autocomplete triggers, key filters):

```json
[
  { "id": 120, "method": "Input.dispatchKeyEvent", "params": { "type": "keyDown", "key": "H", "code": "KeyH", "text": "H", "modifiers": 8 } },
  { "id": 121, "method": "Input.dispatchKeyEvent", "params": { "type": "keyUp", "key": "H", "code": "KeyH", "modifiers": 8 } },
  { "id": 122, "method": "Input.dispatchKeyEvent", "params": { "type": "keyDown", "key": "e", "code": "KeyE", "text": "e" } },
  { "id": 123, "method": "Input.dispatchKeyEvent", "params": { "type": "keyUp", "key": "e", "code": "KeyE" } },
  { "id": 124, "method": "Input.dispatchKeyEvent", "params": { "type": "keyDown", "key": "l", "code": "KeyL", "text": "l" } },
  { "id": 125, "method": "Input.dispatchKeyEvent", "params": { "type": "keyUp", "key": "l", "code": "KeyL" } },
  { "id": 126, "method": "Input.dispatchKeyEvent", "params": { "type": "keyDown", "key": "l", "code": "KeyL", "text": "l" } },
  { "id": 127, "method": "Input.dispatchKeyEvent", "params": { "type": "keyUp", "key": "l", "code": "KeyL" } },
  { "id": 128, "method": "Input.dispatchKeyEvent", "params": { "type": "keyDown", "key": "o", "code": "KeyO", "text": "o" } },
  { "id": 129, "method": "Input.dispatchKeyEvent", "params": { "type": "keyUp", "key": "o", "code": "KeyO" } }
]
```

### Simulating a Right-Click (Context Menu)

```json
{
  "id": 130,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseMoved", "x": 250.0, "y": 180.0, "button": "none" }
}
```

```json
{
  "id": 131,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mousePressed", "x": 250.0, "y": 180.0, "button": "right", "clickCount": 1 }
}
```

```json
{
  "id": 132,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseReleased", "x": 250.0, "y": 180.0, "button": "right", "clickCount": 1 }
}
```

### Simulating a Double-Click

```json
{
  "id": 140,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mousePressed", "x": 150.0, "y": 75.0, "button": "left", "clickCount": 1 }
}
```

```json
{
  "id": 141,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseReleased", "x": 150.0, "y": 75.0, "button": "left", "clickCount": 1 }
}
```

```json
{
  "id": 142,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mousePressed", "x": 150.0, "y": 75.0, "button": "left", "clickCount": 2 }
}
```

```json
{
  "id": 143,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseReleased", "x": 150.0, "y": 75.0, "button": "left", "clickCount": 2 }
}
```

### Simulating Drag and Drop

Drag and drop requires a sequence of mouse events: press at the source, move through intermediate points (to trigger drag detection), and release at the target.

#### Step 1: Get source and target coordinates

Use `DOM.querySelector` and `DOM.getBoxModel` to find the center points of the drag source and drop target elements.

#### Step 2: Initiate the drag

```json
{
  "id": 150,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseMoved", "x": 80.0, "y": 200.0, "button": "none" }
}
```

```json
{
  "id": 151,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mousePressed", "x": 80.0, "y": 200.0, "button": "left", "clickCount": 1 }
}
```

#### Step 3: Move through intermediate points

Avalonia requires actual pointer movement to detect a drag operation. Send several `mouseMoved` events with the `buttons` field set to `1` (left button held) or the `button` field set to `"left"` to maintain the drag state:

```json
{
  "id": 152,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseMoved", "x": 120.0, "y": 200.0, "button": "left", "buttons": 1 }
}
```

```json
{
  "id": 153,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseMoved", "x": 200.0, "y": 200.0, "button": "left", "buttons": 1 }
}
```

```json
{
  "id": 154,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseMoved", "x": 300.0, "y": 200.0, "button": "left", "buttons": 1 }
}
```

```json
{
  "id": 155,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseMoved", "x": 400.0, "y": 200.0, "button": "left", "buttons": 1 }
}
```

#### Step 4: Release at the drop target

```json
{
  "id": 156,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mouseReleased", "x": 400.0, "y": 200.0, "button": "left", "clickCount": 1 }
}
```

When sending `mouseMoved` events during a drag, the `button` parameter set to `"left"` (or the `buttons` bitmask with bit 0 set) causes the server to include `RawInputModifiers.LeftMouseButton` in the modifier flags, which is essential for Avalonia to recognize the pointer movement as a drag operation rather than a hover.

### Keyboard Shortcuts

#### Ctrl+A (Select All)

```json
{ "id": 160, "method": "Input.dispatchKeyEvent", "params": { "type": "keyDown", "key": "a", "code": "KeyA", "modifiers": 2 } }
```

```json
{ "id": 161, "method": "Input.dispatchKeyEvent", "params": { "type": "keyUp", "key": "a", "code": "KeyA", "modifiers": 0 } }
```

#### Ctrl+C (Copy)

```json
{ "id": 162, "method": "Input.dispatchKeyEvent", "params": { "type": "keyDown", "key": "c", "code": "KeyC", "modifiers": 2 } }
```

```json
{ "id": 163, "method": "Input.dispatchKeyEvent", "params": { "type": "keyUp", "key": "c", "code": "KeyC", "modifiers": 0 } }
```

#### Enter Key

```json
{ "id": 164, "method": "Input.dispatchKeyEvent", "params": { "type": "keyDown", "key": "Enter", "code": "Enter" } }
```

```json
{ "id": 165, "method": "Input.dispatchKeyEvent", "params": { "type": "keyUp", "key": "Enter", "code": "Enter" } }
```

#### Escape Key

```json
{ "id": 166, "method": "Input.dispatchKeyEvent", "params": { "type": "keyDown", "key": "Escape", "code": "Escape" } }
```

```json
{ "id": 167, "method": "Input.dispatchKeyEvent", "params": { "type": "keyUp", "key": "Escape", "code": "Escape" } }
```

#### Tab Key

```json
{ "id": 168, "method": "Input.dispatchKeyEvent", "params": { "type": "keyDown", "key": "Tab", "code": "Tab" } }
```

```json
{ "id": 169, "method": "Input.dispatchKeyEvent", "params": { "type": "keyUp", "key": "Tab", "code": "Tab" } }
```

## Input Event Notifications

When the `Input` domain is enabled, the server emits event notifications for user-originated interactions on the target window. These events include the Avalonia selector of the control under the pointer (or focused control for keyboard events), enabling recording and replay scenarios.

### Input.mouseEvent

Emitted for pointer interactions.

```json
{
  "method": "Input.mouseEvent",
  "params": {
    "type": "mousePressed",
    "x": 150.0,
    "y": 75.0,
    "button": "left",
    "clickCount": 1,
    "selector": "#btnSubmit"
  }
}
```

The `selector` field contains the CSS-like selector generated by `SelectorEngine.GetSelector` for the visual (or logical) tree element at the pointer position, resolved via `InputHitTest`.

### Input.keyEvent

Emitted for keyboard interactions.

```json
{
  "method": "Input.keyEvent",
  "params": {
    "type": "keyDown",
    "key": "Enter",
    "selector": "#txtSearch"
  }
}
```

The `selector` field identifies the currently focused element at the time of the key event.

## Other Methods

### Input.setIgnoreInputEvents

Accepted but currently a no-op. This method exists for protocol compatibility.

```json
{ "id": 200, "method": "Input.setIgnoreInputEvents", "params": { "ignore": true } }
```

## Implementation Details

### Thread Safety

All input dispatch methods use `Dispatcher.UIThread.InvokeAsync` to ensure events are processed on the Avalonia UI thread. This guarantees thread-safe access to the visual tree and correct event routing.

### Screencast Integration

After every dispatched input event, the session calls `RequestScreencastFrame()` to capture the resulting visual state. This enables real-time visual feedback when driving the application through CDP, and is essential for recording and video generation in the inspector's Test Studio.

### Input Pipeline

CDP input events bypass the platform windowing system and are injected directly into Avalonia's raw input pipeline via the `TopLevel.PlatformImpl.Input` handler. This means:

- Events are processed identically to platform-originated events.
- Full Avalonia event routing (tunnel → bubble) is preserved.
- Hit testing, focus management, and control state transitions work correctly.
- Animations, visual state changes, and data bindings react to the input.

### Session State Management

Input event subscriptions are tracked per-session using `WeakReference<CdpSession>` and `WeakReference<TopLevel>`. If either the session or the window is garbage-collected, the event handlers automatically unhook on the next event delivery, preventing memory leaks.
