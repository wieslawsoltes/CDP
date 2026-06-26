# CDP Inspector Gap Analysis & Implementation Plan

This report evaluates the features of our custom Chrome DevTools Protocol (CDP) client inspector (`CdpInspectorApp`) compared to the official Google Chrome Browser Developer Tools, identifying functional gaps and outlining a detailed implementation plan for high-impact extensions.

---

## Executive Summary

While `CdpInspectorApp` provides core diagnostic features (visual tree representation, styles inspection, interactive screencasts, memory control counts, and custom Test Studio recorder/replay engines), Google Chrome DevTools has matured over a decade with deep runtime and rendering support. 

Through successive rounds of development, we have closed several critical visual and functional gaps (including the Interactive Box Model Editor, Reference Retainer Tree, Global Workspace Search, and real-time stacked Performance and Network waterfall timelines). This updated plan reflects our achievements and maps out the next-generation diagnostic features specialized for **Avalonia UI** applications.

---

## Feature-by-Feature Gap Analysis

The table below lists each developer tools tab, comparing Google Chrome DevTools' features against `CdpInspectorApp`, evaluating the gap severity, and assessing the feasibility of implementation in Avalonia.

| DevTools Tab | Google Chrome Feature Set | Current CdpInspectorApp | Gap Severity | Feasibility in Avalonia UI |
| :--- | :--- | :--- | :--- | :--- |
| **Elements** | Live DOM editing, drag-and-drop hierarchy, CSS Styles hierarchy, interactive Box Model Editor, Class toggler (`.cls`), Event Listeners pane, DOM Breakpoints, Accessibility tree and attributes. | Visual tree as HTML, node search, delete element, computed/inline styles editing, pseudo-state forcing, AX tree mapping, Event Listeners list, **Interactive XAML Box Model Editor**. | **Resolved** | **Fully Implemented** |
| **Console** | JS REPL with autocomplete, CLI API ($0-$4, `$_`), filter levels, grouping, live watch expressions, stack trace source-code linking. | C# evaluation REPL, console history, completions lookup, log level filter, simple regex search, UI REPL mode, **Live Pinned Expressions**. | **Low** | **High** (add stack trace source-code linking) |
| **Sources** | File navigator, text editor, debugger (breakpoints, call stack, scope variables inspector, watch pane, code hot-swapping). | Workspace file navigator, read-only text file viewer, **Global Workspace Search**. | **Medium** | **High** (add lightweight execution debugger) |
| **Network** | Detailed request log grid, waterfall timeline, response previews, request payload parser, WebSocket message frames list, throttling. | Requests list, request/response headers, mock rules, network throttling, **Waterfall Timing (TTFB / Download)**, **JSON Payload Parser & Response Previews**. | **Resolved** | **Fully Implemented** |
| **Performance** | Performance recorder, flame chart of execution stacks, paint flashing, layout shifts, frame rate tracking, aggregate CPU charts. | Live CPU/FPS metrics checklist, Working Set MB live chart, live control allocations list, target kill button, **Stacked real-time CPU, FPS, & Memory charts**. | **Resolved** | **Fully Implemented** |
| **Memory** | Heap snapshotting, allocation timelines, allocation sampling, object retainers path-to-root graph. | Control allocations snapshot, snapshot comparison, Detached Controls list (memory leak finder), GC button, **Interactive Reference Retainer Tree (GC Roots)**. | **Resolved** | **Fully Implemented** |
| **Application** | Local/Session Storage, IndexedDB, SQLite, Cookies, Cache Storage, Service Workers, Background Services. | Resource dictionary editor, Local/Session Storage key-value editor, Cookies list & editor, empty Background Services. | **Low** | **High** (add SQLite/DB viewer) |
| **Audits** | Lighthouse audits (a11y, SEO, performance, PWA), detailed metrics scoring, recommendations list. | Custom `runDiagnostics` auditing, basic scores, click-to-inspect issue list. | **Low** | **High** (extend metrics and categories) |

---

## High-Impact Feature Recommendations

Based on our updated analysis, we recommend prioritizing the following key features that will elevate `CdpInspectorApp` to a professional-grade .NET diagnostic tool:

### 1. Console Tab: Stack Trace Source-Code Linking
Console logs and exception messages often output call stacks (e.g., `at Namespace.Class.Method() in file.cs:line 123`). We can parse these text representations in log entries, highlight them as clickable links, and automatically navigate the Sources tab editor directly to that file and line.

### 2. Sources Tab: Lightweight C# Breakpoint Debugger
Integrate breakpoint handling into the CDP client and CdpServer to enable stepping through execution flows, stopping at line breakpoints in `.cs` and `.axaml` files, and inspecting the local execution stack frames and scope variables.

### 3. Memory Tab: Allocation Sampling & Timeline
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
*Status: **Planned***
- **Parsing Engine**: Add helper parser in `ConsoleViewModel.cs` that extracts file paths and line numbers from log text patterns.
- **Inter-Tab Navigation**: Fire navigation commands targeting the `SourcesViewModel` when stack trace links are clicked.
- **Visual Highlight**: Select the target file node in the workspace explorer, load its content, scroll, and highlight the line of interest.

### Phase 4: Lightweight Breakpoint Debugger
*Status: **Planned***
- **Server Debugger Hook**: Add `Debugger` domain handling (registering breakpoints, parsing C# expressions, and halting execution threads).
- **Stepping Logic**: Support pause, resume, step-over, step-into, and step-out capabilities.
- **Scope Mappings**: Walk call stack frame metadata to populate local parameters and variables as JSON-serializable remote objects.
