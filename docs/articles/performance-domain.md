---
title: Performance Domain
---

# Performance Domain in CDP Avalonia

The Chrome DevTools Protocol (CDP) **Performance Domain** in the CDP Avalonia project provides APIs to query and stream real-time telemetry from the running application. It measures rendering frame rates (FPS), layout cycle counts and durations, CPU utilization, managed heap allocation rates, and UI thread responsiveness (including dispatcher delay and UI thread blocking duration).

This domain is critical for automated performance testing, verifying that specific views render within budget, detecting UI thread freezes (jank), and monitoring resource consumption over time.

---

## Core Architecture

The backend implementation is located in `PerformanceDomain.cs`. It hooks into the Avalonia UI framework's lifecycle and rendering loop to extract high-fidelity timing information.

```
Avalonia Engine                       Performance Domain
├─ TopLevel Window ─ LayoutUpdated ──────→ SessionPerformanceState
├─ Renderer ──────── SceneInvalidated ───→ SessionPerformanceState
└─ Dispatcher.UIThread ← Post Normal/Render Priority ── SessionPerformanceState
       ↑
       └─ Post Diagnostics ── DispatcherWatchdog
```

### 1. Layout Profiling
In `OnLayoutUpdated`, the performance state hooks into the `LayoutUpdated` event of the active `TopLevel` window:
* It increments `LayoutCount` on each update.
* It starts a high-resolution `Stopwatch` and posts a callback to the `Dispatcher.UIThread` at `DispatcherPriority.Normal`.
* Once executed, it records the elapsed time in seconds as `LayoutDuration`.
* If `TracingDomain` is actively tracing, it generates a manual trace event categorized under `Avalonia.Layout` with the activity name `LayoutPass`.

### 2. Render Loop & FPS Calculation
To track frame updates, the performance state hooks into the protected `SceneInvalidated` event on the window's `Renderer` using reflection in `OnSceneInvalidated`:
* It computes the elapsed time between successive invalidations using `Stopwatch.GetTimestamp()`.
* The instantaneous frames per second is calculated as `1000.0 / elapsedMs` and stored in `Fps`.
* It posts a measuring block to the UI thread at `DispatcherPriority.Render` to track how long rendering tasks sit or execute on the UI queue, storing this in `LastFrameDurationMs` (reported as `FrameDuration` in seconds).
* Similar to layouts, if tracing is active, it generates a trace event under `Avalonia.Rendering` named `RenderFrame`.

### 3. CPU and UI Thread Diagnostics (Watchdog)
To measure responsiveness independently of active layout/render triggers, the domain spawns a background `DispatcherWatchdog`:
* **Queue Delay**: The watchdog schedules a dummy action to post to the `Dispatcher.UIThread` at `DispatcherPriority.Normal` and tracks the elapsed time between scheduling and execution. This indicates the queue latency in seconds (`DispatcherQueueDelay`).
* **Blocking Time**: The watchdog waits on a semaphore for the UI thread callback with a 100ms timeout. If the UI thread is busy and fails to respond within 100ms, the watchdog measures how much longer the UI thread takes to eventually execute the callback. The delta is reported as the UI thread blocking time (`UIThreadBlockingTime`).

---

## CDP Command Reference

The `Performance` domain handles the following CDP actions.

### 1. `Performance.enable`
Enables telemetry collection. It hooks layout/rendering events on the active window and starts a background thread loop that pushes a `Performance.metrics` event to the client once every second.

#### Request Example:
```json
{
  "id": 301,
  "method": "Performance.enable"
}
```

#### Response Example:
```json
{
  "id": 301,
  "result": {}
}
```

---

### 2. `Performance.disable`
Disables telemetry collection. It cancels the background push loop and detaches all event listeners from the window layout engine and renderer.

#### Request Example:
```json
{
  "id": 302,
  "method": "Performance.disable"
}
```

#### Response Example:
```json
{
  "id": 302,
  "result": {}
}
```

---

### 3. `Performance.getMetrics`
Retrieves the current values of all collected performance counters immediately, without waiting for the next periodic event push.

#### Request Example:
```json
{
  "id": 303,
  "method": "Performance.getMetrics"
}
```

#### Response Example:
```json
{
  "id": 303,
  "result": {
    "metrics": [
      { "name": "Timestamp", "value": 1782976822.451 },
      { "name": "Nodes", "value": 142 },
      { "name": "JSHeapUsedSize", "value": 45875200 },
      { "name": "JSHeapTotalSize", "value": 52428800 },
      { "name": "CPUUsage", "value": 4.5 },
      { "name": "LayoutCount", "value": 45 },
      { "name": "LayoutDuration", "value": 0.0035 },
      { "name": "FPS", "value": 60.0 },
      { "name": "FrameDuration", "value": 0.0083 },
      { "name": "DispatcherQueueDelay", "value": 0.0005 },
      { "name": "UIThreadBlockingTime", "value": 0.0 },
      { "name": "MemoryAllocations", "value": 1.25 }
    ]
  }
}
```

---

### 4. `Performance.setTimeDomain`
This command is implemented as a stub for compatibility with Chrome DevTools Protocol clients. It performs no operation and returns an empty result object immediately.

#### Request Example:
```json
{
  "id": 304,
  "method": "Performance.setTimeDomain",
  "params": {
    "timeDomain": "timeTicks"
  }
}
```

#### Response Example:
```json
{
  "id": 304,
  "result": {}
}
```

---

## CDP Event Reference

### 1. `Performance.metrics`
When the domain is enabled, the session state starts a background push loop in `StartPushLoop` that emits this event to the client every 1000 milliseconds.

#### Event Payload Example:
```json
{
  "method": "Performance.metrics",
  "params": {
    "title": "metrics",
    "metrics": [
      { "name": "Timestamp", "value": 1782976823.451 },
      { "name": "Nodes", "value": 142 },
      { "name": "JSHeapUsedSize", "value": 46137344 },
      { "name": "JSHeapTotalSize", "value": 52428800 },
      { "name": "CPUUsage", "value": 2.1 },
      { "name": "LayoutCount", "value": 45 },
      { "name": "LayoutDuration", "value": 0.0 },
      { "name": "FPS", "value": 60.0 },
      { "name": "FrameDuration", "value": 0.0 },
      { "name": "DispatcherQueueDelay", "value": 0.0002 },
      { "name": "UIThreadBlockingTime", "value": 0.0 },
      { "name": "MemoryAllocations", "value": 0.25 }
    ]
  }
}
```

---

## Metrics Reference

The following table describes the performance metrics collected by the Avalonia CDP Server:

| Metric Name | Return Type | Unit | Description |
| :--- | :--- | :--- | :--- |
| **Timestamp** | `double` | Seconds | The current UNIX timestamp representing when the metrics snapshot was captured. |
| **Nodes** | `int` | Count | The total count of active visual elements in the current `TopLevel` visual tree, obtained recursively using `CountVisuals`. |
| **JSHeapUsedSize** | `double` | Bytes | Mapped to the application's physical working set memory size (`Process.WorkingSet64`). |
| **JSHeapTotalSize** | `double` | Bytes | Mapped to the total managed memory allocated by the .NET Garbage Collector (`GC.GetTotalMemory(false)`). |
| **CPUUsage** | `double` | Percentage | Calculated over the last measurement interval across all logical processor cores (normalized by `Environment.ProcessorCount`), capped between `0.0` and `100.0`. |
| **LayoutCount** | `int` | Count | The cumulative number of layout passes executed by Avalonia since telemetry start. |
| **LayoutDuration** | `double` | Seconds | The time taken to execute the most recent layout pass on the UI thread. |
| **FPS** | `double` | Frames/Sec | Instantaneous rendering frame rate computed from successive invalidation signals on the scene renderer. |
| **FrameDuration** | `double` | Seconds | The execution duration of the rendering callback posted on the UI thread at render priority. |
| **DispatcherQueueDelay** | `double` | Seconds | Latency between scheduling an action on the UI dispatcher queue and when it begins executing. |
| **UIThreadBlockingTime** | `double` | Seconds | The duration that the UI Thread blocked task execution beyond the 100ms watchdog threshold. |
| **MemoryAllocations** | `double` | Megabytes (MB) | The volume of managed memory allocated since the last metrics query was performed. |

---

## Guide: Diagnostic Profiling Workflow

By connecting a CDP client to the Avalonia target's performance endpoint, you can automate regression tests for render budget or detect UI thread bottlenecks:

**Diagnostic Profiling Sequence**

Participants: **Automation Agent**, **Avalonia App**

1. Agent → App: `Performance.enable`
2. App → Agent: `{}`
3. App begins pushing `Performance.metrics` events every 1 second
4. **Monitor Loop:**
   - App → Agent: `Performance.metrics` (Telemetry payload)
   - Agent parses metrics for FPS and Blocking Time
   - **If** FPS < 30 or UIThreadBlockingTime > 0.05: Agent logs warning (Frame drop / UI Thread stutter)
5. Agent → App: `Performance.disable`
6. App → Agent: `{}`

### Script Example: Asserting Frame Rate Integrity

An automation script can hook onto the incoming `Performance.metrics` event streams and raise assertions:

1. **Enable performance profiling**: Send `Performance.enable`.
2. **Execute transition**: Trigger an animation or open a heavy view (e.g. click a button).
3. **Assert rendering budgets**:
   * Confirm that `FPS` stays above `50.0`.
   * Assert `UIThreadBlockingTime` remains at `0.0`.
   * Verify that `DispatcherQueueDelay` remains under `0.01` seconds (10ms).
4. **Disable performance profiling**: Send `Performance.disable`.

---

## Implementation References

* **Domain Router**: `PerformanceDomain.cs`
* **Metrics Snapshot Generation**: `GetMetricsAsync`
* **Rendering Event Handlers**: `OnSceneInvalidated`
* **Layout Profiling Hook**: `OnLayoutUpdated`
* **Thread Watchdog Thread Loop**: `WatchdogLoopAsync`
