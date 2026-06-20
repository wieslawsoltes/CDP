# Test Studio Technical Specification

The Test Studio is a declarative UI automation and testing tool integrated directly into the Chrome DevTools Protocol (CDP) Inspector. It provides a simple, readable YAML-based syntax to describe user flows, automated assertions, and debugger actions.

---

## 1. Test Studio Flow YAML Format

A Test Studio test flow is defined in a YAML file consisting of global metadata followed by a list of step commands.

### Structure

```yaml
appId: "CdpSampleApp"
description: "Verify login and dashboard interaction"
---
- launchApp
- tapOn: "Button#btnLogin"
- inputText:
    selector: "TextBox#txtUsername"
    text: "admin"
- inputText:
    selector: "TextBox#txtPassword"
    text: "secret"
- tapOn: "Button#btnSubmit"
- assertVisible: "TextBlock:contains('Welcome back')"
- delay: 1000
```

---

## 2. Command Reference

The Test Studio interpreter supports the following step commands:

### `launchApp`
Initializes or resets the target application page. In the context of Avalonia CDP, this navigates the active target to its root address (`/`) and resets focus.
- **YAML syntax**: `- launchApp`

### `tapOn`
Simulates a left mouse click at the center of the target control.
- **YAML syntax (short form)**: `- tapOn: "<selector>"`
- **YAML syntax (coordinate form)**:
  ```yaml
  - tapOn:
      x: 150
      y: 320
  ```
- **Execution Details**:
  1. Resolves the selector to a `nodeId` via `DOM.querySelector`.
  2. If found, gets the box model coordinates via `DOM.getBoxModel`.
  3. Computes the center of the content quad.
  4. Dispatches `mousePressed` and `mouseReleased` events on the `Input` domain.

### `inputText`
Types a string of text into the target element.
- **YAML syntax (long form)**:
  ```yaml
  - inputText:
      selector: "TextBox#txtInput"
      text: "Hello World"
  ```
- **YAML syntax (short form, applies to currently focused control)**:
  `- inputText: "Hello World"`
- **Execution Details**:
  1. Focuses the target node using `DOM.focus`.
  2. Inserts text via `Input.insertText` (or key event dispatch).

### `clearText`
Clears text inside the focused text control.
- **YAML syntax (short form, focused element)**: `- clearText`
- **YAML syntax (with selector)**: `- clearText: "TextBox#txtInput"`
- **Execution Details**:
  1. Focuses the control.
  2. Evaluates an in-process script or keyboard commands (Ctrl+A followed by Backspace) to empty the field.

### `assertVisible`
Asserts that a target control exists, matches the selector, and has a valid bounding layout (i.e. is visible on screen).
- **YAML syntax**: `- assertVisible: "<selector>"`
- **Execution Details**:
  1. Queries the node via `DOM.querySelector`.
  2. Calls `DOM.getBoxModel` to ensure it is rendered and has non-zero width/height.
  3. If not found or not rendered, triggers the **Smart Waiting** retry loop. Throws failure after timeout.

### `assertNotVisible`
Asserts that a target control does not exist or is not rendered.
- **YAML syntax**: `- assertNotVisible: "<selector>"`
- **Execution Details**:
  1. Queries the node.
  2. Confirms that either the node is not found or its box model cannot be retrieved/has zero size.

### `delay`
Pauses execution for a specified duration in milliseconds.
- **YAML syntax**: `- delay: 1000`

### `back`
Simulates navigating back in history.
- **YAML syntax**: `- back`
- **Execution Details**: Dispatches `Page.goBack` (or equivalent target back navigation).

### `scroll`
Scrolls the active window down or in a specified direction.
- **YAML syntax**: `- scroll` (scrolls down) or:
  ```yaml
  - scroll:
      direction: "down"
      amount: 100
  ```

### `scrollUntilVisible`
Scrolls in a given direction until the target element is visible on the screen.
- **YAML syntax**:
  ```yaml
  - scrollUntilVisible:
      selector: "Button#btnLoadMore"
      direction: "down"
      maxScrolls: 5
  ```

---

## 3. Protocol & Execution Engine Features

### Smart Waiting (Retry Loop)
To reduce flakiness when navigating or waiting for asynchronous actions:
- Every action targeting a selector (like `tapOn`, `inputText`, `assertVisible`) executes inside a retry block.
- The engine polls `DOM.querySelector` every **200ms**.
- If the element is found, it proceeds immediately.
- If the element is not found within **5.0 seconds** (configurable), the step fails and pauses the debugger.

### Selector Resolution
We resolve element references using standard CSS queries, extended for text-matching:
1. **CSS Selector**: `#btnSubmit`, `Grid > StackPanel > Button.primary`.
2. **Text-contains pseudoclass**: `Button:contains('Login')` which matches a button whose visual or logical tree contains the text "Login".
3. **Plain Text Fallback**: If a selector is in quotes and lacks typical CSS characters, it translates internally to `:contains('<text>')`.
