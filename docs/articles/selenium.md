---
title: Selenium E2E Testing Integration
---

# Selenium E2E Testing Integration

Selenium WebDriver is the industry-standard browser automation framework. By leveraging Chrome DevTools Protocol (CDP) capabilities, you can write Selenium C# E2E tests that connect directly to your Avalonia application's built-in DevTools server.

---

## 1. How Selenium Integration Works

Selenium interacts with the Avalonia application over the Chrome DevTools Protocol by communicating directly with the CDP server embedded inside the application process:

```
[ Selenium C# Client ]
          │ (W3C WebDriver over HTTP / DevTools CDP commands)
          ▼
[ ChromeDriver (Port 9515) ]
          │ (CDP JSON-RPC over WebSocket)
          ▼
[ Avalonia CDP Server (Port 9222) ]
```

---

## 2. Using the C# NuGet Package

We provide the shippable NuGet package **`Chrome.DevTools.Automation.Selenium`** to automatically orchestrate target application execution and connect the `ChromeDriver` client instance.

### 1. Installation
Add the package to your test project:
```bash
dotnet add package Chrome.DevTools.Automation.Selenium --prerelease
```

### 2. Write the Test Suite
Inherit from `CdpSeleniumFixture` to automate setup and teardown lifetimes:

```csharp
using System;
using System.Drawing;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using Xunit;
using CDP.Automation.Selenium;

public class MySuite : IClassFixture<CdpSeleniumFixture>
{
    private readonly CdpSeleniumFixture _fixture;
    private readonly ChromeDriver _driver;
    private readonly Actions _actions;

    public MySuite(CdpSeleniumFixture fixture)
    {
        _fixture = fixture;
        _driver = fixture.Driver ?? throw new InvalidOperationException("Selenium driver not initialized.");
        _actions = new Actions(_driver);
    }

    [Fact]
    public void TestButtonClickAndInput()
    {
        // 1. Set window size
        _driver.Manage().Window.Size = new Size(1024, 768);

        // 2. Navigate page
        _driver.Navigate().GoToUrl("http://localhost:9222/home");

        // 3. Find and click elements
        var button = _driver.FindElement(By.Id("btnClickMe"));
        button.Click();

        // 4. Input text and assert state
        var txtInput = _driver.FindElement(By.Id("txtInput"));
        txtInput.Clear();
        txtInput.SendKeys("Hello from Selenium!");

        var status = _driver.FindElement(By.Id("txtStatus"));
        Assert.Equal("Clicked 1 times!", status.Text);
    }
}
```

### 3. Customize Options
You can customize the target DLL execution path, argument configurations, ports, and headless execution flags by overriding the `GetOptions()` method:

```csharp
public class CustomSeleniumFixture : CdpSeleniumFixture
{
    protected override CdpSeleniumOptions GetOptions()
    {
        return new CdpSeleniumOptions
        {
            AppPath = "/path/to/my/app.dll",
            Headless = true,
            AppCdpPort = 9222,
            EnableVerboseLogging = true
        };
    }
}
```

---

## 3. Conformance Details

- **DebuggerAddress Binding**: The driver is attached to the target process using `ChromeOptions.DebuggerAddress`, meaning Chrome DevTools Protocol commands are proxied transparently.
- **Selector Conversions**: Standard CSS selector strings are parsed and mapped to Avalonia visual tree controls by resolving element IDs or class names.
- **Javascript Execution**: Supports standard `/execute/sync` scripts which run sandboxed using `Jint` inside the DevTools domain.

---

## 4. Useful Links

- [Selenium C# Documentation](https://www.selenium.dev/documentation/webdriver/languages/csharp/)
- [Chrome DevTools Protocol (CDP) Documentation](https://chromedevtools.github.io/devtools-protocol/)
- [Vitepress Guide - Appium Integration](/articles/appium)
- [Vitepress Guide - Headless CDP Testing](/articles/headless-cdp-testing)
