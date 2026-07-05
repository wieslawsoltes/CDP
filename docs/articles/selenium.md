---
title: Selenium E2E Testing Integration
---

# Selenium E2E Testing Integration

Selenium WebDriver is one of the most widely used open-source tools for browser automation. Because the Avalonia CDP server exposes standard Chrome DevTools Protocol endpoints, you can configure the C# Selenium client to attach to a running Avalonia application using the Chrome debugger protocol and drive UI tests natively.

---

## 1. Selenium Setup

1.  **Start your Avalonia Application** with the CDP server listening (by default, it starts on `http://127.0.0.1:9222`):
    ```csharp
    CdpServer.Start(port: 9222);
    ```
2.  **Add Selenium package to your test project**:
    ```bash
    dotnet add package OpenQA.Selenium.Chrome
    ```
3.  **Write the Selenium WebDriver Script**:
    Create a script or test fixture to connect and interact using `ChromeOptions`:
    ```csharp
    using OpenQA.Selenium;
    using OpenQA.Selenium.Chrome;

    // Configure ChromeOptions to connect to the running application port
    var options = new ChromeOptions();
    options.DebuggerAddress = "127.0.0.1:9222";

    using var driver = new ChromeDriver(options);
    Console.WriteLine($"Attached successfully to window title: {driver.Title}");

    // Interact with controls using selectors
    var inputElement = driver.FindElement(By.CssSelector("#txtInput"));
    inputElement.SendKeys("Hello from Selenium!");

    var clickButton = driver.FindElement(By.CssSelector("#btnClickMe"));
    clickButton.Click();

    // Assert states
    var statusText = driver.FindElement(By.CssSelector("#txtStatus")).Text;
    Console.WriteLine($"Result: {statusText}");
    ```

---

## 2. Example E2E Testing Suite

We provide a complete, integrated XUnit Selenium test suite under [SeleniumTests.cs](file:///Users/wieslawsoltes/GitHub/CDP/tests/CDP.Selenium.Tests/SeleniumTests.cs).

### Running Tests

By default, the Selenium E2E test runner automatically launches the target application in **Headless mode** for clean execution inside CI/CD virtual frames. However, you can toggle both modes easily:

#### 1. Headless Mode (Default)
Executes the application in a virtual offscreen render buffer:
```bash
dotnet test tests/CDP.Selenium.Tests/
```

#### 2. GUI / Non-Headless Mode (Interactive Debugging)
To see the desktop GUI window spawn on your desktop and watch Selenium drive the inputs:
```bash
HEADLESS=false dotnet test tests/CDP.Selenium.Tests/
```

---

## 3. The Test Fixture & Process Lifecycle

To manage the execution lifespans cleanly, the Selenium test suite implements a `SeleniumFixture` class implementing `IAsyncLifetime`.

This fixture is responsible for:
1. **Pinging the Port**: Probing port `9222` first. If it is already listening (for instance, if you left a target GUI app instance running), it attaches ChromeDriver directly without restarting the app.
2. **Process Spawning**: If the port is closed, it launches the sample project process (`samples/CdpSampleApp/CdpSampleApp.csproj`) with the `--headless` flag (unless `HEADLESS=false` is set).
3. **Diagnostics Logging**: Standard output from the running application is redirected to a local `cdp-sample-app.log` file for startup and protocol call tracing.
4. **ChromeDriver Handshake**: Instantiates `ChromeDriver` using `options.DebuggerAddress = "127.0.0.1:9222"`.

---

## 4. Strict ChromeDriver / CDP Conformance Features

Selenium connects to DevTools via ChromeDriver, which enforces strict protocol schema checks. The Avalonia CDP server incorporates specialized support for these demands:
* **Context Unique ID Lifecycle**: During navigations, `Runtime.executionContextDestroyed` is sent with both the numeric context ID and its `executionContextUniqueId` string key to prevent ChromeDriver navigation crashes.
* **Document Location Evaluation**: Handles evaluations of `document.URL` and `documentURI` on the document object, mapping to active entries in the session's navigation history.
* **Describe Document Node**: Responds to `DOM.describeNode` calls targeting the `CdpRuntimeDocument` object ID, correctly generating a `#document` node type descriptor (nodeType 9) rather than returning standard elements.

---

## 5. Useful Links & Reference Material

* [Selenium C# WebDriver Documentation](https://www.selenium.dev/documentation/webdriver/getting_started/first_script/)
* [ChromeDriver Log Config Reference](https://www.selenium.dev/documentation/webdriver/troubleshooting/logging/)
* [Vitepress Guide - Playwright Integration](/articles/playwright)
* [Vitepress Guide - Headless CDP Testing](/articles/headless-cdp-testing)
