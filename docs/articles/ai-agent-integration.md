---
title: AI Agent Integration
---

# AI Agent Integration

The CDP server provides a natural interface for AI coding agents to inspect, control, and verify Avalonia applications. Because the protocol is identical to Chrome DevTools Protocol, agents designed for browser automation work with Avalonia applications without modification.

## Connection Pattern

AI agents connect to the CDP server via WebSocket:

```
AI Agent
├─ WebSocket ──→ CDP Server :9222 ──→ Avalonia App
└─ WebSocket ──→ CDP Server :9223 ──→ Inspector App
                                        └─ Internal CDP ──→ CDP Server :9222
```

### Discovery

```bash
# List available targets
curl http://127.0.0.1:9222/json
```

Response:
```json
[{
  "id": "1",
  "type": "page",
  "title": "MainWindow",
  "url": "avalonia://MainWindow",
  "webSocketDebuggerUrl": "ws://127.0.0.1:9222/devtools/page/1"
}]
```

### Connect

```python
import websockets
import json

async def connect():
    async with websockets.connect("ws://127.0.0.1:9222/devtools/page/1") as ws:
        # Enable domains
        await ws.send(json.dumps({"id": 1, "method": "DOM.enable"}))
        await ws.send(json.dumps({"id": 2, "method": "Input.enable"}))
        await ws.send(json.dumps({"id": 3, "method": "Runtime.enable"}))
```

## Core Agent Capabilities

### Element Discovery

Find elements using CSS selectors:

```json
{"id": 10, "method": "DOM.getDocument", "params": {"pierce": true, "depth": -1}}
{"id": 11, "method": "DOM.querySelector", "params": {"nodeId": 1, "selector": "#btnSubmit"}}
```

### Click Simulation

Calculate element center from box model and dispatch mouse events:

```json
{"id": 12, "method": "DOM.getBoxModel", "params": {"nodeId": 42}}
{"id": 13, "method": "Input.dispatchMouseEvent", "params": {"type": "mouseMoved", "x": 82.0, "y": 119.0}}
{"id": 14, "method": "Input.dispatchMouseEvent", "params": {"type": "mousePressed", "x": 82.0, "y": 119.0, "button": "left", "clickCount": 1}}
{"id": 15, "method": "Input.dispatchMouseEvent", "params": {"type": "mouseReleased", "x": 82.0, "y": 119.0, "button": "left", "clickCount": 1}}
```

### Text Input

Focus an element and insert text:

```json
{"id": 16, "method": "DOM.focus", "params": {"nodeId": 49}}
{"id": 17, "method": "Input.insertText", "params": {"text": "Agent demo input"}}
```

### State Verification

Evaluate C# expressions to check application state:

```json
{"id": 18, "method": "Runtime.evaluate", "params": {"expression": "Window.DataContext.IsLoggedIn"}}
```

### Screenshot Capture

Take screenshots for visual verification:

```json
{"id": 19, "method": "Page.captureScreenshot", "params": {"format": "png"}}
```

## Inspected Node Reference

The `$0` variable holds the last inspected node, enabling agents to:

1. Set the inspected node: `DOM.setInspectedNode`
2. Query properties: `Runtime.evaluate` with `$0.Text` or `SelectedNode.Bounds`
3. Navigate the tree: Use `Query()` and `QueryAll()` helper functions

## Dual-CDP Control

For advanced scenarios, agents can control both the target application and the Inspector:

```
Agent
  ├── ws://127.0.0.1:9222  → Target App (inspect, click, type)
  └── ws://127.0.0.1:9223  → Inspector (record, replay, report)
```

This enables the agent to:
- Drive the target app through CDP commands
- Start/stop recording through the Inspector
- Trigger Test Studio replay
- Verify test reports and screenshots

## Browser-Like Document Facade

For compatibility with generic browser agents, the Runtime domain exposes browser-like helpers:

```csharp
document.querySelector("#btnSubmit")         // Returns CdpRuntimeElement
document.querySelectorAll("Button")          // Returns element array
document.getElementById("mainPanel")         // Returns by Name
```

These C# facades provide a familiar API surface for agents that expect JavaScript DOM methods.

## Verification Patterns

### Assert Element Exists

```json
{
  "id": 20,
  "method": "DOM.querySelector",
  "params": {"nodeId": 1, "selector": "#expectedElement"}
}
// Assert result.nodeId > 0
```

### Assert Text Content

```json
{
  "id": 21,
  "method": "Runtime.evaluate",
  "params": {"expression": "document.querySelector(\"#lblStatus\").textContent"}
}
// Assert result.value == "Connected"
```

### Assert Visibility

```json
{
  "id": 22,
  "method": "Runtime.evaluate",
  "params": {"expression": "document.querySelector(\"#errorPanel\").isVisible"}
}
// Assert result.value == false
```

## Next Steps

- [Chrome DevTools Connection](/articles/chrome-devtools-connection) — Browser connection methods
- [Self-Inspection](/articles/self-inspection) — Inspector meta-inspection
- [Selector Engine](/articles/selector-engine) — CSS selector syntax reference
- [Runtime Domain](/articles/runtime-domain) — C# expression evaluation
