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

            var args = desktop.Args ?? Environment.GetCommandLineArgs();
            int port = Program.ParsePort(args);

            mainWindowVM.CdpPort = port;
            CdpServer.Start(port);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
