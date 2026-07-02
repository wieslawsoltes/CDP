---
title: Package Guide
---

# Package Guide

Welcome to the **Chrome DevTools Protocol (CDP) Avalonia Package Guide**. The CDP Avalonia ecosystem is designed with a modular architecture, separating the core protocol engine, OS accessibility drivers, client services, reusable UI views, specialized editor components, and ready-to-run inspector tooling. 

This guide details all **11 NuGet packages** that make up this ecosystem.

---

## Ecosystem Overview & Dependency Diagram

The packages coordinate to form a comprehensive desktop inspection and automation suite. The flow begins with low-level accessibility APIs at the bottom and builds up to the global CLI inspector and automation runner at the top:

```
Chrome.DevTools.Inspector (.NET Global Tool - GUI)
Chrome.DevTools.Cli (.NET Global Tool - CLI)
└─ Chrome.DevTools.Inspector.Shared
   ├─ Chrome.DevTools.Avalonia
   │  └─ Chrome.DevTools.Protocol
   │     └─ Chrome.DevTools.Automation.OS
   ├─ Chrome.DevTools.Editor.Minimap
   ├─ Chrome.DevTools.Editor.Nodes
   ├─ Chrome.DevTools.Editor.Nodes.Msagl
   │  └─ Chrome.DevTools.Editor.Nodes
   └─ Chrome.DevTools.Editor.Splits

Chrome.DevTools.DiagnosticTools
├─ Chrome.DevTools.Inspector.Shared  (see above)
└─ Chrome.DevTools.Avalonia          (see above)
```

Below is a summary table mapping each package:

| Package ID | Purpose | Version Status |
| :--- | :--- | :--- |
| **Chrome.DevTools.Protocol** | Base protocol server and client WebSocket routing | Preview / Centralized |
| **Chrome.DevTools.Avalonia** | Embedded CDP server for Avalonia applications | Preview / Centralized |
| **Chrome.DevTools.Automation.OS** | Platform-native window & Accessibility tree driver | Preview / Centralized |
| **Chrome.DevTools.Inspector.Shared** | Common UI components, view models, & Test Studio engines | Preview / Centralized |
| **Chrome.DevTools.DiagnosticTools** | Drop-in in-process DevTools inspector (F12) | Preview / Centralized |
| **Chrome.DevTools.Editor.Minimap** | Standalone high-performance code minimap control | Preview / Centralized |
| **Chrome.DevTools.Editor.Nodes** | Panning, zooming, and drag-and-drop node graph canvas | Preview / Centralized |
| **Chrome.DevTools.Editor.Nodes.Msagl** | Automated hierarchical graph layout provider (MSAGL) | Preview / Centralized |
| **Chrome.DevTools.Editor.Splits** | Resizable binary split panel container control | Preview / Centralized |
| **Chrome.DevTools.Inspector** | Standalone .NET global tool (GUI inspector) | Preview / Centralized |
| **Chrome.DevTools.Cli** | Standalone .NET global tool (CLI automation runner) | Preview / Centralized |

---

## Detailed Package Reference

### 1. Chrome.DevTools.Protocol

The foundational library of the entire project. It implements the JSON-RPC message pump, HTTP routing endpoints (like `/json`, `/json/list`, and `/json/version`), connection session tracking, and the client-side WebSocket manager wrapper. It also contains the client-side `OsAutomationCdpSession` which emulates CDP domains locally over the platform-native automation interfaces without needing network sockets.

| Property | Description |
| :--- | :--- |
| **Package ID** | `Chrome.DevTools.Protocol` |
| **Install Command** | `dotnet add package Chrome.DevTools.Protocol --prerelease` |
| **When to Use** | Use when building custom CDP client tools, client routing proxies, or when using the low-level WebSocket connection APIs directly. |
| **Project References** | `CDP.Automation.OS.csproj` |
| **NuGet Dependencies** | `YamlDotNet`, `SkiaSharp`, `Microsoft.Extensions.Logging.Abstractions` |

#### Key Types
* `CdpServer.cs`: The core listener and HTTP router that processes remote debugger targets.
* `CdpSession.cs`: Manages incoming and outgoing JSON-RPC packet loops for active connections.
* `ICdpService.cs` and `CdpService.cs`: Standardized client interface to discover network targets and manage socket connections.
* `OsAutomationCdpSession.cs`: Client-side virtual session executing CDP requests by translating them to native operating system accessibility nodes.

#### Usage Example
```csharp
using Chrome.DevTools.Protocol;
using Chrome.DevTools.Protocol.Client;

// Discover targets running on a local target port
var service = new CdpService();
var targets = await service.GetTargetsAsync("127.0.0.1:9222");

foreach (var target in targets)
{
    Console.WriteLine($"Target ID: {target.Id}, WebSocket Url: {target.WebSocketDebuggerUrl}");
}
```

---

### 2. Chrome.DevTools.Avalonia

The core server-side integration package for Avalonia UI applications. It embeds a lightweight server within the application process, exposing the visual tree as a DOM structure, mapping user-simulated inputs, executing C# script expressions dynamically, overlaying visual bounds layout guides, and capturing high-DPI screenshots.

| Property | Description |
| :--- | :--- |
| **Package ID** | `Chrome.DevTools.Avalonia` |
| **Install Command** | `dotnet add package Chrome.DevTools.Avalonia --prerelease` |
| **When to Use** | Add this to your main Avalonia application executable project to enable remote inspection, E2E testing, or script automation. |
| **Project References** | `Chrome.DevTools.Protocol.csproj` |
| **NuGet Dependencies** | `Avalonia`, `Avalonia.Markup.Xaml.Loader`, `SkiaSharp`, `Microsoft.CodeAnalysis.CSharp.Scripting`, `Microsoft.CodeAnalysis.Workspaces.Common`, `Microsoft.CodeAnalysis.CSharp.Workspaces`, `Microsoft.CodeAnalysis.Features`, `Microsoft.CodeAnalysis.CSharp.Features`, `Microsoft.Data.Sqlite`, `Microsoft.Extensions.Logging.Abstractions` |

#### Key Types
* `CdpServer.cs`: Extended entry point to configure, start, and stop the CDP server.
* `CdpTargetSession.cs`: Bridges specific window render passes and life-cycle events to the CDP session.
* `SelectorEngine.cs`: Parses and evaluates CSS, class, presence, text, and automation ID selectors against the visual tree.
* `AccessibilityDomain.cs`: Resolves accessibility trees and element traits.
* `CssDomain.cs`: Retrieves computed layouts and updates element styling parameters.
* `DomDomain.cs`: Maps the visual tree structure and returns coordinates.
* `InputDomain.cs`: Simulates pointer actions and raw text insertion.
* `PageDomain.cs`: Captures window snapshots and transmits screencast streams.
* `RuntimeDomain.cs`: Runs dynamic C# scripts in-app with helpful variables.

#### Usage Example
```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Diagnostics.Cdp;

public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow();
        
        // Start server on port 9222
        CdpServer.Start(9222);
    }
    base.OnFrameworkInitializationCompleted();
}
```

---

### 3. Chrome.DevTools.Automation.OS

A cross-platform native operating system automation and accessibility driver. It interfaces with platform-native accessibility engines (AXUIElement on macOS, UIAutomation on Windows, and X11/libX11 on Linux) to identify windows, read screen bounding boxes, dispatch raw OS-level mouse/keyboard coordinates, and take desktop snapshots.

| Property | Description |
| :--- | :--- |
| **Package ID** | `Chrome.DevTools.Automation.OS` |
| **Install Command** | `dotnet add package Chrome.DevTools.Automation.OS --prerelease` |
| **When to Use** | Use when implementing system-level test runners, or automating external applications that cannot be modified with an embedded CDP server. |
| **Project References** | None |
| **NuGet Dependencies** | `SkiaSharp`, `Microsoft.Extensions.Logging.Abstractions` |

#### Key Types
* `OSAutomationService.cs`: Singleton entryway retrieving window handles and target systems.
* `IOsAutomation.cs`: Interface mapping all cross-platform automation method hooks.
* `EnrichedOsAutomation.cs`: Decorator wrapper providing thread-safe caching and fallback checks.
* `OSWindow.cs`: Logical structure tracking screen coordinates and titles of active windows.
* `OSNode.cs`: Node tracking elements inside OS-level accessibility trees.

#### Usage Example
```csharp
using CDP.Automation.OS;

// List active windows via the OS-level accessibility daemon
var service = OSAutomationService.Instance;
var windows = service.GetWindows();

foreach (var win in windows)
{
    Console.WriteLine($"OS Window Title: {win.Title} (ID: {win.Id})");
}
```

---

### 4. Chrome.DevTools.Inspector.Shared

The shared UI core of the custom Chrome DevTools inspector client application. It encapsulates the MVVM pattern, offering views and view models for the Elements, CSS Styles, Console REPL, Network intercepter, Performance monitor, and Flow-compatible Test Studio panels. It also manages YAML parsing, step-by-step debug execution, and output reporting (HTML/PDF/Screencast files).

| Property | Description |
| :--- | :--- |
| **Package ID** | `Chrome.DevTools.Inspector.Shared` |
| **Install Command** | `dotnet add package Chrome.DevTools.Inspector.Shared --prerelease` |
| **When to Use** | Add this when creating custom desktop inspection utilities, embedding Test Studio inside other client applications, or automating headless test scenarios. |
| **Project References** | `Avalonia.Diagnostics.Cdp.csproj`, `CDP.Editor.Minimap.csproj`, `CDP.Editor.Nodes.csproj`, `CDP.Editor.Nodes.Msagl.csproj`, `CDP.Editor.Splits.csproj` |
| **NuGet Dependencies** | `Avalonia`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Jint`, `YamlDotNet`, `Avalonia.AvaloniaEdit`, `AvaloniaEdit.TextMate`, `TextMateSharp.Grammars`, `ProDataGrid`, `Microsoft.Extensions.Logging.Abstractions` |

#### Key Types
* `MainWindowViewModel.cs`: Main orchestrator linking all tabs and connection states.
* `ConnectionViewModel.cs`: Governs connections to either `ws://` network targets or `os://` native windows.
* `TestStudioViewModel.cs`: Powers YAML-based test flows, step validations, and report compilation.
* `HeadlessTestAdapter.cs`: Headless test runner used for executing YAML test scripts programmatically during CI/CD cycles.
* `MainView.axaml.cs`: The master UserControl aggregating all inspector views.

#### Usage Example
```csharp
using CDP.Inspector.Shared.Services;

// Programmatically run a Test Studio YAML script headlessly on an active Window
var adapter = new HeadlessTestAdapter();
string testScript = @"
steps:
  - action: tap
    selector: '#btnClick'
  - action: assertVisible
    selector: '#txtSuccess'
";

await adapter.RunTestAsync(myWindow, testScript, isYamlContent: true);
```

---

### 5. Chrome.DevTools.DiagnosticTools

A diagnostics package for in-process inspection. It serves as a direct, drop-in replacement for the standard Avalonia DevTools inspector window. Once configured, pressing **F12** inside the application opens a custom nested inspector window (hosting the shared inspector view) connected directly to the application's local server.

| Property | Description |
| :--- | :--- |
| **Package ID** | `Chrome.DevTools.DiagnosticTools` |
| **Install Command** | `dotnet add package Chrome.DevTools.DiagnosticTools --prerelease` |
| **When to Use** | Add this to your application if you want to provide built-in, local developer diagnostics with an advanced visual tree selector, console scripting, and Test Studio. |
| **Project References** | `Avalonia.Diagnostics.Cdp.csproj`, `CDP.Inspector.Shared.csproj` |
| **NuGet Dependencies** | `Avalonia` |

#### Key Types
* `CdpDiagnosticsExtensions.cs`: Houses the `AttachCdpInspector` extension method used to link the window trigger.
* `CdpInspectorWindow.axaml.cs`: Window component encapsulating the diagnostic inspector client UI.

#### Usage Example
```csharp
using Avalonia.Controls;
using CDP.DiagnosticTools;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Attaches the CDP inspector to open when pressing F12
        this.AttachCdpInspector(port: 9222);
    }
}
```

---

### 6. Chrome.DevTools.Editor.Minimap

A standalone text editor control built on top of `AvaloniaEdit` that features a scrollable pixel minimap. This package has no dependencies on the Chrome DevTools Protocol or network server modules and is optimized for file editing, YAML syntax review, and long logs.

| Property | Description |
| :--- | :--- |
| **Package ID** | `Chrome.DevTools.Editor.Minimap` |
| **Install Command** | `dotnet add package Chrome.DevTools.Editor.Minimap --prerelease` |
| **When to Use** | Use in any Avalonia application that needs a text editor with a high-performance minimap sidebar preview. |
| **Project References** | None |
| **NuGet Dependencies** | `Avalonia`, `Avalonia.AvaloniaEdit` |

#### Key Types
* `MinimapTextEditor.cs`: The main Avalonia control hosting text buffers and the minimap panel.
* `TextEditorMinimap.cs`: Renders high-performance canvas layers representation of text document flows.

#### Usage Example
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

---

### 7. Chrome.DevTools.Editor.Nodes

A standalone graphical node editor canvas control for Avalonia UI. It features an interactive grid supporting drag-and-drop linking, bezier connection paths, zoom-to-pointer, multi-selection, and customized node templates. It has no network or protocol dependencies.

| Property | Description |
| :--- | :--- |
| **Package ID** | `Chrome.DevTools.Editor.Nodes` |
| **Install Command** | `dotnet add package Chrome.DevTools.Editor.Nodes --prerelease` |
| **When to Use** | Use when building interactive node graphs, graphical state editors, or layout modeling canvases. |
| **Project References** | None |
| **NuGet Dependencies** | `Avalonia` |

#### Key Types
* `NodeEditorViewModel.cs`: Main context tracking nodes, connections, selection sets, and zoom parameters.
* `NodeViewModel.cs`: Represents a node, defining pins, bounds, and user payloads.
* `ConnectionViewModel.cs`: Defines the bezier layout path connecting two nodes.
* `NodeEditorView.axaml.cs`: Interactive control presenting nodes and connection wires on a panning canvas.
* `NodeEditorCanvasPanel.cs`: Layout container panel handling canvas child arrangements.

#### Usage Example
```csharp
using CDP.Editor.Nodes.ViewModels;

var vm = new NodeEditorViewModel();
var nodeA = vm.CreateNode("Start Node", 50.0, 50.0);
var nodeB = vm.CreateNode("End Node", 250.0, 50.0);
vm.ConnectNodes(nodeA, nodeB);
```

---

### 8. Chrome.DevTools.Editor.Nodes.Msagl

A layout integration extension for the Node Editor canvas. It integrates Microsoft's MSAGL library, offering automatic layout solvers (including Sugiyama hierarchy, spring layouts, and multidimensional scaling solvers) to programmatically position nodes and routes.

| Property | Description |
| :--- | :--- |
| **Package ID** | `Chrome.DevTools.Editor.Nodes.Msagl` |
| **Install Command** | `dotnet add package Chrome.DevTools.Editor.Nodes.Msagl --prerelease` |
| **When to Use** | Use when nodes inside your node editor are generated programmatically and require automatic layout routing. |
| **Project References** | `CDP.Editor.Nodes.csproj` |
| **NuGet Dependencies** | `AutomaticGraphLayout`, `AutomaticGraphLayout.Drawing` |

#### Key Types
* `MsaglLayoutProvider.cs`: Bridges node models to MSAGL layouts, calculating and applying spatial coordinates to nodes.

#### Usage Example
```csharp
using CDP.Editor.Nodes.Msagl;

// Register layout provider inside your NodeEditor configuration
NodeEditor.LayoutProviders.Add(new MsaglLayoutProvider());
```

---

### 9. Chrome.DevTools.Editor.Splits

A standalone layout control providing user-resizable split panes. It supports nesting horizontal and vertical grid splits, custom grab handle render layers, and sizing properties.

| Property | Description |
| :--- | :--- |
| **Package ID** | `Chrome.DevTools.Editor.Splits` |
| **Install Command** | `dotnet add package Chrome.DevTools.Editor.Splits --prerelease` |
| **When to Use** | Use when building multi-panel developer tools or resizable layout interfaces in Avalonia UI. |
| **Project References** | None |
| **NuGet Dependencies** | `Avalonia` |

#### Key Types
* `SuperSplit.cs`: Main splits grid container control.
* `SuperSplitBox.cs`: Content wrapper panel indicating hierarchy nodes.

#### Usage Example
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:splits="using:CDP.Editor.Splits.Controls">
    <splits:SuperSplit Name="SplitLayout">
        <!-- Panels added here will render as user-resizable split windows -->
    </splits:SuperSplit>
</UserControl>
```

---

### 10. Chrome.DevTools.Inspector (.NET global tool)

The standalone desktop inspector app packaged as a global .NET CLI tool. When run, it launches a custom DevTools inspector client styled with a premium dark mode theme, allowing developers to scan active ports, attach to local or remote servers, evaluate code, edit elements, and manage visual tests.

| Property | Description |
| :--- | :--- |
| **Package ID** | `Chrome.DevTools.Inspector` |
| **Install Command** | `dotnet tool install -g Chrome.DevTools.Inspector` |
| **When to Use** | Install on development machines or test agent setups to inspect and control any running CDP-enabled Avalonia application. |
| **Project References** | `CDP.Inspector.Shared.csproj` |
| **NuGet Dependencies** | `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Logging.Console` |

#### Key Types
* `Program.cs`: Entry point bootstrapping the Avalonia lifetime loop.
* `App.axaml.cs`: Configures fluent styling, Inter fonts, and compiles views.
* `MainWindow.axaml.cs`: Primary window shell containing the shared client user control.

#### Usage Example
```bash
# Install the tool globally
dotnet tool install -g Chrome.DevTools.Inspector

# Start the inspector app from the command line
cdp-inspector
```

---

### 11. Chrome.DevTools.Cli (.NET global tool)

The standalone CLI test runner and interaction tool packaged as a global .NET CLI tool. When run, it connects to a remote or auto-launched CDP-enabled target, executes YAML test flows/suites headlessly, dumps tree hierarchies, evaluates C# scripts, streams logs in real-time, and dispatches individual pointer/keyboard/scrolling actions.

| Property | Description |
| :--- | :--- |
| **Package ID** | `Chrome.DevTools.Cli` |
| **Install Command** | `dotnet tool install -g Chrome.DevTools.Cli` |
| **When to Use** | Install on test agents, headless environments, or local developer terminals to automate and assert Avalonia app visual state from bash/zsh scripts. |
| **Project References** | `CDP.Inspector.Shared.csproj`, `CDP.Inspector.CLI.csproj` |
| **NuGet Dependencies** | `Avalonia`, `Avalonia.Headless`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Logging.Console`, `System.CommandLine` |

#### Key Types
* `Program.cs`: Setup the headless Avalonia framework loop, parse commands via `System.CommandLine`, and delegate work to `CdpService` / `TestStudioViewModel`.
* `CliApp`: Minimal headless `Application` container loading themes and styling to support the view model bindings.

#### Usage Example
```bash
# Install the CLI tool
dotnet tool install -g Chrome.DevTools.Cli

# Execute a test script headlessly with report generation and video frame capture enabled
cdp-cli run scratch/test_flow.yaml --report --video --output-dir TestReports
```

