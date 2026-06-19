using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Diagnostics.Cdp;

namespace ControlApp;

public class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}

class Program
{
    private static readonly System.Threading.Channels.Channel<JsonObject> _messageChannel = System.Threading.Channels.Channel.CreateUnbounded<JsonObject>();

    public static void Main(string[] args)
    {
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .AfterSetup(_ =>
            {
                Task.Run(RunE2ETestAsync);
            })
            .StartWithClassicDesktopLifetime(args);
    }

    private static async Task RunE2ETestAsync()
    {
        try
        {
            Console.WriteLine("=== STARTING CDP SCREENCAST E2E VERIFICATION ===");
            
            // 1. Create a window and show it
            Window? window = null;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                window = new Window
                {
                    Title = "E2E Test Window",
                    Width = 400,
                    Height = 300
                };
                window.Show();
            });

            // 2. Start the CDP Server manually on port 9236
            CdpServer.Start(9236);
            var targetId = CdpServer.Register(window!, "E2E Screencast Target");

            // 3. Connect a WebSocket client to the server
            var wsUri = new Uri($"ws://localhost:9236/devtools/page/{targetId}");
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(wsUri, CancellationToken.None);
            Console.WriteLine("WebSocket connected to target.");

            // Start background WebSocket message reader
            _ = Task.Run(async () =>
            {
                var buffer = new byte[65536];
                try
                {
                    while (ws.State == WebSocketState.Open)
                    {
                        using var ms = new MemoryStream();
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                            ms.Write(buffer, 0, result.Count);
                        } while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close) break;

                        var jsonStr = Encoding.UTF8.GetString(ms.ToArray());
                        var node = JsonNode.Parse(jsonStr) as JsonObject;
                        if (node != null)
                        {
                            _messageChannel.Writer.TryWrite(node);
                        }
                    }
                }
                catch { }
            });

            // 4. Start Screencast via CDP
            // format: jpeg, quality: 75, maxWidth: 200, maxHeight: 200, everyNthFrame: 1
            var startCommand = new JsonObject
            {
                ["id"] = 1,
                ["method"] = "Page.startScreencast",
                ["params"] = new JsonObject
                {
                    ["format"] = "jpeg",
                    ["quality"] = 75,
                    ["maxWidth"] = 200,
                    ["maxHeight"] = 200,
                    ["everyNthFrame"] = 1
                }
            };

            await SendCommandAsync(ws, startCommand);
            Console.WriteLine("Sent Page.startScreencast.");

            // 5. Receive frame event from WebSocket
            var firstFrame = await ReceiveScreencastFrameAsync();
            Console.WriteLine("Received screencastFrame event!");

            // Assertions on the frame
            var data = firstFrame["params"]?["data"]?.GetValue<string>();
            if (string.IsNullOrEmpty(data))
            {
                throw new Exception("Assertion failed: screencastFrame data is empty!");
            }

            var metadata = firstFrame["params"]?["metadata"];
            if (metadata == null)
            {
                throw new Exception("Assertion failed: screencastFrame metadata is null!");
            }

            var deviceWidth = metadata["deviceWidth"]?.GetValue<double>();
            var deviceHeight = metadata["deviceHeight"]?.GetValue<double>();
            Console.WriteLine($"Resized bounds in metadata: {deviceWidth}x{deviceHeight}");
            if (deviceWidth != 200)
            {
                throw new Exception($"Assertion failed: Expected deviceWidth 200, got {deviceWidth}");
            }

            var sessionId = firstFrame["params"]?["sessionId"]?.GetValue<int>() ?? 0;
            Console.WriteLine($"Session/Frame ID: {sessionId}");

            // 6. Acknowledge the frame (releasing the backpressure SemaphoreSlim)
            var ackCommand = new JsonObject
            {
                ["id"] = 2,
                ["method"] = "Page.screencastFrameAck",
                ["params"] = new JsonObject
                {
                    ["sessionId"] = sessionId
                }
            };
            await SendCommandAsync(ws, ackCommand);
            Console.WriteLine("Sent Page.screencastFrameAck.");

            // 7. Verify Delta Compression / Change Detection (Duplicate Frame Skipping)
            // Wait 500ms. Since the UI did not change, no screencast frame should be sent.
            Console.WriteLine("Waiting to verify that duplicate frames are skipped...");
            var duplicateFrame = await ReceiveEventWithTimeoutAsync("Page.screencastFrame", 500);
            if (duplicateFrame != null)
            {
                throw new Exception("Assertion failed: Duplicate frame was not skipped by delta compression!");
            }
            Console.WriteLine("Verified: Duplicate frame was successfully skipped (delta compression works!).");

            // 8. Trigger visual update by changing window background and requesting a frame
            Console.WriteLine("Changing window background to trigger a new screencast frame...");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                window!.Background = Avalonia.Media.Brushes.Red;
                window!.InvalidateVisual();
                Console.WriteLine($"[ControlApp Debug] Active Sessions Count: {System.Linq.Enumerable.Count(CdpServer.Sessions)}");
                foreach (var sessionItem in CdpServer.Sessions)
                {
                    sessionItem.RequestScreencastFrame();
                }
            });

            // 9. Verify that a new frame (with changes) is sent and received
            var secondFrame = await ReceiveEventWithTimeoutAsync("Page.screencastFrame", 2000);
            if (secondFrame == null)
            {
                throw new Exception("Assertion failed: Failed to receive second screencast frame after visual/layout update!");
            }
            Console.WriteLine("Received second screencast frame after layout update!");

            var secondSessionId = secondFrame["params"]?["sessionId"]?.GetValue<int>() ?? 0;
            Console.WriteLine($"Second Session/Frame ID: {secondSessionId}");
            if (secondSessionId <= sessionId)
            {
                throw new Exception($"Assertion failed: Expected second session ID to be greater than {sessionId}, got {secondSessionId}");
            }

            // 10. Acknowledge the second frame
            var ackCommand2 = new JsonObject
            {
                ["id"] = 3,
                ["method"] = "Page.screencastFrameAck",
                ["params"] = new JsonObject
                {
                    ["sessionId"] = secondSessionId
                }
            };
            await SendCommandAsync(ws, ackCommand2);
            Console.WriteLine("Sent second Page.screencastFrameAck.");

            // 11. Verify visibility change event
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                window!.IsVisible = false;
            });

            var visibilityEvent = await ReceiveVisibilityEventAsync();
            Console.WriteLine("Received Page.screencastVisibilityChanged event!");
            var visible = visibilityEvent["params"]?["visible"]?.GetValue<bool>();
            Console.WriteLine($"Visibility state: {visible}");
            if (visible != false)
            {
                throw new Exception("Assertion failed: expected visible to be false!");
            }

            // 12. Stop Screencast
            var stopCommand = new JsonObject
            {
                ["id"] = 4,
                ["method"] = "Page.stopScreencast",
                ["params"] = new JsonObject()
            };
            await SendCommandAsync(ws, stopCommand);
            Console.WriteLine("Sent Page.stopScreencast.");

            // Clean up
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close test", CancellationToken.None);
            CdpServer.Stop();

            Console.WriteLine("=== E2E SCREENCAST VERIFICATION SUCCESSFUL! ===");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"=== E2E VERIFICATION FAILED: {ex.Message} ===");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    private static async Task SendCommandAsync(ClientWebSocket ws, JsonObject command)
    {
        var json = command.ToJsonString();
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<JsonObject> ReceiveEventAsync(string methodName)
    {
        var result = await ReceiveEventWithTimeoutAsync(methodName, Timeout.Infinite);
        if (result == null)
        {
            throw new Exception($"Timed out or socket closed waiting for event {methodName}");
        }
        return result;
    }

    private static async Task<JsonObject?> ReceiveEventWithTimeoutAsync(string methodName, int timeoutMs)
    {
        using var cts = (timeoutMs == Timeout.Infinite) ? new CancellationTokenSource() : new CancellationTokenSource(timeoutMs);
        try
        {
            while (await _messageChannel.Reader.WaitToReadAsync(cts.Token))
            {
                while (_messageChannel.Reader.TryRead(out var node))
                {
                    if (node != null && node.ContainsKey("method") && node["method"]?.GetValue<string>() == methodName)
                    {
                        return node;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            return null; // Timed out
        }
        return null;
    }

    private static Task<JsonObject> ReceiveScreencastFrameAsync()
        => ReceiveEventAsync("Page.screencastFrame");

    private static Task<JsonObject> ReceiveVisibilityEventAsync()
        => ReceiveEventAsync("Page.screencastVisibilityChanged");
}
