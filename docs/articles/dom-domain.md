---
title: DOM Domain
---

# DOM Domain

The `DOM` domain exposes the Avalonia user interface structure through the Chrome DevTools Protocol. It acts as a bridge between the browser's concept of document nodes and Avalonia's visual/logical trees, enabling automation, inspection, and manipulation of live desktop UI controls using standard Web protocols.

## Overview

Unlike standard web applications where the DOM is built from HTML elements and styled with CSS, Avalonia applications structure their UI using two hierarchical trees: the **Logical Tree** (representing logical controls and user-defined hierarchies) and the **Visual Tree** (representing the actual rendering elements, including control templates, borders, and presenters).

The `DOM` domain bridges this gap by:
1. **Exposing the Tree Hierarchy:** Traversing Avalonia's controls and translating them into virtual DOM nodes.
2. **Assigning Unique Node IDs:** Dynamically mapping each `Visual` or `Control` to a session-unique integer (`nodeId`).
3. **Translating Attributes:** Serializing control properties (such as `Name`, `Bounds`, `IsEnabled`, `Classes`, and `AutomationProperties.AutomationId`) into a flat list of DOM attribute name/value pairs.
4. **Supporting CSS Selector Queries:** Translating web-style CSS selectors into Avalonia control lookups (e.g., `#id`, class names, text content, and automation properties).
5. **Handling Live Actions:** Exposing methods to manipulate attributes, focus controls, request box coordinates, and navigate/remove nodes.

---

## Visual Tree vs. Logical Tree Mapping

The `DOM` domain supports traversing either the **Logical Tree** or the **Visual Tree** based on the `pierce` parameter sent in requests such as `DOM.getDocument` or `DOM.getFlattenedDocument`.

```
                  +-------------------------+
                  |  #document (nodeId = 1) |
                  +-------------------------+
                               |
                               | (Child 0)
                               v
                  +-------------------------+
                  |    MainWindow Node      |
                  +-------------------------+
                               |
            +------------------+------------------+
            | (pierce = false)                    | (pierce = true)
            v                                     v
   [ Logical Tree Mode ]                  [ Visual Tree Mode ]
Exposes XAML-defined controls.          Exposes every visual element,
Hides templates and internal            including control templates,
presenters (e.g. scrollbars).           borders, presenters, etc.
```

### The Node ID Map (`NodeMap`)
A CDP session maintains a `NodeMap` that assigns a unique integer `nodeId` to each `Visual` element as it is traversed or queried. 
* The virtual root is always the `#document` node, which is assigned `nodeId = 1` and has a `nodeType = 9` (Document Node).
* The first child of the `#document` is the target top-level `Window`.
* Regular Avalonia controls are represented as `nodeType = 1` (Element Node) with a `nodeName` matching their C# class name (e.g., `Button`, `TextBox`, `TextBlock`).

### Attribute Representation
In the Chrome DevTools Protocol, element attributes are represented as a flat array of strings containing name-value pairs, rather than a key-value dictionary. For example, `["id", "btnClickMe", "class", "primary"]`.

The CDP server serializes the following Avalonia properties into DOM attributes:

| DOM Attribute | Avalonia Source | Type / Formatting | Description |
|---|---|---|---|
| `type` / `Type` | `Visual.GetType().FullName` | String | The fully qualified C# type name of the control. |
| `id` / `Name` / `Id` | `Control.Name` | String | The unique control name defined in XAML or code. |
| `class` / `Class` | `Control.Classes` | Space-separated string | The Avalonia classes applied to the control (excluding pseudo-classes starting with `:`). |
| `text` / `Text` | Text helper | String | The text or content of the control (read from `TextBlock.Text`, `TextBox.Text`, or content properties). |
| `Bounds` | `Control.Bounds` | `X,Y,Width,Height` | Layout position and size relative to its parent container. |
| `IsEnabled` | `Control.IsEnabled` | `"true"` / `"false"` | Whether the control is enabled. |
| `IsVisible` | `Control.IsVisible` | `"true"` / `"false"` | Whether the control is visible in the tree. |
| `IsFocused` | `Control.IsFocused` | `"true"` / `"false"` | Whether the control currently has keyboard focus. |
| `IsChecked` | `ToggleButton.IsChecked` | `"true"` / `"false"` | The checked state (only applicable to `ToggleButton` and descendants). |
| `IsSelected` | Selection helper | `"true"` / `"false"` | The selection state (for `ListBoxItem`, `TabItem`, `TreeViewItem`, or controls with an `IsSelected` property). |
| `Width` / `Height` | Rounded Bounds | String | Integer representation of the control's current bounds. |
| `Traits` | Auto-detected | Space-separated tags | Metadata flags such as `"text"`, `"long-text"`, or `"square"`. |
| `AccessibilityName` | `AutomationProperties.Name` | String | The accessible name of the control. |
| `AccessibilityHelp` | `AutomationProperties.HelpText` | String | The descriptive help text for screen readers. |
| `AccessibilityId` / `AutomationId` / `AutomationProperties.AutomationId` / `automation-id` | `AutomationProperties.AutomationId` | String | The stable identifier used for automated UI testing. |

---

## DOM Methods Reference

This section details the primary methods implemented by the `DOM` domain in `Avalonia.Diagnostics.Cdp`.

### 1. DOM.getDocument
Retrieves the root DOM node and optionally its descendants. DevTools-compatible clients call this immediately after enabling the domain to construct the initial tree view.

* **Parameters:**
  * `depth` (integer, optional): The maximum depth to which children should be retrieved. Defaults to `-1` (returns the entire tree).
  * `pierce` (boolean, optional): If `true`, the protocol bypasses logical tree wrappers and traverses the full Visual Tree. Defaults to `false` (Logical Tree).

#### Request Example
```json
{
  "id": 100,
  "method": "DOM.getDocument",
  "params": {
    "depth": -1,
    "pierce": true
  }
}
```

#### Response Example
```json
{
  "id": 100,
  "result": {
    "root": {
      "nodeId": 1,
      "backendNodeId": 1,
      "nodeType": 9,
      "nodeName": "#document",
      "localName": "",
      "nodeValue": "",
      "childNodeCount": 1,
      "documentURL": "http://localhost:9222/",
      "baseURL": "http://localhost:9222/",
      "children": [
        {
          "nodeId": 2,
          "backendNodeId": 2,
          "nodeType": 1,
          "nodeName": "MainWindow",
          "localName": "MainWindow",
          "nodeValue": "",
          "childNodeCount": 3,
          "attributes": [
            "type", "CdpSampleApp.MainWindow",
            "id", "mainWin",
            "Name", "mainWin",
            "Width", "800",
            "Height", "600"
          ]
        }
      ]
    }
  }
}
```

---

### 2. DOM.requestChildNodes
Requests that children of the specified node be retrieved and sent back asynchronously via a `DOM.setChildNodes` event. This is typically used during lazy-loading of the DOM tree when a user expands a collapsed folder/element node in the DevTools UI.

* **Parameters:**
  * `nodeId` (integer): The ID of the node to request children for.
  * `depth` (integer, optional): The maximum depth to which children should be retrieved. Defaults to `1`.

#### Request Example
```json
{
  "id": 101,
  "method": "DOM.requestChildNodes",
  "params": {
    "nodeId": 2,
    "depth": 1
  }
}
```

#### Response Example (Synchronous Ack)
```json
{
  "id": 101,
  "result": {}
}
```

#### Notification Example (Asynchronous Event)
```json
{
  "method": "DOM.setChildNodes",
  "params": {
    "parentId": 2,
    "nodes": [
      {
        "nodeId": 10,
        "backendNodeId": 10,
        "nodeType": 1,
        "nodeName": "DockPanel",
        "localName": "DockPanel",
        "nodeValue": "",
        "childNodeCount": 2,
        "attributes": [
          "type", "Avalonia.Controls.DockPanel",
          "Bounds", "0,0,800,600"
        ]
      }
    ]
  }
}
```

---

### 3. DOM.querySelector
Queries the visual or logical tree beneath a specified node using a CSS selector. It returns the ID of the first node matching the criteria.

* **Parameters:**
  * `nodeId` (integer): The ID of the node to start the query from. If `nodeId` is `1`, the query starts from the root `Window`.
  * `selector` (string): The CSS selector to match against.

#### Request Example
```json
{
  "id": 102,
  "method": "DOM.querySelector",
  "params": {
    "nodeId": 1,
    "selector": "#btnClickMe"
  }
}
```

#### Response Example
```json
{
  "id": 102,
  "result": {
    "nodeId": 42
  }
}
```

---

### 4. DOM.querySelectorAll
Queries the visual or logical tree beneath a specified node using a CSS selector and returns the IDs of all matching nodes.

* **Parameters:**
  * `nodeId` (integer): The ID of the node to start the query from.
  * `selector` (string): The CSS selector query.

#### Request Example
```json
{
  "id": 103,
  "method": "DOM.querySelectorAll",
  "params": {
    "nodeId": 1,
    "selector": "Button.primary"
  }
}
```

#### Response Example
```json
{
  "id": 103,
  "result": {
    "nodeIds": [42, 45, 98]
  }
}
```

---

### 5. DOM.getOuterHTML
Generates a pseudo-HTML representation of the specified node and its descendants. The output includes control class names as tags, control names as `id` attributes, classes as `class`, and any present text values as `text` attributes.

* **Parameters:**
  * `nodeId` (integer): The ID of the target node.

#### Request Example
```json
{
  "id": 104,
  "method": "DOM.getOuterHTML",
  "params": {
    "nodeId": 42
  }
}
```

#### Response Example
```json
{
  "id": 104,
  "result": {
    "outerHTML": "<Button id=\"btnClickMe\" class=\"primary\" text=\"Click Me\"><TextBlock text=\"Click Me\" /></Button>"
  }
}
```

---

### 6. DOM.resolveNode
Resolves a given `nodeId` to a runtime JavaScript/C# `RemoteObject` representation. This returns an `objectId` that can be passed directly to `Runtime` evaluation methods (like `Runtime.callFunctionOn` or `Runtime.getProperties`) to inspect underlying C# fields or call methods on the control instance.

* **Parameters:**
  * `nodeId` (integer): The ID of the node to resolve.

#### Request Example
```json
{
  "id": 105,
  "method": "DOM.resolveNode",
  "params": {
    "nodeId": 42
  }
}
```

#### Response Example
```json
{
  "id": 105,
  "result": {
    "object": {
      "type": "object",
      "subtype": "node",
      "className": "Avalonia.Controls.Button",
      "description": "Button (ID=42)",
      "objectId": "object-group-42-id"
    }
  }
}
```

---

### 7. DOM.focus
Sets keyboard and system focus onto the specified node. The target control must implement Avalonia's `IInputElement` interface to be focusable.

* **Parameters:**
  * `nodeId` (integer): The ID of the node to focus.

#### Request Example
```json
{
  "id": 106,
  "method": "DOM.focus",
  "params": {
    "nodeId": 42
  }
}
```

#### Response Example
```json
{
  "id": 106,
  "result": {}
}
```

---

### 8. DOM.setInspectedNode
Informs the server that a specific node has been selected/inspected in the client UI. The server updates its internal state tracking which element is currently highlighted or inspected.

* **Parameters:**
  * `nodeId` (integer): The ID of the inspected node.

#### Request Example
```json
{
  "id": 107,
  "method": "DOM.setInspectedNode",
  "params": {
    "nodeId": 42
  }
}
```

#### Response Example
```json
{
  "id": 107,
  "result": {}
}
```

---

### 9. DOM.setAttributeValue
Modifies an attribute on the specified control. The server translates specific attribute names into standard Avalonia control modifications. All attribute modifications are scheduled dynamically on the Avalonia UI Thread.

* **Attribute Mappings in Write Mode:**
  * `class`: Clears all classes and applies the space-separated classes provided.
  * `name` / `id`: Sets the `control.Name` property.
  * `text`: Sets the `Text` property on `TextBlock` / `TextBox`, the `Header` or `Content` on `HeaderedContentControl`, `Content` on `ContentControl`, or `Header` on `HeaderedItemsControl`.
  * Other properties: Modifies properties dynamically using the CSS property parser helper.

* **Parameters:**
  * `nodeId` (integer): The ID of the node to modify.
  * `name` (string): The attribute name.
  * `value` (string): The value to apply.

#### Request Example
```json
{
  "id": 108,
  "method": "DOM.setAttributeValue",
  "params": {
    "nodeId": 42,
    "name": "text",
    "value": "Submit Form"
  }
}
```

#### Response Example
```json
{
  "id": 108,
  "result": {}
}
```

---

### 10. DOM.removeAttribute
Removes an attribute from a control, resetting its corresponding property to its default value or clearing visual structures.

* **Supported Attributes for Removal:**
  * `class`: Clears the control's `Classes` collection entirely.
  * `name` / `id`: Resets `control.Name` to `null`.

* **Parameters:**
  * `nodeId` (integer): The ID of the node to modify.
  * `name` (string): The attribute name to remove.

#### Request Example
```json
{
  "id": 109,
  "method": "DOM.removeAttribute",
  "params": {
    "nodeId": 42,
    "name": "class"
  }
}
```

#### Response Example
```json
{
  "id": 109,
  "result": {}
}
```

---

### 11. DOM.getBoxModel
Returns box metrics (margin, border, padding, and content coordinates) for the specified node. The coordinates are calculated relative to the top-level host Window using `TranslatePoint` so that testing clients can simulate precise click, hover, or drag operations.

* **Parameters:**
  * `nodeId` (integer): The ID of the node to measure.

#### Request Example
```json
{
  "id": 110,
  "method": "DOM.getBoxModel",
  "params": {
    "nodeId": 42
  }
}
```

#### Response Example
```json
{
  "id": 110,
  "result": {
    "model": {
      "content": [ 120.0, 80.0, 220.0, 80.0, 220.0, 110.0, 120.0, 110.0 ],
      "padding": [ 120.0, 80.0, 220.0, 80.0, 220.0, 110.0, 120.0, 110.0 ],
      "border":  [ 120.0, 80.0, 220.0, 80.0, 220.0, 110.0, 120.0, 110.0 ],
      "margin":  [ 112.0, 72.0, 228.0, 72.0, 228.0, 118.0, 112.0, 118.0 ],
      "width": 100,
      "height": 30
    }
  }
}
```

#### Box Quad Calculations
1. **Border Box:** Defined directly by `visual.Bounds` translated to the host Window origin.
2. **Margin Box:** Calculated by expanding the border box outwards by the control's `Margin` (Left, Top, Right, Bottom).
3. **Padding Box:** Sits inside the border box, subtracting `BorderThickness` layout values.
4. **Content Box:** Sits inside the padding box, subtracting the control's `Padding` dimensions.

---

## Additional Supported DOM Methods

Apart from the 11 main methods, the protocol implementation supports secondary commands:

* **DOM.enable / DOM.disable:** Activates or deactivates observing changes in the visual tree. When enabled, tree modifications prompt the server to emit DOM update events.
* **DOM.getAttributes:** Returns a flat string list of all attributes for a given nodeId without returning the rest of the node structure.
* **DOM.describeNode:** Obtains detailed metadata for a node using `nodeId`, `backendNodeId`, or its remote `objectId`.
* **DOM.getNodeForLocation:** Performs a hit-test at client coordinates `(x, y)` relative to the window and returns the matching `nodeId`.
* **DOM.removeNode:** Asynchronously removes a control from its parent panel or content container on the UI thread.
* **DOM.performSearch / DOM.getSearchResults / DOM.discardSearchResults:** Allows querying the entire tree using text searches or CSS selectors. Returns a search identifier and match counts.
* **DOM.getFlattenedDocument:** Retrieves a flat list of all nodes matching the depth/pierce criteria, rather than a nested tree.
* **DOM.requestNode:** Returns the `nodeId` mapped to a runtime C# `objectId`.
* **DOM.scrollIntoViewIfNeeded:** Asynchronously calls `BringIntoView()` on the control.
* **DOM.setNodeValue / DOM.setNodeName:** Modifies name attributes or control text directly.

---

## CSS Selector Contract

When querying elements through `DOM.querySelector` or `DOM.querySelectorAll`, the `SelectorEngine` supports the following rules:

1. **Name ID Selectors:** `#myButton` matches controls where `Control.Name == "myButton"`.
2. **Explicit Attribute Matching:** `[id="myButton"]`, `[Id="myButton"]`, or `[Name="myButton"]` matches the control's name.
3. **Automation Identifiers:** `[AutomationId="txtInput"]`, `[AccessibilityId="txtInput"]`, or `[AutomationProperties.AutomationId="txtInput"]` matches `AutomationProperties.AutomationId`.
4. **Class Matching:** `[class~="primary"]` matches Avalonia classes applied to the element.
5. **Text Checks:** `TextBlock:contains("Submit")`, `[Text="Submit"]`, or `[text="Submit"]` performs a lookup based on visible text or content.

---

## Thread Safety and UI Thread Dispatching

Since Avalonia UI objects are not thread-safe and can only be accessed or modified from the main UI thread, the CDP server handles threading transparently:
* **Read-only queries:** Access visual/logical properties under the protection of UI thread contexts or thread-safe node structures.
* **Write operations:** Methods like `DOM.setAttributeValue`, `DOM.removeAttribute`, `DOM.removeNode`, `DOM.setNodeName`, `DOM.setNodeValue`, and `DOM.scrollIntoViewIfNeeded` invoke operations asynchronously using:
  ```csharp
  await Dispatcher.UIThread.InvokeAsync(() => { ... });
  ```
  This prevents cross-thread marshalling exceptions and keeps the host application responsive during automation scripts.
