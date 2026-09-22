using System.Net.WebSockets;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp.Domains;
using Avalonia.Headless.XUnit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class MenuRuntimeTests
{
    private static Window CreateMenuWindow(out MenuItem[] items)
    {
        var menu = new Menu { Name = "MainMenu" };
        items = new[]
        {
            new MenuItem { Name = "FileMenu", Header = "_File" },
            new MenuItem { Name = "EditMenu", Header = "_Edit" },
            new MenuItem { Name = "ViewMenu", Header = "_View" }
        };
        foreach (var item in items)
        {
            menu.Items.Add(item);
        }

        var window = new Window
        {
            Width = 640,
            Height = 480,
            Content = new DockPanel { Children = { menu } }
        };
        DockPanel.SetDock(menu, Dock.Top);
        window.Show();
        return window;
    }

    private static async Task<JsonNode?> EvaluateByValueAsync(CdpSession session, string expression)
    {
        var response = await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
        {
            ["expression"] = expression,
            ["returnByValue"] = true
        });

        var result = response["result"] as JsonObject;
        Assert.NotNull(result);
        if (result!.TryGetPropertyValue("subtype", out var subtype) && subtype?.GetValue<string>() == "error")
        {
            Assert.Fail($"'{expression}' failed: {result["description"]?.GetValue<string>()}");
        }

        return result["value"];
    }

    [AvaloniaFact]
    public void MenuItemHeaderMatchesTextAndHeaderAttributeSelectors()
    {
        var window = CreateMenuWindow(out var items);
        try
        {
            var fileItem = items[0];

            Assert.True(SelectorEngine.Matches(fileItem, "[text=\"_File\"]"));
            Assert.True(SelectorEngine.Matches(fileItem, "[Text=\"_File\"]"));
            Assert.True(SelectorEngine.Matches(fileItem, "[Header=\"_File\"]"));
            Assert.True(SelectorEngine.Matches(fileItem, "MenuItem:contains(\"File\")"));
            Assert.False(SelectorEngine.Matches(fileItem, "[text=\"_View\"]"));

            Assert.Same(fileItem, SelectorEngine.QuerySelector(window, "[text=\"_File\"]"));
            Assert.Same(items[2], SelectorEngine.QuerySelector(window, "MenuItem[Header=\"_View\"]"));
            Assert.Equal(3, SelectorEngine.QuerySelectorAll(window, "MenuItem:contains(\"_\")").Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task RuntimeEvaluateReadsMenuHeadersThroughSelectorsCollectionsAndArrays()
    {
        var window = CreateMenuWindow(out _);
        using var clientWs = new ClientWebSocket();
        using var session = new CdpSession(clientWs, window);
        try
        {
            Assert.Equal("_File", (await EvaluateByValueAsync(session, "document.querySelector(\"MenuItem\").Header"))?.GetValue<string>());
            Assert.Equal("_Edit", (await EvaluateByValueAsync(session, "document.querySelector('[text=\"_Edit\"]').Header"))?.GetValue<string>());
            Assert.Equal("_View", (await EvaluateByValueAsync(session, "document.querySelector('[Header=\"_View\"]').Header"))?.GetValue<string>());

            Assert.Equal(3, (await EvaluateByValueAsync(session, "document.querySelector(\"Menu\").Items.Count"))?.GetValue<int>());
            Assert.Equal("_View", (await EvaluateByValueAsync(session, "document.querySelector(\"Menu\").Items[2].Header"))?.GetValue<string>());
            Assert.Equal("_Edit", (await EvaluateByValueAsync(session, "document.querySelector(\"Menu\").Items[1].Header"))?.GetValue<string>());

            Assert.Equal(3, (await EvaluateByValueAsync(session, "document.querySelectorAll(\"MenuItem\").length"))?.GetValue<int>());
            Assert.Equal("_File,_Edit,_View",
                (await EvaluateByValueAsync(session, "Array.from(document.querySelectorAll(\"MenuItem\")).map(function (item) { return item.Header; }).join(\",\")"))?.GetValue<string>());
            Assert.Equal("FileMenu,EditMenu,ViewMenu",
                (await EvaluateByValueAsync(session, "Array.from(document.querySelectorAll(\"MenuItem\")).map(function (item) { return item.id; }).join(\",\")"))?.GetValue<string>());
        }
        finally
        {
            window.Close();
        }
    }
}
