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
        Assert.Equal("button", roleVal["value"]?.GetValue<string>());

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

    [AvaloniaFact]
    public async Task TestGetPartialAXTreeWithRelatives()
    {
        var window = new Window { Title = "Partial Test Window" };
        var button1 = new Button { Name = "btn1" };
        var button2 = new Button { Name = "btn2" };
        var panel = new StackPanel { Children = { button1, button2 } };
        window.Content = panel;
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        int button1Id = session.NodeMap.GetOrAdd(button1);

        // Fetch partial AXTree with fetchRelatives: true
        var paramsObj = new JsonObject
        {
            ["nodeId"] = button1Id,
            ["fetchRelatives"] = true
        };
        var result = await AccessibilityDomain.HandleAsync(session, "getPartialAXTree", paramsObj);
        Assert.NotNull(result);

        var nodes = result["nodes"] as JsonArray;
        Assert.NotNull(nodes);

        // It should contain: button1 itself, button2 (sibling), panel (parent), window (grandparent)
        var nodeIds = nodes.Select(n => n?["nodeId"]?.GetValue<string>()).ToList();
        Assert.Contains(button1Id.ToString(), nodeIds);
        Assert.Contains(session.NodeMap.GetOrAdd(button2).ToString(), nodeIds);
        Assert.Contains(session.NodeMap.GetOrAdd(panel).ToString(), nodeIds);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestAXPropertiesCheckBoxSliderTextBox()
    {
        var window = new Window { Title = "Properties Test Window" };
        var checkBox = new CheckBox { IsChecked = true };
        var slider = new Slider { Minimum = 10, Maximum = 100, Value = 42 };
        var textBox = new TextBox { Text = "Hello automation!" };
        
        // Add extra automation properties to check
        AutomationProperties.SetIsRequiredForForm(textBox, true);
        AutomationProperties.SetPositionInSet(slider, 2);
        AutomationProperties.SetSizeOfSet(slider, 5);

        var panel = new StackPanel { Children = { checkBox, slider, textBox } };
        window.Content = panel;
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var result = await AccessibilityDomain.HandleAsync(session, "getFullAXTree", new JsonObject());
        Assert.NotNull(result);

        var nodes = result["nodes"] as JsonArray;
        Assert.NotNull(nodes);

        // Check Box
        string checkBoxId = session.NodeMap.GetOrAdd(checkBox).ToString();
        var checkBoxNode = nodes.FirstOrDefault(n => n?["nodeId"]?.GetValue<string>() == checkBoxId) as JsonObject;
        Assert.NotNull(checkBoxNode);
        var cbProps = checkBoxNode["properties"] as JsonArray;
        Assert.NotNull(cbProps);
        var checkedProp = cbProps.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "checked");
        Assert.NotNull(checkedProp);
        Assert.Equal("true", checkedProp["value"]?["value"]?.GetValue<string>());

        // Slider
        string sliderId = session.NodeMap.GetOrAdd(slider).ToString();
        var sliderNode = nodes.FirstOrDefault(n => n?["nodeId"]?.GetValue<string>() == sliderId) as JsonObject;
        Assert.NotNull(sliderNode);
        var sliderProps = sliderNode["properties"] as JsonArray;
        Assert.NotNull(sliderProps);
        
        var minProp = sliderProps.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "valuemin");
        Assert.NotNull(minProp);
        Assert.Equal(10.0, minProp["value"]?["value"]?.GetValue<double>());

        var maxProp = sliderProps.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "valuemax");
        Assert.NotNull(maxProp);
        Assert.Equal(100.0, maxProp["value"]?["value"]?.GetValue<double>());

        var valProp = sliderProps.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "value");
        Assert.NotNull(valProp);
        Assert.Equal(42.0, valProp["value"]?["value"]?.GetValue<double>());

        var posProp = sliderProps.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "posinset");
        Assert.NotNull(posProp);
        Assert.Equal(2, posProp["value"]?["value"]?.GetValue<int>());

        var sizeProp = sliderProps.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "setsize");
        Assert.NotNull(sizeProp);
        Assert.Equal(5, sizeProp["value"]?["value"]?.GetValue<int>());

        // TextBox
        string textBoxId = session.NodeMap.GetOrAdd(textBox).ToString();
        var textBoxNode = nodes.FirstOrDefault(n => n?["nodeId"]?.GetValue<string>() == textBoxId) as JsonObject;
        Assert.NotNull(textBoxNode);
        var tbProps = textBoxNode["properties"] as JsonArray;
        Assert.NotNull(tbProps);
        var reqProp = tbProps.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "required");
        Assert.NotNull(reqProp);
        Assert.True(reqProp["value"]?["value"]?.GetValue<bool>());

        window.Close();
    }
}
