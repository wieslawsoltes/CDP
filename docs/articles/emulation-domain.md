---
title: Emulation Domain
---

# Emulation Domain

The Emulation domain enables runtime simulation of different device characteristics, color schemes, and locale settings. It provides a programmatic interface for responsive design testing, theme switching, and internationalization verification without modifying application code.

## Overview

The Emulation domain maps standard Chrome DevTools emulation methods to Avalonia-native APIs:

| CDP Method | Avalonia Effect |
|---|---|
| `setDeviceMetricsOverride` | Resizes the target window |
| `clearDeviceMetricsOverride` | Restores original window size |
| `setEmulatedColorSchemeOverride` | Switches `RequestedThemeVariant` |
| `setEmulatedMedia` | Sets `prefers-color-scheme` feature |
| `setLocaleOverride` | Changes `CultureInfo` for the application |

## Methods

### Emulation.setDeviceMetricsOverride

Resizes the target application window to simulate different viewport dimensions. The original window size is preserved and can be restored later.

**Request:**

```json
{
  "id": 1,
  "method": "Emulation.setDeviceMetricsOverride",
  "params": {
    "width": 375,
    "height": 812,
    "deviceScaleFactor": 0,
    "mobile": false
  }
}
```

**Response:**

```json
{
  "id": 1,
  "result": {}
}
```

The method stores the original window dimensions in a `ConditionalWeakTable` keyed by CDP session, ensuring each session can independently manage its viewport override.

**Common viewport presets:**

| Device | Width | Height |
|--------|-------|--------|
| iPhone SE | 375 | 667 |
| iPhone 14 Pro | 393 | 852 |
| iPad | 768 | 1024 |
| Desktop HD | 1920 | 1080 |
| Desktop 4K | 3840 | 2160 |
| Compact | 320 | 480 |

### Emulation.clearDeviceMetricsOverride

Restores the window to its original size before `setDeviceMetricsOverride` was called.

**Request:**

```json
{
  "id": 2,
  "method": "Emulation.clearDeviceMetricsOverride"
}
```

If no override was previously set, the method is a no-op.

### Emulation.setEmulatedColorSchemeOverride

Switches the Avalonia application's theme variant between dark, light, and system default.

**Request (dark mode):**

```json
{
  "id": 3,
  "method": "Emulation.setEmulatedColorSchemeOverride",
  "params": {
    "colorScheme": "dark"
  }
}
```

**Request (light mode):**

```json
{
  "id": 4,
  "method": "Emulation.setEmulatedColorSchemeOverride",
  "params": {
    "colorScheme": "light"
  }
}
```

**Request (system default):**

```json
{
  "id": 5,
  "method": "Emulation.setEmulatedColorSchemeOverride",
  "params": {
    "colorScheme": ""
  }
}
```

The method sets `Application.Current.RequestedThemeVariant` to `ThemeVariant.Dark`, `ThemeVariant.Light`, or `ThemeVariant.Default` respectively.

### Emulation.setEmulatedMedia

Sets media feature overrides. Currently supports `prefers-color-scheme` for theme switching:

**Request:**

```json
{
  "id": 6,
  "method": "Emulation.setEmulatedMedia",
  "params": {
    "features": [
      {
        "name": "prefers-color-scheme",
        "value": "dark"
      }
    ]
  }
}
```

This provides compatibility with Chrome DevTools frontend, which uses `setEmulatedMedia` for color scheme changes.

### Emulation.setLocaleOverride

Changes the application's culture and locale settings at runtime, affecting number formatting, date display, and text direction:

**Request:**

```json
{
  "id": 7,
  "method": "Emulation.setLocaleOverride",
  "params": {
    "locale": "de-DE"
  }
}
```

**Behavior:**

1. Creates a `CultureInfo` from the specified locale string
2. Sets `CurrentCulture`, `CurrentUICulture`, `DefaultThreadCurrentCulture`, and `DefaultThreadCurrentUICulture`
3. Invalidates all window visuals to trigger re-rendering with the new locale

**Reset to system locale:**

```json
{
  "id": 8,
  "method": "Emulation.setLocaleOverride",
  "params": {
    "locale": ""
  }
}
```

When an empty string is passed, the locale reverts to `CultureInfo.InstalledUICulture`.

**Common locale codes:**

| Locale | Language / Region |
|--------|-------------------|
| `en-US` | English (United States) |
| `de-DE` | German (Germany) |
| `ja-JP` | Japanese (Japan) |
| `ar-SA` | Arabic (Saudi Arabia) — RTL |
| `zh-CN` | Chinese (Simplified) |
| `fr-FR` | French (France) |

## Usage Scenarios

### Responsive Design Testing

Test how your UI adapts to different viewport sizes:

```json
// Test mobile layout
{"id": 1, "method": "Emulation.setDeviceMetricsOverride", "params": {"width": 375, "height": 667}}
{"id": 2, "method": "Page.captureScreenshot"}

// Test tablet layout
{"id": 3, "method": "Emulation.setDeviceMetricsOverride", "params": {"width": 768, "height": 1024}}
{"id": 4, "method": "Page.captureScreenshot"}

// Restore original size
{"id": 5, "method": "Emulation.clearDeviceMetricsOverride"}
```

### Theme Verification

Verify that both dark and light themes render correctly:

```json
{"id": 1, "method": "Emulation.setEmulatedColorSchemeOverride", "params": {"colorScheme": "dark"}}
{"id": 2, "method": "Page.captureScreenshot"}

{"id": 3, "method": "Emulation.setEmulatedColorSchemeOverride", "params": {"colorScheme": "light"}}
{"id": 4, "method": "Page.captureScreenshot"}
```

### Internationalization Testing

Verify locale-dependent formatting:

```json
{"id": 1, "method": "Emulation.setLocaleOverride", "params": {"locale": "de-DE"}}
{"id": 2, "method": "Runtime.evaluate", "params": {"expression": "document.querySelector(\"#lblPrice\").textContent"}}
// Expected: "1.234,56 €" (German number format)

{"id": 3, "method": "Emulation.setLocaleOverride", "params": {"locale": "en-US"}}
{"id": 4, "method": "Runtime.evaluate", "params": {"expression": "document.querySelector(\"#lblPrice\").textContent"}}
// Expected: "$1,234.56" (US number format)
```

## Inspector Integration

The Simulation panel in the Inspector provides UI controls for the Emulation domain:
- **Theme toggle** — Switches between Dark, Light, and System theme variants
- **Viewport presets** — Quick buttons for common device sizes
- **Locale selector** — Dropdown for testing different cultures

## Next Steps

- [Simulation Panel](/articles/simulation-panel) — UI controls for emulation
- [Window Chrome Domain](/articles/window-chrome-domain) — Window state management
- [Page Domain](/articles/page-domain) — Screenshots for visual verification
