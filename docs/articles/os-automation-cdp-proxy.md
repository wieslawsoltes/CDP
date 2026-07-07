---
title: OS Automation CDP Proxy
---

# OS Automation CDP Proxy

The **OS Automation CDP Proxy** (`CDP.Automation.OS`) is a standalone daemon and emulation layer designed to translate standard browser Chrome DevTools Protocol (CDP) commands into native operating system accessibility and automation APIs.

By exposing a virtual CDP target endpoint, the proxy allows standard web automation and testing frameworks (such as Playwright, Puppeteer, and Selenium) to automate **any desktop application** on the operating system (including WinForms, WPF, native macOS Cocoa, Electron, or legacy C++ apps) by treating the operating system's desktop and active windows as a virtual browser context.

---

## 1. Architectural Topology

```text
  Playwright / Puppeteer Client
               | (CDP over WebSocket / Port 9224)
               v
  CDP.Automation.OS (Proxy Daemon)
               |
        +------+------+
        |             |
        v             v
     Windows:       macOS:
  UIAutomation     AppKit Accessibility
   (UIAutomationCore) (NSAccessibility)
```

The proxy acts as a WebSocket server listening on port `9224`. When a Playwright script connects via `chromium.connectOverCDP('http://127.0.0.1:9224')`, the proxy intercepts standard devtools commands, queries the native OS accessibility hierarchies, maps native bounding boxes, and dispatches native hardware input events.

---

## 2. CDP Domain Translations

### A. Target Discovery (`Target` Domain)
*   **`Target.getTargets`**: Queries active OS desktop windows and returns them to the client as virtual page list targets.
*   **`Target.attachToTarget`**: Attaches the WebSocket session to a specific process ID and window handle, focusing all subsequent DOM/Input queries onto that specific application window.

### B. Element Hierarchy & Querying (`DOM` Domain)
*   **`DOM.getDocument`**: Reads the active window's native accessibility element hierarchy and maps it to a standard DOM XML tree:
    *   Window controls become XML/DOM nodes.
    *   OS-native control types (`Button`, `Edit`, `ComboBox`, `Window`) map to tag names.
    *   Native IDs (`AutomationId` on Windows, `AXIdentifier` on macOS) map to standard `id` attributes.
    *   Text values and labels map to `text` or `content` attributes.
*   **`DOM.querySelector`**: Translates CSS selectors into OS automation queries. For example:
    *   `[id="btnSubmit"]` -> Resolves to the element with `AutomationId == "btnSubmit"`.
    *   `Button:contains("Click Me")` -> Resolves to the button containing "Click Me" as its accessibility label or title.

### C. Element Geometry & Coordinates (`DOM.getBoxModel`)
*   **`DOM.getBoxModel`**: Fetches the screen-relative bounding box dimensions of the control via the OS accessibility frame structures (`AXFrame` on macOS; `CurrentBoundingRectangle` on Windows). The proxy maps these dimensions directly to standard CSS layout viewport quad quads (content, border, padding, margin) so browser drivers click the exact coordinate centers.

### D. Inputs (`Input` Domain)
*   **`Input.dispatchMouseEvent`**: Translates click, drag, and mouse moved commands into native OS input event dispatches (`SendInput` on Windows; `CGEventPost` / AppleScript on macOS) to simulate authentic hardware device input.
*   **`Input.insertText`**: Directly injects keyboard focus and pushes keystroke sequences to the target control.

### E. Screen Capture (`Page` Domain)
*   **`Page.captureScreenshot`**: Utilizes native OS capture calls (such as GDI `Graphics.CopyFromScreen` on Windows or `CGWindowListCreateImage` on macOS) to snapshot the exact bounds of the active target window and return it as a base64-encoded PNG payload.

---

## 3. Playwright Example: Automating a WinForms / Native Desktop App

Since the proxy emulates standard browser CDP, you can write unmodified Playwright scripts to automate a native OS application:

```javascript
import { test, expect, chromium } from '@playwright/test';

test('Automate OS Desktop Native Application', async () => {
  // Connect to the OS Automation Proxy Daemon
  const browser = await chromium.connectOverCDP('http://127.0.0.1:9224');
  const context = browser.contexts()[0];
  const page = context.pages()[0]; // Active desktop target window

  // Wait for WinForms / native control to load using accessibility attributes
  const inputControl = page.locator('[AutomationId="txtUserName"]');
  await inputControl.waitFor({ state: 'visible' });

  // Type and click natively
  await inputControl.fill('admin');
  await page.click('[AutomationId="btnSubmit"]');

  // Verify result using standard innerText expectations
  const statusLabel = page.locator('[AutomationId="lblStatus"]');
  await expect(statusLabel).toHaveText('Login Successful');

  await browser.close();
});
```

---

## 4. Platform-Specific Setup and Prerequisites

### Windows Configuration
*   **API Backing**: Utilizes `UIAutomationCore` COM interfaces for high-performance visual-tree traversal, and `User32.dll` for input simulation.
*   **Permissions**: The proxy daemon must run with appropriate privileges (UIAccess or Administrator) to interact with high-privilege application windows (e.g. installers, system configuration views).

### macOS Configuration
*   **API Backing**: Uses `AppKit` and core foundation `AXUIElement` accessibility frameworks.
*   **System Permissions**: The proxy runner executable requires **Accessibility Permissions** granted under macOS System Settings -> Privacy & Security -> Accessibility. If the permission is missing, native DOM traversals and mouse dispatches will return access-denied faults.

---

## 5. Execution Instructions

The `OsAutomationDaemon` is the default standalone proxy mechanism. By default, it can be run on port `9224` using the following instructions.

### Running the Daemon

To build and run the `OsAutomationDaemon` on port `9224`, run the following command from the root of the repository:

```shell
dotnet run --project src/CDP.Automation.OS/CDP.Automation.OS.csproj 9224
```

Upon startup, the console will output:
```text
Starting OS Automation CDP Emulation Daemon on port 9224...
CDP Server running on port 9224.
Press Ctrl+C to exit.
```

Once running, you can connect your CDP clients (such as Playwright, Puppeteer, or the `CdpInspectorApp`) directly to the WebSocket server at `http://127.0.0.1:9224`.
