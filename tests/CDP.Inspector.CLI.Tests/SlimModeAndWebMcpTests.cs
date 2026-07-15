using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using Avalonia.Diagnostics.Cdp.Domains;
using WebMcpDomain = Avalonia.Diagnostics.Cdp.Domains.WebMcpDomain;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class SlimModeAndWebMcpTests
{
    [AvaloniaFact]
    public async Task TestSlimTreePruning()
    {
        var window = new Window { Title = "Slim Mode Test Window" };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // Build a UI layout:
        // Window
        //  └── StackPanel (non-interactive parent)
        //       ├── Button (interactive -> keep)
        //       ├── Border (non-interactive, but contains TextBlock -> keep)
        //       │    └── TextBlock (text container -> keep)
        //       └── Grid (non-interactive, empty -> prune)
        var button = new Button { Name = "testButton", Focusable = true };
        
        var textBlock = new TextBlock { Name = "testTextBlock", Text = "Hello" };
        var border = new Border { Name = "testBorder", Child = textBlock };
        
        var grid = new Grid { Name = "testGrid" }; // completely non-interactive leaf/container with no slim targets
        
        var stack = new StackPanel { Name = "testStack" };
        stack.Children.Add(button);
        stack.Children.Add(border);
        stack.Children.Add(grid);
        
        window.Content = stack;

        // Enable DOM domain with slim parameter = true
        var enableParams = new JsonObject { ["slim"] = true };
        await DomDomain.HandleAsync(session, "enable", enableParams);
        Assert.True(session.UseSlimTree);

        // Retrieve document
        var getDocParams = new JsonObject { ["depth"] = -1 };
        var getDocResult = await DomDomain.HandleAsync(session, "getDocument", getDocParams);
        var rootNode = getDocResult["root"] as JsonObject;
        Assert.NotNull(rootNode);

        // Walk tree to verify button and textBlock are present, and grid is pruned
        bool foundButton = false;
        bool foundTextBlock = false;
        bool foundGrid = false;

        void FindNodes(JsonObject node)
        {
            var nodeName = node["nodeName"]?.GetValue<string>();
            var attributes = node["attributes"] as JsonArray;
            string? idVal = null;
            if (attributes != null)
            {
                for (int i = 0; i < attributes.Count; i += 2)
                {
                    if (attributes[i]?.GetValue<string>() == "id")
                    {
                        idVal = attributes[i + 1]?.GetValue<string>();
                        break;
                    }
                }
            }

            if (idVal == "testButton") foundButton = true;
            if (idVal == "testTextBlock") foundTextBlock = true;
            if (idVal == "testGrid") foundGrid = true;

            var children = node["children"] as JsonArray;
            if (children != null)
            {
                foreach (var child in children)
                {
                    if (child is JsonObject childObj)
                    {
                        FindNodes(childObj);
                    }
                }
            }
        }

        FindNodes(rootNode);

        Assert.True(foundButton, "Button should be kept in slim tree.");
        Assert.True(foundTextBlock, "TextBlock should be kept in slim tree.");
        Assert.False(foundGrid, "Empty Grid should be pruned in slim tree.");

        // Verify with getFlattenedDocument
        var getFlatParams = new JsonObject { ["depth"] = -1 };
        var getFlatResult = await DomDomain.HandleAsync(session, "getFlattenedDocument", getFlatParams);
        var nodes = getFlatResult["nodes"] as JsonArray;
        Assert.NotNull(nodes);

        bool flatFoundButton = false;
        bool flatFoundTextBlock = false;
        bool flatFoundGrid = false;

        foreach (var nodeVal in nodes)
        {
            if (nodeVal is JsonObject nodeObj)
            {
                var attrs = nodeObj["attributes"] as JsonArray;
                string? idVal = null;
                if (attrs != null)
                {
                    for (int i = 0; i < attrs.Count; i += 2)
                    {
                        if (attrs[i]?.GetValue<string>() == "id")
                        {
                            idVal = attrs[i + 1]?.GetValue<string>();
                            break;
                        }
                    }
                }

                if (idVal == "testButton") flatFoundButton = true;
                if (idVal == "testTextBlock") flatFoundTextBlock = true;
                if (idVal == "testGrid") flatFoundGrid = true;
            }
        }

        Assert.True(flatFoundButton, "Button should be kept in flattened slim tree.");
        Assert.True(flatFoundTextBlock, "TextBlock should be kept in flattened slim tree.");
        Assert.False(flatFoundGrid, "Empty Grid should be pruned in flattened slim tree.");

        // Cleanup
        var disableParams = new JsonObject();
        await DomDomain.HandleAsync(session, "disable", disableParams);
        Assert.False(session.UseSlimTree);

        window.Close();
    }

    private class MultiplyTool : IMcpTool
    {
        public string Name => "multiply";
        public string Description => "Multiplies two numbers";
        public JsonObject? InputSchema => new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["a"] = new JsonObject { ["type"] = "number" },
                ["b"] = new JsonObject { ["type"] = "number" }
            }
        };

        public Task<JsonNode?> InvokeAsync(JsonObject input)
        {
            double a = 0;
            double b = 0;
            if (input["a"] is JsonValue valA)
            {
                if (valA.TryGetValue<double>(out double dA)) a = dA;
                else if (valA.TryGetValue<int>(out int iA)) a = iA;
                else if (valA.TryGetValue<long>(out long lA)) a = lA;
            }
            if (input["b"] is JsonValue valB)
            {
                if (valB.TryGetValue<double>(out double dB)) b = dB;
                else if (valB.TryGetValue<int>(out int iB)) b = iB;
                else if (valB.TryGetValue<long>(out long lB)) b = lB;
            }
            JsonNode? result = JsonValue.Create(a * b);
            return Task.FromResult(result);
        }
    }

    [AvaloniaFact]
    public async Task TestWebMcpDomain()
    {
        var window = new Window { Title = "WebMCP Test Window" };
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var tool = new MultiplyTool();
        McpToolRegistry.RegisterTool(tool);

        var events = new List<JsonObject>();
        session.EventSentForTesting += (node) =>
        {
            lock (events)
            {
                events.Add(node);
            }
        };

        // Enable WebMCP domain
        var enableResult = await WebMcpDomain.HandleAsync(session, "enable", new JsonObject());
        Assert.NotNull(enableResult);

        // Should receive WebMCP.toolsAdded event
        await Task.Delay(100);
        JsonObject? toolsAddedEvt = null;
        lock (events)
        {
            toolsAddedEvt = events.Find(e => e["method"]?.GetValue<string>() == "WebMCP.toolsAdded");
        }

        Assert.NotNull(toolsAddedEvt);
        var toolsAddedParams = toolsAddedEvt!["params"] as JsonObject;
        Assert.NotNull(toolsAddedParams);
        var toolsList = toolsAddedParams!["tools"] as JsonArray;
        Assert.NotNull(toolsList);
        
        bool foundMultiply = false;
        foreach (var tNode in toolsList!)
        {
            if (tNode?["name"]?.GetValue<string>() == "multiply")
            {
                foundMultiply = true;
                Assert.Equal("Multiplies two numbers", tNode["description"]?.GetValue<string>());
                Assert.NotNull(tNode["inputSchema"]);
            }
        }
        Assert.True(foundMultiply);

        // Invoke tool
        var invokeParams = new JsonObject
        {
            ["toolName"] = "multiply",
            ["input"] = new JsonObject { ["a"] = 6, ["b"] = 7 }
        };
        var invokeResult = await WebMcpDomain.HandleAsync(session, "invokeTool", invokeParams);
        Assert.NotNull(invokeResult);
        var invocationId = invokeResult["invocationId"]?.GetValue<string>();
        Assert.NotNull(invocationId);

        // Should receive WebMCP.toolInvoked and WebMCP.toolResponded events
        JsonObject? toolInvokedEvt = null;
        JsonObject? toolRespondedEvt = null;

        for (int i = 0; i < 20; i++)
        {
            lock (events)
            {
                toolInvokedEvt ??= events.Find(e => e["method"]?.GetValue<string>() == "WebMCP.toolInvoked" && e["params"]?["invocationId"]?.GetValue<string>() == invocationId);
                toolRespondedEvt ??= events.Find(e => e["method"]?.GetValue<string>() == "WebMCP.toolResponded" && e["params"]?["invocationId"]?.GetValue<string>() == invocationId);
            }
            if (toolInvokedEvt != null && toolRespondedEvt != null) break;
            await Task.Delay(50);
        }

        Assert.NotNull(toolInvokedEvt);
        Assert.NotNull(toolRespondedEvt);

        var invokedParams = toolInvokedEvt!["params"] as JsonObject;
        Assert.Equal("multiply", invokedParams?["toolName"]?.GetValue<string>());

        var respondedParams = toolRespondedEvt!["params"] as JsonObject;
        string? status = respondedParams?["status"]?.GetValue<string>();
        string? errorText = respondedParams?["errorText"]?.GetValue<string>();
        Assert.Null(errorText);
        Assert.Equal("Completed", status);
        Assert.Equal(42.0, respondedParams?["output"]?.GetValue<double>());

        // Disable WebMCP
        await WebMcpDomain.HandleAsync(session, "disable", new JsonObject());

        window.Close();
    }
}
