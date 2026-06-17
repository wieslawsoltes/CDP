using System;
using System.Text.Json.Nodes;
using System.Net.WebSockets;
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
}
