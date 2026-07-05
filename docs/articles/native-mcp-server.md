---
title: Native C# Model Context Protocol (MCP) Server
---

# Native C# Model Context Protocol (MCP) Server

The **CDP Inspector CLI (`cdp-cli`)** includes a built-in, native **Model Context Protocol (MCP)** stdio server. This server enables Large Language Models (LLMs) and agentic frameworks (such as Claude Desktop, Cursor, and VS Code) to interact with and control running Avalonia applications directly, without relying on external web browser wrappers or proxy servers.

---

## How It Works

The native MCP server runs as a subcommand of the CLI tool (`cdp-cli mcp`). Once started, it:
1. Establishes a persistent connection to the target Avalonia application over local CDP sockets.
2. Reads JSON-RPC 2.0 messages from standard input (`stdin`).
3. Executes the corresponding CDP actions (querying the DOM, clicking buttons, inserting text, taking screenshots).
4. Returns the result as formatted JSON-RPC messages to standard output (`stdout`).

To prevent corrupting the stdio-based communication channel, **all diagnostic messages, trace logs, warnings, and runtime errors are redirected to standard error (`stderr`)**.

---

## Installation & Setup

### 1. Prerequisite
Ensure that you have installed the `cdp-cli` tool globally:
```bash
dotnet tool install -g Chrome.DevTools.Cli
```

### 2. Claude Desktop Integration
To configure the native MCP server in **Claude Desktop**, add the following server entry to your configuration settings file (typically located at `~/Library/Application Support/Claude/claude_desktop_config.json` on macOS or `%APPDATA%\Claude\claude_desktop_config.json` on Windows):

```json
{
  "mcpServers": {
    "cdp-native-mcp": {
      "command": "cdp-cli",
      "args": [
        "mcp",
        "--port",
        "9222"
      ]
    }
  }
}
```

### 3. CLI Command Options
The `mcp` subcommand accepts the following options:
* `-p, --port <number>`: Directly binds to the specified local CDP port of the target application (defaults to `9222`).
* `-h, --host <url>`: Full target CDP host address (defaults to `http://127.0.0.1:9222`). If both `--host` and `--port` are provided, they are combined automatically.
* `-t, --target <id>`: Specifies the exact target window/page UUID to bind.
* `-n, --target-name <name>`: Matches a target window by a case-insensitive substring of its title.

---

## Mapped MCP Tools

The native C# MCP server exposes **7 core tools** directly to AI agents:

### 1. `dom_query`
Queries elements in the visual tree using stable CSS selectors.
* **Arguments**:
  * `selector` (string, required): A CSS selector target (e.g. `#btnRefreshTargets`, `[AutomationId="SubmitButton"]`, `TextBlock`).
* **Returns**: Node properties, mapped tag name, and matching element bounds.

### 2. `evaluate`
Evaluates dynamic C# expressions directly in the context of the active Avalonia window.
* **Arguments**:
  * `expression` (string, required): C# statement to execute (e.g. `Window.Title`, `Window.DataContext.Connection.IsConnected`).
* **Returns**: Evaluated value serialized as a string.

### 3. `screenshot`
Captures the visual frame of the running application window.
* **Arguments**: None.
* **Returns**: Base64-encoded PNG image content block.

### 4. `tap`
Simulates a mouse press and release action on a target control.
* **Arguments**:
  * `selector` (string, required): The target selector.
* **Returns**: Success confirmation.

### 5. `input_text`
Focuses the target element and inserts raw text.
* **Arguments**:
  * `selector` (string, required): Target selector.
  * `text` (string, required): Value to insert.
* **Returns**: Success confirmation.

### 6. `clear_text`
Clears text contents of an input field.
* **Arguments**:
  * `selector` (string, required): Target selector.
* **Returns**: Success confirmation.

### 7. `scroll`
Scrolls a scrollable container in a specified direction.
* **Arguments**:
  * `selector` (string, required): Target selector.
  * `direction` (string, required): Direction to scroll (`up`, `down`, `left`, `right`).
* **Returns**: Success confirmation.
