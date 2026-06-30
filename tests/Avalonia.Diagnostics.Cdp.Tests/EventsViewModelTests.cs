using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using Avalonia.Threading;
using CdpInspectorApp.ViewModels;
using Chrome.DevTools.Protocol;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class EventsViewModelTests
{
    public class FakeCdpService : ICdpService
    {
        public bool IsConnected => true;
        public string ConnectionStatus => "";
        public string ConnectedHost => "";
        public string ConnectedTargetId => "";
        public bool IsPreviewScreencastActive { get; set; } = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null) => Task.FromResult(new JsonObject());

        public void FireEvent(string method, JsonObject parameters)
        {
            EventReceived?.Invoke(this, new CdpEventEventArgs(method, parameters));
        }
    }

    [Fact]
    public void TestEventsViewModelLogsIncomingEvents()
    {
        var service = new FakeCdpService();
        var vm = new EventsViewModel(service);

        var payload = new JsonObject
        {
            ["nodeId"] = 42,
            ["selector"] = "#test-node"
        };

        service.FireEvent("DOM.querySelector", payload);

        // Run UI loop dispatcher tasks to ensure events are processed
        Dispatcher.UIThread.RunJobs();

        Assert.Single(vm.FilteredEvents);
        Assert.Equal("DOM.querySelector", vm.FilteredEvents[0].Method);
        Assert.Contains("test-node", vm.FilteredEvents[0].ParamsJson);
    }

    [Fact]
    public void TestEventsViewModelIgnoreScreencast()
    {
        var service = new FakeCdpService();
        var vm = new EventsViewModel(service);

        // By default, IgnoreScreencast should be true
        Assert.True(vm.IgnoreScreencast);

        var framePayload = new JsonObject { ["data"] = "frame-bytes" };
        service.FireEvent("Page.screencastFrame", framePayload);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(vm.FilteredEvents);

        // Toggle ignore off
        vm.IgnoreScreencast = false;
        service.FireEvent("Page.screencastFrame", framePayload);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(vm.FilteredEvents);
        Assert.Equal("Page.screencastFrame", vm.FilteredEvents[0].Method);
    }

    [Fact]
    public void TestEventsViewModelSearchFilter()
    {
        var service = new FakeCdpService();
        var vm = new EventsViewModel(service);

        service.FireEvent("DOM.documentUpdated", new JsonObject());
        service.FireEvent("Console.messageAdded", new JsonObject { ["text"] = "Hello log" });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, vm.FilteredEvents.Count);

        // Filter by method
        vm.SearchQuery = "Console";
        Dispatcher.UIThread.RunJobs();
        Assert.Single(vm.FilteredEvents);
        Assert.Equal("Console.messageAdded", vm.FilteredEvents[0].Method);

        // Filter by payload text
        vm.SearchQuery = "Hello";
        Dispatcher.UIThread.RunJobs();
        Assert.Single(vm.FilteredEvents);
        Assert.Equal("Console.messageAdded", vm.FilteredEvents[0].Method);

        // No matches
        vm.SearchQuery = "nonexistent";
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(vm.FilteredEvents);
    }

    [Fact]
    public void TestEventsViewModelPause()
    {
        var service = new FakeCdpService();
        var vm = new EventsViewModel(service);

        vm.IsPaused = true;
        service.FireEvent("DOM.documentUpdated", new JsonObject());
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(vm.FilteredEvents);
    }

    [Fact]
    public void TestEventsViewModelClear()
    {
        var service = new FakeCdpService();
        var vm = new EventsViewModel(service);

        service.FireEvent("DOM.documentUpdated", new JsonObject());
        Dispatcher.UIThread.RunJobs();
        Assert.Single(vm.FilteredEvents);

        vm.ClearEventsCommand.Execute(null);
        Assert.Empty(vm.FilteredEvents);
        Assert.Null(vm.SelectedEvent);
        Assert.Empty(vm.SelectedEventPayload);
    }

    [Fact]
    public void TestEventsViewModelBufferCap()
    {
        var service = new FakeCdpService();
        var vm = new EventsViewModel(service);

        for (int i = 0; i < 550; i++)
        {
            service.FireEvent($"Event.{i}", new JsonObject());
        }
        Dispatcher.UIThread.RunJobs();

        // Capped at 500 items
        Assert.Equal(500, vm.FilteredEvents.Count);
        Assert.Equal("Event.50", vm.FilteredEvents[0].Method); // The first 50 should be discarded
        Assert.Equal("Event.549", vm.FilteredEvents[499].Method);
    }
}
