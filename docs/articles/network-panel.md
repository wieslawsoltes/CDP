---
title: Network Panel
description: Technical guide to HTTP traffic inspection, throttling profiles, waterfall charts, body previews, and request mocking in the CDP Inspector for Avalonia.
---

# Network Panel

The **Network Panel** provides a comprehensive traffic inspector that logs outbound HTTP operations initiated by `HttpClient` instances inside the running Avalonia application. It functions similarly to browser network inspector consoles, enabling developers to review APIs, mock responses, simulate slow connections, and audit payloads.

---

## 1. Network Requests Log Grid

The primary view lists captured network operations in `lstNetworkRequests`:
- **Name / URL**: The request path. The grid displays relative or full URL paths for API endpoints.
- **Method**: The HTTP method in a distinct yellow font (e.g., `GET`, `POST`, `PUT`, `DELETE`).
- **Status**: The HTTP response status code in green (e.g., `200 OK`, `201 Created`) or red if failed (e.g., `404 Not Found`, `500 Internal Server Error`).
- **Type**: Content classification, such as `xhr`, `js`, `css`, `image`, `json`, or `document`.
- **Time**: The overall latency or duration taken to resolve the network exchange.
- **Waterfall**: A graphic line representing request timeline progression.

---

## 2. Category Filtering and Throttling

To manage noise in busy communication sessions, the panel includes filtering and throttling tools.

### Category Filters ListBox
A tab-like list control allows filtering traffic down to specific mime-types:
- **All**: Displays all recorded network requests.
- **Fetch/XHR**: Isolates background API endpoints.
- **JS / CSS**: Filters script and stylesheet files.
- **Img / Media**: Displays images, video, and audio assets.
- **Doc / Font**: Filters HTML files and custom typography assets.

### Network Throttling Profiles (`cmbThrottling`)
A dropdown menu simulates network connectivity conditions by throttling socket traffic in the target application:
- **No Throttling**: Full network performance.
- **Fast 3G**: Simulates high-latency cellular connections (e.g. 100ms latency, 1.5 Mbps down).
- **Slow 3G**: Simulates low-bandwidth mobile networks (e.g. 400ms latency, 500 Kbps down) for debugging timeout resilience.
- **Offline**: Disconnects socket bindings, returning local mock failures instantly.

---

## 3. Waterfall Timing Watermarks (`WaterfallBar`)

The waterfall column displays timeline watermarks for each HTTP request using a custom `WaterfallBar` control:
- **Start Offset Percent**: The time delta between the session initialization and the start of the request, mapping the node's position horizontally.
- **Time to First Byte (TTFB) Percent**: The interval between sending the request headers and receiving the first byte of response data (represented by a dark orange block).
- **Download Duration Percent**: The time spent reading the response content from the socket stream (represented by a light green block).
- **Tooltip Telemetry**: Hovering over the waterfall bar displays a detailed timing table:
  - Queueing delay
  - Connection handshake duration
  - TTFB latency
  - Content download time

---

## 4. Request and Response Details Drawer

Selecting an HTTP item in the request log opens the details tab control in the right-hand panel.

### Headers Tab
Displays raw string representation of request and response headers:
- **Request Headers**: Shows context tokens like `User-Agent`, `Authorization` bear tokens, `Accept`, and custom user parameters.
- **Response Headers**: Shows server variables like `Content-Type`, `Server`, `Date`, and cache controls.

### Payload Tab
Deconstructs HTTP parameters for inspection:
- **Query Parameters**: Lists key-value items parsed from the query string (e.g., `?id=10&format=json`).
- **Post Parameters**: If the request method is `POST` or `PUT`, lists parameters parsed from form-data or JSON body nodes.

### Preview Tab
Renders the response body according to its content-type:
- **JSON Tree Viewer (`HierarchicalJsonTree`)**: Renders JSON responses in an expandable tree view, formatting objects, arrays, keys, and values.
- **Image Preview**: If the response is a visual file, it displays the image in an `Image` control.
- **Raw Text Box (`txtNetBody`)**: Fallback for unformatted files, showing raw logs or text buffers.

---

## 5. Network Mocking Rules Engine

The Network panel includes a mocking interface that intercepts outbound requests and serves custom responses locally:
- **Add Mock Rule**: Clicking `btnAddMockRule` adds an item to `lstMockRules`.
- **URL Pattern**: Defines match parameters using wildcard patterns (e.g., `*/api/users/*` or `*https://example.com/status*`).
- **Status Code**: Overrides server responses with a custom code (e.g., simulating a `503 Service Unavailable` error).
- **Mock Body**: Accepts custom text or JSON payloads to return to the `HttpClient` caller.
- **Active State Checkbox**: Quickly toggles individual rules on or off without deleting their definition structures.

---

## 6. CDP Protocol Implementation Mappings

Outbound network tracking is accomplished by hooking network socket interceptors inside `HttpClient` and emitting CDP events:

### Enabling Interception
```json
{
  "id": 1,
  "method": "Network.enable"
}
```

### Emitting Request Sent Event
```json
{
  "method": "Network.requestWillBeSent",
  "params": {
    "requestId": "req-101",
    "loaderId": "loader-default",
    "documentURL": "http://127.0.0.1:9222",
    "request": {
      "url": "https://api.example.com/data",
      "method": "POST",
      "headers": {
        "Content-Type": "application/json"
      },
      "postData": "{\"id\":42}"
    },
    "timestamp": 123456.789,
    "type": "XHR"
  }
}
```

### Emitting Response Received Event
```json
{
  "method": "Network.responseReceived",
  "params": {
    "requestId": "req-101",
    "timestamp": 123457.100,
    "type": "XHR",
    "response": {
      "url": "https://api.example.com/data",
      "status": 200,
      "statusText": "OK",
      "headers": {
        "Content-Type": "application/json"
      },
      "mimeType": "application/json"
    }
  }
}
```

By standardizing outbound desktop HTTP profiling to this API scheme, developers can analyze data integration issues using a familiar, unified diagnostic framework.
