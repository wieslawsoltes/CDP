using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class CdpIntegrationTests
{
    [AvaloniaFact]
    public void TestSelectorEngineFindsBtnClickMe()
    {
        var window = new Avalonia.Controls.Window();
        var button = new Avalonia.Controls.Button { Name = "btnClickMe" };
        var panel = new Avalonia.Controls.StackPanel();
        panel.Children.Add(button);
        window.Content = panel;
        window.Show();

        var foundButton = SelectorEngine.QuerySelector(window, "#btnClickMe");
        Assert.NotNull(foundButton);
        Assert.Same(button, foundButton);
    }

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
        ws.Options.DangerousDeflateOptions = new WebSocketDeflateOptions
        {
            ClientMaxWindowBits = 15,
            ServerMaxWindowBits = 15
        };
        return ws;
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
                var ws = CreateClientWebSocket();
                try
                {
                    Console.WriteLine("INTEGRATION_TEST_CLIENT: Initializing ClientWebSocket");
                    var uri = new Uri($"ws://127.0.0.1:{port}/devtools/page/{id}");
                    Console.WriteLine($"INTEGRATION_TEST_CLIENT: Connecting to {uri}");
                    await ConnectWithTimeoutAsync(ws, uri);
                    Console.WriteLine("INTEGRATION_TEST_CLIENT: Connected successfully!");
                    Assert.Equal(WebSocketState.Open, ws.State);

                    // Send DOM.getDocument request
                    var request = new JsonObject
                    {
                        ["id"] = 1,
                        ["method"] = "DOM.getDocument",
                        ["params"] = new JsonObject { ["pierce"] = true }
                    };

                    Console.WriteLine("INTEGRATION_TEST_CLIENT: Sending DOM.getDocument request");
                    var requestBytes = Encoding.UTF8.GetBytes(request.ToJsonString());
                    await ws.SendAsync(new ArraySegment<byte>(requestBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine("INTEGRATION_TEST_CLIENT: Sent request successfully");

                    // Receive entire response
                    using var ms = new MemoryStream();
                    var buffer = new byte[4096];
                    WebSocketReceiveResult result;
                    using var recvCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    do
                    {
                        Console.WriteLine("INTEGRATION_TEST_CLIENT: Awaiting ReceiveAsync");
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), recvCts.Token);
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

                    var children = rootNode["children"] as JsonArray;
                    Assert.NotNull(children);
                    Assert.NotEmpty(children);
                    var windowNode = children[0] as JsonObject;
                    Assert.NotNull(windowNode);
                    Assert.Equal("Window", windowNode["nodeName"]?.GetValue<string>());
                    Assert.Equal("Window", windowNode["localName"]?.GetValue<string>());

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
                finally
                {
                    ws.Dispose();
                }
            });

            // Pump dispatcher queue on UI thread until client task completes
            PumpDispatcher(clientTask);
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
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
        int port = GetFreePort();

        try
        {
            CdpServer.Start(port);

            var clientTask = Task.Run(async () =>
            {
                using var ws = CreateClientWebSocket();
                var uri = new Uri($"ws://127.0.0.1:{port}/devtools/page/{id}");
                await ConnectWithTimeoutAsync(ws, uri);

                // 1. Get Document
                var request = new JsonObject
                {
                    ["id"] = 1,
                    ["method"] = "DOM.getDocument",
                    ["params"] = new JsonObject { ["pierce"] = true }
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

                // 5b. Evaluate modification of Opacity using $0
                var evalOpacityRequest = new JsonObject
                {
                    ["id"] = 50,
                    ["method"] = "Runtime.evaluate",
                    ["params"] = new JsonObject
                    {
                        ["expression"] = "$0.Opacity = 0.5;",
                        ["returnByValue"] = true
                    }
                };
                await SendJsonAsync(ws, evalOpacityRequest);
                var evalOpacityResponse = await ReceiveJsonAsync(ws);
                Assert.Null(evalOpacityResponse["exceptionDetails"]);

                // 5c. Evaluate modification of Content using ((Button)$0).Content
                var evalContentRequest = new JsonObject
                {
                    ["id"] = 51,
                    ["method"] = "Runtime.evaluate",
                    ["params"] = new JsonObject
                    {
                        ["expression"] = "((Button)$0).Content = \"New Content\";",
                        ["returnByValue"] = true
                    }
                };
                await SendJsonAsync(ws, evalContentRequest);
                var evalContentResponse = await ReceiveJsonAsync(ws);
                Assert.Null(evalContentResponse["exceptionDetails"]);

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

            PumpDispatcher(clientTask);

            Assert.Equal(0.5, button.Opacity);
            Assert.Equal("New Content", button.Content);
        }
        finally
        {
            CdpServer.Stop();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TestExtendedCdpFeatures()
    {
        var button = new Button { Name = "testBtn" };
        var window = new Window { Title = "Extended Features Test Window", Content = button };
        window.Show();
        var id = CdpServer.Register(window, "Extended Features Window");
        int port = GetFreePort();

        try
        {
            CdpServer.Start(port);

            var clientTask = Task.Run(async () =>
            {
                using var ws = CreateClientWebSocket();
                var uri = new Uri($"ws://127.0.0.1:{port}/devtools/page/{id}");
                await ConnectWithTimeoutAsync(ws, uri);

                // 1. Get Document
                var request = new JsonObject
                {
                    ["id"] = 1,
                    ["method"] = "DOM.getDocument",
                    ["params"] = new JsonObject { ["pierce"] = true }
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
                var btnNodeId = qResponse["result"]?["nodeId"]?.GetValue<int>() ?? 0;
                Assert.True(btnNodeId > 1);

                // 3. Test CSS.getComputedStyleForNode
                var compRequest = new JsonObject
                {
                    ["id"] = 3,
                    ["method"] = "CSS.getComputedStyleForNode",
                    ["params"] = new JsonObject { ["nodeId"] = btnNodeId }
                };
                await SendJsonAsync(ws, compRequest);
                var compResponse = await ReceiveJsonAsync(ws);
                Assert.Equal(3, compResponse["id"]?.GetValue<int>());
                var computedStyles = compResponse["result"]?["computedStyle"] as JsonArray;
                Assert.NotNull(computedStyles);
                var widthProp = computedStyles.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "width");
                Assert.NotNull(widthProp);

                // 4. Test Overlay.setInspectMode
                var setInspectModeRequest = new JsonObject
                {
                    ["id"] = 4,
                    ["method"] = "Overlay.setInspectMode",
                    ["params"] = new JsonObject
                    {
                        ["mode"] = "searchForNode",
                        ["highlightConfig"] = new JsonObject()
                    }
                };
                await SendJsonAsync(ws, setInspectModeRequest);
                var setInspectModeResponse = await ReceiveJsonAsync(ws);
                Assert.Equal(4, setInspectModeResponse["id"]?.GetValue<int>());

                // 5. Test Page.reload
                var reloadRequest = new JsonObject
                {
                    ["id"] = 5,
                    ["method"] = "Page.reload",
                    ["params"] = new JsonObject()
                };
                await SendJsonAsync(ws, reloadRequest);
                var reloadResponse = await ReceiveJsonAsync(ws);
                Assert.Equal(5, reloadResponse["id"]?.GetValue<int>());

                // 6. Test Input.dispatchMouseEvent (mouseWheel)
                var scrollRequest = new JsonObject
                {
                    ["id"] = 6,
                    ["method"] = "Input.dispatchMouseEvent",
                    ["params"] = new JsonObject
                    {
                        ["type"] = "mouseWheel",
                        ["x"] = 10,
                        ["y"] = 10,
                        ["button"] = "none",
                        ["deltaX"] = 0,
                        ["deltaY"] = 100
                    }
                };
                await SendJsonAsync(ws, scrollRequest);
                var scrollResponse = await ReceiveJsonAsync(ws);
                Assert.Equal(6, scrollResponse["id"]?.GetValue<int>());

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close test", CancellationToken.None);
            });

            PumpDispatcher(clientTask);
        }
        finally
        {
            CdpServer.Stop();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TestDOMDebuggerAndMemoryDomains()
    {
        var button = new Button { Name = "inspectBtn" };
        button.Click += (s, e) => { }; // Register click event handler
        var window = new Window { Title = "Event Debugger Test Window", Content = button };
        window.Show();
        var id = CdpServer.Register(window, "Debugger Window");
        int port = GetFreePort();

        try
        {
            CdpServer.Start(port);

            var clientTask = Task.Run(async () =>
            {
                using var ws = CreateClientWebSocket();
                var uri = new Uri($"ws://127.0.0.1:{port}/devtools/page/{id}");
                await ConnectWithTimeoutAsync(ws, uri);

                // 1. Get Document
                var docRes = await SendJsonAndReceiveAsync(ws, "DOM.getDocument", new JsonObject { ["pierce"] = true });
                
                // 2. Query Selector for Button
                var qRes = await SendJsonAndReceiveAsync(ws, "DOM.querySelector", new JsonObject
                {
                    ["nodeId"] = 1,
                    ["selector"] = "ContentPresenter > Button"
                });
                var btnNodeId = qRes["result"]?["nodeId"]?.GetValue<int>() ?? 0;
                Assert.True(btnNodeId > 1);

                // 3. Resolve Node to get objectId
                var resolveRes = await SendJsonAndReceiveAsync(ws, "DOM.resolveNode", new JsonObject { ["nodeId"] = btnNodeId });
                var objectId = resolveRes["result"]?["object"]?["objectId"]?.GetValue<string>() ?? "";
                Assert.NotEmpty(objectId);

                // 4. Get Event Listeners
                var listenersRes = await SendJsonAndReceiveAsync(ws, "DOMDebugger.getEventListeners", new JsonObject { ["objectId"] = objectId });
                var listeners = listenersRes["result"]?["listeners"] as JsonArray;
                Assert.NotNull(listeners);
                Assert.NotEmpty(listeners); // Button Click handler should be returned
                var clickListener = listeners.FirstOrDefault(l => string.Equals(l?["type"]?.GetValue<string>(), "click", StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(clickListener);
                var handlerDesc = clickListener["handler"]?["description"]?.GetValue<string>() ?? "";
                Assert.Contains("TestDOMDebuggerAndMemoryDomains", handlerDesc); // Target className or handler details should match

                // 5. Get DOM Counters (Memory)
                var countersRes = await SendJsonAndReceiveAsync(ws, "Memory.getDOMCounters", new JsonObject());
                Assert.True(countersRes["result"]?["documents"]?.GetValue<int>() >= 1);
                Assert.True(countersRes["result"]?["nodes"]?.GetValue<int>() > 1);

                // 6. Remove Node
                var removeRes = await SendJsonAndReceiveAsync(ws, "DOM.removeNode", new JsonObject { ["nodeId"] = btnNodeId });
                Assert.NotNull(removeRes);
                Assert.False(removeRes.ContainsKey("error"));

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close test", CancellationToken.None);
            });

            PumpDispatcher(clientTask);
        }
        finally
        {
            CdpServer.Stop();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TestAccessibilityAutomationTreeAndProperties()
    {
        var checkBox = new CheckBox { Name = "chkOption", IsChecked = true };
        var slider = new Slider { Name = "sldValue", Minimum = 10, Maximum = 100, Value = 42 };
        var textBox = new TextBox { Name = "txtInput", Text = "Automation test!" };
        AutomationProperties.SetIsRequiredForForm(textBox, true);

        var panel = new StackPanel { Children = { checkBox, slider, textBox } };
        var window = new Window { Title = "AX Integration Window", Content = panel };
        window.Show();

        var id = CdpServer.Register(window, "AX Window");
        int port = GetFreePort();

        try
        {
            CdpServer.Start(port);

            var clientTask = Task.Run(async () =>
            {
                using var ws = CreateClientWebSocket();
                var uri = new Uri($"ws://127.0.0.1:{port}/devtools/page/{id}");
                await ConnectWithTimeoutAsync(ws, uri);

                // 1. Get Document to populate node map
                await SendJsonAndReceiveAsync(ws, "DOM.getDocument", new JsonObject { ["pierce"] = true });

                // 2. Query selector for TextBox to get its DOM nodeId
                var qRes = await SendJsonAndReceiveAsync(ws, "DOM.querySelector", new JsonObject
                {
                    ["nodeId"] = 1,
                    ["selector"] = "TextBox"
                });
                int txtNodeId = qRes["result"]?["nodeId"]?.GetValue<int>() ?? 0;
                Assert.True(txtNodeId > 1);

                // 3. Get Full AX Tree
                var fullAXRes = await SendJsonAndReceiveAsync(ws, "Accessibility.getFullAXTree", new JsonObject());
                var nodes = fullAXRes["result"]?["nodes"] as JsonArray;
                Assert.NotNull(nodes);
                Assert.NotEmpty(nodes);

                // Find the TextBox node in full AX tree
                var txtAXNode = nodes.FirstOrDefault(n => n?["backendDOMNodeId"]?.GetValue<int>() == txtNodeId) as JsonObject;
                Assert.NotNull(txtAXNode);
                var tbProps = txtAXNode["properties"] as JsonArray;
                Assert.NotNull(tbProps);
                var reqProp = tbProps.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "required");
                Assert.NotNull(reqProp);
                Assert.True(reqProp["value"]?["value"]?.GetValue<bool>());

                // Find the CheckBox node in full AX tree
                var chkAXNode = nodes.FirstOrDefault(n => n?["role"]?["value"]?.GetValue<string>() == "checkbox") as JsonObject;
                Assert.NotNull(chkAXNode);
                var chkProps = chkAXNode["properties"] as JsonArray;
                Assert.NotNull(chkProps);
                var checkedProp = chkProps.FirstOrDefault(p => p?["name"]?.GetValue<string>() == "checked");
                Assert.NotNull(checkedProp);
                Assert.Equal("true", checkedProp["value"]?["value"]?.GetValue<string>());

                // 4. Test getPartialAXTree with fetchRelatives
                var partialAXRes = await SendJsonAndReceiveAsync(ws, "Accessibility.getPartialAXTree", new JsonObject
                {
                    ["nodeId"] = txtNodeId,
                    ["fetchRelatives"] = true
                });
                var partialNodes = partialAXRes["result"]?["nodes"] as JsonArray;
                Assert.NotNull(partialNodes);
                Assert.NotEmpty(partialNodes);

                // Sibling slider should be included in relatives
                var sldAXNode = partialNodes.FirstOrDefault(n => n?["role"]?["value"]?.GetValue<string>() == "slider");
                Assert.NotNull(sldAXNode);

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close test", CancellationToken.None);
            });

            PumpDispatcher(clientTask);
        }
        finally
        {
            CdpServer.Stop();
            window.Close();
        }
    }

    private static async Task<JsonObject> SendJsonAndReceiveAsync(WebSocket ws, string method, JsonObject parameters)
    {
        var request = new JsonObject
        {
            ["id"] = 100,
            ["method"] = method,
            ["params"] = parameters
        };
        await SendJsonAsync(ws, request);
        return await ReceiveJsonAsync(ws);
    }

    private static void PumpDispatcher(Task task, int timeoutMs = 15000)
    {
        int loopCount = 0;
        int maxLoops = timeoutMs / 10;
        while (!task.IsCompleted)
        {
            loopCount++;
            if (loopCount > maxLoops)
            {
                throw new TimeoutException($"Integration test timed out waiting for task to complete after {timeoutMs} ms.");
            }
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        task.GetAwaiter().GetResult();
    }

    [AvaloniaFact]
    public void TestSourcesSearchInWorkspace()
    {
        var window = new Window { Title = "Sources Search Window" };
        window.Show();
        var id = CdpServer.Register(window, "Sources Search Window");
        int port = GetFreePort();

        try
        {
            CdpServer.Start(port);

            var clientTask = Task.Run(async () =>
            {
                using var ws = CreateClientWebSocket();
                var uri = new Uri($"ws://127.0.0.1:{port}/devtools/page/{id}");
                await ConnectWithTimeoutAsync(ws, uri);

                // 1. Perform a case-sensitive search for a unique string in the workspace
                var searchParamsCase = new JsonObject
                {
                    ["query"] = "TestSourcesSearchInWorkspace",
                    ["caseSensitive"] = true
                };
                var searchResCase = await SendJsonAndReceiveAsync(ws, "Sources.searchInWorkspace", searchParamsCase);
                Assert.NotNull(searchResCase);
                Assert.False(searchResCase.ContainsKey("error"), searchResCase.ToJsonString());
                
                var resultObjCase = searchResCase["result"] as JsonObject;
                Assert.NotNull(resultObjCase);
                var matchesCase = resultObjCase["matches"] as JsonArray;
                Assert.NotNull(matchesCase);
                Assert.NotEmpty(matchesCase);
                
                // Verify structure of matches
                foreach (var matchNode in matchesCase)
                {
                    var match = matchNode as JsonObject;
                    Assert.NotNull(match);
                    Assert.NotNull(match["path"]?.GetValue<string>());
                    Assert.True(match["lineNumber"]?.GetValue<int>() > 0);
                    Assert.NotNull(match["lineContent"]?.GetValue<string>());
                }

                // 2. Perform a case-insensitive search for a lowercase version
                var searchParamsInsensitive = new JsonObject
                {
                    ["query"] = "testsourcessearchinworkspace",
                    ["caseSensitive"] = false
                };
                var searchResInsensitive = await SendJsonAndReceiveAsync(ws, "Sources.searchInWorkspace", searchParamsInsensitive);
                Assert.NotNull(searchResInsensitive);
                var resultObjInsensitive = searchResInsensitive["result"] as JsonObject;
                Assert.NotNull(resultObjInsensitive);
                var matchesInsensitive = resultObjInsensitive["matches"] as JsonArray;
                Assert.NotNull(matchesInsensitive);
                Assert.NotEmpty(matchesInsensitive);

                // 3. Perform a case-sensitive search with mismatching case which should find nothing
                var searchParamsMismatch = new JsonObject
                {
                    ["query"] = "testsourcessearchinworkspace",
                    ["caseSensitive"] = true
                };
                var searchResMismatch = await SendJsonAndReceiveAsync(ws, "Sources.searchInWorkspace", searchParamsMismatch);
                Assert.NotNull(searchResMismatch);
                var resultObjMismatch = searchResMismatch["result"] as JsonObject;
                Assert.NotNull(resultObjMismatch);
                var matchesMismatch = resultObjMismatch["matches"] as JsonArray;
                Assert.NotNull(matchesMismatch);
                
                bool containsMethodName = false;
                foreach (var matchNode in matchesMismatch)
                {
                    var content = matchNode?["lineContent"]?.GetValue<string>() ?? "";
                    if (content.Contains("TestSourcesSearchInWorkspace"))
                    {
                        containsMethodName = true;
                    }
                }
                Assert.False(containsMethodName);

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close test", CancellationToken.None);
            });

            PumpDispatcher(clientTask);
        }
        finally
        {
            CdpServer.Stop();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TestTargetAutoAttachAndRouting()
    {
        var window = new Window { Title = "Target Auto Attach Main" };
        window.Show();
        var mainTargetId = CdpServer.Register(window, "Main Page Target");
        int port = GetFreePort();

        try
        {
            CdpServer.Start(port);

            var clientTask = Task.Run(async () =>
            {
                using var ws = CreateClientWebSocket();
                var uri = new Uri($"ws://127.0.0.1:{port}/devtools/browser");
                await ConnectWithTimeoutAsync(ws, uri);

                // 1. Send Target.setAutoAttach
                var setAutoAttachRequest = new JsonObject
                {
                    ["id"] = 1,
                    ["method"] = "Target.setAutoAttach",
                    ["params"] = new JsonObject
                    {
                        ["autoAttach"] = true,
                        ["waitForDebuggerOnStart"] = false,
                        ["flatten"] = true
                    }
                };
                await SendJsonAsync(ws, setAutoAttachRequest);

                // Receive setAutoAttach response and attachedToTarget events
                JsonObject? setAutoAttachResponse = null;
                JsonObject? mainTargetAttachedEvent = null;

                for (int i = 0; i < 50; i++)
                {
                    var msg = await ReceiveJsonAsync(ws);
                    if (msg.ContainsKey("id") && msg["id"]?.GetValue<int>() == 1)
                    {
                        setAutoAttachResponse = msg;
                    }
                    else if (msg.ContainsKey("method") && msg["method"]?.GetValue<string>() == "Target.attachedToTarget")
                    {
                        var targetInfo = msg["params"]?["targetInfo"] as JsonObject;
                        if (targetInfo?["targetId"]?.GetValue<string>() == mainTargetId)
                        {
                            mainTargetAttachedEvent = msg;
                        }
                    }

                    if (setAutoAttachResponse != null && mainTargetAttachedEvent != null)
                    {
                        break;
                    }
                }

                Assert.NotNull(setAutoAttachResponse);
                Assert.NotNull(mainTargetAttachedEvent);

                var sessionId = mainTargetAttachedEvent["params"]?["sessionId"]?.GetValue<string>();
                Assert.False(string.IsNullOrEmpty(sessionId));

                // 2. Query DOM.getDocument through the session ID
                var docRequest = new JsonObject
                {
                    ["id"] = 2,
                    ["sessionId"] = sessionId,
                    ["method"] = "DOM.getDocument",
                    ["params"] = new JsonObject { ["pierce"] = true }
                };
                await SendJsonAsync(ws, docRequest);

                var docResponse = await ReceiveJsonAsync(ws);
                Assert.NotNull(docResponse);
                Assert.Equal(2, docResponse["id"]?.GetValue<int>());
                Assert.Equal(sessionId, docResponse["sessionId"]?.GetValue<string>());
                Assert.NotNull(docResponse["result"]?["root"]);

                // 3. Register a new target and verify it gets auto-attached
                var secondWindow = await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var w = new Window { Title = "Target Auto Attach Second" };
                    w.Show();
                    return w;
                });

                string? secondTargetId = null;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    secondTargetId = CdpServer.Register(secondWindow, "Second Page Target");
                });

                JsonObject? secondTargetAttachedEvent = null;
                for (int i = 0; i < 50; i++)
                {
                    var msg = await ReceiveJsonAsync(ws);
                    if (msg.ContainsKey("method") && msg["method"]?.GetValue<string>() == "Target.attachedToTarget")
                    {
                        var targetInfo = msg["params"]?["targetInfo"] as JsonObject;
                        if (targetInfo?["targetId"]?.GetValue<string>() == secondTargetId)
                        {
                            secondTargetAttachedEvent = msg;
                            break;
                        }
                    }
                }

                Assert.NotNull(secondTargetAttachedEvent);
                var secondSessionId = secondTargetAttachedEvent["params"]?["sessionId"]?.GetValue<string>();
                Assert.False(string.IsNullOrEmpty(secondSessionId));
                Assert.NotEqual(sessionId, secondSessionId);

                await Dispatcher.UIThread.InvokeAsync(() => secondWindow.Close());

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close test", CancellationToken.None);
            });

            PumpDispatcher(clientTask);
        }
        finally
        {
            CdpServer.Stop();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TestPreflightWaitForDebuggerAndScriptInjection()
    {
        int port = GetFreePort();
        Window? window = null;
        var targetIdSource = new TaskCompletionSource<string>();

        try
        {
            CdpServer.WaitForDebugger = true;
            Chrome.DevTools.Protocol.CdpServer.HasWaitedForDebugger = false;
            CdpServer.Start(port);

            var clientTask = Task.Run(async () =>
            {
                var targetId = await targetIdSource.Task;

                using var ws = CreateClientWebSocket();
                var uri = new Uri($"ws://127.0.0.1:{port}/devtools/page/{targetId}");
                await ConnectWithTimeoutAsync(ws, uri);

                // Add pre-flight script to inject
                var addScriptRequest = new JsonObject
                {
                    ["id"] = 1,
                    ["method"] = "Page.addScriptToEvaluateOnNewDocument",
                    ["params"] = new JsonObject
                    {
                        ["source"] = "__raw_window.Title = \"Preflight Success\";"
                    }
                };
                await SendJsonAsync(ws, addScriptRequest);
                var addScriptResponse = await ReceiveJsonAsync(ws);
                Assert.Equal(1, addScriptResponse["id"]?.GetValue<int>());

                // Run pre-flight script and resume
                var runRequest = new JsonObject
                {
                    ["id"] = 2,
                    ["method"] = "Runtime.runIfWaitingForDebugger"
                };
                await SendJsonAsync(ws, runRequest);
                var runResponse = await ReceiveJsonAsync(ws);
                Assert.Equal(2, runResponse["id"]?.GetValue<int>());

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close test", CancellationToken.None);
            });

            // Ensure we resume the target if the client task completes (especially if it fails/faults)
            _ = clientTask.ContinueWith(async t =>
            {
                try
                {
                    var tid = await targetIdSource.Task;
                    CdpServer.ResumeTarget(tid);
                }
                catch { }
            }, TaskScheduler.Default);

            // Post window creation and show to dispatcher queue
            Dispatcher.UIThread.Post(() =>
            {
                window = new Window { Title = "Initial Preflight Title" };
                var tid = CdpServer.GetOrCreateTarget(window).Id;
                targetIdSource.SetResult(tid);
                window.Show();
            });

            // Pump dispatcher until the background client completes
            PumpDispatcher(clientTask);

            // Assertions
            Assert.NotNull(window);
            Assert.Equal("Preflight Success", window.Title);
        }
        finally
        {
            CdpServer.Stop();
            CdpServer.WaitForDebugger = false;
            Chrome.DevTools.Protocol.CdpServer.HasWaitedForDebugger = false;
            if (window != null)
            {
                window.Close();
            }
        }
    }

    private static async Task ConnectWithTimeoutAsync(ClientWebSocket ws, Uri uri, int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        await ws.ConnectAsync(uri, cts.Token);
    }
}

