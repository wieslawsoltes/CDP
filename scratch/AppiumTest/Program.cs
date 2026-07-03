using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Mac;

namespace AppiumTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Appium Hybrid CDP Validation ===");

        // 1. Spawning the target application (CdpSampleApp) headlessly in the background
        Console.WriteLine("Spawning CdpSampleApp headlessly on port 9222...");
        var targetProc = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project samples/CdpSampleApp/CdpSampleApp.csproj -- --headless",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (targetProc == null)
        {
            Console.WriteLine("Failed to start CdpSampleApp process.");
            return;
        }

        // Wait a few seconds to let the CDP server spin up
        await Task.Delay(4000);

        try
        {
            // 2. Establishing Appium Mac2Driver session targeting Finder as the base app context
            Console.WriteLine("Connecting to Appium Server on http://127.0.0.1:4723/...");
            var options = new AppiumOptions();
            options.PlatformName = "Mac";
            options.AutomationName = "Mac2";
            options.AddAdditionalAppiumOption("bundleId", "com.apple.finder");

            using var driver = new MacDriver(new Uri("http://127.0.0.1:4723"), options);
            Console.WriteLine("Successfully created Appium session.");

            // Find all active Mac UI elements/windows to validate Appium native automation works
            var windows = driver.FindElements(By.XPath("//XCUIElementTypeWindow"));
            Console.WriteLine($"Appium discovered {windows.Count} active window elements on macOS desktop.");

            // 3. Launching Secondary CDP connection to CdpSampleApp to verify hybrid state
            Console.WriteLine("Connecting to secondary CDP WebSocket at http://127.0.0.1:9222/json...");
            using var httpClient = new HttpClient();
            var responseStr = await httpClient.GetStringAsync("http://127.0.0.1:9222/json");
            var targets = JsonNode.Parse(responseStr)?.AsArray();
            if (targets == null || targets.Count == 0)
            {
                throw new Exception("No active CDP targets discovered.");
            }

            var wsUrl = targets[0]?["webSocketDebuggerUrl"]?.ToString();
            Console.WriteLine($"Discovered WebSocket URL: {wsUrl}");

            if (wsUrl != null)
            {
                using var ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
                Console.WriteLine("Connected to CDP WebSocket.");

                // Send Runtime.evaluate payload to verify target Jint state
                var evalJson = new JsonObject
                {
                    ["id"] = 42,
                    ["method"] = "Runtime.evaluate",
                    ["params"] = new JsonObject
                    {
                        ["expression"] = "Window.Title",
                        ["returnByValue"] = true
                    }
                };

                var payloadBytes = Encoding.UTF8.GetBytes(evalJson.ToString());
                await ws.SendAsync(new ArraySegment<byte>(payloadBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                var buffer = new byte[8192];
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var responseText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"CDP Response: {responseText}");

                var resultNode = JsonNode.Parse(responseText);
                var evalResultValue = resultNode?["result"]?["result"]?["value"]?.ToString();
                Console.WriteLine($"Target Window Title resolved over Jint: '{evalResultValue}'");
                
                if (evalResultValue == "Avalonia CDP Inspector Sample")
                {
                    Console.WriteLine("Hybrid verification SUCCESSFUL!");
                }
                else
                {
                    Console.WriteLine("Hybrid verification FAILED: Unexpected evaluation result.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Validation error: {ex.Message}");
            Console.WriteLine("Ensure that the Appium server is running locally on port 4723: 'appium'");
        }
        finally
        {
            // Terminate target app process
            try
            {
                if (!targetProc.HasExited)
                {
                    targetProc.Kill();
                }
            }
            catch { }
        }
    }
}
