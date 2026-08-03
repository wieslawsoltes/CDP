using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Chrome.DevTools.Protocol.Inspector;

namespace Chrome.DevTools.Protocol.Inspector.Tests;

public sealed class ChromeV8InspectorIntegrationTests
{
    [Fact(Timeout = 45_000)]
    public async Task RealChromeSupportsV8DebuggingAndScreenshotSession()
    {
        var chromePath = FindChrome();
        if (chromePath is null) return;

        var port = GetAvailablePort();
        var profileDirectory = Path.Combine(Path.GetTempPath(), $"cdp-chrome-v8-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profileDirectory);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = chromePath,
            ArgumentList =
            {
                "--headless=new",
                $"--remote-debugging-port={port}",
                $"--user-data-dir={profileDirectory}",
                "--no-first-run",
                "--no-default-browser-check",
                "--disable-background-networking",
                "about:blank"
            },
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        });
        Assert.NotNull(process);

        try
        {
            var target = (await WaitForPageTargetAsync(new Uri($"http://127.0.0.1:{port}"))).First();
            await using var inspector = new V8InspectorClient();
            var pauses = Channel.CreateUnbounded<JsonObject>();
            inspector.EventReceived += (_, e) =>
            {
                if (e.Method == "Debugger.paused") pauses.Writer.TryWrite(e.Params);
            };

            await inspector.ConnectAsync(new Uri(target.WebSocketDebuggerUrl));
            Assert.Contains("Runtime", inspector.SupportedDomains);
            Assert.Contains("Debugger", inspector.SupportedDomains);
            Assert.Contains("Profiler", inspector.SupportedDomains);
            Assert.Contains("HeapProfiler", inspector.SupportedDomains);

            await inspector.SendCommandAsync("Page.enable");
            await inspector.SendCommandAsync("Runtime.enable");
            await inspector.SendCommandAsync("Debugger.enable");
            await inspector.SendCommandAsync("Profiler.enable");
            await inspector.SendCommandAsync("HeapProfiler.enable");
            await inspector.SendCommandAsync("Page.navigate", new JsonObject
            {
                ["url"] = "data:text/html,<style>body{font-family:system-ui;background:%23292a2d;color:%23e8eaed;padding:48px}code{color:%238ab4f8}</style><h1>Chrome V8 debugging validated</h1><p>Runtime &middot; Debugger &middot; Profiler &middot; HeapProfiler</p><code>cdp://chrome-v8-test.js</code>"
            });

            await inspector.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = "function chromeCompute(a, b) {\n  const sum = a + b;\n  return sum * 2;\n}\n//# sourceURL=cdp://chrome-v8-test.js"
            });
            var breakpoint = await inspector.SendCommandAsync("Debugger.setBreakpointByUrl", new JsonObject
            {
                ["url"] = "cdp://chrome-v8-test.js",
                ["lineNumber"] = 2,
                ["columnNumber"] = 0
            });
            Assert.False(string.IsNullOrWhiteSpace(breakpoint["breakpointId"]?.GetValue<string>()));

            await inspector.SendCommandAsync("Profiler.start");
            var run = inspector.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = "chromeCompute(4, 5)",
                ["returnByValue"] = true
            });
            var paused = await pauses.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            var callFrame = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(paused["callFrames"])[0]);
            Assert.Equal("chromeCompute", callFrame["functionName"]?.GetValue<string>());
            var evaluation = await inspector.SendCommandAsync("Debugger.evaluateOnCallFrame", new JsonObject
            {
                ["callFrameId"] = callFrame["callFrameId"]!.GetValue<string>(),
                ["expression"] = "sum",
                ["returnByValue"] = true
            });
            Assert.Equal(9, evaluation["result"]?["value"]?.GetValue<int>());

            await inspector.SendCommandAsync("Debugger.resume");
            Assert.Equal(18, (await run)["result"]?["value"]?.GetValue<int>());
            Assert.NotEmpty(Assert.IsType<JsonArray>((await inspector.SendCommandAsync("Profiler.stop"))["profile"]?["nodes"]));
            Assert.True((await inspector.SendCommandAsync("Runtime.getHeapUsage"))["totalSize"]?.GetValue<double>() > 0);

            var screenshot = await inspector.SendCommandAsync("Page.captureScreenshot", new JsonObject { ["format"] = "png" });
            var screenshotPath = Environment.GetEnvironmentVariable("CDP_V8_SCREENSHOT_PATH");
            if (!string.IsNullOrWhiteSpace(screenshotPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath) ?? ".");
                await File.WriteAllBytesAsync(screenshotPath, Convert.FromBase64String(screenshot["data"]!.GetValue<string>()));
            }
        }
        finally
        {
            if (!process!.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            try { Directory.Delete(profileDirectory, recursive: true); } catch (IOException) { }
        }
    }

    private static string? FindChrome()
    {
        var candidates = OperatingSystem.IsMacOS()
            ? new[] { "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" }
            : OperatingSystem.IsWindows()
                ? new[] { @"C:\Program Files\Google\Chrome\Application\chrome.exe", @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe" }
                : new[] { "/usr/bin/google-chrome", "/usr/bin/google-chrome-stable", "/usr/bin/chromium", "/usr/bin/chromium-browser" };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<IReadOnlyList<V8InspectorTarget>> WaitForPageTargetAsync(Uri endpoint)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                var targets = await V8InspectorClient.DiscoverTargetsAsync(endpoint);
                var pages = targets.Where(target => target.Type == "page").ToArray();
                if (pages.Length > 0) return pages;
            }
            catch (HttpRequestException)
            {
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("Chrome remote-debugging endpoint did not become ready.");
    }
}
