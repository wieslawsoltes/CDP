---
title: Node Editor
---

# Node Editor

The Node Editor (`Chrome.DevTools.Editor.Nodes`) is a standalone Avalonia control for building and displaying interactive graph-based node diagrams. Combined with the MSAGL layout provider (`Chrome.DevTools.Editor.Nodes.Msagl`), it supports automatic graph layout for visualizing complex relationships.

## Overview

The Node Editor provides:
- A zoomable, pannable canvas for graph visualization
- Draggable node elements with input/output connection ports
- Bezier curve connections between ports
- Automatic layout via Microsoft Automatic Graph Layout (MSAGL)
- Selection, multi-selection, and group operations

## Installation

### Core Node Editor

```bash
dotnet add package Chrome.DevTools.Editor.Nodes --prerelease
```

### With MSAGL Automatic Layout

```bash
dotnet add package Chrome.DevTools.Editor.Nodes.Msagl --prerelease
```

## Usage

### XAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:nodes="clr-namespace:CDP.Editor.Nodes;assembly=CDP.Editor.Nodes">

    <nodes:NodeEditorView
        Name="nodeEditor"
        Background="#1e1e2e"
        GridSize="20"
        ShowGrid="True"/>
</Window>
```

### Building a Graph Programmatically

```csharp
var editor = new NodeEditorView();

// Create nodes
var node1 = new NodeModel
{
    Title = "Input",
    X = 100, Y = 100,
    Outputs = { new PortModel("Output", PortType.Output) }
};

var node2 = new NodeModel
{
    Title = "Process",
    X = 350, Y = 100,
    Inputs = { new PortModel("Input", PortType.Input) },
    Outputs = { new PortModel("Output", PortType.Output) }
};

var node3 = new NodeModel
{
    Title = "Output",
    X = 600, Y = 100,
    Inputs = { new PortModel("Input", PortType.Input) }
};

// Create connections
var connection1 = new ConnectionModel(node1.Outputs[0], node2.Inputs[0]);
var connection2 = new ConnectionModel(node2.Outputs[0], node3.Inputs[0]);

// Add to editor
editor.Graph = new GraphModel
{
    Nodes = { node1, node2, node3 },
    Connections = { connection1, connection2 }
};
```

### Automatic Layout with MSAGL

```csharp
using CDP.Editor.Nodes.Msagl;

// Apply MSAGL automatic layout
var layoutProvider = new MsaglLayoutProvider();
layoutProvider.Layout(editor.Graph);
```

## Key Components

### NodeModel

Represents a single node in the graph:

| Property | Type | Description |
|----------|------|-------------|
| `Title` | string | Display title of the node |
| `X`, `Y` | double | Position on the canvas |
| `Width`, `Height` | double | Node dimensions |
| `Inputs` | ObservableCollection&lt;PortModel&gt; | Input connection ports |
| `Outputs` | ObservableCollection&lt;PortModel&gt; | Output connection ports |
| `IsSelected` | bool | Selection state |

### PortModel

Represents a connection point on a node:

| Property | Type | Description |
|----------|------|-------------|
| `Name` | string | Port label |
| `Type` | PortType | `Input` or `Output` |
| `IsConnected` | bool | Whether a connection exists |

### ConnectionModel

Represents a directed edge between two ports:

| Property | Type | Description |
|----------|------|-------------|
| `Source` | PortModel | Output port (start) |
| `Target` | PortModel | Input port (end) |

### GraphModel

Container for the entire graph:

| Property | Type | Description |
|----------|------|-------------|
| `Nodes` | ObservableCollection&lt;NodeModel&gt; | All nodes |
| `Connections` | ObservableCollection&lt;ConnectionModel&gt; | All connections |

## Canvas Interaction

| Action | Behavior |
|--------|----------|
| Click node | Select node |
| Ctrl+Click | Toggle multi-selection |
| Drag node | Move node (connections follow) |
| Drag canvas | Pan the viewport |
| Mouse wheel | Zoom in/out |
| Drag from port | Create new connection |
| Delete key | Remove selected nodes/connections |

## MSAGL Layout Provider

The `MsaglLayoutProvider` uses Microsoft Automatic Graph Layout to compute optimal node positions:

- **Layered layout** — Arranges nodes in layers based on dependency direction
- **Sugiyama algorithm** — Minimizes edge crossings
- **Edge routing** — Computes smooth Bezier paths avoiding node overlaps

## Usage in the Inspector

The CDP Inspector uses the Node Editor to visualize:
- Visual tree hierarchies as node graphs
- CDP session topology diagrams
- DOM relationship graphs

## Dependencies

| Package | Purpose |
|---------|---------|
| `Chrome.DevTools.Editor.Nodes` | Core node editor control |
| `Chrome.DevTools.Editor.Nodes.Msagl` | MSAGL automatic layout provider |
| `Avalonia` | UI framework |

Both packages are standalone — no CDP or protocol dependency required.

## Next Steps

- [Minimap Editor](/articles/minimap-editor) — Text editor with minimap sidebar
- [Splits Layout](/articles/splits-layout) — Dynamic split pane container
- [Package Guide](/articles/packages) — All available packages
