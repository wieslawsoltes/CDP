using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Diagnostics.Cdp;

namespace WindowsRdpApp;

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
            var mainWindowVM = new ViewModels.MainWindowViewModel();
            desktop.MainWindow = new Views.MainWindow
            {
                DataContext = mainWindowVM
            };

            int port = 9225;
            var args = desktop.Args ?? Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int parsedPort))
                    {
                        port = parsedPort;
                    }
                }
            }

            mainWindowVM.CdpPort = port;
            CdpServer.Start(port);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
