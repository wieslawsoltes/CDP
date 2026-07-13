using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using CDP.Inspector.CLI;
using CdpInspectorApp.Services;
using Chrome.DevTools.Protocol;
using System.CommandLine;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class McpCliTests
{
    [Fact]
    public void TestGetMcpToolsList()
    {
        var tools = Program.GetMcpToolsList();
        Assert.NotNull(tools);
        Assert.Equal(10, tools.Count);

        var toolNames = new List<string>();
        foreach (var tool in tools)
        {
            toolNames.Add(tool?["name"]?.GetValue<string>() ?? "");
        }

        Assert.Contains("dom_query", toolNames);
        Assert.Contains("evaluate", toolNames);
        Assert.Contains("screenshot", toolNames);
        Assert.Contains("tap", toolNames);
        Assert.Contains("input_text", toolNames);
        Assert.Contains("clear_text", toolNames);
        Assert.Contains("scroll", toolNames);
        Assert.Contains("profile_start", toolNames);
        Assert.Contains("profile_stop", toolNames);
        Assert.Contains("profile_take_snapshot", toolNames);
    }

    private class MockCdpService : ICdpService
    {
        public bool IsConnected { get; set; } = false;
        public string ConnectionStatus => "";
        public string ConnectedHost => "";
        public string ConnectedTargetId => "";
        public bool IsPreviewScreencastActive { get; set; } = false;
        public string? HostAddress { get; set; }
        public TargetItem? SelectedTarget { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<CdpEventEventArgs>? EventReceived;

        public Func<string, JsonObject?, JsonObject>? SendCommandCallback { get; set; }

        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null)
        {
            var result = SendCommandCallback?.Invoke(method, parameters) ?? new JsonObject();
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task TestHandleMcpInitialize()
    {
        var mockCdp = new MockCdpService();
        var target = new TargetItem("Test", "ws://127.0.0.1:9222/devtools/page/test", "test");
        var id = JsonValue.Create(123);

        var originalOut = Console.Out;
        using var sw = new System.IO.StringWriter();
        Console.SetOut(sw);

        try
        {
            await Program.HandleMcpMethodAsync(mockCdp, "http://127.0.0.1:9222", target, id, "initialize", null);
            var output = sw.ToString().Trim();
            
            var response = JsonNode.Parse(output) as JsonObject;
            Assert.NotNull(response);
            Assert.Equal("2.0", response["jsonrpc"]?.GetValue<string>());
            Assert.Equal(123, response["id"]?.GetValue<int>());
            
            var result = response["result"] as JsonObject;
            Assert.NotNull(result);
            Assert.Equal("2024-11-05", result["protocolVersion"]?.GetValue<string>());
            Assert.Equal("cdp-mcp-server", result["serverInfo"]?["name"]?.GetValue<string>());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task TestHandleMcpToolsList()
    {
        var mockCdp = new MockCdpService();
        var target = new TargetItem("Test", "ws://127.0.0.1:9222/devtools/page/test", "test");
        var id = JsonValue.Create(456);

        var originalOut = Console.Out;
        using var sw = new System.IO.StringWriter();
        Console.SetOut(sw);

        try
        {
            await Program.HandleMcpMethodAsync(mockCdp, "http://127.0.0.1:9222", target, id, "tools/list", null);
            var output = sw.ToString().Trim();

            var response = JsonNode.Parse(output) as JsonObject;
            Assert.NotNull(response);
            Assert.Equal("2.0", response["jsonrpc"]?.GetValue<string>());
            Assert.Equal(456, response["id"]?.GetValue<int>());

            var result = response["result"] as JsonObject;
            Assert.NotNull(result);
            var tools = result["tools"] as JsonArray;
            Assert.NotNull(tools);
            Assert.Equal(10, tools.Count);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task TestExecuteProfileStart_NoEngine()
    {
        var mockCdp = new MockCdpService();
        var target = new TargetItem("Test", "ws://127.0.0.1:9222/devtools/page/test", "test");
        var args = new JsonObject();

        var methodsCalled = new List<string>();
        mockCdp.SendCommandCallback = (method, parameters) =>
        {
            methodsCalled.Add(method);
            return new JsonObject();
        };

        var response = await Program.ExecuteMcpToolAsync(mockCdp, "http://127.0.0.1:9222", target, "profile_start", args);
        
        Assert.Single(methodsCalled);
        Assert.Equal("Profiler.start", methodsCalled[0]);
        
        var contentArray = response["content"] as JsonArray;
        Assert.NotNull(contentArray);
        Assert.Single(contentArray);
        Assert.Equal("text", contentArray[0]?["type"]?.GetValue<string>());
        Assert.Contains("success", contentArray[0]?["text"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestExecuteProfileStart_WithEngine()
    {
        var mockCdp = new MockCdpService();
        var target = new TargetItem("Test", "ws://127.0.0.1:9222/devtools/page/test", "test");
        var args = new JsonObject
        {
            ["engineName"] = "eventpipe"
        };

        var methodsCalled = new List<string>();
        JsonObject? setProfilingEngineParams = null;
        mockCdp.SendCommandCallback = (method, parameters) =>
        {
            methodsCalled.Add(method);
            if (method == "Profiler.setProfilingEngine")
            {
                setProfilingEngineParams = parameters;
            }
            return new JsonObject();
        };

        var response = await Program.ExecuteMcpToolAsync(mockCdp, "http://127.0.0.1:9222", target, "profile_start", args);
        
        Assert.Equal(2, methodsCalled.Count);
        Assert.Equal("Profiler.setProfilingEngine", methodsCalled[0]);
        Assert.Equal("Profiler.start", methodsCalled[1]);
        Assert.NotNull(setProfilingEngineParams);
        Assert.Equal("eventpipe", setProfilingEngineParams["engineName"]?.GetValue<string>());
    }

    [Fact]
    public async Task TestExecuteProfileStop()
    {
        var mockCdp = new MockCdpService();
        var target = new TargetItem("Test", "ws://127.0.0.1:9222/devtools/page/test", "test");
        var args = new JsonObject();

        mockCdp.SendCommandCallback = (method, parameters) =>
        {
            Assert.Equal("Profiler.stop", method);
            return new JsonObject
            {
                ["profile"] = new JsonObject
                {
                    ["nodes"] = new JsonArray()
                }
            };
        };

        var response = await Program.ExecuteMcpToolAsync(mockCdp, "http://127.0.0.1:9222", target, "profile_stop", args);
        
        var contentArray = response["content"] as JsonArray;
        Assert.NotNull(contentArray);
        Assert.Single(contentArray);
        Assert.Equal("text", contentArray[0]?["type"]?.GetValue<string>());
        
        var textContent = contentArray[0]?["text"]?.GetValue<string>();
        Assert.NotNull(textContent);
        Assert.Contains("profile", textContent);
    }

    [Fact]
    public async Task TestExecuteProfileTakeSnapshot()
    {
        var mockCdp = new MockCdpService();
        var target = new TargetItem("Test", "ws://127.0.0.1:9222/devtools/page/test", "test");
        var args = new JsonObject
        {
            ["name"] = "MySnapshot"
        };

        JsonObject? takeSnapshotParams = null;
        mockCdp.SendCommandCallback = (method, parameters) =>
        {
            Assert.Equal("Profiler.takeJetBrainsMemorySnapshot", method);
            takeSnapshotParams = parameters;
            return new JsonObject
            {
                ["snapshotPath"] = "/tmp/MySnapshot.dmw"
            };
        };

        var response = await Program.ExecuteMcpToolAsync(mockCdp, "http://127.0.0.1:9222", target, "profile_take_snapshot", args);
        
        Assert.NotNull(takeSnapshotParams);
        Assert.Equal("MySnapshot", takeSnapshotParams["name"]?.GetValue<string>());

        var contentArray = response["content"] as JsonArray;
        Assert.NotNull(contentArray);
        Assert.Single(contentArray);
        Assert.Equal("text", contentArray[0]?["type"]?.GetValue<string>());
        Assert.Contains("/tmp/MySnapshot.dmw", contentArray[0]?["text"]?.GetValue<string>());
    }

    [Fact]
    public async Task TestExecuteProfileTakeSnapshot_DefaultName()
    {
        var mockCdp = new MockCdpService();
        var target = new TargetItem("Test", "ws://127.0.0.1:9222/devtools/page/test", "test");
        var args = new JsonObject();

        JsonObject? takeSnapshotParams = null;
        mockCdp.SendCommandCallback = (method, parameters) =>
        {
            Assert.Equal("Profiler.takeJetBrainsMemorySnapshot", method);
            takeSnapshotParams = parameters;
            return new JsonObject
            {
                ["snapshotPath"] = "/tmp/Snapshot.dmw"
            };
        };

        await Program.ExecuteMcpToolAsync(mockCdp, "http://127.0.0.1:9222", target, "profile_take_snapshot", args);
        
        Assert.NotNull(takeSnapshotParams);
        Assert.Equal("Snapshot", takeSnapshotParams["name"]?.GetValue<string>());
    }

    [Fact]
    public void TestResolveHostOptions()
    {
        var hostOption = new Option<string>(new[] { "--host", "-h" }, () => "http://127.0.0.1:9222");
        var portOption = new Option<int?>(new[] { "--port", "-p" });
        var cmd = new RootCommand { hostOption, portOption };

        // Test case 1: Default values (neither option supplied)
        var result1 = cmd.Parse("");
        var host1 = Program.ResolveHost(result1, hostOption, portOption);
        Assert.Equal("http://127.0.0.1:9222", host1);

        // Test case 2: Only port option supplied
        var result2 = cmd.Parse("-p 8080");
        var host2 = Program.ResolveHost(result2, hostOption, portOption);
        Assert.Equal("http://127.0.0.1:8080", host2);

        // Test case 3: Only host option supplied
        var result3 = cmd.Parse("--host http://localhost");
        var host3 = Program.ResolveHost(result3, hostOption, portOption);
        Assert.Equal("http://localhost", host3);

        // Test case 4: Both host and port option supplied
        var result4 = cmd.Parse("--host http://myremotehost --port 3000");
        var host4 = Program.ResolveHost(result4, hostOption, portOption);
        Assert.Equal("http://myremotehost:3000", host4);
    }
}
