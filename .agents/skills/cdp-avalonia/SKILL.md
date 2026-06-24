---
name: cdp-avalonia
description: Install, inspect, automate, record, replay, and verify Avalonia applications through Chrome DevTools Protocol (CDP), including dual-CDP orchestration of CdpInspectorApp and CdpSampleApp.
---

# CDP Avalonia Skill

Use this skill whenever a task involves `Avalonia.Diagnostics.Cdp`, `Chrome.DevTools.Avalonia`, `Chrome.DevTools.DiagnosticTools`, `CdpInspectorApp`, `CdpSampleApp`, Test Studio, recorder/replay behavior, visual tree inspection, accessibility inspection, or agent-driven Avalonia UI automation.

## Core Mental Model

`Avalonia.Diagnostics.Cdp` embeds a lightweight CDP server inside an Avalonia app. Agents connect to the app over WebSocket using the endpoint returned by `/json`, then use CDP domains to inspect and control the UI.

Important defaults in this repository:

- `CdpSampleApp` listens on `http://127.0.0.1:9222`.
- `CdpInspectorApp` listens on `http://127.0.0.1:9223`.
- `CdpInspectorApp` can connect to `CdpSampleApp` and also be controlled through its own CDP endpoint.
- `Runtime.evaluate` executes C# script, with browser-like helper aliases for agents.
- Test Studio report output defaults to `TestReports`, relative to the process working directory unless the UI changes it.

## Installation Patterns

Use `Chrome.DevTools.DiagnosticTools` when the app should expose F12 inspector tooling:

```shell
dotnet add package Chrome.DevTools.DiagnosticTools
```

```csharp
using Avalonia.Controls;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.AttachCdpInspector(port: 9222);
    }
}
```

Use `Chrome.DevTools.Avalonia` when the app needs only a programmatic CDP server:

```shell
dotnet add package Chrome.DevTools.Avalonia
```

```csharp
using Avalonia.Diagnostics.Cdp;

CdpServer.Start(9222);
var targetId = CdpServer.Register(mainWindow, "Main Window");
```

## Discovery And Connection

Agents should discover targets with:

```text
GET http://127.0.0.1:9222/json
GET http://127.0.0.1:9222/json/list
```

Target response shape:

```json
[
  {
    "description": "Main Window",
    "id": "target-id",
    "title": "Main Window",
    "type": "page",
    "webSocketDebuggerUrl": "ws://127.0.0.1:9222/devtools/page/target-id"
  }
]
```

After opening the WebSocket, enable the required domains:

```json
{ "id": 1, "method": "DOM.enable" }
{ "id": 2, "method": "Input.enable" }
{ "id": 3, "method": "Runtime.enable" }
{ "id": 4, "method": "Page.enable" }
```

## Selector Contract

Selectors intentionally look browser-like, while mapping to Avalonia controls.

Use these first:

- `#btnClickMe` for `Control.Name == "btnClickMe"`.
- `[id="btnClickMe"]`, `[Id="btnClickMe"]`, or `[Name="btnClickMe"]` for `Control.Name`.
- `[AccessibilityId="txtTarget"]`, `[AutomationId="txtTarget"]`, or `[AutomationProperties.AutomationId="txtTarget"]` for `AutomationProperties.AutomationId`.
- `[class~="primary"]` for Avalonia classes.
- `TextBlock:contains("Ready")`, `[Text="Ready"]`, or plain quoted text for text/content/header.

Correctness rules:

- Unknown attribute selectors must not match arbitrary controls.
- Presence selectors such as `[id]` and `[AutomationId]` must only match controls that expose those attributes.
- Prefer stable `Name` and `AutomationId` selectors over structural selectors.
- Avoid hard-coded coordinates except after resolving an element through `DOM.getBoxModel`.

## Basic Automation Recipes

Get the document and query a node:

```json
{ "id": 10, "method": "DOM.getDocument", "params": { "pierce": true, "depth": -1 } }
{ "id": 11, "method": "DOM.querySelector", "params": { "nodeId": 1, "selector": "#btnClickMe" } }
```

Get coordinates:

```json
{ "id": 12, "method": "DOM.getBoxModel", "params": { "nodeId": 42 } }
```

Click the element center using the returned `model.content` quad:

```json
{ "id": 13, "method": "Input.dispatchMouseEvent", "params": { "type": "mouseMoved", "x": 82.0, "y": 119.0, "button": "none" } }
{ "id": 14, "method": "Input.dispatchMouseEvent", "params": { "type": "mousePressed", "x": 82.0, "y": 119.0, "button": "left", "clickCount": 1 } }
{ "id": 15, "method": "Input.dispatchMouseEvent", "params": { "type": "mouseReleased", "x": 82.0, "y": 119.0, "button": "left", "clickCount": 1 } }
```

Focus and type:

```json
{ "id": 16, "method": "DOM.focus", "params": { "nodeId": 49 } }
{ "id": 17, "method": "Input.insertText", "params": { "text": "Agent demo input" } }
```

Take a screenshot:

```json
{ "id": 18, "method": "Page.captureScreenshot" }
```

## Runtime Evaluation

`Runtime.evaluate` runs C# script, not JavaScript. Available globals include:

- `Window`
- `SelectedNode`
- `Control`
- `DataContext`
- `ViewModel`
- `Query(selector)`
- `QueryAll(selector)`
- `document.querySelector(selector)`
- `document.querySelectorAll(selector)`
- `document.getElementById(id)`

Useful expressions:

```csharp
document.querySelector("#btnRefreshTargets").id
document.querySelector("#btnReplayLastVideo").isVisible
Window.DataContext.Connection.IsConnected
((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext).Recorder.TestStudio.Steps.Count
```

When using LINQ with view-model collections, cast `Window.DataContext` to the concrete view model first. C# dynamic dispatch cannot accept lambda expressions reliably.

## Dual-CDP Inspector Workflow

Use this for end-to-end inspector scenarios.

1. Start `CdpSampleApp` and `CdpInspectorApp`.
2. Connect to both CDP endpoints.
3. Enable `DOM`, `Input`, `Runtime`, and any required domains.
4. On the inspector endpoint, click `#btnRefreshTargets`.
5. On the inspector endpoint, click `#btnConnect`.
6. Assert `Window.DataContext.Connection.IsConnected == true`.
7. Open the recorder with `#TabRecorder`.
8. Assert `document.querySelector("#imgScreenshot")` exists and its image source is non-null.
9. Record, interact, replay, and verify through inspector state and generated artifacts.

Stable inspector selectors:

- `#btnTogglePreview`
- `#btnRefreshTargets`
- `#cbTargets`
- `#btnConnect`
- `#btnDisconnect`
- `#btnInspect`
- `#chkUseAutomationSelectors`
- `#TabRecorder`
- `#imgScreenshot`
- `#btnTestStudioToggleRecord`
- `#btnTestStudioPlay`
- `#btnTestStudioPause`
- `#btnTestStudioStop`
- `#btnTestStudioStep`
- `#btnTestStudioClear`
- `#chkTestStudioRecordVideo`
- `#chkTestStudioGenerateReports`
- `#txtTestStudioOutputDirectory`
- `#btnReplayLastVideo`
- `#lstSteps`
- `#acbTestStudioTargetSelector`
- `#txtTestStudioInputValue`
- `#btnTestStudioAddTap`
- `#btnTestStudioAddInputText`
- `#btnTestStudioAddScroll`
- `#btnTestStudioAddAssertVisible`
- `#btnTestStudioApplyYaml`

## Recording Through The Visual Preview

When a user asks for a live demo of the inspector controlling the sample, record through the inspector preview instead of directly clicking the sample endpoint.

Recommended flow:

1. Start Test Studio recording by clicking `#btnTestStudioToggleRecord`.
2. Discover the target element in the sample endpoint with `DOM.querySelector`.
3. Read the target box with `DOM.getBoxModel`.
4. Read the sample viewport with `Page.getLayoutMetrics`.
5. Read inspector preview bounds with `DOM.getBoxModel` on `#imgScreenshot`.
6. Map sample coordinates into preview image coordinates.
7. Dispatch mouse events to the inspector endpoint at the mapped preview coordinates.
8. For text, focus through the preview click and send `Input.insertText` to the inspector endpoint.
9. Stop recording by clicking `#btnTestStudioToggleRecord`.
10. Assert `Recorder.RecordedSteps.Count` and `Recorder.TestStudio.Steps.Count`.

Expected verification:

```csharp
var vm = ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext);
vm.Recorder.RecordedSteps.Count
vm.Recorder.TestStudio.Steps.Count
string.Join("|", vm.Recorder.TestStudio.Steps.Select((s, i) => $"{i + 1}:{s.Action}:{s.Selector}:{s.Value}"))
```

If string interpolation or dynamic LINQ causes script compilation issues, rewrite the expression using explicit concatenation and a concrete view-model cast.

## Test Studio Replay Verification

After clicking `#btnTestStudioPlay`, poll:

```csharp
var root = ((CdpInspectorApp.ViewModels.MainWindowViewModel)Window.DataContext);
var vm = root.Recorder.TestStudio;
"exec=" + vm.IsExecuting +
";recording=" + vm.HasLastRunRecording +
";steps=" + vm.Steps.Count +
";passed=" + vm.Steps.Count(s => s.Status.ToString() == "Passed") +
";failed=" + vm.Steps.Count(s => s.Status.ToString() == "Failed") +
";report=" + vm.LastReportPath +
";pdf=" + vm.LastPdfReportPath
```

Success criteria:

- `IsExecuting == false`
- `HasLastRunRecording == true`
- `failed == 0`
- `passed == steps`
- `LastReportPath` exists
- `LastPdfReportPath` exists
- `images/frame_*.jpg` count is greater than zero when video is enabled
- `images/step_*_screenshot.png` count covers the replayed steps

Optionally click `#btnReplayLastVideo` and assert the inspector remains responsive.

## Accessibility Recipes

Full accessibility tree:

```json
{ "id": 20, "method": "Accessibility.getFullAXTree" }
```

Partial tree for a DOM node:

```json
{
  "id": 21,
  "method": "Accessibility.getPartialAXTree",
  "params": { "nodeId": 42, "fetchRelatives": true }
}
```

Verify roles and properties for controls such as `Button`, `CheckBox`, `Slider`, `ProgressBar`, `TextBox`, and `TabItem`. For selection synchronization work, verify both DOM selection and AX selection.

## Coding Guidelines For CDP Work

For `Avalonia.Diagnostics.Cdp`:

- Keep protocol domains focused and small.
- Keep selector behavior deterministic.
- Do not introduce broad fallbacks that return the wrong node.
- Preserve browser-compatible CDP response shapes where practical.
- Use UI-thread dispatch for Avalonia UI interaction.
- Avoid allocations in hot paths such as traversal, dispatch, and recorder event handling when reasonable.
- Cover selector and protocol behavior with focused tests.

For inspector UI work:

- Keep code-behind thin.
- Put behavior in view models and services.
- Route CDP networking through `ICdpService`.
- Add stable `Name` attributes to controls that agents need to click or inspect.
- Do not rename existing automation-critical controls.

For verification:

- Build task-specific scenarios.
- Use `scratch/ControlApp` only when that mode fits the task and is not prohibited by the user.
- For live demo requests, launch the real processes and drive their CDP endpoints directly.
- Always include concrete logs or summarized output in the final walkthrough.

## Common Failure Diagnosis

- `DOM.querySelector("[Unknown=\"x\"]")` returns a node: attribute matching is too permissive.
- `[id]` returns most nodes: presence selector support is broken.
- `#btnTestStudioPlay` cannot be found: Test Studio stable names are missing or the recorder tab is not active.
- `document.querySelector(...)` fails in `Runtime.evaluate`: runtime document facade or imports are broken.
- Preview clicks record generic containers such as `#tabContainer`: coordinate mapping is off or the element is obscured/not visible.
- Replay passes but no frames are generated: check `IsRecordVideoEnabled`, `Page.startScreencast`, and report finalization.
- Direct sample clicks do not demonstrate inspector preview control: drive `#imgScreenshot` on the inspector endpoint.
