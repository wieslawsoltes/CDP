using Avalonia;
using System;
using Avalonia.Headless;
using Avalonia.Threading;
using System.Threading;
using ReactiveUI.Avalonia;

namespace CdpRdpApp;

public class AvaloniaHeadlessPlatformOptions : Avalonia.Headless.AvaloniaHeadlessPlatformOptions
{
    public bool UseDotNetSystemFont { get; set; }
}

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (Array.Exists(args, arg => arg.Equals("--headless", StringComparison.OrdinalIgnoreCase)))
        {
            int port = ResolveHeadlessPort(args);

            var builder = AppBuilder.Configure<App>()
                .UseReactiveUI(_ => { })
                .WithInterFont()
                .LogToTrace()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseDotNetSystemFont = true });

            builder.SetupWithoutStarting();

            Avalonia.Diagnostics.Cdp.CdpServer.EnsureInitialized();

            var window = new MainWindow();
            window.Show();

            Avalonia.Diagnostics.Cdp.CdpServer.Start(port);

            Console.WriteLine($"CdpRdpApp listening on http://127.0.0.1:{port}");

            while (true)
            {
                Dispatcher.UIThread.RunJobs();
                Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Thread.Sleep(16);
            }
        }
        else
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }

    public static int ResolveHeadlessPort(string[] args)
    {
        int port = 9224;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) &&
                i + 1 < args.Length &&
                int.TryParse(args[i + 1], out int parsedPort) &&
                parsedPort is >= 1 and <= 65535)
            {
                port = parsedPort;
            }
        }

        return port;
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
