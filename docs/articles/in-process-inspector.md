---
title: In-Process Inspector
---

# In-Process Inspector

This article explains the In-Process Inspector features in the CDP Avalonia project. The In-Process Inspector provides a built-in debugging interface that runs directly within your application's process, allowing you to inspect, analyze, and test your Avalonia UI without needing to run an external client tool.

---

## Overview

The In-Process Inspector enables developers to attach a Chrome DevTools Protocol (CDP) debugging overlay directly inside the application process. Instead of forcing you to install and manage separate applications, configure complex network connections, or run command-line tools to discover your application targets, the in-process inspector embeds the full DevTools client interface as a native Avalonia window.

### How It Works

Under the hood, the In-Process Inspector utilizes a combination of:
1. **CDP Server Integration**: Automatically starting the local HTTP and WebSocket server using the `CdpServer` class from `Chrome.DevTools.Avalonia`.
2. **Keyboard Event Interception**: Listening for keydown events on your application's main window to toggle the visibility of the debugging UI.
3. **Embedded View Components**: Hosting the same advanced views, view models, and styling systems from `CDP.Inspector.Shared` that are used by the standalone inspector client.
4. **Auto-Connection**: Resolving target WebSocket endpoints locally and initializing a connection to the host application automatically upon display.

When active, the inspector operates concurrently with your application, displaying a separate panel to inspect the visual tree, run runtime C# evaluations, trace layout bounds, and record interaction workflows.

---

## Install Package

To use the in-process inspector, you must install the `Chrome.DevTools.DiagnosticTools` package, which bundles the necessary diagnostic extensions, views, and controller logic.

> [!NOTE]
> This package transitively references `Chrome.DevTools.Avalonia` and `Chrome.DevTools.Inspector.Shared`. You do not need to install them separately.

### Using .NET CLI

Since the package is in active development, make sure to append the `--prerelease` option to pull the latest preview build:

```bash
dotnet add package Chrome.DevTools.DiagnosticTools --prerelease
```

### Using Package Manager Console

Run the following command in the Package Manager Console within your IDE:

```powershell
Install-Package Chrome.DevTools.DiagnosticTools -IncludePrerelease
```

### Using MSBuild PackageReference

If you prefer to edit your project file (`.csproj`) directly, add the package to an `ItemGroup`:

```xml
<ItemGroup>
  <PackageReference Include="Chrome.DevTools.DiagnosticTools" Version="0.1.0-preview.13" />
</ItemGroup>
```

---

## Attach Inspector

To activate the in-process inspector, invoke the `AttachCdpInspector` extension method on your application's main window during startup. This method is defined in the `CdpDiagnosticsExtensions.cs` file under the `Avalonia` namespace.

The typical integration takes place in `App.axaml.cs` within the `OnFrameworkInitializationCompleted` method.

### Example Integration in `App.axaml.cs`

Here is a complete example showing how to attach the inspector in your application startup lifecycle:

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace MyAvaloniaApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 1. Initialize your main window
            desktop.MainWindow = new MainWindow();

            // 2. Attach the In-Process CDP Inspector
            // By default, this starts the server on port 9222 and binds F12 as the trigger key
            desktop.MainWindow.AttachCdpInspector(port: 9222, triggerKey: Key.F12);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

### Method Signature

The `AttachCdpInspector` extension method is defined as follows:

```csharp
public static void AttachCdpInspector(this Window window, int port = 9222, Key triggerKey = Key.F12)
```

It takes the following parameters:

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `window` | `Window` | *(this)* | The target window to inspect and register for keyboard events. |
| `port` | `int` | `9222` | The local port number on which the CDP server will listen. |
| `triggerKey` | `Key` | `Key.F12` | The `Avalonia.Input.Key` value that toggles the inspector overlay. |

> [!IMPORTANT]
> The `AttachCdpInspector` method handles both starting the CDP server and setting up the keyboard handlers. You do not need to call `CdpServer.Start()` manually if you are using the in-process inspector extensions.

---

## Trigger Key Activation

By default, pressing the **F12** key toggles the visibility of the inspector overlay window.

### Keyboard Interception

When the inspector is attached, it registers a handler on the main window's `KeyDown` event. The key interception sequence runs as follows:

1. **Press Trigger Key (F12)**: The key down event is captured.
2. **Handle Event**: The handler marks the event as handled (`e.Handled = true`) to prevent F12 from triggering other controls or default key bindings inside the main window.
3. **Toggle Inspector Window**: The handler calls the internal `ToggleInspector` method.

### Toggling Lifecycle

The toggle behavior manages the inspector window's lifecycle transparently:

* **Initial Open**: If no inspector window is active, a new `CdpInspectorWindow` is instantiated, initialized with a reference to the main window and port, and displayed.
* **Subsequent Open / Focus**: If the inspector window is already open but currently in the background or hidden behind other applications, pressing the hotkey brings the inspector to the foreground using the window's `Activate()` API.
* **Manual Close**: If you close the inspector window via its OS close button, the internal instance reference is safely reset to `null`. Pressing F12 again will spin up a fresh instance.

### Automatic Target Connection

When the `CdpInspectorWindow` loads, it performs the following sequence on the UI thread using the Avalonia `Dispatcher`:

1. Registers the target window with `CdpServer` to generate a unique target ID.
2. Builds the target discovery WebSocket URL (e.g., `ws://127.0.0.1:9222/devtools/page/<target-id>`).
3. Connects the embedded `CdpService` instance directly to the host application's local socket endpoint.

This sequence allows the inspector to display the host window's current state immediately upon load.

---

## Comparison: In-Process vs Standalone Client

Depending on your workflows, environment constraints, and project requirements, you can choose between the **In-Process Inspector** and the **Standalone Client** (`cdp-inspector`).

| Feature | In-Process Inspector | Standalone Client |
| :--- | :--- | :--- |
| **Process Model** | Runs inside the target application's process. | Runs as a separate process. |
| **Memory & GC** | Shares heap allocations and garbage collection cycles with your app. | Completely isolated memory space; zero target heap overhead. |
| **Port Requirements** | Requires starting a local CDP port to route internal client/server messages. | Connects remotely via TCP/IP to any accessible port. |
| **Installation** | Requires adding a NuGet package to the application project. | Installed globally via `.NET SDK` (`dotnet tool install`). |
| **Deployment Footprint** | Adds diagnostic UI DLLs and styles to your application's output bundle. | Target app output remains minimal (only core server is needed). |
| **Window Hierarchy** | Acts as an owned window relative to the host application. | Operates as a completely independent system window. |
| **Security Boundaries** | Perfect for secure environments where network ports are locked down. | Subject to local firewall rules and network policies. |
| **Use Case** | Rapid developer self-inspection, quick local debug iterations. | QA teams, CI/CD automation, performance profiling, remote debugging. |

### When to Choose the In-Process Inspector

* **Local Debugging**: When you need a quick, zero-setup inspector during active development. Just press F12 and start diagnosing layouts, testing CSS properties, or evaluating expressions.
* **Bypassing Network Restrictions**: If local security policies, firewalls, or container boundaries restrict network loopback port communication, running in-process eliminates network connectivity issues.
* **Demonstrating Features**: When distributing demo versions of your application to team members, stakeholders, or clients who want to inspect the visual tree without installing .NET SDK or command-line tools.

### When to Choose the Standalone Client

* **Minimizing Production Overhead**: If you want to keep production binaries as small as possible and avoid shipping views, editor themes, and layouts in your release build.
* **Remote and Multi-Device Debugging**: If your application is running on a different device, a virtual machine, a mobile emulator, or an embedded Linux device, you can connect the standalone client remotely across the network.
* **Performance Profiling**: If you want to analyze memory allocations, garbage collection logs, or render frames without the inspector's own UI rendering overhead affecting the results.

---

## Advanced Configurations

### Conditional Compilation

To ensure that the in-process inspector resources and hotkeys are completely excluded from production release builds, use C# preprocessor directives in your `App.axaml.cs`:

```csharp
#if DEBUG
    // Attach the in-process inspector only in Debug builds
    desktop.MainWindow.AttachCdpInspector(9222);
#endif
```

### Custom Hotkey Bindings

If your application already uses F12 for another feature, you can easily change the trigger key by passing a different value from the `Avalonia.Input.Key` enumeration:

```csharp
// Use F11 as the toggle key instead of F12
desktop.MainWindow.AttachCdpInspector(port: 9222, triggerKey: Key.F11);
```

### Accessing the Inspector Programmatically

The `CdpInspectorWindow` exposes API methods that allow you to interact with the active debugging session programmatically. This is useful for writing automated verifier scripts or custom developer hooks:

```csharp
// Set the active tab in the element tree inspector
inspectorWindow.SetSelectedTreeTabIndex(0);

// Find a specific node's ID using a CSS-style selector
int buttonNodeId = inspectorWindow.FindDomNodeId("#btnClickMe");

// Highlight the selected node in the UI
inspectorWindow.SelectDomNodeById(buttonNodeId);
```

These features make the in-process inspector a versatile tool for both manual debugging sessions and local automated inspections.
