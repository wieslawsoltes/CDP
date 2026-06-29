using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Diagnostics.Cdp;
using Microsoft.Extensions.Logging;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var factory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information); // default non-verbose logging
        });
        CdpLogging.LoggerFactory = factory;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            Avalonia.Diagnostics.Cdp.CdpServer.Start(9223);
            desktop.Exit += (sender, args) =>
            {
                try
                {
                    CdpInspectorApp.Services.AppLauncherService.KillAllLaunchedProcesses();
                }
                catch { }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
