using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class CdpIntegrationTests
{
    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [AvaloniaFact]
    public void TestWebSocketConnectionAndGetDocument()
    {
        Console.WriteLine("INTEGRATION_TEST: Start of test");
        var window = new Window { Title = "Integration Test Window" };
        window.Show(); // Apply template and layout
        var id = CdpServer.Register(window, "Integration Window");
        int port = GetFreePort();
        Console.WriteLine($"INTEGRATION_TEST: Registered window, using port {port}");

        try
        {
            // Start server on free port
            Console.WriteLine("INTEGRATION_TEST: Starting CdpServer");
            CdpServer.Start(port);
            Console.WriteLine("INTEGRATION_TEST: CdpServer started successfully");

            // Start client WebSocket operations on a background thread pool thread
            var clientTask = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("INTEGRATION_TEST_CLIENT: Initializing ClientWebSocket");
                    using var ws = new ClientWebSocket();
                    var uri = new Uri($"ws://localhost:{port}/devtools/page/{id}");
                    Console.WriteLine($"INTEGRATION_TEST_CLIENT: Connecting to {uri}");
                    await ws.ConnectAsync(uri, CancellationToken.None);
                    Console.WriteLine("INTEGRATION_TEST_CLIENT: Connected successfully!");
                    Assert.Equal(WebSocketState.Open, ws.State);

                    // Send DOM.getDocument request
                    var request = new JsonObject
                    {
                        ["id"] = 1,
                        ["method"] = "DOM.getDocument",
                        ["params"] = new JsonObject()
                    };

                    Console.WriteLine("INTEGRATION_TEST_CLIENT: Sending DOM.getDocument request");
                    var requestBytes = Encoding.UTF8.GetBytes(request.ToJsonString());
                    await ws.SendAsync(new ArraySegment<byte>(requestBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine("INTEGRATION_TEST_CLIENT: Sent request successfully");

                    // Receive entire response
                    using var ms = new MemoryStream();
                    var buffer = new byte[4096];
                    WebSocketReceiveResult result;
                    do
                    {
                        Console.WriteLine("INTEGRATION_TEST_CLIENT: Awaiting ReceiveAsync");
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        Console.WriteLine($"INTEGRATION_TEST_CLIENT: Received chunk of size {result.Count}");
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);
                    
                    var responseJson = Encoding.UTF8.GetString(ms.ToArray());
                    Console.WriteLine($"INTEGRATION_TEST_CLIENT: Full response received: {responseJson}");
                    
                    var responseNode = JsonNode.Parse(responseJson) as JsonObject;
                    Assert.NotNull(responseNode);
                    Assert.Equal(1, responseNode["id"]?.GetValue<int>());
                    
                    if (responseNode.ContainsKey("error"))
                    {
                        var error = responseNode["error"] as JsonObject;
                        throw new Exception($"CDP Server Exception: {error?["message"]?.GetValue<string>()}");
                    }

                    var resultNode = responseNode["result"] as JsonObject;
                    Assert.NotNull(resultNode);
                    
                    var rootNode = resultNode["root"] as JsonObject;
                    Assert.NotNull(rootNode);
                    Assert.Equal("#document", rootNode["nodeName"]?.GetValue<string>());

                    // Close websocket connection
                    Console.WriteLine("INTEGRATION_TEST_CLIENT: Closing websocket");
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close test", CancellationToken.None);
                    Console.WriteLine("INTEGRATION_TEST_CLIENT: Closed websocket successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"INTEGRATION_TEST_CLIENT ERROR: {ex}");
                    throw;
                }
            });

            // Pump dispatcher queue on UI thread until client task completes
            int loopCount = 0;
            while (!clientTask.IsCompleted)
            {
                loopCount++;
                if (loopCount % 50 == 0)
                {
                    Console.WriteLine($"INTEGRATION_TEST_UI_LOOP: Pumping dispatcher, clientTask Status: {clientTask.Status}");
                }
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }

            Console.WriteLine("INTEGRATION_TEST: Client task completed, propagating results");
            // Propagate any assertions or exceptions
            clientTask.GetAwaiter().GetResult();
            Console.WriteLine("INTEGRATION_TEST: Test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"INTEGRATION_TEST EXCEPTION: {ex}");
            throw;
        }
        finally
        {
            Console.WriteLine("INTEGRATION_TEST: Cleaning up");
            CdpServer.Stop();
            window.Close();
            Console.WriteLine("INTEGRATION_TEST: Cleanup done");
        }
    }

    private static async Task SendJsonAsync(WebSocket ws, JsonObject json)
    {
        var bytes = Encoding.UTF8.GetBytes(json.ToJsonString());
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<JsonObject> ReceiveJsonAsync(WebSocket ws)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        
        var json = Encoding.UTF8.GetString(ms.ToArray());
        return (JsonObject)JsonNode.Parse(json)!;
    }

    [AvaloniaFact]
    public void TestCdpInspectionAndModification()
    {
        var button = new Button { Name = "inspectBtn" };
        var window = new Window { Title = "Inspection Test Window", Content = button };
        window.Show();
        var id = CdpServer.Register(window, "Inspection Window");
        int port = GetFreePort() + 1;

        try
        {
            CdpServer.Start(port);

            var clientTask = Task.Run(async () =>
            {
                using var ws = new ClientWebSocket();
                var uri = new Uri($"ws://localhost:{port}/devtools/page/{id}");
                await ws.ConnectAsync(uri, CancellationToken.None);

                // 1. Get Document
                var request = new JsonObject
                {
                    ["id"] = 1,
                    ["method"] = "DOM.getDocument",
                    ["params"] = new JsonObject()
                };
                await SendJsonAsync(ws, request);
                var response = await ReceiveJsonAsync(ws);
                Assert.Equal(1, response["id"]?.GetValue<int>());

                // 2. Query Selector for Button
                var qRequest = new JsonObject
                {
                    ["id"] = 2,
                    ["method"] = "DOM.querySelector",
                    ["params"] = new JsonObject
                    {
                        ["nodeId"] = 1,
                        ["selector"] = "ContentPresenter > Button"
                    }
                };
                await SendJsonAsync(ws, qRequest);
                var qResponse = await ReceiveJsonAsync(ws);
                Assert.Equal(2, qResponse["id"]?.GetValue<int>());
                var btnNodeId = qResponse["result"]?["nodeId"]?.GetValue<int>() ?? 0;
                Assert.True(btnNodeId > 1);

                // 3. Focus Button
                var focusRequest = new JsonObject
                {
                    ["id"] = 3,
                    ["method"] = "DOM.focus",
                    ["params"] = new JsonObject { ["nodeId"] = btnNodeId }
                };
                await SendJsonAsync(ws, focusRequest);
                await ReceiveJsonAsync(ws);

                // 4. Set Inspected Node
                var inspectRequest = new JsonObject
                {
                    ["id"] = 4,
                    ["method"] = "DOM.setInspectedNode",
                    ["params"] = new JsonObject { ["nodeId"] = btnNodeId }
                };
                await SendJsonAsync(ws, inspectRequest);
                await ReceiveJsonAsync(ws);

                // 5. Evaluate expression using $0
                var evalRequest = new JsonObject
                {
                    ["id"] = 5,
                    ["method"] = "Runtime.evaluate",
                    ["params"] = new JsonObject
                    {
                        ["expression"] = "$0.Name",
                        ["returnByValue"] = true
                    }
                };
                await SendJsonAsync(ws, evalRequest);
                var evalResponse = await ReceiveJsonAsync(ws);
                Assert.Equal("inspectBtn", evalResponse["result"]?["result"]?["value"]?.GetValue<string>());

                // 6. Set Attribute Value
                var setAttrRequest = new JsonObject
                {
                    ["id"] = 6,
                    ["method"] = "DOM.setAttributeValue",
                    ["params"] = new JsonObject
                    {
                        ["nodeId"] = btnNodeId,
                        ["name"] = "class",
                        ["value"] = "danger active"
                    }
                };
                await SendJsonAsync(ws, setAttrRequest);
                await ReceiveJsonAsync(ws);

                // 7. Resolve Node to RemoteObject
                var resolveRequest = new JsonObject
                {
                    ["id"] = 7,
                    ["method"] = "DOM.resolveNode",
                    ["params"] = new JsonObject { ["nodeId"] = btnNodeId }
                };
                await SendJsonAsync(ws, resolveRequest);
                var resolveResponse = await ReceiveJsonAsync(ws);
                var objectId = resolveResponse["result"]?["object"]?["objectId"]?.GetValue<string>() ?? "";
                Assert.NotEmpty(objectId);

                // 8. Get Properties of RemoteObject
                var getPropsRequest = new JsonObject
                {
                    ["id"] = 8,
                    ["method"] = "Runtime.getProperties",
                    ["params"] = new JsonObject { ["objectId"] = objectId }
                };
                await SendJsonAsync(ws, getPropsRequest);
                var getPropsResponse = await ReceiveJsonAsync(ws);
                Console.WriteLine($"INTEGRATION_TEST_CLIENT: getProperties response: {getPropsResponse.ToJsonString()}");
                var propsList = getPropsResponse["result"]?["result"] as JsonArray;
                Assert.NotNull(propsList);
                var nameProp = propsList.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "Name");
                Assert.NotNull(nameProp);

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close test", CancellationToken.None);
            });

            while (!clientTask.IsCompleted)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }

            clientTask.GetAwaiter().GetResult();
        }
        finally
        {
            CdpServer.Stop();
            window.Close();
        }
    }
}
