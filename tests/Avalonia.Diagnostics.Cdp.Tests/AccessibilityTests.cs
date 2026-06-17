using System;
using System.Text.Json.Nodes;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Diagnostics.Cdp.Domains;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class AccessibilityTests
{
    [AvaloniaFact]
    public async Task TestGetFullAXTree()
    {
        var window = new Window { Title = "Test Window" };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var button = new Button { Name = "testButton" };
        // Set automation properties to verify
        AutomationProperties.SetName(button, "MyAccessibleButton");
        AutomationProperties.SetHelpText(button, "This is a test button help text");

        var stack = new StackPanel();
        stack.Children.Add(button);
        window.Content = stack;

        // Call getFullAXTree handler
        var result = await AccessibilityDomain.HandleAsync(session, "getFullAXTree", new JsonObject());
        Assert.NotNull(result);

        var nodes = result["nodes"] as JsonArray;
        Assert.NotNull(nodes);
        Assert.True(nodes.Count >= 3); // Window, StackPanel, Button

        // Find the node corresponding to our button
        string buttonId = session.NodeMap.GetOrAdd(button).ToString();
        JsonObject? buttonNode = null;
        foreach (var node in nodes)
        {
            if (node?["nodeId"]?.GetValue<string>() == buttonId)
            {
                buttonNode = node as JsonObject;
                break;
            }
        }

        Assert.NotNull(buttonNode);
        Assert.False(buttonNode["ignored"]?.GetValue<bool>());

        var roleVal = buttonNode["role"] as JsonObject;
        Assert.NotNull(roleVal);
        Assert.Equal("role", roleVal["type"]?.GetValue<string>());
        Assert.Equal("Button", roleVal["value"]?.GetValue<string>());

        var nameVal = buttonNode["name"] as JsonObject;
        Assert.NotNull(nameVal);
        Assert.Equal("string", nameVal["type"]?.GetValue<string>());
        Assert.Equal("MyAccessibleButton", nameVal["value"]?.GetValue<string>());

        var descVal = buttonNode["description"] as JsonObject;
        Assert.NotNull(descVal);
        Assert.Equal("string", descVal["type"]?.GetValue<string>());
        Assert.Equal("This is a test button help text", descVal["value"]?.GetValue<string>());

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestEnableDisable()
    {
        var window = new Window { Title = "Test Window" };
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var enableResult = await AccessibilityDomain.HandleAsync(session, "enable", new JsonObject());
        Assert.NotNull(enableResult);
        Assert.Empty(enableResult);

        var disableResult = await AccessibilityDomain.HandleAsync(session, "disable", new JsonObject());
        Assert.NotNull(disableResult);
        Assert.Empty(disableResult);
    }

    [AvaloniaFact]
    public async Task TestGetAXNode()
    {
        var window = new Window { Title = "Test Window" };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var button = new Button { Name = "testButton" };
        AutomationProperties.SetName(button, "MyAccessibleButton");

        var stack = new StackPanel();
        stack.Children.Add(button);
        window.Content = stack;

        // Populate NodeMap
        int buttonNodeId = session.NodeMap.GetOrAdd(button);

        // Call getAXNode handler
        var paramsObj = new JsonObject { ["nodeId"] = buttonNodeId };
        var result = await AccessibilityDomain.HandleAsync(session, "getAXNode", paramsObj);
        Assert.NotNull(result);

        var nodes = result["nodes"] as JsonArray;
        Assert.NotNull(nodes);
        Assert.NotEmpty(nodes);

        // Target AXNode for the button
        var buttonNode = nodes[0] as JsonObject;
        Assert.NotNull(buttonNode);
        Assert.Equal(buttonNodeId.ToString(), buttonNode["nodeId"]?.GetValue<string>());
        Assert.Equal(buttonNodeId, buttonNode["backendDOMNodeId"]?.GetValue<int>());

        var nameVal = buttonNode["name"] as JsonObject;
        Assert.NotNull(nameVal);
        Assert.Equal("MyAccessibleButton", nameVal["value"]?.GetValue<string>());

        window.Close();
    }
}
