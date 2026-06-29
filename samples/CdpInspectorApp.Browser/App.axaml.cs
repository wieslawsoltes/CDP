using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.Browser;

public partial class App : Application
{
    public static string? StartupUrl { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var factory = LoggerFactory.Create(builder =>
        {
#pragma warning disable CA1416
            builder.AddConsole();
#pragma warning restore CA1416
            builder.SetMinimumLevel(LogLevel.Information); // default non-verbose logging
        });
        CdpLogging.LoggerFactory = factory;

        System.Console.WriteLine("[BrowserApp] OnFrameworkInitializationCompleted started.");
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            System.Console.WriteLine("[BrowserApp] ApplicationLifetime is ISingleViewApplicationLifetime. Instantiating MainView...");
            try
            {
                var mainView = new CdpInspectorApp.Views.MainView();
                singleViewPlatform.MainView = mainView;
                System.Console.WriteLine("[BrowserApp] MainView instantiated successfully.");

                if (mainView.DataContext is CdpInspectorApp.ViewModels.MainWindowViewModel vm && !string.IsNullOrEmpty(StartupUrl))
                {
                    string? ws = GetQueryParameter(StartupUrl, "ws");
                    if (!string.IsNullOrEmpty(ws))
                    {
                        System.Console.WriteLine($"[BrowserApp] Found direct WebSocket URL in startup query: {ws}");
                        vm.Connection.HostAddress = ws;
                    }
                    else
                    {
                        string? host = GetQueryParameter(StartupUrl, "host");
                        if (!string.IsNullOrEmpty(host))
                        {
                            System.Console.WriteLine($"[BrowserApp] Found host URL in startup query: {host}");
                            vm.Connection.HostAddress = host;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[BrowserApp] CRITICAL: Failed to instantiate MainView: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
        else
        {
            System.Console.WriteLine($"[BrowserApp] Warning: ApplicationLifetime is {ApplicationLifetime?.GetType().Name ?? "null"}, not ISingleViewApplicationLifetime.");
        }

        base.OnFrameworkInitializationCompleted();
        System.Console.WriteLine("[BrowserApp] OnFrameworkInitializationCompleted finished.");
    }

    private static string? GetQueryParameter(string url, string name)
    {
        try
        {
            int qIdx = url.IndexOf('?');
            if (qIdx == -1) return null;
            string queryString = url.Substring(qIdx + 1);
            string[] parts = queryString.Split('&');
            foreach (var part in parts)
            {
                string[] kv = part.Split('=');
                if (kv.Length == 2 && string.Equals(kv[0], name, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[BrowserApp] Error parsing query parameter: {ex.Message}");
        }
        return null;
    }
}
