---
title: Console Panel
description: Guide to interactive C# REPL execution, command history, autocompletion, inspected node reference ($0), and UI command scripting in the CDP Inspector for Avalonia.
---

# Console Panel

The **Console Panel** provides a rich interactive runtime shell (REPL) and logging manager for the connected Avalonia application. It serves as both a diagnostics output screen for runtime logs and a control center for evaluating expressions directly in the context of the running application.

---

## 1. Log Auditor and Filtering

The console integrates a trace viewer that captures real-time application logs emitted by standard .NET `ILogger` sources and diagnostic trace streams.

### Log Entry Grid (`listLogs`)
All logged statements are shown in a detailed table format:
- **Timestamp**: Time resolution down to milliseconds (`HH:mm:ss.fff`).
- **Level**: Colorful severity indicators (Error in red, Warning in yellow, Info in green, Verbose in grey).
- **Message**: Complete log statement strings, supporting text-wrapping.
- **Double-Tap Interaction**: Double-tapping a log entry automatically selects it, copying the full text and metadata stack trace to the system clipboard for debugging.

### Filter Toolbar
To maintain focus when debugging complex applications, the log grid features filter controls:
- **Severity Toggles**: Quick buttons to toggle specific log levels (e.g., viewing only `Error` and `Warning` logs).
- **Search Box (`FilterQuery`)**: Searches for specific keywords, filtering matching text strings dynamically.
- **Clear Console**: The `btnClearLogs` button wipes all active log buffers.

---

## 2. Interactive C# REPL Execution

Unlike web-based inspectors that run JavaScript, the CDP Avalonia Console runs C# scripting. When a statement is submitted:
1. The text is captured from `txtConsoleInput`.
2. It is sent as a `Runtime.evaluate` CDP request.
3. The server-side script engine compiles and runs the statement within the thread context of the Avalonia application.
4. The resolved output is returned and appended to the console output list.

### Global Object Access
The execution environment exposes several C# globals that allow querying and controlling the application:
- `Window`: Points to the current top-level active Avalonia Window.
- `Control`: Points to the currently inspected visual node.
- `DataContext`: Accesses the data context / view model of the current control.
- `ViewModel`: Convenient alias for `DataContext`.
- `Query(selector)`: Visual tree lookup helper matching stable ID/name selectors.
- `QueryAll(selector)`: Multi-control selection query helper.

### Browser-Like DOM Facades
To keep the shell intuitive, browser-like shortcuts are mapped to C# reflection calls:
- `document.querySelector("#btnConnect")`: Finds a control by Name/ID.
- `document.querySelectorAll(".primary")`: Finds all controls matching a class name.
- `document.getElementById("btnRefreshTargets")`: Directly returns the control with the matching name.

---

## 3. Inspected Node Reference (`$0`)

A hallmark of the console interaction is the `$0` variable. 

### Mapping
The `$0` variable is a persistent reference pointing to the currently selected element in the **Elements Panel**. 
Whenever you click a node in the visual DOM tree, the inspector updates the `$0` reference on the CDP server.

### Advanced Shell Queries
Using `$0`, you can inspect dynamic states, cast components, and execute actions programmatically:
- **Check visibility**:
  `$0.IsVisible`
- **Inspect DataContext properties**:
  `((MyViewModel)$0.DataContext).IsConnected`
- **Modify properties directly**:
  `$0.Opacity = 0.5`
- **Invoke backing command objects**:
  `$0.Command.Execute($0.CommandParameter)`

By using C# castings on `$0`, developers avoid writing complex testing harness code.

---

## 4. UI REPL Mode

Toggling the **UI REPL Mode** switch transforms the console input from a raw C# shell into a natural-language automation scripting line. It allows typing browser-like commands that are automatically translated to Test Studio steps:

| Command Typed | Action Parsed | Value Extracted | Test Studio Action |
|:---|:---|:---|:---|
| `tap #btnConnect` | `tapOn` | Selector: `#btnConnect` | Simulates mouse tap |
| `click #btnClickMe` | `tapOn` | Selector: `#btnClickMe` | Simulates mouse click |
| `doubletap #image` | `doubleTapOn` | Selector: `#image` | Simulates double tap |
| `input #txtUser admin` | `inputText` | Selector: `#txtUser`, Val: `admin` | Types text |
| `assert #label` | `assertVisible` | Selector: `#label` | Asserts control visible |
| `scroll #scroll` | `scrollUntilVisible` | Selector: `#scroll` | Scrolls list container |

When submitted in UI REPL mode, the statement is added to the active Test Studio recording workspace, executing the action on the target application immediately.

---

## 5. Auto-Completion and IntelliSense

When typing in the input box `txtConsoleInput`, the console provides auto-completion suggestions inside a popover list:
- **C# Intellisense**: Queries the backend compiler via `Runtime.getCompletions` to suggest methods, properties, and variables matching the current input caret.
- **UI REPL Selectors**: In UI REPL mode, it queries the `SelectorService` instance to suggest names, IDs, and Automation properties currently present in the target visual tree.
- **Keyboard Navigation**: Pressing `Tab` or `Enter` inserts the highlighted completion, while `Up` and `Down` arrows select adjacent choices.

---

## 6. Command History

To speed up repetitive testing workflows, the input field retains command history:
- Pressing the **Up Arrow** key walks backward through previously evaluated commands.
- Pressing the **Down Arrow** key navigates forward toward the latest input.
- Backed by the view model's `GetPreviousHistoryLine()` and `GetNextHistoryLine()` handlers, the system prevents duplicate history entries when executing the same query repeatedly.

---

## 7. Live Pinned Expressions

The right sidebar features a **Live Pinned Expressions** panel, similar to a watch window in standard compilers:
- **Adding Expressions**: Entering an expression (e.g. `$0.DataContext` or `Window.Width`) in `txtPinnedExpression` adds it to the active watch list.
- **Timer-Based Evaluation**: A background `DispatcherTimer` evaluates all pinned expressions once every `1000` milliseconds.
- **Live State Feedback**: The list updates the value strings dynamically. If an expression causes a runtime compiler issue, it displays the exception trace in red, preserving the other watches.
- **Removal**: Clicking the delete icon executes `RemovePinnedExpressionCommand` to clear the watch.

---

## 8. Complex Object Tree Inspector

If a C# statement returns an object rather than a simple string, the console renders a tree grid (`ConsoleObjectNode`):
- **Hierarchical Nodes**: Properties are listed with names, current values, and types in parentheses.
- **Lazy Loading**: Expanding an object node sends a `Runtime.getProperties` request referencing the object's `objectId`. This fetches child fields on-demand, preventing high memory footprint over the network.
