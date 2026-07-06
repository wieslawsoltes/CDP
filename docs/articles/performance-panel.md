---
title: Performance Panel
description: Technical guide to performance dashboards, timeline charts, UI thread blocking latency, garbage collection triggers, and live control allocations.
---

# Performance Panel

The **Performance Panel** provides real-time telemetry of the resources and rendering metrics of the connected Avalonia application. It enables developers to monitor rendering framerates, diagnose UI thread blocking, trigger garbage collections, and identify layout bottlenecks.

---

## 1. Diagnostics Info Dashboard

The left side of the Performance Panel displays the **Diagnostics Info** dashboard, which provides a detailed breakdown of runtime performance metrics:

### Structural Metrics
- **Visual Nodes (`lblPerfNodes`)**: The total count of active elements in the visual tree. High node counts can cause performance degradation during layout passes and rendering.
- **Target Windows (`lblPerfDocuments`)**: The number of open top-level windows and dialog documents in the application.

### Memory & System Footprint
- **Working Set Size (`lblPerfMemory`)**: The physical RAM allocated to the application process by the operating system.
- **GC Total Memory (`lblPerfGc`)**: The total memory allocated on the managed .NET garbage collection heap.
- **Process PID & OS (`lblPerfPid`, `lblPerfOs`)**: The system process identifier and detailed operating system description (e.g., Mac OS, Windows, or Linux distribution).

### Execution & Rendering Telemetry
- **CPU Usage (`lblPerfCpu`)**: The percentage of processor capacity consumed by the application.
- **Frame Rate (FPS) (`lblPerfFps`)**: The current rendering speed of the UI. Tighter animations require maintaining a stable 60 FPS.
- **Frame Duration (`lblPerfFrameDuration`)**: The time taken to process a single frame. Standard 60 FPS targets require frame completion within `16.6` milliseconds.
- **Layout Passes (`lblPerfLayoutCount`)**: A counter tracking how many visual measure and arrange layout passes have been executed.
- **Layout Duration (`lblPerfLayoutDuration`)**: The time spent calculating layout sizes. Slow layout loops often point to inefficient container nesting.
- **Dispatcher Delay (`lblPerfQueueDelay`)**: The queue delay of the Avalonia dispatcher thread, indicating dispatcher loop starvation.
- **UI Thread Blocking (`lblPerfBlockingTime`)**: Measures the duration of blocking calls on the UI thread, which can cause lag or freezing.

---

## 2. Performance Monitors and Chrono-Charts

The right side of the panel displays a stack of timeline charts using the custom `TimelineChart` control. These charts graph performance history over time:

```
+---------------------------------------------------------+
| CPU Timeline Chart (%)                                  |
|   100% |  _/\_                                          |
|     0% |________/\_____________________________________  |
+---------------------------------------------------------+
| FPS Timeline Chart (FPS)                                |
|     60 |______________________________________/\_______ |
|      0 |  \____/                                        |
+---------------------------------------------------------+
| Memory Timeline Chart (MB)                              |
| 500 MB |       _/\____                                  |
|   0 MB |______/       \________________________________ |
+---------------------------------------------------------+
```

- **CPU History Chart**: Renders CPU consumption (0% to 100%) in orange, making it easy to correlate user activity with CPU spikes.
- **FPS History Chart**: Renders UI frame rates in green, highlighting frame drops during heavy rendering operations.
- **Memory History Chart**: Renders managed heap memory footprint in blue, tracking memory accumulation over time.

---

## 3. Memory and Application Lifecycle Controls

At the top-left of the panel, quick actions allow developers to manipulate runtime behaviors:
- **Refresh Metrics**: Clicking `btnRefreshMetrics` forces an immediate query of system metrics, updating the dashboard counters.
- **Collect Garbage**: Clicking `btnCollectGarbage` calls `GC.Collect()` on the target application process. Correlating this action with the memory timeline chart helps identify whether memory can be reclaimed or if references are leaking.
- **Kill Target App**: The `btnCloseTarget` button terminates the target process immediately (`CloseTargetCommand`). This is useful for stopping stuck application threads or resetting test runs.

---

## 4. Live Control Type Counts in Memory (`lstLiveControls`)

The bottom section of the panel displays the **Live Control Type Counts in Memory** table:
- **Control Type**: The C# class type name of the control (e.g., `Button`, `TextBlock`, `Border`, `Grid`).
- **Live Instances Count**: The number of instances of this control type currently allocated in memory.
- **Use Case**: This table is crucial for identifying layout bloat. If navigating back and forth between pages causes the control count of a page's elements to grow indefinitely, it indicates a memory leak.

---

## 5. CDP Telemetry Protocol Mappings

The Performance Panel queries these metrics by enabling the `Performance` domain and listening to periodic updates:

### Enabling Telemetry
```json
{
  "id": 1,
  "method": "Performance.enable"
}
```

### Retrieving Performance Metrics
```json
{
  "id": 2,
  "method": "Performance.getMetrics"
}
```

### Response Payload
```json
{
  "id": 2,
  "result": {
    "metrics": [
      { "name": "VisualNodes", "value": 348 },
      { "name": "TargetWindows", "value": 1 },
      { "name": "WorkingSetSize", "value": 142857142 },
      { "name": "GcTotalMemory", "value": 45123456 },
      { "name": "CpuUsage", "value": 4.5 },
      { "name": "Fps", "value": 60.0 },
      { "name": "FrameDuration", "value": 2.4 },
      { "name": "LayoutPasses", "value": 240 },
      { "name": "LayoutDuration", "value": 0.8 },
      { "name": "DispatcherDelay", "value": 0.1 },
      { "name": "UIThreadBlockingTime", "value": 0.0 }
    ]
  }
}
```

This telemetry collection runs in the background. It updates the inspector's charts and logs without interrupting user interactions, ensuring accurate performance profiling.
