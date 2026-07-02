---
title: Runtime Domain
---

# Runtime Domain in CDP Avalonia

The Chrome DevTools Protocol (CDP) **Runtime Domain** in `Avalonia.Diagnostics.Cdp` exposes the runtime state of the application. Unlike standard browser-based CDP implementations where the console and runtime execute JavaScript, the Avalonia CDP implementation executes **C# expressions and scripts**.

This is powered primarily by **Roslyn Scripting** (`Microsoft.CodeAnalysis.CSharp.Scripting`), with a robust, reflection-based fallback evaluator for environments where full Roslyn compilation is not supported or when a simpler reflection traversal is requested.

All execution that interacts with the user interface, controls, or data contexts is automatically dispatched to the **Avalonia UI Thread** (`Dispatcher.UIThread`) to ensure thread safety and prevent synchronization issues.

---

## Architecture & Evaluation Engines

When a client sends a `Runtime.evaluate` command, the request passes through the `RuntimeDomain.cs` handler. 

The evaluation pipeline works as follows:

```
Runtime.evaluate Request
└─ ScriptPreprocessor.Preprocess
   └─ Roslyn ScriptState exists or compile succeeds?
      ├─ Yes ──→ Run C# Script via CSharpScript ──→ Convert result to RemoteObject / JSON
      └─ No / Compilation Exception ──→ Fallback: EvaluateExpression Reflection Interpreter ──→ Convert result to RemoteObject / JSON
```

### 1. Roslyn C# Scripting Engine
The primary engine uses `Microsoft.CodeAnalysis.CSharp.Scripting` to compile and run C# scripts. It sets up standard assembly references (such as `System.Linq`, `Avalonia.Visual`, and `Avalonia.Controls`) and imports default namespaces so you don't need to specify fully qualified type names for common types.

### 2. Fallback Reflection Evaluator
If Roslyn compilation fails (due to assembly access, missing references, or runtime limitations), the system falls back to `EvaluateExpression`. This reflection interpreter manually splits and parses property paths, handles basic logic (like logical OR `||` and equivalence comparisons `==`, `===`, `!=`), resolves method overloads, and executes property reads, assignments, and method invocations dynamically.

---

## C# Script Preprocessing

To make writing expressions easier and more aligned with traditional browser developer tools, the evaluation engine pre-processes inputs via `ScriptPreprocessor.Preprocess`:

*   **`$0`** is replaced with **`_0`** (the concrete-typed reference to the currently selected node).
*   **`$vm`** is replaced with **`DataContext`**.
*   **`$dc`** is replaced with **`DataContext`**.

For example, the expression:
```csharp
$vm.Items.Count
```
is preprocessed into:
```csharp
DataContext.Items.Count
```

---

## State Persistence

The evaluation session maintains a stateful C# context. If you define variables or execute statements, the variables are preserved across sequential `Runtime.evaluate` calls using Roslyn's `ContinueWithAsync` mechanism.

> [!NOTE]
> The session state is bound to the currently inspected node. If the inspected node ID changes (meaning you select a different control in the visual tree), the script session is reset (`session.ScriptSession = null`) to prevent stale typed aliases (like `_0`) from referencing incorrect visual targets.

---

## Evaluation Globals (`ReplGlobals`)

When executing C# code in the console or via CDP, you write code within the context of the `ReplGlobals` class. The following globals are exposed and directly accessible:

| Global Symbol | Type | Description |
| :--- | :--- | :--- |
| `SelectedNode` | `Avalonia.Visual?` | The currently selected/inspected node in the visual tree. |
| `_0` | `var` (Typed) | A typed representation of `SelectedNode` declared automatically (e.g., `var _0 = (Button)SelectedNode;`). |
| `Control` | `Avalonia.Controls.Control?` | The inspected node cast to `Control`. |
| `DataContext` | `object?` | The data context of the inspected control (`Control?.DataContext`). |
| `ViewModel` | `object?` | Alias for `DataContext`. |
| `Window` | `Avalonia.Controls.Window?` | The current active window or the parent window of `SelectedNode`. |
| `window` | `CdpRuntimeWindow` | A browser-like global representing the window context. |
| `document` | `CdpRuntimeDocument` | A browser-like entry point for querying elements. |
| `Query(selector)` | `Visual?` | Queries a child relative to the `SelectedNode` (or `Window` if none selected). |
| `QueryAll(selector)` | `IEnumerable<Visual>` | Queries all matching children relative to the `SelectedNode`. |
| `Print(object?)` | `void` | Utility method to write output directly to the system console. |

---

## Browser-like Facade Objects

To assist automation agents built for web browsers, `Avalonia.Diagnostics.Cdp` provides facade objects mimicking the browser DOM.

### `CdpRuntimeWindow`
Wraps the session window and exposes:
*   `document`: Access to `CdpRuntimeDocument`.
*   `visual`: The underlying `Avalonia.Controls.Window`.

### `CdpRuntimeDocument`
Exposes querying methods:
*   `querySelector(selector)`: Queries the visual tree starting from the root window using CDP selectors. Returns a `CdpRuntimeElement`.
*   `querySelectorAll(selector)`: Returns an array of `CdpRuntimeElement` matching the selector.
*   `getElementById(id)`: Helper that maps to `querySelector($"[id=\"{id}\"]")`.
*   `getPropertiesJson(selector)`: Serializes common properties of a control (like `Text`, `Value`, `IsEnabled`, `IsChecked`) into a JSON string.

### `CdpRuntimeElement`
Wraps a specific `Avalonia.Visual` to expose browser-compatible fields:
*   **Properties**:
    *   `nodeId`: The mapped integer ID of the visual node.
    *   `nodeName` / `tagName` / `localName`: The C# type name of the control (e.g., `Button`).
    *   `id`: The value of the control's `Name` attribute.
    *   `textContent` / `innerText` / `value`: Text/Content of the control.
    *   `isVisible` / `isEffectivelyVisible`: Visibility state checks.
    *   `isEnabled`: Enabled state check.
    *   `visual`: The actual `Avalonia.Visual` object.
*   **Methods**:
    *   `getAttribute(name)`: Retrieves simulated attributes (like `id`, `text`, `IsEnabled`).
    *   `matches(selector)`: Checks if the wrapped visual matches a selector.
    *   `querySelector(selector)`: Runs selector queries scoped to this element's children.
    *   `querySelectorAll(selector)`: Scoped query for all children.
    *   `closest(selector)`: Traverses up ancestors to find the nearest match.

---

## Practical C# Expression Examples

### 1. Simple Property Inspections
```csharp
// Inspect the name of the currently selected control
Control.Name

// Read the width of the active window
Window.Width

// Check if a specific button is effectively visible in the tree
document.querySelector("#btnConnect").isEffectivelyVisible
```

### 2. Reading Data Contexts and View Models
```csharp
// Inspect data context
DataContext

// Inspect properties on a concrete view model
((CdpInspectorApp.ViewModels.MainWindowViewModel)DataContext).Recorder.IsRecording

// Query a boolean value from the ViewModel
Window.DataContext.Connection.IsConnected
```

### 3. Modifying Application State
```csharp
// Change a control's property directly
Control.Width = 300

// Set properties on a nested object
Control.Margin = new Avalonia.Thickness(10)
```

### 4. Direct Visual Querying
```csharp
// Find a button and click it programmatically (by invoking a method or command)
var btn = (Button)Query("#btnRefreshTargets");
btn.Command?.Execute(null);
```

---

## Autocomplete / IntelliSense

The domain also implements `Runtime.getCompletions` using Roslyn's `AdhocWorkspace`. It constructs a virtual script file in memory with appropriate imports and local declarations (like `SelectedNode` and `_0` cast to the concrete control type) to determine context-aware completions at the given cursor position.

---

## JSON Protocol Specification

Below are JSON payloads outlining typical requests and responses with the CDP server.

### `Runtime.evaluate` (Return by Value)
Forces the server to serialize and return the literal evaluated result.

**Request:**
```json
{
  "id": 100,
  "method": "Runtime.evaluate",
  "params": {
    "expression": "document.querySelector(\"#btnRefreshTargets\").isEnabled",
    "returnByValue": true
  }
}
```

**Response:**
```json
{
  "id": 100,
  "result": {
    "result": {
      "value": true
    }
  }
}
```

### `Runtime.evaluate` (Return Remote Object Reference)
Returns a reference `objectId` that can be queried or passed to other methods.

**Request:**
```json
{
  "id": 101,
  "method": "Runtime.evaluate",
  "params": {
    "expression": "Window.DataContext",
    "returnByValue": false
  }
}
```

**Response:**
```json
{
  "id": 101,
  "result": {
    "result": {
      "type": "object",
      "className": "CdpInspectorApp.ViewModels.MainWindowViewModel",
      "description": "MainWindowViewModel (CdpInspectorApp.ViewModels.MainWindowViewModel)",
      "objectId": "remote-obj-id-1234"
    }
  }
}
```

### `Runtime.getProperties`
Inspects the public properties of a remote object reference.

**Request:**
```json
{
  "id": 102,
  "method": "Runtime.getProperties",
  "params": {
    "objectId": "remote-obj-id-1234"
  }
}
```

**Response:**
```json
{
  "id": 102,
  "result": {
    "result": [
      {
        "name": "Connection",
        "value": {
          "type": "object",
          "className": "CdpInspectorApp.ViewModels.ConnectionViewModel",
          "description": "ConnectionViewModel (CdpInspectorApp.ViewModels.ConnectionViewModel)",
          "objectId": "remote-obj-id-5678"
        },
        "writable": true,
        "configurable": true,
        "enumerable": true
      },
      {
        "name": "Recorder",
        "value": {
          "type": "object",
          "className": "CdpInspectorApp.ViewModels.RecorderViewModel",
          "description": "RecorderViewModel (CdpInspectorApp.ViewModels.RecorderViewModel)",
          "objectId": "remote-obj-id-9012"
        },
        "writable": true,
        "configurable": true,
        "enumerable": true
      }
    ]
  }
}
```

### `Runtime.callFunctionOn`
Invokes a declaration targeting a specific remote object.

**Request:**
```json
{
  "id": 103,
  "method": "Runtime.callFunctionOn",
  "params": {
    "objectId": "remote-obj-id-1234",
    "functionDeclaration": "function() { return this.Recorder.IsRecording; }",
    "returnByValue": true
  }
}
```

**Response:**
```json
{
  "id": 103,
  "result": {
    "result": {
      "value": false
    }
  }
}
```

---

## Implementation Reference
For implementation details, view the source code at:
*   `RuntimeDomain.cs`
