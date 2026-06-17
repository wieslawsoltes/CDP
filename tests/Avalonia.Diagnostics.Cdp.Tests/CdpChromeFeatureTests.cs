using System;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp.Domains;
using Avalonia.Headless.XUnit;
using Xunit;

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
        CdpServer.Start(9223);
        try
        {
            Assert.Equal(9223, CdpServer.Port);

            var window = new Window { Title = "Port Test Window" };
            window.Show();

            using var clientWs = new ClientWebSocket();
            var session = new CdpSession(clientWs, window);

            var getDocParams = new JsonObject { ["depth"] = -1 };
            var docResult = await DomDomain.HandleAsync(session, "getDocument", getDocParams);
            Assert.NotNull(docResult);

            var root = docResult["root"] as JsonObject;
            Assert.NotNull(root);

            Assert.Equal("http://localhost:9223/", root["documentURL"]?.GetValue<string>());
            Assert.Equal("http://localhost:9223/", root["baseURL"]?.GetValue<string>());

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
              ""type"": ""click"",
              ""selectors"": [[""#btnClickMe""]],
              ""offsetX"": 10,
              ""offsetY"": 20
            },
            {
              ""type"": ""change"",
              ""selectors"": [[""#txtInput""]],
              ""value"": ""Hello World""
            }
          ]
        }";

        var jsonSteps = RecordingParser.Parse(jsonContent);
        Assert.Equal(2, jsonSteps.Count);
        Assert.Equal("click", jsonSteps[0].Type);
        Assert.Equal("#btnClickMe", jsonSteps[0].Selector);
        Assert.Equal(10, jsonSteps[0].OffsetX);
        Assert.Equal(20, jsonSteps[0].OffsetY);

        Assert.Equal("change", jsonSteps[1].Type);
        Assert.Equal("#txtInput", jsonSteps[1].Selector);
        Assert.Equal("Hello World", jsonSteps[1].Value);

        // 2. Puppeteer JS parse test
        string jsContent = @"
        const puppeteer = require('puppeteer');
        (async () => {
          const browser = await puppeteer.launch({ headless: false });
          const page = await browser.newPage();
          await page.goto('http://localhost:9222/');
          const element_0 = await page.waitForSelector('#btnClickMe');
          await element_0.click();
          const element_1 = await page.waitForSelector('#txtInput');
          await element_1.type('Avalonia CDP Automation!');
          await browser.close();
        })();";

        var jsSteps = RecordingParser.Parse(jsContent);
        Assert.Equal(2, jsSteps.Count);
        Assert.Equal("click", jsSteps[0].Type);
        Assert.Equal("#btnClickMe", jsSteps[0].Selector);

        Assert.Equal("change", jsSteps[1].Type);
        Assert.Equal("#txtInput", jsSteps[1].Selector);
        Assert.Equal("Avalonia CDP Automation!", jsSteps[1].Value);
    }
}
