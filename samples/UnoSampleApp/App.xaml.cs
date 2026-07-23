using System;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using Uno.Resizetizer;

namespace UnoSampleApp;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Start CDP diagnostics server on port 9225
        WinUI.Diagnostics.Cdp.CdpServer.EnsureInitialized();
        WinUI.Diagnostics.Cdp.CdpServer.Start(9225);
        Console.WriteLine("Uno Application listening on CDP port: 9225");

        MainWindow = new MainWindow();
        WinUI.Diagnostics.Cdp.CdpServer.GetOrCreateTarget(MainWindow);

        MainWindow.Activate();

        try
        {
            MainWindow.AppWindow?.Resize(new Windows.Graphics.SizeInt32 { Width = 550, Height = 520 });
        }
        catch
        {
            // Ignore platform specific window size exception
        }
    }

    /// <summary>
    /// Invoked when Navigation to a certain page fails
    /// </summary>
    /// <param name="sender">The Frame which failed navigation</param>
    /// <param name="e">Details about the navigation failure</param>
    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }

    /// <summary>
    /// Configures global Uno Platform logging
    /// </summary>
    public static void InitializeLogging()
    {
#if DEBUG
        var factory = LoggerFactory.Create(builder =>
        {
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__
            builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());
            builder.AddConsole();
#else
            builder.AddConsole();
#endif
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);
        });

        Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;
#endif
    }
}
