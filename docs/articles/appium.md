---
title: Appium E2E Testing Integration
---

# Appium E2E Testing Integration

Appium is an open-source test automation framework for mobile and desktop applications. By utilizing the custom Chrome DevTools Protocol (CDP) WebDriver proxy server, you can write E2E tests using standard Appium client libraries in both Node.js (WebdriverIO) and C# (`Appium.WebDriver`) to drive your Avalonia applications.

---

## 1. How Appium Integration Works

The connection structure relies on translating W3C WebDriver commands into Chrome DevTools Protocol (CDP) endpoints over a local WebSocket loop:

```
[ Appium C# or JS Client ]
          │ (W3C WebDriver HTTP Commands)
          ▼
[ Node.js custom Appium Server (Port 4723) ]
          │ (CDP JSON-RPC over WebSocket)
          ▼
[ Avalonia CDP Server (Port 9222) ]
```

---

## 2. Using the C# NuGet Package

We provide the shippable NuGet package **`Chrome.DevTools.Automation.Appium`** which contains all the orchestrator logic to easily set up, launch, and tear down test run processes.

### 1. Installation
Add the package to your test project:
```bash
dotnet add package Chrome.DevTools.Automation.Appium --prerelease
```

This package automatically bundles the Node.js `appium-cdp-driver.js` server script and configures MSBuild to copy it directly to your build output directory, making it instantly runnable.

### 2. Write the Test Suite
Inherit from `CdpAppiumFixture` to automatically manage the application lifecycle:

```csharp
using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using Xunit;
using CDP.Automation.Appium;

public class MySuite : IClassFixture<CdpAppiumFixture>
{
    private readonly CdpAppiumFixture _fixture;
    private readonly AndroidDriver _driver;

    public MySuite(CdpAppiumFixture fixture)
    {
        _fixture = fixture;
        _driver = fixture.Driver ?? throw new InvalidOperationException("Appium driver not initialized.");
    }

    [Fact]
    public void TestButtonClick()
    {
        // Interact with elements using standard selectors
        var button = _driver.FindElement(By.Id("btnClickMe"));
        button.Click();

        var status = _driver.FindElement(By.Id("txtStatus"));
        Assert.Equal("Clicked 1 times!", status.Text);
    }
}
```

### 3. Customize Options
You can configure application paths, ports, headless toggles, and driver scripts by overriding the `GetOptions()` method on the fixture:

```csharp
public class CustomAppiumFixture : CdpAppiumFixture
{
    protected override CdpAppiumOptions GetOptions()
    {
        return new CdpAppiumOptions
        {
            AppPath = "/path/to/my/app.dll",
            Headless = true,
            AppCdpPort = 9222,
            AppiumPort = 4723
        };
    }
}
```

---

## 3. Node.js (WebdriverIO) Setup

If you prefer testing with Node.js/JavaScript, you can use WebdriverIO to connect to the custom proxy server:

1. **Start the Appium Server**:
   ```bash
   node scripts/appium-cdp-driver.js
   ```
2. **Configure your `webdriverio` client options**:
   ```javascript
   import { remote } from 'webdriverio';

   const browser = await remote({
       hostname: '127.0.0.1',
       port: 4723,
       path: '/',
       capabilities: {
           platformName: 'Android',
           'appium:automationName': 'CDP'
       }
   });

   const button = await browser.$('#btnClickMe');
   await button.click();

   await browser.deleteSession();
   ```

---

## 4. Conformance Details
- **W3C/CDP Mapping**: Elements returned by standard queries map to their unique CDP `nodeId` in the page visual tree.
- **isDisplayed() support**: Supports standard W3C visibility evaluations by executing sub-pixel checking and layout bounding rect validations inside the CDP backend.
- **evaluate/execute support**: Fully supports standard WebDriver `/execute/sync` scripts containing raw Javascript (evaluated in Jint sandbox) mapped back to target C# Visual controls.

---

## 5. Useful Links
- [Appium Documentation](http://appium.io/docs/en/about-appium/intro/)
- [Vitepress Guide - Selenium Integration](/articles/selenium)
- [Vitepress Guide - Headless CDP Testing](/articles/headless-cdp-testing)
