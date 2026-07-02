---
title: Network Domain
---

# Network Domain in CDP Avalonia

The Chrome DevTools Protocol (CDP) **Network Domain** in `Avalonia.Diagnostics.Cdp` allows external automation agents and test tools to monitor, inspect, and emulate network activity inside the target Avalonia application. 

By exposing standard CDP events and methods, the network domain gives tools a deep look into outbound HTTP traffic, request/response headers, raw payloads, and transport telemetry. It is particularly valuable for integration testing, diagnostic recording, and offline/throttled network condition testing.

---

## Core Architecture & Network Interception

To capture outgoing and incoming HTTP traffic, the Avalonia CDP implementation utilizes two distinct mechanisms:

1. **Transparent Passive Monitoring**: Leverages the C# native `DiagnosticListener` framework to tap into the `HttpHandlerDiagnosticListener` source. This monitors all standard `HttpClient` requests and responses transparently across the application without modifying any pipelines.
2. **Active Emulation and Pipeline Interception**: Leverages a custom `CdpDelegatingHandler` registered in the application's `HttpClient` message handler chain. This actively intercepts traffic to enforce offline modes, inject latency, block specific URLs, and pause requests for external interception.

**Network Interception Sequence**

Participants: **Application Code**, **HttpClient / CdpDelegatingHandler**, **HttpKeyValueObserver (DiagnosticListener)**, **NetworkDomain (CDP)**, **Destination Server**

1. Application Code → HttpClient: `SendAsync(Request)`
2. HttpClient checks Offline & Latency settings
3. HttpClient checks `setBlockedURLs` rules
4. HttpClient → HttpKeyValueObserver: `System.Net.Http.HttpRequestOut.Start`
5. HttpKeyValueObserver → NetworkDomain: `OnRequestStart()`
6. NetworkDomain broadcasts `Network.requestWillBeSent` event
7. HttpClient → Destination Server: Send Request over network
8. Destination Server → HttpClient: Receive Response (Headers)
9. HttpClient → HttpKeyValueObserver: `System.Net.Http.HttpRequestOut.Stop`
10. HttpKeyValueObserver → NetworkDomain: `OnRequestStop()`
11. NetworkDomain broadcasts `Network.responseReceived` event
12. NetworkDomain wraps content in `InterceptingHttpContent`
13. HttpClient → Application Code: Return Response Stream
14. Application Code → HttpClient: Read Stream / `CopyToAsync`
15. HttpClient applies Bandwidth Throttling
16. HttpClient buffers response (Max 5MB)
17. Application Code finishes reading
18. HttpClient caches Response Body & broadcasts `Network.loadingFinished`

---

## Key Methods and Operations

The Network Domain implements a set of core methods from the Chrome DevTools Protocol specification:

| Method | Description |
| :--- | :--- |
| `Network.enable` | Activates network monitoring, diagnostic observer subscriptions, and hooks into `HttpClient`. |
| `Network.disable` | Deactivates monitoring, disposes event observers, and resets all network emulation parameters. |
| `Network.getResponseBody` | Retrieves the cached response body (text or Base64 encoded binary) for a specific completed `requestId`. |
| `Network.emulateNetworkConditions` | Simulates offline states, custom latency, and maximum download/upload throughput speeds. |
| `Network.setBlockedURLs` | Takes an array of wildcard URL patterns and rejects requests matching any pattern. |

---

## Passive Monitoring via `DiagnosticListener`

When `Network.enable` is invoked, the domain registers a `NetworkDiagnosticObserver` onto `DiagnosticListener.AllListeners`. This observer automatically listens for events from `HttpHandlerDiagnosticListener`.

When an HTTP request is made:
* **Request Start**: The diagnostic key `System.Net.Http.HttpRequestOut.Start` is intercepted. The `HttpKeyValueObserver` extracts the `HttpRequestMessage`, assigns it a unique `requestId` (e.g., `req-1`), maps its headers, and broadcasts a `Network.requestWillBeSent` event to all connected sessions.
* **Request Stop**: The diagnostic key `System.Net.Http.HttpRequestOut.Stop` is intercepted. The observer extracts the `HttpRequestMessage` and `HttpResponseMessage`, maps the response headers and status codes, and broadcasts a `Network.responseReceived` event.

---

## Active Interception via `CdpDelegatingHandler`

Active interception is performed using `CdpDelegatingHandler`, a subclass of `DelegatingHandler`. When registered in the application's `HttpClient` setup, it intercepts every request and applies emulation rules set via CDP:

* **Offline Checks**: If `NetworkDomain.Offline` is true, the handler immediately throws an `HttpRequestException` saying `Network is offline (emulated by CDP).`, preventing the request from reaching the wire.
* **Latency Injection**: If `NetworkDomain.Latency` is configured to a non-zero value, the handler asynchronously delays request transmission using `Task.Delay()`.
* **URL Blocking**: The handler intercepts the target URI and checks it using wildcard pattern-matching in `NetworkDomain.IsBlocked()`. If the URL matches a blocked pattern, an `HttpRequestException` is thrown to simulate a blocked network call.

---

## Response Body Capture and Caching

To allow external agents to inspect response payloads (such as API JSON responses, images, or configuration payloads) via `Network.getResponseBody`, the CDP server intercepts HTTP response streams.

### `InterceptingHttpContent` and `TrackingStream`
When an HTTP response is intercepted in `OnRequestStop`, its `HttpContent` is wrapped inside a custom `InterceptingHttpContent` class. This wrapper clones headers and replaces the read stream with a `TrackingStream`.

As the client application reads from the response stream (e.g., through methods like `ReadAsync` or `CopyToAsync`), the `TrackingStream`:
1. Passes the raw bytes to the underlying stream so the application functions normally.
2. Directs a copy of the read bytes into a memory buffer (`_buffer`) within the `InterceptingHttpContent` object.
3. Automatically applies download bandwidth throttling, injecting millisecond delays if throttling throughput limit is active.
4. Detects when the stream is fully read, triggers `OnComplete()`, and saves the buffered content into the `NetworkDomain` response cache.

### Text vs. Binary Detection
When caching the response body in `OnComplete()`, the content type media header determines how the payload is represented:
* **Binary Payloads**: If the content type begins with `image/`, `audio/`, `video/`, or matches `application/octet-stream`, the bytes are converted to a Base64-encoded string, and cached with the `Base64Encoded` flag set to `true`.
* **Text Payloads**: Otherwise, the bytes are decoded as a UTF-8 string and cached with the `Base64Encoded` flag set to `false`.

---

## Smart Caching & Stream Detection

To prevent application crashes, memory leaks, and request hangs when handling large payloads or infinite server-sent streams, `NetworkDomain` implements smart cache protection policies.

### 1. Size Limit Protection
A hard safety threshold is set in the `InterceptingHttpContent`:
```csharp
private const int MaxCaptureLength = 5 * 1024 * 1024; // 5 MB
```
If the read payload exceeds 5 MB during stream traversal, the `_limitExceeded` flag is set to `true`, and the internal buffer is immediately cleared to release memory. The request payload will not be cached, and subsequent calls to `Network.getResponseBody` for this request ID will return an error indicating that the cache limit was exceeded.

### 2. Stream Bypass Detection
If an HTTP request returns a continuous stream, waiting for stream completion would hang the request or buffer bytes infinitely. `NetworkDomain` automatically checks the media type of the response headers via `IsStreamingMediaType()`:

```csharp
private static bool IsStreamingMediaType(string mediaType)
{
    return mediaType switch
    {
        "text/event-stream" => true,          // Server-Sent Events (SSE)
        "application/x-ndjson" => true,       // Newline Delimited JSON
        "application/json-seq" => true,      // JSON Text Sequences
        "application/grpc" => true,           // gRPC Streams
        "application/grpc+proto" => true,
        "multipart/x-mixed-replace" => true,  // Server Push Video Streams
        "application/octet-stream" => true,   // Generic Binary Streams
        _ => false
    };
}
```
If a streaming content type is matched, the content wrapper sets `_limitExceeded = true` immediately. The stream bypasses buffering entirely and behaves as a pass-through stream, protecting the inspector from running out of memory.

---

## Network Emulation & Throttling

The domain allows testing application behavior under realistic network conditions using `Network.emulateNetworkConditions`.

### Latency Injection
Injects a static delay before sending requests. This is executed in the `SendAsync` loop of `CdpDelegatingHandler` or inside `OnRequestStart` using `Thread.Sleep`.

### Bandwidth Throttling
Simulates slow download bandwidth rates. Whenever `TrackingStream` reads a chunk of bytes, it calls the throttling helper:

```csharp
internal static void ApplyDownloadThrottling(int bytesRead)
{
    if (_downloadThroughput > 0 && bytesRead > 0)
    {
        double delayMs = (bytesRead * 1000.0) / _downloadThroughput;
        if (delayMs > 1)
        {
            System.Threading.Thread.Sleep((int)delayMs);
        }
    }
}
```
This inserts a brief, proportional delay into each read operation, restricting the overall transfer rate to the requested bytes-per-second limit.

---

## Test Studio & Telemetry Integration

The Network Domain works in tandem with the Test Studio automation subsystem. A specialized observer class, `NetworkTelemetryProvider`, collects network events generated during automated test runs.

* **Waterfall Reporting**: Tracks request start and finish timestamps relative to step execution. It records duration, status code, method, and bytes transferred.
* **HTML/PDF Reports**: During report generation, `NetworkTelemetryProvider` processes this data to inject a visual network waterfall diagram and metrics table directly into test reports, using SkiaSharp to draw timelines.

---

## JSON-RPC Protocol Examples

Below are standard JSON payloads for communicating with the Network Domain.

### 1. Enable Network Monitoring
**Request:**
```json
{
  "id": 200,
  "method": "Network.enable",
  "params": {}
}
```
**Response:**
```json
{
  "id": 200,
  "result": {}
}
```

### 2. Network Event: Request Will Be Sent
Fires when an outbound HTTP request is initiated.

**Event Payload:**
```json
{
  "method": "Network.requestWillBeSent",
  "params": {
    "requestId": "req-42",
    "loaderId": "loader-1",
    "documentURL": "http://localhost:9222/",
    "request": {
      "url": "https://api.github.com/repos/wieslawsoltes/CDP",
      "method": "GET",
      "headers": {
        "User-Agent": "Avalonia-Cdp-Client/1.0",
        "Accept": "application/json"
      },
      "initialPriority": "Medium",
      "referrerPolicy": "no-referrer-when-downgrade"
    },
    "timestamp": 1782384592.102,
    "wallTime": 1782384592,
    "initiator": {
      "type": "other"
    },
    "type": "XHR"
  }
}
```

### 3. Network Event: Response Received
Fires when headers are returned from the remote endpoint.

**Event Payload:**
```json
{
  "method": "Network.responseReceived",
  "params": {
    "requestId": "req-42",
    "loaderId": "loader-1",
    "timestamp": 1782384592.512,
    "type": "XHR",
    "response": {
      "url": "https://api.github.com/repos/wieslawsoltes/CDP",
      "status": 200,
      "statusText": "OK",
      "headers": {
        "Content-Type": "application/json; charset=utf-8",
        "Server": "GitHub.com",
        "Content-Length": "1048"
      },
      "mimeType": "application/json",
      "connectionReused": true,
      "connectionId": 0,
      "encodedDataLength": 1048,
      "securityState": "secure"
    }
  }
}
```

### 4. Network Event: Loading Finished
Fires after the application completes reading the response body content.

**Event Payload:**
```json
{
  "method": "Network.loadingFinished",
  "params": {
    "requestId": "req-42",
    "timestamp": 1782384592.650,
    "encodedDataLength": 1048
  }
}
```

### 5. Get Response Body (Text Payload)
**Request:**
```json
{
  "id": 201,
  "method": "Network.getResponseBody",
  "params": {
    "requestId": "req-42"
  }
}
```
**Response:**
```json
{
  "id": 201,
  "result": {
    "body": "{\"id\":12345,\"name\":\"CDP\",\"full_name\":\"wieslawsoltes/CDP\"}",
    "base64Encoded": false
  }
}
```

### 6. Emulate Network Conditions
Simulates a offline state or slow connection.

**Request:**
```json
{
  "id": 202,
  "method": "Network.emulateNetworkConditions",
  "params": {
    "offline": false,
    "latency": 150.0,
    "downloadThroughput": 512000.0,
    "uploadThroughput": 256000.0
  }
}
```
**Response:**
```json
{
  "id": 202,
  "result": {}
}
```

### 7. Set Blocked URLs
Configures the HTTP client to reject matching domains.

**Request:**
```json
{
  "id": 203,
  "method": "Network.setBlockedURLs",
  "params": {
    "urls": [
      "*google-analytics.com*",
      "*.doubleclick.net/*"
    ]
  }
}
```
**Response:**
```json
{
  "id": 203,
  "result": {}
}
```

---

## Reference Files

For implementation details, view the source code of the network instrumentation sub-system:

* `NetworkDomain.cs` - Main domain handler and event broadcaster.
* `CdpDelegatingHandler.cs` - Intercepts network pipelines for throttling and blocking.
* `NetworkTelemetryProvider.cs` - Integrates network event timelines into Test Studio reports.
