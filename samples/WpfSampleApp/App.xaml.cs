using System;
using System.Windows;

namespace WpfSampleApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        int port = 9224;
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                int.TryParse(e.Args[i + 1], out port);
            }
        }

        // Start the WPF CDP diagnostics server
        Wpf.Diagnostics.Cdp.CdpServer.EnsureInitialized();
        Wpf.Diagnostics.Cdp.CdpServer.Start(port);

        Console.WriteLine($"WPF Application listening on CDP port: {port}");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Wpf.Diagnostics.Cdp.CdpServer.Stop();
        base.OnExit(e);
    }
}
