using System;
using System.Threading;
using Microsoft.UI.Xaml;

namespace UnoSampleApp;

public static class Program
{
    public static void Main(string[] args)
    {
        int port = 9225;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                int.TryParse(args[i + 1], out port);
            }
        }

        // Start Uno Platform CDP diagnostics server
        WinUI.Diagnostics.Cdp.CdpServer.EnsureInitialized();
        WinUI.Diagnostics.Cdp.CdpServer.Start(port);
        Console.WriteLine($"Uno Application listening on CDP port: {port}");

        try
        {
            var window = new MainWindow();
            window.Activate();
            WinUI.Diagnostics.Cdp.CdpServer.GetOrCreateTarget(window);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Window initialization note: {ex.Message}");
        }

        Console.WriteLine("Uno Application active and listening for CDP client connections.");
        while (true)
        {
            Thread.Sleep(100);
        }
    }
}
