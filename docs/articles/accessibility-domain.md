---
title: Accessibility Domain
---

# Accessibility Domain

The `Accessibility` domain exposes the application's user interface hierarchy as a virtual accessibility tree (AXTree). It acts as a bridge between the browser's concept of accessible nodes (AXNode) and Avalonia's automation peer subsystem (`AutomationPeer`), allowing automated testers, accessibility tools, and screen reader interfaces to inspect and query the semantic structure of a live Avalonia desktop UI.

The primary implementation is located in `AccessibilityDomain.cs`.

---

## Overview

In Avalonia applications, accessibility details are managed through **Automation Peers** (`AutomationPeer`). These peers define how controls behave and report their properties (such as roles, names, descriptions, and interactive values) to the host operating system's native accessibility APIs (e.g., UI Automation on Windows, NSAccessibility on macOS, and AT-SPI on Linux).

The CDP `Accessibility` domain leverages this native infrastructure to reconstruct a browser-like accessibility tree over WebSocket. 

### Key Characteristics of the virtual AXTree:
1. **String Node IDs:** Unlike the `DOM` domain which uses integer `nodeId`s, the `Accessibility` domain utilizes string `nodeId`s. For controls backed by a `ControlAutomationPeer`, the ID is the string representation of their corresponding DOM integer node ID. For peers without direct visual attachments, a synthetic string is generated (e.g., `synthetic-<hash>`).
2. **Ignored Nodes:** Controls that lack user-facing semantics or are explicitly configured to be hidden from screen readers are filtered out or marked as `ignored: true`.
3. **Property Serialization:** Specialized UI states (such as checked, expanded, focusable, and selected) are exposed as semantic key-value property objects.
4. **Visual Tree Fallback:** If a control or visual element does not expose an automation peer, the domain employs a fallback evaluator to extract basic accessibility values directly from the visual hierarchy.

---

## Automation Peers Mapping

At the core of the `Accessibility` domain is the mapping of Avalonia `AutomationPeer` instances. When traversing the tree, the domain starting point is the application's root `Window` peer, created using `ControlAutomationPeer.CreatePeerForElement(session.Window)`. 

### Ignored Nodes Criteria
An AXNode is marked with `"ignored": true` under any of the following conditions:
* The peer is an instance of `NoneAutomationPeer`.
* The peer is classified as neither a control element nor a content element (`!peer.IsControlElement() && !peer.IsContentElement()`).
* The control has its accessibility view explicitly overridden in XAML or code to `Raw` (e.g., `AutomationProperties.AccessibilityView="Raw"`).
* For fallback visuals, a node is ignored unless its `AccessibilityView` is explicitly set to `Control` or `Content`, or it has an explicit automation name/control type override.

---

## Role Mapping (`AutomationControlType` to CDP Roles)

The domain translates Avalonia's `AutomationControlType` enum to corresponding W3C ARIA accessibility role tokens. The mapping is resolved as follows:

| Avalonia `AutomationControlType` | CDP Role Token | Description / Target Controls |
| :--- | :--- | :--- |
| `Button` | `"button"` | Clickable buttons, checkboxes acting as buttons. |
| `CheckBox` | `"checkbox"` | Dual- or tri-state check boxes. |
| `ComboBox` | `"combobox"` | Dropdown selection lists. |
| `Edit` | `"textbox"` | Text inputs (`TextBox`, `MaskedTextBox`). |
| `List` | `"list"` | Lists or grids (`ListBox`). |
| `ListItem` | `"listitem"` | Selectable items inside lists. |
| `Slider` | `"slider"` | Numeric range adjusters (`Slider`). |
| `Text` | `"StaticText"` | Read-only text elements (`TextBlock`). |
| `Header` | `"heading"` | Section headers or titles. |
| `Menu` | `"menu"` | Application or context menus. |
| `MenuItem` | `"menuitem"` | Individual action items inside menus. |
| `ProgressBar` | `"progressbar"` | Indeterminate or value-based progress indicators. |
| `RadioButton` | `"radio"` | Mutually exclusive option buttons. |
| `ScrollBar` | `"scrollbar"` | Scroll position bars. |
| `Tab` | `"tab"` | Tab items inside a TabControl container. |
| `TabItem` | `"tab"` | Individual tab pages. |
| `ToolTip` | `"tooltip"` | Transient hover labels. |
| `Tree` | `"tree"` | Hierarchical folder trees (`TreeView`). |
| `TreeItem` | `"treeitem"` | Tree node items. |
| `Window` | `"window"` | Root application windows. |

### Fallback and Overrides
1. **Control Type Overrides:** Developers can override a control's role in XAML using `AutomationProperties.ControlTypeOverride`. The domain prioritizes this override value.
2. **Class Names:** If the `AutomationControlType` does not match any of the standard entries, the domain attempts to use the peer's custom class name (`peer.GetClassName()`).
3. **C# Type Name:** If the custom class name is missing or empty, the domain falls back to the control's C# class type name (e.g., `Border`, `Grid`).

---

## Accessibility Properties Mapping

Each AXNode exposes a list of `properties` that detail its current states, input patterns, and attributes. The table below outlines how these map from Avalonia controls:

| CDP Property Name | Value Type | Avalonia Provider / Property | Description |
| :--- | :--- | :--- | :--- |
| `focusable` | `boolean` | `peer.IsKeyboardFocusable()` | Indicates if the node can receive keyboard focus. |
| `focused` | `boolean` | `peer.HasKeyboardFocus()` | Indicates if the node currently has keyboard focus. |
| `disabled` | `boolean` | `!peer.IsEnabled()` | Indicates if the node is disabled (inverted enabled state). |
| `valuemin` | `number` | `IRangeValueProvider.Minimum` | The minimum limit for range controls (e.g., Sliders). |
| `valuemax` | `number` | `IRangeValueProvider.Maximum` | The maximum limit for range controls. |
| `value` | `number` / `string` | `IRangeValueProvider.Value` / `IValueProvider.Value` | The current value of the control. |
| `checked` | `token` (`"true"`, `"false"`, `"mixed"`) | `IToggleProvider.ToggleState` | The toggle state of elements like checkboxes or radio buttons. |
| `expanded` | `boolean` | `IExpandCollapseProvider.ExpandCollapseState` | Indicates whether the control is expanded. |
| `selected` | `boolean` | `ISelectionItemProvider.IsSelected` | Indicates if the item is currently selected. |
| `multiselectable`| `boolean` | `ISelectionProvider.CanSelectMultiple` | Indicates if multiple items can be selected at once. |
| `description` | `string` | `AutomationProperties.GetHelpText(visual)` | An optional explanation of the control's purpose. |
| `keyshortcuts` | `string` | `peer.GetAcceleratorKey()` / `AutomationProperties.GetAcceleratorKey()` | The keyboard shortcut / accelerator key associated with the control. |
| `posinset` | `integer` | `AutomationProperties.GetPositionInSet(visual)` | The 1-based position of the control inside its group or list. |
| `setsize` | `integer` | `AutomationProperties.GetSizeOfSet(visual)` | The total number of items in the group or list. |
| `live` | `token` (`"polite"`, `"assertive"`, `"off"`) | `AutomationProperties.GetLiveSetting(visual)` | How screen readers should announce changes to the control's text. |
| `required` | `boolean` | `AutomationProperties.GetIsRequiredForForm(visual)`| Whether the control is mandatory. |
| `roledescription`| `string` | `peer.GetLocalizedControlType()` | A localized, user-friendly description of the control's role. |

### Control Name Resolution
The text label or name of an AXNode is resolved in the following priority order:
1. `peer.GetName()` (provided by the control's automation peer).
2. `AutomationProperties.GetName(visual)` (configured explicitly in XAML/code).
3. **Content Extraction Fallback:** If neither is set, the domain inspects the control's text or content via `GetControlTextOrContent`:
   - `TextBlock.Text`
   - `TextBox.Text`
   - `ContentControl.Content` (if a string)
   - `HeaderedContentControl.Content` or `HeaderedContentControl.Header` (if strings)
   - `HeaderedItemsControl.Header` (if a string)

---

## Visual Tree Fallback Mechanism

When a `Visual` element in the tree has no associated `AutomationPeer` (or does not inherit from `Control`), the domain invokes `BuildAXNodeFromVisualFallback`. This ensures that even raw visuals can be inspected for automated tests.

During fallback:
* The `nodeId` is generated as the string representation of the visual's internal DOM node ID.
* The node is marked `ignored: true` by default, unless the visual has explicit automation overrides or names.
* If the visual implements `IInputElement`, properties like `focusable`, `focused`, and `disabled` are read directly from its `Focusable`, `IsFocused`, and `IsEnabled` properties respectively.
* Relationships like parent/child are mapped directly from the Avalonia Visual Tree using `visual.GetVisualParent()` and `visual.GetVisualChildren()`.

---

## Accessibility Methods Reference

Below are the CDP methods implemented in the `Accessibility` domain.

### 1. Accessibility.enable
Enables the accessibility domain. Once enabled, client applications can query the AXTree.
* **Parameters:** None
* **Returns:** Empty object

#### Request Example
```json
{
  "id": 20,
  "method": "Accessibility.enable"
}
```

#### Response Example
```json
{
  "id": 20,
  "result": {}
}
```

---

### 2. Accessibility.disable
Disables the accessibility domain.
* **Parameters:** None
* **Returns:** Empty object

#### Request Example
```json
{
  "id": 21,
  "method": "Accessibility.disable"
}
```

#### Response Example
```json
{
  "id": 21,
  "result": {}
}
```

---

### 3. Accessibility.getRootAXNode
Returns the root accessibility node for the active window.
* **Parameters:** None
* **Returns:**
  * `node` (AXNode, optional): The root accessibility node.

#### Request Example
```json
{
  "id": 22,
  "method": "Accessibility.getRootAXNode"
}
```

#### Response Example
```json
{
  "id": 22,
  "result": {
    "node": {
      "nodeId": "1",
      "ignored": false,
      "role": {
        "type": "role",
        "value": "window"
      },
      "backendDOMNodeId": 1,
      "name": {
        "type": "string",
        "value": "Cdp Inspector"
      },
      "childIds": ["2", "5"],
      "properties": [
        {
          "name": "focusable",
          "value": {
            "type": "boolean",
            "value": true
          }
        },
        {
          "name": "focused",
          "value": {
            "type": "boolean",
            "value": true
          }
        }
      ]
    }
  }
}
```

---

### 4. Accessibility.getFullAXTree
Traverses the entire tree of automation peers and fallback visuals, returning a flat list of all AXNodes.
* **Parameters:** None
* **Returns:**
  * `nodes` (array of AXNode): Complete flat list of accessibility nodes.

#### Request Example
```json
{
  "id": 23,
  "method": "Accessibility.getFullAXTree"
}
```

#### Response Example
```json
{
  "id": 23,
  "result": {
    "nodes": [
      {
        "nodeId": "1",
        "ignored": false,
        "role": { "type": "role", "value": "window" },
        "backendDOMNodeId": 1,
        "name": { "type": "string", "value": "Cdp Inspector" },
        "childIds": ["2"]
      },
      {
        "nodeId": "2",
        "ignored": false,
        "role": { "type": "role", "value": "button" },
        "backendDOMNodeId": 2,
        "name": { "type": "string", "value": "Connect" },
        "parentId": "1",
        "properties": [
          {
            "name": "focusable",
            "value": { "type": "boolean", "value": true }
          },
          {
            "name": "disabled",
            "value": { "type": "boolean", "value": false }
          }
        ]
      }
    ]
  }
}
```

---

### 5. Accessibility.getAXNodeAndAncestors
Fetches the accessibility node for the specified target control, along with its parent ancestors up to the root window.
* **Parameters:**
  * `nodeId` (integer, optional): The DOM node ID of the target control.
  * `backendNodeId` (integer, optional): The backend DOM node ID of the target control.
  * `objectId` (string, optional): The runtime object ID of the target control.
* **Returns:**
  * `nodes` (array of AXNode): The target AXNode and all of its ancestors.

#### Request Example
```json
{
  "id": 24,
  "method": "Accessibility.getAXNodeAndAncestors",
  "params": {
    "nodeId": 2
  }
}
```

#### Response Example
```json
{
  "id": 24,
  "result": {
    "nodes": [
      {
        "nodeId": "2",
        "ignored": false,
        "role": { "type": "role", "value": "button" },
        "backendDOMNodeId": 2,
        "name": { "type": "string", "value": "Connect" },
        "parentId": "1"
      },
      {
        "nodeId": "1",
        "ignored": false,
        "role": { "type": "role", "value": "window" },
        "backendDOMNodeId": 1,
        "name": { "type": "string", "value": "Cdp Inspector" }
      }
    ]
  }
}
```

---

### 6. Accessibility.getChildAXNodes
Returns direct children nodes of a parent accessibility node.
* **Parameters:**
  * `id` (string, required): The string ID of the parent accessibility node.
* **Returns:**
  * `nodes` (array of AXNode): The direct children nodes.

#### Request Example
```json
{
  "id": 25,
  "method": "Accessibility.getChildAXNodes",
  "params": {
    "id": "1"
  }
}
```

#### Response Example
```json
{
  "id": 25,
  "result": {
    "nodes": [
      {
        "nodeId": "2",
        "ignored": false,
        "role": { "type": "role", "value": "button" },
        "backendDOMNodeId": 2,
        "name": { "type": "string", "value": "Connect" },
        "parentId": "1"
      }
    ]
  }
}
```

---

### 7. Accessibility.getPartialAXTree
Retrieves a portion of the AXTree around a target element. If `fetchRelatives` is enabled, it returns the target node, its children, all of its ancestors, and the direct siblings of all those ancestors.
* **Parameters:**
  * `nodeId` (integer, optional): The DOM node ID of the target control.
  * `backendNodeId` (integer, optional): The backend DOM node ID.
  * `objectId` (string, optional): The runtime object ID.
  * `fetchRelatives` (boolean, optional): If `true`, returns siblings and ancestors. Defaults to `true`.
* **Returns:**
  * `nodes` (array of AXNode): The partial tree nodes.

#### Request Example
```json
{
  "id": 26,
  "method": "Accessibility.getPartialAXTree",
  "params": {
    "nodeId": 2,
    "fetchRelatives": true
  }
}
```

#### Response Example
```json
{
  "id": 26,
  "result": {
    "nodes": [
      {
        "nodeId": "2",
        "ignored": false,
        "role": { "type": "role", "value": "button" },
        "backendDOMNodeId": 2,
        "name": { "type": "string", "value": "Connect" },
        "parentId": "1"
      },
      {
        "nodeId": "1",
        "ignored": false,
        "role": { "type": "role", "value": "window" },
        "backendDOMNodeId": 1,
        "name": { "type": "string", "value": "Cdp Inspector" },
        "childIds": ["2", "3"]
      },
      {
        "nodeId": "3",
        "ignored": false,
        "role": { "type": "role", "value": "button" },
        "backendDOMNodeId": 3,
        "name": { "type": "string", "value": "Refresh" },
        "parentId": "1"
      }
    ]
  }
}
```

---

### 8. Accessibility.queryAXTree
Queries the virtual accessibility tree starting from a root element, filtering the results by accessible name and/or role token.
* **Parameters:**
  * `nodeId` (integer, optional): The DOM node ID of the root to search under. Defaults to window root.
  * `backendNodeId` (integer, optional): The backend DOM node ID.
  * `objectId` (string, optional): The runtime object ID.
  * `accessibleName` (string, optional): Filter matches containing this substring (case-insensitive).
  * `role` (string, optional): Filter matches whose role exactly matches this token (case-insensitive).
* **Returns:**
  * `nodes` (array of AXNode): The matching accessibility nodes.

#### Request Example
```json
{
  "id": 27,
  "method": "Accessibility.queryAXTree",
  "params": {
    "accessibleName": "Connect",
    "role": "button"
  }
}
```

#### Response Example
```json
{
  "id": 27,
  "result": {
    "nodes": [
      {
        "nodeId": "2",
        "ignored": false,
        "role": { "type": "role", "value": "button" },
        "backendDOMNodeId": 2,
        "name": { "type": "string", "value": "Connect" },
        "parentId": "1"
      }
    ]
  }
}
```
