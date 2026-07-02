---
title: Selector Engine
---

# Selector Engine

The Selector Engine in the Chrome DevTools Protocol (CDP) support for Avalonia UI is a powerful query engine that translates web-style CSS selectors into Avalonia control tree lookups. It allows automated testing scripts, DevTools-compatible inspectors, and AI agents to locate, inspect, and interact with specific controls in a running desktop application without relying on platform-specific desktop automation frameworks.

The Selector Engine can traverse both the **Logical Tree** (for high-level XAML structure) and the **Visual Tree** (for lower-level control layout and rendering details) based on the session's configuration.

---

## How the Engine Evaluates Selectors

The Selector Engine distinguishes between standard CSS selectors and raw text queries. When a selector is queried, the engine processes it through the following pipeline:

1. **Standard CSS Check (`IsStandardCss`):**
   The engine checks if the selector contains typical CSS symbols (such as `#`, `.`, `>`, `:`, `[`, `]`, `*`). 
   * If it does not contain these symbols, and **all** space-separated parts in the query are recognized as loaded visual control types (e.g., `Grid Border Button`), it is treated as a standard CSS selector.
   * If the selector is wrapped in quotes (e.g., `"Click Me"` or `'Click Me'`), it is identified as a raw text literal.

2. **Normalization (`NormalizeSelector`):**
   * Standard CSS selectors are kept as-is.
   * Raw text literals or non-standard CSS selectors are wrapped into a `:contains("...")` pseudo-class (e.g., `"Click Me"` becomes `:contains("Click Me")`).

3. **Tokenization & Traversal:**
   The normalized selector is split into individual tokens (e.g. tags, IDs, classes, attributes, combinators) and matched against the tree nodes starting from the specified root element.

---

## Supported Selector Types

The Selector Engine supports a wide range of selector types, combining standard CSS behaviors with Avalonia-specific properties.

### 1. Tag/Type Selectors
Matches controls by their C# class names. This matching is case-insensitive.
* **Short Name:** Matches the class name directly.
  * Example: `Button`, `TextBox`, `Grid`
* **Fully Qualified Name:** Matches the full namespace path.
  * Example: `Avalonia.Controls.Button`, `Avalonia.Controls.Primitives.TemplatedControl`
* **Wildcard Selector:** Matches any element.
  * Example: `*`

### 2. ID Selectors
Matches controls by their XAML or code-defined `Name` property.
* Syntax: `#controlName`
* Mapping: Internally matches `Control.Name`.
* Example: `#btnRefreshTargets`, `#txtTestStudioInputValue`

### 3. Class Selectors
Matches controls by their applied Avalonia styles and classes.
* Syntax: `.className`
* Mapping: Matches items in `Control.Classes`.
* **Important Rule:** Pseudo-classes (which start with a colon, such as `:pointerover`, `:pressed`, `:disabled`, `:focus`) are automatically stripped from the matching list to avoid checking transient visual states.
* Example: `.primary` (matches controls that have the `primary` class)

### 4. Compound Selectors
Combines type, ID, class, and attributes into a single match condition. All conditions must be satisfied.
* Syntax: `Tag#id.class[attribute=value]`
* Example: `Button#btnClickMe.primary[IsEnabled=true]`

### 5. Combinators
Allows querying elements based on their relationships in the tree hierarchy.
* **Descendant Selector (Space):** Matches elements that are descendants of a parent node at any nesting depth.
  * Example: `Grid Button` (finds any `Button` that is a descendant of a `Grid`)
* **Child Selector (`>`):** Matches elements that are direct children of a parent node.
  * Example: `StackPanel > Button` (finds a `Button` that is an immediate child of a `StackPanel`)

---

## Attribute Selectors

Attribute selectors allow you to filter controls based on their properties, layout metrics, and accessibility attributes.

```css
/* Examples of Attribute Selectors */
[IsEnabled=true]
[class~="primary"]
[AutomationProperties.AutomationId="txtInput"]
```

### Supported Attributes & Property Mappings

The table below lists all DOM attributes supported by the selector engine, how they map to Avalonia C# properties, their return value types, and descriptions:

| DOM Attribute | Avalonia Property Source | Type | Description |
| :--- | :--- | :--- | :--- |
| `type`, `nodeName`, `localName` | `Visual.GetType().Name` | String | The short C# type name of the control. |
| `FullType` | `Visual.GetType().FullName` | String | The fully qualified C# type name. |
| `id`, `Id`, `Name` | `Control.Name` | String | The control's unique Name (e.g., `#id` syntax mapping). |
| `class`, `Class` | `Control.Classes` | String | Space-separated list of applied classes (excluding pseudo-classes). |
| `Text`, `Content`, `Header` | Text helper methods | String | Extracted text from `TextBlock`, `TextBox`, `ContentControl`, etc. |
| `AccessibilityId`, `AutomationId`, `AutomationProperties.AutomationId`, `automation-id` | `AutomationProperties.AutomationId` | String | Stable automation identifier used for testing. |
| `AccessibilityName`, `AutomationName` | `AutomationProperties.Name` | String | The accessible name of the control. |
| `AccessibilityHelp`, `HelpText` | `AutomationProperties.HelpText` | String | Descriptive help text for screen readers. |
| `IsEnabled`, `enabled` | `Control.IsEnabled` | Boolean | Returns `"true"` or `"false"` (case-insensitive). |
| `IsVisible`, `visible` | `Control.IsVisible` | Boolean | Returns `"true"` or `"false"` depending on layout visibility. |
| `IsFocused`, `focused` | `Control.IsFocused` | Boolean | Returns `"true"` or `"false"` if focused. |
| `IsChecked`, `checked` | `ToggleButton.IsChecked` / Property | Boolean | Returns `"true"` or `"false"` (works on checkable controls). |
| `IsSelected`, `selected` | `IsSelected` Property | Boolean | Returns `"true"` or `"false"` (e.g. ListBox items). |
| `Value` | `Value` Property | String | Returns the control value as a string representation. |
| `SelectedIndex` | `SelectedIndex` Property | String | The selected index string representation (e.g., ComboBox). |
| `IsExpanded` | `IsExpanded` Property | Boolean | Returns `"true"` or `"false"` (e.g., Expander, TreeViewItem). |
| `SelectedDate` | `SelectedDate` Property | String | Selected date string representation. |
| `SelectedTime` | `SelectedTime` Property | String | Selected time string representation. |
| `PlaceholderText` | `PlaceholderText` Property | String | Watermark or placeholder string. |
| `Width` | `Control.Bounds.Width` | Rounded Int | Matches rounded width (formatted using invariant culture). |
| `Height` | `Control.Bounds.Height` | Rounded Int | Matches rounded height (formatted using invariant culture). |
| `Traits`, `traits` | Automatic heuristics | String | Space-separated flags: `"text"`, `"long-text"` (len >= 200), `"square"`. |
| `Bounds` | `Control.Bounds` | String | Formatted coordinates: `"X,Y,Width,Height"`. |

### Attribute Presence Selectors
You can query for the presence of an attribute without checking its value.
* Syntax: `[AttributeName]`
* Example: `[AutomationId]` matches any control that has a non-empty `AutomationProperties.AutomationId` set.
* Example: `[id]` matches any control with a non-empty `Name` property.

### Value Matching Operators
* **Exact Match (`=`):** Matches the exact property value.
  * Example: `[IsEnabled=true]`
* **Word Match (`~=`):** Checks if the space-separated list of words contains the specified word. This is particularly useful for checking classes.
  * Example: `[class~="primary"]`

### Unknown Attributes
If a selector uses an unknown attribute or one that does not map to any property on the target control (e.g., `[customAttr="value"]`), the engine will safely return `false` for the match. **Unknown attributes will never match arbitrary controls.**

---

## Pseudo-classes

The Selector Engine implements custom pseudo-classes to perform complex structural and content-based queries.

### `:contains("text")`
Matches elements whose visible text, content, or header contains the specified string (case-insensitive).
* Syntax: `:contains("Click Me")` or `:contains('Click Me')`
* See the [Recursion Rules](#recursion-rules-for-contains) section below for detailed behavior.

### `:nth-child(index)`
Matches elements based on their 1-based child index relative to their visual or logical parent.
* Syntax: `:nth-child(3)` (matches the 3rd child control of its parent)

---

## Recursion Rules for `:contains(...)`

To ensure fast lookup times and natural developer experience, the `:contains` pseudo-class has unique recursion rules governed by the presence of **other filters** in the selector token.

```
       :contains("Submit")               Button:contains("Submit")
     [ Non-Recursive Mode ]                 [ Recursive Mode ]
  Direct text properties only.       Checks Button and searches through
 (e.g. TextBlock.Text matches).      all visual children of the Button.
```

### 1. Non-Recursive Mode (No Other Filters)
If the `:contains(...)` query is used by itself (or generated via a raw text query like `"Submit"`), it runs in **non-recursive** mode.
* **Behavior:** The engine only matches elements that directly expose the text string on their own properties (such as `TextBlock.Text`, `TextBox.Text`, or a string `ContentControl.Content`). It does not search through children.
* **Use Case:** Highly efficient for finding the exact `TextBlock` or presenter rendering a specific string.

### 2. Recursive Mode (With Other Filters)
If `:contains(...)` is combined with other selectors (such as a tag name, class, ID, or attribute, e.g. `Button:contains("Submit")` or `.btn-group:contains("Save")`), it runs in **recursive** mode.
* **Behavior:** The engine checks if the control's own properties contain the text. If they do not, it recursively traverses all visual children of that control to check if any descendant contains the text.
* **Use Case:** Crucial for controls like `Button` in Avalonia, where the button control itself does not directly hold the text; instead, the text is nested inside a template child (like a `ContentPresenter` containing a `TextBlock`).

---

## Best Practices for Control Naming and Automation

When developing Avalonia applications that will be audited, tested, or controlled by automation scripts and AI agents, adhere to these structural design guidelines:

1. **Assign Stable Names (`Name`)**
   Give unique, semantic XAML names to interactive controls (buttons, text fields, tabs) that are targeted during automation.
   ```xml
   <!-- GOOD -->
   <Button Name="btnSubmit" Content="Submit Query"/>
   ```

2. **Expose Stable Automation IDs (`AutomationProperties.AutomationId`)**
   For test robustness across visual modifications, localization changes, and layout restructurings, always prefer assigning an `AutomationId`. This is the most stable selector for testing.
   ```xml
   <!-- RECOMMENDED -->
   <Button AutomationProperties.AutomationId="btnSubmitTransaction" Content="Submit"/>
   ```

3. **Avoid Deep Structural Selectors**
   Do not use brittle selectors that rely on deep nesting. If the visual structure is reorganized, these selectors will break.
   * **Brittle Selector (Avoid):** `Grid > Border > StackPanel > ContentPresenter > Button`
   * **Robust Selector (Prefer):** `Button#btnSubmit` or `[AutomationId="btnSubmitTransaction"]`

4. **Ensure Compiled Bindings are Enabled**
   All bindings in XAML views should use compiled bindings (`x:CompileBindings="True"`) and declare a concrete data type (`x:DataType="..."`). The use of `ReflectionBinding` or disabling compilation is forbidden because compiled bindings ensure proper assembly linking, high performance, and linker-trimming safety (`PublishTrimmed`).

5. **Use Vector Path Icons Over Emojis**
   Do not use raw emoji characters or text glyphs for visual buttons. Emojis can fail to render (showing broken glyph boxes) under custom system font configurations (such as the default Inter font). Instead, use vector path icons (Fluent UI system icons) inside a standard `PathIcon` control:
   ```xml
   <Button Name="btnPlay" Command="{Binding PlayCommand}">
       <StackPanel Orientation="Horizontal" Spacing="6">
           <PathIcon Data="{StaticResource PlayIcon}" Width="14" Height="14"/>
           <TextBlock Text="Play" VerticalAlignment="Center"/>
       </StackPanel>
   </Button>
   ```

---

## CDP Integration & Query Examples

DevTools clients and test runner scripts use the `DOM` domain to invoke query selectors. The request and response schemas align with standard Chrome DevTools Protocol specifications.

### 1. DOM.querySelector
Queries a single node in the tree using a selector.

#### Request Payload
```json
{
  "id": 42,
  "method": "DOM.querySelector",
  "params": {
    "nodeId": 1,
    "selector": "Button#btnRefreshTargets"
  }
}
```

#### Response Payload
```json
{
  "id": 42,
  "result": {
    "nodeId": 15
  }
}
```

### 2. DOM.querySelectorAll
Queries all matching nodes in the tree.

#### Request Payload
```json
{
  "id": 43,
  "method": "DOM.querySelectorAll",
  "params": {
    "nodeId": 1,
    "selector": "Button.primary[IsEnabled=true]"
  }
}
```

#### Response Payload
```json
{
  "id": 43,
  "result": {
    "nodeIds": [15, 23, 42]
  }
}
```
