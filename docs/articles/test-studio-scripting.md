---
title: Test Studio Scripting (Maestro-style)
---

# Test Studio Scripting (Maestro-style)

To support complex testing workflows—such as conditional verification, dynamic calculations, loops, and data flow carry-over—Test Studio includes support for **dynamic JavaScript execution** inside `.flow.yaml` test files. 

This format is modeled directly after the popular declarative scripting syntax used in `maestro.dev`, combining the simplicity of YAML declarations with the programmatic flexibility of a sandboxed JavaScript runtime (backed by the Jint engine).

---

## 1. YAML Script Step Specifications

You can define script execution steps using two custom step types: `runScript` and `assertScript`.

### A. The `runScript` Step
Executes JavaScript code and assigns the return value to a local context variable. This variable can then be referenced by subsequent steps.

#### Inline Script Execution:
```yaml
- type: runScript
  script: |
    // Read the count of child visual elements
    var listItems = QueryAll("ListBoxItem");
    return listItems.length;
  assignTo: itemCount
```

#### External Script Execution:
```yaml
- type: runScript
  file: scripts/calculate_coordinates.js
  assignTo: customOffset
```

### B. The `assertScript` Step
Verifies a JavaScript expression evaluates to a truthy value. If the script returns `false` or throws an exception, the test step fails.

```yaml
- type: assertScript
  expression: "DataContext.Items.Count === 5"
  timeout: 5000 # Wait up to 5s for condition to become true
```

---

## 2. Shared Flow Context & Variable Interpolation

When a script returns a value and specifies `assignTo`, the value is saved in the flow's runtime execution state dictionary (`FlowContext`). 

Any subsequent step can reference this variable inside parameter values, selectors, or text inputs using the syntax `${context.variableName}`.

```yaml
# Step 1: Query UI and store count
- type: runScript
  script: "return QueryAll('[class~=\"primary\"]').length;"
  assignTo: activeItemCount

# Step 2: Use the variable in a text input step
- type: inputText
  selector: "#txtSearchQuery"
  value: "Found ${context.activeItemCount} items"

# Step 3: Use the variable to target a specific index dynamically
- type: click
  selector: "ListBoxItem[SelectedIndex=\"${context.activeItemCount}\"]"
```

---

## 3. Scoped Execution Context Globals

Every JavaScript script runs in a highly optimized sandbox pre-populated with several contextual global objects, matching the active Avalonia UI window and inspected node:

| Global Variable | Type | Description |
| :--- | :--- | :--- |
| `Window` | `Avalonia.Controls.Window` | The active top-level Avalonia application window. |
| `Control` / `SelectedNode` | `Avalonia.Controls.Control` | The currently inspected visual tree node (if set). |
| `DataContext` / `ViewModel` | `object` | The `DataContext` associated with the inspected control. |
| `Query(selector)` | `Func<string, Visual>` | Queries the visual tree using CSS selectors and returns the first match. |
| `QueryAll(selector)` | `Func<string, IEnumerable>` | Queries the visual tree and returns all matches. |
| `context` | `Dictionary` | Exposes previously assigned flow variables. |

### Advanced Scripting Examples:
```javascript
// Query the active view model bindings directly
var userName = ViewModel.Connection.CurrentUser.UserName;
if (userName === "Admin") {
    return true;
}
return false;
```

---

## 4. Replay Engine Implementation Architecture

During YAML flow playbacks, the Test Studio runner implements script execution using a three-stage sequence:

```text
 YAML Flow Step -> Preprocess Context Variables -> Execute Jint Engine -> Store AssignTo Result
```

1.  **Variable Resolution Pre-pass**: The runner scans step attributes for `${context.variableName}` tokens. It replaces the tokens with values stored in the flow's shared execution context dictionary.
2.  **Jint Sandbox Bootstrapping**: A fresh Jint runtime engine is spun up. The visual tree context objects (`Window`, `ViewModel`, custom query delegates) are injected into the script context scope.
3.  **Return Binding**: If `assignTo` is defined, the return value of Jint's `engine.Evaluate(script)` is marshalled into a JSON/C# primitive and written back to the flow dictionary, ready for subsequent steps.
