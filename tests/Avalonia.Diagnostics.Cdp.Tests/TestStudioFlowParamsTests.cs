using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Services;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class TestStudioFlowParamsTests
{
    private class MockCdpService : ICdpService
    {
        public bool IsConnected => true;
        public string ConnectionStatus => "Connected";
        public string ConnectedHost => "http://127.0.0.1:9222";
        public string ConnectedTargetId => "test_target";
        public bool IsPreviewScreencastActive { get; set; }

        public List<(string Method, JsonObject? Params)> SentCommands { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            SentCommands.Add((method, parameters));
            if (method == "DOM.getDocument")
            {
                var root = new JsonObject
                {
                    ["nodeId"] = 1,
                    ["documentURL"] = "http://127.0.0.1:9222",
                    ["baseURL"] = "http://127.0.0.1:9222"
                };
                var res = new JsonObject { ["root"] = root };
                return Task.FromResult(res);
            }
            if (method == "DOM.querySelector")
            {
                return Task.FromResult(new JsonObject { ["nodeId"] = 42 });
            }
            return Task.FromResult(new JsonObject());
        }
    }

    [Fact]
    public void TestStepInterpolation()
    {
        var cdp = new MockCdpService();
        var vm = new TestStudioViewModel(cdp);

        var originalStep = new TestStudioStepModel
        {
            Action = "inputText",
            Selector = "#txt-${USER_ROLE}",
            Value = "hello-${USER_NAME}",
            WhileConditionType = "condition-${COND}",
            WhileConditionValue = "value-${VAL}",
            Parameters = new Dictionary<string, object?>
            {
                { "text", "Greetings ${USER_NAME}!" },
                { "dictParam", new Dictionary<string, object?> { { "key", "${NESTED_VAR}" } } },
                { "listParam", new List<object?> { "item-${VAL}", 123 } }
            }
        };

        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "USER_ROLE", "admin" },
            { "USER_NAME", "Alice" },
            { "COND", "visible" },
            { "VAL", "true" },
            { "NESTED_VAR", "nested_val" }
        };

        var methodInfo = typeof(TestStudioViewModel).GetMethod("InterpolateStep", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(methodInfo);
        
        var invokeRes = methodInfo.Invoke(vm, new object[] { originalStep, env });
        Assert.NotNull(invokeRes);
        var cloned = (TestStudioStepModel)invokeRes;
        
        Assert.Equal("inputText", cloned.Action);
        Assert.Equal("#txt-admin", cloned.Selector);
        Assert.Equal("hello-Alice", cloned.Value);
        Assert.Equal("condition-visible", cloned.WhileConditionType);
        Assert.Equal("value-true", cloned.WhileConditionValue);
        
        Assert.Equal("Greetings Alice!", cloned.Parameters["text"]);
        
        var dictParam = (Dictionary<string, object?>?)cloned.Parameters["dictParam"];
        Assert.NotNull(dictParam);
        Assert.Equal("nested_val", dictParam["key"]);
        
        var listParam = (List<object?>?)cloned.Parameters["listParam"];
        Assert.NotNull(listParam);
        Assert.Equal("item-true", listParam[0]);
        Assert.Equal(123, listParam[1]);

        // Original step must NOT be modified
        Assert.Equal("#txt-${USER_ROLE}", originalStep.Selector);
        Assert.Equal("hello-${USER_NAME}", originalStep.Value);
        Assert.Equal("Greetings ${USER_NAME}!", originalStep.Parameters["text"]);
    }

    [Fact]
    public async Task TestPathResolutionAndSubflowExecution()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        var subDir = Path.Combine(tempDir, "flows");
        Directory.CreateDirectory(subDir);

        var wsFile = Path.Combine(tempDir, "nested_ws.yaml");
        var relFile = Path.Combine(subDir, "nested_rel.yaml");
        var mainFile = Path.Combine(subDir, "main.yaml");

        await File.WriteAllTextAsync(wsFile, @"
- delay: 100
");
        await File.WriteAllTextAsync(relFile, @"
- delay: 200
");
        await File.WriteAllTextAsync(mainFile, @"
- runFlow: ""nested_rel.yaml""
- runFlow: ""nested_ws.yaml""
");

        var cdp = new MockCdpService();
        var vm = new TestStudioViewModel(cdp)
        {
            WorkspaceRootPath = tempDir,
            CurrentFlowFilePath = mainFile,
            IsGenerateReportEnabled = false,
            IsRecordVideoEnabled = false
        };

        var steps = TestStudioYamlParser.Parse(await File.ReadAllTextAsync(mainFile), out _, out _).Select(TestStudioStepModel.FromCoreStep);
        foreach (var s in steps)
        {
            vm.Steps.Add(s);
        }

        await vm.PlayAsync();
        
        int timeout = 100;
        while (vm.IsExecuting && timeout > 0)
        {
            await Task.Delay(50);
            timeout--;
        }

        Assert.False(vm.IsExecuting);
        
        // Cleanup
        try
        {
            Directory.Delete(tempDir, true);
        }
        catch {}
    }

    [Fact]
    public async Task TestSubflowEnvMerging()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var subFile = Path.Combine(tempDir, "subflow.yaml");
        var mainFile = Path.Combine(tempDir, "main.yaml");

        await File.WriteAllTextAsync(subFile, @"
- inputText: ""${VAR1}-${VAR2}""
");
        await File.WriteAllTextAsync(mainFile, @"
- runFlow:
    file: ""subflow.yaml""
    env:
      VAR2: ""local-val""
");

        var cdp = new MockCdpService();
        var vm = new TestStudioViewModel(cdp)
        {
            WorkspaceRootPath = tempDir,
            CurrentFlowFilePath = mainFile,
            IsGenerateReportEnabled = false,
            IsRecordVideoEnabled = false
        };

        var steps = TestStudioYamlParser.Parse(await File.ReadAllTextAsync(mainFile), out _, out _).Select(TestStudioStepModel.FromCoreStep);
        foreach (var s in steps)
        {
            vm.Steps.Add(s);
        }

        var runLoopMethod = typeof(TestStudioViewModel).GetMethod("RunLoopAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(runLoopMethod);

        var parentEnv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "VAR1", "parent-val" },
            { "VAR2", "parent-val-to-be-overridden" }
        };

        var cts = new CancellationTokenSource();
        var invokeRes = runLoopMethod.Invoke(vm, new object[] { parentEnv, cts.Token });
        Assert.NotNull(invokeRes);
        await (Task)invokeRes;

        // Verify SentCommands captured the Input.insertText with the merged parameters
        var insertTextCall = cdp.SentCommands.Find(c => c.Method == "Input.insertText");
        Assert.NotNull(insertTextCall.Params);
        Assert.Equal("parent-val-local-val", insertTextCall.Params!["text"]?.ToString());

        // Cleanup
        try
        {
            Directory.Delete(tempDir, true);
        }
        catch {}
    }
}
