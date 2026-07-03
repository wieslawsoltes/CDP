using Avalonia;
using System;
using Avalonia.Headless;

namespace CdpSampleApp;

public class AvaloniaHeadlessPlatformOptions : Avalonia.Headless.AvaloniaHeadlessPlatformOptions
{
    public bool UseDotNetSystemFont { get; set; }
}

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
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
