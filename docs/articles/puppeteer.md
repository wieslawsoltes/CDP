---
title: Puppeteer E2E Testing Integration
---

# Puppeteer E2E Testing Integration

Puppeteer is a popular Node.js library which provides a high-level API to control Chrome or Chromium over the DevTools Protocol. Because the Avalonia CDP server exposes standard Chrome DevTools Protocol endpoints, Puppeteer can connect directly to your desktop app to automate testing using standard web-style locator and input APIs.

---

## 1. Puppeteer Setup

1.  **Start your Avalonia Application** with the CDP server listening (by default, it starts on `http://127.0.0.1:9222`):
    ```csharp
    CdpServer.Start(port: 9222);
    ```
2.  **Install Puppeteer** in your test suite:
    ```bash
    npm install puppeteer
    ```
3.  **Write the Puppeteer Script**:
    Create a script (e.g., `test-app.js`) to connect and interact:
    ```javascript
    const puppeteer = require('puppeteer');

    (async () => {
      // Connect Puppeteer directly to the Avalonia CDP server
      console.log('Connecting to Avalonia CDP server...');
      const browser = await puppeteer.connect({
        browserURL: 'http://127.0.0.1:9222',
        defaultViewport: null
      });
      
      const pages = await browser.pages();
      const page = pages[0];
      console.log(`Attached successfully to page: ${await page.title()}`);

      // Emulate typing and clicking using selectors
      await page.type('#txtInput', 'Hello from Puppeteer!');
      await page.click('#btnClickMe');

      // Verify outcomes
      const resultText = await page.evaluate(() => document.querySelector('#txtStatus').textContent);
      console.log(`Result text matches: "${resultText}"`);

      // Take a high-fidelity screenshot of the desktop window
      await page.screenshot({ path: 'app-screenshot.png' });

      // Disconnect cleanly
      await browser.disconnect();
      console.log('Puppeteer test completed successfully!');
    })();
    ```

---

## 2. Example E2E Testing Suite

We provide a complete, automated Node.js test runner suite using Puppeteer under [cdp-sample.test.js](file:///Users/wieslawsoltes/GitHub/CDP/tests/puppeteer/cdp-sample.test.js).

### Running Tests

You can run the tests in both **Headless** (default) and **GUI (Interactive)** modes using the environment flags.

#### 1. Headless Mode (Default for CI/CD)
Runs the application headlessly using a virtual display buffer.
```bash
npm run test:puppeteer
```

#### 2. GUI / Non-Headless Mode (Ideal for Interactive Debugging)
To open the actual desktop GUI window and watch the interactions run in real-time, prefix the command with `HEADLESS=false`:
```bash
HEADLESS=false npm run test:puppeteer
```

### Example Test File Structure (`cdp-sample.test.js`)
Here is a simplified structure of the E2E test file:
```javascript
const test = require('node:test');
const assert = require('node:assert');
const puppeteer = require('puppeteer');

test.describe('Avalonia CDP E2E Automation - Puppeteer', () => {
  let browser;
  let page;

  test.before(async () => {
    // Automatically handles spawning target app if not running (see below)
    browser = await puppeteer.connect({
      browserURL: 'http://127.0.0.1:9222',
      defaultViewport: null
    });
    const pages = await browser.pages();
    page = pages[0];
  });

  test.after(async () => {
    if (browser) {
      await browser.disconnect();
    }
  });

  test('Verify home page elements and interaction', async () => {
    await page.click('#btnClickMe');
    const statusText = await page.evaluate(() => document.querySelector('#txtStatus').textContent);
    assert.strictEqual(statusText, 'Clicked 1 times!');
  });
});
```

---

## 3. Spawning the Application via Test Runner

To automate the launch and cleanup of the Avalonia app during local and CI/CD test runs, the Puppeteer test suite implements a dynamic startup helper that:
1. **Checks Port Availability**: Attempts to ping `http://127.0.0.1:9222/json/version` first to see if an instance is already running (e.g. for local interactive development).
2. **Spawns Child Process**: If the port is closed, it spawns `dotnet run --project samples/CdpSampleApp/CdpSampleApp.csproj` (with `--headless` appended by default).
3. **Redirects Logs**: Pipe target app output to `puppeteer-webserver.log` to track target traces and event diagnostics.
4. **Port Probing**: Polls the `/json/version` target endpoint until the server is fully ready before connecting Puppeteer.
5. **Teardown**: Automatically kills the child process tree on test completion.

---

## 4. Underlying Integration Features

To guarantee compatibility with standard, unmodified Puppeteer packages, several CDP server design details are implemented behind the scenes:
* **JSON-RPC Over WebSockets**: Fully maps message ids and parameters.
* **Jint Expression Evaluator**: Safely interprets and evaluates wait/locator helper scripts inside the C# VM.
* **Layout and Bounding Boxes**: Responds to standard viewport and metrics requests so screenshot capture boundaries match the window size.

---

## 5. Useful Links & Reference Material

* [Puppeteer API Documentation](https://pptr.dev/)
* [Puppeteer Connect Options Reference](https://pptr.dev/api/puppeteer.connectoptions)
* [Vitepress Guide - Playwright Integration](/articles/playwright)
* [Vitepress Guide - CSS Selector Engine](/articles/selector-engine)
