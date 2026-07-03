# Plan & Specification: Headless CDP Testing, Code Generators & OS Automation Proxy

This document outlines the architectural plans, technical specifications, and implementation steps for advanced CDP features in Avalonia. These include running tests via a headless adapter, aligning/refactoring the test recorder code generators, exposing an OS-level virtual CDP proxy for generic desktop automation, and showcasing a thin headless test harness.

---

## 1. Headless CDP Test Runner

### Objective
Provide a unified, cross-platform way to execute E2E Playwright/browser-automation tests against Avalonia applications headlessly. This allows tests to run in environments without a display server (e.g., standard GitHub Actions Linux runners, headless docker containers) without spawning visible windows.

### Architecture & Design
Avalonia includes the `Avalonia.Headless` package, which runs applications in-process with mock windowing and input platforms. 

To enable headless CDP testing:
1.  **Command-Line Activation**: Integrate command-line argument parsing in the target application's `Program.cs` to check for a `--headless` switch.
2.  **Headless Platform Bootstrapping**:
    ```csharp
    public static AppBuilder BuildAvaloniaApp()
    {
        var args = Environment.GetCommandLineArgs();
        var builder = AppBuilder.Configure<App>();
        
        if (args.Contains("--headless"))
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
    ```
3.  **CDP Server Initialization**: Start the `CdpServer` inside the App's initialization lifecycle. In headless mode, the window does not display, but the visual tree is fully populated, allowing selectors, accessibility properties, and the `DOM` domain to function natively.
4.  **Playwright Configuration Hook**:
    Modify the Playwright `webServer` command block to append the headless parameter:
    ```javascript
    webServer: {
      command: 'dotnet run --project samples/CdpSampleApp/CdpSampleApp.csproj -- --headless > playwright-webserver.log 2>&1',
      url: 'http://127.0.0.1:9222/json',
      reuseExistingServer: !process.env.CI
    }
    ```

---

## 2. Test Recorder Code Generators Alignment

### Objective
Ensure the test recorder inside `CdpInspectorApp` generates modern, fully compliant, and runnable scripts across multiple test frameworks: Playwright, Puppeteer, Selenium, Appium, and Avalonia Headless.

### Implementation Blueprint
*   **Selector Parity**: Align all code generators to prioritize stable identifiers matching the selector contract:
    *   `#controlName` (Avalonia `Name` / `id` attribute).
    *   `[AutomationId="value"]` (for cross-platform automation standards).
*   **Coordinate-Based & Relative Clicks**:
    *   Compute coordinates relative to the bounding box of target elements.
    *   For Appium and Selenium, translate coordinates to screen bounds or viewport relative offsets.
*   **Wait Actions**:
    *   Inject explicit waits (`await page.waitForSelector(...)`, `wait.Until(...)`) before click/type steps to ensure test scripts don't fail due to rendering or transition delays.

### Multi-Framework Code Generator Specifications & Samples

#### A. Playwright (JS/TS)
Exposes native Chrome DevTools Protocol connections:
```javascript
import { test, expect, chromium } from '@playwright/test';

test('Recorded E2E Flow', async () => {
  const browser = await chromium.connectOverCDP('http://127.0.0.1:9222');
  const page = browser.contexts()[0].pages()[0];
  
  // Interactions
  await page.fill('#txtInput', 'Hello World');
  await page.click('#btnSubmit');
  
  // Assertions
  await expect(page.locator('#txtStatus')).toHaveText('Done');
  await browser.close();
});
```

#### B. Puppeteer (JS/TS)
```javascript
const puppeteer = require('puppeteer');

(async () => {
  const browser = await puppeteer.connect({
    browserURL: 'http://127.0.0.1:9222'
  });
  const page = (await browser.pages())[0];
  
  await page.type('#txtInput', 'Hello World');
  await page.click('#btnSubmit');
  
  const status = await page.$eval('#txtStatus', el => el.textContent);
  console.assert(status === 'Done', 'Status verification failed');
  
  await browser.disconnect();
})();
```

#### C. Selenium C# (CDP Connection)
Uses Selenium 4's native CDP capabilities to communicate with the application:
```csharp
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;

var options = new ChromeOptions();
options.DebuggerAddress = "127.0.0.1:9222";
using var driver = new ChromeDriver(options);

var input = driver.FindElement(By.CssSelector("#txtInput"));
input.SendKeys("Hello World");

var submit = driver.FindElement(By.CssSelector("#btnSubmit"));
submit.Click();

var status = driver.FindElement(By.CssSelector("#txtStatus"));
System.Diagnostics.Debug.Assert(status.Text == "Done");
```

#### D. Appium C#
```csharp
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

var options = new AppiumOptions();
options.AddAdditionalCapability("app", @"C:\Path\To\CdpSampleApp.exe");
options.AddAdditionalCapability("appArguments", "--cdp-port 9222");

using var driver = new WindowsDriver<WindowsElement>(new Uri("http://127.0.0.1:4723"), options);
var input = driver.FindElementByAccessibilityId("txtInput");
input.SendKeys("Hello World");
```

#### E. Avalonia Headless xUnit (In-Process)
Runs in-process without WebSockets, utilizing headless platform simulators:
```csharp
using Avalonia.Headless.XUnit;
using Avalonia.Input;

[AvaloniaTest]
public void TestRecordedFlow()
{
    var window = new MainWindow();
    window.Show();
    
    var input = window.FindControl<TextBox>("txtInput");
    input.Focus();
    window.KeyInput("Hello World");
    
    var submit = window.FindControl<Button>("btnSubmit");
    window.Tap(submit);
    
    var status = window.FindControl<TextBlock>("txtStatus");
    Assert.Equal("Done", status.Text);
}
```

---

## 3. OS Automation CDP Emulation Proxy (`CDP.Automation.OS`)

### Objective
Create a proxy layer that translates the standard browser Chrome DevTools Protocol (CDP) commands into native OS accessibility/automation APIs. This allows standard browser testing frameworks (like Playwright or Puppeteer) to automate *any* desktop app on the operating system by treating the OS desktop as a browser window.

### Architecture Topology
```text
Playwright Client (Browser connection)
       | (CDP over WebSocket on Port 9224)
       v
CDP.Automation.OS (Proxy Daemon)
       |
       +--> macOS: AppKit Accessibility API (NSAccessibility)
       +--> Windows: UIAutomationCore (COM Interfaces)
       +--> Linux: AT-SPI2 / DBus interface
```

### CDP Domain Mapping Specification

#### 1. Target Discovery (`Target` Domain)
*   **`Target.getTargets`**: Lists all active OS desktop windows as virtual "pages" (with window titles, window class names, and process IDs mapped to target IDs).
*   **`Target.attachToTarget`**: Establishes a virtual CDP session bound to a specific application process window.

#### 2. Visual Tree Hierarchy (`DOM` Domain)
*   **`DOM.getDocument`**: Reads the OS window's accessibility element tree. Maps accessibility attributes (`AXRole`, `AXTitle`, `AXIdentifier` on macOS; `ControlType`, `AutomationId`, `Name` on Windows) to DOM node structures.
*   **`DOM.querySelector`**: Translates CSS selectors (e.g. `[AutomationId="txtInput"]`, `Button:contains("Submit")`) to OS automation queries.

#### 3. Bounding Boxes (`DOM.getBoxModel`)
*   **`DOM.getBoxModel`**: Queries the native frame dimensions of the accessibility element. Returns screen-relative bounding coordinates (mapped to the CSS viewport quad model).

#### 4. Inputs (`Input` Domain)
*   **`Input.dispatchMouseEvent`**: Maps clicks, moves, and double-clicks to native system mouse inputs (`CGEventPost` on macOS, `SendInput` on Windows).
*   **`Input.insertText`**: Sends keystrokes to the currently focused UI accessibility element.

#### 5. Telemetry & Captures (`Page` Domain)
*   **`Page.captureScreenshot`**: Takes a snapshot of the targeted application window bounds (via macOS `CGWindowListCreateImage` or Windows GDI `Graphics.CopyFromScreen`).

---

## 4. Headless Thin Test Harness Showcase

### Objective
Showcase a minimal, fast-bootstrapping test runner (`CDP.HeadlessRunner`) that loads a shared assembly or DLL containing the target app's views, mounts them headlessly, runs the CDP server, and facilitates Playwright testing without duplicating build steps.

### Harness Specification
Create a minimal console project targeting `net10.0`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <PublishTrimmed>true</PublishTrimmed>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia.Headless" Version="12.0.5" />
    <ProjectReference Include="..\CdpSampleApp\CdpSampleApp.csproj" />
  </ItemGroup>
</Project>
```

```csharp
using System;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Diagnostics.Cdp;

namespace CDP.HeadlessRunner;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var app = AppBuilder.Configure<CdpSampleApp.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();

        // Boot CDP Server
        CdpServer.Start(port: 9222);
        
        Console.WriteLine("Headless test harness ready and listening on http://127.0.0.1:9222");
        
        // Keep process alive while tests are running
        System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
    }
}
```

This harness can be compiled once and used by Playwright as the startup webServer executable, providing immediate, zero-display testing performance.
