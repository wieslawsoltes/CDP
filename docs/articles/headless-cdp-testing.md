---
title: Headless CDP Testing Guide
---

# Headless CDP Testing Guide

Running end-to-end (E2E) browser automation tests against desktop applications usually requires a full display server (like X11, Wayland, or desktop window managers). However, in automated CI/CD environments (such as standard GitHub Actions Linux runners or headless Docker containers), spawning visible application windows is impossible or highly complex.

The Avalonia CDP suite resolves this by supporting **headless platform execution** integrated with standard Chrome DevTools Protocol targets. This allows Playwright, Puppeteer, or any other CDP client to run tests programmatically in a zero-display environment.

---

## 1. How Headless CDP Works

Avalonia's headless platform execution (`Avalonia.Headless`) mounts and layouts the visual tree completely in-process, bypassing the OS window manager and graphics driver initialization. Because the visual tree is fully populated, the CDP server can traverse layouts, inspect automation IDs, dispatch click/mouse/keyboard events, and execute JavaScript/C# expressions exactly as if a physical window was open.

```text
Playwright Test Run
  | (CDP Protocol / WebSockets)
  v
CdpSampleApp (Headless Mode)
  |--> UseHeadless()
  |--> Mounts Visual Tree in memory
  |--> Exposes CDP WebSocket on Port 9222
```

---

## 2. Bootstrapping Headless Platform in your Application

To configure your Avalonia target application to support headless execution, modify the entry point builder (`Program.cs`) to look for a `--headless` command-line argument.

```csharp
using System;
using Avalonia;
using Avalonia.Headless;

namespace CdpSampleApp;

class Program
{
    [STAThread]
    public static void Main(string[] args) => 
        BuildAvaloniaApp(args).StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp(string[] args)
    {
        var builder = AppBuilder.Configure<App>();

        // Check if headless platform execution is requested
        if (Array.Exists(args, arg => arg.Equals("--headless", StringComparison.OrdinalIgnoreCase)))
        {
            builder.UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseDotNetSystemFont = true
            });
        }
        else
        {
            builder.UsePlatformDetect();
        }

        return builder.LogToTrace();
    }
}
```

---

## 3. Configuring Playwright to Run Headlessly

To automate the launch and execution process, configure Playwright to spawn your target application using the `--headless` argument within your `playwright.config.js` or `playwright.config.ts` file.

```javascript
const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './tests/playwright',
  timeout: 30000,
  webServer: {
    // Spawns the target app in headless mode, redirecting logs to a file
    command: 'dotnet run --project samples/CdpSampleApp/CdpSampleApp.csproj -- --headless > playwright-webserver.log 2>&1',
    // The CDP target discovery endpoint that Playwright polls
    url: 'http://127.0.0.1:9222/json',
    // Skip launching a new instance if an app is already listening locally
    reuseExistingServer: !process.env.CI,
    stdout: 'ignore',
    stderr: 'pipe',
  },
});
```

---

## 4. Headless Execution in CI/CD (GitHub Actions)

When running inside a GitHub Actions pipeline, no physical display is available. By running your app with the `--headless` flag, you can execute standard Playwright tests directly on standard Ubuntu/Windows runners without needing complex frame-buffer wrappers (like `xvfb-run`).

Here is a sample GitHub Actions pipeline configuration (`.github/workflows/e2e-tests.yml`):

```yaml
name: E2E Automation Suite

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
    - name: Checkout Repository
      uses: actions/checkout@v4

    - name: Setup .NET SDK
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'

    - name: Setup Node.js
      uses: actions/setup-node@v4
      with:
        node-version: '20'

    - name: Install Test Dependencies
      run: npm ci

    - name: Install Playwright Browsers
      run: npx playwright install chromium --with-deps

    - name: Run E2E Tests
      run: npx playwright test
```

---

## 5. Troubleshooting & Limitations

### Captured Screenshots
In headless mode, because there is no physical GPU window or monitor drawing surface, call captures (such as `page.screenshot()`) rely on custom bitmap rendering contexts inside the Avalonia headless frame manager. Render layouts match exactly, but hardware-accelerated shaders or web-canvas overlays may render differently.

### Font Configurations
To prevent glyph render bugs or character box layouts from falling back to missing system fonts (which varies heavily across standard Linux CI/CD cloud instances), ensure you set `UseDotNetSystemFont = true` inside `AvaloniaHeadlessPlatformOptions`. This ensures standard font families are emulated correctly using embedded resources.
