---
title: Multi-Framework CDP Integration
---

# Multi-Framework CDP Integration

Because the Avalonia CDP Server implements standard Chrome DevTools Protocol domains (such as `DOM`, `Input`, `Runtime`, `Page`, and `Network`), it supports automation using unmodified browser testing clients. 

This guide details how to integrate and write test scripts for **Puppeteer**, **Selenium**, and **Appium** to automate your desktop application over Chrome DevTools Protocol.

---

## 1. Puppeteer Integration

Puppeteer is a popular Node.js library for controlling Chromium or Chrome over CDP. It can connect directly to the Avalonia application via the browser WebSocket port or HTTP target list.

### Setup and Script Structure
1.  Install Puppeteer:
    ```bash
    npm install puppeteer
    ```
2.  Write the Puppeteer automation script:

```javascript
const puppeteer = require('puppeteer');

(async () => {
  // Option 1: Discover targets using the HTTP port
  const browser = await puppeteer.connect({
    browserURL: 'http://127.0.0.1:9222'
  });
  
  // Option 2: Connect directly using browser websocket url
  // const browser = await puppeteer.connect({
  //   browserWSEndpoint: 'ws://127.0.0.1:9222/devtools/browser'
  // });
  
  const pages = await browser.pages();
  const page = pages[0];
  
  // Configure mock viewport
  await page.setViewport({ width: 800, height: 600 });
  
  // Wait for the target element to render
  await page.waitForSelector('#txtInput');
  
  // Simulate text input and mouse clicks
  await page.type('#txtInput', 'Hello World');
  await page.click('#btnSubmit');
  
  // Retrieve properties using Jint runtime evaluations
  const statusText = await page.$eval('#txtStatus', el => el.textContent);
  console.log(`Assertion verification: '${statusText}'`);
  
  if (statusText !== 'Clicked 1 times!') {
    throw new Error(`Test failed: Expected click result but got '${statusText}'`);
  }
  
  // Disconnect session
  await browser.disconnect();
})();
```

---

## 2. Selenium 4 CDP Integration

Selenium 4 introduced native support for Chromium DevTools Protocol (CDP) commands. You can leverage the C# ChromeDriver / ChromiumDriver developer tools class to execute raw DevTools protocol calls directly on the Avalonia target process.

### C# Selenium Setup
1.  Install the package `Selenium.WebDriver.ChromeDriver` via NuGet.
2.  Initialize the ChromeDriver targeting the debugger address:

```csharp
using System;
using System.Collections.Generic;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

class SeleniumCdpTests
{
    static void Main()
    {
        var options = new ChromeOptions();
        // Point Selenium to the running Avalonia app's CDP port
        options.DebuggerAddress = "127.0.0.1:9222";
        
        using var driver = new ChromeDriver(options);
        
        // Wait for control and type
        var input = driver.FindElement(By.CssSelector("#txtInput"));
        input.SendKeys("Automated via Selenium");
        
        var submit = driver.FindElement(By.CssSelector("#btnSubmit"));
        submit.Click();
        
        // Send a direct DevTools CDP evaluation command to query the Jint runtime
        var cdpParams = new Dictionary<string, object>
        {
            { "expression", "document.querySelector('#txtStatus').textContent" },
            { "returnByValue", true }
        };
        
        // Executes the Runtime.evaluate command using the WebDriver bridge
        var cdpResult = driver.ExecuteCdpCommand("Runtime.evaluate", cdpParams) as Dictionary<string, object>;
        var resultData = cdpResult?["result"] as Dictionary<string, object>;
        var textVal = resultData?["value"]?.ToString();
        
        System.Diagnostics.Debug.Assert(textVal == "Clicked 1 times!", $"Expected Clicked status but got '{textVal}'");
        Console.WriteLine("Selenium CDP assertion completed successfully!");
    }
}
```

---

## 3. Appium Hybrid CDP Automation

Appium is standard for native desktop app UI automation (using Windows Application Driver or Mac2Driver). In complex enterprise automation scenarios, you can combine Appium's native OS-level window interactions with a concurrent CDP WebSocket connection to verify the application's internal model states, run evaluations, or capture network telemetry.

```text
 Appium Client ---> Appium Driver ---> Native Desktop Window Input Simulation
      |
      +--------------> CDP Port 9222 (WebSocket) ---> VM State & Diagnostics
```

### C# Appium Hybrid Implementation
1.  Launch the target application using standard Appium Capabilities, passing the `--cdp-port` command line switch.
2.  Open a concurrent WebSocket to execute internal evaluations.

```csharp
using System;
using System.Net.Http;
using System.Text.Json.Nodes;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

class AppiumCdpHybridTests
{
    static void Main()
    {
        var options = new AppiumOptions();
        options.AddAdditionalCapability("app", @"C:\Path\To\CdpSampleApp.exe");
        options.AddAdditionalCapability("appArguments", "--cdp-port 9222");

        // 1. Establish Appium Driver session
        using var driver = new WindowsDriver<WindowsElement>(new Uri("http://127.0.0.1:4723"), options);
        
        // Perform standard OS accessibility UI automation actions
        var inputControl = driver.FindElementByAccessibilityId("txtInput");
        inputControl.SendKeys("Hybrid Input Value");
        
        var submitBtn = driver.FindElementByAccessibilityId("btnSubmit");
        submitBtn.Click();

        // 2. Establish concurrent CDP connection to query ViewModel internal bindings
        using var httpClient = new HttpClient();
        var response = httpClient.GetStringAsync("http://127.0.0.1:9222/json").Result;
        var targets = JsonNode.Parse(response)?.AsArray();
        
        var wsUrl = targets?[0]?["webSocketDebuggerUrl"]?.ToString();
        Console.WriteLine($"Concurrently connected to CDP websocket: {wsUrl}");
        
        // Execute evaluations or capture diagnostics on the active target page
        // e.g. verify: Window.DataContext.Connection.IsConnected == true
    }
}
```
