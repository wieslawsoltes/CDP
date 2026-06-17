using System;
using System.Text.Json.Nodes;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Diagnostics.Cdp.Domains;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class DomTreeTests
{
    [AvaloniaFact]
    public void TestDomTreeStructure()
    {
        var window = new Window { Title = "Test Window" };
        window.Show(); // Apply template and layout

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var button = new Button { Name = "testButton" };
        button.Classes.Add("primary");
        
        var stack = new StackPanel();
        stack.Children.Add(button);
        window.Content = stack;

        // Verify NodeMap tracking
        int btnId = session.NodeMap.GetOrAdd(button);
        Assert.True(btnId > 1);
        Assert.Same(button, session.NodeMap.GetVisual(btnId));

        // Build DOM Node JSON
        var nodeJson = DomDomain.BuildDomNode(window, session, 1, -1);
        
        Assert.NotNull(nodeJson);
        Assert.Equal("Window", nodeJson["nodeName"]?.GetValue<string>());
        
        // Find button in visual tree
        var match = SelectorEngine.QuerySelector(window, "Button");
        Assert.Same(button, match);
        
        window.Close();
    }

    [AvaloniaFact]
    public async Task TestGetBoxModelLayoutInfo()
    {
        var window = new Window { Title = "Test Window" };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var button = new Button
        {
            Name = "testButton",
            Margin = new Thickness(10, 20, 30, 40),
            Padding = new Thickness(5, 10, 15, 20),
            BorderThickness = new Thickness(2, 4, 6, 8)
        };
        var stack = new StackPanel();
        stack.Children.Add(button);
        window.Content = stack;

        int btnId = session.NodeMap.GetOrAdd(button);

        // Invoke getBoxModel handler
        var paramsObj = new JsonObject { ["nodeId"] = btnId };
        var result = await DomDomain.HandleAsync(session, "getBoxModel", paramsObj);
        Assert.NotNull(result);

        var model = result["model"] as JsonObject;
        Assert.NotNull(model);

        var marginQuad = model["margin"] as JsonArray;
        var borderQuad = model["border"] as JsonArray;
        var paddingQuad = model["padding"] as JsonArray;
        var contentQuad = model["content"] as JsonArray;

        Assert.NotNull(marginQuad);
        Assert.NotNull(borderQuad);
        Assert.NotNull(paddingQuad);
        Assert.NotNull(contentQuad);

        // Verify bounds dimensions (borderQuad width and height)
        double borderW = borderQuad![2]!.GetValue<double>() - borderQuad[0]!.GetValue<double>();
        double borderH = borderQuad[5]!.GetValue<double>() - borderQuad[1]!.GetValue<double>();
        
        Assert.Equal(button.Bounds.Width, borderW, 1);
        Assert.Equal(button.Bounds.Height, borderH, 1);

        // Verify margin boundaries are offset from border
        Assert.Equal(borderQuad[0]!.GetValue<double>() - 10, marginQuad![0]!.GetValue<double>(), 1);
        Assert.Equal(borderQuad[1]!.GetValue<double>() - 20, marginQuad[1]!.GetValue<double>(), 1);

        // Verify padding boundaries are offset from border by borderThickness
        Assert.Equal(borderQuad[0]!.GetValue<double>() + 2, paddingQuad![0]!.GetValue<double>(), 1);

        // Verify content boundaries are offset from padding by padding
        Assert.Equal(paddingQuad[0]!.GetValue<double>() + 5, contentQuad![0]!.GetValue<double>(), 1);

        window.Close();
    }
}
