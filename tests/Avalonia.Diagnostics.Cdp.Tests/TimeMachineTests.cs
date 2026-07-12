#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using Chrome.DevTools.Protocol;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class TimeMachineTests
{
    [Fact]
    public async Task TimeMachine_RecordAndReplayInterception()
    {
        // 1. Arrange
        var service = new CdpService();
        var tm = service.TimeMachine;

        var cmdParams = new JsonObject { ["param"] = "test" };
        var cmdResult = new JsonObject { ["result"] = "ok" };

        // 2. Act - Record command
        tm.StartRecording();
        tm.RecordResponse("DOM.getDocument", cmdParams, cmdResult);
        tm.RecordEvent("Log.entryAdded", new JsonObject { ["text"] = "hello" });
        tm.StopRecording();

        // Assert recorded state
        Assert.Equal(2, tm.Frames.Count);
        Assert.Equal("Response", tm.Frames[0].Type);
        Assert.Equal("DOM.getDocument", tm.Frames[0].Method);
        Assert.Equal("Event", tm.Frames[1].Type);
        Assert.Equal("Log.entryAdded", tm.Frames[1].Method);

        // 3. Act - Replay virtualization
        tm.IsReplaying = true;
        tm.Seek(0);

        var result = await service.SendCommandAsync("DOM.getDocument", cmdParams);

        // Assert response is intercepted from recorded frames list
        Assert.NotNull(result);
        Assert.Equal("ok", result["result"]?.GetValue<string>());
    }

    [Fact]
    public void TimeMachine_ReplayEventRedispatch()
    {
        var service = new CdpService();
        var tm = service.TimeMachine;

        var receivedEvents = new List<CdpEventEventArgs>();
        service.EventReceived += (s, e) => receivedEvents.Add(e);

        tm.StartRecording();
        tm.RecordEvent("Log.entryAdded", new JsonObject { ["text"] = "hello" });
        tm.RecordEvent("Console.messageAdded", new JsonObject { ["text"] = "world" });
        tm.StopRecording();

        receivedEvents.Clear();

        // Seek timeline back to index 1
        tm.Seek(1);

        // Assert both events are re-dispatched sequentially
        Assert.Equal(2, receivedEvents.Count);
        Assert.Equal("Log.entryAdded", receivedEvents[0].Method);
        Assert.Equal("Console.messageAdded", receivedEvents[1].Method);
    }

    [Fact]
    public void TimeMachine_Serialization()
    {
        var tm1 = new TimeMachineService();
        tm1.StartRecording();
        tm1.RecordEvent("Log.entryAdded", new JsonObject { ["text"] = "one" });
        tm1.RecordResponse("DOM.getDocument", null, new JsonObject { ["val"] = 42 });
        tm1.StopRecording();

        var state = tm1.SaveState();
        Assert.NotNull(state);

        var tm2 = new TimeMachineService();
        tm2.LoadState(state);

        Assert.Equal(2, tm2.Frames.Count);
        Assert.Equal("Event", tm2.Frames[0].Type);
        Assert.Equal("one", tm2.Frames[0].Payload?["text"]?.GetValue<string>());
        Assert.Equal("Response", tm2.Frames[1].Type);
        Assert.Equal(42, tm2.Frames[1].Payload?["val"]?.GetValue<int>());
        Assert.True(tm2.IsReplaying);
    }

    [Fact]
    public void TimeMachine_GetReplayResponseAtFrame()
    {
        var tm = new TimeMachineService();
        tm.StartRecording();

        var params1 = new JsonObject { ["id"] = 1 };
        var result1 = new JsonObject { ["data"] = "first" };

        var params2 = new JsonObject { ["id"] = 2 };
        var result2 = new JsonObject { ["data"] = "second" };

        var params3 = new JsonObject { ["id"] = 1 };
        var result3 = new JsonObject { ["data"] = "third" };

        // Frame 0: Response 1
        tm.RecordResponse("DOM.querySelector", params1, result1);
        // Frame 1: Event
        tm.RecordEvent("Log.entryAdded", new JsonObject { ["text"] = "log" });
        // Frame 2: Response 2
        tm.RecordResponse("DOM.querySelector", params2, result2);
        // Frame 3: Response 3
        tm.RecordResponse("DOM.querySelector", params3, result3);
        tm.StopRecording();

        Assert.Equal(4, tm.Frames.Count);

        // Verify querying at/before Frame 1 for params1 finds result1
        var res1 = tm.GetReplayResponseAtFrame("DOM.querySelector", params1, 1);
        Assert.NotNull(res1);
        Assert.Equal("first", res1["data"]?.GetValue<string>());

        // Verify querying at/before Frame 1 for params2 returns null (since it was recorded at Frame 2)
        var res2 = tm.GetReplayResponseAtFrame("DOM.querySelector", params2, 1);
        Assert.Null(res2);

        // Verify querying at/before Frame 2 for params2 finds result2
        var res3 = tm.GetReplayResponseAtFrame("DOM.querySelector", params2, 2);
        Assert.NotNull(res3);
        Assert.Equal("second", res3["data"]?.GetValue<string>());

        // Verify querying at/before Frame 3 for params1 finds result3 (the most recent matching response at/before Frame 3)
        var res4 = tm.GetReplayResponseAtFrame("DOM.querySelector", params1, 3);
        Assert.NotNull(res4);
        Assert.Equal("third", res4["data"]?.GetValue<string>());

        // Verify serialization preserves parameters and querying still works
        var state = tm.SaveState();
        var tm2 = new TimeMachineService();
        tm2.LoadState(state);

        var res5 = tm2.GetReplayResponseAtFrame("DOM.querySelector", params1, 1);
        Assert.NotNull(res5);
        Assert.Equal("first", res5["data"]?.GetValue<string>());

        var res6 = tm2.GetReplayResponseAtFrame("DOM.querySelector", params1, 3);
        Assert.NotNull(res6);
        Assert.Equal("third", res6["data"]?.GetValue<string>());
    }

    [Fact]
    public void TimeMachineViewModel_FilteringAndSorting()
    {
        // Arrange
        var service = new CdpService();
        var tm = service.TimeMachine;
        
        tm.StartRecording();
        tm.RecordResponse("DOM.getDocument", null, null); // Index 0, Domain: DOM
        tm.RecordEvent("Network.requestWillBeSent", null); // Index 1, Domain: Network
        tm.RecordEvent("DOM.childNodeCountUpdated", null); // Index 2, Domain: DOM
        tm.RecordEvent("Accessibility.loadComplete", null); // Index 3, Domain: Accessibility
        tm.StopRecording();

        var vm = new CdpInspectorApp.ViewModels.TimeMachineViewModel(service);

        // Act - Default (Index Ascending, No filter)
        Assert.Equal(4, vm.FilteredFrames.Count);
        Assert.Equal("DOM.getDocument", vm.FilteredFrames[0].Method);
        Assert.Equal("Accessibility.loadComplete", vm.FilteredFrames[3].Method);

        // Act - Filter by search text
        vm.SearchText = "child";
        Assert.Single(vm.FilteredFrames);
        Assert.Equal("DOM.childNodeCountUpdated", vm.FilteredFrames[0].Method);

        // Act - Clear Search text, set Domain filter
        vm.SearchText = "";
        vm.SelectedDomainFilter = "DOM";
        Assert.Equal(2, vm.FilteredFrames.Count);
        Assert.All(vm.FilteredFrames, f => Assert.Equal("DOM", f.Domain));

        // Act - Domain filter + SearchText combo
        vm.SearchText = "getDocument";
        Assert.Single(vm.FilteredFrames);
        Assert.Equal("DOM.getDocument", vm.FilteredFrames[0].Method);

        // Reset filter
        vm.SearchText = "";
        vm.SelectedDomainFilter = "All";

        // Act - Sort Index Descending
        vm.SelectedSortOption = "Index Descending";
        Assert.Equal(4, vm.FilteredFrames.Count);
        Assert.Equal("Accessibility.loadComplete", vm.FilteredFrames[0].Method);
        Assert.Equal("DOM.getDocument", vm.FilteredFrames[3].Method);

        // Act - Sort Method
        vm.SelectedSortOption = "Method";
        Assert.Equal(4, vm.FilteredFrames.Count);
        Assert.Equal("Accessibility.loadComplete", vm.FilteredFrames[0].Method);
        Assert.Equal("Network.requestWillBeSent", vm.FilteredFrames[3].Method);

        // Act - Sort Domain
        vm.SelectedSortOption = "Domain";
        Assert.Equal(4, vm.FilteredFrames.Count);
        Assert.Equal("Accessibility.loadComplete", vm.FilteredFrames[0].Method); // A before D, N
    }

    [Fact]
    public void TimeMachine_OptimizedReplayAndEventDispatch()
    {
        var service = new CdpService();
        var tm = service.TimeMachine;

        var params1 = new JsonObject { ["x"] = 1 };
        var result1 = new JsonObject { ["res"] = "A" };
        var params2 = new JsonObject { ["x"] = 2 };
        var result2 = new JsonObject { ["res"] = "B" };

        tm.StartRecording();
        tm.RecordResponse("DOM.getDocument", params1, result1);
        tm.RecordEvent("Log.entryAdded", new JsonObject { ["text"] = "e1" });
        tm.RecordResponse("DOM.querySelector", params2, result2);
        tm.RecordEvent("Log.entryAdded", new JsonObject { ["text"] = "e2" });
        tm.StopRecording();

        // 1. Verify fast path and caching
        // Querying at frame index 0
        var res1 = tm.GetReplayResponseAtFrame("DOM.getDocument", params1, 0);
        Assert.NotNull(res1);
        Assert.Equal("A", res1["res"]?.GetValue<string>());

        // Querying again with same reference to hit cache or fast path
        var res1Cached = tm.GetReplayResponseAtFrame("DOM.getDocument", params1, 0);
        Assert.NotNull(res1Cached);
        Assert.Equal("A", res1Cached["res"]?.GetValue<string>());

        // Querying with structurally identical but different reference
        var params1Dup = new JsonObject { ["x"] = 1 };
        var res1Dup = tm.GetReplayResponseAtFrame("DOM.getDocument", params1Dup, 0);
        Assert.NotNull(res1Dup);
        Assert.Equal("A", res1Dup["res"]?.GetValue<string>());

        // Querying again with params1Dup to hit cache
        var res1DupCached = tm.GetReplayResponseAtFrame("DOM.getDocument", params1Dup, 0);
        Assert.NotNull(res1DupCached);
        Assert.Equal("A", res1DupCached["res"]?.GetValue<string>());

        // 2. Verify CdpService sequential event dispatch
        var eventsDispatched = new List<CdpEventEventArgs>();
        service.EventReceived += (s, e) => eventsDispatched.Add(e);

        // Seek to 0 (non-sequential) -> should rebuild/clear
        tm.IsReplaying = true;
        tm.Seek(0);
        Assert.Empty(eventsDispatched); // frame 0 is a Response, not Event

        // Helper to trigger FrameChanged without triggering ReplayStateCleared (simulating sequential playback)
        var triggerFrameChanged = () =>
        {
            var eventField = typeof(TimeMachineService).GetField("FrameChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var eventDelegate = (EventHandler?)eventField?.GetValue(tm);
            eventDelegate?.Invoke(tm, EventArgs.Empty);
        };

        // Step to 1 (sequential)
        tm.CurrentFrameIndex = 1; // frame 1 is Event: Log.entryAdded "e1"
        triggerFrameChanged();
        Assert.Single(eventsDispatched);
        Assert.Equal("Log.entryAdded", eventsDispatched[0].Method);
        Assert.Equal("e1", eventsDispatched[0].Params["text"]?.GetValue<string>());

        eventsDispatched.Clear();

        // Step to 2 (sequential)
        tm.CurrentFrameIndex = 2; // frame 2 is a Response, not Event. So no new event dispatched.
        triggerFrameChanged();
        Assert.Empty(eventsDispatched);

        eventsDispatched.Clear();

        // Step to 3 (sequential)
        tm.CurrentFrameIndex = 3; // frame 3 is Event: Log.entryAdded "e2"
        triggerFrameChanged();
        Assert.Single(eventsDispatched);
        Assert.Equal("Log.entryAdded", eventsDispatched[0].Method);
        Assert.Equal("e2", eventsDispatched[0].Params["text"]?.GetValue<string>());

        eventsDispatched.Clear();

        // Now seek back to 1 (non-sequential seek back)
        tm.Seek(1); // should replay from 0 to 1, so it should dispatch the event at 1 again
        Assert.Single(eventsDispatched);
        Assert.Equal("Log.entryAdded", eventsDispatched[0].Method);
        Assert.Equal("e1", eventsDispatched[0].Params["text"]?.GetValue<string>());
    }
}

