# PR Description: E2E Interactive User Preview-Based Recording & Replay Simulation with Playwright Codegen

## Summary of Changes
This pull request extends the Chrome DevTools Protocol (CDP) inspector testing framework by introducing structured, feature-categorized E2E YAML test suites, a reusable sub-flow library, a CLI Playwright test generator, and mandatory visual-preview simulated test requirements.

---

## Key Achievements

### 1. Interactive Preview Simulation Architecture
* Configured E2E test verification scripts to simulate real user actions by directing click and text input events through the inspector's preview pane (`#imgScreenshot` inside the Simulation tab) instead of direct backend protocol endpoints.
* Dynamically maps target element coordinate spaces in the sample viewport to the corresponding preview screenshot coordinates.

### 2. Comprehensive 42-Case YAML E2E Test Suite
* Added 42 E2E YAML test files organized by feature categories:
  * `connection/` (failed ports check, reconnects)
  * `simulation/` (canvas zoom)
  * `elements/` (DOM tree inspect, style adjustments)
  * `console/` (C# script evaluations, history)
  * `sources/` (workspace search outline, files explorer)
  * `network/` (payload queries, headers inspection, clear history)
  * `performance/` (FPS metrics collection)
  * `profiler/` (dotTrace profiles save/load and flame charts)
  * `memory/` (dotMemory allocations analysis)
  * `recorder/` (Test Studio execution, HTML/PDF report generators)

### 3. Reusable Sub-Flow Library
* Introduced a library of common subscripts under `tests/CdpInspectorApp.E2e/shared/` (`connect_to_sample.yaml`, `navigate_to_profiler.yaml`, `navigate_to_memory.yaml`, etc.) invoked via the `runFlow` keyword to eliminate code duplication across the E2E suite.

### 4. Playwright Code Generation CLI (`cdp-cli codegen`)
* Added a new `codegen` command to the inspector CLI runner to compile YAML test flows into standard, executable Playwright spec test files under `tests/playwright/`.
* Allows running generated test scripts headlessly (`npx playwright test tests/playwright/ --headless`) inside local development environments and CI/CD pipelines.

### 5. dotMemory SDK Reflection Load Path & Path Prioritization
* Implemented the dotMemory SDK reflection load path using the JetBrains `JsonWorkspaceIndexSerializer` class when dotMemory/Rider installations are present.
* Resolved a critical assembly resolution ordering bug where .NET Framework versions of assemblies (like `Newtonsoft.Json.dll`) from the ReSharper host folder were resolved before their NetCore equivalents, causing a type load exception (`ReflectionPermission`).
* Added a path prioritization rule to the resolver that favors directories containing "NetCore", successfully resolving dependencies in modern .NET environments.
* Added corresponding unit tests in `ProfilingAnalysisTests.cs` to cover both fallback and SDK reflection-based dmw loading.

---

## Verification & Testing Proof
* All 42 YAML flow files parse successfully and resolve nested sub-flows, verified by the parser unit tests.
* **Unit Tests Status**: **444 tests passed successfully (0 failures)**.
* Verification evidence, commands, and generated report templates are documented in `walkthrough.md`.
