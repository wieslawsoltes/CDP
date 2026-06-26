using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Models;
using Chrome.DevTools.Protocol;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class NetworkViewModelTests
{
    public class MockCdpService : ICdpService
    {
        private bool _isConnected;
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
        public bool IsPreviewScreencastActive { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public System.Collections.Generic.List<TargetItem> TargetList = new();

        public Task<System.Collections.Generic.List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(TargetList);
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            return Task.FromResult(new JsonObject());
        }
    }

    [Fact]
    public void Test_SelectedMockRule_Exposed_And_NotifiesChange()
    {
        var mockService = new MockCdpService { IsConnected = true };
        var viewModel = new NetworkViewModel(mockService);

        Assert.Null(viewModel.SelectedMockRule);

        bool propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(NetworkViewModel.SelectedMockRule))
            {
                propertyChangedRaised = true;
            }
        };

        var rule = new MockRuleModel
        {
            UrlPattern = "*api/v2/*",
            StatusCode = 200,
            MockBody = "OK"
        };

        viewModel.SelectedMockRule = rule;

        Assert.True(propertyChangedRaised);
        Assert.Equal(rule, viewModel.SelectedMockRule);
    }

    [Fact]
    public void Test_Add_And_Remove_MockRules_Commands()
    {
        var mockService = new MockCdpService { IsConnected = true };
        var viewModel = new NetworkViewModel(mockService);

        Assert.Empty(viewModel.MockRules);

        // Add
        viewModel.AddMockRuleCommand.Execute(null);
        Assert.Single(viewModel.MockRules);
        var addedRule = viewModel.MockRules[0];
        Assert.Equal("*api/v1/users*", addedRule.UrlPattern);

        // Remove
        viewModel.RemoveMockRuleCommand.Execute(addedRule);
        Assert.Empty(viewModel.MockRules);
    }
}
