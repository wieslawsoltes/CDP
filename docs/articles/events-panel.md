---
title: Events Panel
description: Detailed documentation on the Events Panel in CDP Inspector, covering live protocol event monitoring, telemetry collection, performance profiling, and event flow timing diagrams.
---

# Events Panel

The **Events Panel** in the CDP Inspector provides developers and test engineers with a live traffic monitor for Chrome DevTools Protocol (CDP) interactions. It serves as an active protocol debugger, letting users observe real-time events, JSON-RPC payloads, and network operations passing between the inspector and the target application. Furthermore, the panel houses telemetry profiling features that capture CPU and memory usage statistics over time to render interactive performance timelines.

This article details the event monitoring framework, telemetry logs, data structures, and the communication lifecycles that govern the Events Panel.

---

## 1. Overview of the Events Panel Workspace

The event logging and telemetry system is split across the following key files:
*   **View Layer**: `EventsView.axaml` structures the live event log layout, detailing filter toggle bars and JSON inspectors.
*   **ViewModel**: `EventsViewModel.cs` processes incoming websocket events, manages logs filtering, handles text searches, and caps event history bounds to prevent memory bloat.
*   **Performance Telemetry View**: `StepTelemetryView.axaml` and `StepTelemetryView.axaml.cs` render performance overlay graphs.
*   **Telemetry Charting Service**: `TelemetryChartService.cs` utilizes SkiaSharp graphics rendering to draw vector-based CPU and memory usage charts dynamically.

---

## 2. Live CDP Event Monitoring

When the inspector connects to a target, a WebSocket connection is initialized. Every protocol message sent from the target (known as a CDP Event Notification) is captured by the client-side `ICdpService` event broker.

### Capture Mechanism
The `EventsViewModel` registers an event handler on the active CDP service:

```csharp
_cdpService.EventReceived += CdpService_EventReceived;
```

When a message is received:
1.  **Ignored Events Check**: It checks if `IgnoreScreencast` is active and the method name is `Page.screencastFrame`. Since screencasting pushes compressed PNG/JPEG tiles up to 60 times a second, logging them would quickly fill the event grid and degrade client performance.
2.  **Entry Packaging**: Incoming frames are parsed into `CdpEventEntry` objects containing a timestamp, the method name, the target selector (if present in the event params), and the formatted JSON parameters.
3.  **Buffer Capping**: To prevent memory leaks during long test runs, the log history is capped at 500 events:
    ```csharp
    _allEvents.Add(entry);
    if (_allEvents.Count > 500)
    {
        var removed = _allEvents[0];
        _allEvents.RemoveAt(0);
        _filteredEvents.Remove(removed);
    }
    ```

### Protocol Filtering
The filter toggle bar categorizes events by their protocol domain:
*   **DOM**: Filter events related to document queries, tree updates, and focus highlights (e.g., `DOM.documentUpdated`, `DOM.setChildNodes`).
*   **Page**: Filter window state events, navigation records, and screencast frames.
*   **Input**: Monitor simulated touch, key strokes, and mouse coordinates.
*   **Runtime**: Track REPL valuations, execution context definitions, and object reference compilations.
*   **Console/Log**: Watch target application runtime warnings and standard debug console outputs.
*   **Network**: Inspect network requests, resource loading metrics, and mock responses.

The filter selection dynamically re-evaluates the collection:

```csharp
bool passDomain = domain switch
{
    "DOM" or "DOMDEBUGGER" => FilterDom,
    "PAGE" => FilterPage,
    "INPUT" => FilterInput,
    "RUNTIME" => FilterRuntime,
    "CONSOLE" or "LOG" => FilterConsoleLog,
    "NETWORK" => FilterNetwork,
    _ => FilterOther
};
```

---

## 3. Telemetry Logs & Performance Metrics

Beyond live event listing, the Events architecture supports active profiling. When executing tests or replaying recorded steps within the Test Studio, the inspector captures performance metrics at high frequencies.

### RunMetricSample Data Structure
The profiling engine gathers resource consumption metrics represented by the `RunMetricSample` model. Each sample captures:
*   `RelativeTimeMs`: The time offset (in milliseconds) from the start of the replay sequence.
*   `MemoryJsHeapUsed`: Memory allocation (in Megabytes) consumed by the application.
*   `CpuUsage`: Processor utilization percentage (from `0.0%` to `100.0%`).

### Telemetry Graph Rendering
The `TelemetryChartService.cs` processes the recorded metric samples and uses **SkiaSharp** to draw a custom `Bitmap` that is loaded into the user interface:

1.  **Plot Area Calculations**: Clears the canvas with a dark theme color (`SKColor(15, 16, 21)`). It calculates drawing padding (left, right, top, bottom margins) to yield the chart bounds.
2.  **Gridlines Drawing**: Renders horizontal dashed divider lines at 25%, 50%, and 75% of maximum scale.
3.  **Step Range Highlight**: Draws a semitransparent blue rectangle background (`SKColor(37, 99, 235, 38)`) to highlight the duration of the currently selected replay step.
4.  **Path Tracing**: Iterates over all samples, plotting points based on time (X axis) and resource values (Y axis). It draws lines connecting the coordinates:
    *   **Memory Path**: Drawn using a colored stroke line showing allocation spikes.
    *   **CPU Path**: Drawn using a contrasting stroke line showing CPU computation peaks.
5.  **Output Conversion**: Encodes the canvas drawing as a PNG stream and converts it into a native `Avalonia.Media.Imaging.Bitmap` for binding.

---

## 4. Event Capturing & Dispatch Flow Diagram

The lifecycle of how CDP events originate, pass over the WebSocket, get handled by the inspector layers, and propagate to the view is outlined in the sequence diagram below:

```text
+-----------------------+              +----------------------+             +----------------------+            +-----------------------+
|  Target Avalonia App  |              |    CdpService (Client)   |             |   EventsViewModel    |            |   EventsView (UI)     |
+-----------------------+              +----------------------+             +----------------------+            +-----------------------+
            |                                      |                                    |                                    |
            |-- [1] Visual Event Occurs ----------->|                                    |                                    |
            |   (e.g., Click, Node Focus)          |                                    |                                    |
            |                                      |                                    |                                    |
            |-- [2] Sends JSON-RPC Event --------->|                                    |                                    |
            |   Method: "DOM.childNodeCountUpdated" |                                    |                                    |
            |   Params: { nodeId: 12, count: 4 }   |                                    |                                    |
            |                                      |-- [3] EventReceived Fired -------->|                                    |
            |                                      |                                    |                                    |
            |                                      |                                    |-- [4] MatchesFilter() Evaluated -->|
            |                                      |                                    |   - IgnoreScreencast? (Yes/No)     |
            |                                      |                                    |   - Domain Enabled? (Yes)          |
            |                                      |                                    |   - Search Query Match? (Yes)      |
            |                                      |                                    |                                    |
            |                                      |                                    |-- [5] Post to UI Thread ---------->|
            |                                      |                                    |   Add to FilteredEvents Collection |
            |                                      |                                    |                                    |
            |                                      |                                    |                                    |-- [6] DataGrid updates
            |                                      |                                    |                                    |   renders row
            |                                      |                                    |                                    |
```

### Telemetry Performance Profile Flow
The sequence diagram below displays the timing flow of Test Studio telemetry capture:

```text
+-----------------------+              +----------------------+             +----------------------+            +-----------------------+
|  Target Avalonia App  |              |    TestStudio Engine |             | TelemetryChartService|            | StepTelemetryView (UI)|
+-----------------------+              +----------------------+             +----------------------+            +-----------------------+
            |                                      |                                    |                                    |
            |                                      |-- [1] Begin Step Playback -------->|                                    |
            |                                      |                                    |                                    |
            |-- [2] Query Performance Metrics ---->|                                    |                                    |
            |   Method: "Performance.getMetrics"   |                                    |                                    |
            |                                      |                                    |                                    |
            |<-- [3] Returns CPU/Mem Data ---------|                                    |                                    |
            |                                      |                                    |                                    |
            |                                      |-- [4] Append RunMetricSample ----->|                                    |
            |                                      |                                    |                                    |
            |                                      |-- [5] Step Replay Finished -------->|                                    |
            |                                      |                                    |                                    |
            |                                      |-- [6] RenderPerformanceChart() --->|                                    |
            |                                      |   Passes samples and time offsets  |                                    |
            |                                      |                                    |-- [7] Draw grid & paths ----------->|
            |                                      |                                    |   Generates SKBitmap               |
            |                                      |                                    |                                    |
            |                                      |<-- [8] Returns Bitmap -------------|                                    |
            |                                      |                                    |                                    |
            |                                      |-- [9] Update Image Binding --------------------------------------------->|
            |                                      |                                                                          | Renders chart overlay
```
---

## 5. Advanced Monitoring Control Functions

To assist with manual telemetry diagnostics, the Events Panel includes additional control utilities:
*   **Pause Stream Toggle**: Freezes incoming events parsing, letting developers inspect static payloads without list items shifting as new commands are received.
*   **Clear Logs**: Flushes the `_allEvents` list and the `FilteredEvents` collection, resetting the memory state.
*   **Search Box**: Performs a case-insensitive search across both the event method string and the formatted JSON parameters list.
*   **Formatted Payload Inspector**: Clicking any row parses and expands the backing JSON parameters structure, displaying nested arrays and objects inside a read-only scrolling editor panel.

---

## Related Topics

- [DOM Debugger Domain](/articles/dom-debugger-domain) — Event listener introspection via CDP
- [Debugger Domain](/articles/debugger-domain) — Breakpoint management and pause/resume
- [Elements Panel](/articles/elements-panel) — Visual tree inspection and node selection

