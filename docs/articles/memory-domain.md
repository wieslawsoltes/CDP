---
title: Memory Domain
---

# Memory Domain in CDP Avalonia

The Chrome DevTools Protocol (CDP) **Memory Domain** in `Avalonia.Diagnostics.Cdp` provides powerful tools for inspecting the application's managed memory footprint, tracking control allocations, collecting garbage, and diagnosing memory leaks. 

In desktop application development (and specifically with Avalonia UI), memory leaks commonly manifest as "detached controls"—UI elements that are removed from the active visual tree but cannot be garbage-collected because they are still held in memory by active event handlers, static collections, styles, or references in the inspection runtime itself.

This article details how the Memory Domain functions under the hood, explains its APIs, and shows how you can use them to build automated memory profiling and leak detection scripts.

---

## Core Architecture & Engine

The Memory Domain is defined inside `MemoryDomain.cs`. It uses a combination of Avalonia runtime class handlers, weak reference tracking, reflection crawling, and V8-compatible heap snapshot building to expose CLR memory details over CDP.

### 1. Control Tracking (`ControlTracker`)
To monitor UI controls as they transition between active and detached states, the Memory Domain implements a static `ControlTracker` registry:
* **Registration**: In `Initialize`, the tracker registers a class handler on `Control.LoadedEvent` to capture all UI controls when they are first loaded.
* **Weak References**: Tracked controls are held inside `List<WeakReference<Visual>>` to prevent the diagnostic engine itself from keeping controls alive.
* **Attachment Verification**: The tracker uses `IsAttachedToVisualTree()` to determine if a control has been detached from the active visual tree.
* **Detached Duration**: When a control is detected as detached, the tracker records the detach timestamp using a `ConditionalWeakTable<Visual, DetachInfo>` so it can compute how long the control has remained detached.

### 2. Retainer Tree Crawling (`ReferenceCrawler`)
When a control is suspected of leaking, the `ReferenceCrawler` crawls the .NET object graph in a Breadth-First Search (BFS) manner:
* **GC Roots**: It queries active top-level windows (`CdpServer.GetWindows()`) and parses all static fields in non-system/non-Microsoft assemblies within the current `AppDomain`.
* **Child Extraction**: It retrieves potential child references by inspecting fields, properties, delegate invocation targets (for event handlers), and arrays.
* **Path Reconstruction**: Once it reaches the target control, it walks backward through the discovered incoming edges to reconstruct a hierarchical retainer path up to the GC root.

### 3. V8-Compatible Heap Snapshots
The Memory Domain implements `takeHeapSnapshot` by translating the Avalonia visual and data context trees into V8 heap node and edge arrays. This allows external tools (like Chrome DevTools or Edge DevTools) to parse and visualize the visual tree as a node heap graph.

---

## CDP Command Reference

The Memory Domain handles the following CDP actions.

### 1. `Memory.getLiveControls`
Computes the current count of active and loaded controls grouped by their C# type name. It traverses the visual trees of all active windows and builds a frequency dictionary.

#### Request Example:
```json
{
  "id": 201,
  "method": "Memory.getLiveControls"
}
```

#### Response Example:
```json
{
  "id": 201,
  "result": {
    "controls": [
      {
        "type": "MainWindow",
        "count": 1
      },
      {
        "type": "Button",
        "count": 8
      },
      {
        "type": "TextBlock",
        "count": 24
      }
    ]
  }
}
```

---

### 2. `Memory.getDetachedControls`
Returns an array of controls that are no longer attached to any active visual tree but are still held in memory by reference. 

> [!NOTE]
> This command prints debug diagnostics to `Console` if it detects that common test targets (e.g., elements named `leakButton`) are still registered in internal CDP session collections (such as `_propertyHandlers`, `_collectionHandlers`, `_classesHandlers`, or `NodeMap`).

#### Request Example:
```json
{
  "id": 202,
  "method": "Memory.getDetachedControls"
}
```

#### Response Example:
```json
{
  "id": 202,
  "result": {
    "detachedControls": [
      {
        "id": "0x00A3F10D",
        "type": "Avalonia.Controls.Button",
        "name": "leakButton",
        "hashCode": 10744077,
        "detachedDurationMs": 5230,
        "hasDataContext": true,
        "dataContextType": "CdpSampleApp.ViewModels.MainViewModel"
      }
    ]
  }
}
```

---

### 3. `Memory.getRetainers`
Performs an on-demand reference traversal starting from known roots to find what is retaining a control with the specified hash code.

#### Request Example:
```json
{
  "id": 203,
  "method": "Memory.getRetainers",
  "params": {
    "hashCode": 10744077
  }
}
```

#### Response Example:
```json
{
  "id": 203,
  "result": {
    "name": "Target Control",
    "type": "Avalonia.Controls.Button",
    "hashCode": 10744077,
    "retainers": [
      {
        "name": "Field '_leakButton'",
        "type": "CdpSampleApp.Views.MainView",
        "hashCode": 5912384,
        "retainers": [
          {
            "name": "Delegate target [0] (OnClickHandler)",
            "type": "CdpSampleApp.ViewModels.MainViewModel",
            "hashCode": 8102345,
            "retainers": [
              {
                "name": "GC Root",
                "type": "Root",
                "hashCode": 0
              }
            ]
          }
        ]
      }
    ]
  }
}
```

---

### 4. `Memory.getHeapInfo`
Queries the standard .NET Garbage Collector (`GC`) to retrieve heap allocation sizes, committed memory, fragmentation, and collection counts.

#### Request Example:
```json
{
  "id": 204,
  "method": "Memory.getHeapInfo"
}
```

#### Response Example:
```json
{
  "id": 204,
  "result": {
    "totalAllocatedBytes": 15482910,
    "committedHeapBytes": 22020096,
    "fragmentedBytes": 1048576,
    "gen0Collections": 14,
    "gen1Collections": 3,
    "gen2Collections": 1
  }
}
```

---

### 5. `Memory.takeHeapSnapshot`
Constructs a comprehensive heap snapshot mapping all visual nodes and their `DataContext` instances. The resulting layout utilizes V8's native node, edge, and string array structure.

#### Request Example:
```json
{
  "id": 205,
  "method": "Memory.takeHeapSnapshot"
}
```

#### Response Example (Truncated):
```json
{
  "id": 205,
  "result": {
    "snapshot": {
      "meta": {
        "node_fields": [ "type", "name", "id", "self_size", "edge_count", "trace_node_id" ],
        "node_types": [
          [ "hidden", "array", "string", "object", "code", "closure", "regexp", "number", "native", "synthetic", "concatenated string", "sliced string" ],
          "string",
          "number",
          "number",
          "number",
          "number"
        ],
        "edge_fields": [ "type", "name_or_index", "to_node" ],
        "edge_types": [
          [ "context", "element", "property", "internal", "hidden", "shortcut" ],
          "string_or_number",
          "number"
        ]
      },
      "node_count": 12,
      "edge_count": 28
    },
    "nodes": [ 3, 0, 1, 96, 2, 0, 3, 1, 2, 64, 0, 0 ],
    "edges": [ 2, 0, 6, 1, 0, 0 ],
    "strings": [ "MainWindow#mainWindow", "MainWindowViewModel" ]
  }
}
```

---

### 6. `Memory.collectGarbage`
Forces the execution of a full .NET garbage collection cycle. To ensure that weak references and finalizers are processed, it runs:
```csharp
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
```
This is a critical command to run prior to querying detached controls to ensure any naturally dead references have been cleaned up.

#### Request Example:
```json
{
  "id": 206,
  "method": "Memory.collectGarbage"
}
```

#### Response Example:
```json
{
  "id": 206,
  "result": {}
}
```

> [!TIP]
> `Memory.forciblyPurgeJavaScriptMemory` is mapped to the exact same logic for compatibility with standard browser automation clients that call it.

---

### 7. Stub Commands
To remain compliant with standard DevTools clients (which call cleanup setups before testing), the following stubs are implemented to return an empty object:
* `Memory.prepareForLeakDetection`
* `Memory.setPressureNotificationsSuppressed`
* `Memory.simulatePressureNotification`

---

## Guide: Detecting Memory Leaks

Using the CDP Memory Domain, you can implement an automated test script to catch UI memory leaks during continuous integration:

**Memory Leak Detection Sequence**

Participants: **Test Runner (CDP Client)**, **Avalonia Application**

*Phase 1 — Establish baseline:*

1. Test Runner → App: `Memory.collectGarbage`
2. App → Test Runner: `{}`
3. Test Runner → App: `Memory.getLiveControls`
4. App → Test Runner: `{ "controls": [...] }`

*Phase 2 — Perform action (Open/Close page):*

5. Test Runner → App: `Input.dispatchMouseEvent` (Click to open View)
6. Test Runner → App: `Input.dispatchMouseEvent` (Click to close View)

*Phase 3 — Verify cleanup:*

7. Test Runner → App: `Memory.collectGarbage`
8. App → Test Runner: `{}`
9. Test Runner → App: `Memory.getDetachedControls`
10. App → Test Runner: `{ "detachedControls": [ ... ] }`

*Phase 4 — Identify leaks:*

11. **If** detached controls are found:
    - Test Runner → App: `Memory.getRetainers(hashCode)`
    - App → Test Runner: `{ "name": "Target", "retainers": [...] }`
    - Test Runner traces the retainer tree to find the leak source

### Automated Leak Detection Recipe
Below is a step-by-step example workflow for a CDP automation agent:

1. **Clean Baseline**: Run `Memory.collectGarbage` to clean up any temporary objects.
2. **Perform UI Loop**: Programmatically open a sub-window, perform interactions, and close the sub-window.
3. **Trigger Cleanup**: Call `Memory.collectGarbage` again.
4. **Identify Detached Objects**: Call `Memory.getDetachedControls`.
5. **Inspect Retention Paths**: If any controls from the closed sub-window appear in the list, extract their `hashCode` and call `Memory.getRetainers` with that hash code. Parse the resulting retainer tree to identify the event handler or property backing the reference leak.

---

## Implementation Reference

* **Domain Controller**: `MemoryDomain.cs`
* **Visual Tree Operations**: `MemoryDomain.cs` (Line 389)
* **Reference Crawler Logic**: `MemoryDomain.cs` (Line 517)
* **Weak Tracker Registry**: `MemoryDomain.cs` (Line 429)
