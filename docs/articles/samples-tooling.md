---
title: Samples and Tooling
---

# Samples and Tooling

The CDP repository includes sample applications, tooling packages, and the Inspector global tool for exploring and demonstrating the Chrome DevTools Protocol integration with Avalonia.

## Sample Applications

### CdpSampleApp

A minimal Avalonia application that demonstrates CDP server integration. It serves as a target for the Inspector and for automated testing.

**Location:** `samples/CdpSampleApp/`

**Run:**
```bash
dotnet run --project samples/CdpSampleApp/CdpSampleApp.csproj
```

**Features:**
- Starts CDP server on `http://127.0.0.1:9222`
- Contains interactive controls (buttons, text fields, checkboxes, lists) for testing
- Named controls with stable `Name` attributes for selector testing
- Demonstrates `AttachCdpInspector()` in-process inspector capability

**Key Controls:**

| Control | Type | Name |
|---------|------|------|
| Click Me | Button | `#btnClickMe` |
| Input Field | TextBox | `#txtInput` |
| Submit | Button | `#btnSubmit` |
| Status Label | TextBlock | `#lblStatus` |
| Item List | ListBox | `#lstItems` |
| Settings | Button | `#btnSettings` |

### CdpInspectorApp

The full-featured Inspector application that connects to CDP targets and provides DevTools-style inspection.

**Location:** `samples/CdpInspectorApp/`

**Run:**
```bash
dotnet run --project samples/CdpInspectorApp/CdpInspectorApp.csproj
```

**Features:**
- Starts its own CDP server on `http://127.0.0.1:9223`
- Connects to target applications via CDP
- Full panel suite: Elements, Console, Sources, Network, Performance, Memory, Application, Simulation, Audits, Events
- Recorder and Test Studio with code generation
- OS Automation target browser

## Inspector Global Tool

The CDP Inspector is available as a .NET global tool for quick installation:

```bash
dotnet tool install -g Chrome.DevTools.Inspector --prerelease
```

**Launch:**
```bash
cdp-inspector
```

**With custom port:**
```bash
cdp-inspector --port 9223 --target 9222
```

## scratch/ControlApp

A dynamic test harness used for verification and development:

**Location:** `scratch/ControlApp/`

**Purpose:**
- Quickly test new CDP features without modifying sample apps
- Run verification scripts against a fresh application instance
- Used by CI agents for automated verification

**Run:**
```bash
dotnet run --project scratch/ControlApp/ControlApp.csproj
```

## Tooling Packages

### Chrome.DevTools.DiagnosticTools

Provides extension methods for attaching the in-process inspector:

```csharp
using Avalonia.Diagnostics.Cdp;

// In App.axaml.cs OnFrameworkInitializationCompleted
if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
{
    desktop.MainWindow = new MainWindow();
    desktop.MainWindow.AttachCdpInspector(9222);
}
```

### Editor Packages

Standalone editor controls used by the Inspector and available for any Avalonia application:

| Package | Description | Use Case |
|---------|-------------|----------|
| `Chrome.DevTools.Editor.Minimap` | Text editor with minimap sidebar | Source code viewing |
| `Chrome.DevTools.Editor.Nodes` | Graph node editor | Relationship visualization |
| `Chrome.DevTools.Editor.Nodes.Msagl` | MSAGL layout for node editor | Automatic graph layout |
| `Chrome.DevTools.Editor.Splits` | Dynamic split pane container | Panel layouts |

## Project Structure

```
CDP/
├── src/
│   ├── Avalonia.Diagnostics.Cdp/          # Core CDP server
│   ├── Chrome.DevTools.Protocol/          # Protocol types + automation
│   ├── CDP.Automation.OS/                 # OS automation backend
│   ├── CDP.Inspector.Shared/              # Inspector shared UI + ViewModels
│   ├── CDP.DiagnosticTools/               # In-process inspector extension
│   ├── CDP.Editor.Minimap/                # Minimap text editor control
│   ├── CDP.Editor.Nodes/                  # Node graph editor control
│   ├── CDP.Editor.Nodes.Msagl/            # MSAGL layout provider
│   └── CDP.Editor.Splits/                 # Split pane container control
├── samples/
│   ├── CdpSampleApp/                      # Target sample application
│   └── CdpInspectorApp/                   # Inspector application
├── tests/
│   └── Avalonia.Diagnostics.Cdp.Tests/    # Unit + integration tests
├── scratch/
│   └── ControlApp/                        # Dynamic test harness
└── docs/                                  # This documentation site
```

## Next Steps

- [Architecture](/articles/architecture) — System architecture overview
- [Package Guide](/articles/packages) — Detailed package reference
- [Build, Test, and Release](/articles/build-test-release) — Development workflow
- [In-Process Inspector](/articles/in-process-inspector) — Embedding the inspector
