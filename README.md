# Avalonia UI Chrome DevTools Protocol (CDP) Support

This library implements server-side support for the **Chrome DevTools Protocol (CDP)** in applications built with the **Avalonia UI** framework.

By embedding a lightweight HTTP and WebSocket server inside an Avalonia application, it enables automated testing, live inspection, selector querying, layout highlighting, input simulation, and runtime scripting from standard browser automation tools (like **Playwright**, **Puppeteer**, **Chrome DevTools**, and **AI Coding Agents**).

---

## Features

- **DOM Domain**: Converts Avalonia's visual tree to a CDP-compliant DOM document tree structure.
- **CSS Domain**: Exposes computed and inline styles for Avalonia controls, and allows live modification of control properties (e.g. background color, margin, size) using C# reflection.
- **Input Domain**: Simulates low-level input events (mouse movement, clicks, mouse wheel scrolls, text entry, and keyboard events) by converting CDP events to Avalonia raw inputs.
- **Page Domain**: Supports high-DPI screenshot capture of the application window or individual elements.
- **Overlay Domain**: Renders element highlights, padding/margin borders, and size tooltips directly onto the window's `AdornerLayer`.
- **Runtime Domain**: Allows executing expressions and invoking functions on Avalonia control instances via Reflection.
- **Target Domain**: Dispatches multiple windows as separate debuggable page targets.

---

## Architectural Overview

```
 ┌───────────────────────────────────────┐
 │ CDP Client (DevTools / Agent / Test)  │
 └──────────────────┬────────────────────┘
                    │ WebSocket / HTTP
 ┌──────────────────▼────────────────────┐
 │              CdpServer                │  (HttpListener Router)
 └──────────────────┬────────────────────┘
                    │ Creates
 ┌──────────────────▼────────────────────┐
 │              CdpSession               │  (JSON-RPC Message Loop)
 └──────────────────┬────────────────────┘
                    │ Dispatches to
 ┌──────────────────┴────────────────────────────────────────────────────┐
 │                                                                       │
 ├───────────────► DOM Domain       ───► Inspects visual tree            │
 ├───────────────► CSS Domain       ───► Inspects & edits styles         │
 ├───────────────► Input Domain     ───► Simulates raw key/mouse inputs  │
 ├───────────────► Page Domain      ───► Captures high-DPI screenshots   │
 ├───────────────► Overlay Domain   ───► Renders highlight adorners      │
 ├───────────────► Runtime Domain   ───► Evaluates C# expressions        │
 └───────────────────────────────────────────────────────────────────────┘
```

All interactions with Avalonia UI visual elements are thread-safe, marshalling operations to the Avalonia UI Thread using `Dispatcher.UIThread.InvokeAsync`.

---

## Getting Started

### 1. Add Reference

Add a reference to the `Avalonia.Diagnostics.Cdp` project (or package) in your main Avalonia application.

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Avalonia.Diagnostics.Cdp.csproj" />
</ItemGroup>
```

### 2. Start the Server

Initialize and start the CDP server in your application startup (typically in `App.axaml.cs` inside `OnFrameworkInitializationCompleted`):

```csharp
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Diagnostics.Cdp;

public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow();
        
        // Start CDP Server on a configurable port (default is 9222)
        CdpServer.Start(9222);
    }

    base.OnFrameworkInitializationCompleted();
}
```

Make sure to stop the server when the application shuts down:

```csharp
// Typically called during application exit/shutdown
CdpServer.Stop();
```

---

## AI Coding Agents Integration Points

This library provides ideal entry points for AI agents (e.g. Playwright-based web agents) to navigate, inspect, and interact with the desktop application:

1. **Selector Engine**: Supports querying elements using a custom visual selector engine:
   - Control types (e.g. `Button`, `TextBox`, `Grid`)
   - Names (e.g. `#btnClickMe` maps to `Name="btnClickMe"`)
   - Classes (e.g. `.primary`)
   - Descendant selectors (e.g. `Grid Border Button`)
   - Child selectors (e.g. `Grid > Border > Button`)
   - Compound selectors (e.g. `Button#btnClickMe.primary`)
2. **Inspected Node `$0`**: Binds the active inspected control node (set via `DOM.setInspectedNode`) to the `$0` variable in the evaluation context, letting agents evaluate C# expressions on the active element.
3. **Text Simulation**: Allows typing raw strings directly into focused input elements using the `Input.dispatchMouseEvent` and `Input.dispatchKeyEvent` methods.
4. **Screenshot Verification**: Agents can capture and inspect screenshots of the window or individual bounding boxes to perform visual verification.
5. **Interactive Highlighting**: Supports the standard Chrome DevTools visual highlighter, showing padding, margin, content boxes, and element names in real-time.

---

## CDP API Specification

The following methods are supported across the core CDP domains:

| Domain | Method | Description |
| :--- | :--- | :--- |
| **DOM** | `getDocument` | Returns the root DOM tree mapping the window visual tree. |
| | `requestChildNodes` | Requests kids of a specific node (used for lazy-loading). |
| | `querySelector` | Query the tree using a CSS selector, returning the node ID. |
| | `querySelectorAll` | Query the tree using a CSS selector, returning all matching node IDs. |
| | `getOuterHTML` | Generates a pseudo-HTML markup string of the visual tree. |
| | `resolveNode` | Resolves a node ID to a Runtime RemoteObject ID. |
| | `focus` | Gives focus to the selected control node. |
| | `setInspectedNode` | Binds the selected control node to the `$0` variable in the evaluation context. |
| | `setAttributeValue` | Updates properties, class lists, or names of a control. |
| | `removeAttribute` | Clears classes or resets control names. |
| **CSS** | `getComputedStyleForNode` | Converts control properties (Background, Width, etc.) to CSS styles. |
| | `getMatchedStylesForNode` | Returns the matching style declarations. |
| | `setStyleTexts` | Performs live modification of a control's properties. |
| **Input** | `dispatchMouseEvent` | Dispatches pointer moves, presses, releases, and wheel scrolls. |
| | `dispatchKeyEvent` | Dispatches key presses, releases, and text inputs. |
| **Page** | `captureScreenshot` | Captures a high-DPI base64-encoded PNG screenshot of the window. |
| **Overlay** | `highlightNode` | Visualizes the control bounds, margin, and padding as a color overlay. |
| | `hideHighlight` | Clears any visible bounding box overlays. |
| **Runtime** | `evaluate` | Executes reflection property/field lookups on target objects. |
| | `callFunctionOn` | Executes helper functions on a mapped remote object. |
| | `getProperties` | Reflects and lists all C# properties/fields on a remote object. |
| **Target** | `getTargets` | Lists all active windows as debuggable page targets. |

---

## Running the Sample App

A sample project is provided in the repository to test the CDP capabilities manually.

1. Start the sample application:
   ```bash
   dotnet run --project samples/CdpSampleApp
   ```
2. Open Google Chrome and navigate to `chrome://inspect`.
3. Under **Devices**, click **Configure...** and verify that `localhost:9222` is listed.
4. You will see **Avalonia CDP Inspector Sample** listed as a target under **Remote Target**.
5. Click **inspect** to open the Chrome DevTools window.
6. Now you can:
   - Walk through the visual tree in the **Elements** panel.
   - Hover over elements in the DevTools to highlight them inside the Avalonia app window.
   - View computed properties in the **Computed** styles panel.
   - Interact with the app (click buttons, write text) and verify inputs are registered.

---

## Development and Testing

### Running Tests
The project uses `Avalonia.Headless.XUnit` to execute headless tests. All tests run on the UI thread and avoid deadlocking by pumping jobs synchronously during async WebSocket handshakes.

To run the full test suite:
```bash
dotnet test
```

### CI Pipeline
A GitHub Actions workflow is set up at `.github/workflows/dotnet.yml` to automatically verify builds and run tests on every push and pull request.
