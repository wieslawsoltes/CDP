---
title: YAML Test Format
---

# YAML Test Format

The CDP Inspector uses a YAML-based test flow format (`.flow.yaml`) for defining, saving, and sharing automated UI test sequences. This format is human-readable, version-control friendly, and supports over 50 action types organized into 9 categories.

## File Structure

A `.flow.yaml` file has the following top-level structure:

```yaml
appId: "CdpSampleApp"
description: "Login flow smoke test"
tags:
  - smoke
  - login
env:
  BASE_URL: "http://127.0.0.1:9222"
steps:
  - action: launchApp
    value: "dotnet run --project samples/CdpSampleApp"
  - action: delay
    value: "2000"
  - action: tap
    selector: "#btnLogin"
  - action: inputText
    selector: "#txtUsername"
    value: "admin"
  - action: assertVisible
    selector: "#lblWelcome"
```

### Top-Level Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `appId` | string | No | Identifier for the target application |
| `description` | string | No | Human-readable description of the test flow |
| `tags` | string[] | No | Tags for filtering and organization |
| `env` | map | No | Default environment variables for variable substitution |
| `steps` | Step[] | Yes | Ordered list of test steps |

## Step Properties

Each step in the `steps` list is a YAML mapping with the following properties:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `action` | string | Yes | The action type to execute |
| `selector` | string/map | Depends | CSS selector or structured selector map |
| `value` | string | Depends | Input value, assertion value, or configuration |
| `id` | string | No | Optional unique identifier for the step |
| `label` | string | No | Human-readable label for reports |
| `status` | string | No | Execution result: `pending`, `running`, `passed`, `failed` |
| `error` | string | No | Error message if the step failed |

## Selector Syntax

Selectors can be specified as a simple string or a structured map with advanced targeting:

### Simple String Selector

```yaml
- action: tap
  selector: "#btnSubmit"
```

### Structured Selector Map

```yaml
- action: tap
  selector:
    text: "Submit"
    enabled: true
```

### Selector Keys

| Key | Description | Example |
|-----|-------------|---------|
| `text` | Match by visible text content | `text: "Submit"` |
| `id` | Match by control Name/ID | `id: "btnSubmit"` |
| `index` | Match by index among siblings | `index: 0` |
| `point` | Match by screen coordinates | `point: "100,200"` |
| `css` | Match by CSS selector | `css: "#panel > Button"` |
| `above` | Element above a reference | `above: "#footer"` |
| `below` | Element below a reference | `below: "#header"` |
| `leftOf` | Element to the left of a reference | `leftOf: "#sidebar"` |
| `rightOf` | Element to the right of a reference | `rightOf: "#sidebar"` |
| `containsChild` | Element containing a child match | `containsChild: "TextBlock"` |
| `childOf` | Element that is a child of | `childOf: "#panel"` |
| `containsDescendants` | Element containing descendant matches | `containsDescendants: "Button"` |
| `traits` | Match by element traits | `traits: "text"` |
| `enabled` | Filter by enabled state | `enabled: true` |
| `checked` | Filter by checked state | `checked: true` |
| `focused` | Filter by focus state | `focused: true` |
| `selected` | Filter by selection state | `selected: true` |
| `width` | Match by element width | `width: 200` |
| `height` | Match by element height | `height: 48` |
| `tolerance` | Position/size matching tolerance | `tolerance: 5` |

**Trait values:**
- `text` — Element contains text content
- `long-text` — Element contains 200+ characters of text
- `square` — Element has aspect ratio within 3%

---

## Action Types Reference

### Interactions

#### `tapOn` / `tap`

Tap (click) an element by selector or point:

```yaml
- action: tapOn
  selector: "#btnSubmit"
```

With advanced parameters:

```yaml
- action: tapOn
  selector:
    text: "Submit"
    retryTapIfNoChange: true
    waitToSettleTimeoutMs: 5000
  repeat: 2
  delay: 500
```

| Parameter | Description |
|-----------|-------------|
| `repeat` | Number of times to repeat the tap |
| `delay` | Delay between repeated taps (ms) |
| `retryTapIfNoChange` | Retry if the UI doesn't change after tap |
| `waitToSettleTimeoutMs` | Wait for UI to settle before acting |

#### `doubleTapOn` / `doubleTap`

Double-tap an element:

```yaml
- action: doubleTapOn
  selector: "#listItem"
```

#### `longPressOn` / `longPress`

Long-press (press and hold) an element:

```yaml
- action: longPressOn
  selector: "#contextTarget"
```

#### `scroll`

Scroll the current view:

```yaml
- action: scroll
  direction: DOWN
  amount: 300
  selector: "#scrollViewer"
```

| Parameter | Values |
|-----------|--------|
| `direction` | `DOWN`, `UP`, `LEFT`, `RIGHT` |
| `amount` | Scroll distance in pixels |
| `selector` | Target element to scroll |

#### `scrollUntilVisible`

Scroll until a target element becomes visible:

```yaml
- action: scrollUntilVisible
  selector:
    text: "Item 50"
  direction: DOWN
  timeout: 30000
  speed: 40
  visibilityPercentage: 80
  centerElement: true
```

| Parameter | Description |
|-----------|-------------|
| `element` | Target element selector |
| `direction` | Scroll direction |
| `timeout` | Maximum wait time (ms) |
| `speed` | Scroll speed (pixels per frame) |
| `visibilityPercentage` | Required visible percentage (0-100) |
| `centerElement` | Scroll to center the element |

#### `swipe`

Swipe gesture by direction, coordinates, or element:

```yaml
- action: swipe
  start: "100,500"
  end: "100,100"
  duration: 300
```

Or by direction:

```yaml
- action: swipe
  direction: LEFT
  from:
    text: "Swipeable Card"
```

| Parameter | Description |
|-----------|-------------|
| `start` | Start coordinates (`x,y`) |
| `end` | End coordinates (`x,y`) |
| `direction` | Swipe direction |
| `from` | Element to start from |
| `duration` | Swipe duration in ms |
| `waitToSettleTimeoutMs` | Wait timeout after swipe |

---

### Input

#### `inputText` / `input`

Type text into the focused element or a specific control:

```yaml
- action: inputText
  selector: "#txtEmail"
  value: "user@example.com"
```

#### `eraseText` / `erase`

Erase characters from the focused field:

```yaml
- action: eraseText
  characters: 10
```

#### `pressKey`

Press a hardware or keyboard key:

```yaml
- action: pressKey
  key: enter
```

**Supported keys:** `home`, `lock`, `enter`, `backspace`, `volume up`, `volume down`, `back`, `escape`, `tab`, `space`, `delete`, `end`

#### `copyTextFrom` / `copy`

Copy text from an element to clipboard:

```yaml
- action: copyTextFrom
  selector: "#lblResult"
```

#### `pasteText` / `paste`

Paste clipboard text into the focused element:

```yaml
- action: pasteText
```

#### `clearText` / `clear`

Clear text in an element:

```yaml
- action: clearText
  selector: "#txtSearch"
```

#### `setClipboard`

Set clipboard text content:

```yaml
- action: setClipboard
  value: "Clipboard content"
```

#### `hideKeyboard`

Dismiss the software keyboard:

```yaml
- action: hideKeyboard
```

#### Random Input Generators

Generate randomized test data:

```yaml
- action: inputRandomEmail        # Random email address
- action: inputRandomPersonName   # Random person name
- action: inputRandomNumber       # Random number
  length: 8
- action: inputRandomText         # Random text string
  length: 20
- action: inputRandomCityName     # Random city name
- action: inputRandomCountryName  # Random country name
- action: inputRandomColorName    # Random color name
```

---

### Assertions

#### `assertVisible`

Assert an element is visible:

```yaml
- action: assertVisible
  selector: "#lblWelcome"
```

#### `assertNotVisible`

Assert an element is not visible:

```yaml
- action: assertNotVisible
  selector: "#errorBanner"
```

#### `assertTrue`

Assert a C# expression evaluates to true:

```yaml
- action: assertTrue
  value: "Window.DataContext.IsLoggedIn == true"
```

#### `assertFalse`

Assert a C# expression evaluates to false:

```yaml
- action: assertFalse
  value: "Window.DataContext.HasErrors"
```

#### `assertScreenshot`

Compare the current screenshot with a reference image:

```yaml
- action: assertScreenshot
  path: "reference/login-page.png"
  cropOn: "#mainPanel"
  thresholdPercentage: 5
  label: "Login page visual check"
```

| Parameter | Description |
|-----------|-------------|
| `path` | Reference image path |
| `cropOn` | Crop to element selector |
| `thresholdPercentage` | Allowed pixel difference (%) |
| `label` | Report label |

#### `extendedWaitUntil`

Wait for visible or not-visible state with a custom timeout:

```yaml
- action: extendedWaitUntil
  visible:
    text: "Loading complete"
  timeout: 30000
```

Or wait for disappearance:

```yaml
- action: extendedWaitUntil
  notVisible:
    text: "Loading..."
  timeout: 15000
```

#### `waitForAnimationToEnd`

Wait for the UI to settle (no visual changes):

```yaml
- action: waitForAnimationToEnd
  timeout: 15000
```

---

### AI

#### `assertWithAI`

Run an AI visual assertion:

```yaml
- action: assertWithAI
  assertion: "The login form should display a username and password field"
  optional: false
```

#### `assertNoDefectsWithAI`

Run an AI visual-defect assertion:

```yaml
- action: assertNoDefectsWithAI
  optional: true
```

#### `extractTextWithAI`

Extract text from the screen using AI:

```yaml
- action: extractTextWithAI
  query: "What is the total price shown?"
  outputVariable: TOTAL_PRICE
  optional: false
```

---

### Navigation

#### `back`

Navigate back:

```yaml
- action: back
```

#### `openLink`

Open a URL or deep link:

```yaml
- action: openLink
  link: "https://example.com/dashboard"
  autoVerify: true
```

---

### Logic

#### `repeat`

Repeat nested commands:

```yaml
- action: repeat
  times: 5
  commands:
    - action: tapOn
      selector: "#btnIncrement"
    - action: delay
      value: "500"
```

With while condition:

```yaml
- action: repeat
  while:
    visible:
      text: "Next"
  commands:
    - action: tapOn
      selector:
        text: "Next"
```

#### `retry`

Retry nested commands on failure:

```yaml
- action: retry
  maxRetries: 3
  commands:
    - action: tapOn
      selector: "#btnSubmit"
    - action: assertVisible
      selector: "#successMessage"
```

Or retry from a file:

```yaml
- action: retry
  maxRetries: 3
  file: "flows/fragile-step.flow.yaml"
```

#### `runFlow`

Run a subflow file or inline command list:

```yaml
- action: runFlow
  file: "flows/login.flow.yaml"
  label: "Login subflow"
  env:
    USERNAME: "admin"
```

With conditional execution:

```yaml
- action: runFlow
  file: "flows/setup.flow.yaml"
  when:
    visible:
      text: "Setup Required"
```

#### `runScript`

Run an external script file:

```yaml
- action: runScript
  file: "scripts/prepare-data.csx"
  env:
    DATA_PATH: "/tmp/test-data"
```

#### `evalScript`

Evaluate an inline script expression:

```yaml
- action: evalScript
  value: "Console.WriteLine(\"Step reached\")"
```

---

### App & Device

#### `launchApp`

Launch an app with optional configuration:

```yaml
- action: launchApp
  appId: "com.example.myapp"
  path: "dotnet run --project MyApp"
  arguments: "--port 9222"
  clearState: true
  stopApp: true
```

| Parameter | Description |
|-----------|-------------|
| `appId` | Application identifier |
| `path` | Executable path or command |
| `arguments` | Command-line arguments |
| `url` | URL to open |
| `clearState` | Clear app state before launch |
| `clearKeychain` | Clear keychain data (iOS) |
| `permissions` | Permission configuration map |
| `stopApp` | Stop existing instance first |

#### `stopApp`

Stop a running app:

```yaml
- action: stopApp
  value: "com.example.myapp"
```

#### `killApp`

Force-kill an app:

```yaml
- action: killApp
  value: "com.example.myapp"
```

#### `clearState`

Clear app state:

```yaml
- action: clearState
  appId: "com.example.myapp"
  label: "Reset app data"
```

#### `clearKeychain`

Clear iOS keychain data:

```yaml
- action: clearKeychain
```

#### `setLocation`

Set mock geolocation:

```yaml
- action: setLocation
  latitude: 37.7749
  longitude: -122.4194
```

#### `setOrientation`

Set device orientation:

```yaml
- action: setOrientation
  value: LANDSCAPE_LEFT
```

**Values:** `PORTRAIT`, `LANDSCAPE_LEFT`, `LANDSCAPE_RIGHT`, `UPSIDE_DOWN`

#### `setAirplaneMode`

Enable or disable airplane mode:

```yaml
- action: setAirplaneMode
  enabled: true
```

#### `toggleAirplaneMode`

Toggle airplane mode:

```yaml
- action: toggleAirplaneMode
```

#### `setPermissions`

Grant or deny app permissions:

```yaml
- action: setPermissions
  permissions:
    camera: allow
    location: deny
  appId: "com.example.myapp"
```

#### `travel`

Simulate a route between locations:

```yaml
- action: travel
  points:
    - latitude: 37.7749
      longitude: -122.4194
    - latitude: 34.0522
      longitude: -118.2437
  speedMps: 30
```

---

### Media

#### `takeScreenshot`

Capture a screenshot:

```yaml
- action: takeScreenshot
  path: "screenshots/dashboard.png"
  cropOn: "#mainPanel"
  label: "Dashboard state"
```

#### `startRecording`

Start device-screen recording:

```yaml
- action: startRecording
  path: "recordings/test-run"
  label: "Full test recording"
```

#### `stopRecording`

Stop screen recording:

```yaml
- action: stopRecording
```

#### `addMedia`

Add media files to the target gallery:

```yaml
- action: addMedia
  items:
    - "assets/photo1.jpg"
    - "assets/photo2.png"
```

---

### Timing

#### `delay`

Wait for a specified duration in milliseconds:

```yaml
- action: delay
  value: "2000"
```

---

## Command Aliases

Several commands have shorter aliases for convenience:

| Alias | Canonical Command |
|-------|-------------------|
| `tap` | `tapOn` |
| `doubleTap` | `doubleTapOn` |
| `longPress` | `longPressOn` |
| `input` | `inputText` |
| `clear` | `clearText` |
| `paste` | `pasteText` |
| `erase` | `eraseText` |
| `copy` | `copyTextFrom` |

## Variable Substitution

Use `{{VARIABLE_NAME}}` syntax to reference environment variables:

```yaml
env:
  USERNAME: "admin"
  PASSWORD: "secret123"

steps:
  - action: inputText
    selector: "#txtUsername"
    value: "{{USERNAME}}"
  - action: inputText
    selector: "#txtPassword"
    value: "{{PASSWORD}}"
```

Variables are resolved at execution time from:
1. The flow's `env` section
2. The selected Test Studio environment
3. System environment variables (as fallback)

## Complete Example

```yaml
appId: "CdpSampleApp"
description: "Full login and dashboard verification"
tags:
  - smoke
  - regression

env:
  APP_PORT: "9222"
  USERNAME: "testuser"

steps:
  # Launch and wait for app startup
  - action: launchApp
    path: "dotnet run --project samples/CdpSampleApp"
    clearState: true
  - action: delay
    value: "3000"

  # Login flow
  - action: tapOn
    selector: "#btnLogin"
  - action: inputText
    selector: "#txtUsername"
    value: "{{USERNAME}}"
  - action: inputText
    selector: "#txtPassword"
    value: "password123"
  - action: tapOn
    selector: "#btnSubmit"

  # Wait for dashboard
  - action: extendedWaitUntil
    visible:
      text: "Welcome"
    timeout: 10000

  # Verify dashboard state
  - action: assertVisible
    selector: "#lblWelcome"
  - action: assertTrue
    value: "Window.DataContext.IsLoggedIn == true"
  - action: assertScreenshot
    path: "reference/dashboard.png"
    thresholdPercentage: 5

  # Test navigation
  - action: scroll
    direction: DOWN
    amount: 500
  - action: scrollUntilVisible
    selector:
      text: "Settings"
    direction: DOWN

  # Run settings verification subflow
  - action: runFlow
    file: "flows/verify-settings.flow.yaml"
    label: "Settings verification"

  # Capture final state
  - action: takeScreenshot
    path: "screenshots/final-state.png"
    label: "Test complete"
```

## Next Steps

- [Test Studio](/articles/test-studio) — Visual editing workspace with YAML IntelliSense
- [Code Generation](/articles/code-generation) — Export to automation frameworks
- [Headless Test Adapter](/articles/headless-test-adapter) — CI/CD execution
- [Recording User Actions](/articles/recording-user-actions) — Auto-generate YAML from interactions
