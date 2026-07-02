---
title: Minimap Editor
---

# Minimap Editor

The Minimap Editor (`Chrome.DevTools.Editor.Minimap`) is a standalone Avalonia control that extends AvaloniaEdit with a Visual Studio-style minimap sidebar. It provides a bird's-eye view of the entire document alongside the main text editor, enabling rapid navigation through large files.

## Overview

The `MinimapTextEditor` control combines:
- A full-featured AvaloniaEdit `TextEditor` with syntax highlighting and line numbers
- A compact, scaled-down minimap rendered alongside the editor
- A viewport indicator showing the currently visible region
- Click-to-navigate and drag-to-scroll minimap interaction

## Installation

```bash
dotnet add package Chrome.DevTools.Editor.Minimap --prerelease
```

## Usage

### XAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:minimap="clr-namespace:CDP.Editor.Minimap;assembly=CDP.Editor.Minimap">

    <minimap:MinimapTextEditor
        Name="editor"
        ShowLineNumbers="True"
        FontFamily="Cascadia Code, JetBrains Mono, Consolas, monospace"
        FontSize="13"
        MinimapWidth="100"
        MinimapFontSize="1.5"/>
</Window>
```

### C# Code-Behind

```csharp
var editor = new MinimapTextEditor
{
    ShowLineNumbers = true,
    FontFamily = new FontFamily("Cascadia Code"),
    FontSize = 13,
    MinimapWidth = 100
};

editor.Text = File.ReadAllText("Program.cs");
```

## Key Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MinimapWidth` | double | 100 | Width of the minimap sidebar in pixels |
| `MinimapFontSize` | double | 1.5 | Font size for minimap text rendering |
| `ShowLineNumbers` | bool | false | Show line numbers in the main editor |
| `Text` | string | "" | The document text content |
| `SyntaxHighlighting` | IHighlightingDefinition | null | TextMate syntax highlighting definition |
| `IsReadOnly` | bool | false | Whether the editor is read-only |

## Minimap Interaction

### Click to Navigate

Clicking on the minimap scrolls the main editor to the corresponding position. The viewport indicator updates to show the new visible region.

### Drag to Scroll

Press and drag on the minimap to smoothly scroll through the document. The viewport indicator follows the cursor position.

### Viewport Indicator

A semi-transparent overlay on the minimap highlights the currently visible region of the document. The indicator height corresponds to the number of visible lines relative to the total document length.

## Architecture

The minimap is rendered using a scaled-down version of the AvaloniaEdit rendering pipeline:

```
┌─────────────────────────────────┬──────────┐
│                                 │ Minimap  │
│   Main TextEditor               │          │
│                                 │ ┌──────┐ │
│   Line 1: using System;        │ │Viewport│ │
│   Line 2: using System.IO;     │ │Indicator││
│   Line 3:                       │ └──────┘ │
│   Line 4: namespace App;       │          │
│   ...                           │          │
│                                 │          │
└─────────────────────────────────┴──────────┘
```

## Usage in the Inspector

The CDP Inspector uses `MinimapTextEditor` in the Sources panel for viewing and editing source files. It provides:
- Syntax highlighting for C#, XAML, JSON, and other file types
- Read-write editing with save support
- Minimap navigation for quick file exploration

## Dependencies

- `AvaloniaEdit` — Core text editor control
- `Avalonia` — UI framework
- No CDP or protocol dependency — this is a standalone editor control

## Next Steps

- [Sources Panel](/articles/sources-panel) — How the Inspector uses the minimap editor
- [Node Editor](/articles/node-editor) — Graph node visualization control
- [Splits Layout](/articles/splits-layout) — Dynamic split pane container
