using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using CDP.Inspector.CLI;
using CdpInspectorApp.Services;
using Chrome.DevTools.Protocol;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class McpCliTests
{
    [Fact]
    public void TestGetMcpToolsList()
    {
        var tools = Program.GetMcpToolsList();
        Assert.NotNull(tools);
        Assert.Equal(7, tools.Count);

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

        public Task ConnectAsync(string host, TargetItem target) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<List<TargetItem>> GetTargetsAsync(string host) => Task.FromResult(new List<TargetItem>());
        public Task<JsonObject> SendCommandAsync(string method, JsonObject? parameters = null) => Task.FromResult(new JsonObject());
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
            Assert.Equal(7, tools.Count);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
