using System;
using System.Text.Json.Nodes;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Diagnostics.Cdp.Domains;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class LogAndSearchTests
{
    [AvaloniaFact]
    public async Task TestDomPerformSearchAndGetResults()
    {
        var window = new Window { Title = "Search Test Window" };
        var button = new Button { Name = "mySearchBtn", Content = "Test Button" };
        window.Content = button;
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        // Perform search by name
        var searchParams = new JsonObject { ["query"] = "mySearchBtn" };
        var searchResult = await DomDomain.HandleAsync(session, "performSearch", searchParams);
        Assert.NotNull(searchResult);
        
        string searchId = searchResult["searchId"]?.GetValue<string>() ?? "";
        int resultCount = searchResult["resultCount"]?.GetValue<int>() ?? 0;
        
        Assert.NotEmpty(searchId);
        Assert.Equal(1, resultCount);

        // Get search results
        var getResultsParams = new JsonObject
        {
            ["searchId"] = searchId,
            ["fromIndex"] = 0,
            ["toIndex"] = 1
        };
        var getResultsResult = await DomDomain.HandleAsync(session, "getSearchResults", getResultsParams);
        var nodeIds = getResultsResult["nodeIds"] as JsonArray;
        Assert.NotNull(nodeIds);
        Assert.Single(nodeIds);

        int matchedNodeId = nodeIds[0]!.GetValue<int>();
        int buttonNodeId = session.NodeMap.GetOrAdd(button);
        Assert.Equal(buttonNodeId, matchedNodeId);

        // Discard search results
        var discardParams = new JsonObject { ["searchId"] = searchId };
        var discardResult = await DomDomain.HandleAsync(session, "discardSearchResults", discardParams);
        Assert.NotNull(discardResult);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TestLogDomainEnableDisable()
    {
        var window = new Window { Title = "Log Test Window" };
        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var enableResult = await LogDomain.HandleAsync(session, "enable", new JsonObject());
        Assert.NotNull(enableResult);

        var clearResult = await LogDomain.HandleAsync(session, "clear", new JsonObject());
        Assert.NotNull(clearResult);

        var disableResult = await LogDomain.HandleAsync(session, "disable", new JsonObject());
        Assert.NotNull(disableResult);
    }
}
