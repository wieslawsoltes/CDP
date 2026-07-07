using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Chrome.DevTools.Protocol;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class BiDiProtocolTests
{
    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static ClientWebSocket CreateClientWebSocket()
    {
        var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);
        return ws;
    }

    private static async Task ConnectWithTimeoutAsync(ClientWebSocket ws, Uri uri)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await ws.ConnectAsync(uri, cts.Token);
    }

    private static async Task<string> ReceiveMessageAsync(ClientWebSocket ws)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static async Task SendMessageAsync(ClientWebSocket ws, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
    }

    [AvaloniaFact]
    public async Task TestWebDriverBiDiFlow()
    {
        var window = new Window { Title = "BiDi Test Window" };
        var button = new Button { Name = "btnClickMe", Content = "Click Me" };
        window.Content = button;
        window.Show();
        var targetId = Avalonia.Diagnostics.Cdp.CdpServer.Register(window, "BiDi Test Window");

        int port = GetFreePort();
        CdpServer.Start(port);
        
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync($"http://127.0.0.1:{port}/session", new StringContent("{}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var contentStr = await response.Content.ReadAsStringAsync();
            var contentNode = JsonNode.Parse(contentStr) as JsonObject;
            Assert.NotNull(contentNode);
            
            var valueObj = contentNode["value"] as JsonObject;
            Assert.NotNull(valueObj);
            
            var sessionId = valueObj["sessionId"]?.GetValue<string>();
            Assert.False(string.IsNullOrEmpty(sessionId));

            var capabilities = valueObj["capabilities"] as JsonObject;
            Assert.NotNull(capabilities);
            var webSocketUrl = capabilities["webSocketUrl"]?.GetValue<string>();
            Assert.Contains("/session/bidi", webSocketUrl);

            var ws = CreateClientWebSocket();
            var wsUri = new Uri($"ws://127.0.0.1:{port}/session/bidi?session={sessionId}");
            await ConnectWithTimeoutAsync(ws, wsUri);
            Assert.Equal(WebSocketState.Open, ws.State);

            var getTreeCmd = new JsonObject
            {
                ["id"] = 1,
                ["method"] = "browsingContext.getTree",
                ["params"] = new JsonObject()
            };
            await SendMessageAsync(ws, getTreeCmd.ToJsonString());

            var getTreeRespStr = await ReceiveMessageAsync(ws);
            var getTreeResp = JsonNode.Parse(getTreeRespStr) as JsonObject;
            Assert.NotNull(getTreeResp);
            Assert.Equal(1, getTreeResp["id"]?.GetValue<int>());
            Assert.Equal("success", getTreeResp["type"]?.GetValue<string>());
            
            var getTreeResult = getTreeResp["result"] as JsonObject;
            Assert.NotNull(getTreeResult);
            var contexts = getTreeResult["contexts"] as JsonArray;
            Assert.NotNull(contexts);
            
            var targetContext = contexts.FirstOrDefault(c => c?["context"]?.GetValue<string>() == targetId);
            Assert.NotNull(targetContext);
            
            foreach (var ctx in contexts)
            {
                var id = ctx?["context"]?.GetValue<string>();
                Assert.False(id?.StartsWith("tab-") ?? false);
            }

            var contextId = targetId;

            var evalCmd = new JsonObject
            {
                ["id"] = 2,
                ["method"] = "script.evaluate",
                ["params"] = new JsonObject
                {
                    ["expression"] = "__raw_window.Title",
                    ["target"] = new JsonObject { ["context"] = contextId },
                    ["awaitPromise"] = false,
                    ["serializationOptions"] = new JsonObject
                    {
                        ["serialization"] = "deep",
                        ["maxDepth"] = 3
                    }
                }
            };
            await SendMessageAsync(ws, evalCmd.ToJsonString());

            var evalRespStr = await ReceiveMessageAsync(ws);
            var evalResp = JsonNode.Parse(evalRespStr) as JsonObject;
            Assert.NotNull(evalResp);
            Assert.Equal(2, evalResp["id"]?.GetValue<int>());
            Assert.Equal("success", evalResp["type"]?.GetValue<string>());
            
            var evalResult = evalResp["result"] as JsonObject;
            Assert.NotNull(evalResult);
            var remoteVal = evalResult["result"] as JsonObject;
            Assert.NotNull(remoteVal);
            Assert.Equal("string", remoteVal["type"]?.GetValue<string>());
            Assert.Equal("BiDi Test Window", remoteVal["value"]?.GetValue<string>());

            var performActionsCmd = new JsonObject
            {
                ["id"] = 3,
                ["method"] = "input.performActions",
                ["params"] = new JsonObject
                {
                    ["context"] = contextId,
                    ["actions"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "pointer",
                            ["id"] = "mouse",
                            ["actions"] = new JsonArray
                            {
                                new JsonObject { ["type"] = "pointerMove", ["x"] = 100, ["y"] = 100 },
                                new JsonObject { ["type"] = "pointerDown", ["button"] = 0 },
                                new JsonObject { ["type"] = "pointerUp", ["button"] = 0 }
                            }
                        }
                    }
                }
            };
            await SendMessageAsync(ws, performActionsCmd.ToJsonString());

            var actionRespStr = await ReceiveMessageAsync(ws);
            var actionResp = JsonNode.Parse(actionRespStr) as JsonObject;
            Assert.NotNull(actionResp);
            Assert.Equal(3, actionResp["id"]?.GetValue<int>());
            Assert.Equal("success", actionResp["type"]?.GetValue<string>());

            var subscribeCmd = new JsonObject
            {
                ["id"] = 4,
                ["method"] = "session.subscribe",
                ["params"] = new JsonObject
                {
                    ["events"] = new JsonArray { "network.beforeRequestSent" }
                }
            };
            await SendMessageAsync(ws, subscribeCmd.ToJsonString());

            var subRespStr = await ReceiveMessageAsync(ws);
            var subResp = JsonNode.Parse(subRespStr) as JsonObject;
            Assert.NotNull(subResp);
            Assert.Equal(4, subResp["id"]?.GetValue<int>());
            Assert.Equal("success", subResp["type"]?.GetValue<string>());

            var dummyParams = new JsonObject
            {
                ["requestId"] = "req-123",
                ["request"] = new JsonObject
                {
                    ["url"] = "http://example.com/test",
                    ["method"] = "GET",
                    ["headers"] = new JsonObject { ["User-Agent"] = "TestAgent" }
                }
            };
            
            global::Chrome.DevTools.Protocol.Domains.NetworkDomain.Initialize();
            var broadcastMethod = typeof(global::Chrome.DevTools.Protocol.Domains.NetworkDomain).GetMethod("BroadcastEvent", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (broadcastMethod != null)
            {
                broadcastMethod.Invoke(null, new object[] { "Network.requestWillBeSent", dummyParams });
            }

            var bidiEventStr = await ReceiveMessageAsync(ws);
            var bidiEvent = JsonNode.Parse(bidiEventStr) as JsonObject;
            Assert.NotNull(bidiEvent);
            Assert.Equal("event", bidiEvent["type"]?.GetValue<string>());
            Assert.Equal("network.beforeRequestSent", bidiEvent["method"]?.GetValue<string>());
            
            var bidiParams = bidiEvent["params"] as JsonObject;
            Assert.NotNull(bidiParams);
            Assert.Equal("main", bidiParams["context"]?.GetValue<string>());
            
            var requestObj = bidiParams["request"] as JsonObject;
            Assert.NotNull(requestObj);
            Assert.Equal("req-123", requestObj["request"]?.GetValue<string>());
            Assert.Equal("http://example.com/test", requestObj["url"]?.GetValue<string>());
            Assert.Equal("GET", requestObj["method"]?.GetValue<string>());

            var headersArr = requestObj["headers"] as JsonArray;
            Assert.NotNull(headersArr);
            var uaHeader = headersArr[0] as JsonObject;
            Assert.NotNull(uaHeader);
            Assert.Equal("User-Agent", uaHeader["name"]?.GetValue<string>());
            var uaVal = uaHeader["value"] as JsonObject;
            Assert.NotNull(uaVal);
            Assert.Equal("string", uaVal["type"]?.GetValue<string>());
            Assert.Equal("TestAgent", uaVal["value"]?.GetValue<string>());

            // Test network.responseCompleted subscription
            var subscribeResponseCmd = new JsonObject
            {
                ["id"] = 7,
                ["method"] = "session.subscribe",
                ["params"] = new JsonObject
                {
                    ["events"] = new JsonArray { "network.responseCompleted" }
                }
            };
            await SendMessageAsync(ws, subscribeResponseCmd.ToJsonString());

            var subResp2Str = await ReceiveMessageAsync(ws);
            var subResp2 = JsonNode.Parse(subResp2Str) as JsonObject;
            Assert.NotNull(subResp2);
            Assert.Equal(7, subResp2["id"]?.GetValue<int>());
            Assert.Equal("success", subResp2["type"]?.GetValue<string>());

            var dummyResponseParams = new JsonObject
            {
                ["requestId"] = "req-123",
                ["response"] = new JsonObject
                {
                    ["url"] = "http://example.com/test",
                    ["status"] = 200,
                    ["statusText"] = "OK",
                    ["mimeType"] = "text/html",
                    ["headers"] = new JsonObject { ["Content-Type"] = "text/html" }
                }
            };

            if (broadcastMethod != null)
            {
                broadcastMethod.Invoke(null, new object[] { "Network.responseReceived", dummyResponseParams });
            }

            var bidiEvent2Str = await ReceiveMessageAsync(ws);
            var bidiEvent2 = JsonNode.Parse(bidiEvent2Str) as JsonObject;
            Assert.NotNull(bidiEvent2);
            Assert.Equal("event", bidiEvent2["type"]?.GetValue<string>());
            Assert.Equal("network.responseCompleted", bidiEvent2["method"]?.GetValue<string>());

            var bidiParams2 = bidiEvent2["params"] as JsonObject;
            Assert.NotNull(bidiParams2);
            Assert.Equal("main", bidiParams2["context"]?.GetValue<string>());

            var responseObj2 = bidiParams2["response"] as JsonObject;
            Assert.NotNull(responseObj2);
            Assert.Equal("http://example.com/test", responseObj2["url"]?.GetValue<string>());
            Assert.Equal(200, responseObj2["status"]?.GetValue<int>());
            Assert.Equal("OK", responseObj2["statusText"]?.GetValue<string>());
            Assert.Equal("text/html", responseObj2["mimeType"]?.GetValue<string>());

            // 1. Test invalid context ID evaluation -> error "no such frame"
            var invalidEvalCmd = new JsonObject
            {
                ["id"] = 5,
                ["method"] = "script.evaluate",
                ["params"] = new JsonObject
                {
                    ["expression"] = "1+1",
                    ["target"] = new JsonObject { ["context"] = "invalid-context-id" }
                }
            };
            await SendMessageAsync(ws, invalidEvalCmd.ToJsonString());
            var invalidEvalRespStr = await ReceiveMessageAsync(ws);
            var invalidEvalResp = JsonNode.Parse(invalidEvalRespStr) as JsonObject;
            Assert.NotNull(invalidEvalResp);
            Assert.Equal(5, invalidEvalResp["id"]?.GetValue<int>());
            Assert.Equal("error", invalidEvalResp["type"]?.GetValue<string>());
            Assert.Equal("no such frame", invalidEvalResp["error"]?.GetValue<string>());

            // 2. Test performActions with special keys (PUA characters like Backspace)
            var keyActionsCmd = new JsonObject
            {
                ["id"] = 6,
                ["method"] = "input.performActions",
                ["params"] = new JsonObject
                {
                    ["context"] = contextId,
                    ["actions"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "key",
                            ["id"] = "keyboard",
                            ["actions"] = new JsonArray
                            {
                                new JsonObject { ["type"] = "keyDown", ["value"] = "\uE003" },
                                new JsonObject { ["type"] = "keyUp", ["value"] = "\uE003" }
                            }
                        }
                    }
                }
            };
            await SendMessageAsync(ws, keyActionsCmd.ToJsonString());
            var keyRespStr = await ReceiveMessageAsync(ws);
            var keyResp = JsonNode.Parse(keyRespStr) as JsonObject;
            Assert.NotNull(keyResp);
            Assert.Equal(6, keyResp["id"]?.GetValue<int>());
            Assert.Equal("success", keyResp["type"]?.GetValue<string>());

            // 3. Reflection-test MapToBiDiRemoteValue mappings for advanced types
            var mapMethod = typeof(BiDiSession).GetMethod("MapToBiDiRemoteValue", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(mapMethod);

            // Date / RegExp
            var dateObj = new JsonObject { ["type"] = "date", ["value"] = "2026-07-07T14:04:51.000Z" };
            var dateResult = mapMethod.Invoke(null, new object[] { dateObj }) as JsonObject;
            Assert.NotNull(dateResult);
            Assert.Equal("date", dateResult["type"]?.GetValue<string>());
            Assert.Equal("2026-07-07T14:04:51.000Z", dateResult["value"]?.GetValue<string>());

            // Map
            var mapObj = new JsonObject
            {
                ["type"] = "map",
                ["value"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["key"] = new JsonObject { ["type"] = "string", ["value"] = "key-name" },
                        ["value"] = new JsonObject { ["type"] = "number", ["value"] = 42 }
                    }
                }
            };
            var mapResult = mapMethod.Invoke(null, new object[] { mapObj }) as JsonObject;
            Assert.NotNull(mapResult);
            Assert.Equal("map", mapResult["type"]?.GetValue<string>());
            var mapVal = mapResult["value"] as JsonArray;
            Assert.NotNull(mapVal);
            Assert.Single(mapVal);
            var firstPair = mapVal[0] as JsonArray;
            Assert.NotNull(firstPair);
            Assert.Equal(2, firstPair.Count);
            Assert.Equal("string", firstPair[0]?["type"]?.GetValue<string>());
            Assert.Equal("key-name", firstPair[0]?["value"]?.GetValue<string>());
            Assert.Equal("number", firstPair[1]?["type"]?.GetValue<string>());
            Assert.Equal(42, firstPair[1]?["value"]?.GetValue<int>());

            // Set
            var setObj = new JsonObject
            {
                ["type"] = "set",
                ["value"] = new JsonArray
                {
                    new JsonObject { ["type"] = "string", ["value"] = "set-item" }
                }
            };
            var setResult = mapMethod.Invoke(null, new object[] { setObj }) as JsonObject;
            Assert.NotNull(setResult);
            Assert.Equal("set", setResult["type"]?.GetValue<string>());
            var setVal = setResult["value"] as JsonArray;
            Assert.NotNull(setVal);
            Assert.Single(setVal);
            Assert.Equal("string", setVal[0]?["type"]?.GetValue<string>());
            Assert.Equal("set-item", setVal[0]?["value"]?.GetValue<string>());

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
        }
        finally
        {
            window.Close();
            CdpServer.Stop();
        }
    }
}
