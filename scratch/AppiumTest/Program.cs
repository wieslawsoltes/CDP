using System;
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
        Console.WriteLine("=== Appium Hybrid Interactive non-headless E2E Validation ===");

        try
        {
            // 1. Initializing Appium session targeting CdpSampleApp binary directly
            Console.WriteLine("Connecting to Appium Server on http://127.0.0.1:4723/...");
            var options = new AppiumOptions();
            options.PlatformName = "Mac";
            options.AutomationName = "Mac2";
            
            // Pass the absolute path to the compiled CdpSampleApp macOS executable binary
            options.App = "/Users/wieslawsoltes/GitHub/CDP/scratch/CdpSampleApp.app";

            using var driver = new MacDriver(new Uri("http://127.0.0.1:4723"), options);
            Console.WriteLine("Successfully created Appium session and launched CdpSampleApp visual window.");

            // Wait a few seconds to let the window draw and the CDP port open
            await Task.Delay(4000);
            // Print page source to debug element hierarchy
            Console.WriteLine("--- Appium Page Source XML ---");
            Console.WriteLine(driver.PageSource);
            Console.WriteLine("------------------------------");
            // 2. Locating and Clicking the 'Click Me' Button natively via Appium Mac2Driver
            Console.WriteLine("Locating 'Click Me' Button natively...");
            // We search directly in the driver context since the session is attached to CdpSampleApp process
            var clickBtn = driver.FindElement(By.XPath("//XCUIElementTypeButton[contains(@title, 'Click Me') or contains(@value, 'Click Me') or contains(@name, 'Click Me') or contains(@label, 'Click Me') or @id='btnClickMe']"));
            Console.WriteLine("Clicking button natively...");
            clickBtn.Click();
            await Task.Delay(1000);

            // 3. Locating and Typing into the TextBox natively via Appium Mac2Driver
            Console.WriteLine("Locating TextBox element natively...");
            var textBox = driver.FindElement(By.XPath("//XCUIElementTypeTextField"));
            Console.WriteLine("Typing text natively...");
            textBox.SendKeys("Appium Interactive!");
            await Task.Delay(1000);

            // 4. Connect to CDP to assert that native interactions updated Jint backend states
            Console.WriteLine("Connecting to concurrent CDP WebSocket to verify bindings...");
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

                // Assert click count state updated to "Clicked 1 times!"
                var clickEval = new JsonObject
                {
                    ["id"] = 101,
                    ["method"] = "Runtime.evaluate",
                    ["params"] = new JsonObject
                    {
                        ["expression"] = "document.querySelector('#txtStatus').textContent",
                        ["returnByValue"] = true
                    }
                };

                await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(clickEval.ToString())), WebSocketMessageType.Text, true, CancellationToken.None);
                var buffer = new byte[8192];
                var clickRes = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var clickResText = Encoding.UTF8.GetString(buffer, 0, clickRes.Count);
                Console.WriteLine($"Click Status CDP: {clickResText}");

                // Assert text binding value matches typed text
                var textEval = new JsonObject
                {
                    ["id"] = 102,
                    ["method"] = "Runtime.evaluate",
                    ["params"] = new JsonObject
                    {
                        ["expression"] = "document.querySelector('#txtInput').value",
                        ["returnByValue"] = true
                    }
                };

                await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(textEval.ToString())), WebSocketMessageType.Text, true, CancellationToken.None);
                var textRes = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var textResText = Encoding.UTF8.GetString(buffer, 0, textRes.Count);
                Console.WriteLine($"TextBox Value CDP: {textResText}");

                var clickVal = JsonNode.Parse(clickResText)?["result"]?["result"]?["value"]?.ToString();
                var textVal = JsonNode.Parse(textResText)?["result"]?["result"]?["value"]?.ToString();

                if (clickVal == "Clicked 1 times!" && textVal == "Appium Interactive!")
                {
                    Console.WriteLine("=== INTERACTIVE HYBRID VERIFICATION SUCCESSFUL! ===");
                }
                else
                {
                    Console.WriteLine("=== INTERACTIVE HYBRID VERIFICATION FAILED ===");
                    Console.WriteLine($"Expected status 'Clicked 1 times!', got '{clickVal}'");
                    Console.WriteLine($"Expected input 'Appium Interactive!', got '{textVal}'");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Verification failed with error: {ex.Message}");
        }
    }
}
