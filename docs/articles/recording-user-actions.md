---
title: Recording User Actions
---

# Recording User Actions

This article provides a detailed breakdown of how the CDP Recorder intercepts user interactions in a live Avalonia application and translates them into logical, replayable test steps. Understanding these mechanics helps you design applications that produce clean, stable recordings.

## Event Interception Pipeline

The recorder attaches tunneled routed event handlers to the target `Window`, ensuring it captures interactions before any control handles them. The following events are intercepted:

| Avalonia Event | Routing Strategy | Purpose |
|---|---|---|
| `PointerPressedEvent` | Tunnel | Begin click/drag tracking |
| `PointerMovedEvent` | Tunnel | Detect drag threshold |
| `PointerReleasedEvent` | Tunnel | Emit click or dragAndDrop step |
| `PointerWheelChangedEvent` | Tunnel | Emit scroll step |
| `GotFocusEvent` | Bubble + Tunnel | Snapshot TextBox initial text |
| `LostFocusEvent` | Bubble + Tunnel | Compare text and emit change step |
| `KeyDownEvent` | Tunnel | Capture special key presses |

All handlers are registered with `handledEventsToo: true`, meaning the recorder captures interactions even when a control marks the event as handled.

:::info
Recording is automatically paused when the CDP session has inspect mode enabled (`session.InspectModeEnabled`). This prevents the Overlay highlight interactions from being recorded as test steps.
:::

## Click Resolution

When the user releases the pointer (completing a click), the recorder follows a multi-step resolution process:

### 1. Hit Testing

The recorder performs an `InputHitTest` at the pointer position relative to the window, finding the deepest visual element under the cursor:

```csharp
var hit = session.Window.InputHitTest(e.GetPosition(session.Window)) as Visual;
```

### 2. Logical Node Resolution

The hit visual is mapped to its logical parent control using `FindLogicalNode`. This step ensures that template internals (like a `TextBlock` inside a `Button`) resolve to the meaningful control:

```
Visual hit: TextBlock "Click Me"
    → Logical: ContentPresenter
        → Logical parent: Button#btnSubmit
```

### 3. Coordinate Calculation

Click coordinates are recorded relative to the resolved control, not the window. This makes steps resilient to layout changes:

```json
{
  "offsetX": 42.0,
  "offsetY": 12.0
}
```

### 4. Button and Modifier Detection

The recorder inspects `PointerUpdateKind` to determine the button type and reads `KeyModifiers` for modifier flags:

```csharp
string button = "left";
if (point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
    button = "right";
else if (point.Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonReleased)
    button = "middle";
```

### 5. Click Count

Double-clicks and triple-clicks are captured through the `ClickCount` property of `PointerPressedEventArgs`, preserving multi-click sequences in the recording.

## Drag and Drop Detection

The recorder distinguishes between clicks and drags using a 10-pixel movement threshold:

1. On `PointerPressed`, the recorder records the start control and position
2. On `PointerMoved`, if the pointer has moved more than 10 pixels from the start position, `_isDragging` is set to `true`
3. On `PointerReleased`:
   - If `_isDragging` is `false`, a `click` step is emitted
   - If `_isDragging` is `true`, a `dragAndDrop` step is emitted with source and target selectors

The drag step includes both the source control (where the drag started) and the target control (where the pointer was released):

```json
{
  "type": "dragAndDrop",
  "selectors": [["#sourceItem"]],
  "targetSelectors": [["#dropZone"]],
  "offsetX": 10.0,
  "offsetY": 8.0,
  "targetOffsetX": 50.0,
  "targetOffsetY": 25.0
}
```

## Text Input Capture

Unlike browser recorders that capture individual keystrokes, the CDP Recorder uses a **focus-based diff strategy** for text input:

### Capture Flow

**Text input capture sequence:**

1. **User** → **TextBox**: Focus (click/tab)
2. **Recorder** → **Recorder**: Snapshot Text → "initial value"
3. **User** → **TextBox**: Type "new text"
4. **User** → **TextBox**: Blur (tab away / click elsewhere)
5. **Recorder** → **Recorder**: Compare "new text" ≠ "initial value"
6. **Recorder** → **Recorder**: Emit change step with "new text"

### Why Focus-Based Capture?

- **Resilience**: Intermediate keystrokes, autocomplete, paste operations, and IME composition all produce the same final `change` step
- **Simplicity**: One step per text field edit instead of dozens of keystroke steps
- **Accuracy**: The recorded value exactly matches what the user sees after editing

### TextBox Discovery

The recorder walks the visual tree upward from the focused element to find the nearest `TextBox`:

```csharp
private static TextBox? FindTextBox(object? source)
{
    if (source is TextBox tb) return tb;
    if (source is Visual visual)
    {
        Visual? current = visual.GetVisualParent();
        while (current != null)
        {
            if (current is TextBox textBox) return textBox;
            current = current.GetVisualParent();
        }
    }
    return null;
}
```

This handles cases where focus events originate from inner template parts (like the `TextPresenter` inside a `TextBox`).

## Scroll Capture

Pointer wheel events are captured per control with delta values scaled by 100× for compatibility with browser automation tools:

```csharp
var step = new JsonObject
{
    ["type"] = "scroll",
    ["selectors"] = new JsonArray { new JsonArray { selector } },
    ["deltaX"] = delta.X * 100.0,
    ["deltaY"] = delta.Y * 100.0
};
```

Only non-zero deltas are recorded, filtering out spurious zero-delta events.

## Special Key Capture

The recorder captures special keys that have structural meaning in test flows:

| Key | Use Case |
|-----|----------|
| Enter | Form submission, dialog confirmation |
| Escape | Dialog dismissal, cancel operations |
| Tab | Focus navigation between fields |
| Backspace / Delete | Text editing |
| Arrow Keys | List navigation, slider adjustment |

Regular character keys are not individually captured — they are covered by the text change mechanism described above.

## Coordinate Scaling for Preview Recording

When recording interactions through the Inspector's Simulation panel preview image (rather than directly on the target application), coordinate scaling is required:

1. The Inspector captures mouse events on the `#imgScreenshot` preview image
2. These coordinates are relative to the displayed image bounds
3. The Inspector scales them to the target application's actual viewport dimensions
4. The scaled coordinates are injected into the target via `Input.dispatchMouseEvent`
5. The Recorder captures these injected events normally

This allows an agent or user to record through the Inspector's preview without directly touching the target application window.

## Selector Stability Best Practices

To produce clean, maintainable recordings:

| Practice | Benefit |
|----------|---------|
| Assign `Name` to interactive controls | Produces `#btnSubmit` selectors |
| Use `AutomationProperties.AutomationId` | Enables `[AutomationId="submit"]` selectors |
| Keep names stable across refactors | Recorded tests survive UI changes |
| Avoid deep nesting without names | Prevents fragile structural selectors |

:::warning
Controls without `Name` or `AutomationId` produce type-path selectors like `Window > Grid > StackPanel > Button`, which break when the layout changes. Always name controls that users interact with.
:::

## Next Steps

- [Recorder Overview](/articles/recorder-overview) — Architecture and CDP protocol reference
- [Test Studio](/articles/test-studio) — Visual editing and replay workspace
- [YAML Test Format](/articles/yaml-test-format) — Step serialization specification
- [Selector Engine](/articles/selector-engine) — Full selector syntax reference
