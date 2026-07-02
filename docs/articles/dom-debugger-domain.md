---
title: DOM Debugger Domain
---

# DOM Debugger Domain

The DOMDebugger domain provides event listener introspection for Avalonia controls. It exposes the routed event handlers attached to `Interactive` controls in a Chrome DevTools-compatible format, enabling agents and DevTools frontends to inspect event subscriptions.

## Methods

### DOMDebugger.getEventListeners

Retrieves all event listeners attached to an Avalonia control:

**Request:**

```json
{
  "id": 1,
  "method": "DOMDebugger.getEventListeners",
  "params": {
    "objectId": "42"
  }
}
```

**Response:**

```json
{
  "id": 1,
  "result": {
    "listeners": [
      {
        "type": "click",
        "useCapture": false,
        "passive": false,
        "once": false,
        "scriptId": "",
        "lineNumber": 0,
        "columnNumber": 0,
        "handler": {
          "type": "function",
          "className": "Function",
          "description": "MainWindowViewModel.OnSubmitClicked",
          "objectId": "handler:1"
        }
      },
      {
        "type": "mousedown",
        "useCapture": true,
        "passive": false,
        "once": false,
        "scriptId": "",
        "lineNumber": 0,
        "columnNumber": 0,
        "handler": {
          "type": "function",
          "className": "Function",
          "description": "DragBehavior.OnPointerPressed",
          "objectId": "handler:2"
        }
      }
    ]
  }
}
```

## Event Name Mapping

Avalonia routed event names are mapped to standard web event names for compatibility with Chrome DevTools:

| Avalonia Event | Web Event Name |
|---|---|
| `Click` | `click` |
| `PointerPressed` | `mousedown` |
| `PointerReleased` | `mouseup` |
| `PointerMoved` | `mousemove` |
| `PointerEnter` | `mouseenter` |
| `PointerLeave` | `mouseleave` |
| `PointerWheelChanged` | `wheel` |
| `KeyDown` | `keydown` |
| `KeyUp` | `keyup` |
| `TextInput` | `input` |
| Other events | Lowercased Avalonia name |

## Routing Strategy Mapping

The `useCapture` flag maps to Avalonia's routing strategies:

| Routing Strategy | `useCapture` | Description |
|---|---|---|
| `RoutingStrategies.Tunnel` | `true` | Events that tunnel from root to target |
| `RoutingStrategies.Bubble` | `false` | Events that bubble from target to root |
| `RoutingStrategies.Direct` | `false` | Events handled only at the target |

## Handler Information

Each listener's `handler` object includes:

| Property | Description |
|---|---|
| `type` | Always `"function"` |
| `className` | Always `"Function"` |
| `description` | `TargetClass.MethodName` format |
| `objectId` | Session-registered object ID for further inspection |

The `objectId` can be used with `Runtime.getProperties` to inspect the handler delegate.

## Implementation Details

The domain uses reflection to access the private `_eventHandlers` field on `Interactive` controls:

1. Resolves the `objectId` to an Avalonia object via the session's object registry
2. Reads `Interactive._eventHandlers` (a dictionary keyed by `RoutedEvent`)
3. For each routed event, enumerates the `RoutedEventHandlerInfo` entries
4. Extracts the delegate method name and target type
5. Maps Avalonia event names to web-standard names
6. Registers each handler delegate as a session object for inspection

:::info
The reflection-based approach uses `[DynamicDependency]` attributes to preserve the `_eventHandlers` field during assembly trimming.
:::

## Stub Methods

The following methods are accepted but return empty responses for DevTools compatibility:

- `setEventListenerBreakpoint`
- `removeEventListenerBreakpoint`
- `setDOMBreakpoint`
- `removeDOMBreakpoint`
- `setInstrumentationBreakpoint`
- `removeInstrumentationBreakpoint`
- `setXHRBreakpoint`
- `removeXHRBreakpoint`
- `setBreakOnCSPViolation`

## Inspector Integration

The Events panel in the Inspector uses `DOMDebugger.getEventListeners` to display event handlers for the currently selected element. Each listener shows:

- Event type name
- Handler method signature
- Whether it uses capture (tunnel) phase
- Click to highlight the handler in the source view

## Next Steps

- [Debugger Domain](/articles/debugger-domain) — Breakpoint and pause/resume debugging
- [Events Panel](/articles/events-panel) — Event listener visualization
- [DOM Domain](/articles/dom-domain) — Element inspection and querying
