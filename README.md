# Avalonia UI Chrome DevTools Protocol (CDP) Support

This library implements server-side support for the **Chrome DevTools Protocol (CDP)** in applications built with the **Avalonia UI** framework.

By embedding a lightweight HTTP and WebSocket server inside an Avalonia application, it enables automated testing, live inspection, selector querying, layout highlighting, input simulation, and runtime scripting from standard browser automation tools (like **Playwright**, **Puppeteer**, **Chrome DevTools**, and **AI Coding Agents**).

<img width="3824" height="2318" alt="image" src="https://github.com/user-attachments/assets/3b9d860d-fc57-421c-b947-742c0f9f70e9" />

## NuGet Packages

| Package Name | Target | Version | Downloads |
| :--- | :--- | :--- | :--- |
| **Chrome.DevTools.Protocol** | Core Protocol & Client Library | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.Protocol.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Protocol/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.Protocol.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Protocol/) |
| **Chrome.DevTools.Avalonia** | Avalonia Server Support | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.Avalonia.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Avalonia/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.Avalonia.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Avalonia/) |
| **Chrome.DevTools.Automation.OS** | OS Automation Support | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.Automation.OS.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Automation.OS/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.Automation.OS.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Automation.OS/) |
| **Chrome.DevTools.Inspector.Shared** | Shared UI Library | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.Inspector.Shared.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Inspector.Shared/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.Inspector.Shared.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Inspector.Shared/) |
| **Chrome.DevTools.Editor.Minimap** | Standalone Minimap & Inline Editor | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.Editor.Minimap.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Editor.Minimap/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.Editor.Minimap.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Editor.Minimap/) |
| **Chrome.DevTools.Editor.Nodes** | Standalone Graph Node Editor | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.Editor.Nodes.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Editor.Nodes/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.Editor.Nodes.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Editor.Nodes/) |
| **Chrome.DevTools.Editor.Nodes.Msagl** | MSAGL Layout Provider | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.Editor.Nodes.Msagl.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Editor.Nodes.Msagl/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.Editor.Nodes.Msagl.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Editor.Nodes.Msagl/) |
| **Chrome.DevTools.Editor.Splits** | Dynamic Splits Layout Container | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.Editor.Splits.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Editor.Splits/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.Editor.Splits.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Editor.Splits/) |
| **Chrome.DevTools.DiagnosticTools** | In-Process Diagnostics | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.DiagnosticTools.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.DiagnosticTools/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.DiagnosticTools.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.DiagnosticTools/) |
| **Chrome.DevTools.Inspector** | .NET Global Tool (GUI) | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.Inspector.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Inspector/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.Inspector.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Inspector/) |
| **Chrome.DevTools.Cli** | .NET Global Tool (CLI) | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.Cli.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Cli/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.Cli.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Cli/) |
| **Chrome.DevTools.Runner** | .NET Global Tool (Headless Runner) | [![NuGet](https://img.shields.io/nuget/v/Chrome.DevTools.Runner.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Runner/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Chrome.DevTools.Runner.svg?style=flat-square)](https://www.nuget.org/packages/Chrome.DevTools.Runner/) |

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

## OS Automation Architecture (CDP Emulation)

When a target application does not have a native CDP server enabled, the inspector can connect to applications using native operating system automation and accessibility APIs (e.g. ApplicationServices/AXUIElement on macOS, UIAutomation on Windows, and X11/libX11 on Linux). The inspector emulates the Chrome DevTools Protocol in-process, translating CDP domains transparently.

```mermaid
graph TD
    subgraph CDP Inspector App
        ConnectionViewModel --> ICdpService
        CdpService[CdpService Facade] -->|os:// host| OsAutomationCdpSession
        CdpService -->|http/ws host| ClientWebSocket
    end
    
    subgraph CDP.Automation.OS
        OsAutomationCdpSession --> OSAutomationService
        OSAutomationService -->|macOS| MacOsAutomation
        OSAutomationService -->|Windows| WindowsAutomation
        OSAutomationService -->|Linux| LinuxAutomation
    end
    
    subgraph Native Operating System
        MacOsAutomation -->|CoreGraphics/AXUIElement| macOS[macOS Accessibility]
        WindowsAutomation -->|UIAutomationCore/Win32| Windows[Windows UIA]
        LinuxAutomation -->|X11/libX11| Linux[Linux X11]
    end
```

This design intercepts the `os://` connection scheme in `CdpService` to bypass network sockets entirely, providing high-performance, low-allocation UI tree query resolution (`DOM.getDocument`, `DOM.querySelector`), coordinate box calculations (`DOM.getBoxModel`), raw mouse/keyboard injection (`Input`), and displayed window capturing (`Page.captureScreenshot`).

## macOS/Windows OS Automation Permissions & Layout Notes

When utilizing **OS Automation Mode (`os://`)** on macOS/Windows, CoreGraphics, UIAutomation, and low-level Input/Accessibility APIs require explicit OS-level permissions to inspect visual trees, simulate inputs, capture target window screenshots, and record native input event taps.

### 1. Accessibility & Input Permissions
To query the UI visual tree of other applications, retrieve elements, simulate keyboard/mouse inputs, and capture global native pointer taps:
- **macOS**:
  - Go to **System Settings** -> **Privacy & Security** -> **Accessibility** (and **Input Monitoring** if required).
  - Enable permission for your terminal app (e.g., **Terminal**, **iTerm**), IDE (e.g., **VS Code**, **Rider**), or the runner executable.
  - > [!NOTE]
    > On macOS/Windows, global native event taps require elevated Accessibility/Input Monitoring permissions. Without them, native mouse taps are skipped, and the inspector falls back to polling focus changes to log click events.
- **Windows**:
  - Ensure the executing process runs with sufficient privileges to interact with other desktop windows under User Account Control (UAC).

### 2. Layout & Workspace Panel Navigation
- **Split Panels Tab Bar**:
  - In the split panels tab items panel, scrolling support via mouse wheel is supported. The scrollbar is hidden, and Left/Right scroll navigation buttons are shown dynamically on the sides of the tab bar when the tabs overflow.

### 3. Screen Recording Permission (macOS)
To capture window screenshots and stream real-time screencast frames to the Simulation Preview pane:
- Go to **System Settings** -> **Privacy & Security** -> **Screen Recording**.
- Enable permission for your terminal app (e.g., **Terminal**, **iTerm**) or IDE/VS Code.
- > [!IMPORTANT]
  > **You must completely restart** your terminal, iTerm, or IDE after granting these permissions for the changes to take effect. If you run the inspector or target app before restarting, macOS will silently deny access, resulting in blank grey boxes, wallpapers, or incorrect window composite crops.

---

## Getting Started

### Add Reference

Install the NuGet package `Chrome.DevTools.Avalonia` to your main Avalonia application:

#### Using .NET CLI (Recommended)
Since packages are currently published as preview versions, include the `--prerelease` flag to retrieve the latest version:
```bash
dotnet add package Chrome.DevTools.Avalonia --prerelease
```

#### Manual XML Reference
If manually adding the package reference to your `.csproj` file, specify the version to ensure a successful NuGet restore:
```xml
<ItemGroup>
  <PackageReference Include="Chrome.DevTools.Avalonia" Version="x.y.z" />
</ItemGroup>
```

### OS Automation Support

If you want to use accessibility-driven operating system automation (AXUIElement on macOS, UIAutomation on Windows, and X11 on Linux) to emulate CDP endpoints in-process, install `Chrome.DevTools.Automation.OS` instead:

```bash
dotnet add package Chrome.DevTools.Automation.OS --prerelease
```

You can then query window status, list open windows, or perform low-level inputs directly:
```csharp
using CDP.Automation.OS;

var windows = OSAutomationService.Instance.GetWindows();
foreach (var win in windows)
{
    Console.WriteLine($"Found Window: {win.Title} ({win.Id})");
}
```

### Standalone Minimap & Editor Support

If you only need the standalone `MinimapTextEditor` control, the code annotation inline layers, or visual gutter margins for your `AvaloniaEdit` editors without any Chrome DevTools Protocol or network server dependencies, install `Chrome.DevTools.Editor.Minimap` instead:

```bash
dotnet add package Chrome.DevTools.Editor.Minimap --prerelease
```

Include the editor's generic styling resources inside your `App.axaml` or `Styles.axaml` styles list:
```xml
<StyleInclude Source="avares://CDP.Editor.Minimap/Themes/Generic.axaml" />
```

Use `MinimapTextEditor` in your XAML views:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:editor="using:XamlPlayground.Editor.Minimap">
    <editor:MinimapTextEditor Name="txtCode" 
                              MinimapEnabled="True" 
                              MinimapShowSlider="True" 
                              ShowLineNumbers="True" />
</UserControl>
```

### Standalone Graph Node Editor Support

If you need a generic, standalone graph node editor control for Avalonia (with support for nodes, drag-and-drop linking, bezier connection paths, zoom-to-pointer, and custom node payload content presenters) without any Chrome DevTools Protocol or network server dependencies, install `Chrome.DevTools.Editor.Nodes` instead:

```bash
dotnet add package Chrome.DevTools.Editor.Nodes --prerelease
```

Use `NodeEditorView` in your XAML views:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:nodes="using:CDP.Editor.Nodes.Views">
    <nodes:NodeEditorView Name="NodeEditor" />
</UserControl>
```

And configure / populate it using `NodeEditorViewModel`:
```csharp
using CDP.Editor.Nodes.ViewModels;

var vm = new NodeEditorViewModel();
var node1 = vm.CreateNode("Step Node 1", 10.0, 20.0);
var node2 = vm.CreateNode("Step Node 2", 210.0, 20.0);
vm.ConnectNodes(node1, node2);
```

### MSAGL Graph Layout Support

If you want automatic graph layout capabilities (such as Sugiyama hierarchical, spring, or MDS layouts) powered by Microsoft Automatic Graph Layout (MSAGL) in the node editor, install `Chrome.DevTools.Editor.Nodes.Msagl` instead:

```bash
dotnet add package Chrome.DevTools.Editor.Nodes.Msagl --prerelease
```

Register the `MsaglLayoutProvider` in the layout providers list of your `NodeEditorViewModel` instance:
```csharp
using CDP.Editor.Nodes.Msagl;

NodeEditor.LayoutProviders.Add(new MsaglLayoutProvider());
```

### Dynamic Splits Layout Support

If you want a dynamic, user-resizable split pane layout container for your views, install `Chrome.DevTools.Editor.Splits` instead:

```bash
dotnet add package Chrome.DevTools.Editor.Splits --prerelease
```

In your XAML, declare a `SuperSplit` layout element:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:splits="using:CDP.Editor.Splits.Controls">
    <splits:SuperSplit Name="SplitLayout" />
</UserControl>
```


### Start the Server

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

### In-Process Diagnostics Inspector

If you prefer to launch the DevTools inspector client directly inside your application's process (as a replacement for Avalonia's built-in DevTools), you can use the `Chrome.DevTools.DiagnosticTools` package.

#### Install Package
Install via the .NET CLI:
```bash
dotnet add package Chrome.DevTools.DiagnosticTools
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

## Running the Sample and Inspector Apps

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

The inspector app can be run in four ways:

#### A. Install as a .NET Tool (Recommended)
You can install the inspector globally on your machine:
```bash
dotnet tool install -g Chrome.DevTools.Inspector
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
4. Record user interactions by clicking the **Recorder** tab, clicking **Start Recording**, performing clicks and text typing on the sample app, and stopping the recording. You can save/export scripts in any of the supported target formats, load them back, and click **Replay** to replay them.

#### D. Run in the Browser (WebAssembly)
A WebAssembly build of `CdpInspectorApp` is available and can be run locally or deployed. Note that the browser client operates purely in outgoing mode and does not listen on local TCP ports for incoming connections.

##### Running Locally:
1. Publish the WebAssembly project:
   ```bash
   dotnet publish samples/CdpInspectorApp.Browser/CdpInspectorApp.Browser.csproj -c Release -o publish
   ```
2. Serve the published directory:
   ```bash
   npx http-server publish/wwwroot -p 8080 -c-1
   ```
3. Open `http://127.0.0.1:8080` in your web browser.

##### Connecting to Targets in the Browser (CORS & Security):
Due to security restrictions in modern web browsers:
* **CORS Blocks HTTP Target Scanning**: Standard web browsers block cross-origin HTTP requests (such as fetching `http://127.0.0.1:9222/json` to discover targets) due to CORS policies. Clicking **Scan Targets** in the browser will fail.
* **Chrome Debugging Port Origin Block**: Standard browsers block pages from opening WebSocket connections directly to Chrome's own remote debugging port (e.g., `ws://127.0.0.1:9222`) to prevent remote control hijacking.

**How to connect successfully from a browser client**:
1. Run your target application (e.g., `CdpSampleApp` or any Avalonia application with `CdpServer` enabled) on port `9222` from your terminal:
   ```bash
   dotnet run --project samples/CdpSampleApp/CdpSampleApp.csproj
   ```
2. Open `http://127.0.0.1:9222/json` in a new browser tab. Since this is a direct navigation, it is allowed by the browser.
3. Copy either the **Target ID** (e.g., `35c3b043-4939-4848-80e3-c31b25f0b1c2`) or the **entire WebSocket Debugger URL** (`webSocketDebuggerUrl`).
4. Paste it directly into the **Host** textbox of the browser inspector (running at `http://127.0.0.1:8080`). The inspector will automatically configure and select the **Direct Connection** target.
5. Click **Connect**. Since the target app uses a custom C# WebSocket server without browser origin restrictions, the browser will connect successfully!

### 3. Testing with CdpCliTool (cdp-cli)
The `Chrome.DevTools.Cli` package installs the `cdp-cli` global tool, enabling developers, test agents, and CI/CD pipelines to list targets, execute test suites, dump tree hierarchies, run C# script evaluations, stream logs, and dispatch standalone actions without launching a GUI application.

#### A. Install as a .NET Tool
You can install the CLI tool globally on your system:
```bash
dotnet tool install -g Chrome.DevTools.Cli
```
Once installed, run it via the terminal command:
```bash
cdp-cli --help
```

#### B. Command Reference

##### 1. List Connection Targets
Scan active target processes running on a CDP host:
```bash
cdp-cli list-targets --host http://127.0.0.1:9222
```

##### 2. Run Test Flows and Suites
Execute a single YAML flow file or a directory containing a suite of YAML files:
```bash
# Run a single YAML flow and generate HTML/PDF test reports and screencast video frames
cdp-cli run scratch/test_flow.yaml --report --video --output-dir TestReports

# Run an entire folder of YAML files (Sequential Suite Runner)
cdp-cli run test-suite/ --report --output-dir TestReports

# Auto-launch target application, execute flow, and shut down
cdp-cli run scratch/test_flow.yaml --auto-launch "dotnet run --project samples/CdpSampleApp" --timeout 45000
```
**Options**:
* `-h, --host <url>`: Host address (default: `http://127.0.0.1:9222`).
* `-t, --target <id>`: Match a specific page target ID.
* `-n, --target-name <name>`: Match target by window title substring.
* `-o, --output-dir <path>`: Directory to write HTML/PDF reports and video frames (default: `TestReports`).
* `-v, --video`: Enable capturing screencast frames during test execution.
* `-r, --report`: Enable generating step-by-step PDF & HTML reports.
* `-e, --env <KEY=VAL>`: Specify environment variables for YAML parameter interpolation. Can be defined multiple times.
* `--auto-launch <path>`: Path to executable to automatically launch before connection.
* `--auto-launch-args <args>`: Arguments to pass to the auto-launched process.
* `--timeout <ms>`: Flow execution timeout in milliseconds (default: `30000`).

##### 3. Print Visual/Accessibility Hierarchy
Print the target application's tree hierarchy:
```bash
# Dump the Accessibility (AX) Tree in clear text format
cdp-cli hierarchy --type accessibility --format text

# Dump the DOM Visual Tree as JSON
cdp-cli hierarchy --type visual --format json
```

##### 4. Evaluate C# Script
Run a C# script or property evaluator against the target app:
```bash
cdp-cli eval "Window.Title"
cdp-cli eval "document.querySelector('#lblClickCount').text"
```

##### 5. Run Single Actions
Simulate a single user interaction action on a target control:
```bash
# Tap (click) a button
cdp-cli action tap "#btnClickMe"

# Type text into a textbox
cdp-cli action input "#txtSearch" "search query"

# Clear text in an input field
cdp-cli action clear "#txtSearch"

# Scroll a scroll-viewer
cdp-cli action scroll "#scrollViewer" down
```

##### 6. Stream Live Logs
Stream target Console log messages, Network requests, and system Events directly to stdout in real-time:
```bash
cdp-cli logs --type all
cdp-cli logs --type console
```

### 4. Running Headlessly with CdpRunnerTool (cdp-runner)
The `Chrome.DevTools.Runner` package installs the `cdp-runner` global tool, enabling you to launch any compiled Avalonia UI application headlessly and expose its CDP server interface on a local port. This facilitates automated web-style end-to-end testing (e.g. using Playwright, Puppeteer, or `cdp-cli`) on local, server, or CI/CD environments without opening any GUI window.

#### A. Install as a .NET Tool
You can install the runner tool globally on your system:
```bash
dotnet tool install -g Chrome.DevTools.Runner
```
Once installed, run it via the terminal command:
```bash
cdp-runner --help
```

#### B. Usage Reference
To boot an Avalonia application headlessly on port `9222`, specify the path to its compiled assembly (`.dll` or `.exe`):
```bash
cdp-runner path/to/YourAvaloniaApp.dll 9222
```
The runner will dynamically load the assembly, search for types inheriting from `Avalonia.Application` and `Avalonia.Controls.Window`, set up the headless platform, instantiate and show the main window, and start the CDP server.

---

## Test Studio & Automated Testing

The project features a **Test Studio** panel inside the inspector app, which provides a visual, interactive test suite workspace using a Flow-compatible YAML syntax.

### Key Capabilities
- **Command Toolbox**: A category-tabbed toolbox grouping actions into *Interactions* (taps, inputs, swipes, scrolls), *Assertions* (visibility and logical checks), *App & Device* (app lifecycle, orientations, geolocations, screenshots), and *Logic* (loops, retries, nested flows).
- **Interactive Execution & Debugging**: Step-by-step test execution (Play, Pause, Step Over, Stop) with real-time status updates (Pending, Running, Passed, Failed).
- **Smart Waiting**: If a target element is not found immediately during playback, the execution engine automatically retries element queries every 200ms for up to 5 seconds before failing.
- **YAML Synchronization**: A live, two-way code editor with line numbers and full TextMate-powered syntax highlighting.
- **Selector Autocomplete**: An application-wide Autocomplete service that gathers all tag names, classes, and IDs from the live DOM tree, automatically suggesting them as you type element selectors.

### Headless CI/CD Execution
The core adapter (`HeadlessTestAdapter`) allows programmatically executing Test Studio YAML scripts on arbitrary Avalonia windows headlessly during unit or integration testing:

```csharp
var adapter = new HeadlessTestAdapter();
// Runs a YAML test script on a target window instance
await adapter.RunTestAsync(myWindow, myYamlContent, isYamlContent: true);
```

### Supported Recorder Target Formats

The Recorder panel in `CdpInspectorApp` supports capturing user actions and exporting them to multiple target formats for automated testing.

The table below outlines each target format, the testing framework it targets, how it connects or is used, and a brief description:

| Target Format | Target Testing Framework | Connection / Usage with Recorder | Description |
| :--- | :--- | :--- | :--- |
| **Puppeteer** | Node.js (JavaScript) | Controls the application over standard CDP. | Great for lightweight scripting and browser-driven orchestration. |
| **Playwright Test** | Playwright (JS/TS Runner) | Connects to the active application via `chromium.connectOverCDP(host)`. | Standard modern E2E testing framework for complex workflows. |
| **Selenium C#** | Selenium WebDriver (NUnit) | Attaches to the active application session via `ChromeOptions.DebuggerAddress`. | Seamless integration into C# .NET NUnit test pipelines. |
| **Appium C#** | Appium Windows Driver (NUnit) | Connects via Appium Server (`http://127.0.0.1:4723/`) using `WindowsDriver`. | Controls Windows desktop controls utilizing dynamic selector translation. |
| **Avalonia Headless** | `Avalonia.Headless.XUnit` | Runs directly in-process. Simulates inputs using window mouse/keyboard extensions. | High-performance headless tests running inside the xUnit test runner. |

### Code Generation Examples

Below are complete examples of the generated code for each recording target format representing a recorded sequence of: setting viewport size to 1024x768, navigating to `http://localhost:9222/foo`, clicking `#btnClick`, entering text into `#txtInput`, asserting `#btnClick` is visible, and asserting `#hidden` is hidden.

<details>
<summary>1. Puppeteer (JavaScript)</summary>

```javascript
const puppeteer = require('puppeteer');

(async () => {
  const browser = await puppeteer.launch({ headless: false });
  const page = await browser.newPage();
  await page.setViewport({ width: 1024, height: 768 });
  await page.goto('http://localhost:9222/foo');

  // Click on element
  const element_2 = await page.waitForSelector('#btnClick');
  await element_2.click();

  // Type text in element
  const element_3 = await page.waitForSelector('#txtInput');
  await element_3.type('hello "world" \\ test');

  // Assert element is visible
  await page.waitForSelector('#btnClick', { visible: true });

  // Assert element is hidden
  await page.waitForSelector('#hidden', { hidden: true });

  await browser.close();
})();
```
</details>

<details>
<summary>2. Playwright Test (JavaScript/TypeScript)</summary>

```typescript
import { test, expect, chromium } from '@playwright/test';

test.describe('CDP Recorded Tests', () => {
  test('recorded test', async () => {
    const browser = await chromium.connectOverCDP('http://localhost:9222');
    const context = browser.contexts()[0];
    const page = context.pages()[0];

    await test.step('Set viewport size', async () => {
      await page.setViewportSize({ width: 1024, height: 768 });
    });

    await test.step('Navigate to http://localhost:9222/foo', async () => {
      await page.goto('http://localhost:9222/foo');
    });

    await test.step('Click on element #btnClick', async () => {
      const element_2 = page.locator('#btnClick');
      await element_2.click();
    });

    await test.step('Type text in element #txtInput', async () => {
      const element_3 = page.locator('#txtInput');
      await element_3.fill('hello "world" \\ test');
    });

    await test.step('Assert element #btnClick is visible', async () => {
      await expect(page.locator('#btnClick')).toBeVisible();
    });

    await test.step('Assert element #hidden is hidden', async () => {
      await expect(page.locator('#hidden')).toBeHidden();
    });

    await browser.close();
  });
});
```
</details>

<details>
<summary>3. Selenium C# (NUnit)</summary>

```csharp
using System;
using System.Drawing;
using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;

namespace SeleniumTests
{
    [TestFixture]
    public class RecordedTests
    {
        private IWebDriver _driver;
        private Actions _actions;

        [SetUp]
        public void SetUp()
        {
            var options = new ChromeOptions();
            options.DebuggerAddress = "localhost:9222";
            _driver = new ChromeDriver(options);
            _actions = new Actions(_driver);
        }

        [TearDown]
        public void TearDown()
        {
            _driver?.Quit();
        }

        [Test]
        public void TestRecordedSteps()
        {
            // Step 1: setViewport
            _driver.Manage().Window.Size = new Size(1024, 768);

            // Step 2: navigate
            _driver.Navigate().GoToUrl("http://localhost:9222/foo");

            // Step 3: click
            _driver.FindElement(By.CssSelector("#btnClick")).Click();

            // Step 4: change
            var element_3 = _driver.FindElement(By.CssSelector("#txtInput"));
            element_3.Clear();
            element_3.SendKeys("hello \"world\" \\ test");

            // Step 5: assertVisible
            Assert.IsTrue(_driver.FindElement(By.CssSelector("#btnClick")).Displayed);

            // Step 6: assertNotVisible
            bool isVisible_5 = false;
            try
            {
                isVisible_5 = _driver.FindElement(By.CssSelector("#hidden")).Displayed;
            }
            catch (NoSuchElementException)
            {
                isVisible_5 = false;
            }
            Assert.IsFalse(isVisible_5);
        }
    }
}
```
</details>

<details>
<summary>4. Appium C# (NUnit)</summary>

```csharp
using System;
using System.Drawing;
using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace AppiumTests
{
    [TestFixture]
    public class RecordedTests
    {
        private WindowsDriver<WindowsElement> _driver;

        [SetUp]
        public void SetUp()
        {
            var options = new AppiumOptions();
            options.AddAdditionalCapability("platformName", "Windows");
            options.AddAdditionalCapability("automationName", "Windows");
            options.AddAdditionalCapability("app", "Root");

            _driver = new WindowsDriver<WindowsElement>(new Uri("http://127.0.0.1:4723/"), options);
        }

        [TearDown]
        public void TearDown()
        {
            _driver?.Quit();
        }

        [Test]
        public void TestRecordedSteps()
        {
            // Step 1: setViewport
            _driver.Manage().Window.Size = new Size(1024, 768);

            // Step 2: navigate
            _driver.Navigate().GoToUrl("http://localhost:9222/foo");

            // Step 3: click
            _driver.FindElementByAccessibilityId("btnClick").Click();

            // Step 4: change
            var element_3 = _driver.FindElementByAccessibilityId("txtInput");
            element_3.Clear();
            element_3.SendKeys("hello \"world\" \\ test");

            // Step 5: assertVisible
            Assert.IsTrue(_driver.FindElementByAccessibilityId("btnClick").Displayed);

            // Step 6: assertNotVisible
            bool isVisible_5 = false;
            try
            {
                isVisible_5 = _driver.FindElementByAccessibilityId("hidden").Displayed;
            }
            catch (Exception)
            {
                isVisible_5 = false;
            }
            Assert.IsFalse(isVisible_5);
        }
    }
}
```
</details>

<details>
<summary>5. Avalonia Headless Tests (xUnit)</summary>

```csharp
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace HeadlessRecordedTests
{
    public class RecordedTests
    {
        [AvaloniaFact]
        public async Task TestRecordedScenario()
        {
            // Initialize target window
            var window = new CdpSampleApp.MainWindow();
            window.Show();

            // Wait for window layout
            await Task.Delay(100);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Input);

            // Step 1: setViewport
            window.Width = 1024;
            window.Height = 768;

            // Step 2: navigate
            if (window is CdpSampleApp.MainWindow mainWin)
            {
                mainWin.Navigate("http://localhost:9222/foo");
            }

            // Step 3: click
            var element_2 = SelectorEngine.QuerySelector(window, "#btnClick") as Control;
            Assert.NotNull(element_2);
            ClickControl(window, element_2, MouseButton.Left, RawInputModifiers.None);

            // Step 4: change
            var element_3 = SelectorEngine.QuerySelector(window, "#txtInput") as Control;
            Assert.NotNull(element_3);
            element_3.Focus();
            window.KeyTextInput("hello \"world\" \\ test");

            // Step 5: assertVisible
            var element_4 = SelectorEngine.QuerySelector(window, "#btnClick") as Control;
            Assert.NotNull(element_4);
            Assert.True(element_4.IsVisible);

            // Step 6: assertNotVisible
            var element_5 = SelectorEngine.QuerySelector(window, "#hidden") as Control;
            Assert.True(element_5 == null || !element_5.IsVisible);

            await Task.Delay(50);
        }

        private static void ClickControl(Window window, Control control, MouseButton button, RawInputModifiers modifiers)
        {
            var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window) ?? new Point();
            window.MouseDown(point, button, modifiers);
            window.MouseUp(point, button, modifiers);
        }

        private static void DragAndDrop(Window window, Control source, Control target)
        {
            var startPoint = source.TranslatePoint(new Point(source.Bounds.Width / 2, source.Bounds.Height / 2), window) ?? new Point();
            var endPoint = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window) ?? new Point();
            window.MouseMove(startPoint);
            window.MouseDown(startPoint, MouseButton.Left);
            window.MouseMove(endPoint);
            window.MouseUp(endPoint, MouseButton.Left);
        }
    }
}
```
</details>

---

## AI Coding Agents, Model Context Protocol (MCP) & Playwright

This library is designed to allow modern AI coding agents and browser automation tools to inspect, automate, and debug desktop Avalonia applications exactly like web pages.

### 1. Model Context Protocol (MCP) Integration

You can integrate your Avalonia desktop application with Google's official **Chrome DevTools MCP Server** ([ChromeDevTools/chrome-devtools-mcp](https://github.com/ChromeDevTools/chrome-devtools-mcp)). This exposes your desktop app directly to LLMs (like Claude, Gemini, GPT) in editors such as Cursor, Claude Desktop, or VS Code.

To configure the Chrome DevTools MCP server, add this to your MCP configuration settings (e.g. `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "chrome-devtools": {
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/server-chrome-devtools",
        "--port",
        "9222"
      ]
    }
  }
}
```

Once connected, your AI assistant can use tools like `inspect_dom`, `evaluate_js`, and `capture_screenshot` to interact with your running Avalonia application.

For detailed instructions and setups, see [Model Context Protocol & Playwright Integration Guide](docs/articles/mcp-and-playwright.md).

### 2. Playwright Integration

Playwright can attach directly to your running Avalonia application using the `connectOverCDP` API:

```javascript
const { chromium } = require('playwright');

(async () => {
  // Connect Playwright to the Avalonia CDP server
  const browser = await chromium.connectOverCDP('http://127.0.0.1:9222');
  const context = browser.contexts()[0];
  const page = context.pages()[0];

  // Interact with Avalonia controls via standard CSS selectors
  await page.waitForSelector('#txtInput');
  await page.fill('#txtInput', 'Hello from Playwright!');
  await page.click('#btnClickMe');

  // Capture high-fidelity screenshots of the app canvas
  await page.screenshot({ path: 'app-screenshot.png' });

  await browser.close();
})();
```

For more detailed examples, see [Model Context Protocol & Playwright Integration Guide](docs/articles/mcp-and-playwright.md).

### 3. Agent Integration Features

1. **Selector Engine**: Supports querying elements using a custom visual selector engine (`Button`, `TextBox`, `#name`, `.class`, etc.).
2. **Inspected Node `$0`**: Binds the active inspected control node to the `$0` variable in the evaluation context, letting agents evaluate C# expressions on the active element.
3. **Target Auto-Attach**: Implements `Target.setAutoAttach` so Playwright/MCP automatically handles multi-window applications and targets.
4. **Layout Metrics**: Implements legacy viewport dimensions in `Page.getLayoutMetrics` to prevent Playwright screenshot coordinate errors.

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

## CDP Self-Inspection & Agentic Testing

Enabling CDP inside the `CdpInspectorApp` client itself on port `9223` allows AI coding agents and programmatic control scripts to fully inspect, command, and verify both the client and the target concurrently. 

This enables automated integration scenarios (e.g. commanding the inspector to connect to a target, starting a recording, clicking and typing elements on the target, stopping the recording, and verifying the generated Puppeteer automation code on the inspector's UI) to run headlessly in under 10 seconds.

For full architectural details, sequence diagrams, and agent recipes, see the [Agent-Driven CDP Self-Inspection & Multi-App Testing Guide](agents.md).

---

## Development and Testing

### Running Tests

#### 1. Unit and Layout Tests
The project uses `Avalonia.Headless.XUnit` to execute headless tests. All tests run on the UI thread and avoid deadlocking by pumping jobs synchronously during async WebSocket handshakes.

To run the unit test suite:
```bash
dotnet test
```

#### 2. Playwright E2E Tests
An end-to-end automation test suite using standard Playwright is located at `tests/playwright/cdp-sample.spec.js`. You can run these tests in both **Headless** and **GUI (Non-Headless)** modes.

* **Headless Mode (Default / CI/CD)**: Runs the application in-process inside a virtual layout buffer without opening any physical window.
  ```bash
  npx playwright test
  ```
* **GUI / Non-Headless Mode (Debugging)**: Opens the desktop application window, allowing you to watch the Playwright tests automate the UI interactively.
  1. Launch the target app manually in GUI mode:
     ```bash
     dotnet run --project samples/CdpSampleApp/CdpSampleApp.csproj
     ```
  2. Run the Playwright test runner in a separate terminal window:
     ```bash
     npx playwright test
     ```
     *(Playwright will automatically reuse and attach to the running GUI application instance on port `9222`).*

### CI Pipeline
A GitHub Actions workflow is set up at `.github/workflows/dotnet.yml` to automatically verify builds and run tests on every push and pull request.
