---
title: API Reference
---

# API Reference

The CDP project ships a modular managed surface area across protocol, inspection, OS automation, and editor control packages. This section keeps the article-led docs intact and adds generated reference pages for the public managed APIs that ship from `src/`.

Use the guide articles for workflows and architecture. Use this reference when you need exact type and member contracts.

## Coverage

- Managed packages covered: 9
- Source links in generated pages point back to the repository paths on GitHub.

## Package Groups

### Core Protocol

| Package | Description | Related guide |
| --- | --- | --- |
| [`Chrome.DevTools.Protocol`](/api/chrome-devtools-protocol/) | Chrome DevTools Protocol (CDP) core server and session logic. | [Architecture](/articles/architecture) |
| [`Chrome.DevTools.Avalonia`](/api/chrome-devtools-avalonia/) | Chrome DevTools Protocol (CDP) diagnostics server support for Avalonia UI. | [Getting Started](/articles/getting-started) |

### Inspector And Diagnostics

| Package | Description | Related guide |
| --- | --- | --- |
| [`Chrome.DevTools.Inspector.Shared`](/api/chrome-devtools-inspector-shared/) | Shared UI components and view models for the CDP Inspector client. | [Inspector App](/articles/inspector-app) |
| [`Chrome.DevTools.DiagnosticTools`](/api/chrome-devtools-diagnostictools/) | In-process CDP diagnostic inspector tool integration for Avalonia UI applications. | [In-Process Inspector](/articles/in-process-inspector) |

### OS Automation

| Package | Description | Related guide |
| --- | --- | --- |
| [`Chrome.DevTools.Automation.OS`](/api/chrome-devtools-automation-os/) | Cross-platform operating system automation and accessibility provider for Chrome DevTools Protocol (CDP). | [OS Automation](/articles/os-automation) |

### Editor Controls

| Package | Description | Related guide |
| --- | --- | --- |
| [`Chrome.DevTools.Editor.Minimap`](/api/chrome-devtools-editor-minimap/) | Standalone Minimap and Inline Editor Extensions for AvaloniaEdit. | [Minimap Editor](/articles/minimap-editor) |
| [`Chrome.DevTools.Editor.Nodes`](/api/chrome-devtools-editor-nodes/) | Standalone, generic graph node editor control for Avalonia UI. | [Node Editor](/articles/node-editor) |
| [`Chrome.DevTools.Editor.Nodes.Msagl`](/api/chrome-devtools-editor-nodes-msagl/) | Microsoft MSAGL layout integration for standalone graph node editor. | [Node Editor](/articles/node-editor) |
| [`Chrome.DevTools.Editor.Splits`](/api/chrome-devtools-editor-splits/) | Dynamic binary split layout container for Avalonia applications. | [Splits Layout](/articles/splits-layout) |

## Reference Notes

- Some packages may temporarily fall back to source-indexed reference pages when the reflection generator cannot load the built assembly in the current environment.
