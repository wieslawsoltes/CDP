using Chrome.DevTools.Protocol.Inspector;

namespace Chrome.DevTools.Protocol.Inspector.Tests;

public sealed class V8InspectorClientTests
{
    [Fact]
    public async Task Options_are_validated_and_the_websocket_can_be_configured()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new V8InspectorClient(new V8InspectorClientOptions
        {
            ReceiveBufferSize = 0
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new V8InspectorClient(new V8InspectorClientOptions
        {
            MaxIncomingMessageSize = 0
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new V8InspectorClient(new V8InspectorClientOptions
        {
            MaxOutgoingMessageSize = 0
        }));

        var configured = false;
        await using var client = new V8InspectorClient(new V8InspectorClientOptions
        {
            ConfigureWebSocket = options =>
            {
                Assert.Equal(TimeSpan.FromSeconds(15), options.KeepAliveInterval);
                Assert.Equal(TimeSpan.FromSeconds(5), options.KeepAliveTimeout);
                options.SetRequestHeader("X-Inspector-Test", "configured");
                configured = true;
            }
        });

        Assert.True(configured);
    }

    [Fact]
    public void Protocol_exception_preserves_structured_error_data()
    {
        var data = System.Text.Json.Nodes.JsonNode.Parse("{\"details\":{\"status\":\"BlockedByActiveFunction\"}}")!;

        var exception = new V8InspectorProtocolException("Debugger.setScriptSource", -32000, "Live edit failed", data);

        Assert.Equal("Debugger.setScriptSource", exception.Method);
        Assert.Equal(-32000, exception.Code);
        Assert.Equal("BlockedByActiveFunction", exception.ProtocolData?["details"]?["status"]?.GetValue<string>());
    }
}
