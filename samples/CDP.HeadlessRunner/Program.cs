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
        var app = AppBuilder.Configure<CdpSampleApp.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();

        // Instantiate and show the main window to register it as a target
        var window = new CdpSampleApp.MainWindow();
        window.Show();

        // Boot CdpServer on port 9222
        CdpServer.Start(port: 9222);
        
        Console.WriteLine("Headless test harness ready and listening on http://127.0.0.1:9222");
        
        // Keep process alive while tests are running
        Thread.Sleep(Timeout.Infinite);
    }
}
