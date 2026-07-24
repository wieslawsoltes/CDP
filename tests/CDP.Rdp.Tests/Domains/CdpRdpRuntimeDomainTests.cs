namespace CDP.Rdp.Tests.Domains;

using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using Avalonia.Diagnostics.Cdp.Domains;
using Avalonia.Headless.XUnit;
using CdpRdpApp;
using CdpRdpApp.ViewModels;
using Xunit;

public class CdpRdpRuntimeDomainTests
{
    [AvaloniaFact]
    public async Task RuntimeEnable_And_Evaluate_DataContextProperties_Succeeds()
    {
        var window = new MainWindow();
        window.Show();

        var vm = (MainWindowViewModel)window.DataContext!;
        vm.Host = "10.0.0.42";
        vm.Port = 3389;

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var enableResult = await RuntimeDomain.HandleAsync(session, "enable", new JsonObject());
        Assert.NotNull(enableResult);

        var evalHost = await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
        {
            ["expression"] = "DataContext.Host",
            ["returnByValue"] = true
        });
        Assert.NotNull(evalHost);
        Assert.Equal("10.0.0.42", evalHost["result"]?["value"]?.GetValue<string>());

        var evalConnected = await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
        {
            ["expression"] = "DataContext.Connection.IsConnected",
            ["returnByValue"] = true
        });
        Assert.NotNull(evalConnected);
        Assert.False(evalConnected["result"]?["value"]?.GetValue<bool>());

        window.Close();
    }

    [AvaloniaFact]
    public async Task RuntimeEvaluate_DocumentQuerySelector_ReturnsControlId()
    {
        var window = new MainWindow();
        window.Show();

        using var clientWs = new ClientWebSocket();
        var session = new CdpSession(clientWs, window);

        var evalControlId = await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
        {
            ["expression"] = "document.querySelector('#txtHost').Name"
        });
        Assert.NotNull(evalControlId);
        Assert.Equal("txtHost", evalControlId["result"]?["value"]?.GetValue<string>());

        var evalBtnId = await RuntimeDomain.HandleAsync(session, "evaluate", new JsonObject
        {
            ["expression"] = "document.querySelector('#btnConnect').Name"
        });
        Assert.NotNull(evalBtnId);
        Assert.Equal("btnConnect", evalBtnId["result"]?["value"]?.GetValue<string>());

        window.Close();
    }
}
