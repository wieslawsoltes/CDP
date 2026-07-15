#nullable enable

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
using Chrome.DevTools.Protocol;
using CdpInspectorApp.Services;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class ScratchNodeEditorTests
{
    private class DummyCdpService : ICdpService
    {
        public bool IsConnected { get; set; } = true;
        public string ConnectionStatus => "";
        public string ConnectedHost => "";
        public string ConnectedTargetId => "";
        public bool IsPreviewScreencastActive { get; set; } = false;

        public string? LastMethodCalled { get; private set; }
        public JsonObject? LastParamsCalled { get; private set; }
        public JsonObject ResponseToReturn { get; set; } = new JsonObject();

#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;
#pragma warning restore CS0067

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            LastMethodCalled = method;
            LastParamsCalled = parameters;
            return Task.FromResult(ResponseToReturn);
        }
    }

    [AvaloniaFact]
    public void ScratchDomNodeViewModel_DefaultsAndCapture()
    {
        var cdp = new DummyCdpService();
        var node = new ScratchDomNodeViewModel(cdp);

        Assert.Equal("", node.RawJsonData);
        Assert.Null(node.Timestamp);
        Assert.False(node.IsCapturing);
        Assert.Equal("Empty", node.DataSummary);

        cdp.ResponseToReturn = new JsonObject { ["root"] = new JsonObject { ["nodeId"] = 1 } };

        node.CaptureCommand.Execute(null);

        Assert.Equal("DOM.getDocument", cdp.LastMethodCalled);
        Assert.NotNull(cdp.LastParamsCalled);
        Assert.Equal(-1, (int?)cdp.LastParamsCalled["depth"]);
        Assert.True((bool?)cdp.LastParamsCalled["pierce"]);
        Assert.Contains("root", node.RawJsonData);
        Assert.NotNull(node.Timestamp);
        Assert.Equal("1 elements", node.DataSummary);
    }

    [AvaloniaFact]
    public void ScratchNetworkNodeViewModel_DefaultsAndPropertyChanges()
    {
        var node = new ScratchNetworkNodeViewModel();

        Assert.Equal("", node.NetworkRequestsJson);
        Assert.Equal(0, node.TotalRequests);
        Assert.Equal(0, node.FailedRequests);
        Assert.False(node.HasFailedRequests);
        Assert.Equal("", node.OutputJson);
        Assert.Null(node.OutputJsonNode);

        bool jsonChanged = false;
        bool totalChanged = false;
        bool failedChanged = false;
        bool hasFailedChanged = false;

        node.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ScratchNetworkNodeViewModel.NetworkRequestsJson)) jsonChanged = true;
            if (e.PropertyName == nameof(ScratchNetworkNodeViewModel.TotalRequests)) totalChanged = true;
            if (e.PropertyName == nameof(ScratchNetworkNodeViewModel.FailedRequests)) failedChanged = true;
            if (e.PropertyName == nameof(ScratchNetworkNodeViewModel.HasFailedRequests)) hasFailedChanged = true;
        };

        node.NetworkRequestsJson = "{\"test\": true}";
        node.TotalRequests = 5;
        node.FailedRequests = 2;

        Assert.True(jsonChanged);
        Assert.True(totalChanged);
        Assert.True(failedChanged);
        Assert.True(hasFailedChanged);

        Assert.Equal("{\"test\": true}", node.NetworkRequestsJson);
        Assert.Equal(5, node.TotalRequests);
        Assert.Equal(2, node.FailedRequests);
        Assert.True(node.HasFailedRequests);
        Assert.Equal("{\"test\": true}", node.OutputJson);
        Assert.NotNull(node.OutputJsonNode);
        Assert.Equal(true, (bool?)node.OutputJsonNode!["test"]);
    }

    [AvaloniaFact]
    public void ScratchDiffNodeViewModel_AutoDiffOnConnections()
    {
        var editor = new ScratchViewModel();
        
        var node1 = new ScratchDomNodeViewModel { Name = "Node1", RawJsonData = "{\n  \"a\": 1\n}" };
        var node2 = new ScratchDomNodeViewModel { Name = "Node2", RawJsonData = "{\n  \"a\": 2\n}" };
        var diffNode = new ScratchDiffNodeViewModel { Name = "Diff" };

        editor.Nodes.Add(node1);
        editor.Nodes.Add(node2);
        editor.Nodes.Add(diffNode);

        // No connections yet
        editor.UpdateAllDiffNodes();
        Assert.Equal("Left (No Input)", diffNode.LeftTitle);
        Assert.Equal("Right (No Input)", diffNode.RightTitle);
        Assert.Empty(diffNode.Diff.DiffLines);

        // Add first connection
        var conn1 = new CDP.Editor.Nodes.ViewModels.ConnectionViewModel(node1, diffNode);
        editor.Connections.Add(conn1);

        Assert.Equal("Node1 (DOM)", diffNode.LeftTitle);
        Assert.Equal("Right (No Input)", diffNode.RightTitle);

        // Add second connection
        var conn2 = new CDP.Editor.Nodes.ViewModels.ConnectionViewModel(node2, diffNode);
        editor.Connections.Add(conn2);

        Assert.Equal("Node1 (DOM)", diffNode.LeftTitle);
        Assert.Equal("Node2 (DOM)", diffNode.RightTitle);
        Assert.NotEmpty(diffNode.Diff.DiffLines);

        // Update data on node2 and verify diff updates
        node2.RawJsonData = "{\n  \"a\": 1\n}";
        
        // Assert all diff lines are Unchanged type
        Assert.All(diffNode.Diff.DiffLines, line => Assert.Equal(DiffType.Unchanged, line.Type));
    }

    [AvaloniaFact]
    public void ScratchViewModel_SaveAndLoadState()
    {
        var editor = new ScratchViewModel();

        var node1 = new ScratchPerformanceNodeViewModel
        {
            Id = "node-1",
            Name = "Data 1",
            X = 50,
            Y = 100,
            RawJsonData = "{\"metric\": 42}"
        };

        var node2 = new ScratchDiffNodeViewModel
        {
            Id = "node-2",
            Name = "Diff 1",
            X = 250,
            Y = 100,
            LeftNodeId = "node-1"
        };

        editor.Nodes.Add(node1);
        editor.Nodes.Add(node2);

        var conn = new CDP.Editor.Nodes.ViewModels.ConnectionViewModel(node1, node2);
        editor.Connections.Add(conn);

        var state = editor.SaveState();
        Assert.NotNull(state);

        var newEditor = new ScratchViewModel();
        newEditor.LoadState(state);

        Assert.Equal(2, newEditor.Nodes.Count);
        Assert.Single(newEditor.Connections);

        var dataNode = newEditor.Nodes.OfType<ScratchPerformanceNodeViewModel>().FirstOrDefault(n => n.Id == "node-1");
        Assert.NotNull(dataNode);
        Assert.Equal("Data 1", dataNode.Name);
        Assert.Equal(50, dataNode.X);
        Assert.Equal(100, dataNode.Y);
        Assert.Equal("{\"metric\": 42}", dataNode.RawJsonData);

        var diffNode = newEditor.Nodes.OfType<ScratchDiffNodeViewModel>().FirstOrDefault(n => n.Id == "node-2");
        Assert.NotNull(diffNode);
        Assert.Equal("Diff 1", diffNode.Name);
        Assert.Equal("node-1", diffNode.LeftNodeId);
        Assert.Equal("Data 1 (Performance)", diffNode.LeftTitle);
    }

    [AvaloniaFact]
    public void Test_ScratchAssertionNode_PathTraversal()
    {
        var jsonText = "{\"root\": {\"children\": [{\"nodeName\": \"div\", \"attributes\": [\"id\", \"btn\"]}]}}";
        var node = JsonNode.Parse(jsonText);

        var val1 = ScratchAssertionNodeViewModel.ResolvePath(node, "root.children[0].nodeName");
        Assert.NotNull(val1);
        Assert.Equal("div", val1.ToString());

        var val2 = ScratchAssertionNodeViewModel.ResolvePath(node, "root.children[0].attributes[1]");
        Assert.NotNull(val2);
        Assert.Equal("btn", val2.ToString());

        var val3 = ScratchAssertionNodeViewModel.ResolvePath(node, "root.children[1].nodeName");
        Assert.Null(val3);

        var val4 = ScratchAssertionNodeViewModel.ResolvePath(node, "root.nonexistent");
        Assert.Null(val4);
    }

    [AvaloniaFact]
    public void Test_ScratchAssertionNode_Operators()
    {
        var inputNode = new ScratchDomNodeViewModel { RawJsonData = "{\"value\": 42, \"text\": \"hello world\", \"exists\": true}" };
        var assertNode = new ScratchAssertionNodeViewModel
        {
            InputNode = inputNode,
            Path = "value",
            Operator = AssertionOperator.Equals,
            ExpectedValue = "42"
        };

        // Equals
        Assert.True(assertNode.Passed);

        // NotEquals
        assertNode.Operator = AssertionOperator.NotEquals;
        assertNode.ExpectedValue = "43";
        Assert.True(assertNode.Passed);

        // Contains
        assertNode.Path = "text";
        assertNode.Operator = AssertionOperator.Contains;
        assertNode.ExpectedValue = "hello";
        Assert.True(assertNode.Passed);

        // GreaterThan
        assertNode.Path = "value";
        assertNode.Operator = AssertionOperator.GreaterThan;
        assertNode.ExpectedValue = "40";
        Assert.True(assertNode.Passed);

        // LessThan
        assertNode.Operator = AssertionOperator.LessThan;
        assertNode.ExpectedValue = "50";
        Assert.True(assertNode.Passed);

        // Exists
        assertNode.Path = "exists";
        assertNode.Operator = AssertionOperator.Exists;
        Assert.True(assertNode.Passed);

        // NotExists
        assertNode.Path = "nonexistent";
        assertNode.Operator = AssertionOperator.NotExists;
        Assert.True(assertNode.Passed);
    }

    private class DummyTimeMachineService : ITimeMachineService
    {
        public bool IsRecording { get; set; }
        public bool IsReplaying { get; set; }
        public int CurrentFrameIndex { get; set; }
        public List<TimeMachineFrame> FramesList { get; } = new List<TimeMachineFrame>();
        public IReadOnlyList<TimeMachineFrame> Frames => FramesList;

        public event EventHandler? FrameChanged;
        public event EventHandler? ReplayStateCleared;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void StartRecording() {}
        public void StopRecording() {}
        public void Clear() {}
        public void Play() {}
        public void Pause() {}
        public void StepForward() {}
        public void StepBackward() {}
        public void Seek(int index)
        {
            CurrentFrameIndex = index;
            FrameChanged?.Invoke(this, EventArgs.Empty);
        }
        public void RecordEvent(string method, JsonObject? parameters) {}
        public void RecordResponse(string method, JsonObject? parameters, JsonObject? result) {}
        public JsonObject? GetReplayResponse(string method, JsonObject? parameters) => null;
        public JsonObject? GetReplayResponseAtFrame(string method, JsonObject? parameters, int frameIndex) => null;
        public JsonNode SaveState() => new JsonObject();
        public void LoadState(JsonNode state) {}
    }

    private class DummyCdpServiceWithTimeMachine : ICdpService
    {
        public bool IsConnected { get; set; } = true;
        public string ConnectionStatus => "";
        public string ConnectedHost => "";
        public string ConnectedTargetId => "";
        public bool IsPreviewScreencastActive { get; set; }
        public ITimeMachineService TimeMachine { get; } = new DummyTimeMachineService();
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null) => Task.FromResult(new JsonObject());
    }

    [AvaloniaFact]
    public void Test_ScratchTimeMachineNode_Pinning()
    {
        var cdp = new DummyCdpServiceWithTimeMachine();
        var tm = (DummyTimeMachineService)cdp.TimeMachine;
        tm.FramesList.Add(new TimeMachineFrame { Index = 0, Payload = new JsonObject { ["val"] = "frame0" } });
        tm.FramesList.Add(new TimeMachineFrame { Index = 1, Payload = new JsonObject { ["val"] = "frame1" } });

        var tmNode = new ScratchTimeMachineNodeViewModel(cdp);

        // Initially follows time machine
        tm.Seek(0);
        Assert.Contains("frame0", tmNode.SelectedFramePayloadText);

        tm.Seek(1);
        Assert.Contains("frame1", tmNode.SelectedFramePayloadText);

        // Pin to frame 0
        tmNode.IsPinned = true;
        tmNode.PinnedFrameIndex = 0;
        Assert.Contains("frame0", tmNode.SelectedFramePayloadText);

        // Time machine moves to frame 1, but node stays pinned to frame 0
        tm.Seek(1);
        Assert.Contains("frame0", tmNode.SelectedFramePayloadText);

        // Unpin, returns to live synchronization (frame 1)
        tmNode.IsPinned = false;
        Assert.Contains("frame1", tmNode.SelectedFramePayloadText);
    }

    [AvaloniaFact]
    public void Test_ScratchViewModel_AssertionAndPinningSerialization()
    {
        var editor = new ScratchViewModel();
        var assertNode = new ScratchAssertionNodeViewModel
        {
            Id = "assert-1",
            Name = "Assertion 1",
            Path = "value.x",
            ExpectedValue = "ok",
            Operator = AssertionOperator.Contains
        };
        editor.Nodes.Add(assertNode);

        var state = editor.SaveState();
        Assert.NotNull(state);

        var newEditor = new ScratchViewModel();
        newEditor.LoadState(state);

        var loaded = newEditor.Nodes.OfType<ScratchAssertionNodeViewModel>().FirstOrDefault();
        Assert.NotNull(loaded);
        Assert.Equal("assert-1", loaded.Id);
        Assert.Equal("value.x", loaded.Path);
        Assert.Equal("ok", loaded.ExpectedValue);
        Assert.Equal(AssertionOperator.Contains, loaded.Operator);
    }

    [AvaloniaFact]
    public void Test_ScratchPageNode_TimeMachineSync()
    {
        var cdp = new DummyCdpServiceWithTimeMachine();
        var tm = (DummyTimeMachineService)cdp.TimeMachine;

        // 1x1 black pixel PNG base64
        string base64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
        tm.FramesList.Add(new TimeMachineFrame
        {
            Index = 0,
            Domain = "Page",
            Method = "Page.screencastFrame",
            Params = new JsonObject { ["data"] = base64Png }
        });

        var pageNode = new ScratchPageNodeViewModel(cdp);
        pageNode.IsSyncedWithTimeMachine = true;

        tm.Seek(0);

        // Wait a small moment for Dispatcher.UIThread to process the Post callback
        System.Threading.Thread.Sleep(50);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(base64Png, pageNode.ScreenshotBase64);
        Assert.NotNull(pageNode.ScreenshotImage);
    }

    [AvaloniaFact]
    public void Test_ScratchImageDiffNode_ComparisonAndCycle()
    {
        var leftNode = new ScratchPageNodeViewModel();
        var rightNode = new ScratchPageNodeViewModel();
        var diffNode = new ScratchImageDiffNodeViewModel();

        string base64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
        try
        {
            byte[] bytes = Convert.FromBase64String(base64Png);
            using var ms = new System.IO.MemoryStream(bytes);
            using var skBmp = SkiaSharp.SKBitmap.Decode(ms);
            Assert.NotNull(skBmp);
        }
        catch (Exception ex)
        {
            Assert.True(false, $"SkiaSharp decode failed: {ex.GetType().FullName}: {ex.Message}");
        }

        leftNode.ScreenshotBase64 = base64Png;
        Assert.Null(leftNode.LastDecodeException);
        Assert.NotNull(leftNode.ScreenshotImage);

        rightNode.ScreenshotBase64 = base64Png;
        Assert.Null(rightNode.LastDecodeException);
        Assert.NotNull(rightNode.ScreenshotImage);

        var connections = new List<CDP.Editor.Nodes.ViewModels.ConnectionViewModel>
        {
            new CDP.Editor.Nodes.ViewModels.ConnectionViewModel(leftNode, diffNode),
            new CDP.Editor.Nodes.ViewModels.ConnectionViewModel(rightNode, diffNode)
        };

        diffNode.LeftNodeId = leftNode.Id;
        diffNode.RightNodeId = rightNode.Id;

        // Perform diff
        diffNode.UpdateDiff(id => id == leftNode.Id ? leftNode : rightNode, connections);

        Assert.Null(diffNode.LastDiffException);
        Assert.NotNull(diffNode.DiffImage);
        Assert.Equal(0.0, diffNode.DiffPercentage);

        // Test cycle detection
        var cyclicDiffNode = new ScratchImageDiffNodeViewModel();
        var cycleConn1 = new CDP.Editor.Nodes.ViewModels.ConnectionViewModel(diffNode, cyclicDiffNode);
        var cycleConn2 = new CDP.Editor.Nodes.ViewModels.ConnectionViewModel(cyclicDiffNode, diffNode);
        var cycleConns = new List<CDP.Editor.Nodes.ViewModels.ConnectionViewModel> { cycleConn1, cycleConn2 };

        diffNode.LeftNodeId = cyclicDiffNode.Id;
        cyclicDiffNode.LeftNodeId = diffNode.Id;

        // Link diffNode -> cyclicDiffNode (succeeds since cyclicDiffNode has no left node set yet)
        diffNode.UpdateDiff(id => id == cyclicDiffNode.Id ? cyclicDiffNode : null, cycleConns);
        Assert.Equal(cyclicDiffNode, diffNode.LeftNode);

        // Link cyclicDiffNode -> diffNode (fails because it creates a cycle: cyclicDiffNode -> diffNode -> cyclicDiffNode)
        cyclicDiffNode.UpdateDiff(id => id == diffNode.Id ? diffNode : null, cycleConns);
        Assert.Null(cyclicDiffNode.LeftNode);
    }

    [AvaloniaFact]
    public void Test_ScratchMvvmNode_ParsingAndConnection()
    {
        var inputNode = new ScratchDomNodeViewModel
        {
            RawJsonData = @"{
                ""type"": ""CdpInspectorApp.ViewModels.MainWindowViewModel"",
                ""controlType"": ""CdpInspectorApp.Views.MainWindow"",
                ""controlName"": ""mainWindow"",
                ""properties"": [
                    {""name"": ""IsConnected"", ""type"": ""System.Boolean"", ""value"": true}
                ],
                ""children"": [
                    {
                        ""type"": ""CdpInspectorApp.ViewModels.RecorderViewModel"",
                        ""controlType"": ""CdpInspectorApp.Views.RecorderView"",
                        ""properties"": []
                    }
                ]
            }"
        };

        var mvvmNode = new ScratchMvvmNodeViewModel
        {
            InputNode = inputNode
        };

        Assert.Equal("CdpInspectorApp.ViewModels.MainWindowViewModel", mvvmNode.CurrentVmType);
        Assert.Equal("MainWindowViewModel", mvvmNode.ShortVmType);
        Assert.Equal(1, mvvmNode.PropertiesCount);
        Assert.Contains("CdpInspectorApp.ViewModels.RecorderViewModel", mvvmNode.MvvmHierarchyText);
    }

    [Fact]
    public void ScratchViewModel_AddScratchNodeCommand_WithPositionOverrides()
    {
        var vm = new ScratchViewModel();
        Assert.Empty(vm.Nodes);

        var parameters = new ScratchViewModel.AddNodeParameters("DOM", 150.0, 320.0);
        vm.AddScratchNodeCommand.Execute(parameters);

        Assert.Single(vm.Nodes);
        var addedNode = vm.Nodes.Single();
        Assert.IsType<ScratchDomNodeViewModel>(addedNode);
        Assert.Equal(150.0, addedNode.X);
        Assert.Equal(320.0, addedNode.Y);
    }

    [Fact]
    public void Test_ScratchNode_TreeLinking()
    {
        var domNode = new ScratchDomNodeViewModel();
        var axNode = new ScratchAccessibilityNodeViewModel();
        var mvvmNode = new ScratchMvvmNodeViewModel();

        domNode.LinkedElementId = "42";
        domNode.LinkedElementName = "Button #btnClickMe";
        Assert.True(domNode.IsLinked);
        Assert.Equal("42", domNode.LinkedElementId);
        Assert.Equal("Button #btnClickMe", domNode.LinkedElementName);

        axNode.LinkedElementId = "ax-7";
        axNode.LinkedElementName = "CheckBox Option";
        Assert.True(axNode.IsLinked);
        Assert.Equal("ax-7", axNode.LinkedElementId);

        mvvmNode.LinkedElementId = "vm-12";
        mvvmNode.LinkedElementName = "MainViewModel";
        Assert.True(mvvmNode.IsLinked);
        Assert.Equal("vm-12", mvvmNode.LinkedElementId);
    }

    [Fact]
    public void Test_ScratchNode_PinConnections()
    {
        var editor = new ScratchViewModel();
        var left = new ScratchDomNodeViewModel { RawJsonData = "{\"value\": 10}" };
        var right = new ScratchDomNodeViewModel { RawJsonData = "{\"value\": 20}" };
        var diff = new ScratchDiffNodeViewModel();

        editor.Nodes.Add(left);
        editor.Nodes.Add(right);
        editor.Nodes.Add(diff);

        Assert.Single(left.Outputs);
        Assert.Equal("dom", left.Outputs[0].Id);

        Assert.Equal(2, diff.Inputs.Count);
        Assert.Equal("left", diff.Inputs[0].Id);
        Assert.Equal("right", diff.Inputs[1].Id);

        editor.ConnectPins(left.Outputs[0], diff.Inputs[0]);
        editor.ConnectPins(right.Outputs[0], diff.Inputs[1]);

        Assert.Equal(2, editor.Connections.Count);

        editor.PropagateNodeUpdate(left);
        editor.PropagateNodeUpdate(right);

        Assert.Equal(left, diff.LeftNode);
        Assert.Equal(right, diff.RightNode);
    }
}


