---
title: CSS Domain
---

# CSS Domain

The `CSS` domain exposes Avalonia control styling information through the Chrome DevTools Protocol. It translates Avalonia properties—such as `Background`, `Margin`, `Padding`, `Width`, `Height`, `Opacity`, and `FontSize`—into CSS-compatible representations so that DevTools-based clients can inspect and live-edit control appearance at runtime.

## Overview

Unlike a web browser where CSS stylesheets drive layout, Avalonia uses a property system built on `AvaloniaProperty` and XAML styles. The CSS domain bridges this gap by:

1. **Reading** Avalonia property values and presenting them as CSS computed styles and inline styles.
2. **Writing** CSS property declarations back to live controls through reflection and the Avalonia property registry.
3. **Tracking** property changes and emitting `CSS.computedStyleUpdated` events to keep DevTools in sync.

All read and write operations are dispatched on the Avalonia UI thread via `Dispatcher.UIThread.InvokeAsync` to ensure thread safety.

## Enabling the Domain

Enable or disable CSS domain events:

```json
{ "id": 1, "method": "CSS.enable" }
```

```json
{ "id": 1, "result": {} }
```

```json
{ "id": 2, "method": "CSS.disable" }
```

```json
{ "id": 2, "result": {} }
```

## CSS.getComputedStyleForNode

Returns the final, resolved property values for a control—analogous to `window.getComputedStyle()` in a browser. The implementation reads the control's current property values and converts them into CSS name/value pairs.

### Request

```json
{
  "id": 10,
  "method": "CSS.getComputedStyleForNode",
  "params": {
    "nodeId": 42
  }
}
```

### Response

```json
{
  "id": 10,
  "result": {
    "computedStyle": [
      { "name": "width",            "value": "200px" },
      { "name": "height",           "value": "50px" },
      { "name": "display",          "value": "block" },
      { "name": "opacity",          "value": "1" },
      { "name": "margin",           "value": "8,8,8,8" },
      { "name": "background-color", "value": "#FF4CAF50" },
      { "name": "padding",          "value": "4,4,4,4" },
      { "name": "font-size",        "value": "14" },
      { "name": "font-family",      "value": "Segoe UI" }
    ]
  }
}
```

### How Properties are Collected

The internal `GetComputedStyles` method always emits `width` and `height` from the control's `Bounds`. For `Control` instances it additionally reads:

| CSS Property       | Avalonia Source              | Notes                                              |
|--------------------|------------------------------|----------------------------------------------------|
| `width`            | `Visual.Bounds.Width`        | Actual rendered width in pixels                    |
| `height`           | `Visual.Bounds.Height`       | Actual rendered height in pixels                   |
| `display`          | `Control.IsVisible`          | `"block"` when visible, `"none"` when hidden       |
| `opacity`          | `Control.Opacity`            | Decimal value 0–1                                  |
| `margin`           | `Control.Margin`             | Avalonia `Thickness` serialized as `L,T,R,B`       |
| `background-color` | `Background` property        | Read via reflection; only present if the property exists |
| `padding`          | `Padding` property           | Read via reflection; only present if the property exists |
| `font-size`        | `FontSize` property          | Read via reflection                                |
| `font-family`      | `FontFamily` property        | Read via reflection                                |

Properties like `Background`, `Padding`, `FontSize`, and `FontFamily` are read through the helper `GetControlProperty`, which looks up the named `AvaloniaProperty` in the control's property registry. This means any control type that registers these properties—`Button`, `TextBlock`, `Border`, etc.—will report them automatically.

## CSS.getMatchedStylesForNode

Returns the inline style declarations and any matching Avalonia style rules for a given node. This is the method DevTools calls to populate the **Styles** panel.

### Request

```json
{
  "id": 11,
  "method": "CSS.getMatchedStylesForNode",
  "params": {
    "nodeId": 42
  }
}
```

### Response

```json
{
  "id": 11,
  "result": {
    "inlineStyle": {
      "styleSheetId": "42",
      "cssProperties": [
        { "name": "width",      "value": "NaNpx",  "important": false, "implicit": false, "text": "width: NaNpx;",  "parsedOk": true, "disabled": false },
        { "name": "height",     "value": "NaNpx",  "important": false, "implicit": false, "text": "height: NaNpx;", "parsedOk": true, "disabled": false },
        { "name": "opacity",    "value": "1",       "important": false, "implicit": false, "text": "opacity: 1;",    "parsedOk": true, "disabled": false },
        { "name": "margin",     "value": "8,8,8,8", "important": false, "implicit": false, "text": "margin: 8,8,8,8;", "parsedOk": true, "disabled": false },
        { "name": "background", "value": "#FF4CAF50","important": false, "implicit": false, "text": "background: #FF4CAF50;", "parsedOk": true, "disabled": false },
        { "name": "padding",    "value": "4,4,4,4", "important": false, "implicit": false, "text": "padding: 4,4,4,4;", "parsedOk": true, "disabled": false }
      ],
      "shorthandEntries": [],
      "cssText": "width: NaNpx; height: NaNpx; opacity: 1; margin: 8,8,8,8; background: #FF4CAF50; padding: 4,4,4,4;",
      "range": { "startLine": 0, "startColumn": 0, "endLine": 0, "endColumn": 0 }
    },
    "matchedCSSRules": [
      {
        "origin": "regular",
        "selectorList": {
          "selectors": [ { "text": "Button.primary" } ],
          "text": "Button.primary"
        },
        "style": {
          "cssProperties": [
            { "name": "background", "value": "#FF2196F3", "important": false, "implicit": false, "text": "background: #FF2196F3;", "parsedOk": true, "disabled": false },
            { "name": "color",      "value": "#FFFFFFFF", "important": false, "implicit": false, "text": "color: #FFFFFFFF;",     "parsedOk": true, "disabled": false }
          ],
          "shorthandEntries": [],
          "cssText": "background: #FF2196F3; color: #FFFFFFFF;"
        }
      }
    ],
    "pseudoElements": [],
    "inherited": [],
    "cssKeyframesRules": []
  }
}
```

### Style Matching Pipeline

The `GetMatchedRules` method collects styles from three sources, in order of specificity:

1. **Application-wide styles** — from `Application.Current.Styles`
2. **Ancestor styles** — from each logical parent, outermost first
3. **Control's own styles** — from `control.Styles`

For each collected `Avalonia.Styling.Style`, the selector is tested against the control. If the selector matches, the style's `Setter` list is converted to CSS property declarations. Avalonia property names are converted to kebab-case CSS names through the `ToCssPropertyName` helper.

### Inline Style

The inline style represents the control's directly-set property values (analogous to the HTML `style` attribute). The `styleSheetId` field is set to the node ID as a string, which allows `CSS.setStyleTexts` to look up the control by ID when applying edits.

## CSS.setStyleTexts

Applies live property modifications to controls at runtime. This is the core method that enables real-time editing in the DevTools **Styles** panel.

### Request

```json
{
  "id": 12,
  "method": "CSS.setStyleTexts",
  "params": {
    "edits": [
      {
        "styleSheetId": "42",
        "text": "background: #FF9C27B0; opacity: 0.8; margin: 16px; padding: 8px 12px;"
      }
    ]
  }
}
```

### Response

```json
{
  "id": 12,
  "result": {
    "styles": [
      {
        "styleSheetId": "42",
        "cssProperties": [
          { "name": "width",      "value": "NaNpx",     "important": false, "implicit": false, "text": "width: NaNpx;",     "parsedOk": true, "disabled": false },
          { "name": "height",     "value": "NaNpx",     "important": false, "implicit": false, "text": "height: NaNpx;",    "parsedOk": true, "disabled": false },
          { "name": "opacity",    "value": "0.8",        "important": false, "implicit": false, "text": "opacity: 0.8;",     "parsedOk": true, "disabled": false },
          { "name": "margin",     "value": "16,16,16,16","important": false, "implicit": false, "text": "margin: 16,16,16,16;", "parsedOk": true, "disabled": false },
          { "name": "background", "value": "#FF9C27B0",  "important": false, "implicit": false, "text": "background: #FF9C27B0;", "parsedOk": true, "disabled": false },
          { "name": "padding",    "value": "12,8,12,8",  "important": false, "implicit": false, "text": "padding: 12,8,12,8;", "parsedOk": true, "disabled": false }
        ],
        "shorthandEntries": [],
        "cssText": "width: NaNpx; height: NaNpx; opacity: 0.8; margin: 16,16,16,16; background: #FF9C27B0; padding: 12,8,12,8;",
        "range": { "startLine": 0, "startColumn": 0, "endLine": 0, "endColumn": 0 }
      }
    ]
  }
}
```

### How Style Text is Applied

The `ApplyStyleText` method parses the CSS text into semicolon-separated declarations. For each `property: value;` pair it:

1. **Strips the `px` suffix** from single-value numeric properties.
2. **Identifies thickness properties** (`margin`, `padding`, `border-width`) and routes them through the thickness parser.
3. **Maps CSS names to Avalonia property names** using a lookup table.
4. **Calls `SetControlProperty`** to apply the converted value.

## Avalonia-to-CSS Property Mapping

The following table shows how CSS property names map to Avalonia properties, both when reading (computed/inline styles) and when writing (via `setStyleTexts`).

### Reading: Avalonia → CSS

| Avalonia Property     | CSS Property      | Conversion                              |
|-----------------------|-------------------|-----------------------------------------|
| `Background`          | `background`      | `IBrush.ToString()` → color string      |
| `Foreground`          | `color`           | `IBrush.ToString()` → color string      |
| `Padding`             | `padding`         | `Thickness.ToString()` → `L,T,R,B`     |
| `Margin`              | `margin`          | `Thickness.ToString()` → `L,T,R,B`     |
| `BorderThickness`     | `border-width`    | `Thickness.ToString()` → `L,T,R,B`     |
| `BorderBrush`         | `border-color`    | `IBrush.ToString()` → color string      |
| `FontSize`            | `font-size`       | `double.ToString()`                     |
| `FontFamily`          | `font-family`     | `FontFamily.ToString()`                 |
| `FontWeight`          | `font-weight`     | `FontWeight.ToString()`                 |
| `Opacity`             | `opacity`         | `double.ToString()` (0–1)              |
| `IsVisible`           | `display`         | `true` → `"block"`, `false` → `"none"` |
| `Bounds.Width`        | `width`           | Rendered pixel width                    |
| `Bounds.Height`       | `height`          | Rendered pixel height                   |

The `ToCssPropertyName` method handles well-known names first, then falls back to converting PascalCase to kebab-case (e.g., `CornerRadius` → `corner-radius`).

### Writing: CSS → Avalonia

| CSS Property       | Avalonia Property   | Value Conversion                             |
|--------------------|---------------------|----------------------------------------------|
| `width`            | `Width`             | Strip `px`, parse to `double`                |
| `height`           | `Height`            | Strip `px`, parse to `double`                |
| `opacity`          | `Opacity`           | Parse to `double`                            |
| `background`       | `Background`        | `Brush.Parse(value)` → `IBrush`              |
| `background-color` | `Background`        | `Brush.Parse(value)` → `IBrush`              |
| `font-size`        | `FontSize`          | Strip `px`, parse to `double`                |
| `font-family`      | `FontFamily`        | `TypeConverter` → `FontFamily`               |
| `margin`           | `Margin`            | Parse CSS shorthand → `Thickness`            |
| `padding`          | `Padding`           | Parse CSS shorthand → `Thickness`            |
| `margin-top`       | `Margin` (Top)      | Modify single component of existing Thickness |
| `padding-left`     | `Padding` (Left)    | Modify single component of existing Thickness |
| `border-width`     | `BorderThickness`   | Parse CSS shorthand → `Thickness`            |

Any property name not in the explicit mapping table is passed through as-is to the Avalonia property registry, so arbitrary Avalonia properties can also be set by name.

## Property Resolution with Reflection

The `SetControlProperty` method uses a two-tier strategy:

### 1. Avalonia Dependency Property Lookup

```csharp
var avProperty = AvaloniaPropertyRegistry.Instance.GetRegistered(control)
    .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

if (avProperty != null)
{
    var converted = ConvertValue(valueStr, avProperty.PropertyType);
    control.SetValue(avProperty, converted);
}
```

This is the preferred path. It queries the `AvaloniaPropertyRegistry` for a registered property matching the given name (case-insensitive). If found, the string value is converted to the property's target type and applied through `Control.SetValue`.

### 2. CLR Property Reflection Fallback

```csharp
var clrProperty = control.GetType().GetProperty(name,
    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

if (clrProperty != null && clrProperty.CanWrite)
{
    var converted = ConvertValue(valueStr, clrProperty.PropertyType);
    clrProperty.SetValue(control, converted);
}
```

If no registered `AvaloniaProperty` is found, the method falls back to standard CLR reflection. This allows setting properties that are not registered as dependency properties.

### Value Conversion

The `ConvertValue` method handles type conversion from CSS string values to .NET types:

| Target Type   | Conversion Strategy                          |
|---------------|----------------------------------------------|
| `string`      | Returned as-is                               |
| `double`      | `double.Parse` with `InvariantCulture`       |
| `float`       | `float.Parse` with `InvariantCulture`        |
| `int`         | `int.Parse` with `InvariantCulture`          |
| `bool`        | `bool.Parse`                                 |
| `Thickness`   | `Thickness.Parse`                            |
| `IBrush`      | `Brush.Parse` (handles hex colors, named colors) |
| Enum types    | `Enum.Parse` (case-insensitive)              |
| Other types   | `TypeDescriptor.GetConverter` → `ConvertFromInvariantString` |

## Thickness Parsing (Margin, Padding, Border)

CSS shorthand values for `margin`, `padding`, and `border-width` follow CSS box model conventions and are parsed into Avalonia `Thickness` values:

| CSS Value          | Interpretation                        | Avalonia Thickness (L, T, R, B) |
|--------------------|---------------------------------------|---------------------------------|
| `8px`              | All sides equal                       | `8, 8, 8, 8`                   |
| `8px 16px`         | Vertical / Horizontal                 | `16, 8, 16, 8`                 |
| `8px 16px 24px`    | Top / Horizontal / Bottom             | `16, 8, 16, 24`                |
| `8px 16px 24px 32px` | Top / Right / Bottom / Left         | `32, 8, 16, 24`                |

Individual thickness components (`margin-top`, `padding-left`, etc.) modify only the specified side of the existing `Thickness` value, preserving the other components.

## Live Editing Examples

### Changing Background Color

```json
{
  "id": 20,
  "method": "CSS.setStyleTexts",
  "params": {
    "edits": [{
      "styleSheetId": "42",
      "text": "background: #FFFF5722;"
    }]
  }
}
```

This calls `Brush.Parse("#FFFF5722")` and sets the control's `Background` property to a deep-orange solid color brush.

### Changing Foreground Color

Since `Foreground` maps from the `color` CSS property, use the Avalonia property name directly:

```json
{
  "id": 21,
  "method": "CSS.setStyleTexts",
  "params": {
    "edits": [{
      "styleSheetId": "42",
      "text": "Foreground: #FFFFFFFF;"
    }]
  }
}
```

### Adjusting Margin with CSS Shorthand

```json
{
  "id": 22,
  "method": "CSS.setStyleTexts",
  "params": {
    "edits": [{
      "styleSheetId": "42",
      "text": "margin: 10px 20px;"
    }]
  }
}
```

This sets the control's `Margin` to `Thickness(20, 10, 20, 10)` (horizontal=20, vertical=10).

### Adjusting Individual Padding Sides

```json
{
  "id": 23,
  "method": "CSS.setStyleTexts",
  "params": {
    "edits": [{
      "styleSheetId": "42",
      "text": "padding-top: 16px; padding-left: 24px;"
    }]
  }
}
```

Only the top and left components of the existing `Padding` are modified; the right and bottom values remain unchanged.

### Changing Opacity

```json
{
  "id": 24,
  "method": "CSS.setStyleTexts",
  "params": {
    "edits": [{
      "styleSheetId": "42",
      "text": "opacity: 0.5;"
    }]
  }
}
```

Sets `Control.Opacity` to `0.5`, making the control semi-transparent.

### Hiding a Control

```json
{
  "id": 25,
  "method": "CSS.setStyleTexts",
  "params": {
    "edits": [{
      "styleSheetId": "42",
      "text": "IsVisible: false;"
    }]
  }
}
```

Since `display: none` does not directly map to an Avalonia property, use the Avalonia property name `IsVisible` directly. The value is parsed as a `bool` via the property registry.

### Resizing a Control

```json
{
  "id": 26,
  "method": "CSS.setStyleTexts",
  "params": {
    "edits": [{
      "styleSheetId": "42",
      "text": "width: 300px; height: 100px;"
    }]
  }
}
```

The `px` suffix is stripped automatically, and the resulting numeric values are set on the `Width` and `Height` Avalonia properties.

## Additional Methods

### CSS.getInlineStylesForNode

Returns the inline style for a node along with an empty attributes style. Useful for clients that request inline and attribute styles separately.

```json
{ "id": 30, "method": "CSS.getInlineStylesForNode", "params": { "nodeId": 42 } }
```

### CSS.getBackgroundColors

Returns the background color, computed font size, and font weight for a node. Used by DevTools for contrast ratio checks.

```json
{ "id": 31, "method": "CSS.getBackgroundColors", "params": { "nodeId": 42 } }
```

```json
{
  "id": 31,
  "result": {
    "backgroundColors": ["#FF4CAF50"],
    "computedFontSize": "14px",
    "computedFontWeight": "Normal"
  }
}
```

### CSS.getPlatformFontsForNode

Returns the platform font information for a node. Reads the control's `FontFamily` property.

```json
{ "id": 32, "method": "CSS.getPlatformFontsForNode", "params": { "nodeId": 42 } }
```

```json
{
  "id": 32,
  "result": {
    "fonts": [
      {
        "familyName": "Segoe UI",
        "postScriptName": "Segoe UI",
        "isCustomFont": false,
        "glyphCount": 1
      }
    ]
  }
}
```

### CSS.forcePseudoState

Forces pseudo-class states on a control. Maps browser pseudo-classes to Avalonia pseudo-classes:

| CSS Pseudo-class | Avalonia Classes Added      |
|------------------|-----------------------------|
| `hover`          | `hover`, `pointerover`      |
| `active`         | `active`, `pressed`         |
| `focus`          | `focus`                     |
| `focus-within`   | `focus-within`              |
| `focus-visible`  | `focus-visible`             |

```json
{
  "id": 33,
  "method": "CSS.forcePseudoState",
  "params": {
    "nodeId": 42,
    "forcedPseudoClasses": ["hover", "focus"]
  }
}
```

### CSS.collectClassNames

Returns all Avalonia style classes and pseudo-classes assigned to a control.

```json
{ "id": 34, "method": "CSS.collectClassNames", "params": { "styleSheetId": "42" } }
```

```json
{
  "id": 34,
  "result": {
    "classNames": ["primary", "accent", ":pointerover"]
  }
}
```

### CSS.getLonghandProperties

Expands a CSS shorthand property into its longhand components. Supports `margin` and `padding` shorthands.

```json
{
  "id": 35,
  "method": "CSS.getLonghandProperties",
  "params": {
    "shorthandName": "margin",
    "value": "8px 16px"
  }
}
```

```json
{
  "id": 35,
  "result": {
    "longhandProperties": [
      { "name": "margin-top",    "value": "8px",  "important": false, "implicit": false, "text": "margin-top: 8px;",    "parsedOk": true, "disabled": false },
      { "name": "margin-right",  "value": "16px", "important": false, "implicit": false, "text": "margin-right: 16px;", "parsedOk": true, "disabled": false },
      { "name": "margin-bottom", "value": "8px",  "important": false, "implicit": false, "text": "margin-bottom: 8px;", "parsedOk": true, "disabled": false },
      { "name": "margin-left",   "value": "16px", "important": false, "implicit": false, "text": "margin-left: 16px;",  "parsedOk": true, "disabled": false }
    ]
  }
}
```

### CSS.getAnimatedStylesForNode

Returns transition information for controls that have `Transitions` defined. Avalonia `TransitionBase` objects are converted to CSS `transition-property` and `transition-duration` declarations.

```json
{ "id": 36, "method": "CSS.getAnimatedStylesForNode", "params": { "nodeId": 42 } }
```

```json
{
  "id": 36,
  "result": {
    "animationStyles": [],
    "transitionsStyle": {
      "styleSheetId": "42",
      "cssProperties": [
        { "name": "transition-property", "value": "opacity, background", "important": false, "implicit": false, "text": "transition-property: opacity, background;", "parsedOk": true, "disabled": false },
        { "name": "transition-duration", "value": "0.3s, 0.5s",         "important": false, "implicit": false, "text": "transition-duration: 0.3s, 0.5s;",        "parsedOk": true, "disabled": false }
      ],
      "shorthandEntries": [],
      "cssText": "transition-property: opacity, background; transition-duration: 0.3s, 0.5s;"
    },
    "inherited": []
  }
}
```

### CSS.resolveValues

Resolves CSS unit values (such as `em` and `calc()`) to pixel values using the node's computed font size.

```json
{
  "id": 37,
  "method": "CSS.resolveValues",
  "params": {
    "nodeId": 42,
    "values": ["2em", "calc(100px + 2em)"]
  }
}
```

```json
{
  "id": 37,
  "result": {
    "results": ["28px", "128px"]
  }
}
```

### CSS.getEnvironmentVariables

Returns environment-level CSS variables, including the device pixel ratio from the primary window's `RenderScaling`.

```json
{ "id": 38, "method": "CSS.getEnvironmentVariables" }
```

```json
{
  "id": 38,
  "result": {
    "environmentVariables": {
      "device-pixel-ratio": "2",
      "safe-area-inset-top": "0px",
      "safe-area-inset-right": "0px",
      "safe-area-inset-bottom": "0px",
      "safe-area-inset-left": "0px"
    }
  }
}
```

## Computed Style Update Tracking

The CSS domain supports two tracking mechanisms for notifying clients when control properties change.

### Batch Tracking

Use `CSS.trackComputedStyleUpdates` to start tracking and `CSS.takeComputedStyleUpdates` to poll for changed node IDs:

```json
{ "id": 40, "method": "CSS.trackComputedStyleUpdates", "params": { "propertiesToTrack": [{ "name": "opacity" }] } }
```

```json
{ "id": 41, "method": "CSS.takeComputedStyleUpdates" }
```

```json
{
  "id": 41,
  "result": {
    "nodeIds": [42, 55, 78]
  }
}
```

### Event-Based Tracking

Use `CSS.trackComputedStyleUpdatesForNode` to receive `CSS.computedStyleUpdated` events whenever a specific node's properties change:

```json
{ "id": 42, "method": "CSS.trackComputedStyleUpdatesForNode", "params": { "nodeId": 42 } }
```

When the tracked node's properties change, the server sends:

```json
{
  "method": "CSS.computedStyleUpdated",
  "params": {
    "nodeId": 42
  }
}
```

## Stub Methods

The following methods are accepted but return empty or minimal responses for protocol compatibility. They exist so that DevTools clients do not encounter errors when issuing these requests:

- `CSS.createStyleSheet` — returns a default stylesheet ID
- `CSS.setStyleSheetText` — applies the text as style edits to the referenced node
- `CSS.addRule` — applies the rule text as style edits to the referenced node
- `CSS.getStyleSheetText` — returns the inline style text for the referenced node
- `CSS.getLocationForSelector` — returns ranges for the `"element"` selector
- `CSS.getLayersForNode` — returns a single default layer
- `CSS.getMediaQueries` — returns an empty media list
- `CSS.startRuleUsageTracking` / `CSS.stopRuleUsageTracking` — no-op; returns empty rule usage
- `CSS.takeCoverageDelta` — returns empty coverage data
- `CSS.forceStartingStyle`, `CSS.setLocalFontsEnabled`, `CSS.setContainerQueryConditionText`, `CSS.setContainerQueryText`, `CSS.setEffectivePropertyValueForNode`, `CSS.setKeyframeKey`, `CSS.setMediaText`, `CSS.setNavigationText`, `CSS.setPropertyRulePropertyName`, `CSS.setRuleSelector`, `CSS.setScopeText`, `CSS.setSupportsText` — all return empty objects

## Reading Properties: GetControlProperty

The `GetControlProperty` helper reads a property value from a control by querying the `AvaloniaPropertyRegistry`:

```csharp
public static object? GetControlProperty(Control control, string name)
{
    var avProperty = AvaloniaPropertyRegistry.Instance.GetRegistered(control)
        .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (avProperty != null)
    {
        return control.GetValue(avProperty);
    }
    return null;
}
```

This is used internally by `GetComputedStyles`, `GetInlineStyle`, `GetBackgroundColors`, and other methods to read property values like `Background`, `Padding`, `FontSize`, and `FontFamily` from any Avalonia control type.

## Session Lifecycle

The CSS domain maintains per-session tracking state in concurrent dictionaries. When a CDP session ends, `CleanupSession` removes all tracking state for that session to prevent memory leaks:

```csharp
public static void CleanupSession(CdpSession session)
{
    _sessionTrackedComputedNodes.TryRemove(session, out _);
    _sessionUpdatedComputedStyleNodes.TryRemove(session, out _);
    _sessionSingleTrackedNode.TryRemove(session, out _);
}
```
