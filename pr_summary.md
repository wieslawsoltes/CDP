# Pull Request: Implement Test Studio and Interactive Recording Support

## Description
This Pull Request introduces the **Test Studio** within the Chrome DevTools Protocol (CDP) Inspector client and the core CDP library. Test Studio provides a visual, interactive test automation environment inspired by Maestro Studio. It enables developers to record user actions on a live preview of the application, review and run those actions step-by-step, edit tests directly via YAML, and perform assertions.

---

## Key Features & Enhancements

### 1. Visual Test Studio Dashboard (`CDP.Inspector.Shared`)
- **Interactive Steps List**: Displays test actions visually, showing real-time execution states (Pending, Running, Passed, Failed).
- **Execution Controls**: Full control toolbar allowing users to Play, Pause, Step Over, Stop, and Clear the test run.
- **Quick-Add Actions**: Left-hand panels to quickly add assertions (`Assert Visible`, `Assert Not Visible`), UI interactions (`Tap`, `Input Text`, `Clear Text`), and utility actions (`Launch App`, `Go Back`, `Scroll`, `Add Delay`).
- **Interactive Console Logger**: Displays a rolling, detailed log of execution steps, element lookups, and errors.
- **Synchronized YAML Editor**: A two-way editor that stays synchronized with the visual steps. Clicking **Apply YAML** loads and compiles the code directly.

### 2. Core Selector Enhancements (`Avalonia.Diagnostics.Cdp`)
- **Text-based Selectors**: Added support for the `:contains("text")` pseudo-class to locate visual tree nodes containing specific text contents.
- **Fallbacks**: Implemented plain-text query fallbacks. When a query selector does not match standard CSS structure or is quoted, the engine falls back to searching for any visual control exhibiting the target content.

### 3. Debugger & Smart Waiting Execution Engine
- **Step Execution Loop**: Backed by a task-cancellable execution runner that processes steps sequentially.
- **Smart Waiting**: If a target element is not found immediately during playback, the engine automatically retries lookups every 200ms for up to 5 seconds before failing.
- **Real-Time Actions Verification**: Adding steps visually triggers immediate action execution and assertion verification on the target application in real-time, verifying test steps immediately upon creation.

### 4. Interactive Event Recording & Translation
- **Simulation Interception**: Catches dispatched user inputs (clicks, keypresses, scrolls) from the `SimulationView` and translates them into appropriate Test Studio actions (e.g. `tapOn`, `inputText`, `pressKey`).
- **Key Event Interception**: Translates `keydown` events into `pressKey` steps.

### 5. YamlDotNet Serialization Migration
- Replaced the custom line parser with the standard `YamlDotNet` package (version `16.3.0`).
- Implemented a custom `ForceDoubleQuoteEmitter` to output string values (such as target selectors, e.g. `"#btnTarget"`) properly wrapped in double quotes while formatting keys and actions cleanly without quotes.

### 6. Critical VM & View Binding Fixes
- **Two-Way Mode Switcher Binding**: Fixed the mode selector in `RecorderView.axaml` to use `Mode=TwoWay` for `IsTestStudioActive`. This ensures the ViewModel backing property updates correctly, allowing incoming event translations to be routed to the Test Studio.
- **Dynamic Command CanExecute Updates**: Added a call to `RaiseCommandCanExecuteChanged()` at the end of the steps collection modification event. This immediately updates the visual disabled/enabled state of the Play, Pause, Stop, and Step Over buttons in the UI when the steps collection is modified.

---

## Verification Results

### 1. Unit Tests
All 79 unit tests pass successfully, including:
- `TestStudioYamlParserTests`: Verifies correct parsing and round-trip formatting of all supported YAML action steps.
- `SelectorTests`: Verifies `:contains(...)` text selector resolution.
```bash
Passed!  - Failed:     0, Passed:    79, Skipped:     0, Total:    79, Duration: 6 s
```

### 2. Headless E2E Verifications (`ControlApp`)
Executed all 7 task-specific E2E verification scenarios inside the headless `ControlApp` verification suite, confirming complete compliance:
- **Scenario 1**: Interactive step construction & auto-YAML synchronization.
- **Scenario 2**: YAML parsing and step loading.
- **Scenario 3**: Replay and execution loop.
- **Scenario 4**: StepOver command.
- **Scenario 5**: Bidirectional selection synchronization.
- **Scenario 6**: Recorder event translation integration.
- **Scenario 7**: View command bindings and enabled state verification.
