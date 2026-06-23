# Implementation Plan: End-to-End UI Automation and Testing (Playwright/Puppeteer Style)

This document provides a highly detailed technical implementation plan and status specification for enabling **Playwright/Puppeteer-style End-to-End (E2E) UI Automation and Testing** in our Chrome DevTools Protocol (CDP) server for Avalonia ([Avalonia.Diagnostics.Cdp](file:///Users/wieslawsoltes/GitHub/CDP/src/Avalonia.Diagnostics.Cdp)) and the companion inspector client ([CdpInspectorApp](file:///Users/wieslawsoltes/GitHub/CDP/samples/CdpInspectorApp)).

---

## 1. Objective & Use Cases

The goal is to expose programmatic control, navigation, inspection, and input simulation hooks of running Avalonia desktop applications over a standardized Chrome DevTools Protocol connection. This enables developer tools, test orchestrators, and AI agents to drive applications natively.

### Key Use Cases
- **Automated Regression Testing**: Running cross-platform functional GUI tests headlessly in GitHub Actions or locally without platform-specific UI drivers (e.g., Appium, WinAppDriver).
- **Interactive UI Recording & Code Export**: Capturing user clicks, text typing, and scroll operations inside a target app and automatically translating them into test scripts (flow yaml, Playwright, Puppeteer, Selenium, Appium, or Avalonia Headless).
- **Programmatic Accessibility (a11y) Auditing**: Inspecting the semantic/accessibility roles and hierarchies of controls to verify accessibility compliance.
- **Visual Debugging**: Locating visual layout bugs, element boundaries (box models), and focus paths via the CDP inspector.

---

## 2. Already Implemented Capabilities

The core of the E2E UI Automation is fully functional, spanning DOM representation, input simulation, target discovery, and self-inspection.

### A. Protocol Method Handlers
We have implemented standard Chrome DevTools Protocol domains to bridge the web-centric CDP specification with Avalonia's native C# structure:

```
        ┌────────────────────────────────────────────────────────┐
        │                 Chrome DevTools Client                 │
        └──────┬───────────────────┬──────────────────────┬──────┘
               │                   │                      │
               │ DOM Domain        │ Input Domain         │ Target Domain
               ▼                   ▼                      ▼
   ┌─────────────────────────┐ ┌─────────────────────────┐ ┌─────────────────────────┐
   │      DOM Tree Map       │ │    Input Simulation     │ │   Target / Window Map   │
   │  - getDocument          │ │  - dispatchMouseEvent   │ │  - getTargets           │
   │  - getOuterHTML         │ │  - dispatchKeyEvent     │ │  - getTargetInfo        │
   │  - querySelector(All)   │ │  - insertText           │ │  - setDiscoverTargets   │
   │  - performSearch        │ │  - emulateTouch         │ │  - attachToTarget       │
   │  - getBoxModel          │ │  - emulateTouchFromMouse│ │  - detachFromTarget     │
   │  - getNodeForLocation   │ │  - synthesizeTapGesture │ │  - sendMessageToTarget  │
   │  - requestChildNodes    │ │  - synthesizeScroll     │ └─────────────────────────┘
   │  - focus                │ │  - synthesizePinch      │
   │  - setInspectedNode     │ └─────────────────────────┘
   │  - getFlattenedDocument │
   │  - scrollIntoViewIfNeeded
   │  - setNodeValue / Name  │
   └─────────────────────────┘
```

#### 1. DOM Domain (Implemented in [DomDomain.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/Avalonia.Diagnostics.Cdp/Domains/DomDomain.cs))
- `DOM.enable` / `DOM.disable`: Subscribes/unsubscribes to visual tree mutation events.
- `DOM.getDocument`: Retrieves the document root representing the active top-level window. Maps `pierce` parameters to visual or logical tree modes (`session.UseLogicalTree = !pierce`).
- `DOM.getAttributes`: Returns serialized flat arrays of attribute name-value pairs for a registered `nodeId`.
- `DOM.describeNode`: Resolves backend node IDs or remote object IDs to return detailed element metadata.
- `DOM.requestChildNodes`: Asynchronously retrieves and pushes children to the client using `DOM.setChildNodes` events.
- `DOM.querySelector` / `DOM.querySelectorAll`: Performs CSS query selectors against the visual/logical tree using `SelectorEngine`.
- `DOM.getOuterHTML`: Generates a mock HTML serialization of a control subtree.
- `DOM.resolveNode`: Binds a visual control to a remote scripting `objectId`.
- `DOM.getBoxModel`: Computes screen coordinates representing content, padding, border, and margin quads of an element.
- `DOM.getNodeForLocation`: Hits-tests coordinates on the active TopLevel to return the hovered visual element.
- `DOM.focus`: Transfers keyboard focus to the target control.
- `DOM.setInspectedNode`: Remembers the highlighted node ID on the session overlay.
- `DOM.setAttributeValue` / `DOM.removeAttribute`: Modifies control classes, names, or general properties.
- `DOM.removeNode`: Remotely detaches control elements from their parent containers (e.g. Panels, ContentControls).
- `DOM.performSearch` / `DOM.getSearchResults` / `DOM.discardSearchResults`: Handles search queries by checking types, names, text content, and matching selectors.
- `DOM.getFlattenedDocument`: Flattens the visual hierarchy into a single linear array.
- `DOM.requestNode`: Maps a remote object ID back to a numeric node ID.
- `DOM.scrollIntoViewIfNeeded`: Invokes `BringIntoView()` on ScrollViewers / controls.
- `DOM.setNodeValue` / `DOM.setNodeName`: Directly sets text contents of TextBoxes/TextBlocks, or control names.

#### 2. Input Domain (Implemented in [InputDomain.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/Avalonia.Diagnostics.Cdp/Domains/InputDomain.cs))
- `Input.dispatchMouseEvent`: Handles simulated clicks, mouse moves, drag sequences, and wheel scrolls.
- `Input.dispatchKeyEvent`: Dispatches physical keyboard events (key downs, key ups, text input characters).
- `Input.insertText`: Focus-aware typing simulation. Fallbacks to direct Caret-based string insertion if the native focus system is uninitialized.
- `Input.emulateTouchFromMouseEvent`: Translates mouse mouse clicks/moves to raw touch point sequences.
- `Input.synthesizeTapGesture` / `Input.synthesizeScrollGesture` / `Input.synthesizePinchGesture`: Simulates gesture sequences (zooms, scrolls, taps) by interpolating coordinates over time.

#### 3. Target Domain (Implemented in [TargetDomain.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/Avalonia.Diagnostics.Cdp/Domains/TargetDomain.cs))
- `Target.getTargets`: Returns active window targets.
- `Target.getTargetInfo`: Retrives metadata for specific targets.
- `Target.setDiscoverTargets`: Discovers new TopLevels as they are registered or removed.
- `Target.attachToTarget` / `Target.detachFromTarget`: Connects or disconnects logical WebSocket sessions.

### B. Low-Level Event Construction (Reflection-Based Raw Input Injection)
Because Avalonia’s input system encapsulates platform-native event generation, the CDP server bypasses standard high-level input events. It directly instantiates and dispatches low-level `RawInputEventArgs` subclasses:
- **Event Construction**:
  - `RawPointerEventArgs` and `RawMouseWheelEventArgs` are constructed using `Activator.CreateInstance`.
  - `RawTouchEventArgs` is created using reflection-based constructor lookup to map parameters: `(IInputDevice device, ulong timestamp, IInputRoot root, RawPointerEventType type, Point position, RawInputModifiers modifiers, long touchPointId)`.
  - `RawKeyEventArgs` is instantiated dynamically via reflection constructors to ensure cross-version compatibility, supporting both the 9-parameter signature (taking `PhysicalKey` and `KeyDeviceType`) and the legacy 6-parameter constructor.
- **Input Pipeline Hooking**: Accesses the native input processing callback through:
  ```csharp
  var platformImpl = typeof(TopLevel).GetProperty("PlatformImpl", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(window);
  var inputCallback = platformImpl?.GetType().GetProperty("Input", BindingFlags.Public | BindingFlags.Instance)?.GetValue(platformImpl) as Action<RawInputEventArgs>;
  ```
- **Device Management**: Accesses `MouseDevice.Primary`, `KeyboardDevice.Instance`, and registers a custom `TouchDevice` to fire gestures under simulated touch states.

### C. Coordinate Parsing & Helper Engines
- **Double/Integer Handling**: The helper `GetDoubleOrDefault` handles floating point coordinate parsing gracefully, tolerating conversions between integer and double types returned in JSON payloads.
- **Hit Testing**: `DOM.getNodeForLocation` leverages `TopLevel.InputHitTest(Point)` to translate screen coordinates directly back to target controls.

### D. CSS/Selector Engine (`SelectorEngine.cs`)
The custom selector engine exposes a subset of CSS:
- **Selectors**:
  - Type matching (matches simple type names like `Button` or fully-qualified class names like `Avalonia.Controls.Button`).
  - ID matching (`#name` or `[id="value"]` matching `Control.Name`).
  - Class matching (`.class` matching `Control.Classes`).
  - Attribute matching: `[AccessibilityId="value"]` maps to `AutomationProperties.AutomationIdProperty`.
  - Combinators: Descendant combinator (whitespace ` `) and direct child combinator (`>`).
  - Pseudo-class `:contains("text")`: Performs case-insensitive matching against a control's `Text`, `Content`, or `Header` properties (using reflection fallback).

### E. Window Discovery & Target Management
- Walks `Application.Current.ApplicationLifetime` to discover windows in standard desktop applications.
- Dynamically notifies sessions when windows are created/closed via `Target.targetCreated` / `Target.targetDestroyed` events.

---

## 3. Missing or Needs Enhancement (Gaps & Future Work)

While the E2E UI automation capabilities cover standard Puppeteer/Playwright methods, the following capabilities represent limitations and opportunities for future enhancement:

### A. Multi-Touch Gestures
- **Current Limitation**: Tap, pinch, and scroll gestures are simulated using hardcoded single or dual pointer identifiers (`touchPointId = 1` or `2`).
- **Required Work**: Extend gesture simulation to accept arbitrary arrays of pointer coordinates and timelines. This would enable multi-finger gestures (e.g. three-finger swipe, rotation gestures with dynamic touch point updates, custom path multi-pointer sequences).

### B. Compound CSS Selector Engine Support
- **Current Limitation**: The parser splits tokens by space/combinators and matches them sequentially. It does not support complex compound selectors.
- **Required Work**:
  - Support combining multiple classes and attributes without combinators (e.g., `Button.primary[IsEnabled=true]:contains("Submit")`).
  - Implement general sibling combinator (`~`) and adjacent sibling combinator (`+`).
  - Support positional pseudo-classes: `:nth-child()`, `:first-of-type`, `:last-child`, `:only-child`, and negation `:not(...)`.
  - Support attribute wildcard operators: prefix (`^=`), suffix (`$=`), and substring (`*=`).

### C. Shadow DOM Pierce Traversal
- **Current Limitation**: The `pierce` flag in `DOM.getDocument` toggles tree walking between the full Visual tree (`pierce = true`) and the Logical tree (`pierce = false`).
- **Required Work**: True pierce traversal should seamlessly bridge boundaries of controls that are rendered across separate visual roots (such as controls hosted in `PopupRoot`, `OverlayContainer`, context menus, tooltips, or combobox dropdowns). The engine needs to pierce these separate top-level hosts and relate them back to the parent target's node map.

### D. Advanced Emulation & Automation States
- **Required Work**:
  - Touch event cancellation and multi-touch sequence interruption.
  - Native file drag-and-drop simulation over elements.
  - Automated screencast rate limiting and screen boundary overrides.

---

## 4. Avalonia-Side Architectural Design

```
    ┌───────────────────────────────┐
    │          CdpServer            │
    └──────────────┬────────────────┘
                   │ owns
                   ▼
    ┌───────────────────────────────┐
    │          CdpSession           │
    └──────┬────────────────┬───────┘
           │                │
           ▼                ▼
    ┌──────────────┐ ┌──────────────┐
    │   NodeMap    │ │ SelectorReg  │
    └──────────────┘ └──────────────┘
```

- **CdpSession**: Manages connection lifecycle. Holds state for the session, including `UseLogicalTree`, `TouchDevice`, and `InspectedNodeId`. It dispatches incoming JSON-RPC calls to relevant domain handlers and schedules frame updates.
- **NodeMap**: Tracks mappings between integer `nodeId`s and live `Visual` controls, ensuring consistent cross-session element references.
- **SelectorRegistry**: Dispatches requests to registered selector generators (`dom` vs `automation` generators).

---

## 5. Inspector-Side UI/UX Design

The client app (`CdpInspectorApp`) uses a clean MVVM structure to orchestrate simulation, inspection, and recording:

- **ElementsViewModel**:
  - Renders hierarchical DOM and Accessibility (AX) trees.
  - Updates layout specs (Margin, Padding, Bounds) using `DOM.getBoxModel` and binding values.
  - Controls pseudo-state overlays and forces pseudo-classes (e.g., `:hover`, `:active`, `:focus`) via the CSS domain.
- **SimulationViewModel**:
  - Exposes interactive widgets to trigger clicks, mouse drags, scroll sequences, text inserts, and custom key simulations.
  - Integrates device metrics overriding (device presets, mobile touch emulation, rotation).
  - Handles screencast frames and screenshots.
- **Test Studio / RecorderViewModel**:
  - Translates recorder events to structured formats: Playwright C#/JS, Puppeteer JS, Selenium C#, Appium C#, and Avalonia Headless xUnit.
  - Manages playback control loop (`PlayAsync`, `StepOverAsync`) and pauses execution dynamically.

---

## 6. Phase-by-Phase Roadmap

### Phase 1: DOM tree & Selector Engine (Completed)
- [x] Create `NodeMap` mapping integer IDs to `Visual` instances.
- [x] Wire up `DOM.getDocument` to recursively serialize the visual tree.
- [x] Implement the `SelectorEngine` with tokenization and descendant CSS matching.
- [x] Expose attribute serialization (name, classes, automation properties).

### Phase 2: Input Simulation Domain (Completed)
- [x] Implement reflection hooks targeting primary input devices.
- [x] Support mouse click, move, and wheel scroll events via `RawPointerEventArgs`.
- [x] Create touch input translation using `RawTouchEventArgs` and simulated touch devices.
- [x] Code key translation helpers mapping standard key strings to Avalonia `Key` enums.

### Phase 3: Target Lifecycle & Multi-Window Switching (Completed)
- [x] Implement `CdpServer` window registration/unregistration hooks.
- [x] Broadcast target lifetime events (`Target.targetCreated` / `Target.targetDestroyed`).
- [x] Support `Target.getTargets` and session auto-attachment.

### Phase 4: Client Recorder & Playback Integration (Completed)
- [x] Add the "Test Studio" UI module in `CdpInspectorApp`.
- [x] Build format translators (Playwright, Puppeteer, Selenium, Appium, Avalonia Headless).
- [x] Create the flow step runner with pause, play, step-over, and assertion evaluation.

### Phase 5: Advanced Selectors & Complex Gestures (Future)
- [ ] Implement multi-touch timeline gestures.
- [ ] Implement compound selectors and positional pseudo-classes.
- [ ] Piercing boundaries of context menus, overlays, and popup roots.

---

## 7. Verification & E2E Testing Strategy

Programmatic verification of the E2E automation is executed by the task-specific verification console application (`ControlApp`).

### Test Setup
- Runs an Avalonia Application headlessly (`UseHeadless(new AvaloniaHeadlessPlatformOptions())`).
- Spawns target control structures (`TextBox`, `Button`, `ScrollViewer`) and starts `CdpServer` on a test port (`9236`).
- Spawns client-side `CdpService` and `MainWindowViewModel` to connect to the test port, verifying self-inspection.

### Programmatic Assertions (Implemented in [Program.cs](file:///Users/wieslawsoltes/GitHub/CDP/scratch/ControlApp/Program.cs))
The verification suite runs the following E2E scenarios:
1. **Interactive Step Construction**: Verifies that adding step commands (Launch, Tap, Input, Assertions) dynamically builds the script list and updates the generated YAML representation.
2. **YAML Parsing**: Asserts that loading and applying flow YAML scripts back correctly parses and populates step parameters.
3. **E2E Play Execution Loop**: Executes steps against the live window. Asserts that the target button click event is fired, the ScrollViewer offset changes, and the text box text is correctly modified and cleared.
4. **Step-by-Step Execution (StepOver)**: Asserts that executing scripts step-by-step pauses and resumes execution in the expected order.
5. **Bidirectional Selection Sync**: Confirms that selecting a node in the Elements tree automatically updates the Recorder's selector.
6. **Recorder Translation Integration**: Simulates mouse clicks/keypresses on the CDP server and asserts that the WebSocket event stream is intercepted, translated, and appended to the Test Studio step list as a recorded action.
7. **Code Generation Exporters**: Evaluates output files and asserts formatting tags for Playwright, Puppeteer, Selenium C#, Appium C#, and Avalonia Headless scripts.
8. **DOM vs. Automation Selector Overrides**: Asserts that checking the `UseAutomationSelectors` option overrides element selectors with automation properties (`[AccessibilityId="value"]`) rather than standard CSS paths.
