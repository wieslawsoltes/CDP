using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Diagnostics.Cdp;

namespace CdpSampleApp;

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
            desktop.MainWindow = new MainWindow();
            
            // Attach the in-process CDP Inspector tool (F12)
            desktop.MainWindow.AttachCdpInspector(9222);
        }

        base.OnFrameworkInitializationCompleted();
    }
}