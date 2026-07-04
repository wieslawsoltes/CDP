using System;
using System.Threading;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Diagnostics.Cdp;

namespace CDP.HeadlessRunner;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("Headless test harness starting on http://127.0.0.1:9222");
        
        AppBuilder.Configure<CdpSampleApp.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .StartWithClassicDesktopLifetime(args);
    }
}
