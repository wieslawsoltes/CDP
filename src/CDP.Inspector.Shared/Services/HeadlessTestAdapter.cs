using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Cdp;
using CdpInspectorApp.ViewModels;
using CdpInspectorApp.Models;

namespace CdpInspectorApp.Services;

public class HeadlessTestAdapter
{
    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Runs a Test Studio YAML script on a given window instance in headless mode.
    /// </summary>
    public async Task RunTestAsync(Window window, string yamlContent, bool isYamlContent = true, int timeoutMs = 30000)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        if (string.IsNullOrEmpty(yamlContent)) throw new ArgumentException("YAML content or file path cannot be empty.", nameof(yamlContent));

        string yaml = yamlContent;
        if (!isYamlContent)
        {
            if (!File.Exists(yamlContent))
            {
                throw new FileNotFoundException("YAML test script file not found.", yamlContent);
            }
            yaml = await File.ReadAllTextAsync(yamlContent);
        }

        // Ensure window is shown
        if (!window.IsVisible)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                window.Show();
                window.Activate();
            });
            // Give window a brief moment to layout/measure
            await Task.Delay(200);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                window.Measure(new Avalonia.Size(window.Width, window.Height));
                window.Arrange(new Avalonia.Rect(0, 0, window.Width, window.Height));
            });
        }

        int port = GetFreeTcpPort();
        CdpServer.Start(port);
        
        string targetId = "";
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            targetId = CdpServer.Register(window, window.Title ?? "Headless Target");
        });

        var cdpService = new CdpService();
        var mainVm = new MainWindowViewModel(cdpService);
        mainVm.Connection.HostAddress = $"http://127.0.0.1:{port}";

        try
        {
            // Connect to local CdpServer
            var targets = await cdpService.GetTargetsAsync($"http://127.0.0.1:{port}");
            var target = targets.FirstOrDefault(t => t.Id == targetId);
            if (target == null)
            {
                throw new Exception($"Could not find target window with ID '{targetId}' registered in CdpServer on port {port}.");
            }
            await cdpService.ConnectAsync($"http://127.0.0.1:{port}", target);

            var testStudio = mainVm.Recorder.TestStudio;
            testStudio.YamlCode = yaml;
            
            // Apply YAML to load steps
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                testStudio.ApplyYamlCommand.Execute(null);
            });

            if (testStudio.Steps.Count == 0)
            {
                throw new Exception("No test steps were parsed from the YAML script.");
            }

            // Start playing the test studio
            await testStudio.PlayAsync();

            // Wait for execution to finish
            int elapsed = 0;
            while (testStudio.IsExecuting && elapsed < timeoutMs)
            {
                await Task.Delay(100);
                elapsed += 100;
            }

            if (testStudio.IsExecuting)
            {
                throw new TimeoutException($"Headless test execution timed out after {timeoutMs / 1000} seconds.");
            }

            // Check if any step failed
            var failedStep = testStudio.Steps.FirstOrDefault(s => s.Status == StepStatus.Failed);
            if (failedStep != null)
            {
                throw new Exception($"Test step failed: '{failedStep.ActionDisplay}' on selector '{failedStep.Selector}'. Error: {failedStep.ErrorMessage}");
            }
        }
        finally
        {
            // Cleanup
            try { await cdpService.DisconnectAsync(); } catch { }
            try 
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CdpServer.Unregister(window);
                });
            }
            catch { }
            try { CdpServer.Stop(); } catch { }
        }
    }

    /// <summary>
    /// Runs a Test Studio YAML script on a new window instance of type TWindow in headless mode.
    /// </summary>
    public async Task RunTestAsync<TWindow>(string yamlFilePath, int timeoutMs = 30000) where TWindow : Window, new()
    {
        TWindow? window = null;
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            window = new TWindow();
        });
        
        if (window == null) throw new Exception($"Failed to instantiate window of type {typeof(TWindow).Name}.");

        try
        {
            await RunTestAsync(window, yamlFilePath, isYamlContent: false, timeoutMs: timeoutMs);
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                window.Close();
            });
        }
    }
}
