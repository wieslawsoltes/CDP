---
title: Test Studio
---

# Test Studio

The Test Studio is the visual test authoring, editing, and execution workspace within the CDP Inspector. It provides a full-featured environment for building, managing, and running automated UI test flows against connected Avalonia applications.

## Overview

Test Studio combines:
- A **step list** for visually building and editing test sequences
- A **YAML code editor** with syntax highlighting and live synchronization
- A **workspace file browser** for managing `.flow.yaml` test files
- **Execution controls** for running, pausing, stepping, and stopping tests
- **Report generation** for HTML and PDF test results with screenshots
- **Video recording** of test playback sessions
- **Environment variable management** for parameterized test flows

## Accessing Test Studio

Test Studio is accessed through the **Recorder** tab in the Inspector. When the tab is selected, the Test Studio workspace appears below the recording controls with the step list, YAML editor, and execution toolbar.

### Key UI Controls

| Control | Selector | Purpose |
|---------|----------|---------|
| Play | `#btnTestStudioPlay` | Run all steps from current position |
| Pause | `#btnTestStudioPause` | Pause execution at current step |
| Stop | `#btnTestStudioStop` | Cancel execution |
| Step | `#btnTestStudioStep` | Execute single step and pause |
| Clear | `#btnTestStudioClear` | Remove all steps |
| Toggle Record | `#btnTestStudioToggleRecord` | Start/stop recording |
| Record Video | `#chkTestStudioRecordVideo` | Enable video capture during replay |
| Generate Reports | `#chkTestStudioGenerateReports` | Enable HTML/PDF report output |
| Output Directory | `#txtTestStudioOutputDirectory` | Report and video output path |
| View HTML Report | `#btnViewHtmlReport` | Open last HTML report |
| View PDF Report | `#btnViewPdfReport` | Open last PDF report |
| Replay Video | `#btnReplayLastVideo` | Play back recorded video |
| Apply YAML | `#btnTestStudioApplyYaml` | Parse YAML into step list |

## Building Test Flows

### Adding Steps Manually

Use the toolbox controls at the bottom of the step list to add steps without recording:

1. **Target Selector** (`#acbTestStudioTargetSelector`) — Enter or select the CSS selector for the target control. This auto-complete box suggests matching controls from the connected application.

2. **Input Value** (`#txtTestStudioInputValue`) — Enter the value for text input or assertion steps.

3. **Action Buttons:**
   - `#btnTestStudioAddTap` — Add a tap/click step
   - `#btnTestStudioAddInputText` — Add a text input step
   - `#btnTestStudioAddScroll` — Add a scroll step
   - `#btnTestStudioAddAssertVisible` — Add a visibility assertion step

### Editing Steps

Each step in the list can be edited inline:
- **Action** — The step type (tap, inputText, scroll, assertVisible, etc.)
- **Selector** — The CSS selector targeting the control
- **Value** — The input value or expected assertion value

Steps support drag-and-drop reordering and right-click context menus for insert, duplicate, and delete operations.

### YAML Synchronization

The step list and YAML editor are bidirectionally synchronized:
- Changes in the step list automatically update the YAML code
- Editing YAML directly and clicking **Apply YAML** (`#btnTestStudioApplyYaml`) updates the step list
- The YAML editor provides syntax highlighting and line numbers

## Execution Engine

### Running Tests

Click **Play** (`#btnTestStudioPlay`) to execute steps sequentially. The execution engine:

1. Iterates through each `TestStudioStepModel` in order
2. Resolves the target control using `DOM.querySelector` with the step's selector
3. Executes the action (click, type, scroll, assert) via CDP protocol commands
4. Captures screenshots before and after each step (when reporting is enabled)
5. Updates the step's status: `Pending` → `Running` → `Passed` or `Failed`
6. Logs execution details to the log panel

### Step-by-Step Execution

Click **Step** (`#btnTestStudioStep`) to execute one step at a time. This pauses after each step, allowing inspection of the application state before proceeding.

### Pause and Resume

Click **Pause** (`#btnTestStudioPause`) to suspend execution at the current step. Click **Play** again to resume from where execution paused.

### Stopping Execution

Click **Stop** (`#btnTestStudioStop`) to cancel execution. The current step is marked as `Failed` and remaining steps stay `Pending`.

### Step Status Indicators

| Status | Indicator | Meaning |
|--------|-----------|---------|
| Pending | Gray | Not yet executed |
| Running | Blue spinner | Currently executing |
| Passed | Green check | Step completed successfully |
| Failed | Red cross | Step failed with error |

## Action Types

The Test Studio supports the following step action types:

| Action | Description | Requires Selector | Requires Value |
|--------|-------------|-------------------|----------------|
| `tap` / `tapOn` | Click a control | Yes | No |
| `inputText` | Type text into a control | Yes | Yes |
| `scroll` | Scroll a control | Yes | Yes (deltaY) |
| `assertVisible` | Assert control is visible | Yes | No |
| `assertNotVisible` | Assert control is not visible | Yes | No |
| `assertText` | Assert control text content | Yes | Yes |
| `assertChecked` | Assert checkbox state | Yes | Yes (true/false) |
| `assertProperty` | Assert arbitrary property | Yes | Yes (property=value) |
| `delay` | Wait for specified duration | No | Yes (milliseconds) |
| `launchApp` | Launch target application | No | Yes (command) |
| `waitForSelector` | Wait until selector matches | Yes | No |
| `screenshot` | Capture screenshot | No | Optional (filename) |
| `navigate` | Navigate to URL | No | Yes (URL) |
| `setViewport` | Set viewport dimensions | No | Yes (WxH) |
| `runFlow` | Execute another `.flow.yaml` file | No | Yes (path) |
| `eval` | Evaluate C# expression | No | Yes (expression) |
| `while` | Repeat nested steps while condition | No | Yes (condition) |

## Workspace File Browser

The left sidebar contains a file browser for managing `.flow.yaml` test files:

### Creating Flow Files

Right-click in the workspace tree and select **New Flow** to create a new `.flow.yaml` file. You can also click the **New** button in the toolbar.

### Opening Flow Files

Click any `.flow.yaml` file to load its steps into the editor. The current file path is shown in the toolbar.

### Running Test Suites

Right-click a directory and select **Run Suite** to execute all `.flow.yaml` files in that directory sequentially. Suite results show pass/fail counts in the toolbar:

- `SuitePassCount` — Number of flows that passed
- `SuiteFailCount` — Number of flows that failed

## Environment Variables

Test Studio supports parameterized test flows through environment variables:

### Defining Environments

Click **Manage Environments** to open the environment editor. Each environment is a named set of key-value pairs:

```yaml
environments:
  - name: staging
    vars:
      BASE_URL: "http://staging.example.com"
      USERNAME: "test-user"
  - name: production
    vars:
      BASE_URL: "http://prod.example.com"
      USERNAME: "admin"
```

### Using Variables in Flows

Reference environment variables in step values using `{{VARIABLE_NAME}}` syntax. The execution engine substitutes variables at runtime based on the selected environment.

## YAML IntelliSense

The YAML editor provides context-aware auto-completion powered by the `YamlIntelliSenseProvider`:

### Command Completion

When typing an `action:` value, the editor suggests all 50+ flow commands from the `FlowCommandCatalog`:

```yaml
- action: |    ← Triggers command completion popup
```

Commands are shown with their display name, category, and description.

### Parameter Completion

After an action is defined, the editor suggests available parameters specific to that command:

```yaml
- action: tapOn
  |    ← Suggests: selector, repeat, delay, retryTapIfNoChange, waitToSettleTimeoutMs
```

### Live Selector Completion

When editing a `selector:` value, the editor queries the connected application's DOM tree and suggests available selectors:

- `#controlName` — Named controls
- `.className` — Style class selectors
- `[AccessibilityId="value"]` — Automation ID selectors
- `:contains("text")` — Text content selectors
- `text:`, `id:`, `css:` — Structured selector keys

### Value Suggestions

The editor provides value suggestions based on the command type:

| Command | Suggested Values |
|---------|-----------------|
| `scroll` | `DOWN`, `UP`, `LEFT`, `RIGHT` |
| `setOrientation` | `PORTRAIT`, `LANDSCAPE_LEFT`, `LANDSCAPE_RIGHT` |
| `pressKey` | `enter`, `backspace`, `tab`, `escape`, `space`, `delete` |
| `setAirplaneMode` | `true`, `false` |
| `inputRandomNumber` | `8`, `9`, `11` (length) |

### Indentation Handling

The IntelliSense provider calculates proper YAML indentation based on the current nesting level, ensuring generated completions maintain valid YAML structure.

## Assertion Inference Engine

The Test Studio can automatically infer assertions based on the control type of the selected element. When a control is selected, the engine analyzes its type and generates relevant assertion steps.

### Built-in Rules

| Rule | Control Types | Generated Assertion |
|------|---------------|---------------------|
| Toggle | CheckBox, ToggleSwitch, RadioButton | `assertChecked` with current state |
| TextBox | TextBox, MaskedTextBox | `assertText` with current value |
| Slider | Slider | `assertProperty` for `Value` |
| Selection | ComboBox, ListBox | `assertProperty` for `SelectedItem` |
| Index | TabControl, ListBox | `assertProperty` for `SelectedIndex` |
| NumericUpDown | NumericUpDown | `assertProperty` for `Value` |
| Expander | Expander | `assertProperty` for `IsExpanded` |
| DateTime | DatePicker, TimePicker | `assertProperty` for `SelectedDate`/`SelectedTime` |
| Focus | Any focusable control | `assertProperty` for `IsFocused` |
| Enabled/Disabled | Any interactive control | `assertProperty` for `IsEnabled` |
| Content | ContentControl | `assertText` with current content |
| Header | HeaderedContentControl | `assertProperty` for `Header` |
| Placeholder | TextBox | `assertProperty` for `PlaceholderText` |

### Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| Stable Delay | 400ms | Time to wait for UI to settle before inferring |
| Poll Interval | 100ms | Check interval during stability detection |

## Visual Node Editor

The Test Studio includes a visual node graph editor for composing test flows graphically:

### Features

- **Each node represents a test step** with action, selector, and value properties
- **Connections define execution order** — drag between node ports to connect
- **Auto-layout** — Automatically arranges nodes horizontally at 200px intervals
- **Bidirectional sync** — Changes in the node editor reflect in the step list and YAML, and vice versa
- **Status visualization** — Node borders are color-coded during execution:
  - Yellow border — Step currently running
  - Green border — Step passed
  - Red border — Step failed
  - Default border — Step pending

### Custom Parameter Editors

Each node displays relevant parameter fields based on its action type, with value suggestions drawn from the `FlowCommandCatalog`. File path parameters include a file picker button.

### Cycle Detection

When compiling steps from the node graph, the engine performs cycle detection using a visited-set algorithm to prevent infinite loops in the execution order.

## Telemetry Integration

Test Studio captures and displays performance telemetry during test execution:

### Performance Metrics

| Metric | Source | Description |
|--------|--------|-------------|
| CPU Usage | `Performance.getMetrics` | Process CPU utilization (%) |
| Memory (JS Heap) | `Performance.getMetrics` | Managed heap usage (MB) |
| FPS | `Performance.getMetrics` | Render frames per second |
| DOM Nodes | `DOM.getDocument` | Active visual tree node count |

### Network Metrics

| Metric | Source | Description |
|--------|--------|-------------|
| Request Count | `Network` domain events | HTTP requests during step |
| Response Bytes | `Network.getResponseBody` | Total response data |
| Request Timeline | `Network` timestamps | Waterfall visualization |

### Chart Rendering

The `TelemetryChartService` renders SkiaSharp-based performance charts with:
- Memory timeline (green line)
- CPU timeline (orange line)
- Step-duration highlight bands
- Dark theme with labeled axes and legend
- Responsive sizing based on available space

### Step Telemetry View

The `StepTelemetryView` control provides tabbed display of per-step telemetry data using the extensible `TelemetryUiRegistry`:

- **Performance tab** — CPU, memory, FPS metrics with interactive chart
- **Network tab** — Request waterfall with method, URL, status code, duration, and timeline bars

Custom telemetry providers can be registered via `TelemetryUiRegistry.Register()`.

## App Launcher

The App Launcher service (`AppLauncherService`) automates application lifecycle management:

### Features

- **Auto-launch** — Start the target application before connecting
- **Auto-connect** — Automatically discovers and connects to the launched app via CDP
- **Process tracking** — Monitors launched processes for graceful shutdown
- **macOS .app bundle support** — Uses `open -a` for macOS application bundles
- **Retry logic** — Retries connection up to 30 times with 1-second intervals
- **Graceful shutdown** — Sends `Runtime.evaluate("Avalonia.Application.Current?.Shutdown()")` before process termination

### Configuration

| Setting | Description |
|---------|-------------|
| Auto-Launch Enabled | Toggle auto-launch on test execution |
| Launch Command | Path/command to start the application |
| Working Directory | Process working directory |
| Launch Arguments | Command-line arguments |
| Launch Delay | Wait time after launch before connecting |
| Connection Port | CDP port to connect to |

## State Persistence

Test Studio persists its state across sessions via the `StateService`:

### Persisted Settings

- Output directory path
- Video recording enabled/disabled
- Report generation enabled/disabled
- Connection host address
- Auto-launch configuration
- Selected environment variables
- Workspace file path

### Storage

State is saved as JSON in `%APPDATA%/CDP.Inspector/state.json` (or platform equivalent). Each component implements `IStateProvider` with a unique `StateKey` for independent save/restore.

## Editor Gutter Indicators

The YAML editor's gutter margin displays step status indicators:

| Indicator | Color | Meaning |
|-----------|-------|---------|
| ▶ Play triangle | Gray | Idle step — click to execute single step |
| ● Red dot | Red | Recording mode active |
| ◉ Blue circle | Blue | Step currently executing |
| ✓ Green checkmark | Green | Step passed |
| ✕ Red X | Red | Step failed |
| ● Gray dot | Gray | Step pending execution |

Clicking a gutter indicator triggers single-step execution at that line, enabling targeted debugging.

## CDP Protocol Integration

Test Studio steps are executed through CDP protocol commands:

| Step Action | CDP Method(s) |
|-------------|---------------|
| `tapOn` | `DOM.querySelector` → `DOM.getBoxModel` → `Input.dispatchMouseEvent` |
| `inputText` | `DOM.querySelector` → `DOM.focus` → `Input.insertText` |
| `scroll` | `DOM.querySelector` → `Input.dispatchMouseEvent` (wheel) |
| `assertVisible` | `DOM.querySelector` → check `nodeId > 0` |
| `assertTrue` | `Runtime.evaluate` → check boolean result |
| `takeScreenshot` | `Page.captureScreenshot` |
| `launchApp` | `AppLauncherService` → process start → CDP connect |
| `setLocation` | `Emulation.setLocaleOverride` |

## Next Steps

- [YAML Test Format](/articles/yaml-test-format) — Full YAML command reference (50+ commands)
- [Code Generation](/articles/code-generation) — Export to automation frameworks
- [Test Reports](/articles/test-reports) — HTML and PDF report output
- [Video Recording](/articles/video-recording) — Screencast capture during replay
- [Headless Test Adapter](/articles/headless-test-adapter) — CI/CD test execution
- [Recording User Actions](/articles/recording-user-actions) — Auto-generate steps from interactions

