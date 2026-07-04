using System;
using System.Threading;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Diagnostics.Cdp;
using Avalonia.Threading;

namespace CDP.HeadlessRunner;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("Headless test harness starting on http://127.0.0.1:9222");
        
        var builder = AppBuilder.Configure<CdpSampleApp.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());

        builder.SetupWithoutStarting();

        var window = new CdpSampleApp.MainWindow();
        window.Show();

        CdpServer.Start(port: 9222);

        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(16);
        }
    }
}
