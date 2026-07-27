using System;
using System.Threading;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using ReactiveUI.Avalonia;

namespace WindowsRdpApp;

public class AvaloniaHeadlessPlatformOptions : Avalonia.Headless.AvaloniaHeadlessPlatformOptions
{
    public bool UseDotNetSystemFont { get; set; }
}

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (Array.Exists(args, arg => arg.Equals("--headless", StringComparison.OrdinalIgnoreCase)))
        {
            int port = 9225;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out port);
                }
            }

            var builder = BuildAvaloniaApp()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseDotNetSystemFont = true });

            builder.SetupWithoutStarting();

            Avalonia.Diagnostics.Cdp.CdpServer.EnsureInitialized();

            var vm = new ViewModels.MainWindowViewModel();
            vm.CdpPort = port;
            var window = new Views.MainWindow
            {
                DataContext = vm
            };
            window.Show();

            Avalonia.Diagnostics.Cdp.CdpServer.Start(port);

            Console.WriteLine($"WindowsRdpApp listening on http://127.0.0.1:{port}");

            while (true)
            {
                Dispatcher.UIThread.RunJobs();
                Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Thread.Sleep(16);
            }
        }
        else
        {
            int port = 9225;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out port);
                }
            }

            Avalonia.Diagnostics.Cdp.CdpServer.EnsureInitialized();
            Avalonia.Diagnostics.Cdp.CdpServer.Start(port);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UseReactiveUI(_ => { })
            .WithInterFont()
            .LogToTrace();

        if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg.Equals("--headless", StringComparison.OrdinalIgnoreCase)))
        {
            builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseDotNetSystemFont = true });
        }
        else
        {
            builder.UsePlatformDetect();
        }

        return builder;
    }
}
