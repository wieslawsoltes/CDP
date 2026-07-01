#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CdpInspectorApp.ViewModels;

namespace CdpInspectorApp.Services;

public interface IAppLauncherService
{
    Task AutoLaunchAppAsync(
        ICdpService cdpService, 
        ConnectionViewModel? connection, 
        string autoLaunchPath, 
        string autoLaunchArguments, 
        Action<string> logAction,
        CancellationToken token);
}

public class AppLauncherService : IAppLauncherService
{
    private static readonly System.Collections.Generic.List<Process> _launchedProcesses = new();
    private static readonly object _lock = new();

    public static void TrackProcess(Process process)
    {
        if (process == null) return;
        lock (_lock)
        {
            _launchedProcesses.Add(process);
        }
    }

    public static void KillAllLaunchedProcesses()
    {
        lock (_lock)
        {
            foreach (var proc in _launchedProcesses)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
                catch { }
            }
            _launchedProcesses.Clear();
        }
    }

    public static async Task ShutdownAndDisconnectAsync(ICdpService cdpService)
    {
        if (cdpService == null || !cdpService.IsConnected) return;

        int pid = 0;
        try
        {
            var pidRes = await cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = "System.Diagnostics.Process.GetCurrentProcess().Id",
                ["returnByValue"] = true
            });
            var resultNode = pidRes["result"] as JsonObject;
            if (resultNode != null && resultNode.ContainsKey("value"))
            {
                int.TryParse(resultNode["value"]?.ToString(), out pid);
            }
        }
        catch { }

        try
        {
            _ = cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
            {
                ["expression"] = "Avalonia.Application.Current?.Shutdown()",
                ["returnByValue"] = true
            });
        }
        catch { }

        try
        {
            await cdpService.DisconnectAsync();
        }
        catch { }

        if (pid > 0)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                var startTime = DateTime.UtcNow;
                while (!proc.HasExited && (DateTime.UtcNow - startTime).TotalSeconds < 3)
                {
                    await Task.Delay(100);
                }
                if (!proc.HasExited)
                {
                    proc.Kill();
                }
            }
            catch { }
        }
    }

    public async Task AutoLaunchAppAsync(
        ICdpService cdpService, 
        ConnectionViewModel? connection, 
        string autoLaunchPath, 
        string autoLaunchArguments, 
        Action<string> logAction,
        CancellationToken token)
    {
        if (string.IsNullOrEmpty(autoLaunchPath))
        {
            logAction("Auto Launch: No application path specified. Skipping launch.");
            return;
        }

        bool isOs = connection?.IsOsAutomation ?? false;
        int pid = 0;

        if (cdpService.IsConnected)
        {
            if (!isOs)
            {
                try
                {
                    var pidRes = await cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
                    {
                        ["expression"] = "System.Diagnostics.Process.GetCurrentProcess().Id",
                        ["returnByValue"] = true
                    });
                    var resultNode = pidRes["result"] as JsonObject;
                    if (resultNode != null && resultNode.ContainsKey("value"))
                    {
                        int.TryParse(resultNode["value"]?.ToString(), out pid);
                    }
                }
                catch { }

                try
                {
                    logAction("Auto Launch: Shutting down connected app...");
                    _ = cdpService.SendCommandAsync("Runtime.evaluate", new JsonObject
                    {
                        ["expression"] = "Avalonia.Application.Current?.Shutdown()",
                        ["returnByValue"] = true
                    });
                }
                catch { }
            }
            else
            {
                logAction("Auto Launch: Disconnecting from current OS window target...");
            }

            try
            {
                await cdpService.DisconnectAsync();
            }
            catch { }

            await Task.Delay(500, token);

            if (pid > 0)
            {
                try
                {
                    var proc = Process.GetProcessById(pid);
                    var startTime = DateTime.UtcNow;
                    while (!proc.HasExited && (DateTime.UtcNow - startTime).TotalSeconds < 5)
                    {
                        await Task.Delay(100, token);
                    }
                    if (!proc.HasExited)
                    {
                        logAction($"Auto Launch: Process {pid} still running. Terminating...");
                        proc.Kill();
                        var killStartTime = DateTime.UtcNow;
                        while (!proc.HasExited && (DateTime.UtcNow - killStartTime).TotalSeconds < 5)
                        {
                            await Task.Delay(100, token);
                        }
                    }
                }
                catch { }
            }
        }

        var existingWindowIds = new System.Collections.Generic.HashSet<string>();
        if (isOs)
        {
            try
            {
                var windows = CDP.Automation.OS.OSAutomationService.Instance.GetWindows();
                foreach (var w in windows)
                {
                    existingWindowIds.Add(w.Id);
                }
            }
            catch { }
        }

        logAction($"Auto Launch: Starting process '{autoLaunchPath}' with arguments '{autoLaunchArguments}'...");
        try
        {
            Process? process = null;
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) &&
                (autoLaunchPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase) || autoLaunchPath.EndsWith(".app/", StringComparison.OrdinalIgnoreCase)))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"-a \"{autoLaunchPath}\" --args {autoLaunchArguments}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                process = Process.Start(psi);
            }
            else
            {
                var psi = new ProcessStartInfo
                {
                    FileName = autoLaunchPath,
                    Arguments = autoLaunchArguments,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                process = Process.Start(psi);
            }

            if (process == null)
            {
                throw new Exception("Process failed to start.");
            }
            TrackProcess(process);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to start process: {ex.Message}");
        }

        if (connection == null)
        {
            throw new Exception("ConnectionViewModel reference is missing.");
        }

        int maxRetries = 30;
        bool connected = false;
        var appName = Path.GetFileNameWithoutExtension(autoLaunchPath);

        for (int i = 0; i < maxRetries; i++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (isOs)
                {
                    logAction($"Auto Launch: Scanning for launched application window '{appName}' (attempt {i + 1}/{maxRetries})...");
                    var windows = CDP.Automation.OS.OSAutomationService.Instance.GetWindows();
                    CDP.Automation.OS.OSWindow? targetWindow = null;

                    // 1. Look for a newly opened window matching appName
                    foreach (var w in windows)
                    {
                        if (!existingWindowIds.Contains(w.Id))
                        {
                            if (w.ProcessName.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                                w.Title.Contains(appName, StringComparison.OrdinalIgnoreCase))
                            {
                                targetWindow = w;
                                break;
                            }
                        }
                    }

                    // 2. Fallback to any window matching appName
                    if (targetWindow == null)
                    {
                        foreach (var w in windows)
                        {
                            if (w.ProcessName.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                                w.Title.Contains(appName, StringComparison.OrdinalIgnoreCase))
                            {
                                targetWindow = w;
                                break;
                            }
                        }
                    }

                    // 3. Fallback to any new window
                    if (targetWindow == null)
                    {
                        foreach (var w in windows)
                        {
                            if (!existingWindowIds.Contains(w.Id))
                            {
                                targetWindow = w;
                                break;
                            }
                        }
                    }

                    if (targetWindow != null)
                    {
                        logAction($"Auto Launch: Found window '{targetWindow.Title}' (ID: {targetWindow.Id}). Connecting...");
                        await connection.RefreshTargetsAsync();
                        TargetItem? foundTarget = null;
                        foreach (var t in connection.Targets)
                        {
                            if (t.Id == targetWindow.Id)
                            {
                                foundTarget = t;
                                break;
                            }
                        }

                        if (foundTarget == null)
                        {
                            foundTarget = new TargetItem(targetWindow.Title, $"os://{targetWindow.Id}", targetWindow.Id);
                            connection.Targets.Add(foundTarget);
                        }

                        connection.SelectedTarget = foundTarget;
                        await connection.ConnectAsync(bypassAutoLaunch: true);

                        if (cdpService.IsConnected)
                        {
                            connected = true;
                            logAction("Auto Launch: Successfully connected to OS window target.");
                            break;
                        }
                    }
                }
                else
                {
                    logAction($"Auto Launch: Attempting to connect to host '{connection.HostAddress}' (attempt {i + 1}/{maxRetries})...");
                    await connection.RefreshTargetsAsync();
                    if (connection.Targets.Count > 0)
                    {
                        await connection.ConnectAsync(bypassAutoLaunch: true);
                        if (cdpService.IsConnected)
                        {
                            connected = true;
                            logAction("Auto Launch: Successfully connected to application.");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logAction($"Auto Launch warning: {ex.Message}");
            }
            await Task.Delay(1000, token);
        }

        if (!connected)
        {
            throw new Exception("Failed to auto-connect to application after launch.");
        }
    }
}
