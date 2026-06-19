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
            Console.WriteLine("=== STARTING TASK-SPECIFIC INPUTS & GESTURES E2E VERIFICATION ===");
            
            // 1. Create a window and controls
            Window? window = null;
            TextBox? textBox = null;
            Border? border = null;
            Button? button = null;
            ScrollViewer? scrollViewer = null;

            bool touchPressed = false;
            int touchPressCount = 0;
            bool tapped = false;
            double scrollDeltaY = 0;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                textBox = new TextBox
                {
                    Width = 100,
                    Height = 50,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
                };
                border = new Border
                {
                    Width = 200,
                    Height = 100,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Background = Avalonia.Media.Brushes.Red
                };
                border.PointerPressed += (s, e) =>
                {
                    if (e.Pointer.Type == Avalonia.Input.PointerType.Touch)
                    {
                        touchPressed = true;
                        touchPressCount++;
                    }
                };
                button = new Button
                {
                    Width = 100,
                    Height = 50,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Content = "Tap Target"
                };
                button.Click += (s, e) => tapped = true;

                scrollViewer = new ScrollViewer
                {
                    Width = 200,
                    Height = 200,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Content = new Canvas { Width = 1000, Height = 1000 }
                };
                scrollViewer.ScrollChanged += (s, e) =>
                {
                    scrollDeltaY = scrollViewer.Offset.Y;
                };

                var containerCanvas = new Canvas { Width = 400, Height = 500 };
                Canvas.SetLeft(textBox, 0); Canvas.SetTop(textBox, 0);
                Canvas.SetLeft(border, 150); Canvas.SetTop(border, 0);
                Canvas.SetLeft(button, 0); Canvas.SetTop(button, 100);
                Canvas.SetLeft(scrollViewer, 0); Canvas.SetTop(scrollViewer, 200);

                containerCanvas.Children.Add(textBox);
                containerCanvas.Children.Add(border);
                containerCanvas.Children.Add(button);
                containerCanvas.Children.Add(scrollViewer);

                window = new Window
                {
                    Title = "E2E Keyboard and Gesture Test Window",
                    Width = 400,
                    Height = 500,
                    WindowDecorations = WindowDecorations.None,
                    Content = containerCanvas
                };
                window.Show();
                window.Activate();
            });

            // Wait for visual tree to arrange
            await Task.Delay(200);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                window!.Measure(new Size(400, 500));
                window!.Arrange(new Rect(0, 0, 400, 500));
            });

            // 2. Start the CDP Server manually on port 9236
            CdpServer.Start(9236);
            var targetId = CdpServer.Register(window!, "E2E Target");

            // 3. Connect a WebSocket client to the server
            var wsUri = new Uri($"ws://localhost:9236/devtools/page/{targetId}");
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(wsUri, CancellationToken.None);
            Console.WriteLine("WebSocket connected to E2E target.");

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

            // 4. Test Keyboard Typing
            Console.WriteLine("Testing Keyboard input typing...");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                textBox!.Focus();
            });
            await Task.Delay(50);

            var keyDownCommand = new JsonObject
            {
                ["id"] = 10,
                ["method"] = "Input.dispatchKeyEvent",
                ["params"] = new JsonObject
                {
                    ["type"] = "rawKeyDown",
                    ["key"] = "KeyA",
                    ["text"] = "a",
                    ["modifiers"] = 0
                }
            };
            await SendCommandAsync(ws, keyDownCommand);

            var charCommand = new JsonObject
            {
                ["id"] = 11,
                ["method"] = "Input.dispatchKeyEvent",
                ["params"] = new JsonObject
                {
                    ["type"] = "char",
                    ["key"] = "KeyA",
                    ["text"] = "a",
                    ["modifiers"] = 0
                }
            };
            await SendCommandAsync(ws, charCommand);

            var keyUpCommand = new JsonObject
            {
                ["id"] = 12,
                ["method"] = "Input.dispatchKeyEvent",
                ["params"] = new JsonObject
                {
                    ["type"] = "keyUp",
                    ["key"] = "KeyA",
                    ["text"] = "",
                    ["modifiers"] = 0
                }
            };
            await SendCommandAsync(ws, keyUpCommand);
            await Task.Delay(100);

            // 5. Test Touch Emulation
            Console.WriteLine("Testing Touch emulation...");
            var touchCommand = new JsonObject
            {
                ["id"] = 20,
                ["method"] = "Input.emulateTouchFromMouseEvent",
                ["params"] = new JsonObject
                {
                    ["type"] = "mousePressed",
                    ["x"] = 200.0,
                    ["y"] = 50.0,
                    ["button"] = "left",
                    ["clickCount"] = 1,
                    ["modifiers"] = 0
                }
            };
            await SendCommandAsync(ws, touchCommand);
            await Task.Delay(100);

            // 6. Test Tap Gesture
            Console.WriteLine("Testing Tap gesture simulation...");
            var tapCommand = new JsonObject
            {
                ["id"] = 30,
                ["method"] = "Input.synthesizeTapGesture",
                ["params"] = new JsonObject
                {
                    ["x"] = 50.0,
                    ["y"] = 125.0,
                    ["tapCount"] = 1,
                    ["duration"] = 50,
                    ["gestureSourceType"] = "mouse"
                }
            };
            await SendCommandAsync(ws, tapCommand);
            await Task.Delay(200);

            // 7. Test Scroll Gesture
            Console.WriteLine("Testing Scroll gesture simulation...");
            var scrollCommand = new JsonObject
            {
                ["id"] = 40,
                ["method"] = "Input.synthesizeScrollGesture",
                ["params"] = new JsonObject
                {
                    ["x"] = 100.0,
                    ["y"] = 300.0,
                    ["xDistance"] = 0.0,
                    ["yDistance"] = -60.0,
                    ["speed"] = 800,
                    ["gestureSourceType"] = "mouse"
                }
            };
            await SendCommandAsync(ws, scrollCommand);
            await Task.Delay(500);

            // 7.5. Test Touch Emulation toggle & Mouse to Touch routing
            Console.WriteLine("Testing Touch Emulation toggle & Mouse-to-Touch routing...");
            
            // Enable touch emulation
            var enableTouchEmulation = new JsonObject
            {
                ["id"] = 45,
                ["method"] = "Emulation.setTouchEmulationEnabled",
                ["params"] = new JsonObject { ["enabled"] = true }
            };
            await SendCommandAsync(ws, enableTouchEmulation);
            await Task.Delay(50);

            // Send a standard mouse pressed event
            var emulatedMousePress = new JsonObject
            {
                ["id"] = 46,
                ["method"] = "Input.dispatchMouseEvent",
                ["params"] = new JsonObject
                {
                    ["type"] = "mousePressed",
                    ["x"] = 200.0,
                    ["y"] = 50.0,
                    ["button"] = "left",
                    ["clickCount"] = 1,
                    ["modifiers"] = 0
                }
            };
            
            // Reset touchPressed to false before verifying
            touchPressed = false;
            await SendCommandAsync(ws, emulatedMousePress);
            await Task.Delay(100);

            // Release mouse
            var emulatedMouseRelease = new JsonObject
            {
                ["id"] = 47,
                ["method"] = "Input.dispatchMouseEvent",
                ["params"] = new JsonObject
                {
                    ["type"] = "mouseReleased",
                    ["x"] = 200.0,
                    ["y"] = 50.0,
                    ["button"] = "left",
                    ["clickCount"] = 1,
                    ["modifiers"] = 0
                }
            };
            await SendCommandAsync(ws, emulatedMouseRelease);
            
            // Disable touch emulation
            var disableTouchEmulation = new JsonObject
            {
                ["id"] = 48,
                ["method"] = "Emulation.setTouchEmulationEnabled",
                ["params"] = new JsonObject { ["enabled"] = false }
            };
            await SendCommandAsync(ws, disableTouchEmulation);
            await Task.Delay(50);
            
            // Verify touchPressed was true during mouse event dispatch
            bool touchEmulationPassed = touchPressed;

            // 7.6. Test Pinch Gesture
            Console.WriteLine("Testing Pinch gesture simulation...");
            var pinchCommand = new JsonObject
            {
                ["id"] = 49,
                ["method"] = "Input.synthesizePinchGesture",
                ["params"] = new JsonObject
                {
                    ["x"] = 250.0,
                    ["y"] = 50.0,
                    ["scaleFactor"] = 2.0,
                    ["relativeSpeed"] = 800,
                    ["gestureSourceType"] = "touch"
                }
            };
            
            touchPressCount = 0;
            await SendCommandAsync(ws, pinchCommand);
            await Task.Delay(500);

            // 8. Assertions & Verification
            Console.WriteLine("Verifying E2E assertions...");
            string? textBoxText = null;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                textBoxText = textBox!.Text;
            });

            Console.WriteLine($"TextBox text: '{textBoxText}'");
            if (textBoxText != "a")
            {
                throw new Exception($"Assertion failed: Expected TextBox text 'a', got '{textBoxText}'");
            }
            Console.WriteLine("-> Keyboard input typing verification PASSED.");

            Console.WriteLine($"Touch pressed: {touchPressed}");
            if (!touchPressed)
            {
                throw new Exception("Assertion failed: Expected touchPressed to be true");
            }
            Console.WriteLine("-> Touch emulation verification PASSED.");

            Console.WriteLine($"Touch emulation toggle passed: {touchEmulationPassed}");
            if (!touchEmulationPassed)
            {
                throw new Exception("Assertion failed: Expected touchEmulationPassed to be true");
            }
            Console.WriteLine("-> Touch emulation toggle verification PASSED.");

            Console.WriteLine($"Touch press count during pinch gesture: {touchPressCount}");
            if (touchPressCount < 2)
            {
                throw new Exception($"Assertion failed: Expected touchPressCount to be >= 2, got {touchPressCount}");
            }
            Console.WriteLine("-> Pinch gesture verification PASSED.");

            Console.WriteLine($"Tapped: {tapped}");
            if (!tapped)
            {
                throw new Exception("Assertion failed: Expected tapped to be true");
            }
            Console.WriteLine("-> Tap gesture verification PASSED.");

            Console.WriteLine($"Scroll offset Y: {scrollDeltaY}");
            if (scrollDeltaY <= 0)
            {
                throw new Exception("Assertion failed: Expected scroll offset Y to be positive");
            }
            Console.WriteLine("-> Scroll gesture verification PASSED.");

            // Clean up
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close E2E test", CancellationToken.None);
            CdpServer.Stop();

            Console.WriteLine("=== ALL TASK-SPECIFIC INPUTS & GESTURES E2E VERIFICATIONS SUCCESSFUL! ===");
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
}
