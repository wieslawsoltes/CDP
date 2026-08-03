# WebScene V8 Inspector integration

`Chrome.DevTools.Protocol` contains a lightweight, raw V8 Inspector host intended for WebScene. It serves Chrome discovery metadata and copies complete UTF-8 Inspector messages between Chrome DevTools and WebScene's native V8 inspector without routing them through the managed `CdpDispatcher`.

This is the CDP-side implementation required by [WebScene issue #7](https://github.com/wieslawsoltes/WebScene/issues/7). WebScene now provides the native adapter and a dependency-free `WebScene.Diagnostics.Cdp` Chrome discovery host. CDP Inspector can connect to that endpoint directly; hosts that already depend on `Chrome.DevTools.Protocol` may instead adapt the same `INativeV8InspectorSession` to `IRawCdpTransport`.

## Package boundaries

The core package depends only on `Microsoft.Extensions.Logging.Abstractions` (plus framework-provided packages). Heavy features are opt-in:

| Package | Optional dependencies and responsibility |
| --- | --- |
| `Chrome.DevTools.Protocol` | Discovery, WebSocket transport, raw Inspector contracts, managed CDP server contracts |
| `Chrome.DevTools.Protocol.Jint` | Jint evaluator and remote-object unwrapping adapter |
| `Chrome.DevTools.Protocol.Skia` | Screencast reconstruction and tiled frame production |
| `Chrome.DevTools.Protocol.Profiling` | EventPipe, dotTrace, and dotMemory engines; explicit `CdpProfilingComposition.Register()` |
| `Chrome.DevTools.Protocol.Automation` | Existing client, OS automation, YAML flows, and reports |
| `Chrome.DevTools.Protocol.Extensions` | Compatibility bundle that references all optional packages |

XAML mutation is now coupled through `ICdpMutationEngine`; Jint values through `ICdpRemoteObjectAdapter`; optional session cleanup through `CdpSessionCleanupRegistry`; and profiler domains through explicit composition.

## WebScene adapter

WebScene's `NativeWebSceneView.OpenV8InspectorSession()` returns a raw duplex
session whose messages match `IRawCdpTransport` semantics. Its native ABI queues
connect, dispatch, and disconnect actions onto the isolate worker, registers the
outer and iframe contexts, implements the nested pause loop, and reports timer /
animation-frame async tasks. The optional WebScene host can be started directly:

```csharp
var inspector = new WebSceneV8InspectorHost(
    view,
    new WebSceneV8InspectorOptions
    {
        Enabled = true,
        Address = IPAddress.Loopback,
        Port = 9229
    });
await inspector.StartAsync();
```

Chrome discovers this endpoint through `chrome://inspect`; CDP Inspector uses
the token-bearing `webSocketDebuggerUrl` from `/json/list`.

The generic CDP-core composition below remains useful when an application wants
one server to publish WebScene alongside other raw V8 targets.

WebScene should implement one `IRawCdpTarget` per V8 context group or debuggable page and one `IRawCdpTransport` per attached DevTools session. `RawCdpTransportBase` supplies a bounded, backpressure-aware outgoing queue for native V8 callbacks.

```csharp
using System.Text;
using Chrome.DevTools.Protocol.Inspector;

sealed class WebSceneInspectorTarget : IRawCdpTarget
{
    private readonly WebSceneV8Runtime _runtime;

    public WebSceneInspectorTarget(WebSceneV8Runtime runtime)
    {
        _runtime = runtime;
        Info = new CdpInspectorTargetInfo(
            runtime.TargetId,
            runtime.DocumentTitle,
            runtime.DocumentUrl,
            Type: "node",
            Description: "WebScene embedded V8 runtime");
    }

    public CdpInspectorTargetInfo Info { get; }

    public ValueTask<IRawCdpTransport> OpenTransportAsync(
        CdpInspectorConnectionContext context,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IRawCdpTransport>(new WebSceneV8Transport(_runtime));
}

sealed class WebSceneV8Transport : RawCdpTransportBase
{
    private readonly NativeInspectorSession _session;

    public WebSceneV8Transport(WebSceneV8Runtime runtime)
    {
        _session = runtime.CreateInspectorSession();
        _session.ProtocolMessage += OnProtocolMessage;
    }

    public override ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        // Dispatch the exact browser payload to v8_inspector::V8InspectorSession.
        _session.DispatchProtocolMessage(message.Span);
        return ValueTask.CompletedTask;
    }

    private void OnProtocolMessage(ReadOnlySpan<byte> message)
    {
        // V8 responses and asynchronous notifications use the same raw channel.
        // A false result means the bounded queue is full; WebScene should close or
        // pause the native session rather than allocating an unbounded backlog.
        if (!TryPublish(message))
            _session.RequestClose("DevTools client is not consuming messages");
    }

    public override async ValueTask DisposeAsync()
    {
        _session.ProtocolMessage -= OnProtocolMessage;
        _session.Dispose();
        await base.DisposeAsync();
    }
}
```

Register the target and start the host explicitly:

```csharp
var targets = new RawCdpTargetRegistry();
targets.Register(new WebSceneInspectorTarget(runtime));

var options = new CdpInspectorServerOptions
{
    Enabled = true,                 // disabled by default
    Port = 9229,
    AccessToken = configuredToken, // random 256-bit token is generated when omitted
    MaxConcurrentSessions = 4,
    MaxMessageBytes = 16 * 1024 * 1024
};

var server = new CdpInspectorServer(
    targets,
    new CdpInspectorVersionInfo(
        Browser: $"WebScene/{webSceneVersion}",
        ProtocolVersion: "1.3",
        UserAgent: webSceneUserAgent,
        V8Version: nativeV8Version),
    options);

await server.StartAsync();
```

The host exposes:

- `GET /json/version`
- `GET /json`
- `GET /json/list`
- `WS /devtools/page/{targetId}?token=...`

Chrome's discovery polling cannot add a token, so HTTP discovery is available without authentication by default and returns token-bearing WebSocket URLs. Set `RequireAuthenticationForDiscovery = true` for non-Chrome clients that can authenticate discovery requests. WebSocket authentication always remains mandatory and also accepts `Authorization: Bearer ...`. Target ids, titles, URLs, browser version, and V8 version come from WebScene rather than hardcoded Chrome values.

## Native V8 requirements

The CDP host does not replace V8 Inspector. WebScene's dedicated-isolate lane now owns the following native lifecycle:

1. Create one `v8_inspector::V8Inspector` for the isolate and an appropriate `V8InspectorClient`.
2. Call `contextCreated` and `contextDestroyed` for every debug context, using stable context-group ids.
3. Create a `V8InspectorSession` for each accepted raw CDP connection. If WebScene cannot safely support multiple sessions, configure `MaxConcurrentSessions = 1`; the host rejects additional attaches with HTTP 503.
4. Forward each browser payload to `V8InspectorSession::dispatchProtocolMessage` unchanged.
5. Forward both `sendResponse` and `sendNotification` callbacks unchanged through `TryPublish`/`PublishAsync`.
6. Implement pause-loop scheduling (`runMessageLoopOnPause` and `quitMessageLoopOnPause`) without blocking WebScene's UI/render thread.
7. Dispose the Inspector session when the WebSocket closes and detach all sessions before destroying the isolate.

The opt-in shared-isolate lane deliberately reports Inspector unavailable: its
independent engine workers cannot safely own one isolate-level pause loop.

Do not implement managed substitutes for V8 `Runtime`, `Debugger`, `Console`, `Profiler`, or `HeapProfiler`. Unsupported browser-oriented methods should reach V8 and return its normal method-not-found response without terminating the connection.

## Debugging original React/TypeScript

V8 Inspector supplies generated JavaScript locations. Debugging the original React/TypeScript source additionally depends on the same source-map data a browser build uses:

- Build with external or inline source maps and preserve `sources`, `sourcesContent`, and stable source paths.
- Give evaluated scripts stable URLs with `//# sourceURL=...` and retain `//# sourceMappingURL=...`.
- Ensure source-map URLs are resolvable from DevTools. For in-memory bundles, either embed maps/data URLs or add a narrowly scoped authenticated source provider in WebScene.
- Preserve source maps in release builds intended for debugging; minification without maps only exposes generated code.
- Keep script ids and URLs stable across reloads where possible. Emit normal V8 `Debugger.scriptParsed` notifications after context recreation.

With those pieces, Chrome DevTools can set source-mapped breakpoints, inspect scopes, step through original React/TypeScript, evaluate expressions, view console output, and use V8 CPU/heap profiling. An IDE can attach only if its debugger supports the same CDP/V8 endpoint and source-map path mapping; the transport itself is IDE-neutral.

### Editing original JS/TS and other mapped languages

The Inspector Sources editor can send direct JavaScript changes to `Debugger.setScriptSource`. For an original source represented by a source map, `V8SourceMutationEngine` first uses a dependency-free mapping-preserving patch when the generated window is textually identical. Transformed or multiline edits use registered `IV8SourceRegenerator` compiler adapters, which receive the original edit, current generated script, source URLs, and current map and must return regenerated JavaScript plus a map containing the edited `sourcesContent`.

The Inspector registers `EsbuildV8SourceRegenerator` by default for `.js`, `.jsx`, `.mjs`, `.cjs`, `.ts`, `.tsx`, `.mts`, and `.cts` maps. It finds esbuild in the source project's `node_modules/.bin`, `CDP_ESBUILD_PATH`, or `PATH`, and uses the nearest `tsconfig.json` when present. A single-source map is transformed in an isolated temporary directory. A multi-source map whose first mapped source is a local entry file is rebuilt as a project bundle: the edited entry is supplied over stdin, imports resolve from the entry directory, and the workspace file is never overwritten. If esbuild orders imported modules before the entry in its new map, `V8SourceMap.RemapSourceIndex` normalizes the regenerated map so the editor's source identity, ignored-source state, and mapped locations remain coherent.

Every regenerated result still goes through `Debugger.setScriptSource` with `dryRun: true` before apply. The source map and breakpoints are updated as one operation, and a breakpoint-rebind failure rolls the script and map back. The adapter also embeds the edited source in `sourcesContent`, so subsequent breakpoint mapping and edits operate on the exact accepted revision.

Mutation planning returns an immutable preview containing the strategy, adapter name, changed source/output spans, line counts, and SHA-256 fingerprints for the original, edited, current-generated, and proposed-generated revisions. The Inspector shows the compact strategy/line summary in the editor header. After V8 accepts the dry run, the Inspector reads the script again and compares its fingerprint with the preview input; if another debugger session or runtime update changed it, the apply is cancelled as stale instead of overwriting the newer script. Compatible regenerators are attempted in registration order, so a host-specific pipeline can fail cleanly and allow a fallback adapter to handle the same source.

Bundled or framework-specific output cannot be reproduced reliably from a source map alone because maps contain positional mappings, not compiler/bundler configuration. The default esbuild adapter therefore does not claim arbitrary dependency-file edits inside an existing webpack, Vite, Rollup, Babel, or SWC bundle. Hosts can inject an `IV8SourceRegenerator` for the owning React/TypeScript pipeline or another source language. Such an adapter should rerun that pipeline with an in-memory overlay for the edited file and return the complete generated script/map. The mutation engine rejects compiler failures, incompatible maps, and maps whose `sourcesContent` does not match the requested edit.

The adapter contract is also the extension point for other source-map-producing languages such as CoffeeScript, Svelte, Vue, or Reason. XAML continues to use `ICdpMutationEngine`, since that path mutates WebScene/Avalonia UI objects rather than replacing V8-generated JavaScript. WebAssembly modules are exposed separately as read-only streamed disassembly: the Inspector consumes `Debugger.getScriptSource`, `Debugger.disassembleWasmModule`, and `Debugger.nextWasmDisassemblyChunk`, retains SourceMap/DWARF metadata from `scriptParsed`, and maps editor breakpoints, run-to-cursor, and paused frames to V8 bytecode offsets. A future Wasm compiler mutation adapter would need to produce a complete validated module and coordinate runtime replacement; source maps alone are not sufficient for that operation.

For compilers that run outside .NET, `ExternalV8SourceRegenerator` implements a versioned JSON stdin/stdout protocol. The Inspector loads only manifests explicitly named by `CDP_V8_SOURCE_REGENERATORS`; each adapter declares its executable, fixed arguments, supported extensions, working directory, and timeout. The request contains the edited source, current generated script, normalized source map, URLs, source index, and revision fingerprints. A successful response contains the complete replacement JavaScript and revision-3 map with the edited source embedded in `sourcesContent`. This supports workspace-specific CoffeeScript, Vue, Svelte, Babel, SWC, Vite, Rollup, or webpack pipelines while preserving the existing V8 validation and rollback transaction. See `docs/articles/sources-panel.md` for the manifest and wire format.

## Security and failure behavior

- The server is disabled by default and binds to `127.0.0.1` by default.
- Loopback discovery is unauthenticated by default for `chrome://inspect` compatibility; only target metadata is exposed, and every debugger WebSocket still requires its generated/configured token.
- Non-loopback binds require `AllowRemoteConnections = true` and a token at least 32 characters long.
- Tokens are compared in constant time. The host does not log tokens or protocol messages.
- The default origin policy permits missing origins and Chrome DevTools schemes; other origins require an explicit allow-list.
- Browser and runtime messages are bounded by `MaxMessageBytes`.
- Concurrent sessions are bounded by `MaxConcurrentSessions`, and `RawCdpTransportBase` bounds queued native notifications.
- Unknown targets return 404, unauthorized WebSockets (and strict discovery requests) return 401, disallowed origins return 403, excess sessions return 503, binary messages close with `InvalidMessageType`, and oversized messages close with `MessageTooBig`.

Treat enabling Inspector access as equivalent to granting code-execution access to the V8 context. Never enable it silently in production.
