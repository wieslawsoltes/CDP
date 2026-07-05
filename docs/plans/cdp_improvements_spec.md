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

---

## 3. Web Framework CDP Integration Specifications & Samples

Standard browser automation frameworks can interact directly with the Avalonia application via Chrome DevTools Protocol by pointing their CDP clients to the target application's port.

### A. Playwright (JS/TS)
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

### B. Puppeteer (JS/TS)
Puppeteer connects to the CDP server using HTTP-based target discovery or a direct WebSocket connection:
```javascript
const puppeteer = require('puppeteer');

(async () => {
  // Option 1: Discover via HTTP port
  const browser = await puppeteer.connect({
    browserURL: 'http://127.0.0.1:9222'
  });
  
  // Option 2: Connect directly using browser websocket url
  // const browser = await puppeteer.connect({
  //   browserWSEndpoint: 'ws://127.0.0.1:9222/devtools/browser'
  // });
  
  const pages = await browser.pages();
  const page = pages[0];
  
  // Set viewport and navigate
  await page.setViewport({ width: 800, height: 600 });
  
  // Input simulation using standard DOM selectors
  await page.waitForSelector('#txtInput');
  await page.type('#txtInput', 'Hello World');
  await page.click('#btnSubmit');
  
  // Assertions
  const status = await page.$eval('#txtStatus', el => el.textContent);
  if (status !== 'Done') {
    throw new Error(`Assertion failed: expected 'Done', got '${status}'`);
  }
  
  await browser.disconnect();
})();
```

### C. Selenium 4 C# (CDP DevTools Connection)
Selenium 4 supports direct execution of Chrome DevTools Protocol commands using driver extension methods:
```csharp
using System;
using System.Collections.Generic;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

class Program
{
    static void Main()
    {
        var options = new ChromeOptions();
        // Point Selenium to the running Avalonia app's CDP port
        options.DebuggerAddress = "127.0.0.1:9222";
        
        using var driver = new ChromeDriver(options);
        
        // Wait for control
        var input = driver.FindElement(By.CssSelector("#txtInput"));
        input.SendKeys("Hello World");
        
        var submit = driver.FindElement(By.CssSelector("#btnSubmit"));
        submit.Click();
        
        // Execute a direct CDP evaluation command using Selenium's command bridge
        var cdpParams = new Dictionary<string, object>
        {
            { "expression", "document.querySelector('#txtStatus').textContent" },
            { "returnByValue", true }
        };
        
        var cdpResult = driver.ExecuteCdpCommand("Runtime.evaluate", cdpParams) as Dictionary<string, object>;
        var resultData = cdpResult?["result"] as Dictionary<string, object>;
        var textVal = resultData?["value"]?.ToString();
        
        System.Diagnostics.Debug.Assert(textVal == "Done", $"Expected 'Done' but got '{textVal}'");
    }
}
```

### D. Appium Hybrid CDP Automation (C#)
Appium drives target application windows at the OS level (using WinAppDriver on Windows or Mac2Driver on macOS) while establishing a concurrent CDP connection to assert view model states or fetch diagnostic telemetry.

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

class AppiumHybridTests
{
    static void Main()
    {
        var options = new AppiumOptions();
        // Launch target app via Windows Application Driver
        options.AddAdditionalCapability("app", @"C:\Path\To\CdpSampleApp.exe");
        options.AddAdditionalCapability("appArguments", "--cdp-port 9222");

        using var driver = new WindowsDriver<WindowsElement>(new Uri("http://127.0.0.1:4723"), options);
        
        // 1. Appium native OS UI Interactions
        var input = driver.FindElementByAccessibilityId("txtInput");
        input.SendKeys("Hello World");
        
        var submit = driver.FindElementByAccessibilityId("btnSubmit");
        submit.Click();

        // 2. Secondary CDP connection to query ViewModel internal state directly
        using var httpClient = new HttpClient();
        var responseStr = httpClient.GetStringAsync("http://127.0.0.1:9222/json").Result;
        var targets = JsonNode.Parse(responseStr)?.AsArray();
        
        // Connect to WebSocket using target websocket debugger URL
        var wsUrl = targets?[0]?["webSocketDebuggerUrl"]?.ToString();
        Console.WriteLine($"Discovered CDP WebSocket URL: {wsUrl}");
        
        // Send a direct Runtime.evaluate to verify the backend ViewModels
        // (evaluates standard C# bindings inside Jint evaluation context)
        // Expression checks: Window.DataContext.Connection.IsConnected
    }
}
```

### E. Avalonia Headless xUnit (In-Process)
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

## 4. OS Automation CDP Emulation Proxy (`CDP.Automation.OS`)

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

## 5. Dynamic JavaScript Execution in Test Studio (Maestro-style)

### Objective
Provide Test Studio with programmability by introducing dynamic JavaScript execution and variable propagation steps into the `.flow.yaml` execution engine, similar to `maestro.dev`. This enables loops, conditional assertions, and multi-step state carry-over natively inside flows.

### YAML Specification

#### 1. Script Evaluation Step (`runScript`)
Runs inline JS code or an external `.js` file, assigning the return value to a local context variable:
```yaml
- type: runScript
  script: |
    // Queries elements in the Avalonia visual tree
    var buttonList = QueryAll("Button");
    return buttonList.length;
  assignTo: totalButtons
```

Or from an external file:
```yaml
- type: runScript
  file: scripts/calculate_offsets.js
  assignTo: clickOffset
```

#### 2. Script Assertion Step (`assertScript`)
Verifies a script expression resolves to a truthy value, failing the step if it returns `false`:
```yaml
- type: assertScript
  expression: "DataContext.Items.Count > 0"
  timeout: 5000
```

#### 3. State Interpolation in Flow Steps
Local context variables assigned via script steps can be interpolated into subsequent step selectors or parameters using `${context.variableName}` syntax:
```yaml
- type: inputText
  selector: "#txtConsoleInput"
  value: "Discovered ${totalButtons} buttons in layout."
```

### Replay Engine Implementation
To support scripting execution in the YAML test runner:
1.  **Shared State Container (`FlowContext`)**: Instantiated for the lifecycle of a flow execution, maintaining a thread-safe `Dictionary<string, object>` of step outputs.
2.  **Jint Engine Initialization**: For each `runScript` and `assertScript` step:
    *   Boot a scoped Jint engine instance.
    *   Inject globals: `Window`, `Control`, `DataContext`, `ViewModel`, `Query` (CSS visual lookup), and `QueryAll`.
    *   Inject the `context` variable (mapped to `FlowContext` keys).
3.  **Variable Resolution Pre-pass**: Before executing any step, scan parameters and selectors for `${context.*}` tokens. Extract keys, look up values in `FlowContext`, and perform string replacement.

---

## 6. Headless Thin Test Harness Showcase

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
