---
title: Recorder Overview
---

# Recorder Overview

The CDP Recorder is a built-in interaction capture system that intercepts real user interactions with an Avalonia application and translates them into structured, replayable test steps. It operates at the protocol level through the `Recorder` CDP domain and integrates tightly with the Inspector's Test Studio for visual test authoring, editing, and execution.

## Architecture

The Recorder architecture spans two layers:

1. **Protocol Layer** — The `RecorderDomain` in `Avalonia.Diagnostics.Cdp` attaches event handlers to the target window and emits `Recorder.stepAdded` CDP events for each captured interaction.
2. **Inspector Layer** — The `RecorderViewModel` in `CDP.Inspector.Shared` listens for these events, translates them into `TestStudioStepModel` instances, and feeds them to the Test Studio for editing, replay, and code generation.

**Recording event flow:**

1. **User** → **Target App (CDP Server)**: Click / Type / Scroll
2. **Target App** → **RecorderDomain**: Tunneled routed event
3. **RecorderDomain** → **RecorderDomain**: Resolve control, generate selector
4. **RecorderDomain** → **Inspector (RecorderViewModel)**: `Recorder.stepAdded` (WebSocket)
5. **Inspector** → **Test Studio**: Add `TestStudioStepModel`
6. **Test Studio** → **Test Studio**: Update YAML, step list UI

## Starting and Stopping Recording

### Via CDP Protocol

Recording is controlled through two methods on the `Recorder` domain:

**Start recording:**

```json
{
  "id": 1,
  "method": "Recorder.start",
  "params": {
    "selectorMode": "automation"
  }
}
```

The optional `selectorMode` parameter controls how selectors are generated:
- `"automation"` — Prefer `AutomationProperties.AutomationId` attributes
- Omitted or `null` — Prefer `Control.Name` (the `#id` selector)

**Stop recording:**

```json
{
  "id": 2,
  "method": "Recorder.stop"
}
```

### Via Inspector UI

In the Inspector application, recording is toggled through the **Record** button (`#btnTestStudioToggleRecord`) in the Recorder tab. The button state reflects `RecorderViewModel.IsRecording`.

## Captured Interaction Types

The recorder captures the following interaction types by attaching tunneled routed event handlers to the target window:

### Click Events

When the user clicks a control, the recorder:
1. Performs an input hit test at the pointer position
2. Resolves the visual to a logical control via `FindLogicalNode`
3. Generates a CSS selector using `SelectorEngine.GetSelector`
4. Emits a `click` step with selector, offset coordinates, button type, click count, and modifier flags

```json
{
  "step": {
    "type": "click",
    "target": "main",
    "selectors": [["#btnSubmit"]],
    "offsetX": 42.0,
    "offsetY": 12.0,
    "button": "left",
    "clickCount": 1,
    "modifiers": 0
  }
}
```

### Drag and Drop

When a pointer press is followed by movement exceeding 10 pixels before release, the recorder emits a `dragAndDrop` step instead of a click:

```json
{
  "step": {
    "type": "dragAndDrop",
    "target": "main",
    "selectors": [["#sourceItem"]],
    "targetSelectors": [["#dropZone"]],
    "offsetX": 10.0,
    "offsetY": 8.0,
    "targetOffsetX": 50.0,
    "targetOffsetY": 25.0,
    "modifiers": 0
  }
}
```

### Text Input (Change Events)

Text input is captured through focus tracking rather than individual keystrokes:
1. When a `TextBox` receives focus, the recorder snapshots its current `Text` value
2. When the `TextBox` loses focus, the recorder compares the current text to the snapshot
3. If the text changed, a `change` step is emitted with the new value

```json
{
  "step": {
    "type": "change",
    "target": "main",
    "selectors": [["#txtUsername"]],
    "value": "admin@example.com"
  }
}
```

### Scroll Events

Mouse wheel interactions on scrollable controls emit `scroll` steps with delta values (multiplied by 100 for compatibility):

```json
{
  "step": {
    "type": "scroll",
    "target": "main",
    "selectors": [["#listView"]],
    "deltaX": 0.0,
    "deltaY": -300.0
  }
}
```

### Key Press Events

Special keys (Enter, Escape, Tab, Backspace, Delete, arrow keys) with optional modifiers are captured as `keydown` steps:

```json
{
  "step": {
    "type": "keydown",
    "target": "main",
    "key": "Enter",
    "modifiers": 0
  }
}
```

Modifier flags follow the Chrome DevTools convention:
| Flag | Value | Key |
|------|-------|-----|
| Alt | 1 | Alt / Option |
| Ctrl | 2 | Control |
| Meta | 4 | Command / Windows |
| Shift | 8 | Shift |

### Initial Steps

When recording starts, two automatic steps are emitted:

1. **setViewport** — Captures the current window client size
2. **navigate** — Records the CDP server URL as the starting navigation target

## Selector Generation

The recorder uses `SelectorEngine.GetSelector` to produce stable, minimal CSS selectors. The strategy depends on the `selectorMode`:

| Mode | Priority | Example |
|------|----------|---------|
| Default | `#Name` → type path | `#btnSubmit` |
| Automation | `[AutomationId]` → `#Name` → type path | `[AutomationId="submit-button"]` |

For best recording results, assign `Name` or `AutomationProperties.AutomationId` to all interactive controls in your XAML.

:::tip Best Practice
Controls that are targets of user interaction should always have a stable `Name` attribute. This ensures recorded selectors remain valid across UI refactors and template changes.
:::

## Session Management

Each CDP session maintains independent recording state through `SessionRecorderState`. Multiple sessions can record simultaneously without interference. When a session disconnects, its recording state is automatically cleaned up via `RecorderDomain.RemoveSession`.

## Integration with Test Studio

Recorded steps flow from the `RecorderDomain` through the CDP WebSocket to the Inspector's `RecorderViewModel`, which:

1. Converts raw JSON steps into `TestStudioStepModel` instances
2. Adds them to the Test Studio's step list
3. Updates the live YAML representation
4. Enables immediate replay, editing, and code export

See [Test Studio](/articles/test-studio) for the full editing and execution workflow, and [YAML Test Format](/articles/yaml-test-format) for the serialization specification.

## Next Steps

- [Recording User Actions](/articles/recording-user-actions) — Detailed breakdown of interaction capture mechanics
- [Test Studio](/articles/test-studio) — Visual test workspace for editing and replaying recordings
- [YAML Test Format](/articles/yaml-test-format) — Specification of the test serialization format
- [Code Generation](/articles/code-generation) — Export recordings to Puppeteer, Playwright, Selenium, Appium, and Avalonia Headless
