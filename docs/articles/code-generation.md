---
title: Code Generation
---

# Code Generation

The CDP Inspector can export recorded test steps to multiple automation framework formats. This enables teams to capture interactions visually and then integrate the generated scripts into their existing CI/CD pipelines and test infrastructure.

## Supported Formats

| Format | Language | Framework | Use Case |
|--------|----------|-----------|----------|
| Puppeteer | JavaScript (Node.js) | Puppeteer | Browser-style CDP automation |
| Playwright Test | TypeScript/JavaScript | Playwright | Cross-browser CDP automation |
| Selenium C# | C# | Selenium + NUnit | .NET WebDriver-based testing |
| Appium C# | C# | Appium + NUnit | Mobile/desktop app testing via WinAppDriver |
| Avalonia Headless | C# | xUnit + Avalonia Headless | In-process Avalonia testing without a window |

## Exporting Code

### From the Inspector UI

1. Record or build your test steps in the [Test Studio](/articles/test-studio)
2. Select the desired output format from the **Format** dropdown in the Recorder tab
3. Click the **Export** button (`#btnExportPuppeteer`)
4. Choose a file save location in the dialog

### Programmatic Export

Each generator implements the `ICodeGenerator` interface:

```csharp
public interface ICodeGenerator
{
    RecordingFormat Format { get; }
    string TabHeader { get; }
    string TitleText { get; }
    string ExportButtonText { get; }
    string Generate(IEnumerable<RecordedStep> steps, string hostAddress);
}
```

Available generators:
- `PuppeteerGenerator`
- `PlaywrightGenerator`
- `SeleniumCSharpGenerator`
- `AppiumCSharpGenerator`
- `AvaloniaHeadlessXUnitGenerator`

### Generating from YAML Recordings

You can also generate scripts directly from visual test recordings saved in YAML format. The conversion is performed by translating parsed flow steps into standard `RecordedStep` elements first.

For code examples and mapping details, see the [YAML Test Recording Conversion Guide](/articles/yaml-conversion).

---

## Puppeteer Script

The Puppeteer generator produces a Node.js script that connects to the CDP server via `puppeteer.connect()`:

```javascript
const puppeteer = require('puppeteer');

(async () => {
  const browser = await puppeteer.connect({
    browserWSEndpoint: 'ws://127.0.0.1:9222/devtools/page/1'
  });

  const pages = await browser.pages();
  const page = pages[0];

  // Step 1: Click #btnLogin
  await page.waitForSelector('#btnLogin');
  await page.click('#btnLogin');

  // Step 2: Type into #txtUsername
  await page.waitForSelector('#txtUsername');
  await page.click('#txtUsername', { clickCount: 3 });
  await page.type('#txtUsername', 'admin');

  // Step 3: Type into #txtPassword
  await page.waitForSelector('#txtPassword');
  await page.click('#txtPassword', { clickCount: 3 });
  await page.type('#txtPassword', 'password123');

  // Step 4: Click #btnSubmit
  await page.waitForSelector('#btnSubmit');
  await page.click('#btnSubmit');

  // Step 5: Assert #lblWelcome is visible
  await page.waitForSelector('#lblWelcome');

  await browser.disconnect();
})();
```

### Features

- Connects via `browserWSEndpoint` to a running CDP server
- Uses `waitForSelector` before each interaction for robustness
- Triple-click to select existing text before typing new values
- Supports click, type, scroll, navigation, and assertions

---

## Playwright Test Script

The Playwright generator produces a TypeScript test using `connectOverCDP`:

```typescript
import { test, expect } from '@playwright/test';

test('recorded flow', async ({ }) => {
  const browser = await require('playwright').chromium.connectOverCDP(
    'http://127.0.0.1:9222'
  );

  const context = browser.contexts()[0];
  const page = context.pages()[0];

  // Step 1: Click #btnLogin
  await page.locator('#btnLogin').click();

  // Step 2: Fill #txtUsername
  await page.locator('#txtUsername').fill('admin');

  // Step 3: Fill #txtPassword
  await page.locator('#txtPassword').fill('password123');

  // Step 4: Click #btnSubmit
  await page.locator('#btnSubmit').click();

  // Step 5: Assert #lblWelcome is visible
  await expect(page.locator('#lblWelcome')).toBeVisible();

  await browser.close();
});
```

### Features

- Uses `connectOverCDP` for direct CDP connection
- Locator-based API with `fill` for text input
- Built-in `expect` assertions
- Automatic waiting via Playwright's locator system

---

## Selenium C# Script

The Selenium generator produces a NUnit test class with `ChromeOptions.DebuggerAddress`:

```csharp
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace GeneratedTests;

[TestFixture]
public class RecordedFlowTest
{
    private ChromeDriver _driver = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new ChromeOptions();
        options.DebuggerAddress = "127.0.0.1:9222";
        _driver = new ChromeDriver(options);
    }

    [TearDown]
    public void TearDown()
    {
        _driver?.Quit();
    }

    [Test]
    public void RecordedFlow()
    {
        // Step 1: Click #btnLogin
        var btnLogin = _driver.FindElement(By.CssSelector("#btnLogin"));
        btnLogin.Click();

        // Step 2: Type into #txtUsername
        var txtUsername = _driver.FindElement(By.CssSelector("#txtUsername"));
        txtUsername.Clear();
        txtUsername.SendKeys("admin");

        // Step 3: Type into #txtPassword
        var txtPassword = _driver.FindElement(By.CssSelector("#txtPassword"));
        txtPassword.Clear();
        txtPassword.SendKeys("password123");

        // Step 4: Click #btnSubmit
        var btnSubmit = _driver.FindElement(By.CssSelector("#btnSubmit"));
        btnSubmit.Click();

        // Step 5: Assert #lblWelcome is visible
        var lblWelcome = _driver.FindElement(By.CssSelector("#lblWelcome"));
        Assert.That(lblWelcome.Displayed, Is.True);
    }
}
```

### Features

- Connects via `DebuggerAddress` to an existing CDP server
- Standard Selenium `FindElement` + `By.CssSelector` API
- `Clear()` before `SendKeys()` for clean text input
- NUnit `Assert.That` for assertions

---

## Appium C# Script

The Appium generator produces a NUnit test using `WindowsDriver`:

```csharp
using NUnit.Framework;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace GeneratedTests;

[TestFixture]
public class RecordedFlowTest
{
    private WindowsDriver _driver = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new AppiumOptions();
        options.PlatformName = "Windows";
        options.AddAdditionalAppiumOption("app", "CdpSampleApp.exe");
        options.AddAdditionalAppiumOption("deviceName", "WindowsPC");

        _driver = new WindowsDriver(
            new Uri("http://127.0.0.1:4723/"),
            options
        );
    }

    [TearDown]
    public void TearDown()
    {
        _driver?.Quit();
    }

    [Test]
    public void RecordedFlow()
    {
        // Step 1: Click btnLogin
        var btnLogin = _driver.FindElement(MobileBy.AccessibilityId("btnLogin"));
        btnLogin.Click();

        // Step 2: Type into txtUsername
        var txtUsername = _driver.FindElement(MobileBy.AccessibilityId("txtUsername"));
        txtUsername.Clear();
        txtUsername.SendKeys("admin");

        // Step 3: Type into txtPassword
        var txtPassword = _driver.FindElement(MobileBy.AccessibilityId("txtPassword"));
        txtPassword.Clear();
        txtPassword.SendKeys("password123");

        // Step 4: Click btnSubmit
        var btnSubmit = _driver.FindElement(MobileBy.AccessibilityId("btnSubmit"));
        btnSubmit.Click();

        // Step 5: Assert lblWelcome is visible
        var lblWelcome = _driver.FindElement(MobileBy.AccessibilityId("lblWelcome"));
        Assert.That(lblWelcome.Displayed, Is.True);
    }
}
```

### Features

- Uses `MobileBy.AccessibilityId` mapped from Avalonia control names
- Connects through WinAppDriver / Appium server
- Standard Appium `WindowsDriver` lifecycle
- Cross-platform potential with Appium mac2 driver

---

## Avalonia Headless xUnit Test

The Avalonia Headless generator produces in-process tests that run without a visible window:

```csharp
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Diagnostics.Cdp.Selector;
using Xunit;

namespace GeneratedTests;

public class RecordedFlowTest
{
    [AvaloniaFact]
    public void RecordedFlow()
    {
        var window = new MainWindow();
        window.Show();

        // Step 1: Click #btnLogin
        var btnLogin = SelectorEngine.QuerySelector(window, "#btnLogin")!;
        btnLogin.Focus();
        window.MouseDown(btnLogin.PointToScreen(
            new Avalonia.Point(
                btnLogin.Bounds.Width / 2,
                btnLogin.Bounds.Height / 2)),
            MouseButton.Left);
        window.MouseUp(btnLogin.PointToScreen(
            new Avalonia.Point(
                btnLogin.Bounds.Width / 2,
                btnLogin.Bounds.Height / 2)),
            MouseButton.Left);

        // Step 2: Type into #txtUsername
        var txtUsername = SelectorEngine.QuerySelector(window, "#txtUsername")!;
        txtUsername.Focus();
        window.KeyTextInput("admin");

        // Step 3: Type into #txtPassword
        var txtPassword = SelectorEngine.QuerySelector(window, "#txtPassword")!;
        txtPassword.Focus();
        window.KeyTextInput("password123");

        // Step 4: Click #btnSubmit
        var btnSubmit = SelectorEngine.QuerySelector(window, "#btnSubmit")!;
        btnSubmit.Focus();
        window.MouseDown(btnSubmit.PointToScreen(
            new Avalonia.Point(
                btnSubmit.Bounds.Width / 2,
                btnSubmit.Bounds.Height / 2)),
            MouseButton.Left);
        window.MouseUp(btnSubmit.PointToScreen(
            new Avalonia.Point(
                btnSubmit.Bounds.Width / 2,
                btnSubmit.Bounds.Height / 2)),
            MouseButton.Left);

        // Step 5: Assert #lblWelcome is visible
        var lblWelcome = SelectorEngine.QuerySelector(window, "#lblWelcome");
        Assert.NotNull(lblWelcome);
        Assert.True(lblWelcome!.IsVisible);
    }
}
```

### Features

- Runs in-process without any window or display server
- Uses `SelectorEngine.QuerySelector` from `Avalonia.Diagnostics.Cdp`
- Direct `MouseDown`/`MouseUp` and `KeyTextInput` simulation
- `[AvaloniaFact]` attribute initializes the Avalonia runtime
- Fastest execution — no network, no CDP server required

---

## Generator Selection Guide

| Scenario | Recommended Format |
|----------|-------------------|
| CI/CD pipeline with existing Playwright infrastructure | Playwright Test |
| Node.js automation scripts | Puppeteer |
| .NET test suite with Selenium WebDriver | Selenium C# |
| Windows desktop app testing via WinAppDriver | Appium C# |
| Fast unit-style tests without external dependencies | Avalonia Headless |
| Cross-platform desktop + mobile testing | Appium C# |

## Next Steps

- [Recorder Overview](/articles/recorder-overview) — Capture interaction architecture
- [Test Studio](/articles/test-studio) — Visual test editing workspace
- [YAML Test Format](/articles/yaml-test-format) — Serialization specification
- [Headless Test Adapter](/articles/headless-test-adapter) — CI/CD execution
