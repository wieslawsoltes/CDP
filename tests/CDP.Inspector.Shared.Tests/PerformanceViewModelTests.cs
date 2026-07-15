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

public class PerformanceViewModelTests
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

        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            return Task.FromResult(new JsonObject());
        }

        public void TriggerEvent(string method, JsonObject @params)
        {
            EventReceived?.Invoke(this, new CdpEventEventArgs(method, @params));
        }
    }

    [AvaloniaFact]
    public void TestCpuBreakdown_CalculatesCorrectPercentages()
    {
        var mockService = new MockCdpService { IsConnected = true };
        var viewModel = new PerformanceViewModel(mockService);

        // Verify defaults
        Assert.Equal(0.0, viewModel.CpuScripting);
        Assert.Equal(0.0, viewModel.CpuRendering);
        Assert.Equal(0.0, viewModel.CpuLayout);
        Assert.Equal(0.0, viewModel.CpuSystem);
        Assert.Equal(100.0, viewModel.CpuIdle);

        // Send a metrics event with specific duration and CPU usage values
        // Case 1: active sum is less than CPUUsage
        // CPUUsage = 40.0%
        // DispatcherQueueDelay = 0.1s (scriptingPct = 10%)
        // LayoutDuration = 0.05s (layoutPct = 5%)
        // FrameDuration = 0.15s (renderingPct = (0.15 - 0.05) * 100 = 10%)
        // Sum = 25%. System should be 40 - 25 = 15%. Idle = 60%.
        var metricsArray = new JsonArray
        {
            new JsonObject { ["name"] = "CPUUsage", ["value"] = 40.0 },
            new JsonObject { ["name"] = "DispatcherQueueDelay", ["value"] = 0.1 },
            new JsonObject { ["name"] = "LayoutDuration", ["value"] = 0.05 },
            new JsonObject { ["name"] = "FrameDuration", ["value"] = 0.15 }
        };
        var paramsObj = new JsonObject { ["metrics"] = metricsArray };

        mockService.TriggerEvent("Performance.metrics", paramsObj);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(60.0, viewModel.CpuIdle);
        Assert.Equal(10.0, viewModel.CpuScripting);
        Assert.Equal(10.0, viewModel.CpuRendering);
        Assert.Equal(5.0, viewModel.CpuLayout);
        Assert.Equal(15.0, viewModel.CpuSystem);
    }

    [AvaloniaFact]
    public void TestCpuBreakdown_ClampsAndScalesActiveValues()
    {
        var mockService = new MockCdpService { IsConnected = true };
        var viewModel = new PerformanceViewModel(mockService);

        // Case 2: active sum exceeds CPUUsage
        // CPUUsage = 15.0%
        // DispatcherQueueDelay = 0.1s (10%)
        // LayoutDuration = 0.05s (5%)
        // FrameDuration = 0.15s (10%)
        // Sum = 25% > 15%.
        // Scale factor: 15 / 25 = 0.6
        // CpuScripting = 10 * 0.6 = 6%
        // CpuLayout = 5 * 0.6 = 3%
        // CpuRendering = 10 * 0.6 = 6%
        // CpuSystem = 0%
        // CpuIdle = 85%
        var metricsArray = new JsonArray
        {
            new JsonObject { ["name"] = "CPUUsage", ["value"] = 15.0 },
            new JsonObject { ["name"] = "DispatcherQueueDelay", ["value"] = 0.1 },
            new JsonObject { ["name"] = "LayoutDuration", ["value"] = 0.05 },
            new JsonObject { ["name"] = "FrameDuration", ["value"] = 0.15 }
        };
        var paramsObj = new JsonObject { ["metrics"] = metricsArray };

        mockService.TriggerEvent("Performance.metrics", paramsObj);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(85.0, viewModel.CpuIdle);
        Assert.Equal(6.0, viewModel.CpuScripting);
        Assert.Equal(6.0, viewModel.CpuRendering);
        Assert.Equal(3.0, viewModel.CpuLayout);
        Assert.Equal(0.0, viewModel.CpuSystem);
    }

    [AvaloniaFact]
    public void TestClearData_ResetsCpuBreakdown()
    {
        var mockService = new MockCdpService { IsConnected = true };
        var viewModel = new PerformanceViewModel(mockService);

        var metricsArray = new JsonArray
        {
            new JsonObject { ["name"] = "CPUUsage", ["value"] = 30.0 }
        };
        mockService.TriggerEvent("Performance.metrics", new JsonObject { ["metrics"] = metricsArray });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(70.0, viewModel.CpuIdle);

        // Disconnect to trigger ClearData()
        mockService.IsConnected = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(100.0, viewModel.CpuIdle);
        Assert.Equal(0.0, viewModel.CpuScripting);
        Assert.Equal(0.0, viewModel.CpuRendering);
        Assert.Equal(0.0, viewModel.CpuLayout);
        Assert.Equal(0.0, viewModel.CpuSystem);
    }
}
