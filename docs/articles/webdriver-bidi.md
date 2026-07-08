# WebDriver BiDi Support

The Avalonia CDP Server includes comprehensive integration for the emerging W3C **WebDriver BiDi** (Bidirectional) protocol. This enables browser-automation and testing frameworks (such as Selenium, Puppeteer, and Playwright) to inspect and control Avalonia applications using standard bidirectional protocols.

---

## Session Architecture and Routing

WebDriver BiDi requires a handshake and route negotiation mechanism to establish a bidirectional session. The server provides both REST HTTP endpoints and WebSocket routing:

### 1. HTTP Session Negotiation (`/session`)
Testing frameworks initiate a connection by sending a standard HTTP `POST` request to `/session`. The server responds with the capabilities and provides the WebSocket URL:

*   **Endpoint**: `POST http://127.0.0.1:9222/session`
*   **Response Payload**:
    ```json
    {
      "value": {
        "sessionId": "bidi-session-uuid",
        "capabilities": {
          "webSocketUrl": "ws://127.0.0.1:9222/session/bidi/bidi-session-uuid"
        }
      }
    }
    ```

### 2. WebSocket Routing (`/session/bidi`)
Once the capabilities are negotiated, the client connects to the WebSocket endpoint.
*   **Routes Supported**:
    *   `/session/bidi` (defaults to creating a new session or re-routing to the last active one)
    *   `/session/bidi/{sessionId}` (connects to or resumes a specific session)

Inside the server, `CdpServer` intercepts WebSocket connections matching these routes and hands communication over to `BiDiSession`.

---

## Script Execution and Deep Serialization

WebDriver BiDi provides powerful script execution via the `script.evaluate` method. This allows client agents to execute custom C# scripts in the Avalonia app context.

### Remote Value Serialization

To transfer complex objects over JSON safely without causing serialization loops, the protocol implements a **Deep Serialization** model. The `script.evaluate` method leverages CDP's deep serialization and maps the results to WebDriver BiDi Remote Value formats:

1.  **Primitives**: Maps simple types (`string`, `number`, `boolean`, `null`, `undefined`) to their respective BiDi shapes:
    ```json
    { "type": "string", "value": "Hello Avalonia" }
    ```
2.  **Arrays**: Recursively serializes all elements and returns them in an array envelope:
    ```json
    {
      "type": "array",
      "value": [
        { "type": "number", "value": 42 },
        { "type": "string", "value": "item" }
      ]
    }
    ```
3.  **Objects**: Maps C# properties or dictionary key-value pairs into arrays of name-value tuples:
    ```json
    {
      "type": "object",
      "value": [
        ["name", { "type": "string", "value": "btnRefresh" }],
        ["isWritable", { "type": "boolean", "value": true }]
      ]
    }
    ```

Clients can control serialization depth by passing `serializationOptions` with a custom `maxDepth` (defaults to `3`).

---

## Input Simulation (Pointer & Key Actions)

BiDi inputs are managed via the `input.performActions` method. This allows client agents to dispatch complex, multi-modal input sequences (such as drag-and-drop or typing with modifier keys) to the application.

The protocol maps incoming action chains directly to CDP's `Input` domain:

### 1. Pointer Actions (`pointer` source type)
*   **`pointerMove`**: Updates coordinate registers (`lastX`, `lastY`) and dispatches a CDP `mouseMoved` event.
*   **`pointerDown`**: Simulates clicking. Maps the standard BiDi button index (0 = Left, 1 = Middle, 2 = Right) to CDP mouse button names (`left`, `middle`, `right`) and dispatches a CDP `mousePressed` event.
*   **`pointerUp`**: Maps the button index and coordinates, dispatching a CDP `mouseReleased` event.

### 2. Key Actions (`key` source type)
*   **`keyDown`**: Sends a CDP `rawKeyDown` event for the specified key value. If the key represents a single character, it also automatically dispatches an `insertText` input command to enter the character into the active text box.
*   **`keyUp`**: Sends a CDP `keyUp` event to release the key.

---

## Event Subscription

WebDriver BiDi features a subscribe-unsubscribe model (`session.subscribe` and `session.unsubscribe`), allowing client agents to listen to specific protocol events.

### Network Event Mapping

The server bridges standard CDP events to W3C BiDi events. For example, subscribing to `network.beforeRequestSent` registers an event listener on the CDP `Network` domain:

1.  The target application triggers a network request.
2.  The CDP `Network.requestWillBeSent` event fires.
3.  `BiDiSession` intercepts the event in `OnNetworkEventBroadcasted`.
4.  The request headers, URL, method, and cookies are mapped to the W3C WebDriver BiDi network payload format:
    ```json
    {
      "type": "event",
      "method": "network.beforeRequestSent",
      "params": {
        "context": "main",
        "navigation": null,
        "redirectCount": 0,
        "request": {
          "request": "request-id-123",
          "url": "http://example.com/api/data",
          "method": "GET",
          "headers": [
            { "name": "Accept", "value": "application/json" }
          ],
          "cookies": [],
          "headersSize": 0,
          "bodySize": 0,
          "timings": {}
        },
        "timestamp": 1718873042000,
        "initiator": { "type": "other" }
      }
    }
    ```
5.  The mapped event is transmitted asynchronously over the client's WebSocket connection.
