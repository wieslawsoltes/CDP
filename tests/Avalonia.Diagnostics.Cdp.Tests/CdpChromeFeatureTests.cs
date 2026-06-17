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
}
