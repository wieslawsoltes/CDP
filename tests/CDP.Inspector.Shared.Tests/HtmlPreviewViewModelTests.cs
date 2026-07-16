using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Services;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class HtmlPreviewViewModelTests
{
    private class DelayedCdpService : ICdpService
    {
        public bool IsConnected { get; set; } = true;
        public string ConnectionStatus => "";
        public string ConnectedHost => "";
        public string ConnectedTargetId => "";
        public bool IsPreviewScreencastActive { get; set; } = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public int DelayMs { get; set; } = 0;
        public Dictionary<int, string> HtmlResponses { get; } = new();

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public async Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            if (method == "DOM.getOuterHTML" && parameters != null)
            {
                int nodeId = parameters["nodeId"]?.GetValue<int>() ?? 0;
                if (DelayMs > 0)
                {
                    await Task.Delay(DelayMs);
                }

                var response = new JsonObject();
                if (HtmlResponses.TryGetValue(nodeId, out var html))
                {
                    response["outerHTML"] = html;
                }
                else
                {
                    response["outerHTML"] = $"<div>Node {nodeId}</div>";
                }
                return response;
            }
            return new JsonObject();
        }
    }

    [Fact]
    public async Task TestAsyncRaceCondition_SlowerRequestDiscarded()
    {
        // Arrange
        var elementsVm = new ElementsViewModel(new DelayedCdpService());
        var cdpService = new DelayedCdpService();

        // We will control the delay dynamically
        var vm = new HtmlPreviewViewModel(cdpService, elementsVm);

        var node1 = new DomNodeModel(1, "Div");
        var node2 = new DomNodeModel(2, "Span");

        cdpService.HtmlResponses[1] = "<div>First Node</div>";
        cdpService.HtmlResponses[2] = "<span>Second Node</span>";

        // Act & Assert
        // First selection: set delay to 200ms
        cdpService.DelayMs = 200;
        elementsVm.SelectedNode = node1;

        // Almost immediately, change selection to node2 and set delay to 10ms
        await Task.Delay(20);
        cdpService.DelayMs = 10;
        elementsVm.SelectedNode = node2;

        // Wait for both to finish
        await Task.Delay(300);

        // Under correct behavior, OuterHtml should be node2's HTML ("<span>Second Node</span>")
        // and NOT node1's HTML even though node1's response finished later.
        Assert.Equal("<span>Second Node</span>", vm.OuterHtml);
    }

    [Fact]
    public async Task TestHighFrequencyNodeSelection_NoCrashOrRace()
    {
        // Arrange
        var elementsVm = new ElementsViewModel(new DelayedCdpService());
        var cdpService = new DelayedCdpService();
        var vm = new HtmlPreviewViewModel(cdpService, elementsVm);

        // Act & Assert
        // Simulate rapid node selections
        for (int i = 1; i <= 100; i++)
        {
            var node = new DomNodeModel(i, $"Node{i}");
            cdpService.HtmlResponses[i] = $"<{node.NodeName}>Content {i}</{node.NodeName}>";
            elementsVm.SelectedNode = node;
            
            // Random tiny delays to simulate rapid asynchronous execution
            if (i % 5 == 0)
            {
                await Task.Delay(1);
            }
        }

        // Wait for all outstanding async operations to complete
        await Task.Delay(200);

        // The outer HTML should be the very last selected node (Node 100)
        Assert.Equal("<Node100>Content 100</Node100>", vm.OuterHtml);
    }

}

