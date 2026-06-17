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
using Avalonia.VisualTree;

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
}
