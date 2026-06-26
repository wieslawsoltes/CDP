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
                response["result"] = new JsonObject
                {
                    ["description"] = $"Evaluated: {expr}"
                };
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
}
