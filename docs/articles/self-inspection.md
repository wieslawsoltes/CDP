---
title: Self-Inspection
---

# Self-Inspection

The CDP Inspector application is itself an Avalonia application with an embedded CDP server. This enables a unique capability: **the Inspector can inspect itself**. This meta-inspection pattern is valuable for debugging the Inspector, developing custom panels, and demonstrating CDP capabilities.

## Dual-CDP Architecture

The Inspector runs its own CDP server on port `9223`, independent of the target application's server on port `9222`:

```
Agent / Second Inspector
  │
  └─ ws://127.0.0.1:9223 ──→ Inspector CDP :9223
                                │
                                └──→ Inspector App
                                       │
                                       └─ Internal CDP Client ──→ Target App CDP :9222
                                                                    │
                                                                    └──→ Target App
```

## Connecting to the Inspector

### Discovery

```bash
curl http://127.0.0.1:9223/json
```

Response:
```json
[{
  "id": "1",
  "type": "page",
  "title": "CDP Inspector",
  "url": "avalonia://CdpInspectorWindow",
  "webSocketDebuggerUrl": "ws://127.0.0.1:9223/devtools/page/1"
}]
```

### Inspect Inspector with Chrome DevTools

```
devtools://devtools/bundled/inspector.html?ws=127.0.0.1:9223/devtools/page/1
```

### Inspect Inspector with Another Inspector Instance

Launch a second Inspector instance pointing at the first:

```bash
# First Inspector (connected to target app on 9222)
dotnet run --project CdpInspectorApp -- --port 9223

# Second Inspector (inspecting the first Inspector)
dotnet run --project CdpInspectorApp -- --port 9224 --target 9223
```

## Use Cases

### Debugging Inspector View Models

Use `Runtime.evaluate` on port `9223` to inspect the Inspector's own state:

```json
{
  "id": 1,
  "method": "Runtime.evaluate",
  "params": {
    "expression": "Window.DataContext.Connection.IsConnected"
  }
}
```

### Inspecting Inspector DOM

Query the Inspector's visual tree to verify panel layouts:

```json
{
  "id": 2,
  "method": "DOM.querySelector",
  "params": {
    "nodeId": 1,
    "selector": "#btnRefreshTargets"
  }
}
```

### Automated Inspector Testing

AI agents can verify Inspector behavior through its CDP port:

```python
# Connect to Inspector's CDP endpoint
async with websockets.connect("ws://127.0.0.1:9223/devtools/page/1") as ws:
    # Click the Connect button
    await ws.send(json.dumps({
        "id": 1,
        "method": "DOM.querySelector",
        "params": {"nodeId": 1, "selector": "#btnConnect"}
    }))
    result = json.loads(await ws.recv())
    node_id = result["result"]["nodeId"]

    # Get button position
    await ws.send(json.dumps({
        "id": 2,
        "method": "DOM.getBoxModel",
        "params": {"nodeId": node_id}
    }))
    box = json.loads(await ws.recv())

    # Click the button
    content = box["result"]["model"]["content"]
    x = (content[0] + content[2]) / 2
    y = (content[1] + content[5]) / 2

    for event_type in ["mousePressed", "mouseReleased"]:
        await ws.send(json.dumps({
            "id": 3,
            "method": "Input.dispatchMouseEvent",
            "params": {
                "type": event_type,
                "x": x, "y": y,
                "button": "left",
                "clickCount": 1
            }
        }))
        await ws.recv()
```

### Recording Inspector Interactions

Start recording on the Inspector's own port to capture interactions with the Inspector UI. This produces recordings that can be replayed to automate Inspector workflows:

```json
{"id": 1, "method": "Recorder.start", "params": {}}
```

## Self-Inspection Verification

A comprehensive self-inspection check validates that the Inspector's CDP server responds correctly:

1. **Discovery** — `GET /json` returns the Inspector window
2. **DOM** — `DOM.getDocument` returns the Inspector's visual tree
3. **Elements** — All stable control names (`#btnRefreshTargets`, `#btnConnect`, etc.) are queryable
4. **Runtime** — `Runtime.evaluate` can read Inspector view model properties
5. **Screenshot** — `Page.captureScreenshot` returns a valid PNG of the Inspector window
6. **Input** — Mouse events can be dispatched to Inspector controls

## Next Steps

- [AI Agent Integration](/articles/ai-agent-integration) — Agent connection patterns
- [Chrome DevTools Connection](/articles/chrome-devtools-connection) — Connection methods
- [Inspector Application](/articles/inspector-app) — Inspector architecture and panels
