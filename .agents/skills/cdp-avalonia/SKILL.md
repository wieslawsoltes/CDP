---
name: cdp-avalonia
description: Guide and instructions for installing, configuring, inspecting, and writing E2E tests for Avalonia applications using Chrome DevTools Protocol (CDP) support.
---

# CDP for Avalonia - Coding Agents Integration & E2E Testing Guide

This skill equips coding agents (such as Gemini, Claude, Copilot, and Codex) with the knowledge and recipes to install, inspect, debug, and automate Avalonia applications using the **Chrome DevTools Protocol (CDP)**.

---

## 1. Overview & Key Use Cases

`CDP.Avalonia` (implemented via `Avalonia.Diagnostics.Cdp`) embeds a lightweight CDP Server directly inside an Avalonia application. This enables external clients, automated scripts, and AI agents to connect via WebSockets and remote-control the GUI.

### Primary Use Cases:
- **Headless E2E Test Automation**: Run fast UI automation in CI/CD environments (e.g. GitHub Actions) without heavy/brittle platform-dependent drivers (like Appium or FlaUI).
- **Remote GUI Inspection**: Browse the visual tree, inspect element bounds, styles, and data contexts.
- **Accessibility (a11y) Audits**: Programmatically query accessibility trees, roles, and properties to verify compliance.
- **Self-Inspection / Dual-CDP Orchestration**: Inspect the debugger client itself while it inspects the target application.

---

## 2. Installation & Configuration

### Step A: Install the NuGet Package
Add the CDP diagnostics package to your Avalonia desktop application project:
```shell
dotnet add package CDP.Avalonia
```

### Step B: Initialize the CDP Server in Code
Enable the protocol in your main window code-behind (`MainWindow.axaml.cs`) or startup lifecycle:

#### Option 1: Quick Extension Method (Auto F12 Keybinding)
Using the extension method automatically starts the server and binds **F12** to open the embedded inspector:
```csharp
using Avalonia;
using Avalonia.Controls;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Starts CDP Server on port 9222, opens Inspector window on F12 keypress
        this.AttachCdpInspector(port: 9222);
    }
}
```

#### Option 2: Programmatic Manual Server Start
For custom startup scripts, headless integration, or manual target registrations:
```csharp
using Avalonia.Diagnostics.Cdp;

// Start server on desired port
CdpServer.Start(9222);

// Register a window target to be discoverable
var targetId = CdpServer.Register(mainWindow, "My Target App Window");

// Stop the server on application exit
CdpServer.Stop();
```

---

## 3. Remote Debugging & Target Discovery

Once the CDP server starts, it exposes a local HTTP JSON target discovery server.

- **Target JSON Discovery Endpoint**: `http://localhost:9222/json` or `http://localhost:9222/json/list`
- **Response Format**:
  ```json
  [
    {
      "description": "My Target App Window",
      "id": "c3e64f43-5095-43f4-8bef-e097d0c8dc1f",
      "title": "My Target App Window",
      "type": "page",
      "webSocketDebuggerUrl": "ws://localhost:9222/devtools/page/c3e64f43-5095-43f4-8bef-e097d0c8dc1f"
    }
  ]
  ```
- **Web Browser Inspect**: Open Google Chrome or any Chromium browser, navigate to `chrome://inspect`, configure network targets to include `localhost:9222`, and click "inspect" under the discovered target list.

---

## 4. E2E Test Automation & Dual-CDP Orchestration

To run automated integration tests, configure a control harness (e.g. a console test runner or xUnit integration) that connects to the WebSockets of both the target and the client applications.

```
       ┌────────────────────────┐
       │   Control App (Agent)  │
       └────┬──────────────┬────┘
            │              │
   CDP: 9223│              │CDP: 9222
            ▼              ▼
 ┌──────────────┐     ┌──────────────┐
 │ CdpInspector │     │  CdpSample   │
 │    Client    │     │  Target App  │
 └──────┬───────┘     └──────────────┘
        │                    ▲
        └────────────────────┘
          CDP WebSocket Connection
```

### Flow Checklist for Coding Agents:
1. **Connect**: Open WebSocket connections to both target (`9222`) and client (`9223`) URLs retrieved from `/json`.
2. **Setup**: Send `DOM.enable` to parse the visual tree.
3. **Inspect**: Query element bounds via `DOM.querySelector` followed by `DOM.getBoxModel`.
4. **Interact**: Calculate element centers and dispatch simulated inputs via `Input.dispatchMouseEvent`.
5. **Verify**: Retrieve states or screenshot verification via `Page.captureScreenshot`.

---

## 5. Coding Agent JSON-RPC Reference Recipes

### Target DOM Query & Center-Click:
```json
// 1. Query Node ID of a button
{ "id": 1, "method": "DOM.querySelector", "params": { "nodeId": 1, "selector": "#btnSubmit" } }

// 2. Get bounding box model for the Node ID
{ "id": 2, "method": "DOM.getBoxModel", "params": { "nodeId": 10 } }

// 3. Dispatch simulated mouse press/release at calculated center coordinates (x, y)
{
  "id": 3,
  "method": "Input.dispatchMouseEvent",
  "params": { "type": "mousePressed", "x": 150.5, "y": 25.0, "button": "left", "clickCount": 1 }
}
```

### Accessibility Auditing:
```json
// Query full accessibility tree mapping
{ "id": 4, "method": "Accessibility.getFullAXTree" }

// Query partial AXTree for a specific node with relatives
{
  "id": 5,
  "method": "Accessibility.getPartialAXTree",
  "params": { "nodeId": 10, "fetchRelatives": true }
}
```

### Screen Capture Verification:
```json
{ "id": 6, "method": "Page.captureScreenshot" }
// Returns base64 encoded png string in: result.data
```

---

## 6. Coding Agent Instructions & Best Practices

When writing or extending code in projects utilizing `CDP.Avalonia`, coding agents must strictly adhere to the following directives:

- **Codex (Code Generation)**:
  - Do not change control `Name` attributes in XAML files. E2E verification test scripts rely on stable element names/selectors.
  - When generating custom control layouts, ensure PascalCase property names are preserved since remote object queries map them directly.

- **Claude (Refactoring & Architecture)**:
  - Keep code-behind windows thin. Delegate CDP network/socket lifecycles and business VM logic to separate service classes (e.g. `ICdpService`).
  - Use ViewModel databindings (`{Binding ...}`) and `ICommand` rather than direct code-behind DOM element manipulation.

- **Copilot (Interactive Commands)**:
  - Query target properties via `Runtime.evaluate` expression scripts rather than relying on coordinates.
  - Ensure all async background WebSocket loops are wrapped in structured exception handling to prevent unhandled app crashes.

- **Gemini (Verification & Auditing)**:
  - When implementing new app features, **always** write custom E2E scenarios in `ControlApp/Program.cs` that target the specific updates.
  - Use `JsonNode.DeepClone()` when broadcasting payloads to multiple sessions to prevent JsonNode parent-ownership exceptions.
  - Use `CdpServer.OriginalOut` instead of `Console.WriteLine` when tracing messages from within CDP WebSocket response hooks to prevent infinite redirection loops.
