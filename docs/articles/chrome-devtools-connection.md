---
title: Chrome DevTools Connection
---

# Chrome DevTools Connection

The CDP server embedded in Avalonia applications exposes standard Chrome DevTools Protocol endpoints. This article explains how to connect using browser DevTools, automation tools, and custom clients.

## Discovery Endpoints

### List Targets

```bash
curl http://127.0.0.1:9222/json
# or
curl http://127.0.0.1:9222/json/list
```

Response:
```json
[
  {
    "id": "1",
    "type": "page",
    "title": "MainWindow",
    "url": "avalonia://MainWindow",
    "webSocketDebuggerUrl": "ws://127.0.0.1:9222/devtools/page/1",
    "devtoolsFrontendUrl": "devtools://devtools/bundled/inspector.html?ws=127.0.0.1:9222/devtools/page/1"
  }
]
```

### Browser Metadata

```bash
curl http://127.0.0.1:9222/json/version
```

Response:
```json
{
  "Browser": "CDP/Avalonia",
  "Protocol-Version": "1.3",
  "User-Agent": "CDP/Avalonia",
  "webSocketDebuggerUrl": "ws://127.0.0.1:9222/devtools/browser"
}
```

## Connection Methods

### Chrome DevTools Frontend

Open Chrome and navigate to:
```
devtools://devtools/bundled/inspector.html?ws=127.0.0.1:9222/devtools/page/1
```

Or use Chrome's `chrome://inspect` page:
1. Open `chrome://inspect` in Chrome
2. Click **Configure** and add `127.0.0.1:9222`
3. Your Avalonia application windows appear as inspectable targets
4. Click **Inspect** to open the DevTools frontend

:::info
Some advanced CDP features (DOM live editing, CSS inspection) work differently because the CDP server maps Avalonia concepts to web equivalents. Core features like Elements, Console, Network, and Performance work as expected.
:::

### Puppeteer (Node.js)

```javascript
const puppeteer = require('puppeteer');

const browser = await puppeteer.connect({
  browserWSEndpoint: 'ws://127.0.0.1:9222/devtools/browser'
});

const pages = await browser.pages();
const page = pages[0];

// Interact with the Avalonia app
await page.click('#btnSubmit');
const text = await page.$eval('#lblResult', el => el.textContent);
console.log('Result:', text);

await browser.disconnect();
```

### Playwright

```typescript
import { chromium } from 'playwright';

const browser = await chromium.connectOverCDP('http://127.0.0.1:9222');
const context = browser.contexts()[0];
const page = context.pages()[0];

await page.locator('#btnSubmit').click();
await expect(page.locator('#lblResult')).toHaveText('Success');

await browser.close();
```

### WebSocket Client (Python)

```python
import asyncio
import json
import websockets

async def main():
    uri = "ws://127.0.0.1:9222/devtools/page/1"
    async with websockets.connect(uri) as ws:
        # Enable DOM domain
        await ws.send(json.dumps({
            "id": 1,
            "method": "DOM.enable"
        }))
        response = json.loads(await ws.recv())
        print("DOM enabled:", response)

        # Get document
        await ws.send(json.dumps({
            "id": 2,
            "method": "DOM.getDocument",
            "params": {"depth": -1}
        }))
        doc = json.loads(await ws.recv())
        print("Root node:", doc["result"]["root"]["nodeName"])

asyncio.run(main())
```

### WebSocket Client (C#)

```csharp
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using var ws = new ClientWebSocket();
await ws.ConnectAsync(
    new Uri("ws://127.0.0.1:9222/devtools/page/1"),
    CancellationToken.None);

// Send command
var command = new { id = 1, method = "DOM.enable" };
var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command));
await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);

// Receive response
var buffer = new byte[65536];
var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
var response = Encoding.UTF8.GetString(buffer, 0, result.Count);
Console.WriteLine(response);
```

## Multi-Window Support

Each Avalonia window is exposed as a separate target. When your application has multiple windows:

```bash
curl http://127.0.0.1:9222/json
```

Returns multiple entries:
```json
[
  {
    "id": "1",
    "title": "MainWindow",
    "webSocketDebuggerUrl": "ws://127.0.0.1:9222/devtools/page/1"
  },
  {
    "id": "2",
    "title": "SettingsWindow",
    "webSocketDebuggerUrl": "ws://127.0.0.1:9222/devtools/page/2"
  }
]
```

Connect to each window's WebSocket endpoint independently.

## CORS Configuration

The CDP server sets CORS headers to allow cross-origin connections:

```
Access-Control-Allow-Origin: *
Access-Control-Allow-Methods: GET, POST, OPTIONS
Access-Control-Allow-Headers: Content-Type
```

This enables browser-based DevTools frontends to connect without CORS restrictions.

## Custom Port Configuration

```csharp
// Start CDP server on a custom port
CdpServer.Start(port: 9333);
```

When using the Inspector:
```bash
dotnet run --project CdpInspectorApp -- --port 9333
```

## Connection Security

:::warning
The CDP server binds to `127.0.0.1` by default, limiting connections to the local machine. Do not expose CDP ports to untrusted networks — the protocol provides full access to the application's runtime, including C# expression evaluation.
:::

For remote debugging, use an SSH tunnel:

```bash
ssh -L 9222:127.0.0.1:9222 user@remote-host
```

## Next Steps

- [AI Agent Integration](/articles/ai-agent-integration) — Agent connection patterns
- [Architecture](/articles/architecture) — Server architecture details
- [Target Domain](/articles/target-domain) — Multi-target management
- [Self-Inspection](/articles/self-inspection) — Inspector meta-inspection
