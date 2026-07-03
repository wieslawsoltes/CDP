---
title: Playwright E2E Testing Integration
---

# Playwright E2E Testing Integration

Playwright is a modern framework for end-to-end web testing. Because the Avalonia CDP server exposes standard Chrome DevTools Protocol endpoints, Playwright can attach to your app using `connectOverCDP` and automate UI actions on desktop controls.

---

## 1. Playwright Setup

1.  **Start your Avalonia Application** with the CDP server listening (by default, it starts on `http://127.0.0.1:9222`):
    ```csharp
    CdpServer.Start(port: 9222);
    ```
2.  **Install Playwright in your test suite**:
    ```bash
    npm install playwright
    ```
3.  **Write the Playwright Script**:
    Create a script (e.g., `test-app.js`) to connect and interact:
    ```javascript
    const { chromium } = require('playwright');

    (async () => {
      // Connect Playwright directly to the Avalonia CDP server
      console.log('Connecting to Avalonia CDP server...');
      const browser = await chromium.connectOverCDP('http://127.0.0.1:9222');
      
      const context = browser.contexts()[0];
      const page = context.pages()[0];
      console.log(`Attached successfully to page: ${await page.title()}`);

      // Wait for application UI elements to load
      await page.waitForSelector('#txtInput');

      // Emulate typing and clicking
      await page.fill('#txtInput', 'Hello from Playwright!');
      await page.click('#btnClickMe');

      // Verify outcomes
      const resultText = await page.locator('#txtResult').innerText();
      console.log(`Result text matches: "${resultText}"`);

      // Take a high-fidelity screenshot of the desktop window
      await page.screenshot({ path: 'app-screenshot.png' });

      // Disconnect cleanly
      await browser.close();
      console.log('Playwright test completed successfully!');
    })();
    ```

---

## 2. Example E2E Testing suite

We provide an example E2E test suite using Playwright test runner under `tests/playwright/cdp-sample.spec.js`.

### Running Tests
To execute the Playwright tests on the running sample app:
```bash
npx playwright test
```

### Example Test File Structure (`cdp-sample.spec.js`)
```javascript
const { test, expect } = require('@playwright/test');

test.describe('Avalonia CDP E2E Automation', () => {
  let browser;
  let context;
  let page;

  test.beforeAll(async () => {
    browser = await chromium.connectOverCDP('http://127.0.0.1:9222');
    context = browser.contexts()[0];
    page = context.pages()[0];
  });

  test.afterAll(async () => {
    await browser.close();
  });

  test('Verify home page elements and interaction', async () => {
    const clickBtn = page.locator('#btnClickMe');
    await clickBtn.click();
    await expect(page.locator('#txtStatus')).toHaveText('Clicked 1 times!');
  });
});
```

---

## 3. Underlying Integration Features

To ensure that standard, unmodified Playwright clients can drive desktop windows, the CDP server handles several advanced capabilities behind the scenes:

### Target Auto-Attachment (`Target.setAutoAttach`)
In complex desktop applications, opening new windows or popup dialogs creates multiple separate pages/targets. The CDP server implements the `Target.setAutoAttach` command. When your script opens a new window, Playwright automatically discovers, hooks onto, and communicates with the new page target WebSocket without needing manual target lookup.

### Viewport Metrics & Layout Verification (`Page.getLayoutMetrics`)
When Playwright takes a screenshot or computes clicks, it requests the layout bounds of the canvas. The CDP server's `Page.getLayoutMetrics` returns legacy layout and visual viewport fields (`layoutViewport`, `visualViewport`, and `contentSize`) alongside CSS equivalents. This allows Playwright to calculate absolute coordinates relative to the window boundary correctly, ensuring screenshots capture the exact active UI canvas.

### Injected Script and Wait Predicate Evaluation
Browser automation tools often inject complex JavaScript snippets. The `Runtime` domain dynamically intercepts Playwright utility functions (such as `UtilityScript.evaluate` and locator polling checks). These scripts are evaluated safely on the server side using the C# expression runner, allowing Playwright's wait helpers to resolve and return natively.

---

## 4. Useful Links & Reference Material

*   [Chrome DevTools Protocol Specification](https://chromedevtools.github.io/devtools-protocol/)
*   [Playwright ConnectOverCDP API Docs](https://playwright.dev/docs/api/class-browsertype#browser-type-connect-over-cdp)
*   [Vitepress Guide - AI Agent Integration](/articles/ai-agent-integration)
*   [Vitepress Guide - CSS Selector Engine](/articles/selector-engine)
