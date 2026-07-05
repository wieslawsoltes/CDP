using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using OpenQA.Selenium.Chrome;

namespace CDP.Automation.Selenium;

public class CdpSeleniumOrchestrator : IAsyncDisposable
{
    private readonly CdpSeleniumOptions _options;
    private Process? _appProcess;

    public ChromeDriver? Driver { get; private set; }

    public CdpSeleniumOrchestrator(CdpSeleniumOptions options)
    {
        _options = options;
    }

    public async Task StartAsync()
    {
        // 1. Launch Target App if requested
        if (_options.StartApp)
        {
            bool isAppPortOpen = IsPortOpen("127.0.0.1", _options.AppCdpPort);
            if (!isAppPortOpen)
            {
                StartAppProcess();
                for (int i = 0; i < 100; i++)
                {
                    if (IsPortOpen("127.0.0.1", _options.AppCdpPort)) break;
                    if (_appProcess != null && _appProcess.HasExited)
                    {
                        throw new Exception($"Target application process exited prematurely. Port {_options.AppCdpPort} never opened.");
                    }
                    await Task.Delay(100);
                }
                await Task.Delay(1500); // settle
            }
        }

        // 2. Connect Selenium Client via ChromeOptions
        var chromeOptions = new ChromeOptions();
        chromeOptions.DebuggerAddress = $"127.0.0.1:{_options.AppCdpPort}";
        if (_options.Headless)
        {
            chromeOptions.AddArgument("--headless");
        }
        chromeOptions.AddArgument("--disable-gpu");
        chromeOptions.AddArgument("--disable-features=DevToolsTabTarget");

        try
        {
            var service = ChromeDriverService.CreateDefaultService();
            service.EnableVerboseLogging = _options.EnableVerboseLogging;
            
            string logPath = _options.ChromeDriverLogPath;
            if (string.IsNullOrEmpty(logPath))
            {
                logPath = Path.Combine(Directory.GetCurrentDirectory(), "chromedriver.log");
            }
            service.LogPath = logPath;

            Driver = new ChromeDriver(service, chromeOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SELENIUM ORCHESTRATOR ERROR] Failed to start ChromeDriver: {ex.Message}");
            throw;
        }
    }

    private void StartAppProcess()
    {
        string dllPath = _options.AppPath;
        if (string.IsNullOrEmpty(dllPath))
        {
            // Auto-detect CdpSampleApp as fallback for testing inside repository
            string baseDir = AppContext.BaseDirectory;
            string config = baseDir.Contains("Release") ? "Release" : "Debug";
            dllPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "samples", "CdpSampleApp", "bin", config, "net10.0", "CdpSampleApp.dll"));
            if (!File.Exists(dllPath))
            {
                dllPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples", "CdpSampleApp", "bin", config, "net10.0", "CdpSampleApp.dll"));
            }
        }

        ProcessStartInfo startInfo;
        if (dllPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{dllPath}\" {(_options.Headless ? "-- --headless" : "")} {_options.AppArguments}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\" {(_options.Headless ? "--headless" : "")} {_options.AppArguments}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        _appProcess = new Process { StartInfo = startInfo };
        _appProcess.Start();

        Task.Run(async () =>
        {
            try
            {
                using var writer = new StreamWriter(new FileStream("cdp-sample-app.log", FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
                while (!_appProcess.HasExited)
                {
                    var line = await _appProcess.StandardOutput.ReadLineAsync();
                    if (line != null)
                    {
                        await writer.WriteLineAsync(line);
                        await writer.FlushAsync();
                    }
                    else
                    {
                        await Task.Delay(50);
                    }
                }
            }
            catch {}
        });
    }

    private static bool IsPortOpen(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200));
            if (!success) return false;
            client.EndConnect(result);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Driver != null)
        {
            try
            {
                Driver.Quit();
                Driver.Dispose();
            }
            catch { }
            Driver = null;
        }

        if (_appProcess != null && !_appProcess.HasExited)
        {
            try
            {
                _appProcess.Kill(entireProcessTree: true);
                _appProcess.WaitForExit(2000);
            }
            catch { }
            if (!_appProcess.HasExited)
            {
                try
                {
                    _appProcess.Kill();
                    _appProcess.WaitForExit(1000);
                }
                catch { }
            }
            _appProcess.Dispose();
            _appProcess = null;
        }

        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}
