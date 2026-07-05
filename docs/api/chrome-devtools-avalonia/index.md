---
title: Chrome.DevTools.Avalonia API
---

# Chrome.DevTools.Avalonia API

Source-indexed API reference for `Chrome.DevTools.Avalonia`.

Chrome DevTools Protocol (CDP) diagnostics server support for Avalonia UI.

## Package

- Package ID: `Chrome.DevTools.Avalonia`
- Source project: `src/Avalonia.Diagnostics.Cdp/Avalonia.Diagnostics.Cdp.csproj`
- Related guide: [Getting Started](/articles/getting-started)

## Notes

- This package is indexed directly from the public source files.
- Detailed member pages are unavailable because the Markdown reflection generator could not load this assembly in the current environment.
- The source index keeps the package discoverable from the docs site and links each public type back to the repository.

## Avalonia.Diagnostics.Cdp

| Type | Kind | Source |
| --- | --- | --- |
| `AutomationSelectorGenerator` | class | [`src/Avalonia.Diagnostics.Cdp/Selector/AutomationSelectorGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Selector/AutomationSelectorGenerator.cs) |
| `AvaloniaCdpTarget` | class | [`src/Avalonia.Diagnostics.Cdp/Server/CdpServer.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Server/CdpServer.cs) |
| `CdpServer` | class | [`src/Avalonia.Diagnostics.Cdp/Server/CdpServer.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Server/CdpServer.cs) |
| `CdpSession` | class | [`src/Avalonia.Diagnostics.Cdp/Server/CdpSession.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Server/CdpSession.cs) |
| `CdpTargetSession` | class | [`src/Avalonia.Diagnostics.Cdp/Server/CdpTargetSession.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Server/CdpTargetSession.cs) |
| `DomSelectorGenerator` | class | [`src/Avalonia.Diagnostics.Cdp/Selector/DomSelectorGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Selector/DomSelectorGenerator.cs) |
| `HighlightAdorner` | class | [`src/Avalonia.Diagnostics.Cdp/Highlighting/HighlightAdorner.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Highlighting/HighlightAdorner.cs) |
| `HighlightOverlayManager` | class | [`src/Avalonia.Diagnostics.Cdp/Highlighting/HighlightOverlayManager.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Highlighting/HighlightOverlayManager.cs) |
| `ISelectorGenerator` | interface | [`src/Avalonia.Diagnostics.Cdp/Selector/ISelectorGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Selector/ISelectorGenerator.cs) |
| `NodeMap` | class | [`src/Avalonia.Diagnostics.Cdp/Selector/NodeMap.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Selector/NodeMap.cs) |
| `PaintRectsAdorner` | class | [`src/Avalonia.Diagnostics.Cdp/Highlighting/PaintRectsAdorner.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Highlighting/PaintRectsAdorner.cs) |
| `PaintRectsOverlayManager` | class | [`src/Avalonia.Diagnostics.Cdp/Highlighting/PaintRectsOverlayManager.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Highlighting/PaintRectsOverlayManager.cs) |
| `SelectorEngine` | class | [`src/Avalonia.Diagnostics.Cdp/Selector/SelectorEngine.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Selector/SelectorEngine.cs) |
| `SelectorRegistry` | class | [`src/Avalonia.Diagnostics.Cdp/Selector/SelectorRegistry.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Selector/SelectorRegistry.cs) |

## Avalonia.Diagnostics.Cdp.Domains

| Type | Kind | Source |
| --- | --- | --- |
| `AccessibilityDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/AccessibilityDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/AccessibilityDomain.cs) |
| `ApplicationDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/ApplicationDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/ApplicationDomain.cs) |
| `AuditsDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/AuditsDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/AuditsDomain.cs) |
| `AutocompleteEngine` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `BrowserDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/BrowserDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/BrowserDomain.cs) |
| `CdpLogSink` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/LogDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/LogDomain.cs) |
| `CdpRuntimeDocument` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `CdpRuntimeElement` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `CdpRuntimeWindow` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `CompositeLogSink` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/LogDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/LogDomain.cs) |
| `ControlTracker` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/MemoryDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/MemoryDomain.cs) |
| `CssDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/CssDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/CssDomain.cs) |
| `DebuggerDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/DebuggerDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/DebuggerDomain.cs) |
| `DetachedControlInfo` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/MemoryDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/MemoryDomain.cs) |
| `DomDebuggerDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/DomDebuggerDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/DomDebuggerDomain.cs) |
| `DomDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/DomDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/DomDomain.cs) |
| `EmulationDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/EmulationDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/EmulationDomain.cs) |
| `InputDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/InputDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/InputDomain.cs) |
| `JintResult` | struct | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `LocalVariablesScope` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/DebuggerDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/DebuggerDomain.cs) |
| `MemoryDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/MemoryDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/MemoryDomain.cs) |
| `OverlayDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/OverlayDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/OverlayDomain.cs) |
| `PageDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/PageDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/PageDomain.cs) |
| `PerformanceDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/PerformanceDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/PerformanceDomain.cs) |
| `PlaywrightExpectFunctionMock` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `PlaywrightExpectResultMock` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `PlaywrightInjectedFunctionMock` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `PlaywrightLocatorLookupFunctionMock` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `PlaywrightLookupResultMock` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `PlaywrightPollFunctionMock` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `PlaywrightUtilityScriptMock` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `RecorderDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RecorderDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RecorderDomain.cs) |
| `ReferenceCrawler` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/MemoryDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/MemoryDomain.cs) |
| `ReferenceEdge` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/MemoryDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/MemoryDomain.cs) |
| `ReplGlobals` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `RuntimeDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `ScriptPreprocessor` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/RuntimeDomain.cs) |
| `WindowChromeDomain` | class | [`src/Avalonia.Diagnostics.Cdp/Domains/WindowChromeDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Avalonia.Diagnostics.Cdp/Domains/WindowChromeDomain.cs) |

