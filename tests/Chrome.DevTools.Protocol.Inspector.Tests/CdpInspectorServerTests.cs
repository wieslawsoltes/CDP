using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Chrome.DevTools.Protocol.Inspector;

namespace Chrome.DevTools.Protocol.Inspector.Tests;

public class CdpInspectorServerTests
{
    [Fact]
    public void Core_assembly_has_no_optional_runtime_references()
    {
        var references = typeof(CdpInspectorServer).Assembly.GetReferencedAssemblies()
            .Select(x => x.Name)
            .Where(x => x is not null)
            .ToArray();

        Assert.DoesNotContain("Jint", references);
        Assert.DoesNotContain("SkiaSharp", references);
        Assert.DoesNotContain("YamlDotNet", references);
        Assert.DoesNotContain("Microsoft.Diagnostics.NETCore.Client", references);
        Assert.DoesNotContain("Microsoft.Diagnostics.Tracing.TraceEvent", references);
        Assert.DoesNotContain("JetBrains.Profiler.Api", references);
        Assert.DoesNotContain("JetBrains.Profiler.SelfApi", references);
        Assert.DoesNotContain("Xaml.Compiler", references);
    }

    [Fact]
    public async Task Discovery_is_chrome_compatible_and_reports_authenticated_socket_metadata()
    {
        await using var fixture = await InspectorFixture.StartAsync();
        using var client = new HttpClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var discovery = await client.GetAsync(new Uri(fixture.Server.DiscoveryUri, "json/version"), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);

        var version = await client.GetStringAsync(fixture.AuthenticatedUri("json/version"), cancellationToken);
        var versionJson = JsonNode.Parse(version)!.AsObject();
        Assert.Equal("WebScene/0.1", versionJson["Browser"]?.GetValue<string>());
        Assert.Equal("13.9.201", versionJson["V8-Version"]?.GetValue<string>());
        Assert.Contains(fixture.Token, versionJson["webSocketDebuggerUrl"]?.GetValue<string>());

        var list = JsonNode.Parse(await client.GetStringAsync(fixture.AuthenticatedUri("json/list"), cancellationToken))!.AsArray();
        var target = Assert.IsType<JsonObject>(Assert.Single(list));
        Assert.Equal("webscene-main", target["id"]?.GetValue<string>());
        Assert.Equal("WebScene React app", target["title"]?.GetValue<string>());
        Assert.Equal("file:///app/index.html", target["url"]?.GetValue<string>());
        Assert.Contains(fixture.Token, target["devtoolsFrontendUrl"]?.GetValue<string>());
    }

    [Fact]
    public async Task Discovery_authentication_can_be_required_explicitly()
    {
        await using var fixture = await InspectorFixture.StartAsync(requireDiscoveryAuthentication: true);
        using var client = new HttpClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var unauthorized = await client.GetAsync(new Uri(fixture.Server.DiscoveryUri, "json/list"), cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var authorized = await client.GetAsync(fixture.AuthenticatedUri("json/list"), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
    }

    [Fact]
    public async Task WebSocket_forwards_requests_responses_and_notifications_unchanged()
    {
        await using var fixture = await InspectorFixture.StartAsync();
        using var socket = new ClientWebSocket();
        var cancellationToken = TestContext.Current.CancellationToken;
        await socket.ConnectAsync(fixture.WebSocketUri(), cancellationToken);

        const string request = "{\"id\":7,\"method\":\"Runtime.enable\"}";
        await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, cancellationToken);

        Assert.Equal(request, await fixture.Target.Transport.Received.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken));
        Assert.Equal("{\"id\":7,\"result\":{}}", await ReceiveTextAsync(socket, cancellationToken));
        Assert.Equal("{\"method\":\"Runtime.consoleAPICalled\",\"params\":{}}", await ReceiveTextAsync(socket, cancellationToken));
    }

    [Fact]
    public async Task Unknown_target_and_disallowed_origin_fail_safely()
    {
        await using var fixture = await InspectorFixture.StartAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var unauthorizedSocket = new ClientWebSocket();
        var unauthorized = new Uri($"ws://127.0.0.1:{fixture.Port}/devtools/page/webscene-main");
        var unauthorizedError = await Assert.ThrowsAnyAsync<WebSocketException>(() => unauthorizedSocket.ConnectAsync(unauthorized, cancellationToken));
        Assert.Contains("401", unauthorizedError.Message);

        using var missingSocket = new ClientWebSocket();
        var missing = new Uri($"ws://127.0.0.1:{fixture.Port}/devtools/page/missing?token={fixture.Token}");
        var missingError = await Assert.ThrowsAnyAsync<WebSocketException>(() => missingSocket.ConnectAsync(missing, cancellationToken));
        Assert.Contains("404", missingError.Message);

        using var originSocket = new ClientWebSocket();
        originSocket.Options.SetRequestHeader("Origin", "https://untrusted.example");
        var originError = await Assert.ThrowsAnyAsync<WebSocketException>(() => originSocket.ConnectAsync(fixture.WebSocketUri(), cancellationToken));
        Assert.Contains("403", originError.Message);
    }

    [Fact]
    public async Task Oversized_browser_message_closes_the_session()
    {
        await using var fixture = await InspectorFixture.StartAsync(maxMessageBytes: 1024);
        using var socket = new ClientWebSocket();
        var cancellationToken = TestContext.Current.CancellationToken;
        await socket.ConnectAsync(fixture.WebSocketUri(), cancellationToken);

        await socket.SendAsync(new byte[2048], WebSocketMessageType.Text, true, cancellationToken);
        var buffer = new byte[128];
        var result = await socket.ReceiveAsync(buffer, cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, result.CloseStatus);
    }

    [Fact]
    public async Task Concurrent_session_limit_rejects_additional_clients()
    {
        await using var fixture = await InspectorFixture.StartAsync(maxConcurrentSessions: 1);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var firstSocket = new ClientWebSocket();
        await firstSocket.ConnectAsync(fixture.WebSocketUri(), cancellationToken);

        using var secondSocket = new ClientWebSocket();
        var error = await Assert.ThrowsAnyAsync<WebSocketException>(() =>
            secondSocket.ConnectAsync(fixture.WebSocketUri(), cancellationToken));

        Assert.Contains("503", error.Message);
    }

    [Fact]
    public async Task Server_is_opt_in_and_remote_bindings_require_explicit_authorization()
    {
        var registry = new RawCdpTargetRegistry();
        var version = new CdpInspectorVersionInfo("WebScene/0.1", "1.3", "WebScene");
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var disabled = new CdpInspectorServer(registry, version, new CdpInspectorServerOptions());
        await Assert.ThrowsAsync<InvalidOperationException>(() => disabled.StartAsync(cancellationToken));

        await using var remote = new CdpInspectorServer(registry, version, new CdpInspectorServerOptions
        {
            Enabled = true,
            Address = IPAddress.Any,
            Port = GetFreePort()
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => remote.StartAsync(cancellationToken));
    }

    [Fact]
    public async Task Server_can_restart_after_a_clean_stop()
    {
        await using var fixture = await InspectorFixture.StartAsync();
        using var client = new HttpClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        await fixture.Server.StopAsync(cancellationToken);
        await fixture.Server.StartAsync(cancellationToken);

        var response = await client.GetAsync(fixture.AuthenticatedUri("json/list"), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var bytes = new byte[4096];
        var result = await socket.ReceiveAsync(bytes, cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        Assert.True(result.EndOfMessage);
        return Encoding.UTF8.GetString(bytes, 0, result.Count);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class InspectorFixture : IAsyncDisposable
    {
        private InspectorFixture(int port, string token, FakeTarget target, CdpInspectorServer server)
        {
            Port = port;
            Token = token;
            Target = target;
            Server = server;
        }

        public int Port { get; }
        public string Token { get; }
        public FakeTarget Target { get; }
        public CdpInspectorServer Server { get; }

        public static async Task<InspectorFixture> StartAsync(
            int maxMessageBytes = 16 * 1024 * 1024,
            int maxConcurrentSessions = 4,
            bool requireDiscoveryAuthentication = false)
        {
            var port = GetFreePort();
            const string token = "67b20765654d4743b745181433c82d78";
            var target = new FakeTarget();
            var registry = new RawCdpTargetRegistry();
            registry.Register(target);
            var server = new CdpInspectorServer(
                registry,
                new CdpInspectorVersionInfo("WebScene/0.1", "1.3", "WebScene V8", "13.9.201"),
                new CdpInspectorServerOptions
                {
                    Enabled = true,
                    Port = port,
                    AccessToken = token,
                    MaxMessageBytes = maxMessageBytes,
                    ReceiveBufferBytes = Math.Min(16 * 1024, maxMessageBytes),
                    MaxConcurrentSessions = maxConcurrentSessions,
                    RequireAuthenticationForDiscovery = requireDiscoveryAuthentication
                });
            await server.StartAsync(TestContext.Current.CancellationToken);
            return new InspectorFixture(port, token, target, server);
        }

        public Uri AuthenticatedUri(string path) => new($"http://127.0.0.1:{Port}/{path}?token={Token}");

        public Uri WebSocketUri() => new($"ws://127.0.0.1:{Port}/devtools/page/webscene-main?token={Token}");

        public ValueTask DisposeAsync() => Server.DisposeAsync();
    }

    private sealed class FakeTarget : IRawCdpTarget
    {
        public FakeTransport Transport { get; } = new();

        public CdpInspectorTargetInfo Info { get; } =
            new("webscene-main", "WebScene React app", "file:///app/index.html", "node", "Embedded V8 runtime");

        public ValueTask<IRawCdpTransport> OpenTransportAsync(CdpInspectorConnectionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IRawCdpTransport>(Transport);
    }

    private sealed class FakeTransport : RawCdpTransportBase
    {
        public TaskCompletionSource<string> Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
        {
            Received.TrySetResult(Encoding.UTF8.GetString(message.Span));
            await PublishAsync(Encoding.UTF8.GetBytes("{\"id\":7,\"result\":{}}"), cancellationToken);
            await PublishAsync(Encoding.UTF8.GetBytes("{\"method\":\"Runtime.consoleAPICalled\",\"params\":{}}"), cancellationToken);
        }
    }
}
