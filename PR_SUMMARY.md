# Pull Request: Migrate Evaluation Engine to Jint & Resolve UI Dispatcher Deadlocks

## Description
This Pull Request migrates the script evaluation engine inside `Avalonia.Diagnostics.Cdp` from the heavy, compilation-bound Roslyn C# scripting engine to a lightweight, sandboxed, and modern **Jint JavaScript Engine**. It resolves recurring UI thread dispatcher deadlocks, fixes Playwright E2E browser-automation expectations, adds robust DOM API emulation via a custom JavaScript Proxy wrapper, and expands integration and network telemetry capabilities.

---

## Key Achievements

### 1. Roslyn to Jint JavaScript Evaluator Migration
* **Evaluation Engine Upgrade**: Removed `Microsoft.CodeAnalysis.CSharp.Scripting` references from evaluation pathways, migrating completely to Jint for lightweight runtime execution in both `Runtime` and `Debugger` (conditional breakpoints) domains.
* **Type-Cast Preprocessor**: Added a regular expression preprocessor to strip complex C# type casts (e.g. `((TestDataContext)Window.DataContext)`) in existing automation scripts, transforming them into valid JavaScript lookups.
* **JS DOM Proxy Interop Layer**: Implemented a dynamic JS `Proxy` wrapper in Jint's evaluation context. When a visual control is bound to `$0` or `_0`, the proxy intercepts common DOM properties (`textContent`, `innerText`, `value`, `name`, `id`, `isVisible`, `isChecked`, `selectedIndex`, `isEnabled`) and maps them dynamically. All other properties (such as `.NET` properties like `Opacity` or `Content`) are forwarded directly to the underlying Avalonia control via `Reflect.get`/`Reflect.set`.
* **Playwright Compatibility Enhancements**:
  * Handled Jint `TypeError` calls from Playwright by mapping the element handles' `h.stop()` calls to a dummy no-op function (`function() {}`).
  * Escaped double quotes inside Playwright locator selectors inside expectation interceptors (e.g. converting `TextBlock:has-text("This is the second window!")` to `TextBlock:has-text(\"This is the second window!\")`) to prevent nested double-quote syntax errors in Jint.

### 2. UI Thread Dispatch Deadlock Resolutions
* Refactored event and action execution handlers across `CdpServer`, `WindowChromeDomain`, `EmulationDomain`, and `DebuggerDomain` to check thread access (`Dispatcher.UIThread.CheckAccess()`) before calling `Dispatcher.UIThread.InvokeAsync()`. This prevents thread blocking and deadlocks when operations are already running on the UI thread.

### 3. Selector Engine & Settle Delays
* Added support in [cdp-sample.spec.js](file:///Users/wieslawsoltes/GitHub/CDP/tests/playwright/cdp-sample.spec.js) for layout transition settling delays (500ms delay inside hooks) so that clicks dispatched immediately after a tab change reliably hit the targets instead of missing them due to transition animations.
* Updated Test 8 to wait for the HTTP request to finish completely (success or failure) to prevent asynchronous request updates from running into subsequent tests and creating state race conditions.

### 4. Modern HttpClient Diagnostics Telemetry
* Enhanced `HttpKeyValueObserver` in [NetworkDomain.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/Chrome.DevTools.Protocol/Domains/NetworkDomain.cs) to match modern .NET Core `HttpClient` diagnostic keys (`System.Net.Http.Request` and `System.Net.Http.Response`) alongside legacy keys (`System.Net.Http.HttpRequestOut.Start` and `System.Net.Http.HttpRequestOut.Stop`), ensuring network telemetry is completely reliable on all runtimes.

---

## Verification & Testing

### 1. Playwright E2E Browser Automation Suite
* **Command**: `npx playwright test`
* **Result**: **All 10 tests passed successfully** in `13.7s`!
  * ✓ Verify home page elements and interaction (1.2s)
  * ✓ Verify text box input and binding updates (523ms)
  * ✓ Verify slider and check box controls (523ms)
  * ✓ Verify navigation between tabs (538ms)
  * ✓ Verify target auto-attachment with second window (568ms)
  * ✓ Verify radio button selection and toggling (545ms)
  * ✓ Verify URL-based page navigation and back interaction (1.0s)
  * ✓ Verify HTTP request execution status update (515ms)
  * ✓ Verify scroll container content visibility and input in secondary tab (1.0s)
  * ✓ Verify full multi-window interaction workflow (586ms)

### 2. C# Unit & Layout Test Suite
* **Command**: `dotnet test`
* **Result**: **All 347 unit and layout tests passed successfully** without any hangs or deadlocks.
