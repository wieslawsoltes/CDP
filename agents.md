# Agent Guide: CDP Avalonia Self-Inspection and Testing

This repository is built around agent-driven Chrome DevTools Protocol (CDP) control for Avalonia applications. Both the sample target app and the inspector app expose CDP endpoints, so agents can inspect, control, record, replay, and verify UI behavior without platform-specific desktop automation.

## Core Architecture

- `CdpSampleApp` is the target application. It starts CDP on `http://127.0.0.1:9222`.
- `CdpInspectorApp` is both an inspector client and a CDP target. It starts CDP on `http://127.0.0.1:9223`.
- Agents use `/json` or `/json/list` to discover each WebSocket endpoint.
- Agents drive UI through CDP domains such as `DOM`, `Input`, `Runtime`, `Page`, `Accessibility`, and `Recorder`.
- The inspector can connect to the sample and then control the sample through its preview pane, Test Studio, recorder, and view models.

Typical dual-CDP topology:

```text
Agent process or script
  |-- CDP WebSocket 9223 --> CdpInspectorApp
  |-- CDP WebSocket 9222 --> CdpSampleApp

CdpInspectorApp
  |-- internal CDP client connection --> CdpSampleApp
```

## When To Use Each Verification Mode

Use the mode that matches the task. Do not force every task through the same scenario.

- For code changes that affect CDP, inspector behavior, recording, replay, visual tree traversal, or sample interactions, create a task-specific verifier with explicit assertions.
- **Mandatory E2E Preview Simulation**: For all implemented code changes, performing a preview-based E2E recording and replay verification (simulating user interactions via the preview canvas `#imgScreenshot` inside the inspector and replaying them with 0 failures) is **strictly mandatory**.
- For normal feature work, `scratch/ControlApp` or `scratch/InteractiveDemo` may be used as a dynamic test harness, but the scenario must be custom to the change being verified.
- For live demos or requests that explicitly say not to use `scratch/ControlApp`, launch the real apps and drive them directly through their CDP ports.
- Do not reuse a static recording/replay script unless the user explicitly asks for that exact static flow.
- Always report the exact verification evidence: commands run, endpoints used, step counts, pass/fail counts, generated report paths, frame counts, screenshots, or relevant runtime state.

## E2E YAML Test Suite Requirements

All new inspector features, tabs, or major code changes must be covered by structured E2E YAML test files inside `tests/CdpInspectorApp.E2e/`.

### Directory Structure & Categorization
The test suite is organized into subfolders matching the feature areas:
- `tests/CdpInspectorApp.E2e/connection/` (connection flows)
- `tests/CdpInspectorApp.E2e/simulation/` (preview simulation)
- `tests/CdpInspectorApp.E2e/elements/` (visual tree & styles panel)
- `tests/CdpInspectorApp.E2e/console/` (eval scripts & REPL)
- `tests/CdpInspectorApp.E2e/sources/` (workspace files explorer)
- `tests/CdpInspectorApp.E2e/network/` (outbound HTTP request capture)
- `tests/CdpInspectorApp.E2e/performance/` (performance chart metrics)
- `tests/CdpInspectorApp.E2e/profiler/` (profiling capture runs)
- `tests/CdpInspectorApp.E2e/memory/` (memory control allocations)
- `tests/CdpInspectorApp.E2e/recorder/` (test recorder & synchronizer)

Each individual test case must be stored in its own separate `.flow.yaml` file (e.g. `connect.flow.yaml`).

### YAML Flow Structure & Metadata Separator
YAML files must be split into two documents using the standard `---` document separator:
1. **Metadata Document**: Contains `appId: "CdpInspectorApp"`, `description`, `tags`, and optionally `env`.
2. **Steps Sequence Document**: A list of sequential execution step mappings using action names as keys (e.g., `tapOn`, `delay`, `evalScript`, `assertTrue`).

Example YAML test file:
```yaml
appId: "CdpInspectorApp"
description: "Tests element view visual tree traversal"
---
- delay: "1000"
- tapOn: "#TabElements"
- delay: "1000"
- assertTrue: "document.querySelector('#TabElements') != null"
```

### Replay & Verification Runner
Verify all test suites using the `cdp-cli run` command pointing to the suite directory:
```bash
dotnet run --project src/CDP.Inspector.CLI/CDP.Inspector.CLI.csproj -- -p 9223 run tests/CdpInspectorApp.E2e/ --report --video
```

### Reusable Sub-Flow Library
To avoid redundant code and speed up authoring, common UI actions are structured as sub-flows inside `tests/CdpInspectorApp.E2e/shared/`. Invoke them in any main flow file via the `runFlow` action:
```yaml
- runFlow: "shared/connect_to_sample.yaml"
- runFlow: "shared/navigate_to_profiler.yaml"
```

### Playwright Code Generation & Headless CI
E2E testing requires exporting YAML flows to executable Playwright scripts for automated headless execution inside CI/CD pipelines.
1. **Generate Playwright Scripts**: Use the CLI `codegen` tool:
   ```bash
   dotnet run --project src/CDP.Inspector.CLI/CDP.Inspector.CLI.csproj -- codegen tests/CdpInspectorApp.E2e/ --playwright-out tests/playwright/
   ```
2. **Execute Headless Playwright Suite**:
   ```bash
   npx playwright test tests/playwright/ --headless
   ```

## Live Dual-CDP Demo Workflow

Use this flow when the user asks to demonstrate CDP in action with coding-agent control.

1. Ensure ports are free:

```shell
lsof -iTCP -sTCP:LISTEN -nP | rg '9222|9223'
```

2. Launch the sample and inspector apps interactively:

```shell
dotnet run --project samples/CdpSampleApp/CdpSampleApp.csproj
dotnet run --project samples/CdpInspectorApp/CdpInspectorApp.csproj
```

3. Discover WebSocket URLs:

```text
GET http://127.0.0.1:9222/json
GET http://127.0.0.1:9223/json
```

4. Connect to both WebSockets and enable core domains:

```json
{ "id": 1, "method": "DOM.enable" }
{ "id": 2, "method": "Input.enable" }
{ "id": 3, "method": "Runtime.enable" }
```

5. Drive the inspector to connect to the sample:

```text
Click #btnRefreshTargets on inspector
Click #btnConnect on inspector
Assert Window.DataContext.Connection.IsConnected == true on inspector
```

6. Open Recorder/Test Studio:

```text
Click #TabRecorder on inspector
Assert #imgScreenshot has a non-null Source
Assert Test Studio controls are visible
```

7. Start recording through the inspector UI:

```text
Click #btnTestStudioToggleRecord
Assert Window.DataContext.Recorder.IsRecording == true
```

8. For a visual-preview recording demo, inject interactions into the inspector preview image, not directly into the sample:

```text
Find target element in sample with DOM.querySelector, for example #btnClickMe
Read sample element coordinates with DOM.getBoxModel
Map sample coordinates to inspector #imgScreenshot bounds
Dispatch mouse events to inspector #imgScreenshot coordinates
```

9. Stop recording and inspect Test Studio state:

```text
Click #btnTestStudioToggleRecord
Assert Window.DataContext.Recorder.IsRecording == false
Read Window.DataContext.Recorder.RecordedSteps.Count
Read Window.DataContext.Recorder.TestStudio.Steps.Count
Read the action, selector, and value for each Test Studio step
```

10. Replay and verify output:

```text
Click #btnTestStudioPlay
Poll Window.DataContext.Recorder.TestStudio until IsExecuting == false
Assert every step status is Passed
Assert failed step count is 0
Assert LastReportPath and LastPdfReportPath exist
Assert images/frame_*.jpg count > 0 when video is enabled
Assert images/step_*_screenshot.png count covers the steps
Optionally click #btnReplayLastVideo and assert the inspector remains responsive
```

11. Stop the app processes you started and verify ports are clear.

## Minimal CDP Client Pattern

Agents should use JSON-RPC over WebSocket. Keep requests small and assert responses.

```json
{ "id": 10, "method": "DOM.getDocument", "params": { "pierce": true, "depth": -1 } }
{ "id": 11, "method": "DOM.querySelector", "params": { "nodeId": 1, "selector": "#btnClickMe" } }
{ "id": 12, "method": "DOM.getBoxModel", "params": { "nodeId": 42 } }
```

For a click, calculate the element center from the `content` quad and send:

```json
{ "id": 13, "method": "Input.dispatchMouseEvent", "params": { "type": "mouseMoved", "x": 82.0, "y": 119.0, "button": "none" } }
{ "id": 14, "method": "Input.dispatchMouseEvent", "params": { "type": "mousePressed", "x": 82.0, "y": 119.0, "button": "left", "clickCount": 1 } }
{ "id": 15, "method": "Input.dispatchMouseEvent", "params": { "type": "mouseReleased", "x": 82.0, "y": 119.0, "button": "left", "clickCount": 1 } }
```

For text input:

```json
{ "id": 16, "method": "DOM.focus", "params": { "nodeId": 49 } }
{ "id": 17, "method": "Input.insertText", "params": { "text": "Agent demo input" } }
```

## Selector Contract For Agents

The CDP server must remain friendly to generic browser-style agents.

- `#controlName` maps to Avalonia `Control.Name`.
- `[id="controlName"]`, `[Id="controlName"]`, and `[Name="controlName"]` map to Avalonia `Control.Name`.
- `[AccessibilityId="value"]`, `[AutomationId="value"]`, and `[AutomationProperties.AutomationId="value"]` map to `AutomationProperties.AutomationId`.
- `[class~="primary"]` maps to Avalonia classes, excluding pseudo classes such as `:pointerover`.
- `[Text="value"]`, `[text="value"]`, and `:contains("value")` can be used for visible text/content/header checks.
- Unknown attributes must not match arbitrary controls.
- Presence selectors such as `[id]` and `[AutomationId]` must only match controls that actually expose those attributes.
- Prefer stable names and automation IDs over structural selectors.

When adding or changing XAML:

- Keep existing `Name` attributes stable.
- Add `Name` to new controls that agents must click, inspect, assert, or record.
- Add `AutomationProperties.AutomationId` when the element is part of user-facing automation or generated recordings.
- Do not rename inspector controls used by automation: `btnRefreshTargets`, `btnConnect`, `btnTogglePreview`, `TabRecorder`, `btnTestStudioToggleRecord`, `btnTestStudioPlay`, `btnReplayLastVideo`, `lstSteps`, and related Test Studio controls.

## Runtime Evaluation Guidance

`Runtime.evaluate` executes C# script in the Avalonia CDP server, not JavaScript. Agents may use these globals:

- `Window` for the current top-level Avalonia window.
- `SelectedNode`, `Control`, `DataContext`, and `ViewModel` for inspected-node work.
- `Query(selector)` and `QueryAll(selector)` for Avalonia visual lookup.
- `document.querySelector(selector)`, `document.querySelectorAll(selector)`, and `document.getElementById(id)` as browser-like helpers for generic agents.

Examples:

```csharp
document.querySelector("#btnRefreshTargets").id
document.querySelector("#btnReplayLastVideo").isVisible
Window.DataContext.Connection.IsConnected
((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count
```

When using LINQ over dynamic view-model properties, cast `Window.DataContext` to the concrete view-model type first. C# dynamic dispatch does not accept lambda expressions without a concrete delegate target.

## Inspector Test Studio Stable Control Names

Agents can drive Test Studio with these selectors:

- `#btnTestStudioPlay`
- `#btnTestStudioPause`
- `#btnTestStudioStop`
- `#btnTestStudioStep`
- `#btnTestStudioClear`
- `#btnTestStudioToggleRecord`
- `#chkTestStudioRecordVideo`
- `#chkTestStudioGenerateReports`
- `#txtTestStudioOutputDirectory`
- `#btnViewHtmlReport`
- `#btnViewPdfReport`
- `#btnReplayLastVideo`
- `#lstSteps`
- `#acbTestStudioTargetSelector`
- `#txtTestStudioInputValue`
- `#btnTestStudioAddTap`
- `#btnTestStudioAddInputText`
- `#btnTestStudioAddScroll`
- `#btnTestStudioAddAssertVisible`
- `#btnTestStudioApplyYaml`

## Vector Icon Assets and Guidelines

To prevent platform-dependent emoji rendering failures (broken glyph boxes) under custom system font configurations (such as the default Inter font), the inspector UI uses vector path icons.

### Fetching Icon Geometries

All icons are sourced from the official [Microsoft Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) repository.

1. **Find the Icon Name**:
   Browse or search the [Fluent UI System Icons GitHub repository](https://github.com/microsoft/fluentui-system-icons) (or use helper sites like [fluenticons.co](https://fluenticons.co/)) to identify the desired icon name (e.g., `Play`, `Stop`, `Folder`, `DocumentSearch`).

2. **Retrieve the SVG Source**:
   To retrieve a regular 24px layout grid icon, construct the raw GitHub URL template:
   ```text
   https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/[IconName]/SVG/ic_fluent_[icon_name_lowercase]_24_regular.svg
   ```
   *Example*: For `Folder`, the path is:
   `https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/Folder/SVG/ic_fluent_folder_24_regular.svg`

3. **Programmatic Extraction Helper (Recommended)**:
   Instead of manually downloading the file and copying the path string, use this Python one-liner in your terminal to fetch the SVG, concatenate all path elements, and format the XAML `StreamGeometry` resource element automatically:
   ```bash
   python3 -c "import urllib.request, re, sys; name=sys.argv[1]; camel=name[0].upper()+name[1:]; url=f'https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/{camel}/SVG/ic_fluent_{name.lower()}_24_regular.svg'; svg=urllib.request.urlopen(url).read().decode('utf-8'); print(f'<StreamGeometry x:Key=\"{camel}Icon\">' + ' '.join(re.findall(r'd=\"([^\"]+)\"', svg)) + '</StreamGeometry>')" [IconName]
   ```
   Replace `[IconName]` with your icon name (e.g. `play`, `stop`, `folder`).

### Defining Resources

1. Add the generated `<StreamGeometry>` resource inside [Styles.axaml](file:///Users/wieslawsoltes/GitHub/CDP/src/CDP.Inspector.Shared/Styles.axaml):
   ```xml
   <StreamGeometry x:Key="CustomIcon">M12 2C...</StreamGeometry>
   ```
2. **Naming Convention**:
   - Keep the suffix `Icon` in the key (e.g., `FolderIcon`, `PlayIcon`, `VideoIcon`).
   - Keep keys unique and descriptive of the semantic meaning or design target.

### Usage in XAML Views

1. **Standard PathIcon Usage**:
   Use the standard Avalonia `<PathIcon>` control. Use a custom width/height (typically `14` or `16`) and colorize using the `Foreground` attribute:
   ```xml
   <PathIcon Data="{StaticResource FolderIcon}" Width="14" Height="14" Foreground="#ffca28"/>
   ```

2. **Buttons and Labels**:
   For buttons with both an icon and text, wrap them in a horizontal `StackPanel` with appropriate spacing and alignment:
   ```xml
   <Button Command="{Binding ToggleCommand}">
       <StackPanel Orientation="Horizontal" Spacing="6">
           <PathIcon Data="{StaticResource CustomIcon}" Width="12" Height="12"/>
           <TextBlock Text="Action" VerticalAlignment="Center"/>
       </StackPanel>
   </Button>
   ```

3. **Styling and Theme Match**:
   - Prefer theme-friendly or distinct semantic colors for `Foreground` (e.g., `#ff5252` for Stop/Delete, `#4caf50` for Play/Start, or `#2196f3` for Info/Blue items).
   - If the icon must follow the text theme, omit the `Foreground` property on the `PathIcon` so it inherits the parent control's foreground color.

### Verification
After introducing new XAML code or modifying resources:
- Run the layout unit tests to verify that the XAML files parse correctly and all referenced resource keys resolve:
  ```bash
  dotnet test tests/Avalonia.Diagnostics.Cdp.Tests/ViewsLayoutTests.cs
  ```
- Build and run the app to verify visual layout and alignment.

## Implementation Guidelines

### `Avalonia.Diagnostics.Cdp`

- Do not use the `dynamic` keyword or inherit from `DynamicObject` anywhere in the codebase. All properties, parameters, and local variables must be statically typed to ensure full assembly trimming-safety (`PublishTrimmed`) and compilation compatibility. If dynamic member lookup or runtime member resolution is necessary (e.g. in the REPL console), use explicit reflection or a manual fallback evaluator instead.
- Keep protocol domain classes focused on protocol behavior.
- Keep selector behavior deterministic and covered by tests.
- Do not add permissive selector fallbacks that silently match the wrong element.
- Keep visual-tree traversal thread-safe and UI-thread aware.
- Preserve CDP-compatible response shapes where practical, even when backed by Avalonia concepts.
- Use `JsonNode.DeepClone()` when broadcasting or reusing JSON payloads across sessions.
- Use `CdpServer.OriginalOut` instead of `Console.WriteLine` inside CDP response hooks if output redirection could recurse.

### `CdpInspectorApp`

- Keep code-behind thin; use view models and commands for state and behavior.
- Route CDP networking through `ICdpService` and `CdpService`.
- Keep tab-specific state in tab-specific view models.
- Prefer bindings and commands over direct control mutation.
- Make new interactive controls discoverable with stable `Name` attributes.
- Prefer not using XML namespace prefixes in XAML if controls/objects are part of the default Avalonia XAML namespace (`https://github.com/avaloniaui`) (e.g., use `<DataGrid>` instead of `<dg:DataGrid>`).
- Compiled bindings must always be enabled (`x:CompileBindings="True"`) on all XAML views. The use of `ReflectionBinding` or disabling compiled bindings via `x:CompileBindings="False"` is strictly forbidden to ensure full linker-trimming safety and runtime performance. All bindings must be statically compiled using proper `x:DataType` annotations and path casting where appropriate.

### OS Automation

- Create a standalone project named `CDP.Automation.OS` targeting `net10.0` with centrally managed packages.
- Common API: Implement a unified cross-platform API for querying windows, finding UI elements (via accessibility/automation hierarchies), retrieving box models, and simulating input (clicks, keyboard focus, text insertion).
- Modern .NET API & Performance Interop:
  - Prefer modern interop with `[LibraryImport]` instead of `[DllImport]` for high-performance marshalling.
  - Minimize heap allocations: use `ReadOnlySpan<char>`, `ref struct`, and avoid boxing or allocating arrays in traversal/query loops.
  - On macOS, use `AppKit` and `Accessibility` APIs via native P/Invoke.
  - On Windows, use `UIAutomationCore` (via direct COM interop/low-level native calls) or `User32` for window and input automation.
  - On Linux, use `AT-SPI2` or a high-performance DBus/sockets interface, or a lightweight fallback structure for Linux desktop automation.
- CDP Emulation:
  - Expose OS automation targets to the inspector as virtual CDP pages/apps.
  - Support `DOM.getDocument`, `DOM.querySelector`, `DOM.getBoxModel`, `Input.dispatchMouseEvent`, `Input.insertText`, `Page.captureScreenshot`, and `SystemInfo.getInfo` by mapping them directly to OS automation implementations.

### Tests

- Add focused unit tests for selector parsing, attribute exposure, runtime helpers, and protocol response shapes.
- Add integration or live-CDP verification when behavior crosses app boundaries.
- For recording/replay changes, verify both logical step state and generated artifacts: HTML report, PDF report, video frames, and step screenshots.
- Include final log output or summarized evidence in the walkthrough.

## Common Failure Modes

- If `DOM.querySelector("[Unknown=\"x\"]")` returns a real node, selector matching is too permissive.
- If `[id]` returns most of the visual tree, presence selectors are broken.
- If `Runtime.evaluate("document.querySelector(...)")` fails, the runtime document facade or imports are broken.
- If preview clicks do not record expected sample selectors, verify coordinate mapping between sample viewport and inspector `#imgScreenshot`.
- If Test Studio replay generates no frames, check `IsRecordVideoEnabled`, `Page.startScreencast`, and report finalization.
- If a direct sample click is not visible as a preview-based recording demo, drive the inspector preview instead of the sample endpoint.

