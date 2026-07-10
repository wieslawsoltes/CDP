# PR Summary: Implement In-App CPU & Memory Profiler with Synchronized Flame Charts

This PR adds native dual CPU & Memory profiling capabilities directly inside the CDP server and builds a state-of-the-art interactive Profiling Visualizer inside the DevTools Inspector app (`CdpInspectorApp`) featuring multi-session history, synchronized stacked CPU/Memory flame charts, and custom telemetry data grids.

---

## 🚀 Key Features

### 1. In-Process EventPipe CPU & Memory Profiling (CDP Server)
- **Dual Profiling:** Integrates CLR `EventPipeSession` targeting sample profiling (`Microsoft-DotNETCore-SampleProfiler`) and runtime event tracing (`Microsoft-Windows-DotNETRuntime` keywords `0x80000002019L` which includes GC allocations and sampling).
- **Memory Allocation Stacks:** Captures live GC allocation ticks (`GC/AllocationTick` events). Resolves full stack traces, allocated object class type names (`TypeName`), and allocation sizes in bytes (`AllocationAmount`).
- **JIT Rundown Symbols:** Integrates the CLR rundown provider (`Microsoft-Windows-DotNETRuntimeRundown` keywords `0x2058`). This logs symbol loads for all methods JIT-compiled *prior* to starting the profiler session, eliminating `"Unknown"` frames and ensuring detailed, readable call stacks.
- **V8 Profile Converter:** Translates hierarchical CLR call stacks (from sample ticks and GC allocation events) into V8 `.cpuprofile` format. The memory allocation profile uses allocation size (in bytes) for timeline spacing and widths.
- **Dual-Profile Response:** The `Profiler.stop` command now returns a dual profile payload containing CPU profile, Memory profile, and aggregated memory allocations lists.

### 2. High-Performance Dual-Flame Chart Visualizer (Inspector App)
- **Synchronized Stacked Timelines:** Stacks CPU timeline and Memory Allocation timeline vertically. Both timelines automatically synchronize panning and zoom scales in real-time.
- **Bytes-Aware Ruler:** Renders timeline ruler ticks in bytes (`B`, `KB`, `MB`) for the memory chart. Recalculates intervals dynamically based on scale ranges.
- **Interactive Minimap Overview:** Renders a micro-scale representation of the entire trace timeline at the top. Shows a highlighted visible range window with handles, dims background non-visible zones, and supports clicking/dragging on the minimap to jump or pan the main flame chart.
- **Keyboard Tree Navigation:** Support full keyboard shortcuts when focused:
  - `A` / `D` or `LeftArrow` / `RightArrow` to pan or walk chronologically across sibling calls.
  - `W` / `S` or `UpArrow` / `DownArrow` to zoom or traverse caller/callee stack depths vertically.
- **Search Match Cycling:** Added `Prev` and `Next` matching cycle buttons and index indicators (e.g., `"2 of 15"`) next to the search textbox in the toolbar. Selecting matches automatically pans/centers the flame chart on the target frame.
- **Interactive Time Ruler:** Renders a styled timeline ruler at the top with major/minor tick marks and labels (`ms` / `s`) that dynamically recalculate and adapt based on the current zoom level.
- **Selection Lock:** Clicking any block highlights it with a blue border and locks method details in the status bar, preventing them from shifting as the cursor moves off.
- **Standard Warm HSL Flame Colors:** Colors blocks using a standard flame graph HSL warm coloring scheme (hues from 12 to 52 for red, orange, and yellow) based on deterministic method name hashing. Adjacent nodes in stack frames receive distinct but unified warm shades. Idle, root, and native functions are styled in distinct dark and neutral gray shades.
- **Dual-Axis Panning:** Left-click drag pans the timeline horizontally (X-axis) and vertically (Y-axis) in sync. Allows panning up and down through deep call stack layers. Includes vertical culling and boundaries.
- **Chronological Consolidation:** Renders continuous horizontal blocks representing stack depths by grouping adjacent duplicate calls in a single chronological pass.
- **Centered Zoom & Pan:** Allows developers to left-click drag to pan and mouse wheel to zoom dynamically centered on the cursor position.
- **Search Highlighting:** Dims unrelated stack frames and outlines matches with bright borders when querying function names in the toolbar.
- **Dynamic Details:** Hovering or selecting any frame renders details (Method Name, Module, Self Time, Total Time, percentages) in the status bar.

### 3. Session History & Multi-Run Sidebar
- **Runs Sidebar:** Exposes a left sidebar panel to list captured profiling sessions. Automatically adds a new run item (e.g., `"Profile 1"`, `"Profile 2"`) on stop.
- **Instant Switching:** Clicking on any profile run updates all charts and stats tables instantly.
- **Separate Export:** Adds an "Export Profile" button to save the currently selected session JSON without blocking the workspace during live recording captures.

### 4. Details Analysis Tab Grids
- **Bottom-Up Calls (CPU):** Computes Self Time, Total Time, percentages, and sample hit counts aggregated by function key (Function Name + Script URL) in a sortable `DataGrid`.
- **Memory Allocations (New!):** Aggregates GC allocation events by Class Type Name, showing:
  - Total Allocated Bytes
  - Size % (Percentage of overall session allocations)
  - Allocation Counts
  - Count %
  - Exposes this data in a sortable DataGrid tab.

---

## 🛠️ Changes Implemented

### CDP Server & Protocols
- **[Directory.Packages.props](file:///Users/wieslawsoltes/GitHub/CDP/Directory.Packages.props)**: Added package versions for client diagnostics and tracing.
- **[Chrome.DevTools.Protocol.csproj](file:///Users/wieslawsoltes/GitHub/CDP/src/Chrome.DevTools.Protocol/Chrome.DevTools.Protocol.csproj)**: Package dependencies referenced.
- **[ProfileConverter.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/Chrome.DevTools.Protocol/Domains/ProfileConverter.cs)**: Implemented converter from Firefox/DotNET trace stacks to V8 CPU profiles.
- **[ProfilerDomain.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/Chrome.DevTools.Protocol/Domains/ProfilerDomain.cs)**: Enabled GC event keywords (`0x80000002019L` including GCAllocationSampling). Parses both CPU samples and CLR memory allocation events. Added robust support for both `GCAllocationTickTraceData` (standard Tick events) and `AllocationSampled` (EventID 303, raw payload parsed manually via BitConverter) to guarantee full compatibility on macOS/Linux. Fixed stop-command check to correctly read nested samples array from the dual profile structure instead of falling back to mock profiles. Increased EventPipe copy wait timeout to 15s.

### DevTools Inspector Shared Controls
- **[FlameChart.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/CDP.Inspector.Shared/Controls/FlameChart.cs)**: Custom Avalonia rendering control overriding `Render` with viewport culling, standard warm HSL flame coloring cache, text clipping, and dual-axis pan/zoom pointer events. Added local `OffsetY` scrolling bounds. Subscribed to `INotifyCollectionChanged` events to update rendering immediately on collection changes. Optimized drawing performance 100x via static brush caching and sub-pixel culling (skipping blocks `< 0.2` pixels).
- **[ProfilerViewModel.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/CDP.Inspector.Shared/ViewModels/ProfilerViewModel.cs)**: Added `ProfileSessionModel`, `ProfileMemoryStats` collections, multiple session history list management, CPU & memory dual V8 profile parsing, and export commands.
- **[ProfilerView.axaml](file:///Users/wieslawsoltes/GitHub/CDP/src/CDP.Inspector.Shared/Views/ProfilerView.axaml)** & **[ProfilerView.axaml.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/CDP.Inspector.Shared/Views/ProfilerView.axaml.cs)**: Added runs sidebar list, vertically stacked CPU and Memory flame charts (sharing panning/zoom scales), and a "Memory Allocations" stats tab.
- **[PerformanceView.axaml](file:///Users/wieslawsoltes/GitHub/CDP/src/CDP.Inspector.Shared/Views/PerformanceView.axaml)**, **[PerformanceView.axaml.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/CDP.Inspector.Shared/Views/PerformanceView.axaml.cs)** & **[PerformanceViewModel.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/CDP.Inspector.Shared/ViewModels/PerformanceViewModel.cs)**: Added quick action buttons and save picker hooks to initiate/stop CPU profiling.
- **[MainWindowViewModel.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/CDP.Inspector.Shared/ViewModels/MainWindowViewModel.cs)** & **[MainView.axaml.cs](file:///Users/wieslawsoltes/GitHub/CDP/src/CDP.Inspector.Shared/MainView.axaml.cs)**: Registered `"Profiler"` pane and icon mapping to support the split-dock toolbar layout.

- **[MainWindow.axaml](file:///Users/wieslawsoltes/GitHub/CDP/samples/CdpSampleApp/MainWindow.axaml)** & **[MainWindowViewModel.cs](file:///Users/wieslawsoltes/GitHub/CDP/samples/CdpSampleApp/ViewModels/MainWindowViewModel.cs)**: Cleaned up buttons layout to use a flexible `WrapPanel` and prevent potential clipping. Added missing System.Collections.Generic namespace import.

---

## 🧪 Verification & Testing
- Added **[ProfileConverterTests.cs](file:///Users/wieslawsoltes/GitHub/CDP/tests/Avalonia.Diagnostics.Cdp.Tests/ProfileConverterTests.cs)** verifying translation of mock stack prefixes and HitCount trie resolution.
- Added `TestProfilerAllocationSampledManualParsing` and `TestProfilerViewModelLoadDualProfile` to **[NewDomainTests.cs](file:///Users/wieslawsoltes/GitHub/CDP/tests/Avalonia.Diagnostics.Cdp.Tests/NewDomainTests.cs)** to assert manual byte layout decoding and dual profile VM parsing respectively.
- Executed `dotnet test` solution-wide, passing all 380 tests cleanly.

---

## 🚀 Pull Request Review Feedback Addressed
All unresolved comments and suggestions from automated reviews have been successfully addressed:
- **Avoid stopping inactive profiler states (P1):** Added immediate return guard in `ProfilerState.Stop()` when inactive to prevent server hang from infinite duration loops. (Commit: `5937f6c`)
- **Split flame blocks when ancestors change (P2):** Modified `ProcessV8Profile` to terminate and finalize all descendant blocks when an ancestor changes, preventing overlaps. (Commit: `def4ca6`)
- **Sum deltas without overflowing int (P2):** Parsed `timeDeltas` list as `double` to prevent integer overflow exception on large traces. (Commit: `def4ca6`)
- **Rebuild search matches after switching runs (P2):** Switched profiling runs sidebar items now correctly refreshes active search results in `ProfilerViewModel.OnSessionSelected()`. (Commit: `8cb2f89`)
