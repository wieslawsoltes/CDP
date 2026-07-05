---
title: Google Model Context Protocol (MCP) Integration
---

# Google Model Context Protocol (MCP) Integration

The Avalonia CDP Server implements a high-fidelity Chrome DevTools Protocol (CDP) server. This makes it compatible with browser-like AI agent protocols such as the **Model Context Protocol (MCP)**. 

Through this integration, AI agents can inspect the visual tree, click buttons, fill text boxes, and debug layout or accessibility issues within a running Avalonia application exactly as they would on a standard web page.

---

## 1. Model Context Protocol (MCP) Setup

Google Chrome's official **Chrome DevTools MCP Server** connects Large Language Models (LLMs) directly to browser debugging protocols. Since the Avalonia CDP server is 100% compliant with standard CDP commands, you can point Chrome DevTools MCP tools directly at your desktop application.

*   **Official MCP Repo:** [ChromeDevTools/chrome-devtools-mcp](https://github.com/ChromeDevTools/chrome-devtools-mcp)
*   **Getting Started Guide:** [Chrome DevTools for Agents](https://developer.chrome.com/docs/devtools/agents/get-started)

### Configuration for Claude Desktop / Cursor / VS Code
Add the following configuration to your MCP settings file (e.g., `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "chrome-devtools": {
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/server-chrome-devtools",
        "--port",
        "9222"
      ]
    }
  }
}
```

---

## 2. Available MCP Tools

Once configured, the AI agent gains access to the following tools:

### inspect_dom
Queries and navigates the visual tree of the desktop application using standard CSS selectors. It queries the visual tree structure and returns attributes, node structure, and tag names.

### evaluate_js
Runs expressions and reflections inside the Avalonia runtime environment. It allows executing C# evaluator expressions safely.

### capture_screenshot
Renders and captures a real-time PNG/JPEG visual frame of the active Avalonia window. Useful for verifying UI state changes or layout issues visually.

### simulate_input
Triggers mouse clicks, pointer movements, and raw keyboard entries on specific elements using their mapped viewport coordinates.

---

## 3. Useful Links & Reference Material

*   [Chrome DevTools Protocol Specification](https://chromedevtools.github.io/devtools-protocol/)
*   [Model Context Protocol (MCP) Specification](https://modelcontextprotocol.io/)
*   [Vitepress Guide - AI Agent Integration](/articles/ai-agent-integration)
*   [Vitepress Guide - CSS Selector Engine](/articles/selector-engine)
