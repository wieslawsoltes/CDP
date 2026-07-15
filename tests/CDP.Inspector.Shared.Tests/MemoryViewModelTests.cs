using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using CdpInspectorApp.ViewModels;
using Chrome.DevTools.Protocol;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class MemoryViewModelTests
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

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public virtual Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            return Task.FromResult(new JsonObject());
        }

        public void TriggerEvent(string method, JsonObject @params)
        {
            EventReceived?.Invoke(this, new CdpEventEventArgs(method, @params));
        }
    }

    [AvaloniaFact]
    public void TestAllocationHistory_ReceivesMetricsAndCappedAt30()
    {
        var mockService = new MockCdpService { IsConnected = true };
        var viewModel = new MemoryViewModel(mockService);

        Assert.Null(viewModel.AllocationHistory);

        // Send 1 metric event
        var metricsArray = new JsonArray
        {
            new JsonObject { ["name"] = "MemoryAllocations", ["value"] = 5.2 }
        };
        var paramsObj = new JsonObject { ["metrics"] = metricsArray };

        mockService.TriggerEvent("Performance.metrics", paramsObj);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(viewModel.AllocationHistory);
        Assert.Single(viewModel.AllocationHistory);
        Assert.Equal(5.2, viewModel.AllocationHistory[0]);

        // Send more to exceed 30
        for (int i = 0; i < 35; i++)
        {
            metricsArray = new JsonArray
            {
                new JsonObject { ["name"] = "MemoryAllocations", ["value"] = (double)i }
            };
            paramsObj = new JsonObject { ["metrics"] = metricsArray };
            mockService.TriggerEvent("Performance.metrics", paramsObj);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.NotNull(viewModel.AllocationHistory);
        Assert.Equal(30, viewModel.AllocationHistory.Count);
        // The last one should be 34
        Assert.Equal(34.0, viewModel.AllocationHistory[29]);
    }

    [AvaloniaFact]
    public void TestClearData_ResetsAllocationHistory()
    {
        var mockService = new MockCdpService { IsConnected = true };
        var viewModel = new MemoryViewModel(mockService);

        var metricsArray = new JsonArray
        {
            new JsonObject { ["name"] = "MemoryAllocations", ["value"] = 1.0 }
        };
        mockService.TriggerEvent("Performance.metrics", new JsonObject { ["metrics"] = metricsArray });
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(viewModel.AllocationHistory);

        // Disconnect to trigger ClearData() via PropertyChanged
        mockService.IsConnected = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Null(viewModel.AllocationHistory);
    }
}
