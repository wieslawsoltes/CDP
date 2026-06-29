using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Diagnostics.Cdp;

namespace CdpInspectorApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            CdpServer.Start(9223);
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
