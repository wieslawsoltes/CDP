---
title: Log Domain
---

# Log Domain

The Log domain bridges Avalonia's built-in logging system to CDP clients, enabling real-time log streaming to DevTools frontends and automation agents.

## Architecture

The Log domain uses a composite sink pattern to intercept Avalonia log messages without disrupting existing logging:

```
Avalonia Logger ──→ CompositeLogSink
                    ├─ Original App LogSink
                    └─ CdpLogSink ──→ LogDomain.BroadcastLog
                                      ├─ CDP Client 1
                                      └─ CDP Client 2
```

## Methods

### Log.enable

Start receiving log events:

```json
{
  "id": 1,
  "method": "Log.enable"
}
```

### Log.disable

Stop receiving log events:

```json
{
  "id": 2,
  "method": "Log.disable"
}
```

## Events

### Log.entryAdded

Emitted for each Avalonia log message after `Log.enable`:

```json
{
  "method": "Log.entryAdded",
  "params": {
    "entry": {
      "source": "Layout",
      "level": "warning",
      "text": "Control exceeded layout bounds",
      "timestamp": 1719936000000
    }
  }
}
```

**Log levels:**
- `verbose` — Trace-level detail
- `info` — Informational messages
- `warning` — Non-critical issues
- `error` — Errors and exceptions

## Implementation

### CdpLogSink

Implements `Avalonia.Logging.ILogSink` and forwards every message to `LogDomain.BroadcastLog`:

- Always enabled for all log levels and areas
- Handles format string interpolation safely (falls back to raw format on failure)
- Broadcasts asynchronously to all connected sessions

### CompositeLogSink

A decorator that wraps the application's existing `ILogSink`:

- Both the original sink and `CdpLogSink` receive every log call
- `IsEnabled` returns `true` if either sink is enabled
- Installed on `CdpServer.Start()`, restored on `CdpServer.Stop()`

This pattern ensures CDP log streaming doesn't interfere with the application's own logging configuration.

## Inspector Integration

The Console panel displays log entries alongside user-entered expressions. Log entries are color-coded by level:

| Level | Color |
|-------|-------|
| Error | Red |
| Warning | Amber |
| Info | Blue |
| Verbose | Gray |

## Next Steps

- [Console Panel](/articles/console-panel) — Interactive console with log display
- [Runtime Domain](/articles/runtime-domain) — C# expression evaluation
- [Architecture](/articles/architecture) — Server lifecycle and initialization
