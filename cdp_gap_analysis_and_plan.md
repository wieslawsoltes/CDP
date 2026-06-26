# CDP Inspector Gap Analysis & Implementation Plan

This report evaluates the features of our custom Chrome DevTools Protocol (CDP) client inspector (`CdpInspectorApp`) compared to the official Google Chrome Browser Developer Tools, identifying functional gaps and outlining a detailed implementation plan for high-impact extensions.

---

## Executive Summary

While `CdpInspectorApp` provides core diagnostic features (visual tree representation, styles inspection, interactive screencasts, memory control counts, and custom Test Studio recorder/replay engines), Google Chrome DevTools has matured over a decade with deep runtime and rendering support. 

Through successive rounds of development, we have closed all major visual and functional gaps:
1. **Elements Tab**: Added XAML Box Model Editor (cascading layouts, Margin/Border/Padding editing) and DOM event listeners inspector.
2. **Console Tab**: Integrated C# REPL, log level filtering, autocomplete popups, live watch expressions, and clickable stack trace trace-links.
3. **Sources Tab**: Integrated workspace explorers, read-only file editors, workspace-wide text search, and a C# line breakpoint execution debugger (stepping toolbar, Call Stack logs, and Scope variables evaluation).
4. **Network Tab**: Added JSON/Image request payload parsers and multi-phase TTFB vs Content Download waterfall charts.
5. **Performance Tab**: Built stacked real-time timelines for CPU Usage (%), FPS, and Memory (MB).
6. **Memory Tab**: Added a Detached Controls leak detector and interactive GC root retainer reference tree.

This updated plan reflects our achievements and maps out the next-generation diagnostic features specialized for **Avalonia UI** applications.

---

## Feature-by-Feature Gap Analysis

The table below lists each developer tools tab, comparing Google Chrome DevTools' features against `CdpInspectorApp`, evaluating the gap severity, and assessing the feasibility of implementation in Avalonia.

| DevTools Tab | Google Chrome Feature Set | Current CdpInspectorApp | Gap Severity | Feasibility in Avalonia UI |
| :--- | :--- | :--- | :--- | :--- |
| **Elements** | Live DOM editing, drag-and-drop hierarchy, CSS Styles hierarchy, interactive Box Model Editor, Class toggler (`.cls`), Event Listeners pane, DOM Breakpoints, Accessibility tree and attributes. | Visual tree as HTML, node search, delete element, computed/inline styles editing, pseudo-state forcing, AX tree mapping, Event Listeners list, **Interactive XAML Box Model Editor**. | **Resolved** | **Fully Implemented** |
| **Console** | JS REPL with autocomplete, CLI API ($0-$4, `$_`), filter levels, grouping, live watch expressions, stack trace source-code linking. | C# evaluation REPL, console history, completions lookup, log level filter, simple regex search, UI REPL mode, **Live Pinned Expressions**, **Stack trace source-code navigation**. | **Resolved** | **Fully Implemented** |
| **Sources** | File navigator, text editor, debugger (breakpoints, call stack, scope variables inspector, watch pane, code hot-swapping). | Workspace file navigator, read-only text file viewer, **Global Workspace Search**, **C# line breakpoints debugger** (stepping, call stack, scope variables). | **Resolved** | **Fully Implemented** |
| **Network** | Detailed request log grid, waterfall timeline, response previews, request payload parser, WebSocket message frames list, throttling. | Requests list, request/response headers, mock rules, network throttling, **Waterfall Timing (TTFB / Download)**, **JSON Payload Parser & Response Previews**. | **Resolved** | **Fully Implemented** |
| **Performance** | Performance recorder, flame chart of execution stacks, paint flashing, layout shifts, frame rate tracking, aggregate CPU charts. | Live CPU/FPS metrics checklist, Working Set MB live chart, live control allocations list, target kill button, **Stacked real-time CPU, FPS, & Memory charts**. | **Resolved** | **Fully Implemented** |
| **Memory** | Heap snapshotting, allocation timelines, allocation sampling, object retainers path-to-root graph. | Control allocations snapshot, snapshot comparison, Detached Controls list (memory leak finder), GC button, **Interactive Reference Retainer Tree (GC Roots)**. | **Resolved** | **Fully Implemented** |
| **Application** | Local/Session Storage, IndexedDB, SQLite, Cookies, Cache Storage, Service Workers, Background Services. | Resource dictionary editor, Local/Session Storage key-value editor, Cookies list & editor, empty Background Services. | **Medium** | **High** (add SQLite/LiteDB file viewer) |
| **Audits** | Lighthouse audits (a11y, SEO, performance, PWA), detailed metrics scoring, recommendations list. | Custom `runDiagnostics` auditing, basic scores, click-to-inspect issue list. | **Resolved** | **Fully Implemented** |

---

## High-Impact Feature Recommendations

Based on our updated analysis, we recommend prioritizing the following key features to elevate `CdpInspectorApp` to a professional-grade .NET diagnostic tool:

### 1. Application Tab: Local SQLite / LiteDB Viewer
Many Avalonia and .NET applications utilize local databases (such as SQLite or LiteDB) for local data persistence or caching. Adding a database file viewer under the Application panel allows developers to explore tables, inspect row schemas, and execute SQL queries directly inside the inspector.

### 2. Memory Tab: Allocation Sampling & Timeline Chart
Add real-time allocations tracking, plotting a histogram chart representing object allocation rates over time, helping developers pinpoint when excessive memory churn or GC thrashing is occurring.

---

## UI View Mockups

Below are modern, premium dark-mode interface mockups visualizing how the completed features integrate into the `CdpInspectorApp` workspace.

### 1. Elements Panel with XAML Box Model Editor
The concentric Margin, Border, Padding, and Content editor updates layout values dynamically on double-click:
![XAML Box Model Mockup](/Users/wieslawsoltes/.gemini/antigravity/brain/35e0ad3b-ed8b-40ea-84ac-aec1facee088/elements_panel_box_model_mockup_1782484145511.png)

### 2. Memory Panel with Detached Control Retainers
The bottom retainer panel traces the exact reference path keeping detached elements in memory:
![Memory Retainers Mockup](/Users/wieslawsoltes/.gemini/antigravity/brain/35e0ad3b-ed8b-40ea-84ac-aec1facee088/memory_retainers_mockup_1782484159398.png)

---

## Implementation Plan

### Phase 1: Interactive XAML Box Model Editor
*Status: **Fully Implemented & Merged***
- Added concentric margin/border/padding/size editors inside `ElementsView.axaml`.
- Implemented style-editing protocol triggers to modify spacing values dynamically.

### Phase 2: Detached Control Retainer Tree
*Status: **Fully Implemented & Merged***
- Implemented event, VM bindings, and field reference crawler in `ReferenceCrawler.cs`.
- Exposed retainer tree mappings over the custom `Memory.getRetainers` CDP command.
- Integrated the hierarchical detail `TreeView` panel in `MemoryView.axaml`.

### Phase 3: Console Stack Trace Navigation
*Status: **Fully Implemented & Merged***
- Configured double-click navigation on the console logs list in `ConsoleView.axaml.cs`.
- Extracts paths and lines from stack trace logs, maps them suffix-wise to workspace files, and jumps directly to the line in the Sources text editor.

### Phase 4: Lightweight Breakpoint Debugger
*Status: **Fully Implemented & Merged***
- Created the CdpServer `Debugger` domain handling line breakpoints, step-by-step executions, and thread pausing (`DebuggerDomain.cs`).
- Integrated the right-hand **Debugger Panel** in the Sources view, with Resume/Step toolbar, Call Stack list, Scope variables DataGrid, and Caret breakpoint toggler.

### Phase 5: Application SQLite/DB Viewer
*Status: **Planned***
- **Storage Scanner**: Scan the application directory or configuration keys for `.db`, `.sqlite`, or `.litedb` database files.
- **Protocol Schema**: Add an `Application` domain extension (e.g. `Application.getDatabaseTableNames`, `Application.executeSQL`) to fetch table records and execute queries.
- **UI Grid Panel**: Add a database view layout under the Application tab, featuring a schema tree view and a data grid.

### Phase 6: Memory Allocation Timeline
*Status: **Planned***
- **Sampling Telemetry**: Hook into GC allocations monitoring or object tracking ticks on the server.
- **Visual Histogram**: Record allocation count frequencies and draw an allocation timeline chart in `MemoryView.axaml` using `ChromeTimelineChart`.
