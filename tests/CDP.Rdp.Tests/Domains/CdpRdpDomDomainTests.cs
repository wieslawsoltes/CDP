namespace CDP.Rdp.Tests.Domains;

using System;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using Avalonia.Diagnostics.Cdp.Domains;
using Avalonia.Diagnostics.Cdp.Rdp;
using Avalonia.Headless.XUnit;
using CdpRdpApp;
using Xunit;

[Xunit.Collection("RdpTests")]
public class CdpRdpDomDomainTests
{
    [AvaloniaFact]
    public async Task GetDocument_And_QuerySelector_ForRdpControl_Succeeds()
    {
        var window = new MainWindow();
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var getDocParams = new JsonObject { ["depth"] = -1, ["pierce"] = true };
        var docResult = await DomDomain.HandleAsync(session, "getDocument", getDocParams);
        Assert.NotNull(docResult);
        Assert.NotNull(docResult["root"]);

        int rootNodeId = docResult["root"]!["nodeId"]!.GetValue<int>();

        var queryRdpParams = new JsonObject
        {
            ["nodeId"] = rootNodeId,
            ["selector"] = "#rdpPreviewControl"
        };
        var rdpQueryResult = await DomDomain.HandleAsync(session, "querySelector", queryRdpParams);
        Assert.NotNull(rdpQueryResult);
        int rdpNodeId = rdpQueryResult["nodeId"]!.GetValue<int>();
        Assert.True(rdpNodeId > 0);

        var rdpControl = window.FindControl<RdpControl>("rdpPreviewControl");
        Assert.NotNull(rdpControl);

        window.Close();
    }

    [AvaloniaFact]
    public async Task QuerySelector_ForHostAndPortControls_Succeeds()
    {
        var window = new MainWindow();
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var docResult = await DomDomain.HandleAsync(session, "getDocument", new JsonObject { ["depth"] = -1 });
        int rootNodeId = docResult["root"]!["nodeId"]!.GetValue<int>();

        var hostQuery = await DomDomain.HandleAsync(session, "querySelector", new JsonObject
        {
            ["nodeId"] = rootNodeId,
            ["selector"] = "#txtHost"
        });
        Assert.NotNull(hostQuery);
        Assert.True(hostQuery["nodeId"]!.GetValue<int>() > 0);

        var btnConnectQuery = await DomDomain.HandleAsync(session, "querySelector", new JsonObject
        {
            ["nodeId"] = rootNodeId,
            ["selector"] = "#btnConnect"
        });
        Assert.NotNull(btnConnectQuery);
        Assert.True(btnConnectQuery["nodeId"]!.GetValue<int>() > 0);

        window.Close();
    }

    [AvaloniaFact]
    public async Task GetBoxModel_ForRdpControl_ReturnsQuadCoordinates()
    {
        var window = new MainWindow();
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var docResult = await DomDomain.HandleAsync(session, "getDocument", new JsonObject { ["depth"] = -1 });
        int rootNodeId = docResult["root"]!["nodeId"]!.GetValue<int>();

        var queryResult = await DomDomain.HandleAsync(session, "querySelector", new JsonObject
        {
            ["nodeId"] = rootNodeId,
            ["selector"] = "#rdpPreviewControl"
        });
        int nodeId = queryResult["nodeId"]!.GetValue<int>();

        var boxModelResult = await DomDomain.HandleAsync(session, "getBoxModel", new JsonObject { ["nodeId"] = nodeId });
        Assert.NotNull(boxModelResult);
        Assert.NotNull(boxModelResult["model"]);

        var model = boxModelResult["model"]!;
        Assert.NotNull(model["content"]);
        Assert.Equal(8, model["content"]!.AsArray().Count);

        window.Close();
    }
}
