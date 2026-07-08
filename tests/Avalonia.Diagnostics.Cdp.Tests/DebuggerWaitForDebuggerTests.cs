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

public class DebuggerWaitForDebuggerTests
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
        return new ClientWebSocket();
    }

    [AvaloniaFact]
    public async Task TestWaitForDebuggerAndScriptInjection()
    {
        int port = GetFreePort();
        CdpServer.Start(port);
        CdpServer.WaitForDebugger = true;

        var window = new Window { Title = "Original Title" };
        var targetId = CdpServer.GetOrCreateTarget(window).Id;

        try
        {
            var clientTask = Task.Run(async () =>
            {
                // Connect client WebSocket directly using upfront targetId
                var ws = CreateClientWebSocket();
                var uri = new Uri($"ws://127.0.0.1:{port}/devtools/page/{targetId}");
                
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        await ws.ConnectAsync(uri, CancellationToken.None);
                        break;
                    }
                    catch (WebSocketException)
                    {
                        if (i == 9) throw;
                        await Task.Delay(100);
                    }
                }
                Assert.Equal(WebSocketState.Open, ws.State);

                // 3. Register script to evaluate on new document using __raw_window reference
                var addScriptRequest = new JsonObject
                {
                    ["id"] = 1,
                    ["method"] = "Page.addScriptToEvaluateOnNewDocument",
                    ["params"] = new JsonObject
                    {
                        ["source"] = "__raw_window.Title = \"Injected Title\";"
                    }
                };
                var requestBytes = Encoding.UTF8.GetBytes(addScriptRequest.ToJsonString());
                await ws.SendAsync(new ArraySegment<byte>(requestBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                // Receive response for script registration
                var buffer = new byte[4096];
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var responseStr = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var responseNode = JsonNode.Parse(responseStr);
                Assert.NotNull(responseNode?["result"]?["identifier"]);

                // 4. Send Runtime.runIfWaitingForDebugger
                var runRequest = new JsonObject
                {
                    ["id"] = 2,
                    ["method"] = "Runtime.runIfWaitingForDebugger",
                    ["params"] = new JsonObject()
                };
                requestBytes = Encoding.UTF8.GetBytes(runRequest.ToJsonString());
                await ws.SendAsync(new ArraySegment<byte>(requestBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                // Receive response for run
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                responseStr = Encoding.UTF8.GetString(buffer, 0, result.Count);
                responseNode = JsonNode.Parse(responseStr);
                Assert.NotNull(responseNode?["result"]);

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            });

            // Ensure we resume the target if the client task completes (especially if it fails/faults)
            _ = clientTask.ContinueWith(t =>
            {
                CdpServer.ResumeTarget(targetId);
            }, TaskScheduler.Default);

            // This will trigger WindowOpenedEvent, block in PushFrame,
            // and resume only after runIfWaitingForDebugger is called.
            window.Show();

            // Wait for background task to complete
            await clientTask;

            // Verify the injected script executed and changed the title
            Assert.Equal("Injected Title", window.Title);

            // Verify the target is no longer waiting for debugger
            Assert.False(Chrome.DevTools.Protocol.CdpServer.IsTargetWaitingForDebugger(targetId));
        }
        finally
        {
            window.Close();
            CdpServer.Stop();
            CdpServer.WaitForDebugger = false;
            Chrome.DevTools.Protocol.CdpServer.HasWaitedForDebugger = false;
        }
    }

    [AvaloniaFact]
    public async Task TestTargetAutoAttachWithWaitForDebuggerOnStart()
    {
        int port = GetFreePort();
        CdpServer.Start(port);

        // Create an initial window to have a target to connect to
        var initialWindow = new Window { Title = "Initial Window" };
        initialWindow.Show();
        var initialTargetId = CdpServer.GetOrCreateTarget(initialWindow).Id;

        // Create the second window
        var secondWindow = new Window { Title = "Second Window" };

        try
        {
            var clientTask = Task.Run(async () =>
            {
                // 1. Connect client WebSocket to the initial page target with retry
                var ws = CreateClientWebSocket();
                var uri = new Uri($"ws://127.0.0.1:{port}/devtools/page/{initialTargetId}");
                
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        await ws.ConnectAsync(uri, CancellationToken.None);
                        break;
                    }
                    catch (WebSocketException)
                    {
                        if (i == 9) throw;
                        await Task.Delay(100);
                    }
                }
                Assert.Equal(WebSocketState.Open, ws.State);

                // 2. Call Target.setAutoAttach with waitForDebuggerOnStart = true
                var setAutoAttachRequest = new JsonObject
                {
                    ["id"] = 1,
                    ["method"] = "Target.setAutoAttach",
                    ["params"] = new JsonObject
                    {
                        ["autoAttach"] = true,
                        ["waitForDebuggerOnStart"] = true,
                        ["flatten"] = true
                    }
                };
                var requestBytes = Encoding.UTF8.GetBytes(setAutoAttachRequest.ToJsonString());
                await ws.SendAsync(new ArraySegment<byte>(requestBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                // Receive response
                var buffer = new byte[4096];
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var responseStr = Encoding.UTF8.GetString(buffer, 0, result.Count);

                string attachedSessionId = null;
                string attachedTargetId = null;

                // Keep reading until we get the Target.attachedToTarget event for the second window
                while (true)
                {
                    var node = JsonNode.Parse(responseStr);
                    if (node?["method"]?.GetValue<string>() == "Target.attachedToTarget")
                    {
                        var paramsObj = node["params"];
                        if (paramsObj != null)
                        {
                            var targetInfo = paramsObj["targetInfo"];
                            if (targetInfo != null && 
                                targetInfo["title"]?.GetValue<string>() == "Second Window" &&
                                targetInfo["type"]?.GetValue<string>() == "page")
                            {
                                Assert.True(paramsObj["waitingForDebugger"]?.GetValue<bool>());
                                attachedSessionId = paramsObj["sessionId"]?.GetValue<string>();
                                attachedTargetId = targetInfo["targetId"]?.GetValue<string>();
                                break;
                            }
                        }
                    }
                    
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    responseStr = Encoding.UTF8.GetString(buffer, 0, result.Count);
                }

                Assert.NotNull(attachedSessionId);
                Assert.NotNull(attachedTargetId);

                // 3. Register a script on the new target (via its session) using __raw_window
                var addScriptRequest = new JsonObject
                {
                    ["id"] = 1,
                    ["method"] = "Page.addScriptToEvaluateOnNewDocument",
                    ["sessionId"] = attachedSessionId,
                    ["params"] = new JsonObject
                    {
                        ["source"] = "__raw_window.Title = \"Second Window Resumed\";"
                    }
                };
                requestBytes = Encoding.UTF8.GetBytes(addScriptRequest.ToJsonString());
                await ws.SendAsync(new ArraySegment<byte>(requestBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                // Receive response to script registration
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                // 4. Send Runtime.runIfWaitingForDebugger on the new session
                var runRequest = new JsonObject
                {
                    ["id"] = 2,
                    ["method"] = "Runtime.runIfWaitingForDebugger",
                    ["sessionId"] = attachedSessionId,
                    ["params"] = new JsonObject()
                };
                requestBytes = Encoding.UTF8.GetBytes(runRequest.ToJsonString());
                await ws.SendAsync(new ArraySegment<byte>(requestBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                // Receive response to runIfWaitingForDebugger (id = 2), looping past other asynchronous events
                while (true)
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var resp = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var node = JsonNode.Parse(resp);
                    if (node?["id"]?.GetValue<int>() == 2)
                    {
                        break;
                    }
                }

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            });

            // This Show() will trigger the auto-attach, get paused, and resume when the client calls runIfWaitingForDebugger
            secondWindow.Show();

            await clientTask;

            // Verify it was successfully resumed
            var secondTargetId = CdpServer.GetOrCreateTarget(secondWindow).Id;
            Assert.False(Chrome.DevTools.Protocol.CdpServer.IsTargetWaitingForDebugger(secondTargetId));
        }
        finally
        {
            initialWindow.Close();
            secondWindow.Close();
            CdpServer.Stop();
        }
    }
}
