using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using Avalonia.Headless.XUnit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Services;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class ConsoleViewModelTests
{
    public class FakeCdpService : ICdpService
    {
        private bool _isConnected = true;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
                }
            }
        }
        public string ConnectionStatus => "";
        public string ConnectedHost => "";
        public string ConnectedTargetId => "";
        public bool IsPreviewScreencastActive { get; set; } = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public List<(string Method, JsonObject? Parameters)> SentCommands { get; } = new();

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            SentCommands.Add((method, parameters));

            var response = new JsonObject();
            if (method == "Runtime.evaluate")
            {
                var expr = parameters?["expression"]?.GetValue<string>();
                if (expr == "window")
                {
                    response["result"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["objectId"] = "window-obj-123",
                        ["description"] = "Window"
                    };
                }
                else
                {
                    response["result"] = new JsonObject
                    {
                        ["description"] = $"Evaluated: {expr}"
                    };
                }
            }
            else if (method == "Runtime.getProperties")
            {
                var properties = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "Width",
                        ["value"] = new JsonObject
                        {
                            ["type"] = "number",
                            ["value"] = 800,
                            ["description"] = "800"
                        }
                    },
                    new JsonObject
                    {
                        ["name"] = "Document",
                        ["value"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["objectId"] = "doc-obj-456",
                            ["description"] = "HTMLDocument"
                        }
                    }
                };
                response["result"] = properties;
            }
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void TestAddAndRemovePinnedExpression()
    {
        var spy = new FakeCdpService();
        var vm = new ConsoleViewModel(spy);

        // Initially empty
        Assert.Empty(vm.PinnedExpressions);

        // Can't add empty
        vm.PinnedExpressionInputText = "";
        Assert.False(vm.AddPinnedExpressionCommand.CanExecute(null));

        // Add an expression
        vm.PinnedExpressionInputText = "Window.Width";
        Assert.True(vm.AddPinnedExpressionCommand.CanExecute(null));
        vm.AddPinnedExpressionCommand.Execute(null);

        Assert.Single(vm.PinnedExpressions);
        Assert.Equal("Window.Width", vm.PinnedExpressions[0].Expression);
        Assert.Equal("", vm.PinnedExpressionInputText);

        // Remove the expression
        var item = vm.PinnedExpressions[0];
        vm.RemovePinnedExpressionCommand.Execute(item);
        Assert.Empty(vm.PinnedExpressions);
    }

    [AvaloniaFact]
    public async Task TestConsoleObjectTreeExpander()
    {
        var spy = new FakeCdpService();
        var vm = new ConsoleViewModel(spy);

        vm.ConsoleInputText = "window";
        await vm.EvaluateAsync();

        Assert.Single(vm.ConsoleHistory);
        var consoleItem = vm.ConsoleHistory[0];
        Assert.True(consoleItem.IsObject);
        Assert.Equal("window-obj-123", consoleItem.ObjectId);
        Assert.NotNull(consoleItem.HierarchicalResult);
        Assert.NotNull(consoleItem.RootNode);

        // Check root node
        var rootNode = consoleItem.RootNode;
        Assert.Equal("Result", rootNode.Name);
        Assert.Equal("window-obj-123", rootNode.ObjectId);
        Assert.True(rootNode.IsExpandable);

        // Trigger loading children
        var children = rootNode.GetChildren().ToList();
        Assert.Single(children);
        Assert.Equal("Loading...", children[0].Name);

        // Wait for background tasks to fetch properties
        for (int i = 0; i < 50; i++)
        {
            if (rootNode.Children.Count > 1 || (rootNode.Children.Count == 1 && rootNode.Children[0].Name != "Loading..."))
            {
                break;
            }
            await Task.Delay(20);
        }

        // Verify loaded properties
        Assert.Equal(2, rootNode.Children.Count);
        Assert.Equal("Width", rootNode.Children[0].Name);
        Assert.Equal("800", rootNode.Children[0].Value);
        Assert.False(rootNode.Children[0].IsExpandable);

        Assert.Equal("Document", rootNode.Children[1].Name);
        Assert.Equal("HTMLDocument", rootNode.Children[1].Value);
        Assert.True(rootNode.Children[1].IsExpandable);
    }
}
