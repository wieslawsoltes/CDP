---
title: Debugger Domain
---

# Debugger Domain

The Debugger domain provides breakpoint management, pause/resume control, and step-through debugging for Avalonia CDP sessions. Unlike browser DevTools which debugs JavaScript, this domain debugs C# script execution within the CDP runtime, using Roslyn for conditional breakpoint evaluation.

## Overview

| Method | Description |
|---|---|
| `enable` | Initialize the debugger |
| `disable` | Deactivate the debugger |
| `setBreakpointByUrl` | Set a breakpoint at a URL and line number |
| `removeBreakpoint` | Remove a breakpoint by ID |
| `resume` | Resume from a paused state |
| `stepOver` | Step over the current statement |
| `stepInto` | Step into the current statement |
| `stepOut` | Step out of the current scope |

## Methods

### Debugger.enable

Initialize the debugger and receive a debugger ID:

```json
{
  "id": 1,
  "method": "Debugger.enable"
}
```

**Response:**

```json
{
  "id": 1,
  "result": {
    "debuggerId": "1"
  }
}
```

### Debugger.setBreakpointByUrl

Set a breakpoint that triggers when execution reaches a specific URL and line number:

```json
{
  "id": 2,
  "method": "Debugger.setBreakpointByUrl",
  "params": {
    "url": "test-flow.yaml",
    "lineNumber": 5,
    "condition": "Window.DataContext.IsLoggedIn == true"
  }
}
```

**Response:**

```json
{
  "id": 2,
  "result": {
    "breakpointId": "test-flow.yaml:5",
    "locations": [
      {
        "scriptId": "test-flow.yaml",
        "lineNumber": 5,
        "columnNumber": 0
      }
    ]
  }
}
```

**Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `url` | string | URL or filename to match (fuzzy, case-insensitive) |
| `urlRegex` | string | Alternative regex pattern for URL matching |
| `lineNumber` | int | Line number (0-indexed) |
| `condition` | string | Optional C# expression evaluated via Roslyn |

### Conditional Breakpoints

The `condition` parameter is evaluated as a C# expression using the same Roslyn scripting engine as `Runtime.evaluate`. It has access to all runtime globals:

```json
{
  "id": 3,
  "method": "Debugger.setBreakpointByUrl",
  "params": {
    "url": "login-flow.yaml",
    "lineNumber": 10,
    "condition": "((MyApp.ViewModels.LoginViewModel)Window.DataContext).LoginAttempts > 3"
  }
}
```

The breakpoint only triggers when the condition evaluates to `true`.

### Debugger.removeBreakpoint

Remove a previously set breakpoint:

```json
{
  "id": 4,
  "method": "Debugger.removeBreakpoint",
  "params": {
    "breakpointId": "test-flow.yaml:5"
  }
}
```

### Debugger.resume

Resume execution after hitting a breakpoint:

```json
{
  "id": 5,
  "method": "Debugger.resume"
}
```

### Debugger.stepOver / stepInto / stepOut

Step through execution:

```json
{
  "id": 6,
  "method": "Debugger.stepOver"
}
```

All step commands signal the internal `ManualResetEventSlim` to unblock the paused thread.

## Events

### Debugger.paused

Emitted when a breakpoint is hit:

```json
{
  "method": "Debugger.paused",
  "params": {
    "callFrames": [
      {
        "callFrameId": "0",
        "functionName": "StepExecution",
        "location": {
          "scriptId": "test-flow.yaml",
          "lineNumber": 5,
          "columnNumber": 0
        },
        "url": "test-flow.yaml",
        "scopeChain": [
          {
            "type": "local",
            "object": {
              "type": "object",
              "objectId": "scope:local:0",
              "className": "LocalScope",
              "description": "Local Variables"
            },
            "name": "Local"
          }
        ],
        "this": {
          "type": "undefined"
        }
      }
    ],
    "reason": "other",
    "hitBreakpoints": ["test-flow.yaml:5"]
  }
}
```

The scope chain's `objectId` can be inspected with `Runtime.getProperties` to view local variables including `CurrentUrl`, `CurrentLine`, and `Message`.

### Debugger.resumed

Emitted when execution resumes:

```json
{
  "method": "Debugger.resumed",
  "params": {}
}
```

## Implementation Details

The debugger uses a `ManualResetEventSlim` to implement thread-blocking pause behavior:

1. When a breakpoint is hit, `CheckBreakpoint()` is called
2. The condition (if any) is evaluated using Roslyn C# scripting
3. If the breakpoint triggers, the calling thread is blocked via `DebuggerBlockEvent.Wait()`
4. A `Debugger.paused` event is sent to all connected clients
5. When `resume` or any step command is received, the event is signaled
6. The blocked thread continues and a `Debugger.resumed` event is sent

:::warning
Breakpoint evaluation blocks the calling thread. If this thread is the UI thread, the application will freeze until resumed. This is intentional behavior for debugging but should be used carefully in production.
:::

## Next Steps

- [DOM Debugger Domain](/articles/dom-debugger-domain) — Event listener inspection
- [Runtime Domain](/articles/runtime-domain) — C# expression evaluation
- [Console Panel](/articles/console-panel) — Interactive debugging console
