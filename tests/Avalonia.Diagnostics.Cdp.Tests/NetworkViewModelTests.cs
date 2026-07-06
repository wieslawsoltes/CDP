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

    [Fact]
    public void Test_NetworkRequestModel_Timing_Properties()
    {
        var model = new NetworkRequestModel();
        model.StartTime = 10.0;
        model.ResponseReceivedTime = 12.5;
        model.EndTime = 13.0;

        // Call UpdateTimeline to process
        // session start: 10.0, session total: 20.0
        model.UpdateTimeline(10.0, 20.0);

        Assert.Equal(2.5, model.TtfbDuration); // 12.5 - 10.0
        Assert.Equal(0.5, model.DownloadDuration); // 13.0 - 12.5
        Assert.Equal(3.0, model.Duration); // 13.0 - 10.0

        // Ratio of TTFB to request duration: 2.5 / 3.0 = 0.8333333333333334
        Assert.True(Math.Abs(model.TtfbPercentOfRequest - (2.5 / 3.0)) < 0.0001);
        // Ratio of Download to request duration: 0.5 / 3.0 = 0.16666666666666666
        Assert.True(Math.Abs(model.DownloadPercentOfRequest - (0.5 / 3.0)) < 0.0001);

        // Formatted durations
        Assert.Equal("2.50 s", model.TtfbDurationText);
        Assert.Equal("500 ms", model.DownloadDurationText);
        Assert.Equal("3.00 s", model.TotalDurationText);
    }
}
