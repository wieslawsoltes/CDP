---
title: Inspector App
---

# Inspector App Overview

The **CDP Inspector App (`CdpInspectorApp`)** is a premium, fully featured developer tool and remote debugging client designed specifically for inspecting and automating **Avalonia UI** applications. Styled after the dark mode theme of Google Chrome DevTools, it interfaces directly with any target application hosting the embedded `CdpServer` or exposed via OS automation emulation.

By consolidating visual tree layout inspection, CSS-like styling modifications, a C# REPL execution console, network tracing, memory analysis, performance recording, and automated test scenario generation (Test Studio), the Inspector App functions as the operational core of the CDP Avalonia framework for developers and autonomous AI coding agents alike.

---

## 1. Architecture and Design Aesthetics

The Inspector App is designed as a modular, high-performance client application built on top of `CDP.Inspector.Shared.csproj`. The user interface is composed of resizable, draggable split-panel boxes allowing full layout customization, visual telemetry graphs, and vector-aligned layouts.

### Architecture Overview

**CDP Inspector App (Client Process)**

*   **ConnectionToolbar** → drives `ConnectionViewModel`, which manages target discovery and WebSocket lifecycle.
*   **SuperSplit Container** → hosts one or more **SuperSplitBox** tab panes, each containing an inspector panel.
*   **Inspector Panels** hosted inside SuperSplitBox panes:
    *   Elements View
    *   Console View
    *   Sources View
    *   Network View
    *   Performance View
    *   Memory View
    *   Simulation View
    *   Recorder / Test Studio
*   `ConnectionViewModel` → creates and manages the **ICdpService** client used for all CDP communication.

**Avalonia Target App (Target Process)**

*   **CdpServer** — exposes WebSocket and HTTP endpoints for remote inspection.
*   **Visual / Logical Tree** — the live Avalonia control tree queried and manipulated by the CDP server.

**Communication**

*   The ICdpService client in the Inspector connects to the CdpServer in the target app over the **Chrome DevTools Protocol** (`ws://` WebSocket).

### Design Principles:
*   **DevTools Dark Theme**: The entire workspace utilizes a slate-black styling language (`#202124` canvas) with optimized color palettes (e.g., `#8ab4f8` accent blue, `#ffca28` yellow highlights, and `#ff5252` warning alerts) preventing eye strain during long inspection sessions.
*   **Vector Icon Assets**: To avoid font rendering issues and broken glyph blocks on custom platforms, all visual cues use Microsoft Fluent system vector geometries. These are stored inside `Styles.axaml` as `<StreamGeometry>` keys and loaded dynamically.
*   **Compiling Bindings & Safety**: The Inspector is compiled with full trim safety (`x:CompileBindings="True"`) to ensure compatibility with modern .NET NativeAOT and publishing trimming constraints, avoiding unsafe reflection bindings where possible.

---

## 2. Installation Options

The Inspector App is highly portable and supports four primary deployment methods depending on your development workflow:

### A. .NET Global Tool
The recommended method for local development is installing the inspector globally from NuGet:

```bash
# Install the inspector as a global CLI tool (include --prerelease for preview versions)
dotnet tool install -g Chrome.DevTools.Inspector --prerelease
```

Once installed, launch it directly from any command prompt or terminal:

```bash
cdp-inspector
```

This commands executes the launcher wrapper configured inside the `CdpInspectorApp.csproj` project.

### B. Single-File Executables
For machines without the .NET SDK installed, pre-compiled, self-contained single-file binary archives are available under the GitHub Releases tab for:
*   **Windows**: `cdp-inspector-win-x64.zip`
*   **Linux**: `cdp-inspector-linux-x64.tar.gz`
*   **macOS (Apple Silicon)**: `cdp-inspector-osx-arm64.tar.gz`
*   **macOS (Intel)**: `cdp-inspector-osx-x64.tar.gz`

These distributions package the required Avalonia and .NET runtimes in a compressed single file, operating with zero external prerequisites.

### C. Running from Source
If building directly from a clone of this repository, run the project from the root folder:

```bash
dotnet run --project samples/CdpInspectorApp/CdpInspectorApp.csproj
```

To run both the target sample application and the inspector together during debugging:

```bash
dotnet run --project samples/CdpSampleApp/CdpSampleApp.csproj &
dotnet run --project samples/CdpInspectorApp/CdpInspectorApp.csproj
```

### D. Browser WebAssembly (WASM) Setup
The inspector is fully compatible with WebAssembly and can run inside standard modern web browsers. The source code resides in the `CdpInspectorApp.Browser.csproj` project.

#### Compilation and Hosting
Publish and serve the browser build locally:

```bash
# 1. Publish the WebAssembly artifacts
dotnet publish samples/CdpInspectorApp.Browser/CdpInspectorApp.Browser.csproj -c Release -o publish

# 2. Serve the static site using http-server
npx http-server publish/wwwroot -p 8080 -c-1
```

Once running, navigate to `http://127.0.0.1:8080` in your web browser.

#### Browser Security & CORS Workaround
Standard browser security engines enforce strict boundaries that affect connection scanning:
*   **CORS Blocks Scanning**: The browser will block cross-origin HTTP calls from the inspector page to `http://127.0.0.1:9222/json` to discover targets.
*   **WebSocket Hijack Mitigation**: Chromium blocks browsers from initiating direct WebSocket connections directly to Chrome’s standard debugging port to prevent site-hijacking.

> [!IMPORTANT]
> To connect successfully from the WASM client, first navigate directly to the target application’s discovery page `http://127.0.0.1:9222/json` in a new browser tab. Copy either the **Target ID** or the entire **webSocketDebuggerUrl** string, return to the browser inspector at `http://127.0.0.1:8080`, paste the value into the **Host** textbox, and select **Direct Connection** before clicking **Connect**.

---

## 3. Connection Workflows

Connecting the inspector to a debugging target requires establishing an active JSON-RPC session over a WebSocket. The inspector UI handles two main workflows through the `ConnectionToolbar.axaml` control:

### A. Scanning Targets (Discovery)
By pointing the host configuration textbox to the loopback address of the target application (e.g., `127.0.0.1:9222`) and clicking **Scan Targets**, the inspector performs an HTTP GET request to `http://127.0.0.1:9222/json` to query available window pages. 

The returned JSON payload enumerates all targets:

```json
[
  {
    "description": "Main Window",
    "id": "e9be9db9-9c59-4d64-a690-349f2b842cd8",
    "title": "CdpSampleApp - Home",
    "type": "page",
    "webSocketDebuggerUrl": "ws://127.0.0.1:9222/devtools/page/e9be9db9-9c59-4d64-a690-349f2b842cd8"
  }
]
```

These entries populate the target dropdown combo box, allowing users to select their target and click **Connect**.

### B. Direct Connection via WebSocket URL
When scanning is blocked (e.g., inside browser sandboxes or complex networks) or when automating runs through scripts, developers can input the exact WebSocket debugging URL (e.g., `ws://127.0.0.1:9222/devtools/page/e9be9db9-9c59-4d64-a690-349f2b842cd8`) directly in the host input. This bypasses the discovery phase and attempts a direct WebSocket handshake.

All connection operations, errors, and performance details are managed via the `ConnectionViewModel.cs` and backed by the client-side `ICdpService.cs` implementation.

---

## 4. Tabs and Panels Overview

The inspector organizes its debugging features into twelve dedicated tabs. Each panel coordinates with a backend CDP domain and handles operations asynchronously.

### Elements Panel
*   **Purpose**: Inspect and modify the live Avalonia visual tree.
*   **View**: `ElementsView.axaml`
*   **ViewModel**: `ElementsViewModel.cs`
*   **Features**:
    *   Walks the visual tree structured like HTML nodes (e.g. `<Button>`, `<TextBox>`).
    *   Lists active classes, control name, types, bounds, and styling attributes.
    *   Provides a **Computed Styles** sidebar to inspect active layout parameters (`width`, `margin`, `padding`, `background`, etc.).
    *   Triggers live layout overlays on the target app's window when hovering over tree nodes.
    *   Binds the selected element to the evaluation variable `$0` for the Console.

### Console Panel
*   **Purpose**: C# REPL scripting sandbox.
*   **View**: `ConsoleView.axaml`
*   **ViewModel**: `ConsoleViewModel.cs`
*   **Features**:
    *   Evaluates C# expressions on the live target using Reflection.
    *   Exposes browser-like helper wrappers such as `document.querySelector(selector)` and `document.getElementById(id)`.
    *   Provides references to `Window`, `SelectedNode`, `Control`, `DataContext`, and `ViewModel` for the active focus.
    *   Supports pinned expressions to monitor properties continuously.

### Sources Panel
*   **Purpose**: Navigate target source code directories and inspect assets.
*   **View**: `SourcesView.axaml`
*   **ViewModel**: `SourcesViewModel.cs`
*   **Features**:
    *   Displays the active project workspace file tree.
    *   Loads and syntax-highlights source code files (C#, XAML, JSON) using an integrated code viewer.
    *   Uses the standalone `MinimapTextEditor.cs` control to display code minimaps and annotations.

### Network Panel
*   **Purpose**: Track HTTP/HTTPS communications made by the target app.
*   **View**: `NetworkView.axaml`
*   **ViewModel**: `NetworkViewModel.cs`
*   **Features**:
    *   Intercepts outgoing `HttpClient` requests and incoming responses.
    *   Logs request methods, URLs, status codes, payload sizes, and elapsed times.
    *   Includes a response body viewer supporting text, JSON, and binary image previews.

### Performance Panel
*   **Purpose**: Profile the execution and rendering performance of the application.
*   **View**: `PerformanceView.axaml`
*   **ViewModel**: `PerformanceViewModel.cs`
*   **Features**:
    *   Tracks real-time Frame Rate (FPS), CPU utilization, and layout/render passes.
    *   Renders dynamic telemetry timeline charts.
    *   Helps diagnose stuttering or expensive layout loops on the Avalonia UI Thread.

### Memory Panel
*   **Purpose**: Investigate control type allocations and heap diagnostics.
*   **View**: `MemoryView.axaml`
*   **ViewModel**: `MemoryViewModel.cs`
*   **Features**:
    *   Summarizes control type counts (e.g. how many `Border` or `TextBlock` controls are in memory).
    *   Displays live allocation bar charts.
    *   Provides a manual trigger to invoke a full garbage collection cycle in the CLR.

### Application Panel
*   **Purpose**: View and edit global resources.
*   **View**: `ApplicationView.axaml`
*   **ViewModel**: `ApplicationViewModel.cs`
*   **Features**:
    *   Lists resources defined under `Application.Current.Resources`.
    *   Allows live editing of color brushes, thickness parameters, strings, and theme details, instantly propagating updates to all open windows.

### Simulation Panel
*   **Purpose**: Visual preview and manual interaction proxy.
*   **View**: `SimulationView.axaml`
*   **ViewModel**: `SimulationViewModel.cs`
*   **Features**:
    *   Renders a base64 Base-PNG screencast frame stream from the target page.
    *   Intercepts mouse clicks, drags, mouse wheel scrolls, and keyboard inputs on the preview canvas, mapping the coordinates and routing them as virtual inputs to the target application.

### Recorder / Test Studio Panel
*   **Purpose**: Record, generate, edit, and play automated visual UI tests.
*   **View**: `RecorderView.axaml` & `TestStudioView.axaml`
*   **ViewModels**: `RecorderViewModel.cs` & `TestStudioViewModel.cs`
*   **Features**:
    *   Records pointer inputs and text entry inputs.
    *   Allows authoring assertions and actions through an interactive toolbox.
    *   Features a dual code editor synced with a Flow-compatible YAML structure.
    *   Offers step-by-step test execution (Play, Pause, Step, Stop) with detailed HTML, PDF, and video frame reports.

### Audits Panel
*   **Purpose**: Auditing accessibility (a11y) tree health.
*   **View**: `AuditsView.axaml`
*   **ViewModel**: `AuditsViewModel.cs`
*   **Features**:
    *   Verifies accessibility guidelines (TAP targets, tab index order, control names).
    *   Flags components missing ARIA-like descriptions or automation IDs.

### Events Panel
*   **Purpose**: Raw CDP event stream logger.
*   **View**: `EventsView.axaml`
*   **ViewModel**: `EventsViewModel.cs`
*   **Features**:
    *   Tracks protocol events (e.g. `DOM.documentUpdated`, `Page.screencastFrame`).
    *   Provides filters to inspect incoming payload messages.

### Window Panel
*   **Purpose**: View and manage target window bounds and states.
*   **View**: `WindowControlView.axaml`
*   **Features**:
    *   Displays current width, height, scaling factor, and target system properties.

---

## 5. Layout and UX Features: Split Panels and Tab Bar Navigation

A core visual layout capability of the Inspector App is its dynamic workspace container built using the `SuperSplit.cs` layout system. 

```
  ┌──────────────────────────────────────────────────────────────┐
  │  ConnectionToolbar (Refresh Targets, Connect, Disconnect)   │
  ├───────────────────────────────┬──────────────────────────────┤
  │                               │                              │
  │   SuperSplitBox [Elements]    │    SuperSplitBox [Console]   │
  │   - Tab scroll layout         │    - Tab scroll layout       │
  │   - Scroll left/right buttons │    - Scroll left/right buttons│
  │   - Zoom panel button         │    - Zoom panel button       │
  │                               │                              │
  └───────────────────────────────┴──────────────────────────────┘
```

### Split Panel Architecture
The `MainView.axaml` layout nests a single `SuperSplit` root control linked to `LayoutRoot` in `MainWindowViewModel.cs`.
*   The layout is defined by a tree structure of `SplitNode.cs` records.
*   Each visual pane is hosted inside a `SuperSplitBox.cs` custom control, supporting drag-and-drop tab floating and docking via the `SuperSplitDragManager.cs`.

### Horizontal Tab Scrolling & Mouse Wheel Mapping
Within the header of each `SuperSplitBox`, tabs are laid out horizontally in a `StackPanel` nested in a `ScrollViewer` named `_tabsScrollViewer`.
*   To keep the UI clean, Scrollbar visibility is configured as:
    ```csharp
    HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
    ```
*   To allow scrolling via standard mice, the ScrollViewer's `PointerWheelChanged` event is intercepted, mapping vertical wheel movement into horizontal offsets:
    ```csharp
    _tabsScrollViewer.PointerWheelChanged += (sender, e) =>
    {
        double delta = e.Delta.Y * 40;
        _tabsScrollViewer.Offset = new Point(Math.Max(0, Math.Min(_tabsScrollViewer.Offset.X - delta, _tabsScrollViewer.Extent.Width - _tabsScrollViewer.Viewport.Width)), 0);
        e.Handled = true;
    };
    ```

### Dynamic Overflow Scroll Buttons
When the layout pane is narrowed and the tab headers exceed the available width (`extent > viewport`), the header dynamically displays left and right navigation buttons (`_btnScrollLeft` and `_btnScrollRight`). 
*   Clicking these buttons scrolls the tab bar by 100 pixels in the corresponding direction.
*   The visibility of the scroll buttons updates automatically whenever the layout changes via the `UpdateScrollButtonsVisibility` method:
    *   **Left Scroll Button**: Visible only if `offset > 0.1` (indicating the tab bar is scrolled forward).
    *   **Right Scroll Button**: Visible only if `offset < (extent - viewport - 0.1)` (indicating there are hidden tabs on the right).

### Zoom Panel Feature
Double-clicking the header panel or clicking the zoom button (`_btnZoom`) maximizes the selected split box to fill the entire application workspace. Double-clicking again restores the original split layout.

---

## 6. Self-Inspection Concept and Dual-CDP Workflow

A unique capability of the `CdpInspectorApp` is **Self-Inspection**. 

### The Self-Inspection Concept
In addition to connecting to other applications as a CDP client, the Inspector App hosts its own embedded `CdpServer` listening on port **`9223`**. This means the inspector is itself a CDP target.

AI agents, testing frameworks, or developer scripts can open a WebSocket connection to `ws://127.0.0.1:9223` to inspect the inspector’s own visual elements, evaluate variables inside `MainWindowViewModel.cs`, simulate inputs on the inspector's buttons, and verify generated code.

### Dual-CDP Control Workflow
The dual-CDP topology orchestrates both applications concurrently:

```
  [Test Script / AI Agent]
         │
         ├───► WebSocket (Port 9223) ───► CdpInspectorApp (Inspector)
         │                                      │
         │                                      ▼ Connects Internally
         └───► WebSocket (Port 9222) ───► CdpSampleApp (Target App)
```

This configuration enables headless integration testing of the developer toolchain:
1.  **Start Target & Inspector**: Launch `CdpSampleApp` on port `9222` and `CdpInspectorApp` on port `9223`.
2.  **Connect to Inspector CDP**: Open a WebSocket connection to the inspector on port `9223`.
3.  **Command Connection to Target**: Instruct the inspector to connect to the target application by simulating a click on the inspector's connect button:
    *   Send `DOM.querySelector` to find `#btnRefreshTargets` and `#btnConnect` on the inspector.
    *   Simulate input clicks on these elements.
4.  **Verify State**: Evaluate C# expressions on the inspector to assert that the connection has been established:
    ```csharp
    // Evaluate C# expression on Inspector (port 9223)
    Window.DataContext.Connection.IsConnected == true
    ```
5.  **Inject Test Steps**: Start recording, simulate actions on the target, stop recording, and verify that the Test Studio generated the correct steps.
6.  **Replay and Verify**: Click the play button on the inspector, wait for completion, and assert that the replayed test suite passed without errors.

This dual-CDP model makes the entire inspection and testing toolchain self-verifying and fully automated.
