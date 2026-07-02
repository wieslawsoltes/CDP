---
title: Elements Panel
description: Technical guide to visual tree inspection, live style editing, computed box models, accessibility trees, and DOM node overlays in the CDP Inspector for Avalonia.
---

# Elements Panel

The **Elements Panel** is the primary interface for inspecting and manipulating the visual tree of a running Avalonia application. Similar to the elements panel in standard browser developer tools, it represents the desktop UI structure as an XML-like hierarchical DOM (Document Model) tree or an AX (Accessibility) tree. Developers and automated test agents can query elements, edit styles, examine properties, and debug layout boundaries in real-time.

---

## 1. Visual Tree and DOM Tree Navigation

At the heart of the Elements Panel is the dual-tree navigation pane, which splits the application structure into two distinct representations:
1. **DOM Tree Tab**: An XML-style hierarchy representing the active controls in the Avalonia visual tree.
2. **Accessibility Tree (AX) Tab**: A semantic tree representing the UI as exposed to screen readers and automated accessibility APIs.

### The DOM Tree Interface (`treeDom`)
The DOM Tree represents control hierarchies using a `DataGrid` custom control supporting hierarchical expansion. Controls are rendered with custom foreground colors to highlight their roles (e.g., green for text blocks, blue for buttons, grey for layout containers).

- **Real-Time Search**: The `txtSearch` box allows querying the visual tree by:
  - Control tags (e.g., `Button`, `TextBox`, `Grid`).
  - Name identifiers (e.g., `#btnConnect`).
  - Active classes (e.g., `.primary`).
  - Unquoted text content inside controls (e.g., finding the element containing "Ready").
- **Inspect Actions**:
  - **Scroll Into View**: Clicking `btnFocus` sends a `DOM.focus` protocol request, causing the target application to scroll the selected control into visible coordinates.
  - **Visual Tree Mode**: Toggling `chkVisualTree` switches the hierarchy representation between a simplified HTML-like structure and the raw, full Avalonia visual tree hierarchy.
  - **Delete Element**: Clicking `btnDeleteControl` executes a node destruction request (`DOM.removeNode`), removing the control from the running target UI. This is highly useful for testing responsive flow layout shifts and UI reflow resilience.

### The Accessibility Tree (`treeAx`)
The Accessibility Tree tab represents controls as nodes that expose semantic parameters. Clicking an AX node automatically highlights and selects its corresponding visual DOM node in the DOM tree, bridging accessibility debugging with structural layout analysis.
- **Refresh Control**: Clicking `btnRefreshAxTree` forces the inspector to query the `Accessibility.getFullAXTree` endpoint to rebuild the AX hierarchy.

---

## 2. Live Inline Style and Class Editing

On the right sidebar of the Elements Panel, the **Styles** subtab allows inspecting and editing style property values in real-time.

### Filter Styles
Users can filter styles by keyword in `txtCssSearch` to isolate specific variables like colors, widths, or margins.

### Pseudostate Panels (`:hov` Toggle)
Toggling the `:hov` button opens a checkbox array (`chkForcedHover`, `chkForcedActive`, `chkForcedFocus`, etc.) allowing developers to force specific Avalonia pseudoclasses:
- `:pointerover` (rendered as `:hover`)
- `:pressed` (rendered as `:active`)
- `:focus`
- `:focus-within`
- `:focus-visible`
- `:disabled`

Forcing these states makes it easy to debug style changes without needing to physically click and hold mouse cursors on the target application.

### Class Rule Manager (`.cls` Toggle)
Toggling the `.cls` panel displays a wrap panel showing all CSS-style classes currently applied to the control.
- **Toggle Classes**: Checking or unchecking class name boxes dynamically inserts or removes the class name from the control's class collection.
- **Add New Class**: Entering a class name into `txtAddClass` and pressing Enter adds a new class name to the control, triggering immediate Avalonia theme updates.

### Inline Styles Grid (`listCssProperties`)
The inline styles table displays editable `Name` and `Value` columns:
- **Real-Time Edits**: Double-clicking the `Value` cell makes the text editable. Modifying the value (e.g., changing `#ff5252` to `#4caf50`) updates the control in the running application instantly.
- **Add Custom Rules**: The text box `txtStyleText` at the bottom of the tab allows typing raw CSS-like properties (e.g., `background: blue; opacity: 0.8;`) and applying them collectively via `btnApplyStyleText`.

---

## 3. Computed Styles and Concentric Box Model Editor

### Computed Rules Grid (`listComputedStyles`)
While inline styles show the values explicitly set on the element, the **Computed rules** subtab lists all resolved layout properties after style sheets, default themes, and parental inheritance are calculated. Properties are filtered via `txtComputedSearch` to identify final rendering values.

### Concentric Box Model Editor
The concentric computed box model is a visual diagram mapping the control's structural boundaries:
- **Margin Layer** (Orange outline): Represents the outermost spacing surrounding the control.
- **Border Layer** (Yellow outline): Represents the boundary thickness of the control's frame.
- **Padding Layer** (Green outline): Represents internal cell margins between the border and child contents.
- **Content Layer** (Blue outline): Displays the current width and height of the control.

```
+---------------------------------------------------------+
| MARGIN (Orange)                                         |
|    [Top]                                                |
|  +---------------------------------------------------+  |
|  | BORDER (Yellow)                                   |  |
|  |    [Top]                                          |  |
|  |  +---------------------------------------------+  |  |
|  |  | PADDING (Green)                             |  |  |
|  |  |    [Top]                                    |  |  |
|  |  |  +---------------------------------------+  |  |  |
|  |  |  | CONTENT (Blue)                        |  |  |  |
|  |  |  |    [Width] x [Height]                 |  |  |  |
|  |  |  +---------------------------------------+  |  |  |
|  |  |    [Bottom]                             |  |  |  |
|  |  +---------------------------------------------+  |  |
|  |    [Bottom]                                       |  |
|  +---------------------------------------------------+  |
|    [Bottom]                                             |
+---------------------------------------------------------+
```

- **Double-Tap Editing**: Double-clicking any of the boundary value fields (e.g., `BoxMarginTop` or `BoxWidth`) activates an in-place `TextBox` input. Typing a number and pressing Enter updates the layout parameters on the target control. For instance, increasing margin values immediately pushes neighboring controls aside.

---

## 4. Attributes, Properties, and Event Listeners

### Attributes Subtab (`listAttributes`)
Visual controls in Avalonia expose XML attributes. The Attributes tab displays active key-value attributes (such as `Name`, `Id`, `Text`, `IsEnabled`):
- **Filtering**: Type in `txtAttributesSearch` to isolate specific attributes.
- **Applying & Deleting**: Modify values directly in the grid, or use the `txtAttrName` and `txtAttrValue` fields at the bottom to insert new properties or remove them using `btnDeleteAttr`.

### Properties Subtab (`listProperties`)
Unlike standard HTML attributes, Avalonia controls have strongly-typed C# backing properties (e.g., dependency properties and styled properties).
- **Type Checking**: The Properties list shows property Names, values, and C# types in parentheses (e.g., `(Avalonia.Media.SolidColorBrush)`).
- **Set Property**: Select a property, type the desired input value in `txtPropertyValue`, and click `Set` (`btnApplyProperty`). The CDP backend will automatically parse and convert strings to their appropriate runtime types.

### Event Listeners Subtab (`listEventListeners`)
This tab registers all events hooked to the control:
- **Type**: The name of the event (e.g., `PointerPressed`, `TextChanged`, `Click`).
- **Handler**: The C# method signature mapped to the event.
- **Capture**: Boolean flag indicating whether the event handler listens in the routing bubble or tunneling phase.

---

## 5. Accessibility Metadata Inspector

The **Accessibility** tab provides a detailed breakdown of the selected control's semantic attributes, helping developers audit compatibility without screen readers active:
- **Role**: The UI role of the element (e.g., `Button`, `CheckBox`, `StaticText`).
- **Name**: The calculated accessibility text label.
- **Description**: Secondary description tooltips.
- **Ignored**: Tells if the control is hidden from screen readers.
- **Parent & Child IDs**: Navigate accessibility tree relationships.

---

## 6. Highlight and Hover Overlay Interaction

A crucial aspect of inspection is the visual highlight overlay:
- **Inspect Highlight Mode**: Checking `chkHighlight` registers mouse hover events on the inspector's preview pane.
- **Visual Bounds Syncing**: Moving the mouse over the preview pane maps coordinates to the sample target window. It triggers a `DOM.highlightNode` action, showing a translucent colored overlay on the sample app indicating:
  - Blue for Content area.
  - Green for Padding.
  - Yellow for Border.
  - Orange for Margin.
- **Click-to-Select**: Clicking the highlighted area auto-selects that control in the DOM tree, avoiding manual tree searching.

---

## 7. Underlying CDP Protocol Specifications

The Elements Panel maps UI actions directly to standard Chrome DevTools Protocol domains:

### Document Retrieval
```json
{
  "id": 1,
  "method": "DOM.getDocument",
  "params": { "pierce": true, "depth": -1 }
}
```

### Visual Highlight Overlay
```json
{
  "id": 2,
  "method": "DOM.highlightNode",
  "params": {
    "highlightConfig": {
      "showInfo": true,
      "contentColor": { "r": 138, "g": 180, "b": 248, "a": 0.5 },
      "paddingColor": { "r": 93, "g": 158, "b": 93, "a": 0.5 },
      "borderColor": { "r": 227, "g": 181, "b": 5, "a": 0.5 },
      "marginColor": { "r": 213, "g": 133, "b": 18, "a": 0.5 }
    },
    "nodeId": 42
  }
}
```

### Fetching Box Models
```json
{
  "id": 3,
  "method": "DOM.getBoxModel",
  "params": { "nodeId": 42 }
}
```

### Updating Styles
```json
{
  "id": 4,
  "method": "CSS.setStyleTexts",
  "params": {
    "edits": [
      {
        "styleSheetId": "inline",
        "range": { "startLine": 0, "startColumn": 0, "endLine": 0, "endColumn": 0 },
        "text": "background: #ff5252"
      }
    ]
  }
}
```

By leveraging these standard protocol structures, developers can inspect desktop layout trees with the same efficiency and tools traditionally reserved for modern web applications.
