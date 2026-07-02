---
title: Splits Layout
---

# Splits Layout

The Splits Layout (`Chrome.DevTools.Editor.Splits`) is a standalone Avalonia control that provides a dynamic, user-resizable split pane container. It supports horizontal and vertical splits, nested split hierarchies, and drag-to-resize behavior.

## Overview

The `SuperSplit` control enables:
- Horizontal and vertical split panes
- Drag-to-resize splitter bars
- Nested split hierarchies (splits within splits)
- Proportional sizing with configurable ratios
- Minimum size constraints per pane
- Programmatic split manipulation

## Installation

```bash
dotnet add package Chrome.DevTools.Editor.Splits --prerelease
```

## Usage

### XAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:splits="clr-namespace:CDP.Editor.Splits;assembly=CDP.Editor.Splits">

    <splits:SuperSplit Orientation="Horizontal">
        <splits:SuperSplitItem Weight="0.3">
            <TextBlock Text="Left Panel" />
        </splits:SuperSplitItem>
        <splits:SuperSplitItem Weight="0.7">
            <TextBlock Text="Right Panel" />
        </splits:SuperSplitItem>
    </splits:SuperSplit>
</Window>
```

### Nested Splits

```xml
<splits:SuperSplit Orientation="Horizontal">
    <splits:SuperSplitItem Weight="0.25">
        <TextBlock Text="Sidebar" />
    </splits:SuperSplitItem>
    <splits:SuperSplitItem Weight="0.75">
        <splits:SuperSplit Orientation="Vertical">
            <splits:SuperSplitItem Weight="0.6">
                <TextBlock Text="Main Content" />
            </splits:SuperSplitItem>
            <splits:SuperSplitItem Weight="0.4">
                <TextBlock Text="Bottom Panel" />
            </splits:SuperSplitItem>
        </splits:SuperSplit>
    </splits:SuperSplitItem>
</splits:SuperSplit>
```

### C# Code

```csharp
var split = new SuperSplit
{
    Orientation = Orientation.Horizontal
};

split.Items.Add(new SuperSplitItem
{
    Weight = 0.3,
    Content = new TextBlock { Text = "Left" }
});

split.Items.Add(new SuperSplitItem
{
    Weight = 0.7,
    Content = new TextBlock { Text = "Right" }
});
```

## Key Properties

### SuperSplit

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Orientation` | Orientation | Horizontal | Split direction |
| `Items` | ObservableCollection&lt;SuperSplitItem&gt; | empty | Child panes |
| `SplitterSize` | double | 4 | Splitter bar thickness in pixels |
| `ShowSplitters` | bool | true | Whether splitter bars are visible |

### SuperSplitItem

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Weight` | double | 1.0 | Proportional size weight |
| `MinSize` | double | 50 | Minimum pane size in pixels |
| `Content` | object | null | Pane content |
| `IsCollapsed` | bool | false | Whether the pane is collapsed |

## Splitter Interaction

| Action | Behavior |
|--------|----------|
| Drag splitter | Resize adjacent panes proportionally |
| Double-click splitter | Reset to equal weights |
| Drag to edge | Collapse pane to minimum size |

## Layout Algorithm

The layout distributes available space according to pane weights:

1. Calculate total available space (container size minus splitter sizes)
2. Sum all pane weights
3. Assign each pane: `size = (weight / totalWeight) * availableSpace`
4. Enforce minimum size constraints
5. Redistribute excess space to other panes

When a pane is collapsed, its weight is redistributed to adjacent panes.

## Usage in the Inspector

The CDP Inspector uses `SuperSplit` extensively for its panel layout:
- Main horizontal split between sidebar and content area
- Vertical splits within panels (e.g., Elements tree above, Styles below)
- Nested splits for complex multi-panel layouts
- User-adjustable proportions that persist across sessions

## Dependencies

- `Avalonia` — UI framework
- No CDP or protocol dependency — standalone layout control

## Next Steps

- [Minimap Editor](/articles/minimap-editor) — Text editor with minimap sidebar
- [Node Editor](/articles/node-editor) — Graph node visualization control
- [Package Guide](/articles/packages) — All available packages
