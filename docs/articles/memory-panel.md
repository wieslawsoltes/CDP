---
title: Memory Panel
description: Technical guide to heap snapshots, baseline memory comparisons, detached controls detection, and retainer tree path traversal in the CDP Inspector for Avalonia.
---

# Memory Panel

The **Memory Panel** is a diagnostic interface used to profile heap allocations, track live control instantiation, detect memory leaks, and inspect reference retainers in the connected Avalonia application.

---

## 1. Heap Snapshots Management

The left panel of the interface provides controls for capturing and exporting memory snapshots:

- **Take Heap Snapshot**: Clicking `btnTakeSnapshot` requests a full memory scan. The application collects reference metrics and creates a new snapshot entry in `lstSnapshots`.
- **Collect Garbage**: The `btnGcCollect` button triggers `GC.Collect()`. It is recommended to run this before taking a snapshot to ensure you only profile active, reachable memory.
- **Export V8 JSON**: The `btnExportSnapshot` command exports the selected heap snapshot as a standard V8-compatible `.heapsnapshot` JSON file. This file can be loaded directly into Chrome Developer Tools or external profilers for deeper object analysis.

---

## 2. Memory Allocation Rate Timeline

The top of the details panel features the **Memory Allocation Rate Timeline**:
- **TimelineChart Integration**: Plots a real-time graph of memory allocation activity in MB.
- **Leak Detection**: Continuous upward memory trends during cyclic UI operations (such as opening and closing windows) indicate memory leaks.

---

## 3. Live Counts and Baseline Comparison Mode

The **Live Counts** tab displays allocation metrics for constructor types:

### Single Snapshot Summary Mode
When Comparison mode is disabled, the panel lists allocation counts for each control type:
- **Control Type / Constructor**: The class type of the object (e.g. `System.String`, `Avalonia.Controls.Button`).
- **Live Instances Count**: The total number of active instances currently allocated in the heap.

### Baseline Comparison Mode
Checking the **Comparison mode** option enables comparison controls:
- **Baseline Selector**: Choose a baseline snapshot to compare against the current snapshot.
- **Comparison Columns**: The table updates to show:
  - **Baseline**: Count of instances in the baseline snapshot.
  - **Current**: Count of instances in the active snapshot.
  - **Delta**: The difference in instance count, highlighted in red (positive increase) or blue (negative decrease).
- **Use Case**: This mode is ideal for verifying that temporary views are cleaned up. For example, taking a baseline snapshot, performing an action (like opening a dialog), closing the dialog, and taking a second snapshot should show a Delta of zero for the dialog's controls. A positive Delta indicates that the dialog's elements are leaking.

---

## 4. Detached Controls (Leaked UI Elements)

In Avalonia applications, a common source of memory leaks is "detached controls". These are controls that have been removed from the visual tree (e.g., closed views) but remain allocated in memory because an active reference (like an event subscription or static cache) is holding onto them.

The **Detached Controls** tab lists these leaked items:
- **Control Type**: The class name of the leaked control.
- **Name**: The control identifier name, if defined in XAML.
- **Detached Duration**: The amount of time (in milliseconds) the control has been detached from the visual tree while remaining allocated in memory.
- **DataContext VM**: The type name of the control's data context view model. If the view model is also retained, it will be listed here.

---

## 5. Retainers Path Tree (Retained By)

When a detached control is selected, the bottom panel displays the **Retainers Path** tree (`treeRetainers`). This tree traces the chain of object references that are holding the selected control in memory:

```
[Target: Avalonia.Controls.Button - HashCode: 0x00A1F2C4]
  └── _parent (Avalonia.Controls.Grid) - HashCode: 0x00B23C41
        └── _contentPresenter (Avalonia.Controls.ContentPresenter) - HashCode: 0x00C39E12
              └── _logicalChildren (List<ILogical>) - HashCode: 0x00D492A7
                    └── CommandHandler (System.EventHandler) - HashCode: 0x00E58F1B
                          └── MainViewModel (CdpInspectorApp.ViewModels.MainWindowViewModel) - HashCode: 0x00F612D3 [Static Root]
```

- **Name**: The field, property, or collection index holding the reference.
- **Type**: The class type of the retaining parent object.
- **HashCode**: The unique hexadecimal hash code of the object instance (`[0x{0:X8}]`), helping differentiate between different instances of the same class.
- **Traversal**: Toggles expand nested retainer paths back to a garbage collection root (such as a static field, local variable, or active thread context), pinpointing the source of the reference leak.

---

## 6. CDP Protocol Heap Profiling Specifications

The Memory Panel communicates with the application's heap profiler using these CDP commands:

### Capture Heap Snapshot Stream
```json
{
  "id": 1,
  "method": "HeapProfiler.takeHeapSnapshot",
  "params": {
    "reportProgress": true,
    "treatGlobalObjectsAsRoots": true,
    "captureNumericValue": true
  }
}
```

### Detached Node Query
```json
{
  "id": 2,
  "method": "HeapProfiler.getDetachedControls"
}
```

### Retainer Chain Resolution
```json
{
  "id": 3,
  "method": "HeapProfiler.getRetainers",
  "params": {
    "objectId": "heap-obj-9876",
    "depth": 5
  }
}
```

This diagnostic data helps developers locate memory leaks without needing to run heavy, external profiling tools on target machines.
