using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace CdpExamples;

public class E2ETestExample
{
    private static int _nextId = 1;

    public static async Task RunAsync()
    {
        // 1. Connect to CdpSampleApp WebSocket url (retrieved from http://localhost:9222/json)
        string targetWsUrl = "ws://localhost:9222/devtools/page/some-target-id";
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(targetWsUrl), CancellationToken.None);

        // Start listening to incoming WebSocket events
        _ = Task.Run(() => ReceiveLoopAsync(ws));

        // 2. Enable DOM domain
        Console.WriteLine("Enabling DOM...");
        await SendCommandAsync(ws, "DOM.enable", new JsonObject());
        await SendCommandAsync(ws, "DOM.getDocument", new JsonObject());

        // 3. Find node ID of a button matching selector "#btnClickMe"
        Console.WriteLine("Locating button #btnClickMe...");
        var queryRes = await SendCommandAsync(ws, "DOM.querySelector", new JsonObject
        {
            ["nodeId"] = 1, // root document node ID
            ["selector"] = "#btnClickMe"
        });
        int buttonNodeId = queryRes["nodeId"]?.GetValue<int>() ?? 0;

        if (buttonNodeId == 0)
        {
            Console.WriteLine("Button not found!");
            return;
        }

        // 4. Retrieve button center coordinates via DOM.getBoxModel
        var boxRes = await SendCommandAsync(ws, "DOM.getBoxModel", new JsonObject
        {
            ["nodeId"] = buttonNodeId
        });
        var contentQuad = boxRes["model"]?["content"] as JsonArray;
        if (contentQuad != null)
        {
            double x1 = contentQuad[0]!.GetValue<double>();
            double y1 = contentQuad[1]!.GetValue<double>();
            double x3 = contentQuad[4]!.GetValue<double>();
            double y3 = contentQuad[5]!.GetValue<double>();
            double centerX = (x1 + x3) / 2;
            double centerY = (y1 + y3) / 2;

            // 5. Dispatch Mouse Pressed and Released events to click the button
            Console.WriteLine($"Clicking button at ({centerX}, {centerY})...");
            await SendCommandAsync(ws, "Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mousePressed",
                ["x"] = centerX,
                ["y"] = centerY,
                ["button"] = "left",
                ["clickCount"] = 1
            });
            await Task.Delay(100);
            await SendCommandAsync(ws, "Input.dispatchMouseEvent", new JsonObject
            {
                ["type"] = "mouseReleased",
                ["x"] = centerX,
                ["y"] = centerY,
                ["button"] = "left",
                ["clickCount"] = 1
            });
        }

        // 6. Capture screenshot to verify outcome
        Console.WriteLine("Capturing screenshot...");
        var screenshotRes = await SendCommandAsync(ws, "Page.captureScreenshot", new JsonObject());
        string base64Png = screenshotRes["data"]?.GetValue<string>() ?? "";
        byte[] imageBytes = Convert.FromBase64String(base64Png);
        System.IO.File.WriteAllBytes("button_clicked.png", imageBytes);
        Console.WriteLine("Screenshot saved to button_clicked.png");

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    private static readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> _pendingRequests = new();

    private static async Task<JsonObject> SendCommandAsync(ClientWebSocket ws, string method, JsonObject parameters)
    {
        int id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonObject>();
        _pendingRequests[id] = tcs;

        var request = new JsonObject
        {
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters
        };

        var bytes = Encoding.UTF8.GetBytes(request.ToJsonString());
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

        return await tcs.Task;
    }

    private static async Task ReceiveLoopAsync(ClientWebSocket ws)
    {
        var buffer = new byte[65536];
        var ms = new System.IO.MemoryStream();
        while (ws.State == WebSocketState.Open)
        {
            try
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;

                string msg = Encoding.UTF8.GetString(ms.ToArray());
                Console.WriteLine($"[RECEIVED] {msg}");

                var responseNode = JsonNode.Parse(msg);
                if (responseNode is JsonObject jsonObj && jsonObj.TryGetPropertyValue("id", out var idNode) && idNode != null)
                {
                    int responseId = idNode.GetValue<int>();
                    if (_pendingRequests.TryRemove(responseId, out var tcs))
                    {
                        if (jsonObj.TryGetPropertyValue("result", out var resultNode) && resultNode is JsonObject resultObj)
                        {
                            tcs.TrySetResult((JsonObject)resultObj.DeepClone());
                        }
                        else
                        {
                            tcs.TrySetResult(new JsonObject());
                        }
                    }
                }
            }
            catch { break; }
        }
    }
}
