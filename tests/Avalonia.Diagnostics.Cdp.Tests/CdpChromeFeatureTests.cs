using System;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp.Domains;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Xunit;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Threading;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Services;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class CdpChromeFeatureTests
{
    [AvaloniaFact]
    public async Task TestSourcesDomainFilesAndContent()
    {
        var window = new Window { Title = "Sources Test Window" };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // 1. Get Workspace Files
        var result = await SourcesDomain.HandleAsync(session, "getWorkspaceFiles", new JsonObject());
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("files"));

        var files = result["files"] as JsonArray;
        Assert.NotNull(files);
        Assert.True(files.Count > 0);

        // Find a test file
        var fileNode = files.FirstOrDefault(f => f?["name"]?.GetValue<string>() == "TestAppBuilder.cs");
        Assert.NotNull(fileNode);

        string relativePath = fileNode["path"]?.GetValue<string>() ?? "";
        Assert.False(string.IsNullOrEmpty(relativePath));

        // 2. Get File Content
        var contentParams = new JsonObject { ["path"] = relativePath };
        var contentResult = await SourcesDomain.HandleAsync(session, "getFileContent", contentParams);
        Assert.NotNull(contentResult);
        Assert.True(contentResult.ContainsKey("content"));

        string content = contentResult["content"]?.GetValue<string>() ?? "";
        Assert.Contains("public class TestAppBuilder", content);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestMemoryDomainLiveControlsAndGc()
    {
        var window = new Window
        {
            Title = "Memory Test Window",
            Content = new StackPanel
            {
                Children =
                {
                    new Button { Content = "Button 1" },
                    new TextBlock { Text = "Text 1" }
                }
            }
        };
        window.Show();
        CdpServer.Register(window, "Memory Test Window");

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // 1. getLiveControls
        var liveResult = await MemoryDomain.HandleAsync(session, "getLiveControls", new JsonObject());
        Assert.NotNull(liveResult);
        Assert.True(liveResult.ContainsKey("controls"));

        var controls = liveResult["controls"] as JsonArray;
        Assert.NotNull(controls);

        // Assert Button and TextBlock and Window are in counts
        var btnCount = controls.FirstOrDefault(c => c?["type"]?.GetValue<string>() == "Button");
        Assert.NotNull(btnCount);
        Assert.True(btnCount["count"]?.GetValue<int>() >= 1);

        var txtCount = controls.FirstOrDefault(c => c?["type"]?.GetValue<string>() == "TextBlock");
        Assert.NotNull(txtCount);
        Assert.True(txtCount["count"]?.GetValue<int>() >= 1);

        // 2. collectGarbage
        var gcResult = await MemoryDomain.HandleAsync(session, "collectGarbage", new JsonObject());
        Assert.NotNull(gcResult);
        Assert.Empty(gcResult);

        window.Close();
        CdpServer.Unregister(window);
    }

    [AvaloniaFact]
    public async Task TestApplicationDomainResources()
    {
        var window = new Window { Title = "Application Resources Test Window" };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // Add a resource directly in Avalonia
        string testKey = "TestCdpResourceKey";
        string testVal = "#FFFF0000"; // Red Hex color
        
        // 1. Set Resource
        var setParams = new JsonObject
        {
            ["key"] = testKey,
            ["value"] = testVal
        };
        var setRes = await ApplicationDomain.HandleAsync(session, "setResource", setParams);
        Assert.NotNull(setRes);

        // Verify key exists in Application.Current.Resources
        Assert.NotNull(Application.Current);
        Assert.True(Application.Current.Resources.ContainsKey(testKey));

        // 2. Get Resources
        var getRes = await ApplicationDomain.HandleAsync(session, "getResources", new JsonObject());
        Assert.NotNull(getRes);
        Assert.True(getRes.ContainsKey("resources"));

        var resources = getRes["resources"] as JsonArray;
        Assert.NotNull(resources);
        
        var entry = resources.FirstOrDefault(r => r?["key"]?.GetValue<string>() == testKey);
        Assert.NotNull(entry);
        Assert.Contains("SolidColorBrush", entry["type"]?.GetValue<string>() ?? "");

        // 3. Delete Resource
        var deleteParams = new JsonObject { ["key"] = testKey };
        var deleteRes = await ApplicationDomain.HandleAsync(session, "deleteResource", deleteParams);
        Assert.NotNull(deleteRes);

        // Verify resource removed
        Assert.False(Application.Current.Resources.ContainsKey(testKey));

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestNetworkDomainObserverExecution()
    {
        var window = new Window { Title = "Network Test Window" };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // Enable network domain
        var enableResult = await NetworkDomain.HandleAsync(session, "enable", new JsonObject());
        Assert.NotNull(enableResult);

        // Directly invoke start and stop events to verify mapping logic does not throw
        var request = new HttpRequestMessage(HttpMethod.Get, "https://jsonplaceholder.typicode.com/todos/1");
        NetworkDomain.OnRequestStart(request);

        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent("{ \"id\": 1 }")
        };
        NetworkDomain.OnRequestStop(request, response);

        // Disable network domain
        var disableResult = await NetworkDomain.HandleAsync(session, "disable", new JsonObject());
        Assert.NotNull(disableResult);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestRecorderDomainAndSelector()
    {
        var button = new Button { Name = "myTestButton" };
        var textBox = new TextBox { Name = "myTestTextBox" };
        var panel = new StackPanel
        {
            Children =
            {
                button,
                textBox
            }
        };
        var window = new Window
        {
            Title = "Recorder Test Window",
            Content = panel
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // 1. Assert selector generation
        string buttonSelector = SelectorEngine.GetSelector(button);
        Assert.Equal("#myTestButton", buttonSelector);

        string textBoxSelector = SelectorEngine.GetSelector(textBox);
        Assert.Equal("#myTestTextBox", textBoxSelector);

        // Assert nested selector when no Name is set
        var anonymousButton = new Button();
        panel.Children.Add(anonymousButton);
        string anonSelector = SelectorEngine.GetSelector(anonymousButton);
        Assert.Contains("StackPanel > Button", anonSelector);

        // 2. Start Recording
        var startRes = await RecorderDomain.HandleAsync(session, "start", new JsonObject());
        Assert.NotNull(startRes);

        // 3. Stop Recording
        var stopRes = await RecorderDomain.HandleAsync(session, "stop", new JsonObject());
        Assert.NotNull(stopRes);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestCustomCdpPort()
    {
        CdpServer.Start(9235);
        try
        {
            Assert.Equal(9235, CdpServer.Port);

            var window = new Window { Title = "Port Test Window" };
            window.Show();

            using var clientWs = new ClientWebSocket();
            var session = new CdpSession(clientWs, window);

            var getDocParams = new JsonObject { ["depth"] = -1 };
            var docResult = await DomDomain.HandleAsync(session, "getDocument", getDocParams);
            Assert.NotNull(docResult);

            var root = docResult["root"] as JsonObject;
            Assert.NotNull(root);

            Assert.Equal("http://localhost:9235/", root["documentURL"]?.GetValue<string>());
            Assert.Equal("http://localhost:9235/", root["baseURL"]?.GetValue<string>());

            window.Close();
        }
        finally
        {
            CdpServer.Stop();
        }
    }

    [Fact]
    public void TestRecordingParser()
    {
        // 1. JSON parse test
        string jsonContent = @"
        {
          ""title"": ""Test Recording"",
          ""steps"": [
            {
              ""type"": ""setViewport"",
              ""width"": 1024,
              ""height"": 768
            },
            {
              ""type"": ""navigate"",
              ""url"": ""http://localhost:9222/home""
            },
            {
              ""type"": ""click"",
              ""selectors"": [[""#btnClickMe""]],
              ""offsetX"": 10,
              ""offsetY"": 20,
              ""button"": ""right"",
              ""clickCount"": 2,
              ""modifiers"": 4
            },
            {
              ""type"": ""change"",
              ""selectors"": [[""#txtInput""]],
              ""value"": ""Hello World""
            },
            {
              ""type"": ""keydown"",
              ""key"": ""Enter"",
              ""modifiers"": 2
            },
            {
              ""type"": ""dragAndDrop"",
              ""selectors"": [[""#dragSrc""]],
              ""targetSelectors"": [[""#dragTgt""]],
              ""offsetX"": 5,
              ""offsetY"": 5,
              ""targetOffsetX"": 15,
              ""targetOffsetY"": 15,
              ""modifiers"": 0
            }
          ]
        }";

        var jsonSteps = RecordingParser.Parse(jsonContent);
        Assert.Equal(6, jsonSteps.Count);

        Assert.Equal("setViewport", jsonSteps[0].Type);
        Assert.Equal(1024, jsonSteps[0].Width);
        Assert.Equal(768, jsonSteps[0].Height);

        Assert.Equal("navigate", jsonSteps[1].Type);
        Assert.Equal("http://localhost:9222/home", jsonSteps[1].Url);

        Assert.Equal("click", jsonSteps[2].Type);
        Assert.Equal("#btnClickMe", jsonSteps[2].Selector);
        Assert.Equal(10, jsonSteps[2].OffsetX);
        Assert.Equal(20, jsonSteps[2].OffsetY);
        Assert.Equal("right", jsonSteps[2].Button);
        Assert.Equal(2, jsonSteps[2].ClickCount);
        Assert.Equal(4, jsonSteps[2].Modifiers);

        Assert.Equal("change", jsonSteps[3].Type);
        Assert.Equal("#txtInput", jsonSteps[3].Selector);
        Assert.Equal("Hello World", jsonSteps[3].Value);

        Assert.Equal("keydown", jsonSteps[4].Type);
        Assert.Equal("Enter", jsonSteps[4].Key);
        Assert.Equal(2, jsonSteps[4].Modifiers);

        Assert.Equal("dragAndDrop", jsonSteps[5].Type);
        Assert.Equal("#dragSrc", jsonSteps[5].Selector);
        Assert.Equal("#dragTgt", jsonSteps[5].TargetSelector);
        Assert.Equal(5, jsonSteps[5].OffsetX);
        Assert.Equal(5, jsonSteps[5].OffsetY);
        Assert.Equal(15, jsonSteps[5].TargetOffsetX);
        Assert.Equal(15, jsonSteps[5].TargetOffsetY);

        // 2. Puppeteer JS parse test
        string jsContent = @"
        const puppeteer = require('puppeteer');
        (async () => {
          const browser = await puppeteer.launch({ headless: false });
          const page = await browser.newPage();
          await page.setViewport({ width: 1200, height: 900 });
          await page.goto('http://localhost:9222/dashboard');
          const element_0 = await page.waitForSelector('#btnClickMe');
          await element_0.click({ button: 'right', clickCount: 2 });
          const element_1 = await page.waitForSelector('#txtInput');
          await element_1.type('Avalonia CDP Automation!');
          const dragSrc = await page.waitForSelector('#dragSrc');
          const dragTgt = await page.waitForSelector('#dragTgt');
          await dragSrc.dragTo(dragTgt);
          await page.keyboard.press('Tab');
          await browser.close();
        })();";

        var jsSteps = RecordingParser.Parse(jsContent);
        Assert.Equal(6, jsSteps.Count);

        Assert.Equal("setViewport", jsSteps[0].Type);
        Assert.Equal(1200, jsSteps[0].Width);
        Assert.Equal(900, jsSteps[0].Height);

        Assert.Equal("navigate", jsSteps[1].Type);
        Assert.Equal("http://localhost:9222/dashboard", jsSteps[1].Url);

        Assert.Equal("click", jsSteps[2].Type);
        Assert.Equal("#btnClickMe", jsSteps[2].Selector);

        Assert.Equal("change", jsSteps[3].Type);
        Assert.Equal("#txtInput", jsSteps[3].Selector);
        Assert.Equal("Avalonia CDP Automation!", jsSteps[3].Value);

        Assert.Equal("dragAndDrop", jsSteps[4].Type);
        Assert.Equal("#dragSrc", jsSteps[4].Selector);
        Assert.Equal("#dragTgt", jsSteps[4].TargetSelector);

        Assert.Equal("keydown", jsSteps[5].Type);
        Assert.Equal("Tab", jsSteps[5].Key);
    }

    [Fact]
    public void TestPlaywrightScriptGenerationAndParsing()
    {
        // 1. Playwright JS parse test
        string pwContent = @"
        import { test, expect, chromium } from '@playwright/test';

        test.describe('CDP Recorded Tests', () => {
          test('recorded test', async () => {
            const browser = await chromium.connectOverCDP('http://localhost:9222');
            const context = browser.contexts()[0];
            const page = context.pages()[0];

            await test.step('Set viewport size', async () => {
              await page.setViewportSize({ width: 1400, height: 1050 });
            });
            await test.step('Navigate to profile', async () => {
              await page.goto('http://localhost:9222/profile');
            });
            await test.step('Click on #btnSave', async () => {
              const element_0 = page.locator('#btnSave');
              await element_0.click({ button: 'right', clickCount: 3, modifiers: ['Shift', 'Control'] });
            });
            await test.step('Type text', async () => {
              const element_1 = page.locator('#txtBio');
              await element_1.fill('Testing Playwright Support!');
            });
            await test.step('Drag and drop', async () => {
              const dragSrc = page.locator('#item1');
              const dragTgt = page.locator('#item2');
              await dragSrc.dragTo(dragTgt);
            });
            await test.step('Press Escape', async () => {
              await page.keyboard.press('Escape');
            });
            await test.step('Verify visibility', async () => {
              await expect(page.locator('#btnSave')).toBeVisible();
              await expect(page.locator('#btnCancel')).toBeHidden();
            });
            await browser.close();
          });
        });";

        var pwSteps = RecordingParser.Parse(pwContent);
        Assert.Equal(8, pwSteps.Count);

        Assert.Equal("setViewport", pwSteps[0].Type);
        Assert.Equal(1400, pwSteps[0].Width);
        Assert.Equal(1050, pwSteps[0].Height);

        Assert.Equal("navigate", pwSteps[1].Type);
        Assert.Equal("http://localhost:9222/profile", pwSteps[1].Url);

        Assert.Equal("click", pwSteps[2].Type);
        Assert.Equal("#btnSave", pwSteps[2].Selector);
        Assert.Equal("right", pwSteps[2].Button);
        Assert.Equal(3, pwSteps[2].ClickCount);
        // Modifiers: Shift=4, Control=2 -> 4 + 2 = 6
        Assert.Equal(6, pwSteps[2].Modifiers);

        Assert.Equal("change", pwSteps[3].Type);
        Assert.Equal("#txtBio", pwSteps[3].Selector);
        Assert.Equal("Testing Playwright Support!", pwSteps[3].Value);

        Assert.Equal("dragAndDrop", pwSteps[4].Type);
        Assert.Equal("#item1", pwSteps[4].Selector);
        Assert.Equal("#item2", pwSteps[4].TargetSelector);

        Assert.Equal("keydown", pwSteps[5].Type);
        Assert.Equal("Escape", pwSteps[5].Key);

        Assert.Equal("assertVisible", pwSteps[6].Type);
        Assert.Equal("#btnSave", pwSteps[6].Selector);

        Assert.Equal("assertNotVisible", pwSteps[7].Type);
        Assert.Equal("#btnCancel", pwSteps[7].Selector);

        // 2. Generation test
        var stepsList = new List<CdpInspectorApp.Models.RecordedStepModel>
        {
            new CdpInspectorApp.Models.RecordedStepModel { Type = "setViewport", Width = 1400, Height = 1050 },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "navigate", Url = "http://localhost:9222/profile" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "click", Selector = "#btnSave", Button = "right", ClickCount = 3, Modifiers = 6 },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "change", Selector = "#txtBio", Value = "Testing Playwright Support!" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "dragAndDrop", Selector = "#item1", TargetSelector = "#item2" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "keydown", Key = "Escape" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "assertVisible", Selector = "#btnSave" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "assertNotVisible", Selector = "#btnCancel" }
        };

        var vm = new RecorderViewModel(new AccessibilitySearchTests.MockCdpService(), () => "localhost:9222");
        vm.SelectedFormat = RecordingFormat.PlaywrightTest;
        vm.LoadParsedSteps(stepsList);

        string generated = vm.GeneratedCode;
        Assert.Contains("import { test, expect, chromium } from '@playwright/test';", generated);
        Assert.Contains("test.describe('CDP Recorded Tests', () => {", generated);
        Assert.Contains("await test.step('Click on element #btnSave', async () => {", generated);
        Assert.Contains("await test.step('Assert element #btnSave is visible', async () => {", generated);
        Assert.Contains("await expect(page.locator('#btnSave')).toBeVisible();", generated);
        Assert.Contains("await expect(page.locator('#btnCancel')).toBeHidden();", generated);
        Assert.Contains("chromium.connectOverCDP('http://localhost:9222')", generated);
        Assert.Contains("page.locator('#btnSave')", generated);
        Assert.Contains("button: 'right'", generated);
        Assert.Contains("clickCount: 3", generated);
        Assert.Contains("modifiers: ['Control', 'Shift']", generated);
    }

    [Fact]
    public void TestEscapedStringLiteralParsingAndGeneration()
    {
        // 1. Parser verification for mixed/escaped quotes
        string jsInput = @"
        const element_0 = await page.waitForSelector(':contains(""Save"")');
        await element_0.type('don\'t');
        const element_1 = page.locator("":contains('Cancel')"");
        await element_1.fill(""I said \""yes\"" and backslash \\ test"");
        await page.goto('http://localhost:9222/foo?x=\'bar\'');
        await expect(page.locator("":contains('Save')"")).toBeVisible();
        await page.waitForSelector(':contains(""Cancel"")', { hidden: true });
        ";

        var parsed = RecordingParser.Parse(jsInput);
        Assert.Equal(5, parsed.Count);

        Assert.Equal("change", parsed[0].Type);
        Assert.Equal(":contains(\"Save\")", parsed[0].Selector);
        Assert.Equal("don't", parsed[0].Value);

        Assert.Equal("change", parsed[1].Type);
        Assert.Equal(":contains('Cancel')", parsed[1].Selector);
        Assert.Equal("I said \"yes\" and backslash \\ test", parsed[1].Value);

        Assert.Equal("navigate", parsed[2].Type);
        Assert.Equal("http://localhost:9222/foo?x='bar'", parsed[2].Url);

        Assert.Equal("assertVisible", parsed[3].Type);
        Assert.Equal(":contains('Save')", parsed[3].Selector);

        Assert.Equal("assertNotVisible", parsed[4].Type);
        Assert.Equal(":contains(\"Cancel\")", parsed[4].Selector);

        // 2. Generator verification (Playwright format)
        var steps = new List<CdpInspectorApp.Models.RecordedStepModel>
        {
            new CdpInspectorApp.Models.RecordedStepModel 
            { 
                Type = "change", 
                Selector = ":contains(\"Save\")", 
                Value = "don't" 
            },
            new CdpInspectorApp.Models.RecordedStepModel 
            { 
                Type = "change", 
                Selector = ":contains('Cancel')", 
                Value = "I said \"yes\" and backslash \\ test" 
            }
        };

        var vm = new RecorderViewModel(new AccessibilitySearchTests.MockCdpService(), () => "localhost:9222");
        vm.SelectedFormat = RecordingFormat.PlaywrightTest;
        vm.LoadParsedSteps(steps);

        string generated = vm.GeneratedCode;

        Assert.Contains(@"await test.step('Type text in element :contains(""Save"")', async () => {", generated);
        Assert.Contains(@"page.locator(':contains(""Save"")')", generated);
        Assert.Contains(@"fill('don\'t')", generated);

        Assert.Contains(@"await test.step('Type text in element :contains(\'Cancel\')', async () => {", generated);
        Assert.Contains(@"page.locator(':contains(\'Cancel\')')", generated);
        Assert.Contains(@"fill('I said ""yes"" and backslash \\ test')", generated);
    }

    [Fact]
    public void TestSeleniumCSharpCodeGeneration()
    {
        var steps = new List<CdpInspectorApp.Models.RecordedStepModel>
        {
            new CdpInspectorApp.Models.RecordedStepModel { Type = "setViewport", Width = 1024, Height = 768 },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "navigate", Url = "http://localhost:9222/foo" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "click", Selector = "#btnClick" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "change", Selector = "#txtInput", Value = "hello \"world\" \\ test" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "assertVisible", Selector = "#btnClick" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "assertNotVisible", Selector = "#hidden" }
        };

        var generator = new SeleniumCSharpGenerator();
        string generated = generator.Generate(steps, "localhost:9222");

        Assert.Contains("using OpenQA.Selenium;", generated);
        Assert.Contains("using OpenQA.Selenium.Chrome;", generated);
        Assert.Contains("options.DebuggerAddress = \"localhost:9222\";", generated);
        Assert.Contains("_driver.Manage().Window.Size = new Size(1024, 768);", generated);
        Assert.Contains("_driver.Navigate().GoToUrl(\"http://localhost:9222/foo\");", generated);
        Assert.Contains("_driver.FindElement(By.CssSelector(\"#btnClick\")).Click();", generated);
        Assert.Contains("var element_3 = _driver.FindElement(By.CssSelector(\"#txtInput\"));", generated);
        Assert.Contains("element_3.SendKeys(\"hello \\\"world\\\" \\\\ test\");", generated);
        Assert.Contains("Assert.IsTrue(_driver.FindElement(By.CssSelector(\"#btnClick\")).Displayed);", generated);
        Assert.Contains("Assert.IsFalse(isVisible_5);", generated);
    }

    [Fact]
    public void TestAppiumCSharpCodeGeneration()
    {
        var steps = new List<CdpInspectorApp.Models.RecordedStepModel>
        {
            new CdpInspectorApp.Models.RecordedStepModel { Type = "setViewport", Width = 1024, Height = 768 },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "navigate", Url = "http://localhost:9222/foo" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "click", Selector = "#btnClick" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "change", Selector = "#txtInput", Value = "hello \"world\" \\ test" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "assertVisible", Selector = "#btnClick" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "assertNotVisible", Selector = "#hidden" }
        };

        var generator = new AppiumCSharpGenerator();
        string generated = generator.Generate(steps, "localhost:9222");

        Assert.Contains("using OpenQA.Selenium.Appium;", generated);
        Assert.Contains("using OpenQA.Selenium.Appium.Windows;", generated);
        Assert.Contains("options.AddAdditionalCapability(\"platformName\", \"Windows\");", generated);
        Assert.Contains("_driver.Manage().Window.Size = new Size(1024, 768);", generated);
        Assert.Contains("_driver.Navigate().GoToUrl(\"http://localhost:9222/foo\");", generated);
        Assert.Contains("_driver.FindElementByAccessibilityId(\"btnClick\").Click();", generated);
        Assert.Contains("var element_3 = _driver.FindElementByAccessibilityId(\"txtInput\");", generated);
        Assert.Contains("element_3.SendKeys(\"hello \\\"world\\\" \\\\ test\");", generated);
        Assert.Contains("Assert.IsTrue(_driver.FindElementByAccessibilityId(\"btnClick\").Displayed);", generated);
        Assert.Contains("Assert.IsFalse(isVisible_5);", generated);
    }

    [Fact]
    public void TestAvaloniaHeadlessXUnitCodeGeneration()
    {
        var steps = new List<CdpInspectorApp.Models.RecordedStepModel>
        {
            new CdpInspectorApp.Models.RecordedStepModel { Type = "setViewport", Width = 1024, Height = 768 },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "navigate", Url = "http://localhost:9222/foo" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "click", Selector = "#btnClick", Button = "right", ClickCount = 3, Modifiers = 6 }, // Control=2, Shift=4
            new CdpInspectorApp.Models.RecordedStepModel { Type = "change", Selector = "#txtInput", Value = "hello \"world\" \\ test" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "keydown", Key = "Enter", Modifiers = 2 }, // Control=2
            new CdpInspectorApp.Models.RecordedStepModel { Type = "dragAndDrop", Selector = "#src", TargetSelector = "#dst" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "assertVisible", Selector = "#btnClick" },
            new CdpInspectorApp.Models.RecordedStepModel { Type = "assertNotVisible", Selector = "#hidden" }
        };

        var generator = new AvaloniaHeadlessXUnitGenerator();
        string generated = generator.Generate(steps, "localhost:9222");

        Assert.Contains("using Avalonia.Headless.XUnit;", generated);
        Assert.Contains("using Avalonia.Diagnostics.Cdp;", generated);
        Assert.Contains("window.Width = 1024;", generated);
        Assert.Contains("window.Height = 768;", generated);
        Assert.Contains("mainWin.Navigate(\"http://localhost:9222/foo\");", generated);
        Assert.Contains("var element_2 = SelectorEngine.QuerySelector(window, \"#btnClick\") as Control;", generated);
        Assert.Contains("ClickControl(window, element_2, MouseButton.Right, RawInputModifiers.Control | RawInputModifiers.Shift);", generated);
        Assert.Contains("for (int c_2 = 0; c_2 < 3; c_2++)", generated);
        Assert.Contains("var element_3 = SelectorEngine.QuerySelector(window, \"#txtInput\") as Control;", generated);
        Assert.Contains("element_3.Focus();", generated);
        Assert.Contains("window.KeyTextInput(\"hello \\\"world\\\" \\\\ test\");", generated);
        Assert.Contains("window.KeyPress(Key.Enter, RawInputModifiers.Control);", generated);
        Assert.Contains("window.KeyRelease(Key.Enter, RawInputModifiers.Control);", generated);
        Assert.Contains("var source_5 = SelectorEngine.QuerySelector(window, \"#src\") as Control;", generated);
        Assert.Contains("var target_5 = SelectorEngine.QuerySelector(window, \"#dst\") as Control;", generated);
        Assert.Contains("DragAndDrop(window, source_5, target_5);", generated);
        Assert.Contains("Assert.True(element_6.IsVisible);", generated);
        Assert.Contains("Assert.True(element_7 == null || !element_7.IsVisible);", generated);
    }

    [AvaloniaFact]
    public void TestRecorderDragAndDropTargetResolution()
    {
        var thumb = new Button { Name = "thumb" };
        var track = new Border { Name = "myTrack", Child = thumb };
        var slider = new StackPanel { Name = "mySlider", Children = { track } };
        var window = new Window
        {
            Title = "Drag Test Window",
            Content = slider
        };
        window.Show();

        var startControl = thumb;
        var endControl = thumb;

        var current = endControl as Avalonia.Visual;
        Control? resolvedTarget = null;
        while (current != null)
        {
            bool isDescendant = false;
            var parent = current.GetVisualParent();
            while (parent != null)
            {
                if (parent == startControl)
                {
                    isDescendant = true;
                    break;
                }
                parent = parent.GetVisualParent();
            }

            if (current != startControl && !isDescendant)
            {
                if (current is Control parentControl)
                {
                    resolvedTarget = parentControl;
                    break;
                }
            }
            current = current.GetVisualParent();
        }

        Assert.NotNull(resolvedTarget);
        Assert.Equal("myTrack", resolvedTarget.Name);

        window.Close();
    }

    [AvaloniaFact]
    public void TestHighlightOverlayCreationAndRemoval()
    {
        var button = new Button { Name = "testButton" };
        var window = new Window
        {
            Title = "Highlight Test Window",
            Content = button
        };
        window.Show();

        // 1. Show Highlight
        HighlightOverlayManager.ShowHighlight(window, button);

        var adornerLayer = Avalonia.Controls.Primitives.AdornerLayer.GetAdornerLayer(button);
        Assert.NotNull(adornerLayer);

        var adorner = adornerLayer.Children.FirstOrDefault(c => c is HighlightAdorner);
        Assert.NotNull(adorner);
        Assert.Equal(button, ((HighlightAdorner)adorner).AdornedVisual);

        // 2. Hide Highlight
        HighlightOverlayManager.HideHighlight(window);

        var adornerAfterHide = adornerLayer.Children.FirstOrDefault(c => c is HighlightAdorner);
        Assert.Null(adornerAfterHide);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestPageScreencast()
    {
        var window = new Window { Title = "Screencast Test Window" };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // 1. Start Screencast
        var startResult = await PageDomain.HandleAsync(session, "startScreencast", new JsonObject());
        Assert.NotNull(startResult);

        // 2. Acknowledge a frame (simulating browser behavior)
        session.AcknowledgeScreencastFrame(1);

        // 3. Stop Screencast
        var stopResult = await PageDomain.HandleAsync(session, "stopScreencast", new JsonObject());
        Assert.NotNull(stopResult);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestPageScreencastWithOptions()
    {
        var window = new Window { Title = "Screencast Test Window", Width = 300, Height = 200 };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        var @params = new JsonObject
        {
            ["format"] = "jpeg",
            ["quality"] = 80,
            ["maxWidth"] = 150,
            ["maxHeight"] = 150
        };

        var startResult = await PageDomain.HandleAsync(session, "startScreencast", @params);
        Assert.NotNull(startResult);

        session.RequestScreencastFrame();

        int retries = 50;
        string? screencastFrameMsg = null;
        while (retries-- > 0 && screencastFrameMsg == null)
        {
            await Task.Delay(50);
            lock (fakeWs.SentMessages)
            {
                screencastFrameMsg = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("Page.screencastFrame"));
            }
        }

        Assert.NotNull(screencastFrameMsg);

        var node = System.Text.Json.Nodes.JsonNode.Parse(screencastFrameMsg);
        Assert.NotNull(node);
        Assert.Equal("Page.screencastFrame", node["method"]?.GetValue<string>());

        var frameParams = node["params"];
        Assert.NotNull(frameParams);
        var data = frameParams["data"]?.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(data));

        var metadata = frameParams["metadata"];
        Assert.NotNull(metadata);

        var deviceWidth = metadata["deviceWidth"]?.GetValue<double>();
        Assert.Equal(150, deviceWidth);

        var stopResult = await PageDomain.HandleAsync(session, "stopScreencast", new JsonObject());
        Assert.NotNull(stopResult);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TestPageScreencastEveryNthFrame()
    {
        var window = new Window { Title = "Screencast Nth Frame Window", Width = 300, Height = 200 };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        var @params = new JsonObject
        {
            ["everyNthFrame"] = 2
        };

        var startResult = await PageDomain.HandleAsync(session, "startScreencast", @params);
        Assert.NotNull(startResult);

        // 1. First frame (Counter = 1) is sent immediately upon startScreencast
        int retries = 50;
        string? frame1Msg = null;
        while (retries-- > 0 && frame1Msg == null)
        {
            await Task.Delay(50);
            lock (fakeWs.SentMessages)
            {
                frame1Msg = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("Page.screencastFrame"));
            }
        }
        Assert.NotNull(frame1Msg);

        // 2. Trigger another frame. This will be Counter = 2.
        // Since 2 % 2 == 0, this frame should be sent.
        lock (fakeWs.SentMessages)
        {
            fakeWs.SentMessages.Clear();
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            window.Background = Avalonia.Media.Brushes.Red;
        });

        session.RequestScreencastFrame();

        retries = 50;
        string? frame2Msg = null;
        while (retries-- > 0 && frame2Msg == null)
        {
            await Task.Delay(50);
            lock (fakeWs.SentMessages)
            {
                frame2Msg = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("Page.screencastFrame"));
            }
        }
        Assert.NotNull(frame2Msg);

        // 3. Trigger another frame. This will be Counter = 3.
        // Since 3 % 2 != 0, this frame should be SKIPPED.
        lock (fakeWs.SentMessages)
        {
            fakeWs.SentMessages.Clear();
        }

        session.RequestScreencastFrame();
        await Task.Delay(200);

        lock (fakeWs.SentMessages)
        {
            Assert.Empty(fakeWs.SentMessages.Where(m => m.Contains("Page.screencastFrame")));
        }

        var stopResult = await PageDomain.HandleAsync(session, "stopScreencast", new JsonObject());
        Assert.NotNull(stopResult);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TestPageScreencastVisibilityChanged()
    {
        var window = new Window { Title = "Screencast Visibility Window" };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        var startResult = await PageDomain.HandleAsync(session, "startScreencast", new JsonObject());
        Assert.NotNull(startResult);

        window.IsVisible = false;

        int retries = 50;
        string? visibilityMsg = null;
        while (retries-- > 0 && visibilityMsg == null)
        {
            await Task.Delay(50);
            lock (fakeWs.SentMessages)
            {
                visibilityMsg = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("Page.screencastVisibilityChanged"));
            }
        }

        Assert.NotNull(visibilityMsg);
        var node = System.Text.Json.Nodes.JsonNode.Parse(visibilityMsg);
        Assert.NotNull(node);
        Assert.Equal("Page.screencastVisibilityChanged", node["method"]?.GetValue<string>());
        var visibleVal = node["params"]?["visible"]?.GetValue<bool>();
        Assert.False(visibleVal);

        var stopResult = await PageDomain.HandleAsync(session, "stopScreencast", new JsonObject());
        Assert.NotNull(stopResult);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TestPageScreencastDuplicateFrameSkipping()
    {
        var window = new Window { Title = "Screencast Duplicate Frame Window", Width = 300, Height = 200 };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        // 1. Start Screencast
        var startResult = await PageDomain.HandleAsync(session, "startScreencast", new JsonObject());
        Assert.NotNull(startResult);

        // First frame should be captured automatically
        int retries = 50;
        string? firstFrameMsg = null;
        while (retries-- > 0 && firstFrameMsg == null)
        {
            await Task.Delay(50);
            lock (fakeWs.SentMessages)
            {
                firstFrameMsg = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("Page.screencastFrame"));
            }
        }

        Assert.NotNull(firstFrameMsg);
        
        // Clear sent messages to make checking the next one easy
        lock (fakeWs.SentMessages)
        {
            fakeWs.SentMessages.Clear();
        }

        // Acknowledge the frame (this triggers a request for the next frame and releases the backpressure semaphore)
        session.AcknowledgeScreencastFrame(1);

        // Wait a bit to let the loop run and perform change detection (delta compression).
        // Since there are no visual changes, the second frame should be skipped.
        await Task.Delay(200);

        lock (fakeWs.SentMessages)
        {
            var duplicateMsg = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("Page.screencastFrame"));
            Assert.Null(duplicateMsg); // Should be skipped!
        }

        var stopResult = await PageDomain.HandleAsync(session, "stopScreencast", new JsonObject());
        Assert.NotNull(stopResult);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TestDomMutationsAdditionAndRemoval()
    {
        var panel = new StackPanel();
        var window = new Window
        {
            Title = "DOM Mutation Test Window",
            Content = panel
        };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        await DomDomain.HandleAsync(session, "enable", new JsonObject());

        var button = new Button { Name = "myNewBtn" };
        panel.Children.Add(button);

        await Task.Delay(100);

        var insertedMsg = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("DOM.childNodeInserted"));
        Assert.NotNull(insertedMsg);
        Assert.Contains("myNewBtn", insertedMsg);
        Assert.Contains("parentNodeId", insertedMsg);
        Assert.DoesNotContain("parentId", insertedMsg);

        panel.Children.Remove(button);
        await Task.Delay(100);

        var removedMsg = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("DOM.childNodeRemoved"));
        Assert.NotNull(removedMsg);
        Assert.Contains("parentNodeId", removedMsg);
        Assert.DoesNotContain("parentId", removedMsg);

        session.StopObservingVisualTree();
        window.Close();
    }

    [AvaloniaFact]
    public async Task TestDomAttributeMutations()
    {
        var button = new Button { Name = "btnInit" };
        var window = new Window
        {
            Title = "Attribute Test Window",
            Content = button
        };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        await DomDomain.HandleAsync(session, "enable", new JsonObject());

        button.Classes.Add("primary");
        button.IsEnabled = false;

        await Task.Delay(100);

        var modifiedMsgs = fakeWs.SentMessages.Where(m => m.Contains("DOM.attributeModified")).ToList();
        Assert.NotEmpty(modifiedMsgs);
        Assert.Contains(modifiedMsgs, m => m.Contains("primary"));
        Assert.Contains(modifiedMsgs, m => m.Contains("false"));

        session.StopObservingVisualTree();
        window.Close();
    }

    [AvaloniaFact]
    public async Task TestExecutionContextCreatedEvent()
    {
        var window = new Window { Title = "Context Test Window" };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        await RuntimeDomain.HandleAsync(session, "enable", new JsonObject());

        var contextMsg = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("Runtime.executionContextCreated"));
        Assert.NotNull(contextMsg);
        Assert.Contains("top", contextMsg);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestPageResourcesAndContent()
    {
        var window = new Window { Title = "Page Resources Test Window" };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        var treeResult = await PageDomain.HandleAsync(session, "getResourceTree", new JsonObject());
        Assert.NotNull(treeResult);
        Assert.True(treeResult.ContainsKey("frameTree"));
        var frameTree = treeResult["frameTree"] as JsonObject;
        Assert.NotNull(frameTree);
        var resources = frameTree["resources"] as JsonArray;
        Assert.NotNull(resources);
        Assert.True(resources.Count > 0);

        var fileUrlNode = resources.FirstOrDefault(r => r?["url"]?.GetValue<string>()?.Contains("TestAppBuilder.cs") == true);
        Assert.NotNull(fileUrlNode);
        var url = fileUrlNode["url"]?.GetValue<string>() ?? "";

        var contentParams = new JsonObject { ["url"] = url };
        var contentResult = await PageDomain.HandleAsync(session, "getResourceContent", contentParams);
        Assert.NotNull(contentResult);
        Assert.True(contentResult.ContainsKey("content"));
        var content = contentResult["content"]?.GetValue<string>() ?? "";
        Assert.Contains("public class TestAppBuilder", content);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestEmulationThemeAndLocale()
    {
        var window = new Window { Title = "Emulation Test Window" };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        // 1. Test setEmulatedColorSchemeOverride
        var colorSchemeParams = new JsonObject { ["colorScheme"] = "dark" };
        var res1 = await EmulationDomain.HandleAsync(session, "setEmulatedColorSchemeOverride", colorSchemeParams);
        Assert.NotNull(res1);
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);

        // 2. Test setEmulatedMedia
        var mediaParams = new JsonObject
        {
            ["features"] = new JsonArray
            {
                new JsonObject { ["name"] = "prefers-color-scheme", ["value"] = "light" }
            }
        };
        var res2 = await EmulationDomain.HandleAsync(session, "setEmulatedMedia", mediaParams);
        Assert.NotNull(res2);
        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);

        // 3. Test setLocaleOverride
        var localeParams = new JsonObject { ["locale"] = "de-DE" };
        var res3 = await EmulationDomain.HandleAsync(session, "setLocaleOverride", localeParams);
        Assert.NotNull(res3);
        Assert.Equal("de-DE", System.Globalization.CultureInfo.CurrentCulture.Name);

        // Restore defaults
        Application.Current!.RequestedThemeVariant = ThemeVariant.Default;

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestNetworkAndRuntimeClearingAndThrottling()
    {
        var window = new Window { Title = "Console & Network Override Test Window" };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        // 1. Test Runtime.discardConsoleEntries
        var discardRes = await RuntimeDomain.HandleAsync(session, "discardConsoleEntries", new JsonObject());
        Assert.NotNull(discardRes);

        // 2. Test Network.emulateNetworkConditions
        var conditions = new JsonObject
        {
            ["offline"] = true,
            ["latency"] = 150.0,
            ["downloadThroughput"] = 102400.0,
            ["uploadThroughput"] = 51200.0
        };
        var emulateRes = await NetworkDomain.HandleAsync(session, "emulateNetworkConditions", conditions);
        Assert.NotNull(emulateRes);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestLogicalTreeModePierceFalse()
    {
        var button = new Button { Content = "Test Button", Name = "myButton" };
        var panel = new StackPanel
        {
            Children = { button }
        };
        var window = new Window
        {
            Title = "Logical Tree Window",
            Content = panel
        };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // 1. Get document with pierce: false (Logical Tree Mode)
        var docParams = new JsonObject { ["depth"] = -1, ["pierce"] = false };
        var docResult = await DomDomain.HandleAsync(session, "getDocument", docParams);
        Assert.NotNull(docResult);
        Assert.True(session.UseLogicalTree);

        var root = docResult["root"] as JsonObject;
        Assert.NotNull(root);

        // Let's verify outerHTML is representing logical tree only
        var windowNode = root["children"]?[0] as JsonObject;
        Assert.NotNull(windowNode);

        // Retrieve outerHTML of the window under logical mode
        var htmlParams = new JsonObject { ["nodeId"] = session.NodeMap.GetOrAdd(window) };
        var htmlResult = await DomDomain.HandleAsync(session, "getOuterHTML", htmlParams);
        Assert.NotNull(htmlResult);
        string html = htmlResult["outerHTML"]?.GetValue<string>() ?? "";
        
        // Logical tree should have: Window, StackPanel, Button
        // BUT it must NOT contain visual template parts like ContentPresenter, Border, VisualLayerManager etc.
        Assert.Contains("<Window", html);
        Assert.Contains("<StackPanel", html);
        Assert.Contains("<Button", html);
        Assert.DoesNotContain("ContentPresenter", html);
        Assert.DoesNotContain("Border", html);
        Assert.DoesNotContain("VisualLayerManager", html);

        // 2. DOM.querySelector should work with logical path/selector
        var queryParams = new JsonObject
        {
            ["nodeId"] = 1,
            ["selector"] = "Window > StackPanel > Button"
        };
        var queryResult = await DomDomain.HandleAsync(session, "querySelector", queryParams);
        Assert.NotNull(queryResult);
        int matchedNodeId = queryResult["nodeId"]?.GetValue<int>() ?? 0;
        Assert.True(matchedNodeId > 0);
        var matchedVisual = session.NodeMap.GetVisual(matchedNodeId);
        Assert.Same(button, matchedVisual);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestLogicalTreeNotifications()
    {
        var panel = new StackPanel();
        var window = new Window
        {
            Title = "Logical Notifications Window",
            Content = panel
        };
        window.Show();

        var fakeWs = new FakeWebSocket();
        var sessionWithFake = new CdpSession(fakeWs, window);
        sessionWithFake.UseLogicalTree = true;
        sessionWithFake.StartObservingVisualTree();

        // Add a logical child
        var button = new Button { Name = "newBtn" };
        panel.Children.Add(button);

        // Wait a tiny bit for UI thread
        await Task.Delay(100);

        // Verify we received childNodeInserted
        var insertedEvent = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("DOM.childNodeInserted"));
        Assert.NotNull(insertedEvent);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestSwitchTreeModeRestartsObservers()
    {
        var panel = new StackPanel();
        var window = new Window
        {
            Title = "Switch Mode Test Window",
            Content = panel
        };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        // Start observing in Visual Tree Mode (default)
        await DomDomain.HandleAsync(session, "enable", new JsonObject());
        Assert.True(session.IsDomEnabled);
        Assert.False(session.UseLogicalTree);

        // Switch to Logical Tree Mode by calling getDocument with pierce: false
        await DomDomain.HandleAsync(session, "getDocument", new JsonObject { ["pierce"] = false });
        Assert.True(session.IsDomEnabled);
        Assert.True(session.UseLogicalTree);

        // Clear sent messages
        fakeWs.SentMessages.Clear();

        // Add a child
        var button = new Button { Name = "newBtn" };
        panel.Children.Add(button);

        // Wait a tiny bit for UI thread
        await Task.Delay(100);

        // Verify we received childNodeInserted
        var insertedEvent = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("DOM.childNodeInserted"));
        Assert.NotNull(insertedEvent);

        session.StopObservingVisualTree();
        window.Close();
    }

    [AvaloniaFact]
    public void TestFindLogicalNodeResolution()
    {
        var panel = new StackPanel();
        var button = new Button { Content = "Test" };
        panel.Children.Add(button);

        var window = new Window { Content = panel };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        window.UpdateLayout();

        var textBlock = button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
        Assert.NotNull(textBlock);

        var resolvedTb = session.FindLogicalNode(textBlock);
        Assert.Same(button, resolvedTb);

        window.Close();
    }

    private class NonVisualLogicalNode : StyledElement
    {
        public void AddLogicalChild(Avalonia.LogicalTree.ILogical child)
        {
            LogicalChildren.Add(child);
        }

        public void RemoveLogicalChild(Avalonia.LogicalTree.ILogical child)
        {
            LogicalChildren.Remove(child);
        }
    }

    [AvaloniaFact]
    public void TestLogicalVisualChildrenWithNonVisualLogicalNode()
    {
        var host = new NonVisualLogicalNode();
        var button = new Button { Name = "btnUnderHost" };
        host.AddLogicalChild(button);

        var results = System.Linq.Enumerable.ToList(CdpSession.GetLogicalVisualChildren(host));
        Assert.Single(results);
        Assert.Same(button, results[0]);
    }

    private class LogicalStackPanel : StackPanel
    {
        public void AddLogicalChild(Avalonia.LogicalTree.ILogical child)
        {
            LogicalChildren.Add(child);
        }

        public void RemoveLogicalChild(Avalonia.LogicalTree.ILogical child)
        {
            LogicalChildren.Remove(child);
        }
    }

    [AvaloniaFact]
    public async Task TestLogicalTreeNotificationsWithNonVisualLogicalNode()
    {
        var panel = new LogicalStackPanel();
        var host = new NonVisualLogicalNode();
        panel.AddLogicalChild(host);

        var window = new Window
        {
            Title = "Logical Notifications NonVisual Window",
            Content = panel
        };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);
        session.UseLogicalTree = true;
        session.StartObservingVisualTree();

        // Dynamically add a button inside the non-visual logical host
        var button = new Button { Name = "btnUnderHost" };
        host.AddLogicalChild(button);

        // Wait a tiny bit for UI thread
        await Task.Delay(100);

        // Verify we received childNodeInserted
        var insertedEvent = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("DOM.childNodeInserted"));
        Assert.NotNull(insertedEvent);
        Assert.Contains("btnUnderHost", insertedEvent);

        // Clear sent messages
        fakeWs.SentMessages.Clear();

        // Dynamically remove the button
        host.RemoveLogicalChild(button);

        // Wait a tiny bit for UI thread
        await Task.Delay(100);

        // Verify we received childNodeRemoved
        var removedEvent = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("DOM.childNodeRemoved"));
        Assert.NotNull(removedEvent);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestNetworkThrottlingSimulationOffline()
    {
        var window = new Window { Title = "Network Throttling Test Window" };
        window.Show();

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        // 0. Enable Network domain
        await NetworkDomain.HandleAsync(session, "enable", new JsonObject());

        // 1. Enable Network emulation conditions with offline = true
        var conditions = new JsonObject
        {
            ["offline"] = true,
            ["latency"] = 0.0,
            ["downloadThroughput"] = 0.0,
            ["uploadThroughput"] = 0.0
        };
        await NetworkDomain.HandleAsync(session, "emulateNetworkConditions", conditions);

        // 2. Try starting an HTTP request and verify it throws HttpRequestException
        using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://example.com");
        Assert.Throws<System.Net.Http.HttpRequestException>(() =>
        {
            NetworkDomain.OnRequestStart(request);
        });

        // 3. Clear emulation conditions (offline = false)
        var resetConditions = new JsonObject
        {
            ["offline"] = false,
            ["latency"] = 0.0,
            ["downloadThroughput"] = -1.0,
            ["uploadThroughput"] = -1.0
        };
        await NetworkDomain.HandleAsync(session, "emulateNetworkConditions", resetConditions);

        // 4. Try starting the HTTP request again and confirm it does not throw
        NetworkDomain.OnRequestStart(request);

        NetworkDomain.RemoveSession(session);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TestInspectModeAndSimulatedClick()
    {
        var window = new Window
        {
            Title = "Inspect Test Window",
            Width = 300,
            Height = 200,
            Content = new Button { Content = "Target Button", Width = 100, Height = 50 }
        };
        window.Show();

        // Wait for window to be fully ready and laid out
        for (int i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        // 1. Enable inspect mode
        var setInspectParams = new JsonObject
        {
            ["mode"] = "searchForNode",
            ["highlightConfig"] = new JsonObject()
        };
        await OverlayDomain.HandleAsync(session, "setInspectMode", setInspectParams);

        // 2. Simulate mouse press on the button
        var clickParams = new JsonObject
        {
            ["type"] = "mousePressed",
            ["x"] = 50.0,
            ["y"] = 25.0,
            ["button"] = "left",
            ["clickCount"] = 1,
            ["modifiers"] = 0
        };
        
        // Force layout
        window.Measure(new Size(300, 200));
        window.Arrange(new Rect(0, 0, 300, 200));
        Dispatcher.UIThread.RunJobs();
        
        await InputDomain.HandleAsync(session, "dispatchMouseEvent", clickParams);

        // Give dispatcher some time to process
        for (int i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        // 3. Verify that Overlay.inspectNodeRequested was sent
        string? inspectMsg = null;
        lock (fakeWs.SentMessages)
        {
            inspectMsg = fakeWs.SentMessages.FirstOrDefault(m => m.Contains("Overlay.inspectNodeRequested"));
        }

        Assert.NotNull(inspectMsg);
        var node = System.Text.Json.Nodes.JsonNode.Parse(inspectMsg);
        Assert.NotNull(node);
        Assert.Equal("Overlay.inspectNodeRequested", node["method"]?.GetValue<string>());
        
        var backendNodeId = node["params"]?["backendNodeId"]?.GetValue<int>();
        Assert.True(backendNodeId > 0);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestKeyboardInputWithoutDuplication()
    {
        var textBox = new TextBox
        {
            Width = 100,
            Height = 50,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        var window = new Window
        {
            Title = "Keyboard Test Window",
            Width = 300,
            Height = 200,
            Content = textBox
        };
        window.Show();
        window.Activate();

        for (int i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        var focused = textBox.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(focused, "TextBox should acquire focus");
        Assert.True(textBox.IsFocused, "TextBox IsFocused should be true");

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        // Simulate typing "a" using CDP standard sequence
        var keyDownParams = new JsonObject
        {
            ["type"] = "rawKeyDown",
            ["key"] = "KeyA",
            ["text"] = "a",
            ["modifiers"] = 0
        };
        await InputDomain.HandleAsync(session, "dispatchKeyEvent", keyDownParams);

        var charParams = new JsonObject
        {
            ["type"] = "char",
            ["key"] = "KeyA",
            ["text"] = "a",
            ["modifiers"] = 0
        };
        await InputDomain.HandleAsync(session, "dispatchKeyEvent", charParams);

        var keyUpParams = new JsonObject
        {
            ["type"] = "keyUp",
            ["key"] = "KeyA",
            ["text"] = "",
            ["modifiers"] = 0
        };
        await InputDomain.HandleAsync(session, "dispatchKeyEvent", keyUpParams);

        for (int i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        Assert.Equal("a", textBox.Text);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestTouchEmulationFromMouseEvent()
    {
        bool touchPressed = false;
        var border = new Border
        {
            Width = 100,
            Height = 100,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Background = Avalonia.Media.Brushes.Red
        };
        border.PointerPressed += (s, e) =>
        {
            if (e.Pointer.Type == PointerType.Touch)
            {
                touchPressed = true;
            }
        };

        var window = new Window
        {
            Title = "Touch Test Window",
            Width = 300,
            Height = 200,
            Content = border
        };
        window.Show();
        window.Activate();

        // Force layout
        window.Measure(new Size(300, 200));
        window.Arrange(new Rect(0, 0, 300, 200));
        for (int i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        var touchParams = new JsonObject
        {
            ["type"] = "mousePressed",
            ["x"] = 50.0,
            ["y"] = 50.0,
            ["button"] = "left",
            ["clickCount"] = 1,
            ["modifiers"] = 0
        };
        await InputDomain.HandleAsync(session, "emulateTouchFromMouseEvent", touchParams);

        for (int i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        Assert.True(touchPressed);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestSynthesizeTapGesture()
    {
        bool tapped = false;
        var button = new Button
        {
            Width = 100,
            Height = 50,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Content = "Tap Target"
        };
        button.Click += (s, e) => tapped = true;

        var window = new Window
        {
            Title = "Tap Test Window",
            Width = 300,
            Height = 200,
            Content = button
        };
        window.Show();
        window.Activate();

        // Force layout
        window.Measure(new Size(300, 200));
        window.Arrange(new Rect(0, 0, 300, 200));
        for (int i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        var tapParams = new JsonObject
        {
            ["x"] = 50.0,
            ["y"] = 25.0,
            ["tapCount"] = 1,
            ["duration"] = 20,
            ["gestureSourceType"] = "touch"
        };
        await InputDomain.HandleAsync(session, "synthesizeTapGesture", tapParams);

        for (int i = 0; i < 15; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        Assert.True(tapped);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestSynthesizeScrollGesture()
    {
        double scrollDeltaY = 0;
        var scrollViewer = new ScrollViewer
        {
            Width = 200,
            Height = 200,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Content = new Canvas { Width = 1000, Height = 1000 }
        };
        scrollViewer.ScrollChanged += (s, e) =>
        {
            scrollDeltaY = scrollViewer.Offset.Y;
        };

        var window = new Window
        {
            Title = "Scroll Test Window",
            Width = 300,
            Height = 300,
            Content = scrollViewer
        };
        window.Show();
        window.Activate();

        // Force layout
        window.Measure(new Size(300, 300));
        window.Arrange(new Rect(0, 0, 300, 300));
        for (int i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        // Test with Mouse Wheel
        var scrollParamsMouse = new JsonObject
        {
            ["x"] = 100.0,
            ["y"] = 100.0,
            ["xDistance"] = 0.0,
            ["yDistance"] = -50.0,
            ["speed"] = 800,
            ["gestureSourceType"] = "mouse"
        };
        await InputDomain.HandleAsync(session, "synthesizeScrollGesture", scrollParamsMouse);

        for (int i = 0; i < 15; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        Assert.True(scrollDeltaY > 0, $"Expected positive Y offset, got Y={scrollDeltaY}");

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestSynthesizePinchGesture()
    {
        var border = new Border
        {
            Width = 200,
            Height = 200,
            Background = Avalonia.Media.Brushes.Red
        };
        int touchCount = 0;
        border.PointerPressed += (s, e) =>
        {
            if (e.Pointer.Type == PointerType.Touch)
            {
                touchCount++;
            }
        };

        var window = new Window
        {
            Title = "Pinch Test Window",
            Width = 300,
            Height = 300,
            Content = border
        };
        window.Show();
        window.Activate();

        window.Measure(new Size(300, 300));
        window.Arrange(new Rect(0, 0, 300, 300));
        for (int i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        using var fakeWs = new FakeWebSocket();
        var session = new CdpSession(fakeWs, window);

        var pinchParams = new JsonObject
        {
            ["x"] = 100.0,
            ["y"] = 100.0,
            ["scaleFactor"] = 2.0,
            ["relativeSpeed"] = 800,
            ["gestureSourceType"] = "touch"
        };
        await InputDomain.HandleAsync(session, "synthesizePinchGesture", pinchParams);

        for (int i = 0; i < 15; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        Assert.True(touchCount >= 2, $"Expected at least 2 touch presses (for pinch gesture two points), got {touchCount}");

        window.Close();
    }
}

public class FakeWebSocket : System.Net.WebSockets.WebSocket
{
    public System.Collections.Generic.List<string> SentMessages { get; } = new();
    private System.Net.WebSockets.WebSocketState _state = System.Net.WebSockets.WebSocketState.Open;

    public override System.Net.WebSockets.WebSocketState State => _state;
    public override string? SubProtocol => null;
    public override System.Net.WebSockets.WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;

    public override System.Threading.Tasks.Task SendAsync(System.ArraySegment<byte> buffer, System.Net.WebSockets.WebSocketMessageType messageType, bool endOfMessage, System.Threading.CancellationToken cancellationToken)
    {
        var msg = System.Text.Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
        SentMessages.Add(msg);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public override System.Threading.Tasks.Task<System.Net.WebSockets.WebSocketReceiveResult> ReceiveAsync(System.ArraySegment<byte> buffer, System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.FromResult(new System.Net.WebSockets.WebSocketReceiveResult(0, System.Net.WebSockets.WebSocketMessageType.Close, true));
    }

    public override System.Threading.Tasks.Task CloseAsync(System.Net.WebSockets.WebSocketCloseStatus closeStatus, string? statusDescription, System.Threading.CancellationToken cancellationToken)
    {
        _state = System.Net.WebSockets.WebSocketState.Closed;
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public override System.Threading.Tasks.Task CloseOutputAsync(System.Net.WebSockets.WebSocketCloseStatus closeStatus, string? statusDescription, System.Threading.CancellationToken cancellationToken)
    {
        _state = System.Net.WebSockets.WebSocketState.Closed;
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public override void Abort()
    {
        _state = System.Net.WebSockets.WebSocketState.Closed;
    }

    public override void Dispose()
    {
    }
}
