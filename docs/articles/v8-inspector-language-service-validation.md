# V8 Inspector and JavaScript language-service validation

Validation performed on 2026-08-04 in Release configuration on macOS.

## Real V8 debugging session

The desktop CDP Inspector connected to a real Node V8 inspector started with
`--inspect-brk`. The session discovered the target, enabled the Runtime and
Debugger domains, paused at startup, loaded the runtime JavaScript source, and
exposed the active line, call stack, and local scope. The Inspector application
was simultaneously inspected and controlled through its own remote CDP
endpoint on port 9223.

![CDP Inspector paused in a real Node V8 session](/assets/v8-debugging/cdp-inspector-node-v8-paused.png)

[Recorded step/resume session (MP4)](/assets/v8-debugging/cdp-inspector-node-v8-debug-session.mp4)

## JavaScript and TypeScript editor service

`Chrome.DevTools.JavaScript.LanguageServer` embeds the official TypeScript 5.9
language service and standard-library declarations. It runs in-process through
Jint without a Node or global `tsserver` dependency and provides completion,
quick info, diagnostics, signature help, definitions, references, rename,
symbols, semantic classifications, and formatting for JS, JSX, TS, and TSX.

The Inspector Sources editor currently wires completion, quick info, and
debounced diagnostics. Runtime debugger hover remains the priority while V8 is
paused. The remaining editor commands and deeper Chrome DevTools-parity work
are tracked in WebScene issue #7.

## Automated evidence

- Full CDP Inspector headless suite: 154 passed.
- Full V8 Inspector suite: 39 passed, 1 environment-gated WebScene acceptance
  test skipped.
- Embedded JavaScript language-service suite: 3 passed.
- Avalonia accessibility serialization suite: 10 passed.
- Core protocol suite: 10 passed.
- Real Chrome V8 integration: breakpoint, paused-frame evaluation, resume,
  CPU profiling, heap usage, and screenshot capture passed.
- Real Node CdpService integration: debugger pause/scopes/properties and the
  30-second WebSocket heartbeat boundary passed.
- Full solution Release build: succeeded with 0 errors.

The WebScene React acceptance test remains intentionally environment-gated and
must be rerun whenever a new WebScene V8 binary is published.
