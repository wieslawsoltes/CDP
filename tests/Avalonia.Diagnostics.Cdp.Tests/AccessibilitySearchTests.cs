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

public class AccessibilitySearchTests
{
    public class MockCdpService : ICdpService
    {
        public bool IsConnected { get; set; } = true;
        public string ConnectionStatus => "";
        public string ConnectedHost => "";
        public string ConnectedTargetId => "";

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            return Task.FromResult(new JsonObject());
        }
    }

    [Fact]
    public void TestAccessibilityTreeSearchAndSelection()
    {
        var cdpService = new MockCdpService();
        var vm = new ElementsViewModel(cdpService);

        // Build a mock AXTree hierarchy:
        // Window (Root)
        //   ├── CheckBox "Enable Settings" (NodeId=10)
        //   ├── Slider "Volume Control" (NodeId=20)
        //   └── Button "Click Me" (NodeId=30)
        //       └── Label "Sub Settings" (NodeId=40)

        var rootNode = new AxNodeModel("1", "Window", "MainWindow", false, null);
        var cbNode = new AxNodeModel("10", "CheckBox", "Enable Settings", false, null);
        var sliderNode = new AxNodeModel("20", "Slider", "Volume Control", false, null);
        var btnNode = new AxNodeModel("30", "Button", "Click Me", false, null);
        var subLabel = new AxNodeModel("40", "Label", "Sub Settings", false, null);

        rootNode.Children.Add(cbNode);
        rootNode.Children.Add(sliderNode);
        rootNode.Children.Add(btnNode);
        btnNode.Children.Add(subLabel);

        vm.AxRootNodes.Add(rootNode);

        // 1. Search for "Slider" (Matches Role)
        vm.AxSearchQuery = "Slider";
        vm.AxSearchCommand.Execute(null);

        Assert.NotNull(vm.SelectedAxNode);
        Assert.Equal("20", vm.SelectedAxNode.NodeId);
        Assert.True(rootNode.IsExpanded);
        Assert.True(sliderNode.IsSelected);

        // 2. Search for "Settings" (Matches "Enable Settings" and "Sub Settings")
        vm.AxSearchQuery = "settings";
        
        // First execution -> selects "Enable Settings" (NodeId=10)
        vm.AxSearchCommand.Execute(null);
        Assert.NotNull(vm.SelectedAxNode);
        Assert.Equal("10", vm.SelectedAxNode.NodeId);
        Assert.True(cbNode.IsSelected);

        // Second execution -> cycles to "Sub Settings" (NodeId=40)
        vm.AxSearchCommand.Execute(null);
        Assert.NotNull(vm.SelectedAxNode);
        Assert.Equal("40", vm.SelectedAxNode.NodeId);
        Assert.True(btnNode.IsExpanded); // parent of subLabel should expand
        Assert.True(subLabel.IsSelected);

        // Third execution -> cycles back to "Enable Settings" (NodeId=10)
        vm.AxSearchCommand.Execute(null);
        Assert.NotNull(vm.SelectedAxNode);
        Assert.Equal("10", vm.SelectedAxNode.NodeId);
        Assert.True(cbNode.IsSelected);
    }
}
