using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Services;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class DesignerViewModelTests
{
    private class FakeCdpService : ICdpService
    {
        public bool IsConnected { get; set; } = true;
        public string ConnectionStatus => "";
        public string ConnectedHost => "";
        public string ConnectedTargetId => "";
        public bool IsPreviewScreencastActive { get; set; } = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public List<(string Method, JsonObject? Parameters)> SentCommands { get; } = new();

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task ConnectAsync(string host, TargetItem target, bool autoResume) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            SentCommands.Add((method, parameters));

            var response = new JsonObject();
            if (method == "DOM.getDocument")
            {
                response["root"] = new JsonObject { ["nodeId"] = 1 };
            }
            else if (method == "DOM.querySelector")
            {
                var selector = parameters?["selector"]?.GetValue<string>();
                if (selector != null && selector.Contains("TextBlock"))
                {
                    response["nodeId"] = 100;
                }
                else
                {
                    response["nodeId"] = 42;
                }
            }
            else if (method == "DOM.getNodeForLocation")
            {
                var x = parameters?["x"]?.GetValue<int>() ?? 0;
                response["nodeId"] = (x > 100) ? 42 : 100;
            }
            else if (method == "DOM.getBoxModel")
            {
                response["model"] = new JsonObject
                {
                    ["margin"] = new JsonArray { 0, 0, 100, 0, 100, 100, 0, 100 },
                    ["border"] = new JsonArray { 0, 0, 100, 0, 100, 100, 0, 100 },
                    ["padding"] = new JsonArray { 0, 0, 100, 0, 100, 100, 0, 100 },
                    ["content"] = new JsonArray { 10.0, 10.0, 90.0, 10.0, 90.0, 70.0, 10.0, 70.0 },
                    ["width"] = 80.0,
                    ["height"] = 60.0
                };
            }
            else if (method == "CSS.getComputedStyleForNode")
            {
                var style = new JsonArray
                {
                    new JsonObject { ["name"] = "margin-left", ["value"] = "10px" },
                    new JsonObject { ["name"] = "margin-top", ["value"] = "10px" },
                    new JsonObject { ["name"] = "margin-right", ["value"] = "10px" },
                    new JsonObject { ["name"] = "margin-bottom", ["value"] = "10px" },
                    new JsonObject { ["name"] = "padding-left", ["value"] = "5px" },
                    new JsonObject { ["name"] = "padding-top", ["value"] = "5px" },
                    new JsonObject { ["name"] = "padding-right", ["value"] = "5px" },
                    new JsonObject { ["name"] = "padding-bottom", ["value"] = "5px" }
                };
                response["computedStyle"] = style;
            }
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void TestToolboxInitialization()
    {
        var spy = new FakeCdpService();
        var elementsVm = new ElementsViewModel(spy);
        var vm = new DesignerViewModel(spy, () => elementsVm);

        vm.DetectedPlatform = "Avalonia";
        Assert.NotEmpty(vm.ToolboxItems);
        Assert.Contains("Button", vm.ToolboxItems.Select(x => x.Name));

        vm.DetectedPlatform = "HTML";
        Assert.NotEmpty(vm.ToolboxItems);
        Assert.Contains("div", vm.ToolboxItems.Select(x => x.Name));
    }

    [Fact]
    public async Task TestMultiSelection()
    {
        var spy = new FakeCdpService();
        var elementsVm = new ElementsViewModel(spy);
        var vm = new DesignerViewModel(spy, () => elementsVm);

        // Add dummy nodes
        var node1 = new DomNodeModel(42, "Button");
        var node2 = new DomNodeModel(100, "TextBlock");
        elementsVm.RootNodes.Add(node1);
        elementsVm.RootNodes.Add(node2);

        // First selection (not holding Ctrl)
        await vm.SelectElementAtLocationAsync(50, 50, false);
        Assert.Single(vm.SelectedElements);
        Assert.Equal(100, vm.SelectedElements[0].NodeId);

        // Second selection holding Ctrl
        await vm.SelectElementAtLocationAsync(150, 150, true);
        Assert.Equal(2, vm.SelectedElements.Count);
        Assert.Contains(100, vm.SelectedElements.Select(x => x.NodeId));
        Assert.Contains(42, vm.SelectedElements.Select(x => x.NodeId));
    }

    [Fact]
    public void TestSnapToGridAndRealTimeBounds()
    {
        var spy = new FakeCdpService();
        var elementsVm = new ElementsViewModel(spy);
        var vm = new DesignerViewModel(spy, () => elementsVm);

        // Selected element bounds
        vm.UpdateSelectedBoundsRealTime(11, 23, 105, 50);

        Assert.Equal(11, vm.SelectedElementX);
        Assert.Equal(23, vm.SelectedElementY);
        Assert.Equal(105, vm.SelectedElementWidth);
        Assert.Equal(50, vm.SelectedElementHeight);
    }

    [Fact]
    public async Task TestContainerAttachmentGrid()
    {
        var spy = new FakeCdpService();
        var elementsVm = new ElementsViewModel(spy);
        var vm = new DesignerViewModel(spy, () => elementsVm);

        var node = new DomNodeModel(42, "Button");
        elementsVm.SelectedNode = node;
        await vm.SelectElementAsync("#btnMyButton");

        vm.GridRow = 2;
        vm.GridColumn = 3;
        vm.GridRowSpan = 1;
        vm.GridColumnSpan = 4;

        spy.SentCommands.Clear();
        await vm.ApplyGridAttachmentAsync();

        var rowCmd = spy.SentCommands.FirstOrDefault(c => c.Method == "DOM.setAttributeValue" && c.Parameters?["name"]?.GetValue<string>() == "Grid.Row");
        Assert.NotNull(rowCmd.Parameters);
        Assert.Equal("2", rowCmd.Parameters["value"]?.GetValue<string>());

        var colSpanCmd = spy.SentCommands.FirstOrDefault(c => c.Method == "DOM.setAttributeValue" && c.Parameters?["name"]?.GetValue<string>() == "Grid.ColumnSpan");
        Assert.NotNull(colSpanCmd.Parameters);
        Assert.Equal("4", colSpanCmd.Parameters["value"]?.GetValue<string>());
    }

    [Fact]
    public async Task TestContainerDefinitionsGrid()
    {
        var spy = new FakeCdpService();
        var elementsVm = new ElementsViewModel(spy);
        var vm = new DesignerViewModel(spy, () => elementsVm);

        var node = new DomNodeModel(42, "Grid");
        elementsVm.SelectedNode = node;
        await vm.SelectElementAsync("#myGrid");

        vm.RowDefinitions = "Auto,*,2*";
        vm.ColumnDefinitions = "*,200";

        spy.SentCommands.Clear();
        await vm.ApplyGridDefinitionsAsync();

        var rowDefCmd = spy.SentCommands.FirstOrDefault(c => c.Method == "DOM.setAttributeValue" && c.Parameters?["name"]?.GetValue<string>() == "RowDefinitions");
        Assert.NotNull(rowDefCmd.Parameters);
        Assert.Equal("Auto,*,2*", rowDefCmd.Parameters["value"]?.GetValue<string>());
    }
}
