# Avalonia UI Chrome DevTools Protocol (CDP) Support

This library implements server-side support for the **Chrome DevTools Protocol (CDP)** in applications built with the **Avalonia UI** framework.

By embedding a lightweight HTTP and WebSocket server inside an Avalonia application, it enables automated testing, live inspection, selector querying, layout highlighting, input simulation, and runtime scripting from standard browser automation tools (like **Playwright**, **Puppeteer**, **Chrome DevTools**, and **AI Coding Agents**).

## NuGet Packages

| Package Name | Target | Version | Downloads |
| :--- | :--- | :--- | :--- |
| **CDP.Avalonia** | Core Library | [![NuGet](https://img.shields.io/nuget/v/CDP.Avalonia.svg?style=flat-square)](https://www.nuget.org/packages/CDP.Avalonia/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/CDP.Avalonia.svg?style=flat-square)](https://www.nuget.org/packages/CDP.Avalonia/) |
| **CDP.Inspector.Shared** | Shared UI Library | [![NuGet](https://img.shields.io/nuget/v/CDP.Inspector.Shared.svg?style=flat-square)](https://www.nuget.org/packages/CDP.Inspector.Shared/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/CDP.Inspector.Shared.svg?style=flat-square)](https://www.nuget.org/packages/CDP.Inspector.Shared/) |
| **CDP.DiagnosticTools** | In-Process Diagnostics | [![NuGet](https://img.shields.io/nuget/v/CDP.DiagnosticTools.svg?style=flat-square)](https://www.nuget.org/packages/CDP.DiagnosticTools/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/CDP.DiagnosticTools.svg?style=flat-square)](https://www.nuget.org/packages/CDP.DiagnosticTools/) |
| **CDP.Inspector** | .NET Global Tool | [![NuGet](https://img.shields.io/nuget/v/CDP.Inspector.svg?style=flat-square)](https://www.nuget.org/packages/CDP.Inspector/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/CDP.Inspector.svg?style=flat-square)](https://www.nuget.org/packages/CDP.Inspector/) |


---

## Features

- **DOM Domain**: Converts Avalonia's visual tree to a CDP-compliant DOM document tree structure.
- **CSS Domain**: Exposes computed and inline styles for Avalonia controls, and allows live modification of control properties (e.g. background color, margin, size) using C# reflection.
- **Input Domain**: Simulates low-level input events (mouse movement, clicks, mouse wheel scrolls, text entry, and keyboard events) by converting CDP events to Avalonia raw inputs.
- **Page Domain**: Supports high-DPI screenshot capture of the application window or individual elements.
- **Overlay Domain**: Renders element highlights, padding/margin borders, and size tooltips directly onto the window's `AdornerLayer`.
- **Runtime Domain**: Allows executing expressions and invoking functions on Avalonia control instances via Reflection, including parameterized method calls.
- **Target Domain**: Dispatches multiple windows as separate debuggable page targets.
- **Network Domain**: Intercepts outbound `HttpClient` requests and response bodies with smart caching and stream detection.
- **Sources Domain**: Navigates active workspace directory trees and serves source code files safely under relative path boundaries.
- **Application Domain**: Exposes live querying and mutation of global resources under `Application.Current.Resources`.
- **Memory Domain**: Computes live visual control type allocations and triggers garbage collections.
- **Recorder Domain**: Intercepts pointer and focus interactions to record, export (JSON/Puppeteer), load, and replay visual test scenarios.

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

Add the NuGet package reference `CDP.Avalonia` to your main Avalonia application:

```xml
<ItemGroup>
  <PackageReference Include="CDP.Avalonia" Version="1.0.0" />
</ItemGroup>
```

Or install it via the .NET CLI:
```bash
dotnet add package CDP.Avalonia
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

### 3. In-Process Diagnostics Inspector

If you prefer to launch the DevTools inspector client directly inside your application's process (as a replacement for Avalonia's built-in DevTools), you can use the `CDP.DiagnosticTools` package.

#### Install Package
Install via the .NET CLI:
```bash
dotnet add package CDP.DiagnosticTools
```

#### Attach the Inspector
Call `AttachCdpInspector` on your main window (typically in `App.axaml.cs` inside `OnFrameworkInitializationCompleted`):

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow();
        
        // Attach the in-process CDP Inspector (default trigger key is F12)
        desktop.MainWindow.AttachCdpInspector(9222);
    }

    base.OnFrameworkInitializationCompleted();
}
```

When running, press **F12** inside the window to open the in-process DevTools client connected directly to the target application's CDP server.

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
| **Network** | `enable` / `disable` | Enables/disables outbound HTTP request monitoring. |
| | `getResponseBody` | Retrieves the body payload of a completed HTTP request. |
| **Sources** | `getWorkspaceFiles` | Recursively returns relative file paths under the active workspace. |
| | `getFileContent` | Retrieves the text content of a source file within workspace boundaries. |
| **Application** | `getResources` | Lists global resources in the `Application.Current.Resources` registry. |
| | `setResource` | Registers or mutates a key-value global resource brush. |
| | `deleteResource` | Deletes a global resource by key. |
| **Memory** | `getLiveControls` | Computes live control allocations by class type. |
| | `collectGarbage` | Triggers a full garbage collection in the CLR. |
| **Recorder** | `start` / `stop` | Starts/stops intercepting user events for automation script recording. |

---

## Running the Sample App and Inspector

Sample projects are provided in the repository to demonstrate the CDP capabilities:
- **CdpSampleApp**: A simple target application listening on port `9222`.
- **CdpInspectorApp**: A custom modular inspector app styled like Chrome DevTools, which connects to the sample app. It also starts its own CDP server on port `9223` for self-inspection.

### 1. Manual Testing with Chrome DevTools

You can connect standard Google Chrome (or any Chromium-based browser like Microsoft Edge, Brave, or Opera) directly to your running Avalonia application to inspect and debug it.

#### Target Discovery Setup

1. Start the sample application:
   ```bash
   dotnet run --project samples/CdpSampleApp
   ```
2. Open Google Chrome and navigate to `chrome://inspect`.
3. Ensure the **Discover network targets** checkbox is enabled.
4. Click **Configure...** next to "Discover network targets".
5. Add the target addresses for the applications you want to debug:
   - `127.0.0.1:9222` (default port for `CdpSampleApp`)
   - `127.0.0.1:9223` (default port for `CdpInspectorApp` client self-inspection)
   
   > [!IMPORTANT]
   > On macOS, `localhost` may resolve first to the IPv6 loopback address (`::1`). Since Chrome's target discovery daemon has known compatibility issues with IPv6 loopback discovery, you should explicitly use the IPv4 loopback IP address `127.0.0.1` instead of `localhost` in the discovery settings.

6. Click **Done**.
7. Under the **Remote Target** section, you will see your running Avalonia application listed (e.g., **Avalonia CDP Inspector Sample**).
8. Click the **inspect** link next to the target. This will launch a standard Chrome DevTools window connected directly to the Avalonia process.

#### Direct Connection via WebSocket URL

For automated tools, scripts, or manual debugging where auto-discovery is not preferred:
1. Make an HTTP GET request to the target discovery endpoint: `http://127.0.0.1:9222/json` (or `http://127.0.0.1:9223/json`).
2. The response will return a JSON list of available window targets, including a `devtoolsFrontendUrl` property.
3. Construct or copy the URL:
   ```
   devtools://devtools/bundled/inspector.html?ws=127.0.0.1:9222/devtools/page/<Target-ID>
   ```
   *Note: Due to security restrictions in modern browsers, direct navigation to `devtools://` links via the address bar may be blocked. The `chrome://inspect` page remains the recommended and most reliable way to launch the DevTools frontend.*

#### CORS & Preflight Support

Standard Chrome DevTools requires Cross-Origin Resource Sharing (CORS) headers and preflight handling to retrieve target details from a local web server. The built-in `CdpServer` has full support for this:
- Outbound responses automatically include the `Access-Control-Allow-Origin: *` header.
- Correctly handles HTTP `OPTIONS` preflight requests, returning a `200 OK` status with the correct CORS headers.
- No special browser flags (such as disabling web security) are needed to discover or connect to targets.

#### Available DevTools Features

Once connected, you can use the following standard panels:
- **Elements**: 
  - Walk the Avalonia visual tree structured as HTML elements (e.g., `<Window>`, `<Grid>`, `<Button>`, `<TextBlock>`).
  - View control names, types, and classes as element attributes.
  - Hovering over elements in the tree highlights them in real-time in the running Avalonia application, drawing overlay margins, paddings, and content bounds.
  - Select an element in the tree, then open the **Console** and use the `$0` reference variable to run runtime C# expression evaluation on that specific control instance.
- **Computed Styles**:
  - Check the **Computed** panel in the Styles sidebar to inspect active layout and visual properties (such as `width`, `height`, `margin`, `padding`, `background`, `foreground`, `opacity`, `visibility`).
  - Edit inline style properties to dynamically alter control attributes at runtime.
- **Console**:
  - Execute C# runtime statements and query visual properties using the `Runtime` domain.
- **Screenshots**:
  - Capture high-DPI screenshots of individual visual components or the main window using DevTools' device toolbar or screenshot shortcuts.

### 2. Testing with CdpInspectorApp

The inspector app can be run in three ways:

#### A. Install as a .NET Tool (Recommended)
You can install the inspector globally on your machine:
```bash
dotnet tool install -g CDP.Inspector
```
Once installed, launch it directly from your terminal:
```bash
cdp-inspector
```

#### B. Download Single-File Executables
Pre-compiled, self-contained single-file binaries are available under the [Releases](https://github.com/wieslawsoltes/CDP/releases) section for:
- **Windows** (`cdp-inspector-win-x64.zip`)
- **Linux** (`cdp-inspector-linux-x64.tar.gz`)
- **macOS** (Apple Silicon `cdp-inspector-osx-arm64.tar.gz` and Intel `cdp-inspector-osx-x64.tar.gz`)

#### C. Run from Source
If running from source in the cloned repository:
1. Start both the sample application and the inspector:
   ```bash
   dotnet run --project samples/CdpSampleApp &
   dotnet run --project samples/CdpInspectorApp
   ```
2. Click **Scan Targets** inside the inspector, select `CdpSampleApp (127.0.0.1:9222)`, and click **Connect**.
3. Explore the redesigned Chrome DevTools dark mode panels (Elements, Console, Sources, Network, Performance, Application, and Simulation).
4. Record user interactions by clicking the **Recorder** tab, clicking **Start Recording**, performing clicks and text typing on the sample app, and stopping the recording. You can save/export the Puppeteer scripts, load them back, and click **Replay** to replay them.

---

## CDP Self-Inspection & Agentic Testing

Enabling CDP inside the `CdpInspectorApp` client itself on port `9223` allows AI coding agents and programmatic control scripts to fully inspect, command, and verify both the client and the target concurrently. 

This enables automated integration scenarios (e.g. commanding the inspector to connect to a target, starting a recording, clicking and typing elements on the target, stopping the recording, and verifying the generated Puppeteer automation code on the inspector's UI) to run headlessly in under 10 seconds.

For full architectural details, sequence diagrams, and agent recipes, see the [Agent-Driven CDP Self-Inspection & Multi-App Testing Guide](agents.md).

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
