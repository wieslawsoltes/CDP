---
title: Getting Started
---

# Getting Started

This guide walks you through adding Chrome DevTools Protocol (CDP) support to your Avalonia application — from installing the NuGet package to connecting an inspector and verifying everything works.

## Prerequisites

Before you begin, make sure you have the following installed:

| Requirement | Minimum Version | Notes |
| :--- | :--- | :--- |
| **.NET SDK** | 10.0 | [Download .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **Avalonia UI** | 12.0 | Referenced automatically by the CDP packages |
| **IDE (optional)** | — | Visual Studio 2022+, JetBrains Rider, or VS Code with C# Dev Kit |

> [!TIP]
> Verify your .NET SDK version by running `dotnet --version` in your terminal. The output should be `10.0.x` or later.

```bash
dotnet --version
# Expected: 10.0.100 or later
```

---

## Choose Your Package

The CDP ecosystem is modular — install only what you need. The table below helps you pick the right NuGet package(s) for your scenario:

| Scenario | Package | Description |
| :--- | :--- | :--- |
| **Expose your app for remote inspection & automation** | `Chrome.DevTools.Avalonia` | Embeds a CDP server (HTTP + WebSocket) in your Avalonia app. This is the core package most users need. |
| **In-process DevTools inspector (F12)** | `Chrome.DevTools.DiagnosticTools` | Attaches a full inspector UI inside your app's process. Press F12 to open — no external tools required. |
| **Standalone inspector tool** | `Chrome.DevTools.Inspector` | .NET global tool. Install once, use from any terminal to connect to any CDP-enabled Avalonia app. |
| **OS-level automation (no CDP server in target)** | `Chrome.DevTools.Automation.OS` | Uses native accessibility APIs (AXUIElement, UIAutomation, X11) to automate apps without an embedded CDP server. |
| **Core protocol types & client library** | `Chrome.DevTools.Protocol` | Low-level CDP types and WebSocket client. Use when building custom tooling or integrations. |
| **Minimap & inline code editor** | `Chrome.DevTools.Editor.Minimap` | Standalone `MinimapTextEditor` control for AvaloniaEdit. No CDP or network dependencies. |
| **Graph node editor** | `Chrome.DevTools.Editor.Nodes` | Standalone visual node graph editor control. No CDP or network dependencies. |
| **MSAGL graph layout** | `Chrome.DevTools.Editor.Nodes.Msagl` | Adds automatic Sugiyama/spring/MDS layout to the node editor via Microsoft MSAGL. |
| **Dynamic split pane layout** | `Chrome.DevTools.Editor.Splits` | Resizable split pane container control for building multi-panel UIs. |
| **Shared inspector UI library** | `Chrome.DevTools.Inspector.Shared` | Reusable views, view models, and styles shared by the inspector applications. |

> [!NOTE]
> For the majority of use cases, you only need **`Chrome.DevTools.Avalonia`**. The other packages are additive and can be installed alongside it as needed.

---

## Install the NuGet Package

Add the core CDP server package to your Avalonia application project:

### Using .NET CLI (Recommended)

Since packages are currently published as preview versions, include the `--prerelease` flag to retrieve the latest version:

```bash
dotnet add package Chrome.DevTools.Avalonia --prerelease
```

### Using the Package Manager Console

```powershell
Install-Package Chrome.DevTools.Avalonia -IncludePrerelease
```

### Manual XML Reference

If you prefer editing your `.csproj` file directly, add the package reference with an explicit version:

```xml
<ItemGroup>
  <PackageReference Include="Chrome.DevTools.Avalonia" Version="x.y.z" />
</ItemGroup>
```

> [!TIP]
> Replace `x.y.z` with the latest version from [NuGet.org](https://www.nuget.org/packages/Chrome.DevTools.Avalonia/). You can find it by running:
> ```bash
> dotnet package search Chrome.DevTools.Avalonia --prerelease
> ```

---

## Start the CDP Server

Initialize and start the CDP server in your application startup. The typical place is in `App.axaml.cs` inside the `OnFrameworkInitializationCompleted` method.

### Complete Example: `App.axaml.cs`

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Diagnostics.Cdp;
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
            desktop.MainWindow = new MainWindow();

            // Start the CDP server on port 9222
            CdpServer.Start(9222);

            // Ensure the server stops cleanly when the app exits
            desktop.ShutdownRequested += (_, _) =>
            {
                CdpServer.Stop();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

### Key Points

| API | Description |
| :--- | :--- |
| `CdpServer.Start(port)` | Starts the HTTP listener and WebSocket server on the specified port. Each open window becomes a debuggable page target. |
| `CdpServer.Stop()` | Gracefully shuts down the server, closing all active WebSocket sessions. |

> [!IMPORTANT]
> Call `CdpServer.Start()` **after** your `MainWindow` has been assigned but **before** `base.OnFrameworkInitializationCompleted()` returns. This ensures the visual tree is available for inspection as soon as the server starts.

> [!WARNING]
> Make sure port `9222` (or your chosen port) is not already in use by another process. You can check with:
> ```bash
> # macOS / Linux
> lsof -iTCP:9222 -sTCP:LISTEN
>
> # Windows
> netstat -ano | findstr :9222
> ```

---

## Verify the Server

Once your application is running, verify the CDP server is responding by making an HTTP request to the target discovery endpoint:

### Using curl

```bash
curl http://127.0.0.1:9222/json
```

### Expected Response

You should see a JSON array listing your application's windows as debuggable targets:

```json
[
  {
    "description": "",
    "devtoolsFrontendUrl": "devtools://devtools/bundled/inspector.html?ws=127.0.0.1:9222/devtools/page/<target-id>",
    "id": "<target-id>",
    "title": "My Avalonia App",
    "type": "page",
    "url": "",
    "webSocketDebuggerUrl": "ws://127.0.0.1:9222/devtools/page/<target-id>"
  }
]
```

> [!TIP]
> You can also open `http://127.0.0.1:9222/json` directly in your web browser to view the target list. The `webSocketDebuggerUrl` is the endpoint that clients use to establish a CDP WebSocket connection.

### Additional Discovery Endpoints

| Endpoint | Description |
| :--- | :--- |
| `http://127.0.0.1:9222/json` | Lists all debuggable page targets |
| `http://127.0.0.1:9222/json/list` | Alias for `/json` |
| `http://127.0.0.1:9222/json/version` | Returns server version information |

---

## Connect the Inspector

The CDP Inspector is a full-featured, Chrome DevTools-inspired desktop application for inspecting and debugging your Avalonia app. It provides Elements, Console, Sources, Network, Performance, Application, Simulation, and Recorder panels.

### Install as a .NET Global Tool

```bash
dotnet tool install -g Chrome.DevTools.Inspector --prerelease
```

### Launch the Inspector

```bash
cdp-inspector
```

### Connect to Your App

1. The inspector will launch with a connection panel.
2. Click **Scan Targets** — it will discover your running app on `127.0.0.1:9222`.
3. Select your application from the target list.
4. Click **Connect**.

Once connected, you can:

- **Elements panel**: Browse and inspect the Avalonia visual tree as structured HTML-like elements.
- **Console panel**: Execute C# runtime expressions against your application.
- **Simulation panel**: View a live screencast preview and simulate pointer / keyboard input.
- **Recorder panel**: Record, replay, and export user interaction scripts.

> [!TIP]
> The inspector itself exposes a CDP server on port `9223`, enabling self-inspection and AI agent-driven automation of the inspector tool.

### Updating the Inspector

To update to the latest version:

```bash
dotnet tool update -g Chrome.DevTools.Inspector --prerelease
```

### Alternative: Download Pre-Built Binaries

Pre-compiled single-file executables are available under the [Releases](https://github.com/wieslawsoltes/CDP/releases) page:

| Platform | File |
| :--- | :--- |
| Windows x64 | `cdp-inspector-win-x64.zip` |
| macOS Apple Silicon | `cdp-inspector-osx-arm64.tar.gz` |
| macOS Intel | `cdp-inspector-osx-x64.tar.gz` |
| Linux x64 | `cdp-inspector-linux-x64.tar.gz` |

---

## In-Process Inspector

If you prefer an integrated debugging experience without launching a separate inspector application, the `Chrome.DevTools.DiagnosticTools` package embeds the full inspector UI directly inside your app. Press **F12** to toggle it — just like browser DevTools.

### Install the Package

```bash
dotnet add package Chrome.DevTools.DiagnosticTools --prerelease
```

### Attach the Inspector

Call `AttachCdpInspector` on your main window in `App.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
            desktop.MainWindow = new MainWindow();

            // Attach the in-process CDP Inspector
            // The parameter specifies the CDP server port (starts the server automatically)
            // Default trigger key is F12
            desktop.MainWindow.AttachCdpInspector(9222);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

### Usage

| Action | Effect |
| :--- | :--- |
| Press **F12** | Opens the in-process inspector window connected to your app |
| Press **F12** again | Closes the inspector |

> [!NOTE]
> `AttachCdpInspector(9222)` both starts the CDP server on port 9222 **and** sets up the F12 hotkey. You do **not** need to call `CdpServer.Start()` separately when using this method.

> [!TIP]
> The in-process inspector is ideal during development. For production builds, you can conditionally attach it:
> ```csharp
> #if DEBUG
>     desktop.MainWindow.AttachCdpInspector(9222);
> #endif
> ```

---

## Connect Chrome DevTools

You can connect standard Google Chrome (or any Chromium-based browser) directly to your Avalonia application for a familiar debugging experience.

### Step-by-Step Setup

1. **Start your Avalonia application** with the CDP server enabled (as configured above).

2. **Open Chrome** and navigate to:
   ```
   chrome://inspect
   ```

3. **Enable network target discovery**:
   - Check the **Discover network targets** checkbox.
   - Click **Configure...** next to it.

4. **Add your application's address**:
   ```
   127.0.0.1:9222
   ```

5. Click **Done**.

6. Your Avalonia application will appear under **Remote Target**. Click **inspect** to open the DevTools window.

> [!IMPORTANT]
> On macOS, `localhost` may resolve to the IPv6 loopback address (`::1`). Chrome's target discovery has known compatibility issues with IPv6 loopback. Always use **`127.0.0.1`** instead of `localhost` in the discovery settings.

### Available DevTools Panels

Once connected, you can use the following standard Chrome DevTools features:

| Panel | Capability |
| :--- | :--- |
| **Elements** | Walk the Avalonia visual tree as HTML-like elements (`<Window>`, `<Grid>`, `<Button>`, etc.). Hover to highlight controls with margin/padding overlays. |
| **Computed Styles** | Inspect layout properties (width, height, margin, padding, background, foreground, opacity). Edit properties live. |
| **Console** | Execute C# runtime expressions. Use `$0` to reference the currently selected element. |
| **Screenshots** | Capture high-DPI screenshots of the window or individual elements. |

> [!TIP]
> After selecting an element in the Elements panel, switch to the Console and type `$0` to get a live reference to that Avalonia control. You can then evaluate any C# expression on it, for example:
> ```
> $0.Width
> $0.DataContext
> $0.Classes
> ```

---

## Selectors Quick Reference

CDP for Avalonia uses CSS-style selectors to query the visual tree. Here are the most common patterns:

| Selector | Maps To | Example |
| :--- | :--- | :--- |
| `#name` | `Control.Name` | `#btnSubmit` |
| `TagName` | Control type name | `Button`, `TextBox`, `Grid` |
| `.className` | Avalonia style classes | `.primary`, `.accent` |
| `Parent > Child` | Direct child | `StackPanel > Button` |
| `Ancestor Descendant` | Any descendant | `Grid TextBlock` |
| `[AutomationId="value"]` | `AutomationProperties.AutomationId` | `[AutomationId="loginBtn"]` |
| `[Text="value"]` | Visible text content | `[Text="Submit"]` |
| `:contains("text")` | Content/text contains | `:contains("Hello")` |

> [!TIP]
> Prefer stable `Name` and `AutomationId` attributes over structural selectors. They survive layout changes and make your automation scripts more resilient.

---

## Supported CDP Domains

The CDP server implements the following Chrome DevTools Protocol domains:

| Domain | Key Methods | Description |
| :--- | :--- | :--- |
| **DOM** | `getDocument`, `querySelector`, `querySelectorAll`, `getBoxModel`, `getOuterHTML` | Visual tree inspection and element querying |
| **CSS** | `getComputedStyleForNode`, `getMatchedStylesForNode`, `setStyleTexts` | Style inspection and live property editing |
| **Input** | `dispatchMouseEvent`, `dispatchKeyEvent`, `insertText` | Mouse, keyboard, and text input simulation |
| **Page** | `captureScreenshot`, `startScreencast`, `stopScreencast` | High-DPI screenshots and live screen streaming |
| **Overlay** | `highlightNode`, `hideHighlight` | Visual element highlighting with bounds overlays |
| **Runtime** | `evaluate`, `callFunctionOn`, `getProperties` | C# expression evaluation via reflection |
| **Target** | `getTargets` | Multi-window target discovery |
| **Network** | `enable`, `disable`, `getResponseBody` | HTTP request monitoring and body inspection |
| **Sources** | `getWorkspaceFiles`, `getFileContent` | Source code file browsing |
| **Application** | `getResources`, `setResource`, `deleteResource` | Global resource registry management |
| **Memory** | `getLiveControls`, `collectGarbage` | Control allocation tracking and GC |
| **Recorder** | `start`, `stop` | User interaction recording for test automation |
| **Accessibility** | `getFullAXTree`, `getPartialAXTree` | Accessibility tree inspection |
| **SystemInfo** | `getInfo` | System and process information |

---

## Troubleshooting

### Common Issues

| Problem | Solution |
| :--- | :--- |
| `curl http://127.0.0.1:9222/json` returns "Connection refused" | Ensure `CdpServer.Start(9222)` is called before trying to connect. Verify the app is running. |
| Port 9222 already in use | Another process (e.g., Chrome with remote debugging) may be using the port. Use a different port or stop the conflicting process. |
| Chrome can't discover the target | Use `127.0.0.1` instead of `localhost`. Enable "Discover network targets" in `chrome://inspect`. |
| Inspector shows blank preview | On macOS, grant **Screen Recording** permission to your terminal/IDE in System Settings → Privacy & Security. **Restart the terminal completely** after granting permission. |
| OS automation returns no windows | On macOS, grant **Accessibility** permission to your terminal/IDE in System Settings → Privacy & Security → Accessibility. |

---

## Next Steps

Now that you have CDP running in your Avalonia application, explore these topics to go further:

- **[Architecture Overview](architecture.md)** — Understand the CDP server internals, session management, and domain dispatch pipeline.
- **[Selectors & Querying](selector-engine.md)** — Deep dive into CSS selector support, attribute matching, and visual tree traversal.
- **[Runtime Evaluation](runtime-domain.md)** — Execute C# expressions, inspect properties, and script your application at runtime.
- **[Recording & Replay](recorder-overview.md)** — Record user interactions and export automation scripts in Puppeteer, Playwright, Selenium, Appium, and Avalonia Headless formats.
- **[Test Studio](test-studio.md)** — Build and run visual test suites with the interactive Test Studio panel.
- **[AI Agent Integration](ai-agent-integration.md)** — Connect AI coding agents (Playwright, Puppeteer, or custom WebSocket clients) to automate your Avalonia app.
- **[OS Automation](os-automation.md)** — Use native accessibility APIs to automate apps without an embedded CDP server.
- **[API Reference](/api/)** — Full CDP domain method reference and response schemas.
