#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using Chrome.DevTools.Protocol;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class TestStudioNodeEditorTests
{
    private class DummyCdpService : ICdpService
    {
        public bool IsConnected { get; set; } = true;
        public string ConnectionStatus => "";
        public string ConnectedHost => "";
        public string ConnectedTargetId => "";
        public bool IsPreviewScreencastActive { get; set; } = false;

#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;
#pragma warning restore CS0067

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null) => Task.FromResult(new JsonObject());
    }

    [Fact]
    public void TestStudioNodeViewModel_DefaultsAndSuggestions()
    {
        var node = new TestStudioNodeViewModel();

        Assert.NotNull(node.Id);
        Assert.NotEmpty(node.Id);
        Assert.Equal("", node.Name);
        Assert.Equal("", node.Action);
        Assert.Equal("", node.Selector);
        Assert.Equal("", node.Value);
        Assert.Equal(160, node.Width);
        Assert.Equal(100, node.Height);
        Assert.False(node.IsSelected);

        Assert.NotEmpty(node.CommandSuggestions);
        Assert.NotEmpty(node.ValueSuggestions);
        Assert.NotNull(node.SelectorSuggestions);
    }

    [Fact]
    public void TestStudioConnectionViewModel_DynamicBezierPoints()
    {
        var node1 = new TestStudioNodeViewModel { X = 100, Y = 200, Width = 160, Height = 100 };
        var node2 = new TestStudioNodeViewModel { X = 400, Y = 300, Width = 160, Height = 100 };

        var connection = new TestStudioConnectionViewModel(node1, node2);

        // Calculate expected points:
        // StartPoint = (node1.X + node1.Width, node1.Y + node1.Height / 2) -> (260, 250)
        // EndPoint = (node2.X, node2.Y + node2.Height / 2) -> (400, 350)
        // ControlPoint1 = (StartPoint.X + 80, StartPoint.Y) -> (340, 250)
        // ControlPoint2 = (EndPoint.X - 80, EndPoint.Y) -> (320, 350)

        Assert.Equal(260, connection.StartPoint.X);
        Assert.Equal(250, connection.StartPoint.Y);
        Assert.Equal(400, connection.EndPoint.X);
        Assert.Equal(350, connection.EndPoint.Y);
        Assert.Equal(340, connection.ControlPoint1.X);
        Assert.Equal(250, connection.ControlPoint1.Y);
        Assert.Equal(320, connection.ControlPoint2.X);
        Assert.Equal(350, connection.ControlPoint2.Y);

        // Move node1 and assert points update
        bool startPointChanged = false;
        connection.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(connection.StartPoint))
            {
                startPointChanged = true;
            }
        };

        node1.X = 150;
        Assert.True(startPointChanged);
        Assert.Equal(310, connection.StartPoint.X);
        Assert.Equal(390, connection.ControlPoint1.X);
    }

    [Fact]
    public void TestStudioNodeEditorViewModel_NodeAndConnectionActions()
    {
        var editor = new TestStudioNodeEditorViewModel();

        var node1 = editor.CreateNode("Node1", "click", "#btn1", "", 10, 20);
        var node2 = editor.CreateNode("Node2", "click", "#btn2", "", 150, 20);

        Assert.Equal(2, editor.Nodes.Count);
        Assert.Equal("Node1", editor.Nodes[0].Name);

        editor.ConnectNodes(node1, node2);
        Assert.Single(editor.Connections);
        Assert.Same(node1, editor.Connections[0].FromNode);
        Assert.Same(node2, editor.Connections[0].ToNode);

        // Select and delete
        editor.SelectNode(node1);
        Assert.True(node1.IsSelected);
        Assert.False(node2.IsSelected);

        editor.DeleteSelectedCommand.Execute(null);

        Assert.Single(editor.Nodes);
        Assert.Same(node2, editor.Nodes[0]);
        // Connection should be deleted when node1 was deleted
        Assert.Empty(editor.Connections);
    }

    [Fact]
    public void TestStudioNodeEditorViewModel_SequentialCompileAndLoopProtection()
    {
        var editor = new TestStudioNodeEditorViewModel();

        var node1 = editor.CreateNode("Step1", "click", "#btn1", "", 0, 0);
        var node2 = editor.CreateNode("Step2", "inputText", "#txt1", "Hello", 0, 0);

        // Chain them: node1 -> node2
        editor.ConnectNodes(node1, node2);

        editor.SyncSteps();

        // Compile should include both sequentially
        Assert.Equal(2, editor.CompiledSteps.Count);
        Assert.Equal("click", editor.CompiledSteps[0].Action);
        Assert.Equal("#btn1", editor.CompiledSteps[0].Selector);
        Assert.Equal("inputText", editor.CompiledSteps[1].Action);
        Assert.Equal("Hello", editor.CompiledSteps[1].Value);

        // Create a circular dependency: node2 -> node1
        editor.ConnectNodes(node2, node1);

        // Compile should terminate due to loop protection without throwing StackOverflowException
        editor.SyncSteps();
        Assert.Equal(2, editor.CompiledSteps.Count);
    }

    [Fact]
    public void TestStudioNodeEditorViewModel_AutoLayout()
    {
        var editor = new TestStudioNodeEditorViewModel();

        var node1 = editor.CreateNode("Node1", "click", "", "", 0, 0);
        var node2 = editor.CreateNode("Node2", "click", "", "", 0, 0);
        var node3 = editor.CreateNode("Node3", "click", "", "", 0, 0);

        editor.ConnectNodes(node1, node2);
        // node3 is disconnected

        editor.AutoLayoutCommand.Execute(null);

        // Expected X coordinates based on auto layout:
        // i = 0 (node1): 200 * 0 + 10 = 10
        // i = 1 (node2): 200 * 1 + 10 = 210
        // i = 2 (node3): 200 * 2 + 10 = 410
        // Y coordinates should be 20
        Assert.Equal(10, node1.X);
        Assert.Equal(20, node1.Y);

        Assert.Equal(210, node2.X);
        Assert.Equal(20, node2.Y);

        Assert.Equal(410, node3.X);
        Assert.Equal(20, node3.Y);
    }

    [Fact]
    public void TestStudioViewModel_ImportYamlSyncsToNodeEditor()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());

        // Define a simple YAML flow with two steps
        string yaml = @"appId: ""http://localhost:9222""
description: ""Test Flow""
---
- tapOn: ""#btn1""
- inputText:
    selector: ""#txt1""
    text: ""Hello World""";

        vm.YamlCode = yaml;
        vm.ApplyYaml();

        // Check VM Steps list
        Assert.Equal(2, vm.Steps.Count);
        Assert.Equal("tapOn", vm.Steps[0].Action);
        Assert.Equal("#btn1", vm.Steps[0].Selector);
        Assert.Equal("inputText", vm.Steps[1].Action);
        Assert.Equal("#txt1", vm.Steps[1].Selector);
        Assert.Equal("Hello World", vm.Steps[1].Value);

        // Check NodeEditor synchronization
        // It should have: 2 step nodes total
        Assert.Equal(2, vm.NodeEditor.Nodes.Count);

        var node1 = vm.NodeEditor.Nodes.OfType<TestStudioNodeViewModel>().FirstOrDefault(n => n.Action == "tapOn");
        Assert.NotNull(node1);
        Assert.Equal("#btn1", node1.Selector);

        var node2 = vm.NodeEditor.Nodes.OfType<TestStudioNodeViewModel>().FirstOrDefault(n => n.Action == "inputText");
        Assert.NotNull(node2);
        Assert.Equal("#txt1", node2.Selector);
        Assert.Equal("Hello World", node2.Value);

        // Check horizontal chain connections: Node1 -> Node2
        Assert.Single(vm.NodeEditor.Connections);

        var conn = vm.NodeEditor.Connections.FirstOrDefault(c => c.FromNode == node1);
        Assert.NotNull(conn);
        Assert.Same(node2, conn.ToNode);

        // Verify layout coordinates are set/aligned by AutoLayout
        Assert.Equal(10.0, node1.X);
        Assert.Equal(20.0, node1.Y);
        Assert.Equal(210.0, node2.X);
        Assert.Equal(20.0, node2.Y);
    }

    [Fact]
    public void TestStudioNodeEditorViewModel_ModifyNodePropertiesUpdatesCompiledStepsImmediately()
    {
        var editor = new TestStudioNodeEditorViewModel();

        var node1 = editor.CreateNode("Step1", "click", "#btn1", "", 0, 0);

        editor.SyncSteps();

        // Immediate compilation check when node created
        Assert.Single(editor.CompiledSteps);
        Assert.Equal("click", editor.CompiledSteps[0].Action);
        Assert.Equal("#btn1", editor.CompiledSteps[0].Selector);

        // Modify node1 properties
        node1.Action = "inputText";
        Assert.Equal("inputText", editor.CompiledSteps[0].Action);

        node1.Selector = "#inputField";
        Assert.Equal("#inputField", editor.CompiledSteps[0].Selector);

        node1.Value = "new value";
        Assert.Equal("new value", editor.CompiledSteps[0].Value);
    }

    [Fact]
    public void TestStudioNodeEditorViewModel_CompileUnconnectedNodesGracefully()
    {
        var editor = new TestStudioNodeEditorViewModel();

        var node1 = editor.CreateNode("Step1", "click", "#btn1", "", 0, 0);
        var node2 = editor.CreateNode("Step2", "tapOn", "#btn2", "", 0, 0);

        editor.SyncSteps();
        
        // Since node1 and node2 are unconnected, node1 is resolved as start (no incoming)
        // compile starting from node1 should yield only node1
        Assert.Single(editor.CompiledSteps);
        Assert.Equal("click", editor.CompiledSteps[0].Action);

        // Connect node1 -> node2
        editor.ConnectNodes(node1, node2);
        editor.SyncSteps();

        // Node1 and node2 should be compiled sequentially
        Assert.Equal(2, editor.CompiledSteps.Count);
        Assert.Equal("click", editor.CompiledSteps[0].Action);
        Assert.Equal("tapOn", editor.CompiledSteps[1].Action);
    }

    [Fact]
    public void TestStudioViewModel_BidirectionalSyncAndSelection_Flow()
    {
        var vm = new TestStudioViewModel(new DummyCdpService());

        // 1. Initial State
        Assert.Empty(vm.NodeEditor.Nodes); // Empty initial nodes

        // 2. Add step to Steps -> check node created in NodeEditor automatically
        var step1 = new TestStudioStepModel { Action = "tapOn", Selector = "#btn1" };
        vm.Steps.Add(step1);

        Assert.Single(vm.NodeEditor.Nodes);
        var stepNode1 = vm.NodeEditor.Nodes.OfType<TestStudioNodeViewModel>().FirstOrDefault(n => n.Step == step1);
        Assert.NotNull(stepNode1);
        Assert.Equal("tapOn", stepNode1!.Action);

        // 3. Selection sync: Steps -> NodeEditor
        vm.SelectedStep = step1;
        Assert.True(stepNode1.IsSelected);
        Assert.Same(stepNode1, vm.SelectedStepNode);

        // 4. Selection sync: NodeEditor -> Steps
        var step2 = new TestStudioStepModel { Action = "inputText", Selector = "#txt1", Value = "hello" };
        vm.Steps.Add(step2);

        var stepNode2 = vm.NodeEditor.Nodes.OfType<TestStudioNodeViewModel>().FirstOrDefault(n => n.Step == step2);
        Assert.NotNull(stepNode2);

        vm.NodeEditor.SelectNode(stepNode2!);
        Assert.True(stepNode2!.IsSelected);
        Assert.False(stepNode1.IsSelected);
        Assert.Same(step2, vm.SelectedStep);
        Assert.Same(stepNode2, vm.SelectedStepNode);

        // 5. Node Editor property changes updates steps list automatically in real time
        stepNode2.Value = "world";
        Assert.Equal("world", step2.Value);
    }

    [Fact]
    public void TestStudioNodeViewModel_StepStatusPropagation_Flow()
    {
        var step = new TestStudioStepModel { Action = "tapOn", Selector = "#btn1" };
        var node = new TestStudioNodeViewModel { Step = step };

        Assert.Equal(StepStatus.Pending, node.Status);
        Assert.False(node.IsRunning);
        Assert.False(node.IsPassed);
        Assert.False(node.IsFailed);

        step.Status = StepStatus.Running;
        Assert.Equal(StepStatus.Running, node.Status);
        Assert.True(node.IsRunning);
        Assert.False(node.IsPassed);
        Assert.False(node.IsFailed);

        step.Status = StepStatus.Passed;
        Assert.Equal(StepStatus.Passed, node.Status);
        Assert.False(node.IsRunning);
        Assert.True(node.IsPassed);
        Assert.False(node.IsFailed);

        step.Status = StepStatus.Failed;
        Assert.Equal(StepStatus.Failed, node.Status);
        Assert.False(node.IsRunning);
        Assert.False(node.IsPassed);
        Assert.True(node.IsFailed);
    }

    [Fact]
    public void TestStudioNodeEditorViewModel_CopyPasteClonesFullStepData()
    {
        var editor = new TestStudioNodeEditorViewModel();

        var originalStep = new TestStudioStepModel
        {
            Action = "tapOn",
            Selector = "#btn1",
            Value = "val1",
            WhileConditionType = "visible",
            WhileConditionValue = "#btn2"
        };
        originalStep.Parameters["testParam"] = "paramVal";

        var node = editor.CreateNode("Step1", "tapOn", "#btn1", "val1", 10, 20);
        node.Step = originalStep;

        // Select node
        editor.SelectNode(node);

        // Copy and paste
        editor.CopySelectedNodes();
        editor.PasteNodes();

        // Check that pasted node exists and has a step cloned with parameters and condition
        Assert.Equal(2, editor.Nodes.Count);
        var pastedNode = editor.Nodes.OfType<TestStudioNodeViewModel>().FirstOrDefault(n => n != node);
        Assert.NotNull(pastedNode);
        Assert.NotNull(pastedNode.Step);
        Assert.NotSame(originalStep, pastedNode.Step); // should be a deep clone

        Assert.Equal("tapOn", pastedNode.Step.Action);
        Assert.Equal("#btn1", pastedNode.Step.Selector);
        Assert.Equal("val1", pastedNode.Step.Value);
        Assert.Equal("visible", pastedNode.Step.WhileConditionType);
        Assert.Equal("#btn2", pastedNode.Step.WhileConditionValue);
        Assert.True(pastedNode.Step.Parameters.ContainsKey("testParam"));
        Assert.Equal("paramVal", pastedNode.Step.Parameters["testParam"]);
    }
}
