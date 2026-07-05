using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace CDP.Automation.Appium;

public class CdpAppiumOrchestrator : IAsyncDisposable
{
    private readonly CdpAppiumOptions _options;
    private Process? _appProcess;
    private Process? _driverProcess;

    public AndroidDriver? Driver { get; private set; }

    public CdpAppiumOrchestrator(CdpAppiumOptions options)
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

        // 2. Launch Custom Appium Driver if requested
        if (_options.StartDriver)
        {
            bool isDriverPortOpen = IsPortOpen("127.0.0.1", _options.AppiumPort);
            if (!isDriverPortOpen)
            {
                StartDriverProcess();
                for (int i = 0; i < 100; i++)
                {
                    if (IsPortOpen("127.0.0.1", _options.AppiumPort)) break;
                    if (_driverProcess != null && _driverProcess.HasExited)
                    {
                        throw new Exception($"Appium Driver process exited prematurely. Port {_options.AppiumPort} never opened.");
                    }
                    await Task.Delay(100);
                }
            }
        }

        // 3. Connect Appium Client
        var appiumOptions = new AppiumOptions();
        appiumOptions.PlatformName = _options.PlatformName;
        appiumOptions.AutomationName = _options.AutomationName;

        try
        {
            Driver = new AndroidDriver(new Uri(_options.AppiumServerUri), appiumOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[APPIUM ORCHESTRATOR ERROR] Failed to start AndroidDriver: {ex.Message}");
            throw;
        }
    }

    private void StartAppProcess()
    {
        string dllPath = _options.AppPath;
        if (string.IsNullOrEmpty(dllPath))
        {
            // Auto-detect CdpSampleApp as fallback for testing inside the repository
            string baseDir = AppContext.BaseDirectory;
            string config = baseDir.Contains("Release") ? "Release" : "Debug";
            dllPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "samples", "CdpSampleApp", "bin", config, "net10.0", "CdpSampleApp.dll"));
            if (!File.Exists(dllPath))
            {
                dllPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples", "CdpSampleApp", "bin", config, "net10.0", "CdpSampleApp.dll"));
            }
        }

        if (!File.Exists(dllPath))
        {
            dllPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "CdpSampleApp", "CdpSampleApp.csproj"));
            if (!File.Exists(dllPath))
            {
                dllPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples", "CdpSampleApp", "CdpSampleApp.csproj"));
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
                using var writer = new StreamWriter(new FileStream("cdp-sample-app-appium.log", FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
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

    private void StartDriverProcess()
    {
        string scriptPath = _options.AppiumDriverScriptPath;
        if (string.IsNullOrEmpty(scriptPath))
        {
            scriptPath = Path.Combine(AppContext.BaseDirectory, "appium-cdp-driver.js");
        }
        if (!File.Exists(scriptPath))
        {
            // Fallback for tests inside the repository
            scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "appium-cdp-driver.js"));
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "scripts", "appium-cdp-driver.js"));
            }
        }

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Appium CDP driver script not found. Copy 'appium-cdp-driver.js' to execution directory or specify it in options.", scriptPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{scriptPath}\" --port {_options.AppiumPort} --cdp-host http://127.0.0.1:{_options.AppCdpPort}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _driverProcess = new Process { StartInfo = startInfo };
        _driverProcess.Start();

        Task.Run(async () =>
        {
            try
            {
                using var writer = new StreamWriter(new FileStream("cdp-appium-driver-run.log", FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
                while (!_driverProcess.HasExited)
                {
                    var line = await _driverProcess.StandardOutput.ReadLineAsync();
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

        if (_driverProcess != null && !_driverProcess.HasExited)
        {
            try { _driverProcess.Kill(entireProcessTree: true); } catch { }
            _driverProcess.Dispose();
        }

        if (_appProcess != null && !_appProcess.HasExited)
        {
            try { _appProcess.Kill(entireProcessTree: true); } catch { }
            _appProcess.Dispose();
        }

        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}
